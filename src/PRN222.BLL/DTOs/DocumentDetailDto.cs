namespace PRN222.BLL.DTOs;

/// <summary>
/// DTO for detailed document view including chunks.
/// </summary>
public class DocumentDetailDto
{
    public int Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public int ChunkCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ChunkingStrategy { get; set; }
    public string? ErrorMessage { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public int CourseId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Preview of the extracted text (first 2000 characters).</summary>
    public string? ExtractedTextPreview { get; set; }

    /// <summary>List of chunks for this document.</summary>
    public List<ChunkDto> Chunks { get; set; } = new();
}
