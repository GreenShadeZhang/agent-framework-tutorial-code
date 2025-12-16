using WorkflowDesigner.Api.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Declarative;
using Microsoft.Extensions.AI;
using WorkflowDesigner.Api.Repository;

namespace WorkflowDesigner.Api.Services;

/// <summary>
/// 工作流服务实现
/// </summary>
public class WorkflowService : IWorkflowService
{
    private readonly IRepository<WorkflowDefinition> _workflowRepository;
    private readonly IRepository<ExecutionLog> _executionRepository;
    private readonly IRepository<AgentDefinition> _agentRepository;
    private readonly ITemplateService _templateService;
    private readonly WorkflowExecutor _workflowExecutor;
    private readonly IChatClient _chatClient;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<WorkflowService> _logger;

    public WorkflowService(
        IRepository<WorkflowDefinition> workflowRepository,
        IRepository<ExecutionLog> executionRepository,
        IRepository<AgentDefinition> agentRepository,
        ITemplateService templateService,
        WorkflowExecutor workflowExecutor,
        IChatClient chatClient,
        ILoggerFactory loggerFactory,
        ILogger<WorkflowService> logger)
    {
        _workflowRepository = workflowRepository;
        _executionRepository = executionRepository;
        _agentRepository = agentRepository;
        _templateService = templateService;
        _workflowExecutor = workflowExecutor;
        _chatClient = chatClient;
        _loggerFactory = loggerFactory;
        _logger = logger;
    }

    public async Task<IEnumerable<WorkflowDefinition>> GetAllWorkflowsAsync()
    {
        return await _workflowRepository.GetAllAsync();
    }

    public async Task<WorkflowDefinition?> GetWorkflowByIdAsync(string id)
    {
        return await _workflowRepository.GetByIdAsync(id);
    }

    public async Task<WorkflowDefinition> CreateWorkflowAsync(WorkflowDefinition workflow)
    {
        workflow.CreatedAt = DateTime.UtcNow;
        workflow.UpdatedAt = DateTime.UtcNow;
        return await _workflowRepository.AddAsync(workflow);
    }

    public async Task<WorkflowDefinition?> UpdateWorkflowAsync(string id, WorkflowDefinition workflow)
    {
        var existing = await _workflowRepository.GetByIdAsync(id);
        if (existing == null)
        {
            return null;
        }

        workflow.Id = id;
        workflow.CreatedAt = existing.CreatedAt;
        workflow.UpdatedAt = DateTime.UtcNow;
        return await _workflowRepository.UpdateAsync(workflow);
    }

    public async Task<bool> DeleteWorkflowAsync(string id)
    {
        return await _workflowRepository.DeleteAsync(id);
    }

    public async Task<ExecutionLog> ExecuteWorkflowAsync(string id, Dictionary<string, object> parameters)
    {
        var workflow = await _workflowRepository.GetByIdAsync(id);
        if (workflow == null)
        {
            throw new ArgumentException($"Workflow {id} not found");
        }

        var executionLog = new ExecutionLog
        {
            WorkflowId = id,
            InputParameters = parameters,
            StartedAt = DateTime.UtcNow,
            Status = ExecutionStatus.Running
        };

        try
        {
            // TODO: 实现实际的工作流执行逻辑
            // 这里需要集成 Agent Framework 的执行引擎
            _logger.LogInformation("Starting workflow execution: {WorkflowId}", id);

            // 模拟执行步骤
            var steps = new List<ExecutionStep>();
            for (int i = 0; i < workflow.Nodes.Count; i++)
            {
                var node = workflow.Nodes[i];
                var step = new ExecutionStep
                {
                    NodeId = node.Id,
                    NodeName = node.Data.ContainsKey("name") ? node.Data["name"].ToString() ?? "" : "",
                    Order = i,
                    Status = ExecutionStatus.Completed,
                    StartedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow
                };
                steps.Add(step);
            }

            executionLog.Steps = steps;
            executionLog.Status = ExecutionStatus.Completed;
            executionLog.CompletedAt = DateTime.UtcNow;
            executionLog.DurationMs = (long)(executionLog.CompletedAt.Value - executionLog.StartedAt).TotalMilliseconds;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing workflow {WorkflowId}", id);
            executionLog.Status = ExecutionStatus.Failed;
            executionLog.ErrorMessage = ex.Message;
            executionLog.CompletedAt = DateTime.UtcNow;
            executionLog.DurationMs = (long)(executionLog.CompletedAt.Value - executionLog.StartedAt).TotalMilliseconds;
        }

        await _executionRepository.AddAsync(executionLog);
        return executionLog;
    }

