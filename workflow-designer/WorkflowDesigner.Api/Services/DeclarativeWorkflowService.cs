using System.Runtime.CompilerServices;
using WorkflowDesigner.Api.Models;
using WorkflowDesigner.Api.Repository;
using Microsoft.Extensions.AI;

namespace WorkflowDesigner.Api.Services;

/// <summary>
/// 声明式工作流服务实现 - 独立于简单工作流的实现
/// </summary>
public class DeclarativeWorkflowService : IDeclarativeWorkflowService
{
    private readonly IRepository<DeclarativeWorkflowDefinition> _repository;
    private readonly YamlConversionService _yamlService;
    private readonly IChatClient _chatClient;
    private readonly ILogger<DeclarativeWorkflowService> _logger;

    public DeclarativeWorkflowService(
        IRepository<DeclarativeWorkflowDefinition> repository,
        YamlConversionService yamlService,
        IChatClient chatClient,
        ILogger<DeclarativeWorkflowService> logger)
    {
        _repository = repository;
        _yamlService = yamlService;
        _chatClient = chatClient;
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

            // 初始化变量上下文
            var variables = new Dictionary<string, object>
            {
                ["user_input"] = userInput,
                ["conversation.user.message"] = userInput
            };

            // 执行工作流
            var currentExecutorId = workflow.StartExecutorId;
            var executedCount = 0;
            var maxIterations = workflow.MaxIterations > 0 ? workflow.MaxIterations : 100;

            while (!string.IsNullOrEmpty(currentExecutorId) && executedCount < maxIterations)
            {
                var executor = workflow.Executors.FirstOrDefault(e => e.Id == currentExecutorId);
                if (executor == null)
                {
                    _logger.LogWarning("未找到执行器: {Id}", currentExecutorId);
                    break;
                }

                var step = new DeclarativeExecutionStep
                {
                    ExecutorId = executor.Id,
                    ExecutorName = executor.Name,
                    ExecutorType = executor.Type.ToString(),
                    StartedAt = DateTime.UtcNow
                };

                try
                {
                    // 执行节点
                    var output = await ExecuteExecutorAsync(executor, variables, userInput);
                    step.Output = output;
                    step.Status = ExecutionStatus.Completed;
                    step.CompletedAt = DateTime.UtcNow;

                    // 检查是否是结束节点
                    if (executor.Type == ExecutorType.EndWorkflow || executor.Type == ExecutorType.EndConversation)
                    {
                        result.Output = output ?? variables.GetValueOrDefault("result")?.ToString() ?? "";
                        break;
                    }
                }
                catch (Exception ex)
                {
                    step.Status = ExecutionStatus.Failed;
                    step.Output = ex.Message;
                    step.CompletedAt = DateTime.UtcNow;
                    _logger.LogError(ex, "执行节点 {Name} 失败", executor.Name);
                }

                result.Steps.Add(step);
                executedCount++;

                // 获取下一个执行器
                currentExecutorId = GetNextExecutorId(workflow, executor, variables);
            }

            result.ExecutedNodes = executedCount;
            result.Variables = variables;
            result.Status = ExecutionStatus.Completed;
            result.CompletedAt = DateTime.UtcNow;

            // 如果没有明确的输出，使用最后一个步骤的输出
            if (string.IsNullOrEmpty(result.Output) && result.Steps.Count > 0)
            {
                result.Output = result.Steps.LastOrDefault(s => !string.IsNullOrEmpty(s.Output))?.Output ?? "工作流执行完成";
            }

            _logger.LogInformation("工作流执行完成: {Name}, 执行节点数: {Count}", 
                workflow.Name, executedCount);
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

        var variables = new Dictionary<string, object>
        {
            ["user_input"] = userInput,
            ["conversation.user.message"] = userInput
        };

        var currentExecutorId = workflow.StartExecutorId;
        var executedCount = 0;
        var maxIterations = workflow.MaxIterations > 0 ? workflow.MaxIterations : 100;

        while (!string.IsNullOrEmpty(currentExecutorId) && executedCount < maxIterations && !cancellationToken.IsCancellationRequested)
        {
            var executor = workflow.Executors.FirstOrDefault(e => e.Id == currentExecutorId);
            if (executor == null) break;

            yield return new ExecutionEvent
            {
                Type = ExecutionEventType.NodeStarted,
                NodeId = executor.Id,
                NodeName = executor.Name,
                Status = ExecutionStatus.Running,
                Message = $"开始执行: {executor.Name}"
            };

            string? output = null;
            ExecutionEvent? resultEvent = null;
            bool shouldBreak = false;
            
            try
            {
                output = await ExecuteExecutorAsync(executor, variables, userInput);

                resultEvent = new ExecutionEvent
                {
                    Type = ExecutionEventType.NodeCompleted,
                    NodeId = executor.Id,
                    NodeName = executor.Name,
                    Status = ExecutionStatus.Completed,
                    Message = output,
                    Data = new Dictionary<string, object>
                    {
                        ["executorType"] = executor.Type.ToString()
                    }
                };

                if (executor.Type == ExecutorType.EndWorkflow || executor.Type == ExecutorType.EndConversation)
                {
                    shouldBreak = true;
                }
            }
            catch (Exception ex)
            {
                resultEvent = new ExecutionEvent
                {
                    Type = ExecutionEventType.NodeFailed,
                    NodeId = executor.Id,
                    NodeName = executor.Name,
                    Status = ExecutionStatus.Failed,
                    Message = ex.Message
                };
            }

            if (resultEvent != null)
            {
                yield return resultEvent;
            }

            if (shouldBreak) break;

            executedCount++;
            currentExecutorId = GetNextExecutorId(workflow, executor, variables);
        }

        yield return new ExecutionEvent
        {
            Type = ExecutionEventType.WorkflowCompleted,
            Status = ExecutionStatus.Completed,
            Message = "工作流执行完成",
            Data = new Dictionary<string, object>
            {
                ["executedNodes"] = executedCount,
                ["variables"] = variables
            }
        };
    }

