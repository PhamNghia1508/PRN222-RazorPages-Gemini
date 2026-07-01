using PRN222.BLL.DTOs;

namespace PRN222.BLL.Services.Interfaces;

/// <summary>
/// Service interface for document management operations.
/// </summary>
public interface IDocumentService
{
    /// <summary>Get all documents.</summary>
    Task<IEnumerable<DocumentDto>> GetAllDocumentsAsync();

    /// <summary>Get aggregate document metrics and the five most recent documents for the dashboard.</summary>
    Task<DocumentDashboardSummaryDto> GetDashboardSummaryAsync(IEnumerable<int>? courseIds = null);

    /// <summary>Get a document with full details and chunks.</summary>
    Task<DocumentDetailDto?> GetDocumentByIdAsync(int id);

    /// <summary>Get all documents for a specific course.</summary>
    Task<IEnumerable<DocumentDto>> GetDocumentsByCourseAsync(int courseId);

    /// <summary>Get a privately stored image extracted from a document chunk.</summary>
    Task<DocumentImageDto?> GetChunkImageAsync(int chunkId);

    /// <summary>Upload and save a document file.</summary>
    Task<DocumentDto> UploadDocumentAsync(DocumentUploadDto dto, Stream fileStream);

    /// <summary>Process a document: extract text, chunk, and update status.</summary>
    Task ProcessDocumentAsync(int documentId);

    /// <summary>Enqueue document processing to run asynchronously in the background.</summary>
    Task EnqueueProcessDocumentAsync(int documentId);

    /// <summary>Delete a document and its associated file and chunks.</summary>
    Task DeleteDocumentAsync(int id);
}
