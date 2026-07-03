using Microsoft.EntityFrameworkCore;
using PRN222.DAL.Data;
using PRN222.DAL.Entities;
using PRN222.DAL.Entities.Enums;
using PRN222.DAL.Repositories.Interfaces;

namespace PRN222.DAL.Repositories;

/// <summary>
/// Repository implementation for Document entity with specialized query methods.
/// </summary>
public class DocumentRepository : Repository<Document>, IDocumentRepository
{
    public DocumentRepository(ChatbotDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Document>> GetByCourseIdAsync(int courseId)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(d => d.Course)
            .Where(d => d.CourseId == courseId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();
    }

    public async Task<Document?> GetWithChunksAsync(int documentId)
    {
        return await _dbSet
            .Include(d => d.Course)
                .ThenInclude(course => course.Department)
            .Include(d => d.UploadedByUser)
            .Include(d => d.ArchivedByUser)
            .Include(d => d.CancelledByUser)
            .Include(d => d.Chunks.OrderBy(c => c.ChunkIndex))
                .ThenInclude(c => c.Embeddings)
            .FirstOrDefaultAsync(d => d.Id == documentId);
    }

    public async Task<Document?> GetWithCourseAsync(int documentId)
    {
        return await _dbSet
            .Include(d => d.Course)
            .Include(d => d.UploadedByUser)
            .FirstOrDefaultAsync(d => d.Id == documentId);
    }

    public override async Task<IEnumerable<Document>> GetAllAsync()
    {
        return await _dbSet
            .AsNoTracking()
            .Include(d => d.Course)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();
    }

    public async Task UpdateStatusAsync(int documentId, DocumentStatus status, string? errorMessage = null)
    {
        var document = await _dbSet.FindAsync(documentId);
        if (document != null)
        {
            document.Status = status;
            document.ErrorMessage = errorMessage;
            document.UpdatedAt = DateTime.UtcNow;
        }
    }

    public async Task<bool> TryCancelUploadedAsync(
        int documentId,
        string uploadedByUserId,
        DateTime cancelledAt,
        string cancellationReason)
    {
        var affectedRows = await _dbSet
            .Where(document =>
                document.Id == documentId &&
                document.UploadedByUserId == uploadedByUserId &&
                document.Status == DocumentStatus.Uploaded)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(document => document.Status, DocumentStatus.Cancelled)
                .SetProperty(document => document.CancelledByUserId, uploadedByUserId)
                .SetProperty(document => document.CancelledAt, cancelledAt)
                .SetProperty(document => document.CancellationReason, cancellationReason)
                .SetProperty(document => document.UpdatedAt, cancelledAt));

        return affectedRows == 1;
    }

    public async Task<bool> TryStartUploadedProcessingAsync(int documentId, DateTime processingStartedAt)
    {
        var affectedRows = await _dbSet
            .Where(document =>
                document.Id == documentId &&
                document.Status == DocumentStatus.Uploaded)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(document => document.Status, DocumentStatus.Processing)
                .SetProperty(document => document.ErrorMessage, (string?)null)
                .SetProperty(document => document.UpdatedAt, processingStartedAt));

        return affectedRows == 1;
    }
}
