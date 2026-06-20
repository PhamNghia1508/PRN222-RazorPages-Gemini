namespace PRN222.BLL.DTOs;

public record ChatMessageDto(
    int Id,
    int SessionId,
    string Role,
    string Content,
    float? ConfidenceScore,
    DateTime CreatedAt,
    List<ChatCitationDto> Citations,
    bool? IsHelpful = null);
