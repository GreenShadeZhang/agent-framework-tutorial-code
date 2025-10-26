using System.Text.Json;
using AgentGroupChat.Models;
using LiteDB;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;
using SysJsonSerializer = System.Text.Json.JsonSerializer;

namespace AgentGroupChat.AgentHost.Services;

/// <summary>
/// LiteDB 实现的 ChatMessageStore
/// 参考 Agent Framework Step07 的 VectorChatMessageStore 设计
/// 将消息存储在独立的 LiteDB 集合中，Thread 序列化时只保存 SessionId
/// </summary>
public class LiteDbChatMessageStore : ChatMessageStore
{
    private readonly ILiteCollection<PersistedChatMessage> _messagesCollection;
    private readonly ILogger<LiteDbChatMessageStore>? _logger;

    /// <summary>
    /// 会话 ID（用于存储和查询消息的键）
    /// </summary>
    public string SessionId { get; private set; }

    /// <summary>
    /// Agent ID（用于标识消息来源）
    /// </summary>
    public string AgentId { get; private set; }

    /// <summary>
    /// Agent 名称（用于显示）
    /// </summary>
    public string AgentName { get; private set; }

    /// <summary>
    /// Agent 头像（用于显示）
    /// </summary>
    public string AgentAvatar { get; private set; }

    /// <summary>
    /// 构造函数（用于新建 Thread）
    /// </summary>
    public LiteDbChatMessageStore(
        ILiteCollection<PersistedChatMessage> messagesCollection,
        string sessionId,
        string agentId = "assistant",
        string agentName = "Assistant",
        string agentAvatar = "🤖",
        ILogger<LiteDbChatMessageStore>? logger = null)
    {
        _messagesCollection = messagesCollection ?? throw new ArgumentNullException(nameof(messagesCollection));
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        AgentId = agentId ?? "assistant";
        AgentName = agentName ?? "Assistant";
        AgentAvatar = agentAvatar ?? "🤖";
        _logger = logger;

        _logger?.LogDebug("Created LiteDbChatMessageStore for session {SessionId} with Agent {AgentName}", 
            SessionId, AgentName);
    }

    /// <summary>
    /// 构造函数（用于从序列化状态恢复）
    /// </summary>
    public LiteDbChatMessageStore(
        ILiteCollection<PersistedChatMessage> messagesCollection,
        JsonElement serializedStoreState,
        ILogger<LiteDbChatMessageStore>? logger = null)
    {
        _messagesCollection = messagesCollection ?? throw new ArgumentNullException(nameof(messagesCollection));
        _logger = logger;

        // 从序列化状态恢复 SessionId 和 Agent 信息
        if (serializedStoreState.ValueKind is JsonValueKind.Object)
        {
            SessionId = serializedStoreState.GetProperty("sessionId").GetString() 
                ?? throw new InvalidOperationException("Failed to deserialize SessionId from serialized state");
            
            // 恢复 Agent 信息
            AgentId = serializedStoreState.TryGetProperty("agentId", out var agentIdProp) 
                ? (agentIdProp.GetString() ?? "assistant") 
                : "assistant";
            AgentName = serializedStoreState.TryGetProperty("agentName", out var agentNameProp) 
                ? (agentNameProp.GetString() ?? "Assistant") 
                : "Assistant";
            AgentAvatar = serializedStoreState.TryGetProperty("agentAvatar", out var agentAvatarProp) 
                ? (agentAvatarProp.GetString() ?? "🤖") 
                : "🤖";
            
            _logger?.LogDebug("Restored LiteDbChatMessageStore for session {SessionId} with Agent {AgentName}", 
                SessionId, AgentName);
        }
        else
        {
            throw new InvalidOperationException("Invalid serialized state format");
        }
    }

