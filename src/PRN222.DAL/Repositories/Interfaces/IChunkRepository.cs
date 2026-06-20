using PRN222.DAL.Entities;

namespace PRN222.DAL.Repositories.Interfaces;

/// <summary>
/// Repository interface for DocumentChunk entity with specialized queries.
/// </summary>
public interface IChunkRepository : IRepository<DocumentChunk>
{
    /// <summary>Get all chunks for a specific document, ordered by chunk index.</summary>
    Task<IEnumerable<DocumentChunk>> GetByDocumentIdAsync(int documentId);

    /// <summary>Add multiple chunks in a single operation.</summary>
    Task AddRangeAsync(IEnumerable<DocumentChunk> chunks);

    /// <summary>Delete all chunks belonging to a specific document.</summary>
    Task DeleteByDocumentIdAsync(int documentId);

    /// <summary>Get the total count of chunks for a document.</summary>
    Task<int> GetCountByDocumentIdAsync(int documentId);

    /// <summary>Get all chunks along with their embeddings for multiple documents in a single query.</summary>
    Task<IEnumerable<DocumentChunk>> GetChunksWithEmbeddingsByDocumentIdsAsync(IEnumerable<int> documentIds);
}
