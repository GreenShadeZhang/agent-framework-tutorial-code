using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows.Declarative;
using Microsoft.Extensions.AI;
using WorkflowDesigner.Api.Models;
using WorkflowDesigner.Api.Repository;

namespace WorkflowDesigner.Api.Services;

/// <summary>
/// 简化的 WorkflowAgentProvider 实现
/// 用于本地 OpenAI 模型，不依赖 Azure AI Foundry
/// </summary>
public class SimpleWorkflowAgentProvider : WorkflowAgentProvider
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<SimpleWorkflowAgentProvider> _logger;
    private readonly IRepository<AgentDefinition> _agentRepository;
    
    // 存储每个conversation的消息
    private readonly Dictionary<string, List<ChatMessage>> _conversationMessages = new();
    
    // 缓存 agent 定义
    private readonly Dictionary<string, AgentDefinition> _agentCache = new();

    public SimpleWorkflowAgentProvider(
        IChatClient chatClient, 
        IRepository<AgentDefinition> agentRepository,
        ILogger<SimpleWorkflowAgentProvider> logger)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 创建会话
    /// </summary>
    public override Task<string> CreateConversationAsync(CancellationToken cancellationToken = default)
    {
        // 生成唯一的会话 ID
        var conversationId = Guid.NewGuid().ToString();
        
        // 初始化消息列表
        _conversationMessages[conversationId] = new List<ChatMessage>();
        
        _logger.LogInformation("Created conversation: {ConversationId}", conversationId);
        return Task.FromResult(conversationId);
    }

    /// <summary>
    /// 创建消息
    /// </summary>
    public override Task<ChatMessage> CreateMessageAsync(
        string conversationId, 
        ChatMessage conversationMessage, 
        CancellationToken cancellationToken = default)
    {
        // 将消息添加到会话消息列表中
        if (_conversationMessages.TryGetValue(conversationId, out var messages))
        {
            messages.Add(conversationMessage);
        }
        
        _logger.LogInformation("Created message in conversation {ConversationId}: {Role} - {Content}", 
            conversationId, conversationMessage.Role, conversationMessage.Text);
        return Task.FromResult(conversationMessage);
    }

    /// <summary>
    /// 获取单条消息
    /// </summary>
    public override Task<ChatMessage> GetMessageAsync(
        string conversationId, 
        string messageId, 
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Message retrieval is not supported in this simplified provider");
    }

    /// <summary>
    /// 获取会话中的所有消息
    /// </summary>
    public override async IAsyncEnumerable<ChatMessage> GetMessagesAsync(
        string conversationId,
        int? limit = null,
        string? after = null,
        string? before = null,
        bool newestFirst = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("📋 GetMessagesAsync called for conversation {ConversationId}", conversationId);
        
        // 从存储中获取消息
        if (_conversationMessages.TryGetValue(conversationId, out var messages))
        {
            _logger.LogInformation("  Found {Count} messages in conversation", messages.Count);
            foreach (var msg in messages)
            {
                yield return msg;
            }
        }
        else
        {
            _logger.LogInformation("  No messages found in conversation");
        }
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// 调用 Agent 并流式返回结果
    /// </summary>
    public override async IAsyncEnumerable<AgentRunResponseUpdate> InvokeAgentAsync(
        string agentId,
        string? agentVersion,
        string? conversationId,
        IEnumerable<ChatMessage>? messages,
        IDictionary<string, object?>? inputArguments,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Invoking agent {AgentId} in conversation {ConversationId}", 
            agentId, conversationId ?? "default");

        // 获取 agent 定义（包含 instructions）
        AgentDefinition? agentDef = await GetAgentDefinitionAsync(agentId);
        string? systemPrompt = agentDef?.InstructionsTemplate;
        
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            _logger.LogInformation("📋 Using agent instructions: {Instructions}", 
                systemPrompt.Length > 100 ? systemPrompt.Substring(0, 100) + "..." : systemPrompt);
        }

        // 构建消息列表
        List<ChatMessage> chatMessages = new List<ChatMessage>();
        
        // 1. 添加 system prompt（如果有）
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            chatMessages.Add(new ChatMessage(ChatRole.System, systemPrompt));
        }
        
        // 2. 添加历史消息（不包括 system messages）
        if (conversationId != null && _conversationMessages.TryGetValue(conversationId, out var convMessages))
        {
            var historyMessages = convMessages.Where(m => m.Role != ChatRole.System).ToList();
            chatMessages.AddRange(historyMessages);
            _logger.LogInformation("📚 Loaded {Count} historical messages from conversation", historyMessages.Count);
        }
        
        // 3. 添加新的输入消息
        if (messages != null)
        {
            chatMessages.AddRange(messages.Where(m => m.Role != ChatRole.System));
        }
        
        // 如果没有任何用户消息，添加默认消息
        if (!chatMessages.Any(m => m.Role == ChatRole.User))
        {
            chatMessages.Add(new ChatMessage(ChatRole.User, "Hello"));
        }

        _logger.LogInformation("💬 Sending {Count} messages to ChatClient for agent {AgentId}", chatMessages.Count, agentId);
        foreach (var msg in chatMessages)
        {
            var preview = msg.Text.Length > 50 ? msg.Text.Substring(0, 50) + "..." : msg.Text;
            _logger.LogInformation("  - {Role}: {Text}", msg.Role, preview);
        }

        // 调用 ChatClient（非流式）
        _logger.LogInformation("🤖 Calling ChatClient.GetResponseAsync for agent {AgentId}...", agentId);
        var response = await _chatClient.GetResponseAsync(chatMessages, cancellationToken: cancellationToken);
        _logger.LogInformation("✅ ChatClient returned response: {ResponseText}", response.Text);
        
        // 生成唯一ID和时间戳
        var responseId = Guid.NewGuid().ToString();
        var messageId = Guid.NewGuid().ToString();
        var createdAt = DateTimeOffset.UtcNow;
        
        var assistantMessage = new ChatMessage(ChatRole.Assistant, response.Text)
        {
            MessageId = messageId,
            CreatedAt = createdAt
        };
        
        // 存储assistant响应到conversation（不存储system message）
        if (conversationId != null && _conversationMessages.TryGetValue(conversationId, out var convMessagesForUpdate))
        {
            convMessagesForUpdate.Add(assistantMessage);
            _logger.LogInformation("💾 Stored assistant message in conversation {ConversationId}", conversationId);
        }
        
        // 返回完整的 AgentRunResponseUpdate（包含所有必需属性）
        _logger.LogInformation("📤 Yielding AgentRunResponseUpdate: responseId={ResponseId}, messageId={MessageId}, createdAt={CreatedAt}", 
            responseId, messageId, createdAt);
        yield return new AgentRunResponseUpdate(ChatRole.Assistant, response.Text)
        {
            ResponseId = responseId,
            MessageId = messageId,
            CreatedAt = createdAt,
            AuthorName = agentDef?.Name ?? agentId,
            AgentId = agentId,
        };
        _logger.LogInformation("✅ InvokeAgentAsync completed for agent {AgentId}", agentId);
    }
    
    /// <summary>
    /// 获取 Agent 定义（带缓存）
    /// </summary>
    private async Task<AgentDefinition?> GetAgentDefinitionAsync(string agentNameOrId)
    {
        // 先查缓存
        if (_agentCache.TryGetValue(agentNameOrId, out var cachedAgent))
        {
            return cachedAgent;
        }
        
        // 从数据库查找（先按 Name 查找，再按 ID 查找）
        var allAgents = await _agentRepository.GetAllAsync();
        var agent = allAgents.FirstOrDefault(a => 
            a.Name.Equals(agentNameOrId, StringComparison.OrdinalIgnoreCase) || 
            a.Id == agentNameOrId);
        
        if (agent != null)
        {
            // 缓存结果
            _agentCache[agentNameOrId] = agent;
            _logger.LogInformation("📦 Cached agent definition: {AgentId} -> {AgentName}", agentNameOrId, agent.Name);
        }
        else
        {
            _logger.LogWarning("⚠️ Agent not found: {AgentId}", agentNameOrId);
        }
        
        return agent;
    }
}
