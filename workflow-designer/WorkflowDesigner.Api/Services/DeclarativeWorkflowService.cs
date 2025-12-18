using System.Runtime.CompilerServices;
using WorkflowDesigner.Api.Models;
using WorkflowDesigner.Api.Repository;
using Microsoft.Extensions.AI;

namespace WorkflowDesigner.Api.Services;

/// <summary>
/// 声明式工作流服务实现 - 使用 Agent Framework 执行工作流
/// </summary>
public class DeclarativeWorkflowService : IDeclarativeWorkflowService
{
    private readonly IRepository<DeclarativeWorkflowDefinition> _repository;
    private readonly YamlConversionService _yamlService;
    private readonly IWorkflowService _workflowService;
    private readonly ILogger<DeclarativeWorkflowService> _logger;

    public DeclarativeWorkflowService(
        IRepository<DeclarativeWorkflowDefinition> repository,
        YamlConversionService yamlService,
        IWorkflowService workflowService,
        ILogger<DeclarativeWorkflowService> logger)
    {
        _repository = repository;
        _yamlService = yamlService;
        _workflowService = workflowService;
        _logger = logger;
    }

    public async Task<IEnumerable<DeclarativeWorkflowDefinition>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }

    public async Task<DeclarativeWorkflowDefinition?> GetByIdAsync(string id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<DeclarativeWorkflowDefinition> CreateAsync(DeclarativeWorkflowDefinition workflow)
    {
        workflow.CreatedAt = DateTime.UtcNow;
        workflow.UpdatedAt = DateTime.UtcNow;
        
        _logger.LogInformation("创建声明式工作流: {Name}, ID: {Id}", workflow.Name, workflow.Id);
        return await _repository.AddAsync(workflow);
    }

    public async Task<DeclarativeWorkflowDefinition?> UpdateAsync(string id, DeclarativeWorkflowDefinition workflow)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null)
        {
            _logger.LogWarning("未找到要更新的工作流: {Id}", id);
            return null;
        }

        workflow.Id = id;
        workflow.CreatedAt = existing.CreatedAt;
        workflow.UpdatedAt = DateTime.UtcNow;
        
        _logger.LogInformation("更新声明式工作流: {Id}", id);
        return await _repository.UpdateAsync(workflow);
    }

    public async Task<bool> DeleteAsync(string id)
    {
        _logger.LogInformation("删除声明式工作流: {Id}", id);
        return await _repository.DeleteAsync(id);
    }

    public Task<DeclarativeWorkflowDefinition> ImportFromYamlAsync(string yaml)
    {
        var workflow = _yamlService.ParseFromYaml(yaml);
        _logger.LogInformation("从 YAML 解析工作流: {Name}, 节点数: {Count}", 
            workflow.Name, workflow.Executors.Count);
        
        return CreateAsync(workflow);
    }

    public async Task<string> ExportToYamlAsync(string id)
    {
        var workflow = await _repository.GetByIdAsync(id);
        if (workflow == null)
        {
            throw new InvalidOperationException($"工作流 {id} 不存在");
        }

        return _yamlService.ConvertToYaml(workflow);
    }

    public async Task<DeclarativeExecutionResult> ExecuteAsync(string id, string userInput)
    {
        var workflow = await _repository.GetByIdAsync(id);
        if (workflow == null)
        {
            throw new InvalidOperationException($"工作流 {id} 不存在");
        }

        var result = new DeclarativeExecutionResult
        {
            WorkflowId = id,
            WorkflowName = workflow.Name,
            UserInput = userInput,
            Status = ExecutionStatus.Running,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("开始执行声明式工作流: {Name}, 用户输入: {Input}", 
                workflow.Name, userInput);

            // 转换为 YAML
            var yaml = _yamlService.ConvertToYaml(workflow);
            
            // 使用 Agent Framework 执行
            var executedSteps = 0;
            await foreach (var evt in _workflowService.ExecuteYamlWorkflowAsync(yaml, userInput))
            {
                // 记录步骤
                if (evt.Type == ExecutionEventType.NodeStarted || evt.Type == ExecutionEventType.NodeCompleted)
                {
                    var step = new DeclarativeExecutionStep
                    {
                        ExecutorId = evt.NodeId ?? "unknown",
                        ExecutorName = evt.NodeName ?? "Unknown",
                        ExecutorType = "Executor",
                        StartedAt = evt.Timestamp,
                        Status = evt.Status,
                        Output = evt.Message
                    };

                    if (evt.Type == ExecutionEventType.NodeCompleted)
                    {
                        step.CompletedAt = evt.Timestamp;
                        executedSteps++;
                    }

                    result.Steps.Add(step);
                }
                else if (evt.Type == ExecutionEventType.NodeFailed)
                {
                    var step = new DeclarativeExecutionStep
                    {
                        ExecutorId = evt.NodeId ?? "unknown",
                        ExecutorName = evt.NodeName ?? "Unknown",
                        ExecutorType = "Executor",
                        StartedAt = evt.Timestamp,
                        CompletedAt = evt.Timestamp,
                        Status = ExecutionStatus.Failed,
                        Output = evt.Message
                    };
                    result.Steps.Add(step);
                }
                else if (evt.Type == ExecutionEventType.WorkflowCompleted)
                {
                    result.Output = evt.Message ?? "工作流执行完成";
                }
                else if (evt.Type == ExecutionEventType.WorkflowFailed)
                {
                    result.Status = ExecutionStatus.Failed;
                    result.ErrorMessage = evt.Message ?? "工作流执行失败";
                    break;
                }
            }

            result.ExecutedNodes = executedSteps;
            result.Status = ExecutionStatus.Completed;
            result.CompletedAt = DateTime.UtcNow;

            _logger.LogInformation("工作流执行完成: {Name}, 执行节点数: {Count}", 
                workflow.Name, executedSteps);
        }
        catch (Exception ex)
        {
            result.Status = ExecutionStatus.Failed;
            result.ErrorMessage = ex.Message;
            result.CompletedAt = DateTime.UtcNow;
            _logger.LogError(ex, "执行工作流 {Name} 失败", workflow.Name);
        }

        return result;
    }

    public async IAsyncEnumerable<ExecutionEvent> ExecuteStreamAsync(
        string id,
        string userInput,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var workflow = await _repository.GetByIdAsync(id);
        if (workflow == null)
        {
            yield return new ExecutionEvent
            {
                Type = ExecutionEventType.WorkflowFailed,
                Message = $"工作流 {id} 不存在",
                Status = ExecutionStatus.Failed
            };
            yield break;
        }

        _logger.LogInformation("开始流式执行声明式工作流: {Name}", workflow.Name);

        yield return new ExecutionEvent
        {
            Type = ExecutionEventType.WorkflowStarted,
            Message = $"开始执行工作流: {workflow.Name}",
            Status = ExecutionStatus.Running,
            Data = new Dictionary<string, object>
            {
                ["workflowId"] = id,
                ["workflowName"] = workflow.Name,
                ["userInput"] = userInput
            }
        };

        // 转换为 YAML
        string? yaml = null;
        string? errorMessage = null;
        
        try
        {
            yaml = _yamlService.ConvertToYaml(workflow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "转换工作流为 YAML 失败");
            errorMessage = ex.Message;
        }

        if (errorMessage != null)
        {
            yield return new ExecutionEvent
            {
                Type = ExecutionEventType.WorkflowFailed,
                Message = $"转换工作流失败: {errorMessage}",
                Status = ExecutionStatus.Failed
            };
            yield break;
        }

        // 使用 Agent Framework 执行，直接转发事件
        await foreach (var evt in _workflowService.ExecuteYamlWorkflowAsync(yaml!, userInput, cancellationToken))
        {
            yield return evt;
        }
    }
}
