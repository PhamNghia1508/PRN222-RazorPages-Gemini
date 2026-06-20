using PRN222.DAL.Entities;
using PRN222.DAL.Entities.Enums;

namespace PRN222.DAL.Repositories.Interfaces;

/// <summary>
/// Repository interface for Document entity with specialized queries.
/// </summary>
public interface IDocumentRepository : IRepository<Document>
{
    /// <summary>Get all documents belonging to a specific course.</summary>
    Task<IEnumerable<Document>> GetByCourseIdAsync(int courseId);

    /// <summary>Get a document with its chunks eagerly loaded.</summary>
    Task<Document?> GetWithChunksAsync(int documentId);

    /// <summary>Get a document with its course information eagerly loaded.</summary>
    Task<Document?> GetWithCourseAsync(int documentId);

    /// <summary>Update the processing status of a document.</summary>
    Task UpdateStatusAsync(int documentId, DocumentStatus status, string? errorMessage = null);
}
