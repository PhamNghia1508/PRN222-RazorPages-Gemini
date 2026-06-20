using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services.Interfaces;
using PRN222.BLL.Services.Rag;
using PRN222.DAL.Entities;
using PRN222.DAL.Repositories.Interfaces;

namespace PRN222.BLL.Services;

public class BenchmarkService : IBenchmarkService
{
    private readonly IRepository<BenchmarkRun> _runRepository;
    private readonly IRepository<BenchmarkResult> _resultRepository;
    private readonly IRepository<ChunkEmbedding> _chunkEmbeddingRepository;
    private readonly IQAPairRepository _qaPairRepository;
    private readonly IChunkRepository _chunkRepository;
    private readonly IDocumentRepository _documentRepository;
    private readonly IRepository<EmbeddingModel> _embeddingModelRepository;
    private readonly IEmbeddingService _embeddingService;
    private readonly EmbeddingServiceFactory _embeddingServiceFactory;
    private readonly IRagRetrievalService _ragRetrievalService;
    private readonly ILlmService _llmService;
    private readonly IFineTunedModelService _fineTunedModelService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<BenchmarkService> _logger;

    public BenchmarkService(
        IRepository<BenchmarkRun> runRepository,
        IRepository<BenchmarkResult> resultRepository,
        IRepository<ChunkEmbedding> chunkEmbeddingRepository,
        IQAPairRepository qaPairRepository,
        IChunkRepository chunkRepository,
        IDocumentRepository documentRepository,
        IRepository<EmbeddingModel> embeddingModelRepository,
        IEmbeddingService embeddingService,
        EmbeddingServiceFactory embeddingServiceFactory,
        IRagRetrievalService ragRetrievalService,
        ILlmService llmService,
        IFineTunedModelService fineTunedModelService,
        IUnitOfWork unitOfWork,
        ILogger<BenchmarkService> logger)
    {
        _runRepository = runRepository;
        _resultRepository = resultRepository;
        _chunkEmbeddingRepository = chunkEmbeddingRepository;
        _qaPairRepository = qaPairRepository;
        _chunkRepository = chunkRepository;
        _documentRepository = documentRepository;
        _embeddingModelRepository = embeddingModelRepository;
        _embeddingService = embeddingService;
        _embeddingServiceFactory = embeddingServiceFactory;
        _ragRetrievalService = ragRetrievalService;
        _llmService = llmService;
        _fineTunedModelService = fineTunedModelService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ────────────────────────────────────────────────────────────
    //  GET ALL RUNS
    // ────────────────────────────────────────────────────────────
    public async Task<IEnumerable<BenchmarkRunDto>> GetAllRunsAsync()
    {
        var runs = await _runRepository.GetQueryable()
            .Include(r => r.EmbeddingModel)
            .Include(r => r.Results)
            .OrderByDescending(r => r.StartedAt)
            .ToListAsync();

        return runs.Select(MapRunToDto);
    }

    // ────────────────────────────────────────────────────────────
    //  GET RUN SUMMARY (with aggregated RAGAS)
    // ────────────────────────────────────────────────────────────
    public async Task<BenchmarkSummaryDto?> GetRunSummaryAsync(int runId)
    {
        var run = await _runRepository.GetQueryable()
            .Include(r => r.EmbeddingModel)
            .Include(r => r.Results)
            .FirstOrDefaultAsync(r => r.Id == runId);

        if (run is null) return null;

        var resultDtos = run.Results.Select(r => new BenchmarkResultDto
        {
            Id = r.Id,
            Question = r.Question,
            GroundTruth = r.GroundTruth,
            GeneratedAnswer = r.GeneratedAnswer,
            Faithfulness = r.Faithfulness,
            AnswerRelevancy = r.AnswerRelevancy,
            ContextPrecision = r.ContextPrecision,
            ContextRecall = r.ContextRecall
        }).ToList();

        float avgF = resultDtos.Count > 0 ? resultDtos.Average(r => r.Faithfulness) : 0;
        float avgAR = resultDtos.Count > 0 ? resultDtos.Average(r => r.AnswerRelevancy) : 0;
        float avgCP = resultDtos.Count > 0 ? resultDtos.Average(r => r.ContextPrecision) : 0;
        float avgCR = resultDtos.Count > 0 ? resultDtos.Average(r => r.ContextRecall) : 0;

        return new BenchmarkSummaryDto
        {
            Run = MapRunToDto(run),
            Results = resultDtos,
            AvgFaithfulness = MathF.Round(avgF, 4),
            AvgAnswerRelevancy = MathF.Round(avgAR, 4),
            AvgContextPrecision = MathF.Round(avgCP, 4),
            AvgContextRecall = MathF.Round(avgCR, 4),
            OverallScore = MathF.Round(HarmonicMean(avgF, avgAR, avgCP, avgCR), 4)
        };
    }

    // ────────────────────────────────────────────────────────────
    //  CREATE & RUN BENCHMARK
    // ────────────────────────────────────────────────────────────
    public async Task<int> CreateAndRunBenchmarkAsync(CreateBenchmarkRunDto dto, int courseId)
    {
        // 1. Resolve embedding model name
        var embeddingModel = await _embeddingModelRepository.GetByIdAsync(dto.EmbeddingModelId);
        string embeddingModelName = embeddingModel?.Name ?? _embeddingService.ModelName;
        var selectedEmbeddingService = runUsesRetrieval(dto.ExperimentType)
            ? _embeddingServiceFactory.GetByModelName(embeddingModelName)
            : _embeddingService;

        // 2. Create the run record
        var run = new BenchmarkRun
        {
            Name = dto.Name,
            ExperimentType = NormalizeExperimentType(dto.ExperimentType),
            ChunkingStrategy = dto.ChunkingStrategy,
            EmbeddingModelId = dto.EmbeddingModelId,
            CourseId = courseId,
            ChunkSize = dto.ChunkSize,
            ChunkOverlap = dto.ChunkOverlap,
            Status = "Running",
            StartedAt = DateTime.UtcNow
        };

        await _runRepository.AddAsync(run);
        await _unitOfWork.SaveChangesAsync();

        try
        {
            // 3. Prefer QA pairs generated from uploaded course documents.
            // These are linked to source chunks, so RAG recall is evaluated against the same knowledge base it retrieves from.
            var autoGenerated = await _qaPairRepository.GetQueryable()
                .Where(q => q.CourseId == courseId && q.DocumentChunkId != null)
                .OrderBy(q => Guid.NewGuid())
                .Take(50)
                .ToListAsync();

            var testSet = autoGenerated;

            if (testSet.Count == 0)
            {
                run.Status = "Failed";
                run.CompletedAt = DateTime.UtcNow;
                _runRepository.Update(run);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogWarning(
                    "Benchmark {RunId}: No document-grounded QAPairs found for course {CourseId}. Upload/index documents, then generate the test set from chunks before benchmarking.",
                    run.Id, courseId);
                return run.Id;
            }

            // 4. Load all chunks with embeddings for this course
            List<DocumentChunk> allChunks = new();
            Dictionary<int, float[]> chunkEmbeddings = new();

            if (run.ExperimentType == "RAG")
            {
                var documents = await _documentRepository.GetByCourseIdAsync(courseId);
                var docIds = documents.Select(d => d.Id).ToList();
                allChunks = (await _chunkRepository.GetChunksWithEmbeddingsByDocumentIdsAsync(docIds)).ToList();
                await EnsureChunkEmbeddingsAsync(allChunks, selectedEmbeddingService);
                chunkEmbeddings = BuildEmbeddingLookup(allChunks, embeddingModelName);
            }

            // Pre-parse embeddings for the selected model into a lookup: ChunkId → float[]
            _logger.LogInformation(
                "Benchmark {RunId}: Processing {TestCount} questions as {ExperimentType} (model: {Model}, chunks: {ChunkCount}).",
                run.Id, testSet.Count, run.ExperimentType, embeddingModelName, allChunks.Count);

            // 5. Evaluate each QAPair
            var successCount = 0;
            var failureCount = 0;

            foreach (var qa in testSet)
            {
                try
                {
                    var result = run.ExperimentType == "FineTuned"
                        ? await EvaluateFineTunedQuestionAsync(qa, run.Id)
                        : await EvaluateRagQuestionAsync(qa, allChunks, chunkEmbeddings, run.Id, selectedEmbeddingService);

                    await _resultRepository.AddAsync(result);
                    successCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Benchmark {RunId}: Error evaluating QAPair {QAId}.", run.Id, qa.Id);
                    failureCount++;

                    // Persist a zero-score result so we don't lose track
                    await _resultRepository.AddAsync(new BenchmarkResult
                    {
                        BenchmarkRunId = run.Id,
                        Question = qa.Question,
                        GroundTruth = qa.Answer,
                        GeneratedAnswer = "[ERROR] " + ex.Message,
                        Faithfulness = 0,
                        AnswerRelevancy = 0,
                        ContextPrecision = 0,
                        ContextRecall = 0
                    });

                    if (run.ExperimentType == "FineTuned" && IsFineTunedConfigurationError(ex))
                    {
                        _logger.LogWarning(
                            "Benchmark {RunId}: Stopping FineTuned benchmark early because the provider configuration is invalid: {Message}",
                            run.Id,
                            ex.Message);
                        break;
                    }
                }
            }

            run.Status = successCount == 0 && failureCount > 0
                ? "Failed"
                : failureCount > 0
                    ? "CompletedWithErrors"
                    : "Completed";
            run.CompletedAt = DateTime.UtcNow;
            _runRepository.Update(run);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "Benchmark {RunId}: Finished with status {Status}. Success: {SuccessCount}, failed: {FailureCount}.",
                run.Id, run.Status, successCount, failureCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Benchmark {RunId}: Fatal error during run.", run.Id);
            run.Status = "Failed";
            run.CompletedAt = DateTime.UtcNow;
            _runRepository.Update(run);
            await _unitOfWork.SaveChangesAsync();
        }

        return run.Id;
    }

    // ────────────────────────────────────────────────────────────
    //  PRIVATE: Evaluate a single Q&A pair through the RAG pipeline
    // ────────────────────────────────────────────────────────────
    private async Task<BenchmarkResult> EvaluateRagQuestionAsync(
        QAPair qa,
        List<DocumentChunk> allChunks,
        Dictionary<int, float[]> chunkEmbeddings,
        int runId,
        IEmbeddingService embeddingService)
    {
        // a) Embed the question
        float[] questionEmbedding = await embeddingService.GenerateEmbeddingAsync(qa.Question);

        var topChunks = _ragRetrievalService.RankChunks(
            questionEmbedding,
            allChunks,
            chunkEmbeddings,
            minSimilarity: 0.3f,
            maxResults: 3);

        // For auto-generated QA, qa.DocumentChunkId points to the chunk that produced the question.
        // If retrieval fails to bring that source chunk into top context, low Context Recall is a valid research result.

        string context = _ragRetrievalService.BuildBenchmarkContext(topChunks);
        string prompt = _ragRetrievalService.BuildBenchmarkPrompt(qa.Question, context);

        string generatedAnswer = await _llmService.GenerateAnswerAsync(prompt, context);
        ThrowIfProviderErrorAnswer(generatedAnswer);

        // e) Compute approximate RAGAS metrics
        float faithfulness = ComputeFaithfulness(generatedAnswer, qa.Answer);
        float answerRelevancy = await ComputeAnswerRelevancyAsync(qa.Question, generatedAnswer, embeddingService);
        float contextPrecision = ComputeContextPrecision(topChunks);
        float contextRecall = ComputeContextRecall(context, qa.Answer);

        return new BenchmarkResult
        {
            BenchmarkRunId = runId,
            Question = qa.Question,
            GroundTruth = qa.Answer,
            GeneratedAnswer = generatedAnswer,
            Faithfulness = faithfulness,
            AnswerRelevancy = answerRelevancy,
            ContextPrecision = contextPrecision,
            ContextRecall = contextRecall
        };
    }

    private async Task<BenchmarkResult> EvaluateFineTunedQuestionAsync(QAPair qa, int runId)
    {
        var generatedAnswer = await _fineTunedModelService.GenerateAnswerAsync(qa.Question);
        ThrowIfProviderErrorAnswer(generatedAnswer);

        var faithfulness = ComputeFaithfulness(generatedAnswer, qa.Answer);
        var answerRelevancy = await ComputeAnswerRelevancyAsync(qa.Question, generatedAnswer, _embeddingService);

        return new BenchmarkResult
        {
            BenchmarkRunId = runId,
            Question = qa.Question,
            GroundTruth = qa.Answer,
            GeneratedAnswer = generatedAnswer,
            Faithfulness = faithfulness,
            AnswerRelevancy = answerRelevancy,
            ContextPrecision = 0,
            ContextRecall = 0
        };
    }

    private static bool IsFineTunedConfigurationError(Exception ex)
    {
        var message = ex.Message;

        return message.Contains("Model not supported by provider", StringComparison.OrdinalIgnoreCase)
            || message.Contains("does not have sufficient permissions", StringComparison.OrdinalIgnoreCase)
            || message.Contains("FineTunedModel:ModelId is not configured", StringComparison.OrdinalIgnoreCase);
    }

    // ────────────────────────────────────────────────────────────
    //  RAGAS Metric Approximations
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Faithfulness ≈ keyword overlap between generated answer and ground truth.
    /// Extracts significant keywords (≥ 3 chars) from ground truth and checks coverage.
    /// </summary>
    private static float ComputeFaithfulness(string generatedAnswer, string groundTruth)
    {
        if (string.IsNullOrWhiteSpace(generatedAnswer) || string.IsNullOrWhiteSpace(groundTruth))
            return 0f;

        var groundKeywords = ExtractKeywords(groundTruth);
        if (groundKeywords.Count == 0) return 0f;

        int matchCount = groundKeywords.Count(kw =>
            generatedAnswer.Contains(kw, StringComparison.OrdinalIgnoreCase));

        return MathF.Min(1f, (float)matchCount / groundKeywords.Count);
    }

    /// <summary>
    /// Answer Relevancy ≈ cosine similarity between embedding(question) and embedding(generatedAnswer).
    /// </summary>
    private async Task<float> ComputeAnswerRelevancyAsync(
        string question,
        string generatedAnswer,
        IEmbeddingService embeddingService)
    {
        if (string.IsNullOrWhiteSpace(generatedAnswer)) return 0f;

        try
        {
            float[] qEmb = await embeddingService.GenerateEmbeddingAsync(question);
            float[] aEmb = await embeddingService.GenerateEmbeddingAsync(generatedAnswer);
            float sim = VectorMath.CosineSimilarity(qEmb, aEmb);
            return MathF.Max(0f, MathF.Min(1f, sim));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to compute AnswerRelevancy via embeddings. Returning 0.");
            return 0f;
        }
    }

    /// <summary>
    /// Context Precision = proportion of retrieved chunks with similarity > 0.5.
    /// </summary>
    private static float ComputeContextPrecision(IReadOnlyCollection<RetrievedChunk> topChunks)
    {
        if (topChunks.Count == 0) return 0f;
        int relevant = topChunks.Count(c => c.Similarity > 0.5f);
        return (float)relevant / topChunks.Count;
    }

    /// <summary>
    /// Context Recall ≈ keyword overlap between context and ground truth keywords.
    /// </summary>
    private static float ComputeContextRecall(string context, string groundTruth)
    {
        if (string.IsNullOrWhiteSpace(context) || string.IsNullOrWhiteSpace(groundTruth))
            return 0f;

        var groundKeywords = ExtractKeywords(groundTruth);
        if (groundKeywords.Count == 0) return 0f;

        int matchCount = groundKeywords.Count(kw =>
            context.Contains(kw, StringComparison.OrdinalIgnoreCase));

        return MathF.Min(1f, (float)matchCount / groundKeywords.Count);
    }

    // ────────────────────────────────────────────────────────────
    //  HELPERS
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Pre-parse all chunk embeddings for a specific model into a dictionary
    /// so we avoid repeated JSON deserialization during scoring.
    /// </summary>
    private Dictionary<int, float[]> BuildEmbeddingLookup(
        List<DocumentChunk> chunks, string embeddingModelName)
    {
        var lookup = new Dictionary<int, float[]>();

        foreach (var chunk in chunks)
        {
            var embedding = chunk.Embeddings
                .FirstOrDefault(e => e.EmbeddingModelName == embeddingModelName);

            if (embedding is null || string.IsNullOrWhiteSpace(embedding.EmbeddingVector))
                continue;

            try
            {
                var vector = JsonSerializer.Deserialize<float[]>(embedding.EmbeddingVector);
                if (vector is { Length: > 0 })
                {
                    lookup[chunk.Id] = vector;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize embedding for chunk {ChunkId}.", chunk.Id);
            }
        }

        _logger.LogInformation(
            "Built embedding lookup: {Parsed}/{Total} chunks for model '{Model}'.",
            lookup.Count, chunks.Count, embeddingModelName);

        return lookup;
    }

    private async Task EnsureChunkEmbeddingsAsync(
        List<DocumentChunk> chunks,
        IEmbeddingService embeddingService)
    {
        var missingChunks = chunks
            .Where(chunk => !chunk.Embeddings.Any(e =>
                e.EmbeddingModelName.Equals(embeddingService.ModelName, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (missingChunks.Count == 0)
            return;

        _logger.LogInformation(
            "Backfilling {Count} real chunk embeddings for model '{Model}'.",
            missingChunks.Count, embeddingService.ModelName);

        var created = 0;
        foreach (var chunk in missingChunks)
        {
            var vector = await embeddingService.GenerateEmbeddingAsync(chunk.Content);
            if (!IsUsableVector(vector))
            {
                throw new InvalidOperationException(
                    $"Embedding provider '{embeddingService.ModelName}' returned an empty/zero vector for chunk {chunk.Id}.");
            }

            var chunkEmbedding = new ChunkEmbedding
            {
                ChunkId = chunk.Id,
                EmbeddingModelName = embeddingService.ModelName,
                EmbeddingVector = JsonSerializer.Serialize(vector),
                CreatedAt = DateTime.UtcNow
            };

            await _chunkEmbeddingRepository.AddAsync(chunkEmbedding);
            chunk.Embeddings.Add(chunkEmbedding);
            created++;

            if (created % 10 == 0)
                await _unitOfWork.SaveChangesAsync();
        }

        await _unitOfWork.SaveChangesAsync();
    }

    /// <summary>
    /// Extract keywords (≥ 3 characters) from a text string, de-duplicated and lowercased.
    /// </summary>
    private static List<string> ExtractKeywords(string text)
    {
        var separators = new[] { ' ', '\n', '\r', '\t', ',', '.', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}', '"', '\'' };
        return text
            .Split(separators, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3)
            .Select(w => w.ToLowerInvariant())
            .Distinct()
            .ToList();
    }

    private static bool IsUsableVector(float[] vector) =>
        vector.Length > 0 && vector.Any(v => Math.Abs(v) > 1e-10f);

    /// <summary>
    /// Harmonic mean of four metrics. Returns 0 if any metric is zero.
    /// </summary>
    private static float HarmonicMean(float a, float b, float c, float d)
    {
        if (a <= 0 || b <= 0 || c <= 0 || d <= 0) return 0f;
        return 4f / (1f / a + 1f / b + 1f / c + 1f / d);
    }

    private static void ThrowIfProviderErrorAnswer(string generatedAnswer)
    {
        if (string.IsNullOrWhiteSpace(generatedAnswer))
            throw new InvalidOperationException("Provider returned an empty answer.");

        var errorMarkers = new[]
        {
            "Xin lỗi, đã xảy ra lỗi khi kết nối với AI",
            "Xin lỗi, tôi không thể trả lời câu hỏi này",
            "Error: Call LLM thất bại",
            "[ERROR]"
        };

        if (errorMarkers.Any(marker => generatedAnswer.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException(generatedAnswer);
    }

    private static BenchmarkRunDto MapRunToDto(BenchmarkRun run) => new()
    {
        Id = run.Id,
        Name = run.Name,
        ExperimentType = string.IsNullOrWhiteSpace(run.ExperimentType) ? "RAG" : run.ExperimentType,
        ChunkingStrategy = run.ChunkingStrategy,
        EmbeddingModelName = run.EmbeddingModel?.Name ?? "Unknown",
        EmbeddingModelId = run.EmbeddingModelId,
        CourseId = run.CourseId,
        ChunkSize = run.ChunkSize,
        ChunkOverlap = run.ChunkOverlap,
        Status = run.Status,
        StartedAt = run.StartedAt,
        CompletedAt = run.CompletedAt,
        TotalQuestions = run.Results?.Count ?? 0
    };

    private static string NormalizeExperimentType(string? value)
    {
        return string.Equals(value, "FineTuned", StringComparison.OrdinalIgnoreCase)
            ? "FineTuned"
            : "RAG";
    }

    private static bool runUsesRetrieval(string? experimentType) =>
        !string.Equals(experimentType, "FineTuned", StringComparison.OrdinalIgnoreCase);
}
