namespace PRN222.BLL.DTOs;

/// <summary>
/// DTO for a document chunk.
/// </summary>
public record ChunkDto(
    int Id,
    int ChunkIndex,
    string Content,
    int TokenCount,
    int? StartPage,
    int? EndPage,
    string? ChapterSection);
