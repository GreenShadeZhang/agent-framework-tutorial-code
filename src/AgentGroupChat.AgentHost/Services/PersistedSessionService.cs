using System.Text.Json;
using AgentGroupChat.Models;
using LiteDB;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;

namespace AgentGroupChat.AgentHost.Services;

/// <summary>
/// 基于 LiteDB 的会话持久化服务（重构版）
/// 支持 Agent Framework 的 AgentThread 序列化和反序列化
/// 优化：消息存储在独立的 messages 集合中，Thread 只保存最小元数据
/// 参考 Agent Framework Step06 和 Step07 的最佳实践
/// </summary>
public class PersistedSessionService : IDisposable
{
    private readonly LiteDatabase _database;
    private readonly ILiteCollection<PersistedChatSession> _sessions;
    private readonly ILiteCollection<PersistedChatMessage> _messages;
    private readonly ILogger<PersistedSessionService>? _logger;
    private readonly bool _ownsDatabase; // 标记是否拥有数据库实例（用于决定是否 Dispose）
    
    // 内存缓存：热会话（最近访问的会话）
    private readonly Dictionary<string, (PersistedChatSession Session, DateTime LastAccess)> _hotCache;
    private readonly int _maxCacheSize = 10;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(30);

    /// <summary>
    /// 构造函数（使用依赖注入的 LiteDatabase 单例）- 推荐方式
    /// </summary>
    public PersistedSessionService(LiteDatabase database, ILogger<PersistedSessionService>? logger = null)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _logger = logger;
        _hotCache = new Dictionary<string, (PersistedChatSession, DateTime)>();
        _ownsDatabase = false; // 不拥有数据库，不负责 Dispose
        
        // 获取会话和消息集合
        _sessions = _database.GetCollection<PersistedChatSession>("sessions");
        _messages = _database.GetCollection<PersistedChatMessage>("messages");
        
        // 创建索引以优化查询性能
        _sessions.EnsureIndex(x => x.Id);
        _sessions.EnsureIndex(x => x.LastUpdated);
        _sessions.EnsureIndex(x => x.IsActive);
        
        // 为消息集合创建索引
        _messages.EnsureIndex(x => x.SessionId);
        _messages.EnsureIndex(x => x.Timestamp);
        _messages.EnsureIndex(x => x.Id);
        
