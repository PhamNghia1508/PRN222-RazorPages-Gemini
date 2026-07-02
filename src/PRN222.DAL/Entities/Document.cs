using PRN222.DAL.Entities.Enums;

namespace PRN222.DAL.Entities;

/// <summary>
/// Represents an uploaded document (PDF, DOCX, etc.) belonging to a course.
/// </summary>
public class Document
{
    public int Id { get; set; }

    public int CourseId { get; set; }

    /// <summary>Stored file name (GUID-based to avoid conflicts).</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Original file name as uploaded by the user.</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>MIME type, e.g. "application/pdf", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>File size in bytes.</summary>
    public long FileSize { get; set; }

    /// <summary>Path to the stored file on disk.</summary>
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>Full extracted text from the document.</summary>
    public string? ExtractedText { get; set; }

    /// <summary>Number of chunks generated from the document.</summary>
    public int ChunkCount { get; set; }

    /// <summary>Chunking strategy used, e.g. "FixedSize", "Sentence", "Paragraph".</summary>
    public string? ChunkingStrategy { get; set; }

    /// <summary>ID of the embedding model used to embed this document's chunks.</summary>
    public int? EmbeddingModelId { get; set; }

    /// <summary>Current processing status of the document.</summary>
    public DocumentStatus Status { get; set; } = DocumentStatus.Uploaded;

    /// <summary>Error message if processing failed.</summary>
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public string? ArchivedByUserId { get; set; }

    public DateTime? ArchivedAt { get; set; }

    public string? ArchiveReason { get; set; }

    public DocumentStatus? ArchivedFromStatus { get; set; }

    // Navigation properties
    public Course Course { get; set; } = null!;
    public EmbeddingModel? EmbeddingModel { get; set; }
    public ApplicationUser? ArchivedByUser { get; set; }
    public ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
}
