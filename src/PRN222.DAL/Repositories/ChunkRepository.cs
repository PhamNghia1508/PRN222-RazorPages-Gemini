using Microsoft.EntityFrameworkCore;
using PRN222.DAL.Data;
using PRN222.DAL.Entities;
using PRN222.DAL.Repositories.Interfaces;

namespace PRN222.DAL.Repositories;

/// <summary>
/// Repository implementation for DocumentChunk entity with batch operations.
/// </summary>
public class ChunkRepository : Repository<DocumentChunk>, IChunkRepository
{
    public ChunkRepository(ChatbotDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<DocumentChunk>> GetByDocumentIdAsync(int documentId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(c => c.DocumentId == documentId)
            .OrderBy(c => c.ChunkIndex)
            .ToListAsync();
    }

    public async Task AddRangeAsync(IEnumerable<DocumentChunk> chunks)
    {
        await _dbSet.AddRangeAsync(chunks);
    }

    public async Task DeleteByDocumentIdAsync(int documentId)
    {
        var chunks = await _dbSet
            .Where(c => c.DocumentId == documentId)
            .ToListAsync();

        _dbSet.RemoveRange(chunks);
    }

    public async Task<int> GetCountByDocumentIdAsync(int documentId)
    {
        return await _dbSet.CountAsync(c => c.DocumentId == documentId);
    }

    public async Task<IEnumerable<DocumentChunk>> GetChunksWithEmbeddingsByDocumentIdsAsync(IEnumerable<int> documentIds)
    {
        return await _dbSet
            .AsNoTracking() // Optimize RAM usage by avoiding EF Core change tracking
            .Include(c => c.Embeddings)
            .Where(c => documentIds.Contains(c.DocumentId))
            .ToListAsync();
    }
}