    /// <summary>
    /// 添加消息到 LiteDB
    /// </summary>
    public override async Task AddMessagesAsync(
        IEnumerable<AIChatMessage> messages, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var persistedMessages = messages.Select(msg => 
            {
                var isUserMessage = msg.Role.ToString().Equals("user", StringComparison.OrdinalIgnoreCase);
                
                return new PersistedChatMessage
                {
                    Id = $"{SessionId}_{msg.MessageId}",
                    SessionId = SessionId,
                    MessageId = msg.MessageId ?? Guid.NewGuid().ToString(),
                    Timestamp = DateTimeOffset.UtcNow,
                    SerializedMessage = SysJsonSerializer.Serialize(msg),
                    MessageText = msg.Text,
                    Role = msg.Role.ToString(),
                    
                    // ✅ 修复：正确填充 Agent 信息
                    AgentId = isUserMessage ? "user" : AgentId,
                    AgentName = isUserMessage ? "User" : AgentName,
                    AgentAvatar = isUserMessage ? "👤" : AgentAvatar,
                    
                    IsUser = isUserMessage,
                    
                    // 尝试从消息内容中提取图片 URL
                    ImageUrl = ExtractImageUrl(msg)
                };
            }).ToList();

            // LiteDB 的 Upsert 操作（插入或更新）
            await Task.Run(() =>
            {
                foreach (var msg in persistedMessages)
                {
                    _messagesCollection.Upsert(msg);
                }
            }, cancellationToken);

            _logger?.LogDebug("Added {Count} messages to session {SessionId} (Agent: {AgentName})", 
                persistedMessages.Count, SessionId, AgentName);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error adding messages to session {SessionId}", SessionId);
            throw;
        }
    }

    /// <summary>
    /// 从消息中提取图片 URL
    /// </summary>
    private string? ExtractImageUrl(AIChatMessage msg)
    {
        // 检查 AdditionalProperties
        if (msg.AdditionalProperties?.TryGetValue("imageUrl", out var imageUrl) == true)
        {
            return imageUrl?.ToString();
        }
        
        // TODO: 检查 Contents 中是否有图片内容（需要添加 using Microsoft.Extensions.AI）
        // 暂时返回 null，图片 URL 通过 AdditionalProperties 传递
        
        return null;
    }

    /// <summary>
    /// 从 LiteDB 获取消息
    /// </summary>
    public override async Task<IEnumerable<AIChatMessage>> GetMessagesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var persistedMessages = await Task.Run(() =>
            {
                return _messagesCollection
                    .Find(m => m.SessionId == SessionId)
                    .OrderBy(m => m.Timestamp)
                    .ToList();
            }, cancellationToken);

            var messages = persistedMessages
                .Select(pm => SysJsonSerializer.Deserialize<AIChatMessage>(pm.SerializedMessage)!)
                .Where(m => m != null)
                .ToList();

            _logger?.LogDebug("Retrieved {Count} messages from session {SessionId}", 
                messages.Count, SessionId);

            return messages;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting messages from session {SessionId}", SessionId);
            return Enumerable.Empty<AIChatMessage>();
        }
    }

    /// <summary>
    /// 序列化存储状态（保存 SessionId 和 Agent 信息）
    /// 这是关键：不序列化消息本身，只序列化会话和 Agent 元数据
    /// </summary>
    public override JsonElement Serialize(JsonSerializerOptions? jsonSerializerOptions = null)
    {
        _logger?.LogDebug("Serializing store state for session {SessionId} with Agent {AgentName}", 
            SessionId, AgentName);
        
        // 序列化 SessionId 和 Agent 信息
        var state = new Dictionary<string, string>
        {
            ["sessionId"] = SessionId,
            ["agentId"] = AgentId,
            ["agentName"] = AgentName,
            ["agentAvatar"] = AgentAvatar
        };
        
        return SysJsonSerializer.SerializeToElement(state, jsonSerializerOptions);
    }

    /// <summary>
    /// 获取消息总数（用于统计）
    /// </summary>
    public int GetMessageCount()
    {
        return _messagesCollection.Count(m => m.SessionId == SessionId);
    }

    /// <summary>
    /// 清除会话的所有消息
    /// </summary>
    public async Task ClearMessagesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.Run(() =>
            {
                _messagesCollection.DeleteMany(m => m.SessionId == SessionId);
            }, cancellationToken);

            _logger?.LogInformation("Cleared all messages for session {SessionId}", SessionId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error clearing messages for session {SessionId}", SessionId);
            throw;
        }
    }

    /// <summary>
    /// 获取消息摘要列表（用于 UI 展示）
    /// </summary>
    public List<ChatMessageSummary> GetMessageSummaries()
    {
        try
        {
            var messages = _messagesCollection
                .Find(m => m.SessionId == SessionId)
                .OrderBy(m => m.Timestamp)
                .ToList();

            return messages.Select(pm => new ChatMessageSummary
            {
                AgentId = pm.AgentId ?? "user",
                AgentName = pm.AgentName ?? "User",
                AgentAvatar = pm.AgentAvatar ?? (pm.IsUser ? "👤" : "🤖"),
                Content = pm.MessageText ?? string.Empty,
                ImageUrl = pm.ImageUrl,
                IsUser = pm.IsUser,
                Timestamp = pm.Timestamp.UtcDateTime,
                MessageType = string.IsNullOrEmpty(pm.ImageUrl) ? "text" : "image"
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting message summaries for session {SessionId}", SessionId);
            return new List<ChatMessageSummary>();
        }
    }
}
