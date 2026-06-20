namespace PRN222.DAL.Repositories.Interfaces;

/// <summary>
/// Unit of Work interface for managing database transactions across repositories.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>Save all pending changes to the database.</summary>
    Task<int> SaveChangesAsync();

    /// <summary>Begin a new database transaction.</summary>
    Task BeginTransactionAsync();

    /// <summary>Commit the current transaction.</summary>
    Task CommitTransactionAsync();

    /// <summary>Rollback the current transaction.</summary>
    Task RollbackTransactionAsync();
}
