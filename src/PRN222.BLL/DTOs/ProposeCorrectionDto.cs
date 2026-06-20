namespace PRN222.BLL.DTOs;

public record ProposeCorrectionDto(
    int CourseId,
    int? MessageId,
    string TriggeredQuestion,
    string SuggestedAnswer
);
