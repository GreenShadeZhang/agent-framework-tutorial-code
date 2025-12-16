using LiteDB;

namespace WorkflowDesigner.Api.Repository;

/// <summary>
/// LiteDB 数据库上下文
/// </summary>
public class LiteDbContext : IDisposable
{
    private readonly LiteDatabase _database;
    private bool _disposed = false;

    public LiteDbContext(string connectionString)
    {
        _database = new LiteDatabase(connectionString);
    }

    /// <summary>
    /// 获取集合
    /// </summary>
    public ILiteCollection<T> GetCollection<T>(string? name = null)
    {
        return _database.GetCollection<T>(name);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _database?.Dispose();
            }
            _disposed = true;
        }
    }
}
