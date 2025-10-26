using System.Text.Json;
using AgentGroupChat.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;
using SysJsonSerializer = System.Text.Json.JsonSerializer;

namespace AgentGroupChat.AgentHost.Services;

/// <summary>
/// EF Core implementation of ChatMessageStore
/// Replaces LiteDB implementation with Entity Framework Core
/// </summary>
public class EfCoreChatMessageStore : ChatMessageStore
{
    private readonly IMessageCollection _messagesCollection;
    private readonly ILogger<EfCoreChatMessageStore>? _logger;

    public string SessionId { get; private set; }
    public string AgentId { get; private set; }
    public string AgentName { get; private set; }
    public string AgentAvatar { get; private set; }

    public EfCoreChatMessageStore(
        IMessageCollection messagesCollection,
        string sessionId,
        string agentId = "assistant",
        string agentName = "Assistant",
        string agentAvatar = "🤖",
        ILogger<EfCoreChatMessageStore>? logger = null)
    {
        _messagesCollection = messagesCollection ?? throw new ArgumentNullException(nameof(messagesCollection));
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        AgentId = agentId ?? "assistant";
        AgentName = agentName ?? "Assistant";
        AgentAvatar = agentAvatar ?? "🤖";
        _logger = logger;

        _logger?.LogDebug("Created EfCoreChatMessageStore for session {SessionId} with Agent {AgentName}", 
            SessionId, AgentName);
    }

    public EfCoreChatMessageStore(
        IMessageCollection messagesCollection,
        JsonElement serializedStoreState,
        ILogger<EfCoreChatMessageStore>? logger = null)
    {
        _messagesCollection = messagesCollection ?? throw new ArgumentNullException(nameof(messagesCollection));
        _logger = logger;

        if (serializedStoreState.ValueKind is JsonValueKind.Object)
        {
            SessionId = serializedStoreState.GetProperty("sessionId").GetString() 
                ?? throw new InvalidOperationException("Failed to deserialize SessionId from serialized state");
            
            AgentId = serializedStoreState.TryGetProperty("agentId", out var agentIdProp) 
                ? (agentIdProp.GetString() ?? "assistant") 
                : "assistant";
            AgentName = serializedStoreState.TryGetProperty("agentName", out var agentNameProp) 
                ? (agentNameProp.GetString() ?? "Assistant") 
                : "Assistant";
            AgentAvatar = serializedStoreState.TryGetProperty("agentAvatar", out var agentAvatarProp) 
                ? (agentAvatarProp.GetString() ?? "🤖") 
                : "🤖";
            
            _logger?.LogDebug("Restored EfCoreChatMessageStore for session {SessionId} with Agent {AgentName}", 
                SessionId, AgentName);
        }
        else
        {
            throw new InvalidOperationException("Invalid serialized state format");
        }
    }

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
                    
                    AgentId = isUserMessage ? "user" : AgentId,
                    AgentName = isUserMessage ? "User" : AgentName,
                    AgentAvatar = isUserMessage ? "👤" : AgentAvatar,
                    
                    IsUser = isUserMessage,
                    ImageUrl = ExtractImageUrl(msg)
                };
            }).ToList();

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

    private string? ExtractImageUrl(AIChatMessage msg)
    {
        if (msg.AdditionalProperties?.TryGetValue("imageUrl", out var imageUrl) == true)
        {
            return imageUrl?.ToString();
        }
        return null;
    }

    public override async Task<IEnumerable<AIChatMessage>> GetMessagesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var persistedMessages = await Task.Run(() =>
            {
                return _messagesCollection.Find(SessionId).ToList();
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

    public override JsonElement Serialize(JsonSerializerOptions? jsonSerializerOptions = null)
    {
        _logger?.LogDebug("Serializing store state for session {SessionId} with Agent {AgentName}", 
            SessionId, AgentName);
        
        var state = new Dictionary<string, string>
        {
            ["sessionId"] = SessionId,
            ["agentId"] = AgentId,
            ["agentName"] = AgentName,
            ["agentAvatar"] = AgentAvatar
        };
        
        return SysJsonSerializer.SerializeToElement(state, jsonSerializerOptions);
    }

    public int GetMessageCount()
    {
        return _messagesCollection.Count(SessionId);
    }

    public async Task ClearMessagesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.Run(() =>
            {
                _messagesCollection.DeleteMany(SessionId);
            }, cancellationToken);

            _logger?.LogInformation("Cleared all messages for session {SessionId}", SessionId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error clearing messages for session {SessionId}", SessionId);
            throw;
        }
    }

    public List<ChatMessageSummary> GetMessageSummaries()
    {
        try
        {
            var messages = _messagesCollection.Find(SessionId);

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
