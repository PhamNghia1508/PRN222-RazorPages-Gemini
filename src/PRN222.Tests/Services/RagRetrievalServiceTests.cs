using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using PRN222.BLL.Services.Interfaces;
using PRN222.BLL.Services.Rag;
using PRN222.DAL.Entities;
using PRN222.DAL.Entities.Enums;
using PRN222.DAL.Repositories.Interfaces;
using System.Text.Json;

namespace PRN222.Tests.Services;

public class RagRetrievalServiceTests
{
    private readonly Mock<IDocumentRepository> _documentRepositoryMock = new();
    private readonly Mock<IChunkRepository> _chunkRepositoryMock = new();
    private readonly Mock<IEmbeddingService> _embeddingServiceMock = new();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
    private readonly Mock<ILogger<RagRetrievalService>> _loggerMock = new();

    [Fact]
    public void RankChunks_ShouldFilterSortAndLimitRelevantChunks()
    {
        var service = CreateService();
        var chunks = new List<DocumentChunk>
        {
            new() { Id = 1, Content = "best" },
            new() { Id = 2, Content = "weak" },
            new() { Id = 3, Content = "second" }
        };
        var embeddings = new Dictionary<int, float[]>
        {
            [1] = new[] { 1f, 0f },
            [2] = new[] { 0.2f, 0.8f },
            [3] = new[] { 0.8f, 0.2f }
        };

        var results = service.RankChunks(
            new[] { 1f, 0f },
            chunks,
            embeddings,
            minSimilarity: 0.5f,
            maxResults: 2);

        results.Select(r => r.Chunk.Id).Should().Equal(1, 3);
        results.Select(r => r.Similarity).Should().BeInDescendingOrder();
    }

    [Fact]
    public void BuildChatContext_ShouldIncludeDocumentNameSimilarityAndContent()
    {
        var service = CreateService();
        var chunks = new[]
        {
            new RetrievedChunk(
                new DocumentChunk { Id = 10, DocumentId = 5, Content = "MVC stands for Model View Controller." },
                0.9123f)
        };

        var context = service.BuildChatContext(
            chunks,
            new Dictionary<int, string> { [5] = "lesson.pdf" });

        context.Should().Contain("lesson.pdf");
        context.Should().Contain("91.2");
        context.Should().Contain("MVC stands for Model View Controller.");
        context.Should().Contain("---");
    }

    [Fact]
    public void BuildChatPrompt_ShouldUseSelectedCourseAndAvoidPrn222Hardcoding()
    {
        var service = CreateService();

        var prompt = service.BuildChatPrompt(
            question: "Java collection là gì?",
            context: "List, Set và Map là các collection phổ biến.",
            courseName: "Java");

        prompt.Should().Contain("Java");
        prompt.Should().NotContain("môn PRN222");
        prompt.Should().NotContain("C#/.NET");
    }

    [Fact]
    public async Task SearchCourseAsync_ShouldRetrieveOnlyIndexedDocumentsAndScoreMatchingEmbeddings()
    {
        var service = CreateService();
        _embeddingServiceMock.SetupGet(s => s.ModelName).Returns("test-model");
        _embeddingServiceMock.Setup(s => s.GenerateEmbeddingAsync("What is MVC?"))
            .ReturnsAsync(new[] { 1f, 0f });

        _documentRepositoryMock.Setup(r => r.GetByCourseIdAsync(7))
            .ReturnsAsync(new[]
            {
                new Document { Id = 1, OriginalFileName = "indexed.pdf", Status = DocumentStatus.Indexed },
                new Document { Id = 2, OriginalFileName = "uploaded.pdf", Status = DocumentStatus.Uploaded },
                new Document { Id = 3, OriginalFileName = "archived.pdf", Status = DocumentStatus.Archived }
            });

        _chunkRepositoryMock.Setup(r => r.GetChunksWithEmbeddingsByDocumentIdsAsync(
                It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 1 }))))
            .ReturnsAsync(new[]
            {
                new DocumentChunk
                {
                    Id = 10,
                    DocumentId = 1,
                    Content = "MVC content",
                    Embeddings =
                    {
                        new ChunkEmbedding
                        {
                            EmbeddingModelName = "test-model",
                            EmbeddingVector = JsonSerializer.Serialize(new[] { 1f, 0f })
                        }
                    }
                }
            });

        var result = await service.SearchCourseAsync(
            courseId: 7,
            query: "What is MVC?",
            embeddingService: _embeddingServiceMock.Object,
            minSimilarity: 0.45f,
            maxResults: 3);

        result.TopChunks.Should().ContainSingle();
        result.TopChunks[0].Chunk.Id.Should().Be(10);
        result.DocumentNames[1].Should().Be("indexed.pdf");
    }

    private RagRetrievalService CreateService()
    {
        return new RagRetrievalService(
            _documentRepositoryMock.Object,
            _chunkRepositoryMock.Object,
            _cache,
            _loggerMock.Object);
    }
}
