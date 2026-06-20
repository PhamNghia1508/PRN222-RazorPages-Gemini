namespace PRN222.BLL.DTOs;

public record ChatCitationDto(
    int Id,
    int MessageId,
    int ChunkId,
    float RelevanceScore,
    string SnippetText,
    string DocumentName,
    int StartPage,
    int EndPage,
    string? ImageUrl = null);
