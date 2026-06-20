using PRN222.BLL.DTOs;

namespace PRN222.Web.Models.Evaluation;

public sealed class BenchmarkRunDetailViewModel
{
  public required BenchmarkSummaryDto Summary { get; init; }
  public required PagedResult<BenchmarkResultDto> Results { get; init; }
}