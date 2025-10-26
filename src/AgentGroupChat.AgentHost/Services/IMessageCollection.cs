using AgentGroupChat.Models;

namespace AgentGroupChat.AgentHost.Services;

/// <summary>
/// Interface for message collection access
/// Provides abstraction for accessing persisted chat messages
/// </summary>
public interface IMessageCollection
{
    /// <summary>
    /// Get messages for a specific session
    /// </summary>
    IEnumerable<PersistedChatMessage> Find(string sessionId);

    /// <summary>
    /// Count messages for a specific session
    /// </summary>
    int Count(string sessionId);

    /// <summary>
    /// Insert or update a message
    /// </summary>
    void Upsert(PersistedChatMessage message);

    /// <summary>
    /// Delete messages for a specific session
    /// </summary>
    int DeleteMany(string sessionId);
}
