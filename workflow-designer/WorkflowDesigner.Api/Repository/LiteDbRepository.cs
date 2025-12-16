using LiteDB;
using System.Linq.Expressions;

namespace WorkflowDesigner.Api.Repository;

/// <summary>
/// LiteDB 仓储基类
/// </summary>
public class LiteDbRepository<T> : IRepository<T> where T : class
{
    private readonly LiteDbContext _context;
    private readonly ILiteCollection<T> _collection;

    public LiteDbRepository(LiteDbContext context, string? collectionName = null)
    {
        _context = context;
        _collection = _context.GetCollection<T>(collectionName);
    }

    public async Task<IEnumerable<T>> GetAllAsync()
    {
        return await Task.Run(() => _collection.FindAll());
    }

    public async Task<T?> GetByIdAsync(string id)
    {
        var bsonValue = new BsonValue(id);
        return await Task.Run(() => _collection.FindById(bsonValue));
    }

    public async Task<T> AddAsync(T entity)
    {
        await Task.Run(() => _collection.Insert(entity));
        return entity;
    }

    public async Task<T> UpdateAsync(T entity)
    {
        await Task.Run(() => _collection.Update(entity));
        return entity;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var bsonValue = new BsonValue(id);
        return await Task.Run(() => _collection.Delete(bsonValue));
    }

    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        return await Task.Run(() => _collection.Find(predicate));
    }
}
