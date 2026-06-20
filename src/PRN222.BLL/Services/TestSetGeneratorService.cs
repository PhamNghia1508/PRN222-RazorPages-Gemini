using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services.Interfaces;
using PRN222.DAL.Entities;
using PRN222.DAL.Entities.Enums;
using PRN222.DAL.Repositories.Interfaces;

namespace PRN222.BLL.Services;

public class TestSetGeneratorService : ITestSetGeneratorService
{
    private const int MinChunkContentLength = 150;

    private readonly IQAPairRepository _qaPairRepository;
    private readonly IDocumentRepository _documentRepository;
    private readonly IChunkRepository _chunkRepository;
    private readonly ILlmService _llmService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TestSetGeneratorService> _logger;
    private readonly TimeSpan _chunkDelay;
    private readonly TimeSpan _rateLimitRetryDelay;

    public TestSetGeneratorService(
        IQAPairRepository qaPairRepository,
        IDocumentRepository documentRepository,
        IChunkRepository chunkRepository,
        ILlmService llmService,
        IUnitOfWork unitOfWork,
        ILogger<TestSetGeneratorService> logger,
        IConfiguration configuration)
    {
        _qaPairRepository = qaPairRepository;
        _documentRepository = documentRepository;
        _chunkRepository = chunkRepository;
        _llmService = llmService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _chunkDelay = TimeSpan.FromMilliseconds(GetNonNegativeIntSetting(configuration, "Gemini:RateLimit:ChunkDelayMs", 10000));
        _rateLimitRetryDelay = TimeSpan.FromMilliseconds(GetNonNegativeIntSetting(configuration, "Gemini:RateLimit:RetryDelayMs", 120000));
    }

    public async Task<int> GenerateFromDocumentsAsync(int courseId, int questionsPerChunk = 2, int maxChunks = 25)
    {
        return await GenerateFromDocumentsAsync(
            courseId,
            questionsPerChunk,
            maxChunks,
            CancellationToken.None);
    }

    public async Task<int> GenerateFromDocumentsAsync(
        int courseId,
        int questionsPerChunk,
        int maxChunks,
        CancellationToken cancellationToken,
        ITestSetGenerationJobManager? jobManager = null)
    {
        if (courseId <= 0) throw new ArgumentOutOfRangeException(nameof(courseId));
        if (questionsPerChunk <= 0) throw new ArgumentOutOfRangeException(nameof(questionsPerChunk));
        if (maxChunks <= 0) return 0;

        var documents = await _documentRepository.GetByCourseIdAsync(courseId);
        var indexedDocIds = documents
            .Where(d => d.Status == DocumentStatus.Indexed)
            .Select(d => d.Id)
            .ToList();

        if (indexedDocIds.Count == 0)
        {
            _logger.LogWarning("No indexed documents found for course {CourseId}.", courseId);
            jobManager?.ReportStarted(courseId, 0);
            jobManager?.Complete(courseId, 0, 0);
            return 0;
        }

        var candidateChunks = (await _chunkRepository.GetChunksWithEmbeddingsByDocumentIdsAsync(indexedDocIds))
            .Where(c => !string.IsNullOrWhiteSpace(c.Content) && c.Content.Length >= MinChunkContentLength)
            .OrderBy(_ => Guid.NewGuid())
            .Take(maxChunks)
            .ToList();

        var createdCount = 0;
        var processedChunks = 0;
        jobManager?.ReportStarted(courseId, candidateChunks.Count);

        for (var i = 0; i < candidateChunks.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Test set generation stopped for course {CourseId} after {Processed}/{Total} chunks.",
                    courseId, processedChunks, candidateChunks.Count);
                break;
            }

            var chunk = candidateChunks[i];

            try
            {
                var qaJson = await GenerateQAPairsWithRetryAsync(chunk.Content, questionsPerChunk, chunk.Id);
                var qaPairs = ParseQaPairs(qaJson);

                foreach (var qa in qaPairs)
                {
                    if (string.IsNullOrWhiteSpace(qa.Question) || string.IsNullOrWhiteSpace(qa.Answer))
                    {
                        continue;
                    }

                    await _qaPairRepository.AddAsync(new QAPair
                    {
                        CourseId = courseId,
                        DocumentChunkId = chunk.Id,
                        Question = qa.Question.Trim(),
                        Answer = qa.Answer.Trim(),
                        RelevanceScore = 1.0,
                        CreatedAt = DateTime.UtcNow
                    });

                    createdCount++;
                }
            }
            catch (Exception ex)
            {
                jobManager?.ReportError(courseId, ex.Message);
                _logger.LogError(ex, "Failed to generate QA pairs for chunk {ChunkId}. Continuing batch.", chunk.Id);
            }

            processedChunks++;
            jobManager?.ReportProgress(
                courseId,
                processedChunks,
                createdCount,
                $"Processed {processedChunks}/{candidateChunks.Count} chunks. Created {createdCount} Q&A pairs.");

