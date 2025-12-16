using Microsoft.AspNetCore.Mvc;
using WorkflowDesigner.Api.Models;
using WorkflowDesigner.Api.Services;
using System.Text;

namespace WorkflowDesigner.Api.Controllers;

/// <summary>
/// 声明式工作流控制器
/// 提供增强的工作流管理、YAML 转换和执行功能
/// </summary>
[ApiController]
[Route("api/declarative-workflows")]
public class DeclarativeWorkflowsController : ControllerBase
{
    private readonly IDeclarativeWorkflowService _workflowService;
    private readonly YamlConversionService _yamlService;
    private readonly ILogger<DeclarativeWorkflowsController> _logger;

    public DeclarativeWorkflowsController(
        IDeclarativeWorkflowService workflowService,
        YamlConversionService yamlService,
        ILogger<DeclarativeWorkflowsController> logger)
    {
        _workflowService = workflowService;
        _yamlService = yamlService;
        _logger = logger;
    }

    #region CRUD Operations

    /// <summary>
    /// 获取所有声明式工作流
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DeclarativeWorkflowDefinition>>> GetAll()
    {
        var workflows = await _workflowService.GetAllAsync();
        return Ok(workflows);
    }

    /// <summary>
    /// 根据ID获取声明式工作流
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<DeclarativeWorkflowDefinition>> GetById(string id)
    {
        var workflow = await _workflowService.GetByIdAsync(id);
        if (workflow == null)
        {
            return NotFound($"工作流 {id} 不存在");
        }
        return Ok(workflow);
    }

