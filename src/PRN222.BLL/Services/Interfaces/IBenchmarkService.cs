using PRN222.BLL.DTOs;

namespace PRN222.BLL.Services.Interfaces;

public interface IBenchmarkService
{
    /// <summary>Get all benchmark runs ordered by most recent first.</summary>
    Task<IEnumerable<BenchmarkRunDto>> GetAllRunsAsync();

    /// <summary>Get a single run with its aggregated RAGAS metrics and individual results.</summary>
    Task<BenchmarkSummaryDto?> GetRunSummaryAsync(int runId);

    /// <summary>
    /// Create a new BenchmarkRun, execute the RAG pipeline for each QAPair in the course,
    /// compute approximate RAGAS metrics, and persist everything to the database.
    /// Returns the Id of the created BenchmarkRun.
    /// </summary>
    Task<int> CreateAndRunBenchmarkAsync(CreateBenchmarkRunDto dto, int courseId);
}
