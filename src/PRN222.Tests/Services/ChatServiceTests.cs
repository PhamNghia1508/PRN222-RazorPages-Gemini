using FluentAssertions;
using Moq;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services;
using PRN222.BLL.Services.Interfaces;
using PRN222.BLL.Services.Rag;
using PRN222.DAL.Entities;
using PRN222.DAL.Entities.Enums;
using PRN222.DAL.Repositories.Interfaces;

namespace PRN222.Tests.Services;

public class ChatServiceTests
{
    private readonly Mock<IChatSessionRepository> _sessionRepoMock = new();
    private readonly Mock<IChatMessageRepository> _messageRepoMock = new();
    private readonly Mock<IEmbeddingService> _embeddingServiceMock = new();
    private readonly Mock<IRagRetrievalService> _ragRetrievalServiceMock = new();
    private readonly Mock<ILlmService> _llmServiceMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRepository<Course>> _courseRepoMock = new();
    private readonly ChatService _chatService;

    public ChatServiceTests()
    {
        _embeddingServiceMock.SetupGet(e => e.ModelName).Returns("test-model");
        _ragRetrievalServiceMock.Setup(s => s.SearchCourseAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<IEmbeddingService>(),
                It.IsAny<float>(),
                It.IsAny<int>()))
            .ReturnsAsync(new RagSearchResult(
                new List<RetrievedChunk>(),
                new Dictionary<int, string>()));
        _ragRetrievalServiceMock.Setup(s => s.BuildChatContext(
                It.IsAny<IEnumerable<RetrievedChunk>>(),
                It.IsAny<IReadOnlyDictionary<int, string>>()))
            .Returns("");
        _ragRetrievalServiceMock.Setup(s => s.BuildChatPrompt(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns((string question, string context, string courseName) => $"Prompt: {courseName}\n{question}\n{context}");

        _chatService = new ChatService(
            _sessionRepoMock.Object,
            _messageRepoMock.Object,
            _embeddingServiceMock.Object,
            _ragRetrievalServiceMock.Object,
            _llmServiceMock.Object,
            _unitOfWorkMock.Object,
            _courseRepoMock.Object);
    }

    [Fact]
    public async Task AskQuestionAsync_ShouldCreateSession_WhenSessionIdIsNull()
    {
        var dto = new AskQuestionDto { CourseId = 1, SessionId = null, Question = "What is ASP.NET Core?", UserId = "user-1" };

        _sessionRepoMock.Setup(r => r.AddAsync(It.IsAny<ChatSession>()))
            .Callback<ChatSession>(s => s.Id = 99)
            .Returns(Task.CompletedTask);
        _llmServiceMock.Setup(l => l.GenerateAnswerAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("ASP.NET Core is a cross-platform framework.");

        var result = await _chatService.AskQuestionAsync(dto);

        result.Should().NotBeNull();
        result.SessionId.Should().Be(99);
        _sessionRepoMock.Verify(r => r.AddAsync(It.Is<ChatSession>(s => s.UserId == "user-1")), Times.Once);
    }

    [Fact]
    public async Task AskQuestionAsync_ShouldReturnTopChunkCitationsFromRagRetrieval()
    {
        var dto = new AskQuestionDto { CourseId = 1, SessionId = 1, Question = "What is MVC?", UserId = "user-1" };
        var matchingChunk = new DocumentChunk
        {
            Id = 10,
            DocumentId = 1,
            Content = "MVC stands for Model-View-Controller.",
            Document = new Document { OriginalFileName = "test1.pdf" }
        };

        _sessionRepoMock.Setup(r => r.GetByIdForUserAsync(1, "user-1")).ReturnsAsync(new ChatSession { Id = 1, UserId = "user-1" });
        _ragRetrievalServiceMock.Setup(s => s.SearchCourseAsync(
                1,
                dto.Question,
                _embeddingServiceMock.Object,
                0.45f,
                3))
            .ReturnsAsync(new RagSearchResult(
                new List<RetrievedChunk> { new(matchingChunk, 1.0f) },
                new Dictionary<int, string> { [1] = "test1.pdf" }));
        _llmServiceMock.Setup(l => l.GenerateAnswerAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("MVC is an architectural pattern.");

        var result = await _chatService.AskQuestionAsync(dto);

        result.Should().NotBeNull();
        result.Citations.Should().ContainSingle();
        result.Citations.First().ChunkId.Should().Be(10);
        result.Citations.First().RelevanceScore.Should().BeApproximately(1.0f, 0.001f);
    }

    private async Task AskQuestionAsync_ShouldReturnCourseAwareNoContextAnswer_WhenNoChunksMatch()
    {
        var dto = new AskQuestionDto { CourseId = 5, SessionId = null, Question = "Java collection là gì?", UserId = "user-1" };

        _sessionRepoMock.Setup(r => r.AddAsync(It.IsAny<ChatSession>()))
            .Callback<ChatSession>(s => s.Id = 50)
            .Returns(Task.CompletedTask);
        _llmServiceMock.Setup(l => l.GenerateAnswerAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("Xin lỗi, không tìm thấy tài liệu liên quan.");

        var result = await _chatService.AskQuestionAsync(dto);

        result.Should().NotBeNull();
        result.Content.Should().Contain("không tìm thấy");
        result.Citations.Should().BeEmpty();
        result.ConfidenceScore.Should().Be(0);
    }

    [Fact]
    public async Task AskQuestionAsync_ShouldUseSelectedCourseName_WhenCourseHasNoRetrievedContext()
    {
        var dto = new AskQuestionDto { CourseId = 5, SessionId = null, Question = "Java collection là gì?", UserId = "user-1" };

        _sessionRepoMock.Setup(r => r.AddAsync(It.IsAny<ChatSession>()))
            .Callback<ChatSession>(s => s.Id = 51)
            .Returns(Task.CompletedTask);
        _courseRepoMock.Setup(r => r.GetByIdAsync(5))
            .ReturnsAsync(new Course { Id = 5, Name = "Java" });

        var result = await _chatService.AskQuestionAsync(dto);

        result.Should().NotBeNull();
        result.Content.Should().Contain("Java");
        result.Content.Should().Contain("chưa có tài liệu");
        result.Content.Should().NotContain("PRN222");
        result.Content.Should().NotContain("C#");
        result.Citations.Should().BeEmpty();
        result.ConfidenceScore.Should().Be(0);
        _llmServiceMock.Verify(l => l.GenerateAnswerAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetSessionByIdAsync_ShouldReturnNull_WhenSessionBelongsToAnotherUser()
    {
        _sessionRepoMock.Setup(r => r.GetByIdForUserAsync(7, "user-1"))
            .ReturnsAsync((ChatSession?)null);

        var result = await _chatService.GetSessionByIdAsync(7, "user-1");

        result.Should().BeNull();
        _sessionRepoMock.Verify(r => r.GetByIdForUserAsync(7, "user-1"), Times.Once);
    }

    [Fact]
    public async Task SubmitFeedbackAsync_ShouldOnlyUpdateAssistantMessageOwnedByUser()
    {
        var ownedMessage = new ChatMessage
        {
            Id = 10,
            Role = MessageRole.Assistant,
            Session = new ChatSession { UserId = "user-1" }
        };
        var messages = new List<ChatMessage> { ownedMessage };
        _messageRepoMock.Setup(r => r.GetQueryable()).Returns(messages.AsAsyncQueryable());

        var result = await _chatService.SubmitFeedbackAsync(10, true, "user-1");

        result.Should().BeTrue();
        ownedMessage.IsHelpful.Should().BeTrue();
        _messageRepoMock.Verify(r => r.Update(ownedMessage), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Theory]
    [InlineData("another-user", MessageRole.Assistant)]
    [InlineData("user-1", MessageRole.User)]
    public async Task SubmitFeedbackAsync_ShouldRejectMessageOutsideOwnershipOrAssistantRole(
        string sessionOwnerId,
        MessageRole role)
    {
        var message = new ChatMessage
        {
            Id = 10,
            Role = role,
            Session = new ChatSession { UserId = sessionOwnerId }
        };
        var messages = new List<ChatMessage> { message };
        _messageRepoMock.Setup(r => r.GetQueryable()).Returns(messages.AsAsyncQueryable());

        var result = await _chatService.SubmitFeedbackAsync(10, true, "user-1");

        result.Should().BeFalse();
        message.IsHelpful.Should().BeNull();
        _messageRepoMock.Verify(r => r.Update(It.IsAny<ChatMessage>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task GetStudentAnalyticsAsync_ShouldUsePersistedCourseScopeAndNotLeakOtherCourses()
    {
        var messages = new List<ChatMessage>
        {
            new()
            {
                Id = 1,
                SessionId = 1,
                CourseId = 100,
                Role = MessageRole.Assistant,
                Content = "Visible answer with evidence",
                ConfidenceScore = 0.8f
            },
            new()
            {
                Id = 2,
                SessionId = 2,
                CourseId = 200,
                Role = MessageRole.Assistant,
                Content = "Other course answer",
                ConfidenceScore = 0.8f
            },
            new()
            {
                Id = 3,
                SessionId = 3,
                CourseId = 100,
                Role = MessageRole.Assistant,
                Content = "Visible answer without evidence",
                ConfidenceScore = 0.8f
            }
        };
        _messageRepoMock.Setup(r => r.GetQueryable()).Returns(messages.AsAsyncQueryable());
        _courseRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Course>
        {
            new() { Id = 100, Name = "PRN222" },
            new() { Id = 200, Name = "Other" }
        });

        var result = await _chatService.GetStudentAnalyticsAsync(new[] { 100 });

        result.TotalQueries.Should().Be(2);
        result.HelpfulnessRate.Should().Be(0);
    }

    [Fact]
    public async Task GetStudentAnalyticsAsync_ShouldLoadFailedQuestionsInSingleBulkQuery()
    {
        var messages = new List<ChatMessage>
        {
            new()
            {
                Id = 1,
                SessionId = 1,
                CourseId = 100,
                Role = MessageRole.User,
                Content = "Question one"
            },
            new()
            {
                Id = 2,
                SessionId = 1,
                CourseId = 100,
                Role = MessageRole.Assistant,
                Content = "Answer one",
                ConfidenceScore = 0.2f
            },
            new()
            {
                Id = 3,
                SessionId = 2,
                CourseId = 100,
                Role = MessageRole.User,
                Content = "Question two"
            },
            new()
            {
                Id = 4,
                SessionId = 2,
                CourseId = 100,
                Role = MessageRole.Assistant,
                Content = "Answer two",
                ConfidenceScore = 0.8f,
                IsHelpful = false
            }
        };
        _messageRepoMock.Setup(r => r.GetQueryable()).Returns(messages.AsAsyncQueryable());
        _courseRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Course>
        {
            new() { Id = 100, Name = "PRN222" }
        });

        var result = await _chatService.GetStudentAnalyticsAsync(new[] { 100 });

        result.FailedQueries.Select(q => q.Question)
            .Should()
            .BeEquivalentTo("Question one", "Question two");
        _messageRepoMock.Verify(r => r.GetQueryable(), Times.Exactly(2));
    }
}
