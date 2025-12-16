using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using WorkflowDesigner.Api.Models;
using Scriban;
using WorkflowDesigner.Api.Repository;

namespace WorkflowDesigner.Api.Services;

/// <summary>
/// 工作流执行器 - 使用 Agent Framework 执行工作流
/// </summary>
public class WorkflowExecutor
{
    private readonly IChatClient _chatClient;
    private readonly IRepository<AgentDefinition> _agentRepository;
    private readonly ILogger<WorkflowExecutor> _logger;

    public WorkflowExecutor(
        IChatClient chatClient,
        IRepository<AgentDefinition> agentRepository,
        ILogger<WorkflowExecutor> logger)
    {
        _chatClient = chatClient;
        _agentRepository = agentRepository;
        _logger = logger;
    }

    /// <summary>
    /// 执行工作流并流式返回结果
    /// </summary>
    public async IAsyncEnumerable<WorkflowExecutionEvent> ExecuteWorkflowAsync(
        WorkflowDefinition workflow,
        Dictionary<string, object> parameters)
    {
        _logger.LogInformation("开始执行工作流: {WorkflowName}", workflow.Name);

        // 查找起始节点
        var startNode = workflow.Nodes.FirstOrDefault(n => n.Type == WorkflowNodeType.Start);
        if (startNode == null)
        {
            yield return new WorkflowExecutionEvent
            {
                Type = "error",
                Message = "工作流没有起始节点"
            };
            yield break;
        }

        // 查找第一个执行节点
        var firstEdge = workflow.Edges.FirstOrDefault(e => e.Source == startNode.Id);
        if (firstEdge == null)
        {
            yield return new WorkflowExecutionEvent
            {
                Type = "error",
                Message = "起始节点没有连接到任何节点"
            };
            yield break;
        }

        // 初始化执行上下文
        var context = new WorkflowExecutionContext
        {
            Parameters = parameters,
            NodeOutputs = new Dictionary<string, object>()
        };

        yield return new WorkflowExecutionEvent
        {
            Type = "start",
            Message = $"开始执行工作流: {workflow.Name}",
            NodeId = startNode.Id
        };

        // 从第一个节点开始执行
        var currentNodeId = firstEdge.Target;
        
        while (!string.IsNullOrEmpty(currentNodeId))
        {
            var node = workflow.Nodes.FirstOrDefault(n => n.Id == currentNodeId);
            if (node == null)
            {
                yield return new WorkflowExecutionEvent
                {
                    Type = "error",
                    Message = $"节点 {currentNodeId} 不存在"
                };
                yield break;
            }

            _logger.LogInformation("执行节点: {NodeId} ({NodeType})", node.Id, node.Type);

            // 根据节点类型执行
            switch (node.Type)
            {
                case WorkflowNodeType.Agent:
                    await foreach (var evt in ExecuteAgentNodeAsync(node, workflow, context))
                    {
                        yield return evt;
                        if (evt.Type == "agent_response")
                        {
                            currentNodeId = evt.NextNodeId;
                        }
                    }
                    break;

                case WorkflowNodeType.Condition:
                    var conditionResult = await ExecuteConditionNodeAsync(node, workflow, context);
                    yield return conditionResult;
                    currentNodeId = conditionResult.NextNodeId;
                    break;

                case WorkflowNodeType.End:
                    yield return new WorkflowExecutionEvent
                    {
                        Type = "end",
                        Message = "工作流执行完成",
                        NodeId = node.Id
                    };
                    currentNodeId = null;
                    break;

                default:
                    yield return new WorkflowExecutionEvent
                    {
                        Type = "error",
                        Message = $"不支持的节点类型: {node.Type}"
                    };
                    yield break;
            }
        }

        _logger.LogInformation("工作流执行完成: {WorkflowName}", workflow.Name);
    }