    /// <summary>
    /// 执行单个执行器
    /// </summary>
    private async Task<string?> ExecuteExecutorAsync(
        ExecutorDefinition executor, 
        Dictionary<string, object> variables,
        string userInput)
    {
        _logger.LogDebug("执行节点: {Name}, 类型: {Type}", executor.Name, executor.Type);

        switch (executor.Type)
        {
            case ExecutorType.SetVariable:
            case ExecutorType.SetTextVariable:
                return ExecuteSetVariable(executor, variables);

            case ExecutorType.SendActivity:
                return ExecuteSendActivity(executor, variables);

            case ExecutorType.Question:
                return ExecuteQuestion(executor, variables);

            case ExecutorType.InvokeAzureAgent:
                return await ExecuteInvokeAgentAsync(executor, variables, userInput);

            case ExecutorType.ConditionGroup:
                return "条件评估完成";

            case ExecutorType.Foreach:
                return "循环执行完成";

            case ExecutorType.EndWorkflow:
                return variables.GetValueOrDefault("result")?.ToString() ?? "工作流结束";

            case ExecutorType.EndConversation:
                return "会话结束";

            default:
                _logger.LogWarning("未实现的执行器类型: {Type}", executor.Type);
                return $"执行器 {executor.Name} 完成";
        }
    }

    private string ExecuteSetVariable(ExecutorDefinition executor, Dictionary<string, object> variables)
    {
        var variableName = executor.Config.GetValueOrDefault("variableName")?.ToString() ?? "";
        var value = executor.Config.GetValueOrDefault("value")?.ToString() ?? "";

        // 替换变量引用
        value = ReplaceVariables(value, variables);
        variables[variableName] = value;

        return $"设置变量 {variableName} = {value}";
    }

    private string ExecuteSendActivity(ExecutorDefinition executor, Dictionary<string, object> variables)
    {
        var message = executor.Config.GetValueOrDefault("message")?.ToString() ?? "";
        message = ReplaceVariables(message, variables);
        return message;
    }

    private string ExecuteQuestion(ExecutorDefinition executor, Dictionary<string, object> variables)
    {
        var prompt = executor.Config.GetValueOrDefault("prompt")?.ToString() ?? "";
        var resultVariable = executor.Config.GetValueOrDefault("resultVariable")?.ToString() ?? "response";
        
        prompt = ReplaceVariables(prompt, variables);
        
        // 在实际场景中，这里会等待用户输入
        // 目前模拟将用户输入存储到结果变量
        variables[resultVariable] = variables.GetValueOrDefault("user_input")?.ToString() ?? "";
        
        return prompt;
    }

    private async Task<string> ExecuteInvokeAgentAsync(
        ExecutorDefinition executor, 
        Dictionary<string, object> variables,
        string userInput)
    {
        var agentName = executor.Config.GetValueOrDefault("name")?.ToString() ?? "Agent";
        var instructions = executor.Config.GetValueOrDefault("instructionsTemplate")?.ToString() ?? "";
        
        instructions = ReplaceVariables(instructions, variables);
        var inputMessage = ReplaceVariables(userInput, variables);

        _logger.LogInformation("调用智能体 {Name}, 输入: {Input}", agentName, inputMessage);

        try
        {
            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, instructions),
                new ChatMessage(ChatRole.User, inputMessage)
            };

            var response = await _chatClient.GetResponseAsync(messages);
            var result = response.Text ?? "";

