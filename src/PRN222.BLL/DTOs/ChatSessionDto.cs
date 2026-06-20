namespace PRN222.BLL.DTOs;

public record ChatSessionDto(
    int Id,
    string SessionTitle,
    DateTime CreatedAt,
    DateTime? LastMessageAt,
    int MessageCount);
