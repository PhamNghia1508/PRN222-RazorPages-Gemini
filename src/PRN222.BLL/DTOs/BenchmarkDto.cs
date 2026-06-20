namespace PRN222.BLL.DTOs;

/// <summary>
/// Represents a single benchmark run for display in the UI.
/// </summary>
public class BenchmarkRunDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ExperimentType { get; set; } = "RAG";
    public string ChunkingStrategy { get; set; } = string.Empty;
    public string EmbeddingModelName { get; set; } = string.Empty;
    public int EmbeddingModelId { get; set; }
    public int? CourseId { get; set; }
    public int ChunkSize { get; set; }
    public int ChunkOverlap { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalQuestions { get; set; }
}

/// <summary>
/// Represents a single Q&A evaluation result within a benchmark run.
/// </summary>
public class BenchmarkResultDto
{
    public int Id { get; set; }
    public string Question { get; set; } = string.Empty;
    public string GroundTruth { get; set; } = string.Empty;
    public string GeneratedAnswer { get; set; } = string.Empty;
    public float Faithfulness { get; set; }
    public float AnswerRelevancy { get; set; }
    public float ContextPrecision { get; set; }
    public float ContextRecall { get; set; }
}

/// <summary>
/// Aggregated summary of a benchmark run with averaged RAGAS metrics.
/// Used to render the Evaluation Dashboard charts.
/// </summary>
public class BenchmarkSummaryDto
{
    public BenchmarkRunDto Run { get; set; } = null!;
    public List<BenchmarkResultDto> Results { get; set; } = new();

    // Averaged RAGAS metrics
    public float AvgFaithfulness { get; set; }
    public float AvgAnswerRelevancy { get; set; }
    public float AvgContextPrecision { get; set; }
    public float AvgContextRecall { get; set; }

    /// <summary>Harmonic mean of the four averaged metrics.</summary>
    public float OverallScore { get; set; }
}

/// <summary>
/// Input DTO for creating a new benchmark run.
/// </summary>
public class CreateBenchmarkRunDto
{
    public string Name { get; set; } = string.Empty;
    public string ExperimentType { get; set; } = "RAG";
    public string ChunkingStrategy { get; set; } = "FixedSize";
    public int EmbeddingModelId { get; set; }
    public int ChunkSize { get; set; } = 512;
    public int ChunkOverlap { get; set; } = 50;
    public int CourseId { get; set; }
}
