using System.Text.Json;
using AgentGroupChat.AgentHost.Data;
using AgentGroupChat.Models;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgentGroupChat.AgentHost.Services;

/// <summary>
/// EF Core implementation of session persistence service
/// Supports both SQLite and PostgreSQL through provider-agnostic EF Core
/// </summary>
public class EfCoreSessionService : ISessionService, IDisposable
{
    private readonly AgentDbContext _dbContext;
    private readonly IMessageCollection _messageCollection;
    private readonly ILogger<EfCoreSessionService>? _logger;
    
    // Memory cache: hot sessions (recently accessed sessions)
    private readonly Dictionary<string, (PersistedChatSession Session, DateTime LastAccess)> _hotCache;
    private readonly int _maxCacheSize = 10;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(30);

    public EfCoreSessionService(
        AgentDbContext dbContext,
        ILogger<EfCoreSessionService>? logger = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger;
        _hotCache = new Dictionary<string, (PersistedChatSession, DateTime)>();
        _messageCollection = new EfCoreMessageCollection(_dbContext);

        // Ensure database is created
        _dbContext.Database.EnsureCreated();
        
        _logger?.LogInformation("EfCoreSessionService initialized");
    }

    #region Basic CRUD Operations

    public List<PersistedChatSession> GetAllSessions()
    {
        try
        {
            var sessions = _dbContext.Sessions
                .OrderByDescending(s => s.LastUpdated)
                .ToList();
            
            // Clear ThreadData to reduce transfer size
            foreach (var session in sessions)
            {
                session.ThreadData = string.Empty;
            }
            
            _logger?.LogDebug("Retrieved {Count} sessions", sessions.Count);
            return sessions;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting all sessions");
            return new List<PersistedChatSession>();
        }
    }

    public PersistedChatSession? GetSession(string id)
    {
        try
        {
            // Check cache first
            if (_hotCache.TryGetValue(id, out var cached))
            {
                // Check if cache is expired
                if (DateTime.UtcNow - cached.LastAccess < _cacheExpiration)
                {
                    _logger?.LogDebug("Session {SessionId} retrieved from cache", id);
                    _hotCache[id] = (cached.Session, DateTime.UtcNow);
                    return cached.Session;
                }
                else
                {
                    _hotCache.Remove(id);
                }
            }

            // Load from database
            var session = _dbContext.Sessions.Find(id);
            
            if (session != null)
            {
                AddToCache(id, session);
                _logger?.LogDebug("Session {SessionId} retrieved from database", id);
            }
            else
            {
                _logger?.LogWarning("Session {SessionId} not found", id);
            }
            
            return session;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting session {SessionId}", id);
            return null;
        }
    }

    public PersistedChatSession CreateSession(string? name = null)
    {
        try
        {
            var session = new PersistedChatSession
            {
                Id = Guid.NewGuid().ToString(),
                Name = name ?? $"Session {DateTime.Now:yyyy-MM-dd HH:mm}",
                ThreadData = string.Empty,
                CreatedAt = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow,
                IsActive = true,
                Version = 1
            };
            
            _dbContext.Sessions.Add(session);
            _dbContext.SaveChanges();
            AddToCache(session.Id, session);
            
            _logger?.LogInformation("Created new session {SessionId} with name '{Name}'", 
                session.Id, session.Name);
            
            return session;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error creating session");
            throw;
        }
    }

    public void UpdateSessionMetadata(string sessionId, string? name = null, bool? isActive = null)
    {
        try
        {
            var session = GetSession(sessionId);
            if (session == null)
            {
                throw new InvalidOperationException($"Session {sessionId} not found");
            }

            if (name != null)
                session.Name = name;
            
            if (isActive.HasValue)
                session.IsActive = isActive.Value;
            
            session.LastUpdated = DateTime.UtcNow;
            
            _dbContext.Sessions.Update(session);
            _dbContext.SaveChanges();
            UpdateCache(sessionId, session);
            
            _logger?.LogDebug("Updated metadata for session {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating session metadata {SessionId}", sessionId);
            throw;
        }
    }

