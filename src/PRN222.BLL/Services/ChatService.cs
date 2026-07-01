using PRN222.BLL.DTOs;
using PRN222.BLL.Services.Interfaces;
using PRN222.DAL.Entities;
using PRN222.DAL.Entities.Enums;
using PRN222.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace PRN222.BLL.Services;

public class ChatService : IChatService
{
    private readonly IChatSessionRepository _sessionRepository;
    private readonly IChatMessageRepository _messageRepository;
    private readonly IEmbeddingService _embeddingService;
    private readonly IRagRetrievalService _ragRetrievalService;
    private readonly ILlmService _llmService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRepository<Course> _courseRepository;

    public ChatService(
        IChatSessionRepository sessionRepository,
        IChatMessageRepository messageRepository,
        IEmbeddingService embeddingService,
        IRagRetrievalService ragRetrievalService,
        ILlmService llmService,
        IUnitOfWork unitOfWork,
        IRepository<Course> courseRepository)
    {
        _sessionRepository = sessionRepository;
        _messageRepository = messageRepository;
        _embeddingService = embeddingService;
        _ragRetrievalService = ragRetrievalService;
        _llmService = llmService;
        _unitOfWork = unitOfWork;
        _courseRepository = courseRepository;
    }

    public async Task<IEnumerable<ChatSessionDto>> GetSessionsAsync(string userId)
    {
        EnsureUserId(userId);
        var sessions = await _sessionRepository.GetSessionsAsync(userId);
        return sessions.Select(s => new ChatSessionDto(
            s.Id,
            s.SessionTitle,
            s.CreatedAt,
            s.LastMessageAt,
            0));
    }

    public async Task<ChatSessionDto?> GetSessionByIdAsync(int sessionId, string userId)
    {
        EnsureUserId(userId);
        var session = await _sessionRepository.GetByIdForUserAsync(sessionId, userId);
        if (session == null) return null;

        return new ChatSessionDto(
            session.Id,
            session.SessionTitle,
            session.CreatedAt,
            session.LastMessageAt,
            0);
    }

    public async Task<IEnumerable<ChatMessageDto>> GetMessagesBySessionIdAsync(int sessionId, string userId)
    {
        EnsureUserId(userId);
        var session = await _sessionRepository.GetByIdForUserAsync(sessionId, userId);
        if (session == null)
        {
            throw new InvalidOperationException($"Session {sessionId} not found.");
        }

        var messages = await _messageRepository.GetMessagesBySessionIdAsync(sessionId);
        return messages.Select(MapToMessageDto);
    }

    public async Task<ChatSessionDto> CreateSessionAsync(string title, string userId)
    {
        EnsureUserId(userId);
        var session = new ChatSession
        {
            UserId = userId,
            SessionTitle = string.IsNullOrWhiteSpace(title) ? "Cuộc hội thoại mới" : title,
            CreatedAt = DateTime.UtcNow
        };

        await _sessionRepository.AddAsync(session);
        await _unitOfWork.SaveChangesAsync();

        return new ChatSessionDto(session.Id, session.SessionTitle, session.CreatedAt, null, 0);
    }

    public async Task<ChatMessageDto> AskQuestionAsync(AskQuestionDto dto)
    {
        EnsureUserId(dto.UserId);
        var session = await ResolveSessionAsync(dto);
        await SaveUserMessageAsync(session, dto.Question, dto.CourseId);
        var courseName = await GetCourseNameAsync(dto.CourseId);

        var retrieval = await _ragRetrievalService.SearchCourseAsync(
            dto.CourseId,
            dto.Question,
            _embeddingService,
            minSimilarity: 0.45f,
            maxResults: 3);
        var topChunks = retrieval.TopChunks;
        var context = _ragRetrievalService.BuildChatContext(topChunks, retrieval.DocumentNames);
        var answer = topChunks.Any()
            ? await _llmService.GenerateAnswerAsync(
                _ragRetrievalService.BuildChatPrompt(dto.Question, context, courseName),
                context)
            : BuildNoContextAnswer(courseName, retrieval.DocumentNames.Any());

        var assistantMsg = new ChatMessage
        {
            SessionId = session.Id,
            CourseId = dto.CourseId,
            Role = MessageRole.Assistant,
            Content = answer,
            ConfidenceScore = topChunks.Any() ? topChunks.First().Similarity : 0,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var item in topChunks)
        {
            assistantMsg.Citations.Add(new ChatCitation
            {
                ChunkId = item.Chunk.Id,
                RelevanceScore = item.Similarity,
                SnippetText = item.Chunk.Content.Length > 200
                    ? item.Chunk.Content[..200] + "..."
                    : item.Chunk.Content
            });
        }

        await _messageRepository.AddAsync(assistantMsg);
        session.LastMessageAt = DateTime.UtcNow;
        _sessionRepository.Update(session);
        await _unitOfWork.SaveChangesAsync();

        return new ChatMessageDto(
            assistantMsg.Id,
            assistantMsg.SessionId,
            assistantMsg.Role.ToString(),
            assistantMsg.Content,
            assistantMsg.ConfidenceScore,
            assistantMsg.CreatedAt,
            topChunks.Select(tc =>
            {
                retrieval.DocumentNames.TryGetValue(tc.Chunk.DocumentId, out var documentName);

                return new ChatCitationDto(
                    0,
                    assistantMsg.Id,
                    tc.Chunk.Id,
                    tc.Similarity,
                    tc.Chunk.Content.Length > 200 ? tc.Chunk.Content[..200] + "..." : (tc.Chunk.Content ?? ""),
                    string.IsNullOrWhiteSpace(documentName) ? "Tài liệu" : documentName,
                    tc.Chunk.StartPage ?? 0,
                    tc.Chunk.EndPage ?? 0,
                    tc.Chunk.ImageUrl);
            }).ToList());
    }

