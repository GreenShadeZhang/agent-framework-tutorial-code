using System.Text.Json;
using AgentGroupChat.Models;
using LiteDB;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;

namespace AgentGroupChat.AgentHost.Services;

/// <summary>
/// 基于 LiteDB 的会话持久化服务
/// 支持 Agent Framework 的 AgentThread 序列化和反序列化
/// 结合官方示例的持久化机制和 LiteDB 的轻量级存储
/// </summary>
public class PersistedSessionService : IDisposable
{
    private readonly LiteDatabase _database;
    private readonly ILiteCollection<PersistedChatSession> _sessions;
    private readonly ILogger<PersistedSessionService>? _logger;
    
    // 内存缓存：热会话（最近访问的会话）
    private readonly Dictionary<string, (PersistedChatSession Session, DateTime LastAccess)> _hotCache;
    private readonly int _maxCacheSize = 10;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(30);

    public PersistedSessionService(ILogger<PersistedSessionService>? logger = null)
    {
        _logger = logger;
        _hotCache = new Dictionary<string, (PersistedChatSession, DateTime)>();

        // 初始化 LiteDB
        var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        Directory.CreateDirectory(dbPath);
        
        var dbFilePath = Path.Combine(dbPath, "sessions.db");
        _database = new LiteDatabase(dbFilePath);
        _sessions = _database.GetCollection<PersistedChatSession>("sessions");
        
        // 创建索引以优化查询性能
        _sessions.EnsureIndex(x => x.Id);
        _sessions.EnsureIndex(x => x.LastUpdated);
        _sessions.EnsureIndex(x => x.IsActive);
        
        _logger?.LogInformation("PersistedSessionService initialized with database at: {DbPath}", dbFilePath);
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
    /// 删除会话
    /// </summary>
    public void DeleteSession(string id)
    {
        try
        {
            var deleted = _sessions.Delete(id);
            _hotCache.Remove(id);
            
            if (deleted)
            {
                _logger?.LogInformation("Deleted session {SessionId}", id);
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
    /// 保存 AgentThread 到会话
    /// 这是核心方法，实现官方示例的 thread.Serialize() 机制
    /// </summary>
    public void SaveThread(string sessionId, AgentThread thread, List<ChatMessageSummary>? summaries = null)
    {
        try
        {
            var session = GetSession(sessionId);
            if (session == null)
            {
                throw new InvalidOperationException($"Session {sessionId} not found");
            }

            // 序列化 AgentThread（官方机制）
            JsonElement serializedThread = thread.Serialize();
            session.ThreadData = System.Text.Json.JsonSerializer.Serialize(serializedThread, new JsonSerializerOptions 
            { 
                WriteIndented = false // 紧凑格式以节省空间
            });
            
            // 更新摘要（如果提供）
            if (summaries != null)
            {
                session.MessageSummaries = summaries;
                session.MessageCount = summaries.Count;
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
        return new Dictionary<string, object>
        {
            { "TotalSessions", _sessions.Count() },
            { "ActiveSessions", _sessions.Count(x => x.IsActive) },
            { "CachedSessions", _hotCache.Count },
            { "DatabaseSizeBytes", new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "sessions.db")).Length }
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
        _database?.Dispose();
    }
}
