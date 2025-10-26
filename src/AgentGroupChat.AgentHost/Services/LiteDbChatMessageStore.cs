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
    /// 构造函数（用于新建 Thread）
    /// </summary>
    public LiteDbChatMessageStore(
        ILiteCollection<PersistedChatMessage> messagesCollection,
        string sessionId,
        ILogger<LiteDbChatMessageStore>? logger = null)
    {
        _messagesCollection = messagesCollection ?? throw new ArgumentNullException(nameof(messagesCollection));
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        _logger = logger;

        _logger?.LogDebug("Created LiteDbChatMessageStore for session {SessionId}", SessionId);
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

        // 从序列化状态恢复 SessionId
        if (serializedStoreState.ValueKind is JsonValueKind.String)
        {
            SessionId = serializedStoreState.Deserialize<string>() 
                ?? throw new InvalidOperationException("Failed to deserialize SessionId from serialized state");
            
            _logger?.LogDebug("Restored LiteDbChatMessageStore for session {SessionId}", SessionId);
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
            var persistedMessages = messages.Select(msg => new PersistedChatMessage
            {
                Id = $"{SessionId}_{msg.MessageId}",
                SessionId = SessionId,
                MessageId = msg.MessageId ?? Guid.NewGuid().ToString(),
                Timestamp = DateTimeOffset.UtcNow,
                SerializedMessage = SysJsonSerializer.Serialize(msg),
                MessageText = msg.Text,
                Role = msg.Role.ToString(),
                // 注意：Agent Framework 的 ChatMessage 可能没有直接的 AgentId 等字段
                // 这些信息可能在 msg.AdditionalProperties 或其他地方
                IsUser = msg.Role.ToString().Equals("user", StringComparison.OrdinalIgnoreCase)
            }).ToList();

            // LiteDB 的 Upsert 操作（插入或更新）
            await Task.Run(() =>
            {
                foreach (var msg in persistedMessages)
                {
                    _messagesCollection.Upsert(msg);
                }
            }, cancellationToken);

            _logger?.LogDebug("Added {Count} messages to session {SessionId}", 
                persistedMessages.Count, SessionId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error adding messages to session {SessionId}", SessionId);
            throw;
        }
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
    /// 序列化存储状态（只序列化 SessionId）
    /// 这是关键：不序列化消息本身，只序列化 SessionId
    /// </summary>
    public override JsonElement Serialize(JsonSerializerOptions? jsonSerializerOptions = null)
    {
        _logger?.LogDebug("Serializing store state for session {SessionId}", SessionId);
        
        // 只序列化 SessionId，消息已经存储在 LiteDB 中
        return SysJsonSerializer.SerializeToElement(SessionId, jsonSerializerOptions);
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