    private async Task<ChatSession> ResolveSessionAsync(AskQuestionDto dto)
    {
        if (!dto.SessionId.HasValue || dto.SessionId.Value == 0)
        {
            var title = dto.Question.Length > 30 ? dto.Question[..30] + "..." : dto.Question;
            var session = new ChatSession
            {
                UserId = dto.UserId,
                SessionTitle = string.IsNullOrWhiteSpace(title) ? "Cuộc hội thoại mới" : title,
                CreatedAt = DateTime.UtcNow
            };
            await _sessionRepository.AddAsync(session);
            await _unitOfWork.SaveChangesAsync();
            return session;
        }

        return await _sessionRepository.GetByIdForUserAsync(dto.SessionId.Value, dto.UserId)
            ?? throw new InvalidOperationException($"Session {dto.SessionId.Value} not found.");
    }

    private static void EnsureUserId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("UserId is required.", nameof(userId));
        }
    }

    private async Task SaveUserMessageAsync(ChatSession session, string question, int courseId)
    {
        var userMsg = new ChatMessage
        {
            SessionId = session.Id,
            CourseId = courseId,
            Role = MessageRole.User,
            Content = question,
            CreatedAt = DateTime.UtcNow
        };
        await _messageRepository.AddAsync(userMsg);

        session.LastMessageAt = DateTime.UtcNow;
        _sessionRepository.Update(session);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<string> GetCourseNameAsync(int courseId)
    {
        var course = await _courseRepository.GetByIdAsync(courseId);
        return string.IsNullOrWhiteSpace(course?.Name)
            ? $"môn #{courseId}"
            : course.Name;
    }

    private static string BuildNoContextAnswer(string courseName, bool hasAnyCourseDocuments)
    {
        if (!hasAnyCourseDocuments)
        {
            return $"Hiện môn {courseName} chưa có tài liệu đã index trong kho tri thức, nên tôi chưa thể trả lời dựa trên nguồn học liệu của môn này. Vui lòng chọn môn có tài liệu hoặc nhờ giảng viên/trưởng bộ môn nạp tài liệu cho {courseName}.";
        }

        return $"Tôi chưa tìm thấy đoạn tài liệu phù hợp trong môn {courseName} cho câu hỏi này. Bạn có thể hỏi cụ thể hơn, hoặc kiểm tra lại tài liệu của môn đã được xử lý và index đầy đủ chưa.";
    }

    private static ChatMessageDto MapToMessageDto(ChatMessage m)
    {
        return new ChatMessageDto(
            m.Id,
            m.SessionId,
            m.Role.ToString(),
            m.Content,
            m.ConfidenceScore,
            m.CreatedAt,
            m.Citations.Select(c => new ChatCitationDto(
                c.Id,
                c.MessageId,
                c.ChunkId,
                c.RelevanceScore,
                c.SnippetText ?? "",
                c.Chunk?.Document?.OriginalFileName ?? "Tài liệu",
                c.Chunk?.StartPage ?? 0,
                c.Chunk?.EndPage ?? 0,
                c.Chunk?.ImageUrl)).ToList(),
            m.IsHelpful);
    }

    public async Task<bool> SubmitFeedbackAsync(int messageId, bool isHelpful, string userId)
    {
        EnsureUserId(userId);
        var message = await _messageRepository.GetQueryable()
            .Include(m => m.Session)
            .FirstOrDefaultAsync(m =>
                m.Id == messageId &&
                m.Role == MessageRole.Assistant &&
                m.Session.UserId == userId);
        if (message == null) return false;

        message.IsHelpful = isHelpful;
        _messageRepository.Update(message);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<StudentAnalyticsDto> GetStudentAnalyticsAsync(IEnumerable<int> visibleCourseIds)
    {
        var courseIdList = visibleCourseIds.Distinct().ToList();
        if (courseIdList.Count == 0)
        {
            return new StudentAnalyticsDto(0, 0.0, 0.0, new List<FailedQueryDto>(), new List<TopDocumentDto>());
        }

        var visibleAssistantMessages = _messageRepository.GetQueryable()
            .AsNoTracking()
            .Where(m => m.Role == MessageRole.Assistant)
            .Where(m =>
                (m.CourseId.HasValue && courseIdList.Contains(m.CourseId.Value)) ||
                (!m.CourseId.HasValue &&
                 m.Citations.Any(c => courseIdList.Contains(c.Chunk.Document.CourseId))));

        var totalQueries = await visibleAssistantMessages.CountAsync();
        var votedCount = await visibleAssistantMessages.CountAsync(m => m.IsHelpful.HasValue);
        var helpfulnessRate = votedCount > 0
            ? (double)await visibleAssistantMessages.CountAsync(m => m.IsHelpful == true) / votedCount
            : 0.0;
        var avgConfidenceScore = await visibleAssistantMessages
            .Where(m => m.ConfidenceScore.HasValue)
            .AverageAsync(m => (double?)m.ConfidenceScore) ?? 0.0;

        var failedQueryEntities = await visibleAssistantMessages
            .Where(m => m.IsHelpful == false || (m.ConfidenceScore.HasValue && m.ConfidenceScore.Value < 0.5f))
            .OrderByDescending(m => m.CreatedAt)
            .Take(15)
            .Select(m => new
            {
                m.Id,
                m.SessionId,
                m.Content,
                m.ConfidenceScore,
                m.IsHelpful,
                m.CreatedAt,
                CourseId = m.CourseId ?? m.Citations
                    .Select(c => (int?)c.Chunk.Document.CourseId)
                    .FirstOrDefault()
            })
            .ToListAsync();

        var failedQueries = new List<FailedQueryDto>();
        var failedSessionIds = failedQueryEntities
            .Select(message => message.SessionId)
            .ToHashSet();
        var userMessagesBySession = failedSessionIds.Count == 0
            ? new Dictionary<int, List<ChatMessage>>()
            : (await _messageRepository.GetQueryable()
                .AsNoTracking()
                .Where(message =>
                    failedSessionIds.Contains(message.SessionId) &&
                    message.Role == MessageRole.User)
                .Select(message => new ChatMessage
                {
                    Id = message.Id,
                    SessionId = message.SessionId,
                    Content = message.Content
                })
                .ToListAsync())
                .GroupBy(message => message.SessionId)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(message => message.Id).ToList());

        var courseMap = (await _courseRepository.GetAllAsync())
            .Where(course => courseIdList.Contains(course.Id))
            .ToDictionary(course => course.Id, course => course.Name);

        foreach (var m in failedQueryEntities)
        {
            userMessagesBySession.TryGetValue(m.SessionId, out var sessionUserMessages);
            var questionMsg = sessionUserMessages?
                .LastOrDefault(other => other.Id < m.Id);

            string questionText = questionMsg?.Content ?? "Không rõ câu hỏi";

            int? courseId = m.CourseId;
            string courseName = (courseId.HasValue && courseMap.TryGetValue(courseId.Value, out var name))
                ? name
                : "Chưa phân loại";

            failedQueries.Add(new FailedQueryDto(
                m.Id,
                questionText,
                m.Content,
                m.ConfidenceScore,
                m.IsHelpful,
                courseName,
                m.CreatedAt,
                courseId
            ));
        }

        // 3. Top Cited Documents
        var documentCitations = await visibleAssistantMessages
            .SelectMany(m => m.Citations)
            .Where(c => courseIdList.Contains(c.Chunk.Document.CourseId))
            .GroupBy(c => new
            {
                c.Chunk.DocumentId,
                c.Chunk.Document.OriginalFileName,
                c.Chunk.Document.FileName,
                c.Chunk.Document.CourseId
            })
            .Select(g => new {
                g.Key.DocumentId,
                DocumentName = g.Key.OriginalFileName ?? g.Key.FileName,
                g.Key.CourseId,
                Doc = new { CourseId = g.Key.CourseId },
                Count = g.Count(),
            })
            .OrderByDescending(g => g.Count)
            .Take(10)
            .ToListAsync();

        var topCitedDocuments = documentCitations.Select(dc => new TopDocumentDto(
            dc.DocumentId,
            dc.DocumentName,
            courseMap.TryGetValue(dc.Doc.CourseId, out var cName) ? cName : "Chưa phân loại",
            dc.Count
        )).ToList();

        return new StudentAnalyticsDto(
            totalQueries,
            helpfulnessRate,
            avgConfidenceScore,
            failedQueries,
            topCitedDocuments
        );
    }
}
