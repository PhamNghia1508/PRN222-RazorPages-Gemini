namespace PRN222.BLL.DTOs;

/// <summary>
/// DTO for document upload request.
/// </summary>
public class DocumentUploadDto
{
    /// <summary>Course to associate the document with.</summary>
    public int CourseId { get; set; }

    /// <summary>Original file name.</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>MIME type of the uploaded file.</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Size of the file in bytes.</summary>
    public long FileSize { get; set; }

    /// <summary>Chunk size in characters for text splitting.</summary>
    public int ChunkSize { get; set; } = 512;

    /// <summary>Overlap in characters between consecutive chunks.</summary>
    public int ChunkOverlap { get; set; } = 50;
}