            await _unitOfWork.SaveChangesAsync();

            if (i < candidateChunks.Count - 1)
            {
                try
                {
                    await Task.Delay(_chunkDelay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        jobManager?.Complete(courseId, processedChunks, createdCount);
        return createdCount;
    }

    public async Task ClearAutoGeneratedAsync(int courseId)
    {
        var autoGenerated = await _qaPairRepository.GetQueryable()
            .Where(q => q.CourseId == courseId && q.DocumentChunkId != null)
            .ToListAsync();

        foreach (var qa in autoGenerated)
        {
            _qaPairRepository.Delete(qa);
        }

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<TestSetStatsDto> GetStatsAsync(int courseId)
    {
        var qaPairs = await _qaPairRepository.GetQueryable()
            .Where(q => q.CourseId == courseId)
            .ToListAsync();

        var documents = await _documentRepository.GetByCourseIdAsync(courseId);
        var indexedDocIds = documents
            .Where(d => d.Status == DocumentStatus.Indexed)
            .Select(d => d.Id)
            .ToList();

        var totalChunksAvailable = 0;
        if (indexedDocIds.Count > 0)
        {
            totalChunksAvailable = (await _chunkRepository.GetChunksWithEmbeddingsByDocumentIdsAsync(indexedDocIds))
                .Count(c => !string.IsNullOrWhiteSpace(c.Content) && c.Content.Length >= MinChunkContentLength);
        }

        var manualCount = qaPairs.Count(q => q.DocumentChunkId == null);
        var autoGeneratedCount = qaPairs.Count(q => q.DocumentChunkId != null);

        return new TestSetStatsDto
        {
            ManualCount = manualCount,
            AutoGeneratedCount = autoGeneratedCount,
            TotalCount = qaPairs.Count,
            UniqueChunksUsed = qaPairs
                .Where(q => q.DocumentChunkId != null)
                .Select(q => q.DocumentChunkId!.Value)
                .Distinct()
                .Count(),
            TotalChunksAvailable = totalChunksAvailable
        };
    }

    public async Task<PagedResult<QAPairDto>> GetAutoGeneratedPreviewAsync(int courseId, int page = 1, int pageSize = 25)
    {
        var normalizedPageSize = PagedResult<QAPairDto>.NormalizePageSize(pageSize);
        var query = _qaPairRepository.GetQueryable()
            .Where(q => q.CourseId == courseId && q.DocumentChunkId != null)
            .OrderByDescending(q => q.CreatedAt)
            .ThenByDescending(q => q.Id);

        var totalItems = await query.CountAsync();
        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)normalizedPageSize);
        var normalizedPage = page < 1 ? 1 : page;
        if (totalPages > 0 && normalizedPage > totalPages)
        {
            normalizedPage = totalPages;
        }

        var items = await query
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(q => new QAPairDto
            {
                Id = q.Id,
                CourseId = q.CourseId,
                DocumentChunkId = q.DocumentChunkId,
                Question = q.Question,
                Answer = q.Answer,
                RelevanceScore = q.RelevanceScore,
                CreatedAt = q.CreatedAt
            })
            .ToListAsync();

        return PagedResult<QAPairDto>.CreateFromPage(items, normalizedPage, normalizedPageSize, totalItems);
    }

    private async Task<string> GenerateQAPairsWithRetryAsync(string chunkContent, int questionsPerChunk, int chunkId)
    {
        try
        {
            return await _llmService.GenerateQAPairsAsync(chunkContent, questionsPerChunk);
        }
        catch (Exception ex) when (IsRateLimitException(ex))
        {
            _logger.LogWarning(ex, "Rate limited while generating QA pairs for chunk {ChunkId}. Retrying once after delay.", chunkId);
            await Task.Delay(_rateLimitRetryDelay);
            return await _llmService.GenerateQAPairsAsync(chunkContent, questionsPerChunk);
        }
    }

    private static int GetNonNegativeIntSetting(IConfiguration configuration, string key, int defaultValue)
    {
        var value = configuration[key];
        return int.TryParse(value, out var parsed) && parsed >= 0
            ? parsed
            : defaultValue;
    }

    private static bool IsRateLimitException(Exception ex)
    {
        return ex.Message.Contains("429", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("too many requests", StringComparison.OrdinalIgnoreCase);
    }

    private static List<GeneratedQaPair> ParseQaPairs(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return new List<GeneratedQaPair>();
        }

        var json = ExtractJsonArray(rawJson);
        return JsonSerializer.Deserialize<List<GeneratedQaPair>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<GeneratedQaPair>();
    }

    private static string ExtractJsonArray(string value)
    {
        var trimmed = value.Trim();
        var start = trimmed.IndexOf('[');
        var end = trimmed.LastIndexOf(']');

        return start >= 0 && end >= start
            ? trimmed[start..(end + 1)]
            : trimmed;
    }

    private sealed class GeneratedQaPair
    {
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
    }
}
