using Microsoft.EntityFrameworkCore.Storage;
using PRN222.DAL.Data;
using PRN222.DAL.Repositories.Interfaces;

namespace PRN222.DAL.Repositories;

/// <summary>
/// Unit of Work implementation for managing transactions across multiple repositories.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly ChatbotDbContext _context;
    private IDbContextTransaction? _transaction;

    public UnitOfWork(ChatbotDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public async Task BeginTransactionAsync()
    {
        _transaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}
