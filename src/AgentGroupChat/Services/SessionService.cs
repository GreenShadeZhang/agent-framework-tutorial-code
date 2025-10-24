using AgentGroupChat.Models;
using LiteDB;

namespace AgentGroupChat.Services;

/// <summary>
/// Service for persisting chat sessions using LiteDB.
/// </summary>
public class SessionService : IDisposable
{
    private readonly LiteDatabase _database;
    private readonly ILiteCollection<ChatSession> _sessions;

    public SessionService()
    {
        var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        Directory.CreateDirectory(dbPath);
        _database = new LiteDatabase(Path.Combine(dbPath, "sessions.db"));
        _sessions = _database.GetCollection<ChatSession>("sessions");
    }

    public List<ChatSession> GetAllSessions()
    {
        return _sessions.FindAll().OrderByDescending(s => s.LastUpdated).ToList();
    }

    public ChatSession? GetSession(string id)
    {
        return _sessions.FindById(id);
    }

    public ChatSession CreateSession(string? name = null)
    {
        var session = new ChatSession
        {
            Id = Guid.NewGuid().ToString(),
            Name = name ?? $"Session {DateTime.Now:yyyy-MM-dd HH:mm}"
        };
        _sessions.Insert(session);
        return session;
    }

    public void UpdateSession(ChatSession session)
    {
        session.LastUpdated = DateTime.UtcNow;
        _sessions.Update(session);
    }

    public void DeleteSession(string id)
    {
        _sessions.Delete(id);
    }

    public void Dispose()
    {
        _database?.Dispose();
    }
}
