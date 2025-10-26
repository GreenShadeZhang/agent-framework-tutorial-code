using AgentGroupChat.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentGroupChat.AgentHost.Services;

/// <summary>
/// 工作流管理服务
/// 负责根据智能体组动态创建和管理 Handoff Workflows
/// </summary>
public class WorkflowManager
{
    private readonly IChatClient _chatClient;
    private readonly AgentRepository _agentRepository;
    private readonly AgentGroupRepository _groupRepository;
    private readonly McpToolService _mcpToolService;
    private readonly ILogger<WorkflowManager>? _logger;
    
    // 缓存已创建的 workflows（key: groupId）
    private readonly Dictionary<string, Workflow> _workflowCache = new();

    public WorkflowManager(
        IChatClient chatClient,
        AgentRepository agentRepository,
        AgentGroupRepository groupRepository,
        McpToolService mcpToolService,
        ILogger<WorkflowManager>? logger = null)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _groupRepository = groupRepository ?? throw new ArgumentNullException(nameof(groupRepository));
        _mcpToolService = mcpToolService ?? throw new ArgumentNullException(nameof(mcpToolService));
        _logger = logger;
    }

    /// <summary>
    /// 获取或创建指定组的 Workflow
    /// </summary>
    public Workflow GetOrCreateWorkflow(string groupId)
    {
        // 检查缓存
        if (_workflowCache.TryGetValue(groupId, out var cachedWorkflow))
        {
            _logger?.LogDebug("Using cached workflow for group {GroupId}", groupId);
            return cachedWorkflow;
        }

        // 创建新的 workflow
        var workflow = CreateWorkflow(groupId);
        _workflowCache[groupId] = workflow;
        
        _logger?.LogInformation("Created and cached new workflow for group {GroupId}", groupId);
        return workflow;
    }

    /// <summary>
    /// 创建基于智能体组的 Handoff Workflow
    /// </summary>
    private Workflow CreateWorkflow(string groupId)
    {
        // 获取组配置
        var group = _groupRepository.GetById(groupId);
        if (group == null)
        {
            throw new InvalidOperationException($"Agent group {groupId} not found");
        }

        if (!group.Enabled)
        {
            throw new InvalidOperationException($"Agent group {groupId} is disabled");
        }

        // 加载组内的智能体
        var agentProfiles = new List<AgentProfile>();
        foreach (var agentId in group.AgentIds)
        {
            var persistedAgent = _agentRepository.GetById(agentId);
            if (persistedAgent != null && persistedAgent.Enabled)
            {
                agentProfiles.Add(persistedAgent.ToAgentProfile());
            }
            else
            {
                _logger?.LogWarning("Agent {AgentId} not found or disabled, skipping", agentId);
            }
        }

        if (agentProfiles.Count == 0)
        {
            throw new InvalidOperationException($"No enabled agents found in group {groupId}");
        }

        _logger?.LogInformation("Creating workflow for group {GroupId} with {AgentCount} agents: {AgentNames}",
            groupId, agentProfiles.Count, string.Join(", ", agentProfiles.Select(a => a.Name)));

        // 获取所有 MCP 工具
        var mcpTools = _mcpToolService.GetAllTools().ToList();
        _logger?.LogInformation("Loaded {McpToolCount} MCP tools for specialist agents", mcpTools.Count);

        // 生成 Triage Agent 的指令
        var triageInstructions = GenerateTriageInstructions(group, agentProfiles);

        // 创建 Triage Agent（不使用 MCP 工具，只负责路由）
        // ⚠️ 重要：Agent name 必须简短，因为框架会基于它生成 handoff 函数名
        // OpenAI API 限制工具函数名最多 64 字符
        // 格式：handoff_to_{agent_name}_{guid} 
        // 所以 agent_name 应该尽量短（建议 < 20 字符）
        var triageAgent = new ChatClientAgent(
            _chatClient,
            instructions: triageInstructions,
            name: $"triage",  // 简化名称，不包含 groupId
            description: $"Router for {group.Name}");

        _logger?.LogDebug("Created triage agent for group {GroupId} (no tools)", groupId);

        // 创建 Specialist Agents（使用 MCP 工具）
        // ⚠️ 同样的原因，使用简短的 agent name
        var specialistAgents = agentProfiles.Select(profile =>
            new ChatClientAgent(
                _chatClient,
                instructions: profile.SystemPrompt +
                    "\n\n重要提示：如果用户询问超出你专业领域的问题，" +
                    "你可以建议他们询问其他智能体，但仍需提供有帮助的回答。" +
                    "\n\n使用工具时：" +
                    "\n- 调用工具后，务必等待结果并将其融入你的回复中。" +
                    "\n- 以清晰、用户友好的格式呈现工具结果。" +
                    "\n- 对于图像URL，描述生成的内容并提供链接。" +
                    "\n- 对于数据结果，以易读的方式总结关键信息。" +
                    "\n- 永远不要只返回原始工具输出 - 始终添加上下文和解释。",
                name: profile.Id,  // 使用简短的 ID（如 "anna", "elena"）
                description: profile.Description,
                tools: [.. mcpTools])  // 为 Specialist Agents 添加 MCP 工具
        ).ToList();

        _logger?.LogInformation("Created {SpecialistCount} specialist agents for group {GroupId} with {McpToolCount} MCP tools each",
            specialistAgents.Count, groupId, mcpTools.Count);

        // 使用 AgentWorkflowBuilder 构建 Handoff Workflow
        var builder = AgentWorkflowBuilder.CreateHandoffBuilderWith(triageAgent);

        // 配置 handoff 路径
        builder.WithHandoffs(triageAgent, specialistAgents)
               .WithHandoffs(specialistAgents, triageAgent);

        var workflow = builder.Build();

        _logger?.LogInformation("Workflow built successfully for group {GroupId}", groupId);

        return workflow;
    }

    /// <summary>
    /// 生成 Triage Agent 的系统指令
    /// </summary>
    private string GenerateTriageInstructions(AgentGroup group, List<AgentProfile> agentProfiles)
    {
        // 如果组有自定义 Triage 提示词，使用它
        if (!string.IsNullOrWhiteSpace(group.TriageSystemPrompt))
        {
            return group.TriageSystemPrompt;
        }

        // 否则生成默认的 Triage 指令（中文版）
        var specialistDescriptions = string.Join("\n", agentProfiles.Select(profile =>
            $"- {profile.Id}（{profile.Name}）：{profile.Description}（性格：{profile.Personality}）"
        ));

        return "你是一个透明的路由智能体。你的唯一任务是分析消息并调用handoff函数。\n" +
               "关键规则：\n" +
               "1. 永远不要生成任何文本回复 - 你对用户完全透明和不可见\n" +
               "2. 立即调用handoff函数，不需要任何解释或文本\n" +
               "3. 不要确认、问候或回应 - 只是默默地路由\n" +
               "\n可用的专家智能体：\n" +
               specialistDescriptions +
               "\n\n你的任务：默默分析消息，然后立即将其转交给最合适的专家。" +
               "根据话题、关键词、语气和上下文进行选择。做出决定后立即调用handoff。";
    }

    /// <summary>
    /// 清除指定组的 workflow 缓存
    /// </summary>
    public void ClearWorkflowCache(string groupId)
    {
        if (_workflowCache.Remove(groupId))
        {
            _logger?.LogInformation("Cleared workflow cache for group {GroupId}", groupId);
        }
    }

    /// <summary>
    /// 清除所有 workflow 缓存
    /// </summary>
    public void ClearAllWorkflowCache()
    {
        _workflowCache.Clear();
        _logger?.LogInformation("Cleared all workflow cache");
    }

    /// <summary>
    /// 获取所有可用的组信息
    /// </summary>
    public List<AgentGroup> GetAvailableGroups()
    {
        return _groupRepository.GetAllEnabled();
    }
}
