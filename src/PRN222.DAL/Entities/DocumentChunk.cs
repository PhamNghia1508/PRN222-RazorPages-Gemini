namespace PRN222.DAL.Entities;

/// <summary>
/// Represents a text chunk extracted from a document after chunking.
/// </summary>
public class DocumentChunk
{
    public int Id { get; set; }

    public int DocumentId { get; set; }

    /// <summary>Zero-based index of this chunk within the document.</summary>
    public int ChunkIndex { get; set; }

    /// <summary>The text content of this chunk.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Estimated token count for this chunk.</summary>
    public int TokenCount { get; set; }

    /// <summary>Starting page number (if applicable, e.g. PDF).</summary>
    public int? StartPage { get; set; }

    /// <summary>Ending page number (if applicable).</summary>
    public int? EndPage { get; set; }

    /// <summary>Chapter or section reference (if extractable).</summary>
    public string? ChapterSection { get; set; }

    /// <summary>Optional URL to an image associated with this chunk.</summary>
    public string? ImageUrl { get; set; }

    // Navigation properties
    public Document Document { get; set; } = null!;
    public ICollection<ChunkEmbedding> Embeddings { get; set; } = new List<ChunkEmbedding>();
}
