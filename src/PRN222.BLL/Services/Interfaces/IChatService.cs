using PRN222.BLL.DTOs;

namespace PRN222.BLL.Services.Interfaces;

public interface IChatService
{
    Task<IEnumerable<ChatSessionDto>> GetSessionsAsync(string userId);
    Task<ChatSessionDto?> GetSessionByIdAsync(int sessionId, string userId);
    Task<IEnumerable<ChatMessageDto>> GetMessagesBySessionIdAsync(int sessionId, string userId);
    Task<ChatSessionDto> CreateSessionAsync(string title, string userId);
    Task<ChatMessageDto> AskQuestionAsync(AskQuestionDto dto);
    Task<bool> SubmitFeedbackAsync(int messageId, bool isHelpful, string userId);
    Task<StudentAnalyticsDto> GetStudentAnalyticsAsync(IEnumerable<int> visibleCourseIds);
}
