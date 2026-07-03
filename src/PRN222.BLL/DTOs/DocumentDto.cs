namespace PRN222.BLL.DTOs;

/// <summary>
/// DTO for document list display.
/// </summary>
public record DocumentDto(
    int Id,
    string FileName,
    string OriginalFileName,
    string ContentType,
    long FileSize,
    int ChunkCount,
    string Status,
    string CourseName,
    int CourseId,
    DateTime CreatedAt,
    string? UploadedByEmail = null,
    DateTime? UploadedAt = null,
    string? UploadedByUserId = null);