    /// <summary>
    /// 执行智能体节点
    /// </summary>
    private async IAsyncEnumerable<WorkflowExecutionEvent> ExecuteAgentNodeAsync(
        WorkflowNode node,
        WorkflowDefinition workflow,
        WorkflowExecutionContext context)
    {
        yield return new WorkflowExecutionEvent
        {
            Type = "node_start",
            Message = $"开始执行智能体节点",
            NodeId = node.Id
        };

        // 获取智能体ID
        string? agentId = null;
        if (node.Data.TryGetValue("agentId", out var aid1) && aid1 is string a1 && !string.IsNullOrEmpty(a1))
        {
            agentId = a1;
        }
        else if (node.Data.TryGetValue("id", out var aid2) && aid2 is string a2 && !string.IsNullOrEmpty(a2))
        {
            agentId = a2;
        }

        if (string.IsNullOrEmpty(agentId))
        {
            yield return new WorkflowExecutionEvent
            {
                Type = "error",
                Message = "智能体节点未配置智能体ID",
                NodeId = node.Id
            };
            yield break;
        }

        // 加载智能体定义
        var agent = await _agentRepository.GetByIdAsync(agentId);
        if (agent == null)
        {
            yield return new WorkflowExecutionEvent
            {
                Type = "error",
                Message = $"智能体 {agentId} 不存在",
                NodeId = node.Id
            };
            yield break;
        }

        // 准备输入
        var inputVariables = node.Data.TryGetValue("inputVariables", out var inVars) && inVars is List<string> vars
            ? vars
            : new List<string>();

        var inputContext = new Dictionary<string, object>();
        foreach (var varName in inputVariables)
        {
            if (context.Parameters.TryGetValue(varName, out var paramValue))
            {
                inputContext[varName] = paramValue;
            }
            else if (context.NodeOutputs.TryGetValue(varName, out var nodeOutput))
            {
                inputContext[varName] = nodeOutput;
            }
        }

        // 渲染提示词
        var instructions = node.Data.TryGetValue("instructionsTemplate", out var customInstr) && customInstr is string ci && !string.IsNullOrEmpty(ci)
            ? ci
            : agent.InstructionsTemplate;

        var template = Template.Parse(instructions);
        var renderedInstructions = await template.RenderAsync(inputContext);

        // 构建用户消息
        var userMessage = context.Parameters.TryGetValue("user_input", out var userInput)
            ? userInput.ToString() ?? ""
            : "";

        // 创建 Agent Framework Agent
        var chatAgent = new ChatClientAgent(
            _chatClient,
            instructions: renderedInstructions,
            name: agent.Name,
            description: agent.Description
        );

        _logger.LogInformation("调用智能体: {AgentName}, 指令长度: {InstructionsLength}", 
            agent.Name, renderedInstructions.Length);

        // 执行智能体
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, userMessage)
        };

        WorkflowExecutionEvent? resultEvent = null;
        
        try
        {
            // 使用 GetResponseAsync 而不是 InvokeAsync
            var response = await _chatClient.GetResponseAsync(messages);
            var responseText = response.ToString() ?? "";

            // 保存输出
            var outputVariables = node.Data.TryGetValue("outputVariables", out var outVars) && outVars is List<string> ovars
                ? ovars
                : new List<string> { "result" };

            foreach (var varName in outputVariables)
            {
                context.NodeOutputs[varName] = responseText;
            }

            resultEvent = new WorkflowExecutionEvent
            {
                Type = "agent_response",
                Message = responseText,
                NodeId = node.Id,
                AgentName = agent.Name,
                NextNodeId = FindNextNodeId(node.Id, workflow)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行智能体节点失败: {NodeId}", node.Id);
            resultEvent = new WorkflowExecutionEvent
            {
                Type = "error",
                Message = $"执行智能体失败: {ex.Message}",
                NodeId = node.Id
            };
        }

        if (resultEvent != null)
        {
            yield return resultEvent;
        }
    }

    /// <summary>
    /// 执行条件节点
    /// </summary>
    private async Task<WorkflowExecutionEvent> ExecuteConditionNodeAsync(
        WorkflowNode node,
        WorkflowDefinition workflow,
        WorkflowExecutionContext context)
    {
        var condition = node.Data.TryGetValue("condition", out var cond) && cond is string c
            ? c
            : "";

        if (string.IsNullOrEmpty(condition))
        {
            return new WorkflowExecutionEvent
            {
                Type = "error",
                Message = "条件节点未配置条件表达式",
                NodeId = node.Id
            };
        }

        // 简单的条件评估 (TODO: 使用更强大的表达式引擎)
        var template = Template.Parse("{{ " + condition + " }}");
        var result = await template.RenderAsync(context.NodeOutputs);
        var isTrue = result.Trim().ToLower() == "true";

        // 查找对应的边
        var nextEdge = workflow.Edges.FirstOrDefault(e =>
            e.Source == node.Id && (isTrue
                ? (e.Condition == "true" || e.SourceHandle == "true")
                : (e.Condition == "false" || e.SourceHandle == "false")));

        return new WorkflowExecutionEvent
        {
            Type = "condition_evaluated",
            Message = $"条件评估: {condition} = {isTrue}",
            NodeId = node.Id,
            NextNodeId = nextEdge?.Target
        };
    }

    /// <summary>
    /// 查找下一个节点ID
    /// </summary>
    private string? FindNextNodeId(string currentNodeId, WorkflowDefinition workflow)
    {
        var edge = workflow.Edges.FirstOrDefault(e => e.Source == currentNodeId);
        return edge?.Target;
    }
}

/// <summary>
/// 工作流执行上下文
/// </summary>
public class WorkflowExecutionContext
{
    public Dictionary<string, object> Parameters { get; set; } = new();
    public Dictionary<string, object> NodeOutputs { get; set; } = new();
}

/// <summary>
/// 工作流执行事件
/// </summary>
public class WorkflowExecutionEvent
{
    public string Type { get; set; } = "";
    public string Message { get; set; } = "";
    public string? NodeId { get; set; }
    public string? AgentName { get; set; }
    public string? NextNodeId { get; set; }
}
