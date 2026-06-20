using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PRN222.BLL.Services;
using PRN222.BLL.Services.Interfaces;
using PRN222.DAL.Entities;
using PRN222.DAL.Repositories.Interfaces;
using Xunit;

namespace PRN222.Tests.Services;

public class FinetuneServiceTests
{
    private readonly Mock<IQAPairRepository> _qaPairRepositoryMock = new();
    private readonly Mock<IChunkRepository> _chunkRepositoryMock = new();
    private readonly Mock<ILlmService> _llmServiceMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ILogger<FinetuneService>> _loggerMock = new();

    [Fact]
    public async Task GenerateDatasetForCourseAsync_ShouldGenerateOnlyRemainingPairsUpToTarget()
    {
        var courseId = 1;
        var existingQAs = Enumerable.Range(1, 48)
            .Select(i => new QAPair
            {
                Id = i,
                CourseId = courseId,
                DocumentChunkId = i,
                Question = $"Existing question {i}?",
                Answer = $"Existing answer {i}"
            })
            .ToList();

        var chunks = Enumerable.Range(1, 3)
            .Select(i => new DocumentChunk
            {
                Id = i,
                ChunkIndex = i - 1,
                Content = new string('A', 150),
                Document = new Document { Id = i, CourseId = courseId }
            })
            .ToList();

        _qaPairRepositoryMock.Setup(r => r.GetQueryable())
            .Returns(existingQAs.AsAsyncQueryable());
        _chunkRepositoryMock.Setup(r => r.GetQueryable())
            .Returns(chunks.AsAsyncQueryable());
        _llmServiceMock.Setup(s => s.GenerateQAPairsAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync((string _, int count) => BuildQaJson(count));

        var service = CreateService();

        var generatedCount = await service.GenerateDatasetForCourseAsync(courseId, 50);

        generatedCount.Should().Be(2);
        _qaPairRepositoryMock.Verify(r => r.AddAsync(It.IsAny<QAPair>()), Times.Exactly(2));
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Exactly(2));
        _llmServiceMock.Verify(s => s.GenerateQAPairsAsync(It.IsAny<string>(), 1), Times.Exactly(2));
    }

    [Fact]
    public async Task GenerateDatasetForCourseAsync_ShouldReuseProcessedChunksWhenTargetIsNotMet()
    {
        var courseId = 1;
        var chunks = Enumerable.Range(1, 3)
            .Select(i => new DocumentChunk
            {
                Id = i,
                ChunkIndex = i - 1,
                Content = new string('C', 150),
                Document = new Document { Id = i, CourseId = courseId }
            })
            .ToList();

        var existingQAs = chunks
            .Select(chunk => new QAPair
            {
                Id = chunk.Id,
                CourseId = courseId,
                DocumentChunkId = chunk.Id,
                Question = $"Existing question {chunk.Id}?",
                Answer = $"Existing answer {chunk.Id}"
            })
            .ToList();

        _qaPairRepositoryMock.Setup(r => r.GetQueryable())
            .Returns(existingQAs.AsAsyncQueryable());
        _chunkRepositoryMock.Setup(r => r.GetQueryable())
            .Returns(chunks.AsAsyncQueryable());
        _llmServiceMock.Setup(s => s.GenerateQAPairsAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync((string _, int count) => BuildQaJson(count));

        var service = CreateService();

        var generatedCount = await service.GenerateDatasetForCourseAsync(courseId, 5);

        generatedCount.Should().Be(2);
        _qaPairRepositoryMock.Verify(r => r.AddAsync(It.IsAny<QAPair>()), Times.Exactly(2));
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Exactly(2));
    }

    [Fact]
    public async Task GenerateDatasetForCourseAsync_ShouldSkipInvalidLlmJsonWithoutThrowing()
    {
        var courseId = 1;
        var chunks = new List<DocumentChunk>
        {
            new()
            {
                Id = 10,
                ChunkIndex = 0,
                Content = new string('B', 150),
                Document = new Document { Id = 20, CourseId = courseId }
            }
        };

        _qaPairRepositoryMock.Setup(r => r.GetQueryable())
            .Returns(new List<QAPair>().AsAsyncQueryable());
        _chunkRepositoryMock.Setup(r => r.GetQueryable())
            .Returns(chunks.AsAsyncQueryable());
        _llmServiceMock.Setup(s => s.GenerateQAPairsAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync("Error: network failure");

        var service = CreateService();

        var generatedCount = await service.GenerateDatasetForCourseAsync(courseId, 50);

        generatedCount.Should().Be(0);
        _qaPairRepositoryMock.Verify(r => r.AddAsync(It.IsAny<QAPair>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    private FinetuneService CreateService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:RateLimit:ChunkDelayMs"] = "0",
                ["Gemini:RateLimit:RetryDelayMs"] = "0",
                ["Gemini:RateLimit:MaxRetries"] = "1"
            })
            .Build();

        return new FinetuneService(
            _qaPairRepositoryMock.Object,
            _chunkRepositoryMock.Object,
            _llmServiceMock.Object,
            _unitOfWorkMock.Object,
            configuration,
            _loggerMock.Object);
    }

    private static string BuildQaJson(int count)
    {
        var items = Enumerable.Range(1, count)
            .Select(i => $$"""{"question":"Generated question {{Guid.NewGuid()}}?","answer":"Generated answer {{i}}"}""");

        return "[" + string.Join(",", items) + "]";
    }
}

internal static class AsyncQueryableTestExtensions
{
    public static IQueryable<T> AsAsyncQueryable<T>(this IEnumerable<T> source)
    {
        return new TestAsyncEnumerable<T>(source);
    }
}

internal sealed class TestAsyncQueryProvider<TEntity> : IAsyncQueryProvider
{
    private readonly IQueryProvider _inner;

    public TestAsyncQueryProvider(IQueryProvider inner)
    {
        _inner = inner;
    }

    public IQueryable CreateQuery(Expression expression)
    {
        return new TestAsyncEnumerable<TEntity>(expression);
    }

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        return new TestAsyncEnumerable<TElement>(expression);
    }

    public object? Execute(Expression expression)
    {
        return _inner.Execute(expression);
    }

    public TResult Execute<TResult>(Expression expression)
    {
        return _inner.Execute<TResult>(expression);
    }

    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
    {
        var expectedResultType = typeof(TResult).GetGenericArguments()[0];
        var executionResult = typeof(IQueryProvider)
            .GetMethods()
            .Single(method => method.Name == nameof(IQueryProvider.Execute) && method.IsGenericMethod)
            .MakeGenericMethod(expectedResultType)
            .Invoke(_inner, new object[] { expression });

        return (TResult)typeof(Task)
            .GetMethod(nameof(Task.FromResult))!
            .MakeGenericMethod(expectedResultType)
            .Invoke(null, new[] { executionResult })!;
    }
}

internal sealed class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
{
    public TestAsyncEnumerable(IEnumerable<T> enumerable)
        : base(enumerable)
    {
    }

    public TestAsyncEnumerable(Expression expression)
        : base(expression)
    {
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
    }

    IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
}

internal sealed class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
{
    private readonly IEnumerator<T> _inner;

    public TestAsyncEnumerator(IEnumerator<T> inner)
    {
        _inner = inner;
    }

    public T Current => _inner.Current;

    public ValueTask DisposeAsync()
    {
        _inner.Dispose();
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> MoveNextAsync()
    {
        return ValueTask.FromResult(_inner.MoveNext());
    }
}