            // 存储结果到变量
            variables["agent_response"] = result;
            variables["result"] = result;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "调用智能体 {Name} 失败", agentName);
            return $"智能体调用失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 获取下一个执行器ID
    /// </summary>
    private string? GetNextExecutorId(
        DeclarativeWorkflowDefinition workflow, 
        ExecutorDefinition currentExecutor,
        Dictionary<string, object> variables)
    {
        // 处理 GotoAction
        if (currentExecutor.Type == ExecutorType.GotoAction)
        {
            return currentExecutor.Config.GetValueOrDefault("targetExecutorId")?.ToString();
        }

        // 查找边组
        var edgeGroup = workflow.EdgeGroups.FirstOrDefault(eg => eg.SourceExecutorId == currentExecutor.Id);
        if (edgeGroup == null || edgeGroup.Edges.Count == 0)
        {
            return null;
        }

        // 处理条件分支
        if (currentExecutor.Type == ExecutorType.ConditionGroup && edgeGroup.Edges.Count > 1)
        {
            // 评估条件，返回第一个匹配的目标
            foreach (var edge in edgeGroup.Edges)
            {
                if (string.IsNullOrEmpty(edge.Condition) || EvaluateCondition(edge.Condition, variables))
                {
                    return edge.TargetExecutorId;
                }
            }
        }

        // 默认返回第一条边的目标
        return edgeGroup.Edges.FirstOrDefault()?.TargetExecutorId;
    }

    /// <summary>
    /// 替换变量引用 - 支持 Agent Framework 官方格式
    /// 支持的格式:
    /// - =Variable (整个值是变量引用)
    /// - =Local.Variable
    /// - =${scope.variable}
    /// - ${variable}
    /// - $(variable)
    /// </summary>
    private string ReplaceVariables(string template, Dictionary<string, object> variables)
    {
        if (string.IsNullOrEmpty(template)) return template;

        // 处理 Agent Framework 官方格式: =Variable 或 =Local.Variable (整个值是变量引用)
        if (template.StartsWith("=") && !template.StartsWith("=${") && !template.StartsWith("=$("))
        {
            var varName = template.Substring(1); // 去掉 = 前缀
            
            // 直接查找完整变量名
            if (variables.TryGetValue(varName, out var directValue))
            {
                return directValue?.ToString() ?? "";
            }
            
            // 尝试不同的大小写和格式
            var normalizedName = varName.Replace(".", "_");
            if (variables.TryGetValue(normalizedName, out var normalizedValue))
            {
                return normalizedValue?.ToString() ?? "";
            }
            
            // 尝试查找匹配的变量（不区分大小写）
            var matchedKey = variables.Keys.FirstOrDefault(k => 
                k.Equals(varName, StringComparison.OrdinalIgnoreCase) ||
                k.Replace(".", "_").Equals(varName.Replace(".", "_"), StringComparison.OrdinalIgnoreCase));
            
            if (matchedKey != null)
            {
                return variables[matchedKey]?.ToString() ?? "";
            }
        }

        // 处理 =${variable} 格式
        var result = System.Text.RegularExpressions.Regex.Replace(
            template, 
            @"=\$\{([^}]+)\}", 
            match => 
            {
                var varName = match.Groups[1].Value;
                return variables.TryGetValue(varName, out var val) ? val?.ToString() ?? "" : match.Value;
            });

        // 处理 ${variable} 格式
        result = System.Text.RegularExpressions.Regex.Replace(
            result, 
            @"\$\{([^}]+)\}", 
            match => 
            {
                var varName = match.Groups[1].Value;
                return variables.TryGetValue(varName, out var val) ? val?.ToString() ?? "" : match.Value;
            });

        // 处理 $(variable) 格式
        result = System.Text.RegularExpressions.Regex.Replace(
            result, 
            @"\$\(([^)]+)\)", 
            match => 
            {
                var varName = match.Groups[1].Value;
                return variables.TryGetValue(varName, out var val) ? val?.ToString() ?? "" : match.Value;
            });

        return result;
    }

    /// <summary>
    /// 评估条件表达式 (简单实现)
    /// </summary>
    private bool EvaluateCondition(string condition, Dictionary<string, object> variables)
    {
        // 简单的条件评估
        // TODO: 实现完整的表达式评估器
        
        condition = ReplaceVariables(condition, variables);
        
        // 处理简单的比较
        if (condition.Contains("=="))
        {
            var parts = condition.Split("==");
            if (parts.Length == 2)
            {
                return parts[0].Trim() == parts[1].Trim().Trim('"', '\'');
            }
        }

        if (condition.ToLower() == "true") return true;
        if (condition.ToLower() == "false") return false;

        // 默认返回 true
        return true;
    }
}
