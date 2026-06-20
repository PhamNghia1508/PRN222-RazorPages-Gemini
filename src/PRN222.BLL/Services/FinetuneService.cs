using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services.Interfaces;
using PRN222.DAL.Entities;
using PRN222.DAL.Repositories.Interfaces;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace PRN222.BLL.Services;

public class FinetuneService : IFinetuneService
{
    private readonly IQAPairRepository _qaPairRepository;
    private readonly IChunkRepository _chunkRepository;
    private readonly ILlmService _llmService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FinetuneService> _logger;
    private readonly int _chunkDelayMs;
    private readonly int _retryDelayMs;
    private readonly int _maxRetries;
    private const int MaxQAPairsPerChunk = 5;

    public FinetuneService(
        IQAPairRepository qaPairRepository,
        IChunkRepository chunkRepository,
        ILlmService llmService,
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        ILogger<FinetuneService> logger)
    {
        _qaPairRepository = qaPairRepository;
        _chunkRepository = chunkRepository;
        _llmService = llmService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _chunkDelayMs = GetIntSetting(configuration, "Gemini:RateLimit:ChunkDelayMs", 4000);
        _retryDelayMs = GetIntSetting(configuration, "Gemini:RateLimit:RetryDelayMs", 60000);
        _maxRetries = GetIntSetting(configuration, "Gemini:RateLimit:MaxRetries", 3);

        if (_chunkDelayMs < 0)
            _chunkDelayMs = 0;
        if (_retryDelayMs < 0)
            _retryDelayMs = 0;
        if (_maxRetries < 1)
            _maxRetries = 1;
    }

    public async Task<IEnumerable<QAPairDto>> GetQAPairsByCourseAsync(int courseId)
    {
        var entities = await _qaPairRepository.GetQueryable()
            .Where(q => q.CourseId == courseId && q.DocumentChunkId != null)
            .ToListAsync();
        return entities.Select(q => new QAPairDto
        {
            Id = q.Id,
            CourseId = q.CourseId,
            DocumentChunkId = q.DocumentChunkId,
            Question = q.Question,
            Answer = q.Answer,
            RelevanceScore = q.RelevanceScore,
            CreatedAt = q.CreatedAt
        }).OrderByDescending(q => q.CreatedAt);
    }

    public async Task<int> GenerateDatasetForCourseAsync(int courseId, int targetCount = 50)
    {
        if (targetCount < 1)
            throw new ArgumentOutOfRangeException(nameof(targetCount), "Target count must be at least 1.");

        // 1. Get all chunks for the course
        var allChunks = await _chunkRepository.GetQueryable()
            .Where(c => c.Document.CourseId == courseId)
            .OrderBy(c => c.DocumentId)
            .ThenBy(c => c.ChunkIndex)
            .ToListAsync();
        
        // 2. Get chunks that already have QA pairs
        var existingQAs = await _qaPairRepository.GetQueryable()
            .Where(q => q.CourseId == courseId && q.DocumentChunkId != null)
            .ToListAsync();

        var existingCount = existingQAs.Count;
        var remainingTarget = targetCount - existingCount;
        if (remainingTarget <= 0)
            return 0;

        var processedChunkIds = existingQAs
            .Where(q => q.DocumentChunkId.HasValue)
            .Select(q => q.DocumentChunkId!.Value)
            .ToHashSet();
        var existingQuestions = existingQAs
            .Select(q => NormalizeQuestion(q.Question))
            .Where(q => !string.IsNullOrWhiteSpace(q))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        
        // 3. Prefer new chunks, then reuse older chunks if the dataset is still below target.
        var chunksToProcess = allChunks
            .Where(c => !processedChunkIds.Contains(c.Id))
            .Concat(allChunks.Where(c => processedChunkIds.Contains(c.Id)))
            .ToList();
        
        int generatedCount = 0;

        for (var chunkIndex = 0; chunkIndex < chunksToProcess.Count && generatedCount < remainingTarget; chunkIndex++)
        {
            var chunk = chunksToProcess[chunkIndex];
            if (string.IsNullOrWhiteSpace(chunk.Content) || chunk.Content.Length < 100)
                continue; // Skip too short chunks

            if (chunkIndex > 0 && _chunkDelayMs > 0)
                await Task.Delay(_chunkDelayMs);

            var remainingChunks = Math.Max(1, chunksToProcess.Count - chunkIndex);
            var needed = remainingTarget - generatedCount;
            var qaCountForChunk = Math.Clamp(
                (int)Math.Ceiling((double)needed / remainingChunks),
                1,
                MaxQAPairsPerChunk);

            int retryCount = 0;
            bool success = false;

            while (retryCount < _maxRetries && !success)
            {
                try
                {
                    var jsonResponse = await _llmService.GenerateQAPairsAsync(chunk.Content, qaCountForChunk);
                    var qaList = ParseQAPairs(jsonResponse);

                    var addedForChunk = 0;
                    foreach (var qa in qaList)
                    {
                        if (generatedCount >= remainingTarget)
                            break;

                        if (string.IsNullOrWhiteSpace(qa.Question) || string.IsNullOrWhiteSpace(qa.Answer))
                            continue;

                        var normalizedQuestion = NormalizeQuestion(qa.Question);
                        if (!existingQuestions.Add(normalizedQuestion))
                            continue;

                        await _qaPairRepository.AddAsync(new QAPair
                        {
                            CourseId = courseId,
                            DocumentChunkId = chunk.Id,
                            Question = qa.Question.Trim(),
                            Answer = qa.Answer.Trim(),
                            RelevanceScore = 1.0,
                            CreatedAt = DateTime.UtcNow
                        });

                        generatedCount++;
                        addedForChunk++;
                    }

                    if (addedForChunk > 0)
                        await _unitOfWork.SaveChangesAsync();
                    else
                        _logger.LogWarning("No valid QA pairs generated for chunk {ChunkId}. Raw response: {Response}", chunk.Id, TrimForLog(jsonResponse));

                    success = true;
                }
                catch (HttpRequestException ex) when (ex.Message.Contains("429"))
                {
                    retryCount++;
                    _logger.LogWarning("Rate limit hit chunk {ChunkId}, retry {Retry}/{MaxRetries}. Waiting {Delay}ms...", chunk.Id, retryCount, _maxRetries, _retryDelayMs);
                    if (_retryDelayMs > 0)
                        await Task.Delay(_retryDelayMs);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating QA for chunk {ChunkId}", chunk.Id);
                    break;
                }
            }
        }

        return generatedCount;
    }

