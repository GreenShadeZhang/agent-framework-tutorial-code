using AgentGroupChat.Models;
using Microsoft.Agents.AI;

namespace AgentGroupChat.AgentHost.Services;

/// <summary>
/// Interface for session persistence service
/// Defines the contract for managing chat sessions and messages
/// </summary>
public interface ISessionService
{
    /// <summary>
    /// Get all sessions (without full thread data, metadata only)
    /// </summary>
    List<PersistedChatSession> GetAllSessions();

    /// <summary>
    /// Get a specific session by ID (includes full thread data)
    /// </summary>
    PersistedChatSession? GetSession(string id);

    /// <summary>
    /// Create a new session
    /// </summary>
    PersistedChatSession CreateSession(string? name = null);

    /// <summary>
    /// Update session metadata (name, active status)
    /// </summary>
    void UpdateSessionMetadata(string sessionId, string? name = null, bool? isActive = null);

    /// <summary>
    /// Delete a session (cascading delete of messages)
    /// </summary>
    void DeleteSession(string id);

    /// <summary>
    /// Save AgentThread to session
    /// </summary>
    void SaveThread(string sessionId, AgentThread thread);

    /// <summary>
    /// Load AgentThread from session
    /// </summary>
    AgentThread? LoadThread(string sessionId, AIAgent agent);

    /// <summary>
    /// Get or create AgentThread for a session
    /// </summary>
    AgentThread GetOrCreateThread(string sessionId, AIAgent agent);

    /// <summary>
    /// Get message summaries for a session (for UI display)
    /// </summary>
    List<ChatMessageSummary> GetMessageSummaries(string sessionId);

    /// <summary>
    /// Clear all messages in a session
    /// </summary>
    void ClearSessionMessages(string sessionId);

    /// <summary>
    /// Get database statistics
    /// </summary>
    Dictionary<string, object> GetStatistics();

    /// <summary>
    /// Clean up old inactive sessions
    /// </summary>
    int CleanupOldSessions(int daysOld = 30);

    /// <summary>
    /// Clean up expired cache entries
    /// </summary>
    void CleanupExpiredCache();
}
