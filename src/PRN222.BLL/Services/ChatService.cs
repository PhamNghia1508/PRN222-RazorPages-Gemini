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
        var courseIdSet = visibleCourseIds.ToHashSet();
        var messagesForAnalytics = await _messageRepository.GetQueryable()
            .Include(m => m.Citations)
                .ThenInclude(c => c.Chunk)
                    .ThenInclude(chk => chk.Document)
            .Where(m => m.Role == MessageRole.Assistant)
            .Where(m =>
                (m.CourseId.HasValue && courseIdSet.Contains(m.CourseId.Value)) ||
                (!m.CourseId.HasValue &&
                 m.Citations.Any(c => courseIdSet.Contains(c.Chunk.Document.CourseId))))
            .ToListAsync();

        // Calculate KPIs
        int totalQueries = messagesForAnalytics.Count;

        var votedMessages = messagesForAnalytics.Where(m => m.IsHelpful.HasValue).ToList();
        double helpfulnessRate = votedMessages.Any()
            ? (double)votedMessages.Count(m => m.IsHelpful == true) / votedMessages.Count
            : 0.0;

        var confidenceMessages = messagesForAnalytics.Where(m => m.ConfidenceScore.HasValue).ToList();
        double avgConfidenceScore = confidenceMessages.Any()
            ? (double)confidenceMessages.Average(m => m.ConfidenceScore!.Value)
            : 0.0;

        // 2. Identify Failed / Low Confidence / Downvoted Queries
        var failedQueryEntities = messagesForAnalytics
            .Where(m => m.IsHelpful == false || (m.ConfidenceScore.HasValue && m.ConfidenceScore.Value < 0.5f))
            .OrderByDescending(m => m.CreatedAt)
            .Take(15)
            .ToList();

        var failedQueries = new List<FailedQueryDto>();
        var failedSessionIds = failedQueryEntities
            .Select(message => message.SessionId)
            .ToHashSet();
        var userMessagesBySession = (await _messageRepository.GetQueryable()
                .Where(message =>
                    failedSessionIds.Contains(message.SessionId) &&
                    message.Role == MessageRole.User)
                .ToListAsync())
            .GroupBy(message => message.SessionId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(message => message.Id).ToList());

        var allCourses = await _courseRepository.GetAllAsync();
        var courseMap = allCourses.ToDictionary(c => c.Id, c => c.Name);

        foreach (var m in failedQueryEntities)
        {
            userMessagesBySession.TryGetValue(m.SessionId, out var sessionUserMessages);
            var questionMsg = sessionUserMessages?
                .LastOrDefault(other => other.Id < m.Id);

            string questionText = questionMsg?.Content ?? "Không rõ câu hỏi";

            int? courseId = m.CourseId;
            courseId ??= m.Citations
                .Select(c => c.Chunk?.Document?.CourseId)
                .FirstOrDefault(id => id.HasValue);
            if (!courseId.HasValue)
            {
                courseId = messagesForAnalytics
                    .Where(other => other.SessionId == m.SessionId)
                    .SelectMany(other => other.Citations)
                    .Select(c => c.Chunk?.Document?.CourseId)
                    .FirstOrDefault(id => id.HasValue);
            }
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
        var documentCitations = messagesForAnalytics
            .SelectMany(m => m.Citations)
            .Where(c => c.Chunk?.Document != null && courseIdSet.Contains(c.Chunk.Document.CourseId))
            .GroupBy(c => c.Chunk.DocumentId)
            .Select(g => new {
                DocumentId = g.Key,
                Count = g.Count(),
                Doc = g.First().Chunk.Document
            })
            .OrderByDescending(g => g.Count)
            .Take(10)
            .ToList();

        var topCitedDocuments = documentCitations.Select(dc => new TopDocumentDto(
            dc.DocumentId,
            dc.Doc.OriginalFileName ?? dc.Doc.FileName,
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
