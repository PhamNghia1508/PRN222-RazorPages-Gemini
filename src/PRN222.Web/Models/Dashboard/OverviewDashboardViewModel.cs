using PRN222.BLL.DTOs;

namespace PRN222.Web.Models.Dashboard;

public class OverviewDashboardViewModel
{
    public int TotalDocuments { get; init; }
    public int IndexedDocuments { get; init; }
    public int FailedDocuments { get; init; }
    public int IndexedChunks { get; init; }
    public int TotalCourses { get; init; }
    public string EvaluationStatus { get; init; } = "Ready to configure";
    public IReadOnlyList<PipelineStepViewModel> PipelineSteps { get; init; } = Array.Empty<PipelineStepViewModel>();
    public IReadOnlyList<DocumentDto> RecentDocuments { get; init; } = Array.Empty<DocumentDto>();
    public IReadOnlyList<NextActionViewModel> NextActions { get; init; } = Array.Empty<NextActionViewModel>();
}

public record PipelineStepViewModel(
    string Key,
    string Label,
    string Description,
    string Status,
    string IconCssClass);

public record NextActionViewModel(
    string Label,
    string Description,
    string PagePath,
    string IconCssClass,
    string CssClass,
    bool IsEnabled);
