using Microsoft.AspNetCore.Mvc;
using WorkflowDesigner.Api.Models;
using WorkflowDesigner.Api.Services;
using System.Runtime.CompilerServices;

namespace WorkflowDesigner.Api.Controllers;

/// <summary>
/// 工作流管理控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class WorkflowsController : ControllerBase
{
    private readonly IWorkflowService _workflowService;
    private readonly ILogger<WorkflowsController> _logger;

    public WorkflowsController(IWorkflowService workflowService, ILogger<WorkflowsController> logger)
    {
        _workflowService = workflowService;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有工作流
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<WorkflowDto>>> GetAll()
    {
        try
        {
            var workflows = await _workflowService.GetAllWorkflowsAsync();
            
            // 转换为 DTO，列表不包含完整数据
            var dtos = workflows.Select(w => WorkflowDto.FromEntity(w, includeFullData: false)).ToList();
            
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all workflows");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// 根据ID获取工作流
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<WorkflowDto>> GetById(string id)
    {
        try
        {
            var workflow = await _workflowService.GetWorkflowByIdAsync(id);
            if (workflow == null)
            {
                return NotFound();
            }
            
            // 转换为 DTO，详情包含完整数据
            var dto = WorkflowDto.FromEntity(workflow, includeFullData: true);
            
            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting workflow {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// 创建工作流
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<WorkflowDefinition>> Create([FromBody] WorkflowDefinition workflow)
    {
        try
        {
            var created = await _workflowService.CreateWorkflowAsync(workflow);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating workflow");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// 更新工作流
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<WorkflowDefinition>> Update(string id, [FromBody] WorkflowDefinition workflow)
    {
        try
        {
            var updated = await _workflowService.UpdateWorkflowAsync(id, workflow);
            if (updated == null)
            {
                return NotFound();
            }
            return Ok(updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating workflow {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// 删除工作流
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id)
    {
        try
        {
            var result = await _workflowService.DeleteWorkflowAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting workflow {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// 执行工作流（旧方法 - 手动执行）
    /// </summary>
    [HttpPost("{id}/execute")]
    public async Task<ActionResult<ExecutionLog>> Execute(string id, [FromBody] Dictionary<string, object> parameters)
    {
        try
        {
            var executionLog = await _workflowService.ExecuteWorkflowAsync(id, parameters);
            return Ok(executionLog);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Workflow {Id} not found", id);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing workflow {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// 使用 Agent Framework 执行工作流（流式返回事件）
    /// </summary>
    [HttpPost("{id}/execute-framework")]
    public async Task ExecuteWithFramework(
        string id, 
        [FromBody] ExecuteFrameworkRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting framework execution for workflow {Id}", id);
        
        // 设置 SSE 响应头
        Response.ContentType = "text/event-stream";
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        
        try
        {
            await foreach (var evt in _workflowService.ExecuteWorkflowWithFrameworkAsync(
                id, 
                request.UserInput ?? "Hello", 
                cancellationToken))
            {
                // 序列化为 JSON
                var json = System.Text.Json.JsonSerializer.Serialize(evt, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                });
                
                // 写入 SSE 格式: data: {json}\n\n
                await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
                
                _logger.LogDebug("Sent SSE event: {Type}", evt.Type);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during workflow execution");
            
            // 发送错误事件
            var errorEvent = new ExecutionEvent
            {
                Type = ExecutionEventType.WorkflowFailed,
                Message = $"Execution error: {ex.Message}",
                Timestamp = DateTime.UtcNow
            };
            
            var json = System.Text.Json.JsonSerializer.Serialize(errorEvent, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });
            
            await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        
        _logger.LogInformation("Framework execution completed for workflow {Id}", id);
    }

    /// <summary>
    /// 执行框架请求模型
    /// </summary>
    public class ExecuteFrameworkRequest
    {
        public string? UserInput { get; set; }
    }

    /// <summary>
    /// 导出工作流为 YAML 格式 (POST - 接收完整工作流数据)
    /// </summary>
    [HttpPost("export-yaml")]
    public ActionResult<string> ExportWorkflowToYaml([FromBody] DeclarativeWorkflowDefinition workflow)
    {
        try
        {
            var yamlService = HttpContext.RequestServices.GetRequiredService<YamlConversionService>();
            var yaml = yamlService.ConvertToYaml(workflow);
            return Content(yaml, "text/yaml");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting workflow to YAML");
            return StatusCode(500, $"导出失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 从 YAML 导入工作流（仅解析，不保存）
    /// 注意：要导入并保存声明式工作流，请使用 /api/declarative-workflows/import-yaml
    /// </summary>
    [HttpPost("import-yaml")]
    public async Task<ActionResult<DeclarativeWorkflowDefinition>> ImportWorkflowFromYaml()
    {
        try
        {
            using var reader = new StreamReader(Request.Body);
            var yaml = await reader.ReadToEndAsync();
            
            var yamlService = HttpContext.RequestServices.GetRequiredService<YamlConversionService>();
            var workflow = yamlService.ParseFromYaml(yaml);
            
            _logger.LogInformation("YAML 工作流已解析，名称: {Name}, 节点数: {Count}", 
                workflow.Name, workflow.Executors.Count);
            
            return Ok(workflow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing workflow from YAML");
            return BadRequest($"导入失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 导出为 Agent Framework YAML 格式 (根据 ID)
    /// </summary>
    [HttpGet("{id}/export-yaml")]
    public async Task<ActionResult<string>> ExportToAgentFrameworkYaml(string id)
    {
        try
        {
            var yaml = await _workflowService.ConvertToAgentFrameworkYamlAsync(id);
            return Ok(new { yaml });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Workflow {Id} not found", id);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting workflow {Id} to YAML", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// 下载 Agent Framework YAML 文件
    /// </summary>
    [HttpGet("{id}/download-yaml")]
    public async Task<ActionResult> DownloadAgentFrameworkYaml(string id)
    {
        try
        {
            var yaml = await _workflowService.ConvertToAgentFrameworkYamlAsync(id);
            var workflow = await _workflowService.GetWorkflowByIdAsync(id);
            
            var fileName = $"{workflow?.Name ?? "workflow"}.yaml";
            var bytes = System.Text.Encoding.UTF8.GetBytes(yaml);
            
            return File(bytes, "application/x-yaml", fileName);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Workflow {Id} not found", id);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading workflow {Id} as YAML", id);
            return StatusCode(500, "Internal server error");
        }
    }
}
