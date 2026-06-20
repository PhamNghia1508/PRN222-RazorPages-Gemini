namespace PRN222.DAL.Repositories.Interfaces;

/// <summary>
/// Generic repository interface providing standard CRUD operations.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();
    Task AddAsync(T entity);
    void Update(T entity);
    void Delete(T entity);
    IQueryable<T> GetQueryable();
}