    public void DeleteSession(string id)
    {
        try
        {
            // Delete all messages for the session (cascade delete)
            var deletedMessagesCount = _messageCollection.DeleteMany(id);
            
            // Delete the session itself
            var session = _dbContext.Sessions.Find(id);
            if (session != null)
            {
                _dbContext.Sessions.Remove(session);
                _dbContext.SaveChanges();
                
                // Remove from cache
                _hotCache.Remove(id);
                
                _logger?.LogInformation("Deleted session {SessionId} and {MessageCount} related messages", 
                    id, deletedMessagesCount);
            }
            else
            {
                _logger?.LogWarning("Session {SessionId} not found for deletion", id);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting session {SessionId}", id);
            throw;
        }
    }

    #endregion

    #region AgentThread Persistence

    public void SaveThread(string sessionId, AgentThread thread)
    {
        try
        {
            var session = GetSession(sessionId);
            if (session == null)
            {
                throw new InvalidOperationException($"Session {sessionId} not found");
            }

            // Serialize AgentThread
            JsonElement serializedThread = thread.Serialize();
            session.ThreadData = JsonSerializer.Serialize(serializedThread, new JsonSerializerOptions 
            { 
                WriteIndented = false
            });
            
            // Update message statistics
            session.MessageCount = _messageCollection.Count(sessionId);
            
            // Update last message preview
            var lastMessage = _messageCollection.Find(sessionId)
                .OrderByDescending(m => m.Timestamp)
                .FirstOrDefault();
            
            if (lastMessage != null)
            {
                session.LastMessagePreview = lastMessage.MessageText?.Length > 50 
                    ? lastMessage.MessageText.Substring(0, 50) + "..." 
                    : lastMessage.MessageText;
                session.LastMessageSender = lastMessage.AgentName ?? (lastMessage.IsUser ? "User" : "Agent");
            }
            
            session.LastUpdated = DateTime.UtcNow;
            
            _dbContext.Sessions.Update(session);
            _dbContext.SaveChanges();
            UpdateCache(sessionId, session);
            
            _logger?.LogDebug("Saved AgentThread for session {SessionId}, message count: {Count}", 
                sessionId, session.MessageCount);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error saving thread for session {SessionId}", sessionId);
            throw;
        }
    }

    public AgentThread? LoadThread(string sessionId, AIAgent agent)
    {
        try
        {
            var session = GetSession(sessionId);
            if (session == null)
            {
                _logger?.LogWarning("Cannot load thread: session {SessionId} not found", sessionId);
                return null;
            }

            if (string.IsNullOrEmpty(session.ThreadData))
            {
                _logger?.LogDebug("Session {SessionId} has no thread data, will create new thread", sessionId);
                return null;
            }

            // Deserialize AgentThread
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(session.ThreadData);
            var thread = agent.DeserializeThread(jsonElement);
            
            _logger?.LogDebug("Loaded AgentThread for session {SessionId}", sessionId);
            return thread;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading thread for session {SessionId}", sessionId);
            return null;
        }
    }

    public AgentThread GetOrCreateThread(string sessionId, AIAgent agent)
    {
        var thread = LoadThread(sessionId, agent);
        if (thread != null)
        {
            return thread;
        }

        var newThread = agent.GetNewThread();
        _logger?.LogDebug("Created new thread for session {SessionId}", sessionId);
        return newThread;
    }

    public List<ChatMessageSummary> GetMessageSummaries(string sessionId)
    {
        try
        {
            var messages = _messageCollection.Find(sessionId);

            return messages.Select(pm => new ChatMessageSummary
            {
                AgentId = pm.AgentId ?? (pm.IsUser ? "user" : "assistant"),
                AgentName = pm.AgentName ?? (pm.IsUser ? "User" : "Assistant"),
                AgentAvatar = pm.AgentAvatar ?? (pm.IsUser ? "👤" : "🤖"),
                Content = pm.MessageText ?? string.Empty,
                ImageUrl = pm.ImageUrl,
                IsUser = pm.IsUser,
                Timestamp = pm.Timestamp.LocalDateTime,
                MessageType = string.IsNullOrEmpty(pm.ImageUrl) ? "text" : "image"
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting message summaries for session {SessionId}", sessionId);
            return new List<ChatMessageSummary>();
        }
    }

    public void ClearSessionMessages(string sessionId)
    {
        try
        {
            _messageCollection.DeleteMany(sessionId);
            
            // Update session statistics
            var session = GetSession(sessionId);
            if (session != null)
            {
                session.MessageCount = 0;
                session.LastMessagePreview = null;
                session.LastMessageSender = null;
                session.LastUpdated = DateTime.UtcNow;
                _dbContext.Sessions.Update(session);
                _dbContext.SaveChanges();
                UpdateCache(sessionId, session);
            }
            
            _logger?.LogInformation("Cleared all messages for session {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error clearing messages for session {SessionId}", sessionId);
            throw;
        }
    }

    #endregion

    #region Cache Management

    private void AddToCache(string sessionId, PersistedChatSession session)
    {
        if (_hotCache.Count >= _maxCacheSize)
        {
            var oldestKey = _hotCache
                .OrderBy(x => x.Value.LastAccess)
                .First()
                .Key;
            _hotCache.Remove(oldestKey);
        }

        _hotCache[sessionId] = (session, DateTime.UtcNow);
    }

    private void UpdateCache(string sessionId, PersistedChatSession session)
    {
        if (_hotCache.ContainsKey(sessionId))
        {
            _hotCache[sessionId] = (session, DateTime.UtcNow);
        }
    }

    public void CleanupExpiredCache()
    {
        var expiredKeys = _hotCache
            .Where(x => DateTime.UtcNow - x.Value.LastAccess > _cacheExpiration)
            .Select(x => x.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _hotCache.Remove(key);
        }

        if (expiredKeys.Any())
        {
            _logger?.LogDebug("Cleaned up {Count} expired cache entries", expiredKeys.Count);
        }
    }

    #endregion

    #region Statistics and Maintenance

    public Dictionary<string, object> GetStatistics()
    {
        var totalSessions = _dbContext.Sessions.Count();
        var activeSessions = _dbContext.Sessions.Count(x => x.IsActive);
        var totalMessages = _dbContext.Messages.Count();

        return new Dictionary<string, object>
        {
            { "TotalSessions", totalSessions },
            { "ActiveSessions", activeSessions },
            { "TotalMessages", totalMessages },
            { "CachedSessions", _hotCache.Count },
            { "DatabaseProvider", _dbContext.Database.ProviderName ?? "Unknown" }
        };
    }

    public int CleanupOldSessions(int daysOld = 30)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);
            var oldSessions = _dbContext.Sessions
                .Where(x => !x.IsActive && x.LastUpdated < cutoffDate)
                .ToList();
            
            int count = 0;
            foreach (var session in oldSessions)
            {
                _messageCollection.DeleteMany(session.Id);
                _dbContext.Sessions.Remove(session);
                _hotCache.Remove(session.Id);
                count++;
            }
            
            _dbContext.SaveChanges();
            
            _logger?.LogInformation("Cleaned up {Count} old sessions", count);
            return count;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error cleaning up old sessions");
            return 0;
        }
    }

    #endregion

    /// <summary>
    /// Internal method to get message collection for ChatMessageStore
    /// </summary>
    internal IMessageCollection GetMessagesCollection()
    {
        return _messageCollection;
    }

    public void Dispose()
    {
        _logger?.LogInformation("Disposing EfCoreSessionService");
        // DbContext is managed by DI container, so we don't dispose it here
    }
}
