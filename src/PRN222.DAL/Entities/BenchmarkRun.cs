namespace PRN222.DAL.Entities;

/// <summary>
/// Represents a single benchmark run comparing RAG strategies/embedding models.
/// </summary>
public class BenchmarkRun
{
    public int Id { get; set; }

    /// <summary>Name/description of this benchmark run.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Chunking strategy used, e.g. "FixedSize", "Sentence", "Semantic".</summary>
    public string ChunkingStrategy { get; set; } = string.Empty;

    /// <summary>Experiment type, e.g. "RAG" or "FineTuned".</summary>
    public string ExperimentType { get; set; } = "RAG";

    public int EmbeddingModelId { get; set; }

    public int? CourseId { get; set; }

    /// <summary>Chunk size in tokens used for this benchmark.</summary>
    public int ChunkSize { get; set; }

    /// <summary>Overlap in tokens between consecutive chunks.</summary>
    public int ChunkOverlap { get; set; }

    /// <summary>Status of the benchmark run, e.g. "Running", "Completed", "Failed".</summary>
    public string Status { get; set; } = "Pending";

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    // Navigation properties
    public EmbeddingModel EmbeddingModel { get; set; } = null!;
    public Course? Course { get; set; }
    public ICollection<BenchmarkResult> Results { get; set; } = new List<BenchmarkResult>();
}