    public async IAsyncEnumerable<ExecutionEvent> ExecuteWorkflowStreamAsync(
        string id,
        Dictionary<string, object> parameters,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var workflow = await _workflowRepository.GetByIdAsync(id);
        if (workflow == null)
        {
            yield return new ExecutionEvent
            {
                Type = ExecutionEventType.WorkflowFailed,
                Status = ExecutionStatus.Failed,
                Message = $"Workflow {id} not found"
            };
            yield break;
        }

        _logger.LogInformation("开始使用 Agent Framework 执行工作流: {WorkflowName}", workflow.Name);

        // 发送工作流开始事件
        yield return new ExecutionEvent
        {
            Type = ExecutionEventType.WorkflowStarted,
            Status = ExecutionStatus.Running,
            Message = $"开始执行工作流: {workflow.Name}",
            Data = new Dictionary<string, object> { ["workflowId"] = id }
        };

        var executionLog = new ExecutionLog
        {
            WorkflowId = id,
            InputParameters = parameters,
            StartedAt = DateTime.UtcNow,
            Status = ExecutionStatus.Running
        };

        var steps = new List<ExecutionStep>();
        bool hasError = false;
        string? errorMessage = null;

        // 使用 WorkflowExecutor 执行工作流
        await foreach (var evt in _workflowExecutor.ExecuteWorkflowAsync(workflow, parameters))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield return new ExecutionEvent
                {
                    Type = ExecutionEventType.WorkflowFailed,
                    Status = ExecutionStatus.Cancelled,
                    Message = "工作流执行已取消"
                };
                yield break;
            }

