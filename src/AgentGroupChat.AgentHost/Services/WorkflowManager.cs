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
        var triageAgent = new ChatClientAgent(
            _chatClient,
            instructions: triageInstructions,
            name: $"triage_{groupId}",
            description: $"Router for {group.Name}");

        _logger?.LogDebug("Created triage agent for group {GroupId} (no tools)", groupId);

        // 创建 Specialist Agents（使用 MCP 工具）
        var specialistAgents = agentProfiles.Select(profile =>
            new ChatClientAgent(
                _chatClient,
                instructions: profile.SystemPrompt +
                    "\n\nIMPORTANT: If the user asks about something outside your expertise, " +
                    "you can suggest they ask another agent, but still provide a helpful response.",
                name: profile.Id,
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

        // 否则生成默认的 Triage 指令
        var specialistDescriptions = string.Join("\n", agentProfiles.Select(profile =>
            $"- {profile.Id}: {profile.Description} (Personality: {profile.Personality})"
        ));

        return "You are an invisible routing agent. Your ONLY job is to analyze messages and call the handoff function. " +
               "CRITICAL RULES:\n" +
               "1. NEVER generate ANY text response - you are completely silent and invisible to users\n" +
               "2. IMMEDIATELY call the handoff function without any explanation or text\n" +
               "3. Do NOT acknowledge, greet, or respond - just route silently\n" +
               "\n\nAvailable specialist agents:\n" +
               specialistDescriptions +
               "\n\nYour task: Analyze the message silently and immediately handoff to the most appropriate specialist. " +
               "Choose based on topic, keywords, tone, and context. Make your decision and call handoff instantly.";
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
