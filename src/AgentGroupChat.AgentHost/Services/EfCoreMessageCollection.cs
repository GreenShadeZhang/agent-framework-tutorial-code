using AgentGroupChat.Models;

namespace AgentGroupChat.AgentHost.Services;

/// <summary>
/// EF Core implementation of message collection interface
/// Provides abstraction over EF Core DbSet for PersistedChatMessage
/// </summary>
public class EfCoreMessageCollection : IMessageCollection
{
    private readonly AgentDbContext _dbContext;

    public EfCoreMessageCollection(AgentDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public IEnumerable<PersistedChatMessage> Find(string sessionId)
    {
        return _dbContext.Messages
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.Timestamp)
            .ToList();
    }

    public int Count(string sessionId)
    {
        return _dbContext.Messages.Count(m => m.SessionId == sessionId);
    }

    public void Upsert(PersistedChatMessage message)
    {
        var existing = _dbContext.Messages.Find(message.Id);
        if (existing != null)
        {
            _dbContext.Entry(existing).CurrentValues.SetValues(message);
        }
        else
        {
            _dbContext.Messages.Add(message);
        }
        _dbContext.SaveChanges();
    }

    public int DeleteMany(string sessionId)
    {
        var messages = _dbContext.Messages.Where(m => m.SessionId == sessionId).ToList();
        _dbContext.Messages.RemoveRange(messages);
        _dbContext.SaveChanges();
        return messages.Count;
    }
}