        _logger?.LogInformation("PersistedSessionService initialized using injected LiteDatabase instance");
    }

    #region 基础 CRUD 操作

    /// <summary>
    /// 获取所有会话（不包含完整 Thread 数据，仅元数据和摘要）
    /// </summary>
    public List<PersistedChatSession> GetAllSessions()
    {
        try
        {
            var sessions = _sessions.Query()
                .OrderByDescending(s => s.LastUpdated)
                .ToList();
            
            // 清除 ThreadData 以减少传输数据量
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

    /// <summary>
    /// 获取特定会话（包含完整 Thread 数据）
    /// </summary>
    public PersistedChatSession? GetSession(string id)
    {
        try
        {
            // 先查缓存
            if (_hotCache.TryGetValue(id, out var cached))
            {
                // 检查缓存是否过期
                if (DateTime.UtcNow - cached.LastAccess < _cacheExpiration)
                {
                    _logger?.LogDebug("Session {SessionId} retrieved from cache", id);
                    _hotCache[id] = (cached.Session, DateTime.UtcNow); // 更新访问时间
                    return cached.Session;
                }
                else
                {
                    // 缓存过期，移除
                    _hotCache.Remove(id);
                }
            }

            // 从数据库加载
            var session = _sessions.FindById(id);
            
            if (session != null)
            {
                // 加入缓存
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

    /// <summary>
    /// 创建新会话
    /// </summary>
    public PersistedChatSession CreateSession(string? name = null)
    {
        try
        {
            var session = new PersistedChatSession
            {
                Id = Guid.NewGuid().ToString(),
                Name = name ?? $"Session {DateTime.Now:yyyy-MM-dd HH:mm}",
                ThreadData = string.Empty, // 空 thread，首次对话时初始化
                CreatedAt = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow,
                IsActive = true,
                Version = 1
            };
            
            _sessions.Insert(session);
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

    /// <summary>
    /// 更新会话元数据（名称等）
    /// </summary>
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
            
            _sessions.Update(session);
            UpdateCache(sessionId, session);
            
            _logger?.LogDebug("Updated metadata for session {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating session metadata {SessionId}", sessionId);
            throw;
        }
    }

    /// <summary>
    /// 删除会话（级联删除相关消息）
    /// </summary>
    public void DeleteSession(string id)
    {
        try
        {
            // 先删除该会话的所有消息（级联删除）
            var deletedMessagesCount = _messages.DeleteMany(m => m.SessionId == id);
            
            // 再删除会话本身
            var deleted = _sessions.Delete(id);
            
            // 从缓存中移除
            _hotCache.Remove(id);
            
            if (deleted)
            {
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

    #region AgentThread 持久化核心功能

    /// <summary>
    /// 保存 AgentThread 到会话（优化版）
    /// Thread 序列化数据只包含最小元数据（SessionId），消息由 ChatMessageStore 管理
    /// </summary>
    public void SaveThread(string sessionId, AgentThread thread)
    {
        try
        {
            var session = GetSession(sessionId);
            if (session == null)
            {
                throw new InvalidOperationException($"Session {sessionId} not found");
            }

            // 序列化 AgentThread（现在只包含 SessionId 等元数据，不包含消息）
            JsonElement serializedThread = thread.Serialize();
            session.ThreadData = System.Text.Json.JsonSerializer.Serialize(serializedThread, new JsonSerializerOptions 
            { 
                WriteIndented = false // 紧凑格式以节省空间
            });
            
            // 更新消息统计（从 messages 集合计算）
            session.MessageCount = _messages.Count(m => m.SessionId == sessionId);
            
            // 更新最后消息预览
            var lastMessage = _messages
                .Find(m => m.SessionId == sessionId)
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
            
            // 保存到数据库
            _sessions.Update(session);
            
            // 更新缓存
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

    /// <summary>
    /// 从会话加载 AgentThread
    /// 这是核心方法，实现官方示例的 agent.DeserializeThread() 机制
    /// </summary>
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

            // 反序列化 AgentThread（官方机制）
            var jsonElement = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(session.ThreadData);
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

    /// <summary>
    /// 获取或创建 AgentThread
    /// 便捷方法：如果会话已有 thread 则加载，否则创建新的
    /// </summary>
    public AgentThread GetOrCreateThread(string sessionId, AIAgent agent)
    {
        var thread = LoadThread(sessionId, agent);
        if (thread != null)
        {
            return thread;
        }

        // 创建新 thread
        var newThread = agent.GetNewThread();
        _logger?.LogDebug("Created new thread for session {SessionId}", sessionId);
        return newThread;
    }

    /// <summary>
    /// 获取 LiteDB 消息集合的引用（用于 ChatMessageStore）
    /// </summary>
    public ILiteCollection<PersistedChatMessage> GetMessagesCollection()
    {
        return _messages;
    }

    /// <summary>
    /// 获取会话的消息摘要（用于 UI 展示）
    /// </summary>
    public List<ChatMessageSummary> GetMessageSummaries(string sessionId)
    {
        try
        {
            var messages = _messages
                .Find(m => m.SessionId == sessionId)
                .OrderBy(m => m.Timestamp)
                .ToList();

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

    /// <summary>
    /// 清除会话的所有消息
    /// </summary>
    public void ClearSessionMessages(string sessionId)
    {
        try
        {
            _messages.DeleteMany(m => m.SessionId == sessionId);
            
            // 更新会话统计
            var session = GetSession(sessionId);
            if (session != null)
            {
                session.MessageCount = 0;
                session.LastMessagePreview = null;
                session.LastMessageSender = null;
                session.LastUpdated = DateTime.UtcNow;
                _sessions.Update(session);
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

    #region 缓存管理

    private void AddToCache(string sessionId, PersistedChatSession session)
    {
        // 如果缓存已满，移除最旧的项
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

    /// <summary>
    /// 清理过期缓存
    /// </summary>
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

    #region 统计和维护

    /// <summary>
    /// 获取数据库统计信息
    /// </summary>
    public Dictionary<string, object> GetStatistics()
    {
        var dbFileInfo = new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "sessions.db"));
        
        return new Dictionary<string, object>
        {
            { "TotalSessions", _sessions.Count() },
            { "ActiveSessions", _sessions.Count(x => x.IsActive) },
            { "TotalMessages", _messages.Count() },
            { "CachedSessions", _hotCache.Count },
            { "DatabaseSizeBytes", dbFileInfo.Exists ? dbFileInfo.Length : 0 }
        };
    }

    /// <summary>
    /// 清理旧的非活跃会话（可用于定期维护）
    /// </summary>
    public int CleanupOldSessions(int daysOld = 30)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);
            var oldSessions = _sessions.Find(x => !x.IsActive && x.LastUpdated < cutoffDate);
            
            int count = 0;
            foreach (var session in oldSessions)
            {
                _sessions.Delete(session.Id);
                _hotCache.Remove(session.Id);
                count++;
            }
            
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

    public void Dispose()
    {
        _logger?.LogInformation("Disposing PersistedSessionService");
        
        // 只有当我们拥有数据库实例时才 Dispose
        // 使用 DI 的单例实例由容器管理，不应该在这里 Dispose
        if (_ownsDatabase)
        {
            _database?.Dispose();
        }
    }
}
