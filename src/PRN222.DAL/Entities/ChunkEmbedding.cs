namespace PRN222.DAL.Entities;

/// <summary>
/// Stores the embedding vector for a document chunk, produced by a specific embedding model.
/// </summary>
public class ChunkEmbedding
{
    public int Id { get; set; }

    public int ChunkId { get; set; }

    /// <summary>Name of the embedding model used to generate this vector.</summary>
    public string EmbeddingModelName { get; set; } = string.Empty;

    /// <summary>Embedding vector stored as JSON array of floats, e.g. "[0.1, 0.2, ...]".</summary>
    public string EmbeddingVector { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public DocumentChunk Chunk { get; set; } = null!;
}