            // 将 WorkflowExecutionEvent 转换为 ExecutionEvent
            switch (evt.Type)
            {
                case "node_start":
                    yield return new ExecutionEvent
                    {
                        Type = ExecutionEventType.NodeStarted,
                        NodeId = evt.NodeId,
                        NodeName = evt.AgentName ?? "节点",
                        Status = ExecutionStatus.Running,
                        Message = evt.Message
                    };
                    break;

                case "agent_response":
                    var step = new ExecutionStep
                    {
                        NodeId = evt.NodeId ?? "",
                        NodeName = evt.AgentName ?? "智能体",
                        Order = steps.Count,
                        Status = ExecutionStatus.Completed,
                        StartedAt = DateTime.UtcNow,
                        CompletedAt = DateTime.UtcNow,
                        Output = new Dictionary<string, object>
                        {
                            ["response"] = evt.Message
                        }
                    };
                    steps.Add(step);

                    yield return new ExecutionEvent
                    {
                        Type = ExecutionEventType.NodeCompleted,
                        NodeId = evt.NodeId,
                        NodeName = evt.AgentName,
                        Status = ExecutionStatus.Completed,
                        Message = evt.Message,
                        Data = step.Output
                    };
                    break;

                case "condition_evaluated":
                    yield return new ExecutionEvent
                    {
                        Type = ExecutionEventType.NodeCompleted,
                        NodeId = evt.NodeId,
                        NodeName = "条件节点",
                        Status = ExecutionStatus.Completed,
                        Message = evt.Message
                    };
                    break;

                case "error":
                    hasError = true;
                    errorMessage = evt.Message;
                    yield return new ExecutionEvent
                    {
                        Type = ExecutionEventType.NodeFailed,
                        NodeId = evt.NodeId,
                        Status = ExecutionStatus.Failed,
                        Message = evt.Message
                    };
                    break;

                case "end":
                    yield return new ExecutionEvent
                    {
                        Type = ExecutionEventType.WorkflowCompleted,
                        Status = ExecutionStatus.Completed,
                        Message = evt.Message
                    };
                    break;
            }
        }

        executionLog.Steps = steps;
        
        if (hasError)
        {
            executionLog.Status = ExecutionStatus.Failed;
            executionLog.ErrorMessage = errorMessage;
            executionLog.CompletedAt = DateTime.UtcNow;
            executionLog.DurationMs = (long)(executionLog.CompletedAt.Value - executionLog.StartedAt).TotalMilliseconds;
            
            await _executionRepository.AddAsync(executionLog);

            yield return new ExecutionEvent
            {
                Type = ExecutionEventType.WorkflowFailed,
                Status = ExecutionStatus.Failed,
                Message = $"工作流执行失败: {errorMessage}"
            };
        }
        else
        {
            executionLog.Status = ExecutionStatus.Completed;
            executionLog.CompletedAt = DateTime.UtcNow;
            executionLog.DurationMs = (long)(executionLog.CompletedAt.Value - executionLog.StartedAt).TotalMilliseconds;

            await _executionRepository.AddAsync(executionLog);

            // 发送工作流完成事件
            yield return new ExecutionEvent
            {
                Type = ExecutionEventType.WorkflowCompleted,
                Status = ExecutionStatus.Completed,
                Message = $"工作流执行完成，耗时 {executionLog.DurationMs}ms",
                Data = new Dictionary<string, object>
                {
                    ["executionId"] = executionLog.Id,
                    ["duration"] = executionLog.DurationMs
                }
            };
        }
    }

    private async Task<ExecutionStep> ExecuteNodeAsync(
        WorkflowNode node,
        Dictionary<string, object> parameters,
        WorkflowDefinition workflow)
    {
        var step = new ExecutionStep
        {
            NodeId = node.Id,
            NodeName = node.Data.ContainsKey("name") ? node.Data["name"]?.ToString() ?? "" : "",
            Order = workflow.Nodes.IndexOf(node),
            Status = ExecutionStatus.Running,
            StartedAt = DateTime.UtcNow,
            Input = new Dictionary<string, object>(parameters)
        };

        try
        {
            // 根据节点类型执行不同的逻辑
            switch (node.Type)
            {
                case WorkflowNodeType.Agent:
                    // 执行智能体节点
                    if (node.Data.ContainsKey("agentId"))
                    {
                        var agentId = node.Data["agentId"]?.ToString();
                        if (!string.IsNullOrEmpty(agentId))
                        {
                            var agent = await _agentRepository.GetByIdAsync(agentId);
                            if (agent != null)
                            {
                                // 渲染 prompt 模板
                                var prompt = await _templateService.RenderAsync(
                                    agent.InstructionsTemplate,
                                    parameters);
                                
                                step.Output["prompt"] = prompt;
                                step.Output["agentName"] = agent.Name;
                                
                                // TODO: 调用实际的 AI 模型
                                step.Output["response"] = $"[模拟响应] 智能体 {agent.Name} 处理了请求";
                            }
                        }
                    }
                    break;

                case WorkflowNodeType.Condition:
                    // 执行条件节点
                    step.Output["condition"] = "true";
                    break;

                case WorkflowNodeType.Start:
                case WorkflowNodeType.End:
                    // 开始/结束节点不需要特殊处理
                    break;
            }

            step.Status = ExecutionStatus.Completed;
            step.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing node {NodeId}", node.Id);
            step.Status = ExecutionStatus.Failed;
            step.ErrorMessage = ex.Message;
            step.CompletedAt = DateTime.UtcNow;
        }

        return step;
    }

    public async Task<string> RenderPromptTemplateAsync(
        WorkflowDefinition workflow,
        Dictionary<string, object> parameters)
    {
        try
        {
            // 收集所有智能体的模板
            var prompts = new List<string>();

            foreach (var node in workflow.Nodes.Where(n => n.Type == WorkflowNodeType.Agent))
            {
                if (node.Data.ContainsKey("agentId"))
                {
                    var agentId = node.Data["agentId"]?.ToString();
                    if (!string.IsNullOrEmpty(agentId))
                    {
                        var agent = await _agentRepository.GetByIdAsync(agentId);
                        if (agent != null)
                        {
                            var rendered = await _templateService.RenderAsync(
                                agent.InstructionsTemplate,
                                parameters);
                            prompts.Add($"[{agent.Name}]\n{rendered}");
                        }
                    }
                }
            }

            return string.Join("\n\n---\n\n", prompts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering prompt template");
            throw;
        }
    }

    public async Task<string> ExportToYamlAsync(WorkflowDefinition workflow)
    {
        try
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var yamlObject = new
            {
                id = workflow.Id,
                name = workflow.Name,
                description = workflow.Description,
                version = workflow.Version,
                parameters = workflow.Parameters.Select(p => new
                {
                    name = p.Name,
                    type = p.Type,
                    required = p.Required,
                    defaultValue = p.DefaultValue,
                    description = p.Description
                }),
                nodes = workflow.Nodes.Select(n => new
                {
                    id = n.Id,
                    type = n.Type.ToString(),
                    position = new { x = n.Position.X, y = n.Position.Y },
                    data = n.Data
                }),
                edges = workflow.Edges.Select(e => new
                {
                    id = e.Id,
                    source = e.Source,
                    target = e.Target,
                    type = e.Type.ToString(),
                    condition = e.Condition
                })
            };

            return await Task.FromResult(serializer.Serialize(yamlObject));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting workflow to YAML");
            throw;
        }
    }

    public async Task<WorkflowDefinition> ImportFromYamlAsync(string yaml, string name)
    {
        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var yamlObject = deserializer.Deserialize<Dictionary<string, object>>(yaml);

            var workflow = new WorkflowDefinition
            {
                Name = name,
                YamlContent = yaml,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // TODO: 解析 YAML 内容填充 workflow 对象

            return await CreateWorkflowAsync(workflow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing workflow from YAML");
            throw;
        }
    }

    /// <summary>
    /// 将工作流转换为 Agent Framework AdaptiveDialog 格式的 YAML
    /// </summary>
    public async Task<string> ConvertToAgentFrameworkYamlAsync(string workflowId)
    {
        var workflow = await _workflowRepository.GetByIdAsync(workflowId);
        if (workflow == null)
        {
            throw new InvalidOperationException($"Workflow {workflowId} not found");
        }

        // 从 workflowDump 中解析真实的节点数据
        var actualNodes = await ParseWorkflowNodesAsync(workflow);
        
        // 构建智能体映射表
        var agentMap = await BuildAgentMapAsync(actualNodes);
        
        // 构建 AdaptiveDialog 动作序列
        var actions = await BuildActionSequenceAsync(workflow, actualNodes, agentMap);
        
        // 创建 AdaptiveDialog 结构
        var adaptiveDialog = new Dictionary<string, object>
        {
            ["kind"] = "Workflow",
            ["id"] = workflow.Id,
            ["trigger"] = new Dictionary<string, object>
            {
                ["kind"] = "OnUnknownIntent",
                ["id"] = $"{workflow.Id}_trigger",
                ["actions"] = actions
            }
        };

        // 序列化为 YAML
        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .WithIndentedSequences() // 确保序列使用缩进格式
            .Build();

        var yamlContent = serializer.Serialize(adaptiveDialog);
        
        // 验证 YAML 格式：确保第一行没有缩进
        if (yamlContent.StartsWith(" ") || yamlContent.StartsWith("\t"))
        {
            _logger.LogWarning("Generated YAML has leading whitespace, trimming...");
            yamlContent = yamlContent.TrimStart();
        }
        
        return yamlContent;
    }

    /// <summary>
    /// 从 workflowDump 解析节点数据
    /// </summary>
    private async Task<List<WorkflowNode>> ParseWorkflowNodesAsync(WorkflowDefinition workflow)
    {
        var actualNodes = workflow.Nodes;
        
        if (string.IsNullOrEmpty(workflow.WorkflowDump))
        {
            return actualNodes;
        }

        try
        {
            var dumpData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(workflow.WorkflowDump);
            if (dumpData?.TryGetValue("nodes", out var nodesElement) == true)
            {
                var nodesJson = nodesElement.GetRawText();
                var parsedNodes = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(nodesJson);
                
                if (parsedNodes != null)
                {
                    foreach (var parsedNode in parsedNodes)
                    {
                        if (parsedNode.TryGetValue("id", out var nodeIdObj) && 
                            nodeIdObj is System.Text.Json.JsonElement nodeIdElem)
                        {
                            var nodeId = nodeIdElem.GetString();
                            var existingNode = actualNodes.FirstOrDefault(n => n.Id == nodeId);
                            
                            if (existingNode != null && 
                                parsedNode.TryGetValue("data", out var dataObj) && 
                                dataObj is System.Text.Json.JsonElement dataElem)
                            {
                                var dataDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(dataElem.GetRawText());
                                if (dataDict != null)
                                {
                                    existingNode.Data = dataDict.ToDictionary(
                                        kvp => kvp.Key,
                                        kvp => kvp.Value is System.Text.Json.JsonElement elem ? elem.ToString() : kvp.Value
                                    );
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse workflowDump, using original nodes");
        }

        return actualNodes;
    }

    /// <summary>
    /// 构建智能体映射表（ID -> AgentDefinition）
    /// </summary>
    private async Task<Dictionary<string, AgentDefinition>> BuildAgentMapAsync(List<WorkflowNode> nodes)
    {
        var agentMap = new Dictionary<string, AgentDefinition>();

        foreach (var node in nodes.Where(n => n.Type == WorkflowNodeType.Agent))
        {
            var agentId = ExtractAgentId(node);
            if (agentId != null && !agentMap.ContainsKey(agentId))
            {
                var agent = await _agentRepository.GetByIdAsync(agentId);
                if (agent != null)
                {
                    agentMap[agentId] = agent;
                }
            }
        }

        return agentMap;
    }

    /// <summary>
    /// 从节点提取智能体 ID
    /// </summary>
    private string? ExtractAgentId(WorkflowNode node)
    {
        if (node.Data.TryGetValue("agentId", out var agentIdObj1) && 
            agentIdObj1 is string aid1 && 
            !string.IsNullOrEmpty(aid1))
        {
            return aid1;
        }
        
        if (node.Data.TryGetValue("id", out var agentIdObj2) && 
            agentIdObj2 is string aid2 && 
            !string.IsNullOrEmpty(aid2))
        {
            return aid2;
        }

        return null;
    }

    /// <summary>
    /// 从节点提取智能体名称
    /// </summary>
    private string? ExtractAgentName(WorkflowNode node, Dictionary<string, AgentDefinition> agentMap)
    {
        // 优先使用节点中的名称
        if (node.Data.TryGetValue("agentName", out var agentNameObj) && 
            agentNameObj is string agentName && 
            !string.IsNullOrEmpty(agentName))
        {
            return agentName;
        }
        
        if (node.Data.TryGetValue("name", out var nameObj) && 
            nameObj is string name && 
            !string.IsNullOrEmpty(name))
        {
            return name;
        }

        // 从 agentMap 查找
        var agentId = ExtractAgentId(node);
        if (agentId != null && agentMap.TryGetValue(agentId, out var agent))
        {
            return agent.Name;
        }

        return null;
    }

    /// <summary>
    /// 构建 Action 序列
    /// </summary>
    private async Task<List<Dictionary<string, object>>> BuildActionSequenceAsync(
        WorkflowDefinition workflow,
        List<WorkflowNode> nodes,
        Dictionary<string, AgentDefinition> agentMap)
    {
        var actions = new List<Dictionary<string, object>>();
        var visitedNodes = new HashSet<string>();

        // 找到起始节点
        var startNode = nodes.FirstOrDefault(n => n.Type == WorkflowNodeType.Start);
        if (startNode == null)
        {
            return actions;
        }

        // 从起始节点开始构建动作链
        var firstEdge = workflow.Edges.FirstOrDefault(e => e.Source == startNode.Id);
        if (firstEdge != null)
        {
            await BuildActionChainAsync(firstEdge.Target, workflow, nodes, agentMap, actions, visitedNodes);
        }

        return actions;
    }

    /// <summary>
    /// 递归构建动作链
    /// </summary>
    private async Task BuildActionChainAsync(
        string nodeId,
        WorkflowDefinition workflow,
        List<WorkflowNode> nodes,
        Dictionary<string, AgentDefinition> agentMap,
        List<Dictionary<string, object>> actions,
        HashSet<string> visitedNodes)
    {
        if (string.IsNullOrEmpty(nodeId) || visitedNodes.Contains(nodeId))
        {
            return;
        }

        visitedNodes.Add(nodeId);
        var node = nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node == null)
        {
            return;
        }

        switch (node.Type)
        {
            case WorkflowNodeType.Agent:
                await BuildAgentActionAsync(node, workflow, nodes, agentMap, actions, visitedNodes);
                break;

            case WorkflowNodeType.Condition:
                await BuildConditionActionAsync(node, workflow, nodes, agentMap, actions, visitedNodes);
                break;

            case WorkflowNodeType.End:
                BuildEndActionAsync(node, actions);
                break;
        }
    }

    /// <summary>
    /// 构建智能体动作
    /// </summary>
    private async Task BuildAgentActionAsync(
        WorkflowNode node,
        WorkflowDefinition workflow,
        List<WorkflowNode> nodes,
        Dictionary<string, AgentDefinition> agentMap,
        List<Dictionary<string, object>> actions,
        HashSet<string> visitedNodes)
    {
        var agentName = ExtractAgentName(node, agentMap);
        if (string.IsNullOrEmpty(agentName))
        {
            _logger.LogWarning("Agent node {NodeId} has no agent name, skipping", node.Id);
            return;
        }

        var action = new Dictionary<string, object>
        {
            ["kind"] = "InvokeAzureAgent",
            ["id"] = node.Id,
            ["conversationId"] = "=System.ConversationId",
            ["agent"] = new Dictionary<string, object>
            {
                ["name"] = agentName
            }
            // 移除 output 配置，使用默认的 autoSend=true 行为
        };

        actions.Add(action);

        // 继续处理下一个节点
        var nextEdge = workflow.Edges.FirstOrDefault(e => e.Source == node.Id);
        if (nextEdge != null)
        {
            await BuildActionChainAsync(nextEdge.Target, workflow, nodes, agentMap, actions, visitedNodes);
        }
    }

    /// <summary>
    /// 构建条件动作
    /// </summary>
    private async Task BuildConditionActionAsync(
        WorkflowNode node,
        WorkflowDefinition workflow,
        List<WorkflowNode> nodes,
        Dictionary<string, AgentDefinition> agentMap,
        List<Dictionary<string, object>> actions,
        HashSet<string> visitedNodes)
    {
        var conditionExpression = "true";
        if (node.Data.TryGetValue("condition", out var conditionObj))
        {
            var rawCondition = conditionObj?.ToString() ?? "true";
            conditionExpression = ConvertScribanToPowerFx(rawCondition);
        }

        // 查找 true 和 false 分支
        var trueEdge = workflow.Edges.FirstOrDefault(e => 
            e.Source == node.Id && (e.Condition == "true" || e.SourceHandle == "true"));
        var falseEdge = workflow.Edges.FirstOrDefault(e => 
            e.Source == node.Id && (e.Condition == "false" || e.SourceHandle == "false"));

        // 构建分支动作
        var trueActions = new List<Dictionary<string, object>>();
        var falseActions = new List<Dictionary<string, object>>();

        if (trueEdge != null)
        {
            var trueVisited = new HashSet<string>(visitedNodes);
            await BuildActionChainAsync(trueEdge.Target, workflow, nodes, agentMap, trueActions, trueVisited);
        }

        if (falseEdge != null)
        {
            var falseVisited = new HashSet<string>(visitedNodes);
            await BuildActionChainAsync(falseEdge.Target, workflow, nodes, agentMap, falseActions, falseVisited);
        }

        // 创建 ConditionGroup 动作
        var conditions = new List<Dictionary<string, object>>();
        
        if (trueActions.Any())
        {
            conditions.Add(new Dictionary<string, object>
            {
                ["id"] = $"{node.Id}_true",
                ["condition"] = conditionExpression,
                ["actions"] = trueActions
            });
        }
        
        if (falseActions.Any())
        {
            conditions.Add(new Dictionary<string, object>
            {
                ["id"] = $"{node.Id}_false",
                ["condition"] = $"!({conditionExpression})",
                ["actions"] = falseActions
            });
        }

        if (conditions.Any())
        {
            var conditionGroup = new Dictionary<string, object>
            {
                ["kind"] = "ConditionGroup",
                ["id"] = node.Id,
                ["conditions"] = conditions
            };
            actions.Add(conditionGroup);
        }
    }

    /// <summary>
    /// 构建结束动作
    /// </summary>
    private void BuildEndActionAsync(WorkflowNode node, List<Dictionary<string, object>> actions)
    {
        var message = "工作流执行完成";
        if (node.Data.TryGetValue("message", out var messageObj) && messageObj is string msg)
        {
            message = msg;
        }

        var action = new Dictionary<string, object>
        {
            ["kind"] = "SendActivity",
            ["id"] = node.Id,
            ["activity"] = message
        };
        actions.Add(action);
    }

    /// <summary>
    /// 将 Scriban 表达式转换为 PowerFx 公式
    /// </summary>
    private string ConvertScribanToPowerFx(string scribanExpression)
    {
        if (string.IsNullOrWhiteSpace(scribanExpression))
        {
            return "=true";
        }

        var expression = scribanExpression.Trim();
        
        // 移除 Scriban 语法
        expression = expression.Replace("{{", "").Replace("}}", "").Trim();
        expression = expression.Replace("{%", "").Replace("%}", "").Trim();
        expression = expression.Replace("if ", "").Replace(" then", "").Trim();
        
        // 转换变量引用
        expression = expression.Replace("input.", "turn.input.");
        expression = expression.Replace("user.", "turn.");
        
        // 确保以 = 开头（PowerFx 公式标记）
        if (!expression.StartsWith("="))
        {
            expression = "=" + expression;
        }

        return expression;
    }

    /// <summary>
    /// 使用 Agent Framework 执行工作流（流式返回事件）
    /// </summary>
    public async IAsyncEnumerable<ExecutionEvent> ExecuteWorkflowWithFrameworkAsync(
        string workflowId,
        string userInput,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Agent Framework workflow execution: {WorkflowId}", workflowId);

        // 步骤1: 生成 Agent Framework YAML
        yield return new ExecutionEvent
        {
            Type = ExecutionEventType.WorkflowStarted,
            Message = "Generating Agent Framework YAML...",
            Timestamp = DateTime.UtcNow
        };

        // 使用局部变量存储结果，避免 yield + try-catch 冲突
        string? yaml = null;
        string? yamlPath = null;
        string? errorMessage = null;

        // 步骤1: 生成 YAML
        try
        {
            yaml = await ConvertToAgentFrameworkYamlAsync(workflowId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate YAML for workflow {WorkflowId}", workflowId);
            errorMessage = ex.Message;
        }

        if (errorMessage != null)
        {
            yield return new ExecutionEvent
            {
                Type = ExecutionEventType.WorkflowFailed,
                Message = $"Failed to generate YAML: {errorMessage}",
                Timestamp = DateTime.UtcNow
            };
            yield break;
        }

        // 步骤2: 保存临时 YAML 文件
        yamlPath = Path.Combine(Path.GetTempPath(), $"workflow_{workflowId}_{Guid.NewGuid()}.yaml");
        await File.WriteAllTextAsync(yamlPath, yaml!, cancellationToken);
        
        // 日志输出 YAML 内容以便调试
        _logger.LogInformation("Generated YAML content:\n{YamlContent}", yaml);

        yield return new ExecutionEvent
        {
            Type = ExecutionEventType.LogMessage,
            Message = "Building workflow from YAML...",
            Timestamp = DateTime.UtcNow
        };

        // 步骤3: 获取 workflow 定义
        var workflow = await _workflowRepository.GetByIdAsync(workflowId);
        if (workflow == null)
        {
            yield return new ExecutionEvent
            {
                Type = ExecutionEventType.WorkflowFailed,
                Message = $"Workflow {workflowId} not found",
                Timestamp = DateTime.UtcNow
            };

            // 清理临时文件
            CleanupTempFile(yamlPath);
            yield break;
        }

        // 步骤4: 构建 workflow
        Workflow? workflowObj = null;
        try
        {
            // 创建 WorkflowAgentProvider
            var agentProvider = new SimpleWorkflowAgentProvider(
                _chatClient, 
                _agentRepository,
                _loggerFactory.CreateLogger<SimpleWorkflowAgentProvider>());
            
            var options = new DeclarativeWorkflowOptions(agentProvider)
            {
                LoggerFactory = _loggerFactory
            };

            workflowObj = DeclarativeWorkflowBuilder.Build<string>(yamlPath, options);
            _logger.LogInformation("Workflow built successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build workflow from YAML");
            errorMessage = ex.Message;
        }

        if (workflowObj == null || errorMessage != null)
        {
            yield return new ExecutionEvent
            {
                Type = ExecutionEventType.WorkflowFailed,
                Message = $"Failed to build workflow: {errorMessage}",
                Timestamp = DateTime.UtcNow
            };
            CleanupTempFile(yamlPath);
            yield break;
        }

        yield return new ExecutionEvent
        {
            Type = ExecutionEventType.LogMessage,
            Message = "Starting workflow execution...",
            Timestamp = DateTime.UtcNow
        };

        // 步骤5: 执行 workflow（不使用 try-catch 以避免与 yield return 冲突）
        StreamingRun? run = null;
        
        _logger.LogInformation("Calling InProcessExecution.StreamAsync with input: {Input}", userInput);
        run = await InProcessExecution.StreamAsync(
            workflowObj,
            userInput,
            cancellationToken: cancellationToken
        );
        _logger.LogInformation("StreamingRun created, starting WatchStreamAsync...");

        var eventCount = 0;
        await foreach (var evt in run.WatchStreamAsync(cancellationToken))
        {
            eventCount++;
            _logger.LogInformation("Received workflow event #{Count}: {EventType}", eventCount, evt.GetType().Name);
            
            // 特殊处理错误事件，打印详细信息
            if (evt is ExecutorFailedEvent failedEvt)
            {
                var exception = failedEvt.Data as Exception;
                _logger.LogError("❌ Executor {ExecutorId} failed: {ErrorMessage}", 
                    failedEvt.ExecutorId, 
                    exception?.Message ?? failedEvt.Data?.ToString() ?? "Unknown error");
                if (exception != null)
                {
                    _logger.LogError("Exception type: {ExceptionType}", exception.GetType().FullName);
                    _logger.LogError("Stack trace: {StackTrace}", exception.StackTrace);
                }
            }
            else if (evt is WorkflowErrorEvent errorEvt)
            {
                var exception = errorEvt.Data as Exception;
                _logger.LogError("❌ Workflow error: {ErrorMessage}", 
                    exception?.Message ?? errorEvt.Data?.ToString() ?? "Unknown error");
                if (exception != null)
                {
                    _logger.LogError("Exception type: {ExceptionType}", exception.GetType().FullName);
                    _logger.LogError("Stack trace: {StackTrace}", exception.StackTrace);
                }
            }
            
            var executionEvent = MapWorkflowEventToExecutionEvent(evt);
            if (executionEvent != null)
            {
                yield return executionEvent;
            }
            else
            {
                _logger.LogDebug("Event {EventType} not mapped to ExecutionEvent", evt.GetType().Name);
            }
        }
        
        _logger.LogInformation("WatchStreamAsync completed, total events: {Count}", eventCount);

        yield return new ExecutionEvent
        {
            Type = ExecutionEventType.WorkflowCompleted,
            Message = "Workflow execution completed successfully",
            Timestamp = DateTime.UtcNow
        };

        // 清理资源
        if (run != null)
        {
            await run.DisposeAsync();
        }
        CleanupTempFile(yamlPath);
    }

    private void CleanupTempFile(string? filePath)
    {
        if (filePath == null) return;

        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogDebug("Deleted temporary YAML file: {Path}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete temporary YAML file: {Path}", filePath);
        }
    }

    /// <summary>
    /// 将 Agent Framework 的 WorkflowEvent 映射到我们的 ExecutionEvent
    /// </summary>
    private ExecutionEvent? MapWorkflowEventToExecutionEvent(WorkflowEvent evt)
    {
        return evt switch
        {
            ExecutorInvokedEvent invokeEvt => new ExecutionEvent
            {
                Type = ExecutionEventType.NodeStarted,
                NodeId = invokeEvt.ExecutorId,
                Message = $"Executor started: {invokeEvt.ExecutorId}",
                Timestamp = DateTime.UtcNow
            },
            ExecutorCompletedEvent completeEvt => new ExecutionEvent
            {
                Type = ExecutionEventType.NodeCompleted,
                NodeId = completeEvt.ExecutorId,
                Message = $"Executor completed: {completeEvt.ExecutorId}",
                Data = completeEvt.Data != null 
                    ? new Dictionary<string, object> { ["result"] = completeEvt.Data.ToString() ?? "" }
                    : null,
                Timestamp = DateTime.UtcNow
            },
            ExecutorFailedEvent failedEvt => new ExecutionEvent
            {
                Type = ExecutionEventType.NodeFailed,
                NodeId = failedEvt.ExecutorId,
                Message = $"Executor failed: {(failedEvt.Data as Exception)?.Message ?? failedEvt.Data?.ToString() ?? "Unknown error"}",
                Data = failedEvt.Data != null
                    ? new Dictionary<string, object> 
                    { 
                        ["error"] = (failedEvt.Data as Exception)?.Message ?? failedEvt.Data.ToString() ?? "",
                        ["exceptionType"] = failedEvt.Data.GetType().Name
                    }
                    : null,
                Timestamp = DateTime.UtcNow
            },
            WorkflowErrorEvent errorEvt => new ExecutionEvent
            {
                Type = ExecutionEventType.WorkflowFailed,
                Message = $"Workflow error: {(errorEvt.Data as Exception)?.Message ?? errorEvt.Data?.ToString() ?? "Unknown error"}",
                Data = errorEvt.Data != null
                    ? new Dictionary<string, object>
                    {
                        ["error"] = (errorEvt.Data as Exception)?.Message ?? errorEvt.Data.ToString() ?? "",
                        ["exceptionType"] = errorEvt.Data.GetType().Name
                    }
                    : null,
                Timestamp = DateTime.UtcNow
            },
            AgentRunUpdateEvent agentEvt => new ExecutionEvent
            {
                Type = ExecutionEventType.ProgressUpdate,
                Message = agentEvt.Update.Text ?? "",
                Timestamp = DateTime.UtcNow
            },
            WorkflowOutputEvent outputEvt => new ExecutionEvent
            {
                Type = ExecutionEventType.LogMessage,
                Message = "Workflow output received",
                Data = outputEvt.Data != null
                    ? new Dictionary<string, object> { ["output"] = outputEvt.Data.ToString() ?? "" }
                    : null,
                Timestamp = DateTime.UtcNow
            },
            MessageActivityEvent msgEvt => new ExecutionEvent
            {
                Type = ExecutionEventType.LogMessage,
                Message = msgEvt.Message ?? "",
                Timestamp = DateTime.UtcNow
            },
            _ => null // 忽略其他事件类型
        };
    }
}
