namespace WorkflowDesigner.Api.Repository;

/// <summary>
/// 仓储接口
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
public interface IRepository<T> where T : class
{
    /// <summary>
    /// 获取所有实体
    /// </summary>
    Task<IEnumerable<T>> GetAllAsync();

    /// <summary>
    /// 根据ID获取实体
    /// </summary>
    Task<T?> GetByIdAsync(string id);

    /// <summary>
    /// 添加实体
    /// </summary>
    Task<T> AddAsync(T entity);

    /// <summary>
    /// 更新实体
    /// </summary>
    Task<T> UpdateAsync(T entity);

    /// <summary>
    /// 删除实体
    /// </summary>
    Task<bool> DeleteAsync(string id);

    /// <summary>
    /// 查询实体
    /// </summary>
    Task<IEnumerable<T>> FindAsync(System.Linq.Expressions.Expression<Func<T, bool>> predicate);
}