    /// <summary>
    /// 创建声明式工作流
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<DeclarativeWorkflowDefinition>> Create(
        [FromBody] DeclarativeWorkflowDefinition workflow)
    {
        try
        {
            var created = await _workflowService.CreateAsync(workflow);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建工作流失败");
            return BadRequest($"创建失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新声明式工作流
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<DeclarativeWorkflowDefinition>> Update(
        string id, 
        [FromBody] DeclarativeWorkflowDefinition workflow)
    {
        try
        {
            var updated = await _workflowService.UpdateAsync(id, workflow);
            if (updated == null)
            {
                return NotFound($"工作流 {id} 不存在");
            }
            return Ok(updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新工作流 {Id} 失败", id);
            return BadRequest($"更新失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 删除声明式工作流
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id)
    {
        var result = await _workflowService.DeleteAsync(id);
        if (!result)
        {
            return NotFound($"工作流 {id} 不存在");
        }
        return NoContent();
    }

    #endregion

    #region YAML Operations

    /// <summary>
    /// 从 YAML 导入工作流（仅解析，不保存）
    /// </summary>
    [HttpPost("parse-yaml")]
    [Consumes("text/yaml", "text/plain", "application/x-yaml")]
    [Produces("application/json")]
    public ActionResult<DeclarativeWorkflowDefinition> ParseFromYaml()
    {
        try
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var yaml = reader.ReadToEnd();
            
            _logger.LogInformation("Parsing workflow from YAML ({Length} chars)", yaml.Length);
            
            var workflow = _yamlService.ParseFromYaml(yaml);
            
            return Ok(workflow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing workflow from YAML");
            return BadRequest(new { error = "Failed to parse workflow", details = ex.Message });
        }
    }

    /// <summary>
    /// 从 YAML 导入工作流并保存
    /// </summary>
    [HttpPost("import-yaml")]
    public async Task<ActionResult<DeclarativeWorkflowDefinition>> ImportFromYaml()
    {
        try
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var yaml = await reader.ReadToEndAsync();
            
            if (string.IsNullOrWhiteSpace(yaml))
            {
                return BadRequest("YAML 内容不能为空");
            }

            _logger.LogInformation("Importing workflow from YAML ({Length} chars)", yaml.Length);
            
            var workflow = await _workflowService.ImportFromYamlAsync(yaml);
            _logger.LogInformation("工作流已导入并保存，ID: {Id}, 名称: {Name}", workflow.Id, workflow.Name);
            
            return Ok(workflow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing workflow from YAML");
            return BadRequest(new { error = "Failed to import workflow", details = ex.Message });
        }
    }

    /// <summary>
    /// 将声明式工作流转换为 YAML（不保存）
    /// </summary>
    [HttpPost("export-yaml")]
    [Consumes("application/json")]
    [Produces("text/yaml")]
    public ActionResult<string> ExportToYaml([FromBody] DeclarativeWorkflowDefinition workflow)
    {
        try
        {
            _logger.LogInformation("Exporting workflow {Name} to YAML", workflow.Name);
            
            var yaml = _yamlService.ConvertToYaml(workflow);
            
            return Content(yaml, "text/yaml", Encoding.UTF8);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting workflow to YAML");
            return StatusCode(500, new { error = "Failed to export workflow", details = ex.Message });
        }
    }

    /// <summary>
    /// 根据ID导出工作流为 YAML
    /// </summary>
    [HttpGet("{id}/export-yaml")]
    public async Task<ActionResult<object>> ExportWorkflowToYaml(string id)
    {
        try
        {
            var yaml = await _workflowService.ExportToYamlAsync(id);
            return Ok(new { yaml });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出工作流 {Id} 失败", id);
            return StatusCode(500, $"导出失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 下载 YAML 文件
    /// </summary>
    [HttpGet("{id}/download-yaml")]
    public async Task<ActionResult> DownloadYaml(string id)
    {
        try
        {
            var workflow = await _workflowService.GetByIdAsync(id);
            if (workflow == null)
            {
                return NotFound($"工作流 {id} 不存在");
            }

            var yaml = await _workflowService.ExportToYamlAsync(id);
            var fileName = $"{workflow.Name.Replace(" ", "_")}.yaml";
            
            return File(
                Encoding.UTF8.GetBytes(yaml),
                "text/yaml",
                fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载工作流 {Id} 失败", id);
            return StatusCode(500, $"下载失败: {ex.Message}");
        }
    }

    #endregion

    #region Execution

    /// <summary>
    /// 执行声明式工作流
    /// </summary>
    [HttpPost("{id}/execute")]
    public async Task<ActionResult<DeclarativeExecutionResult>> Execute(
        string id,
        [FromBody] ExecuteRequest request)
    {
        try
        {
            var userInput = request.Input ?? request.UserInput ?? "";
            _logger.LogInformation("执行声明式工作流 {Id}, 输入: {Input}", id, userInput);

            var result = await _workflowService.ExecuteAsync(id, userInput);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行工作流 {Id} 失败", id);
            return StatusCode(500, $"执行失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 流式执行声明式工作流 (SSE)
    /// </summary>
    [HttpPost("{id}/execute-stream")]
    public async Task ExecuteStream(string id, [FromBody] ExecuteRequest request)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        var userInput = request.Input ?? request.UserInput ?? "";

        try
        {
            await foreach (var evt in _workflowService.ExecuteStreamAsync(id, userInput, HttpContext.RequestAborted))
            {
                var json = System.Text.Json.JsonSerializer.Serialize(evt, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                });
                
                await Response.WriteAsync($"data: {json}\n\n");
                await Response.Body.FlushAsync();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("客户端断开连接");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "流式执行工作流 {Id} 失败", id);
            
            var errorEvent = new ExecutionEvent
            {
                Type = ExecutionEventType.WorkflowFailed,
                Status = ExecutionStatus.Failed,
                Message = ex.Message
            };
            
            var errorJson = System.Text.Json.JsonSerializer.Serialize(errorEvent);
            await Response.WriteAsync($"data: {errorJson}\n\n");
        }
    }

    #endregion

    #region Validation

    /// <summary>
    /// 验证工作流定义
    /// </summary>
    [HttpPost("validate")]
    public ActionResult<ValidationResult> Validate([FromBody] DeclarativeWorkflowDefinition workflow)
    {
        try
        {
            var result = ValidateWorkflow(workflow);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating workflow");
            return StatusCode(500, new { error = "Failed to validate workflow", details = ex.Message });
        }
    }

    /// <summary>
    /// 预览 YAML 转换结果（不保存）
    /// </summary>
    [HttpPost("preview-yaml")]
    public ActionResult<YamlPreviewResult> PreviewYaml([FromBody] DeclarativeWorkflowDefinition workflow)
    {
        try
        {
            var yaml = _yamlService.ConvertToYaml(workflow);
            var validation = ValidateWorkflow(workflow);
            
            return Ok(new YamlPreviewResult
            {
                Yaml = yaml,
                Validation = validation,
                ExecutorCount = workflow.Executors.Count,
                EdgeCount = workflow.EdgeGroups.Sum(g => g.Edges.Count),
                VariableCount = workflow.Variables.Count,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error previewing YAML");
            return StatusCode(500, new { error = "Failed to preview YAML", details = ex.Message });
        }
    }

    /// <summary>
    /// 获取支持的执行器类型
    /// </summary>
    [HttpGet("executor-types")]
    public ActionResult<IEnumerable<ExecutorTypeInfo>> GetExecutorTypes()
    {
        var types = Enum.GetValues<ExecutorType>()
            .Select(t => new ExecutorTypeInfo
            {
                Type = t.ToString(),
                Category = GetExecutorCategory(t),
                Description = GetExecutorDescription(t),
            })
            .ToList();

        return Ok(types);
    }

    /// <summary>
    /// 获取执行器配置 Schema
    /// </summary>
    [HttpGet("executor-schema/{type}")]
    public ActionResult<object> GetExecutorSchema(string type)
    {
        if (!Enum.TryParse<ExecutorType>(type, true, out var executorType))
        {
            return NotFound(new { error = $"Unknown executor type: {type}" });
        }

        var schema = GetSchemaForExecutorType(executorType);
        return Ok(schema);
    }

    // ==================== 辅助方法 ====================

    private ValidationResult ValidateWorkflow(DeclarativeWorkflowDefinition workflow)
    {
        var errors = new List<ValidationError>();
        var warnings = new List<ValidationWarning>();

        // 检查起始执行器
        if (string.IsNullOrEmpty(workflow.StartExecutorId))
        {
            errors.Add(new ValidationError
            {
                Type = "missing_start",
                Message = "未设置起始执行器",
            });
        }
        else if (!workflow.Executors.Any(e => e.Id == workflow.StartExecutorId))
        {
            errors.Add(new ValidationError
            {
                Type = "invalid_start",
                Message = "起始执行器不存在",
                ExecutorId = workflow.StartExecutorId,
            });
        }

        // 检查孤立节点
        var connectedIds = new HashSet<string>();
        foreach (var group in workflow.EdgeGroups)
        {
            connectedIds.Add(group.SourceExecutorId);
            foreach (var edge in group.Edges)
            {
                connectedIds.Add(edge.TargetExecutorId);
            }
        }

        foreach (var executor in workflow.Executors)
        {
            if (!connectedIds.Contains(executor.Id) && executor.Id != workflow.StartExecutorId)
            {
                warnings.Add(new ValidationWarning
                {
                    Type = "unreachable",
                    Message = $"执行器 \"{executor.Name}\" 不可达",
                    ExecutorId = executor.Id,
                });
            }
        }

        // 检查无效连接
        var executorIds = new HashSet<string>(workflow.Executors.Select(e => e.Id));
        foreach (var group in workflow.EdgeGroups)
        {
            if (!executorIds.Contains(group.SourceExecutorId))
            {
                errors.Add(new ValidationError
                {
                    Type = "invalid_connection",
                    Message = "边组的源执行器不存在",
                });
            }

            foreach (var edge in group.Edges)
            {
                if (!executorIds.Contains(edge.TargetExecutorId))
                {
                    errors.Add(new ValidationError
                    {
                        Type = "invalid_connection",
                        Message = "边的目标执行器不存在",
                        EdgeId = edge.Id,
                    });
                }
            }
        }

        // 检查循环依赖
        if (HasCycle(workflow))
        {
            errors.Add(new ValidationError
            {
                Type = "cycle",
                Message = "工作流存在循环依赖",
            });
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
        };
    }

    private bool HasCycle(DeclarativeWorkflowDefinition workflow)
    {
        var adjacency = new Dictionary<string, List<string>>();
        
        foreach (var executor in workflow.Executors)
        {
            adjacency[executor.Id] = new List<string>();
        }

        foreach (var group in workflow.EdgeGroups)
        {
            if (adjacency.ContainsKey(group.SourceExecutorId))
            {
                adjacency[group.SourceExecutorId].AddRange(group.Edges.Select(e => e.TargetExecutorId));
            }
        }

        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        foreach (var executor in workflow.Executors)
        {
            if (HasCycleDfs(executor.Id, adjacency, visited, recursionStack))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasCycleDfs(string node, Dictionary<string, List<string>> adjacency, 
        HashSet<string> visited, HashSet<string> recursionStack)
    {
        if (recursionStack.Contains(node))
        {
            return true;
        }

        if (visited.Contains(node))
        {
            return false;
        }

        visited.Add(node);
        recursionStack.Add(node);

        if (adjacency.TryGetValue(node, out var neighbors))
        {
            foreach (var neighbor in neighbors)
            {
                if (HasCycleDfs(neighbor, adjacency, visited, recursionStack))
                {
                    return true;
                }
            }
        }

        recursionStack.Remove(node);
        return false;
    }

    private static string GetExecutorCategory(ExecutorType type)
    {
        return type switch
        {
            ExecutorType.ChatAgent or ExecutorType.FunctionAgent or ExecutorType.ToolAgent 
                or ExecutorType.MagenticOrchestrator or ExecutorType.AzureAgent => "agents",
            
            ExecutorType.Condition or ExecutorType.ConditionGroup or ExecutorType.Foreach 
                or ExecutorType.Goto or ExecutorType.BreakLoop or ExecutorType.ContinueLoop 
                or ExecutorType.EndWorkflow or ExecutorType.EndConversation => "controlFlow",
            
            ExecutorType.SetVariable or ExecutorType.SetMultipleVariables or ExecutorType.ParseValue 
                or ExecutorType.EditTable or ExecutorType.ResetVariable or ExecutorType.ClearAllVariables => "stateManagement",
            
            ExecutorType.SendActivity or ExecutorType.AddConversationMessage 
                or ExecutorType.RetrieveConversationMessages => "messages",
            
            ExecutorType.CreateConversation or ExecutorType.DeleteConversation 
                or ExecutorType.CopyConversationMessages => "conversation",
            
            ExecutorType.Question or ExecutorType.FunctionApproval => "humanInput",
            
            ExecutorType.FunctionExecutor or ExecutorType.McpTool or ExecutorType.OpenApiTool 
                or ExecutorType.CodeInterpreter or ExecutorType.FileSearch or ExecutorType.WebSearch => "tools",
            
            ExecutorType.SubWorkflow or ExecutorType.ParallelExecution 
                or ExecutorType.FanOut or ExecutorType.FanIn => "workflow",
            
            _ => "other"
        };
    }

    private static string GetExecutorDescription(ExecutorType type)
    {
        return type switch
        {
            ExecutorType.ChatAgent => "通用对话智能体",
            ExecutorType.FunctionAgent => "支持函数调用的智能体",
            ExecutorType.ToolAgent => "支持多种工具的智能体",
            ExecutorType.MagenticOrchestrator => "多智能体协调编排",
            ExecutorType.AzureAgent => "Azure AI Foundry 智能体",
            ExecutorType.Condition => "基于条件的分支选择",
            ExecutorType.ConditionGroup => "多条件分支选择",
            ExecutorType.Foreach => "遍历集合执行",
            ExecutorType.Goto => "跳转到指定节点",
            ExecutorType.BreakLoop => "跳出当前循环",
            ExecutorType.ContinueLoop => "跳过当前迭代",
            ExecutorType.EndWorkflow => "结束当前工作流",
            ExecutorType.EndConversation => "结束整个会话",
            ExecutorType.SetVariable => "设置单个变量值",
            ExecutorType.SetMultipleVariables => "同时设置多个变量",
            ExecutorType.ParseValue => "解析和转换数据",
            ExecutorType.EditTable => "操作表格数据",
            ExecutorType.ResetVariable => "重置变量到默认值",
            ExecutorType.ClearAllVariables => "清除所有变量",
            ExecutorType.SendActivity => "发送消息给用户",
            ExecutorType.AddConversationMessage => "向对话添加消息",
            ExecutorType.RetrieveConversationMessages => "获取对话历史",
            ExecutorType.CreateConversation => "创建新的对话会话",
            ExecutorType.DeleteConversation => "删除对话会话",
            ExecutorType.CopyConversationMessages => "复制对话消息",
            ExecutorType.Question => "向用户提问并等待回复",
            ExecutorType.FunctionApproval => "请求用户审批函数调用",
            ExecutorType.FunctionExecutor => "执行自定义函数",
            ExecutorType.McpTool => "MCP协议工具",
            ExecutorType.OpenApiTool => "OpenAPI接口调用",
            ExecutorType.CodeInterpreter => "执行代码",
            ExecutorType.FileSearch => "搜索文件内容",
            ExecutorType.WebSearch => "搜索网页信息",
            ExecutorType.SubWorkflow => "调用子工作流",
            ExecutorType.ParallelExecution => "并行执行多个分支",
            ExecutorType.FanOut => "分发到多个目标",
            ExecutorType.FanIn => "合并多个分支",
            _ => type.ToString()
        };
    }

    private static object GetSchemaForExecutorType(ExecutorType type)
    {
        // 返回执行器配置的 JSON Schema
        return type switch
        {
            ExecutorType.ChatAgent or ExecutorType.FunctionAgent or ExecutorType.ToolAgent => new
            {
                type = "object",
                properties = new
                {
                    name = new { type = "string", description = "智能体名称" },
                    description = new { type = "string", description = "智能体描述" },
                    instructionsTemplate = new { type = "string", format = "textarea", description = "指令模板" },
                    modelConfig = new
                    {
                        type = "object",
                        properties = new
                        {
                            provider = new { type = "string", @enum = new[] { "OpenAI", "AzureOpenAI", "Anthropic", "GoogleAI", "Ollama", "Custom" } },
                            model = new { type = "string" },
                            temperature = new { type = "number", minimum = 0, maximum = 2 },
                            maxTokens = new { type = "integer" },
                        }
                    },
                    enableStreaming = new { type = "boolean", @default = true },
                    reflectOnToolUse = new { type = "boolean", @default = false },
                },
                required = new[] { "name", "instructionsTemplate" }
            },
            ExecutorType.Condition => new
            {
                type = "object",
                properties = new
                {
                    expression = new { type = "string", description = "条件表达式" },
                    trueBranchTarget = new { type = "string", description = "条件为真时的目标" },
                    falseBranchTarget = new { type = "string", description = "条件为假时的目标" },
                },
                required = new[] { "expression" }
            },
            ExecutorType.Foreach => new
            {
                type = "object",
                properties = new
                {
                    itemsExpression = new { type = "string", description = "集合表达式" },
                    itemVariableName = new { type = "string", @default = "item" },
                    indexVariableName = new { type = "string", @default = "index" },
                },
                required = new[] { "itemsExpression" }
            },
            ExecutorType.SendActivity => new
            {
                type = "object",
                properties = new
                {
                    message = new { type = "string", format = "textarea", description = "消息内容" },
                    messageType = new { type = "string", @enum = new[] { "text", "markdown", "card" } },
                },
                required = new[] { "message" }
            },
            ExecutorType.Question => new
            {
                type = "object",
                properties = new
                {
                    prompt = new { type = "string", format = "textarea", description = "提示语" },
                    resultVariable = new { type = "string", description = "结果变量名" },
                    validationExpression = new { type = "string", description = "验证表达式" },
                    timeout = new { type = "integer", description = "超时秒数" },
                },
                required = new[] { "prompt", "resultVariable" }
            },
            _ => new { type = "object", properties = new { } }
        };
    }

    #endregion
}

// ==================== DTO 模型 ====================

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<ValidationError> Errors { get; set; } = new();
    public List<ValidationWarning> Warnings { get; set; } = new();
}

public class ValidationError
{
    public string Type { get; set; } = "";
    public string Message { get; set; } = "";
    public string? ExecutorId { get; set; }
    public string? EdgeId { get; set; }
}

public class ValidationWarning
{
    public string Type { get; set; } = "";
    public string Message { get; set; } = "";
    public string? ExecutorId { get; set; }
}

public class YamlPreviewResult
{
    public string Yaml { get; set; } = "";
    public ValidationResult Validation { get; set; } = new();
    public int ExecutorCount { get; set; }
    public int EdgeCount { get; set; }
    public int VariableCount { get; set; }
}

public class ExecutorTypeInfo
{
    public string Type { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
}

/// <summary>
/// 执行工作流请求
/// </summary>
public class ExecuteRequest
{
    /// <summary>
    /// 用户输入
    /// </summary>
    public string? Input { get; set; }

    /// <summary>
    /// 用户输入 (别名)
    /// </summary>
    public string? UserInput { get; set; }

    /// <summary>
    /// 参数
    /// </summary>
    public Dictionary<string, object>? Parameters { get; set; }
}