    public async Task<string> ExportDatasetToJsonlAsync(int courseId, string formatType = "openai")
    {
        var qas = await _qaPairRepository.GetQueryable()
            .Where(q => q.CourseId == courseId && q.DocumentChunkId != null)
            .ToListAsync();
        var sb = new StringBuilder();
        
        foreach (var qa in qas)
        {
            if (formatType.ToLower() == "openai")
            {
                // Format: {"messages": [{"role": "system", "content": "You are a helpful assistant."}, {"role": "user", "content": "..."}, {"role": "assistant", "content": "..."}]}
                var entry = new
                {
                    messages = new[]
                    {
                        new { role = "system", content = "Bạn là một trợ lý ảo học thuật thông minh, giúp sinh viên giải đáp thắc mắc về môn học một cách ngắn gọn và chính xác." },
                        new { role = "user", content = qa.Question },
                        new { role = "assistant", content = qa.Answer }
                    }
                };
                sb.AppendLine(JsonSerializer.Serialize(entry));
            }
            else
            {
                // Format Gemini text (legacy/simple)
                var entry = new
                {
                    contents = new[]
                    {
                        new { role = "user", parts = new[] { new { text = qa.Question } } },
                        new { role = "model", parts = new[] { new { text = qa.Answer } } }
                    }
                };
                sb.AppendLine(JsonSerializer.Serialize(entry));
            }
        }
        
        return sb.ToString();
    }
    
    private class TempQA
    {
        public string? Question { get; set; }
        public string? Answer { get; set; }
    }

    private static IReadOnlyList<TempQA> ParseQAPairs(string? rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
            return Array.Empty<TempQA>();

        var json = ExtractJsonArray(rawResponse);
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<TempQA>();

        try
        {
            return JsonSerializer.Deserialize<List<TempQA>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<TempQA>();
        }
        catch (JsonException)
        {
            return Array.Empty<TempQA>();
        }
    }

    private static string? ExtractJsonArray(string rawResponse)
    {
        var cleaned = rawResponse
            .Replace("```json", "", StringComparison.OrdinalIgnoreCase)
            .Replace("```", "")
            .Trim();

        if (cleaned.StartsWith("Error:", StringComparison.OrdinalIgnoreCase))
            return null;

        var startIndex = cleaned.IndexOf('[');
        var endIndex = cleaned.LastIndexOf(']');
        if (startIndex >= 0 && endIndex > startIndex)
            return cleaned.Substring(startIndex, endIndex - startIndex + 1);

        startIndex = cleaned.IndexOf('{');
        endIndex = cleaned.LastIndexOf('}');
        if (startIndex >= 0 && endIndex > startIndex)
            return "[" + cleaned.Substring(startIndex, endIndex - startIndex + 1) + "]";

        return null;
    }

    private static string NormalizeQuestion(string? question)
    {
        return string.Join(' ', (question ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string TrimForLog(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Length <= 300 ? value : value[..300] + "...";
    }

    private static int GetIntSetting(IConfiguration configuration, string key, int defaultValue)
    {
        var value = configuration[key];
        return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }
}
