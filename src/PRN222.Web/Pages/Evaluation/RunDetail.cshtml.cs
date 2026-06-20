using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services.Interfaces;
using PRN222.Web.Infrastructure;
using PRN222.Web.Models.Evaluation;

namespace PRN222.Web.Pages.Evaluation;

[Authorize(Roles = ApplicationRoles.ModelOperations)]
public class RunDetailModel(
    IBenchmarkService benchmarkService,
    ICourseAccessService courseAccessService) : PageModel
{
    public BenchmarkRunDetailViewModel Detail { get; private set; } = null!;
    public IReadOnlyDictionary<string, string?> RouteValues { get; private set; } = new Dictionary<string, string?>();

    public async Task<IActionResult> OnGetAsync(int runId, int page = 1, int pageSize = 25)
    {
        var summary = await benchmarkService.GetRunSummaryAsync(runId);
        if (summary is null)
        {
            return NotFound();
        }

        if (!await CanAccessCourseAsync(summary.Run.CourseId))
        {
            return Forbid();
        }

        var pagedResults = PagedResult<BenchmarkResultDto>.Create(
            summary.Results.OrderBy(r => r.Id),
            page,
            pageSize);

        RouteValues = new Dictionary<string, string?>
        {
            ["runId"] = runId.ToString(),
            ["pageSize"] = pagedResults.PageSize.ToString()
        };

        Detail = new BenchmarkRunDetailViewModel
        {
            Summary = summary,
            Results = pagedResults
        };

        return Page();
    }

    public async Task<IActionResult> OnGetExportCsvAsync(int runId)
    {
        var summary = await benchmarkService.GetRunSummaryAsync(runId);
        if (summary is null)
        {
            return NotFound();
        }

        if (!await CanAccessCourseAsync(summary.Run.CourseId))
        {
            return Forbid();
        }

        var sb = new StringBuilder();
        sb.AppendLine("Question,Ground Truth,Generated Answer,Faithfulness,Answer Relevancy,Context Precision,Context Recall");

        foreach (var r in summary.Results)
        {
            sb.AppendLine($"\"{Escape(r.Question)}\",\"{Escape(r.GroundTruth)}\",\"{Escape(r.GeneratedAnswer)}\",{r.Faithfulness:F4},{r.AnswerRelevancy:F4},{r.ContextPrecision:F4},{r.ContextRecall:F4}");
        }

        var fileName = $"benchmark_{summary.Run.Name.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.csv";
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", fileName);
    }

    private static string Escape(string value) =>
        value.Replace("\"", "\"\"").Replace("\n", " ").Replace("\r", "");

    private bool IsCourseScopedUser() =>
        User?.Identity?.IsAuthenticated == true &&
        !User.IsInRole(ApplicationRoles.Admin) &&
        (User.IsInRole(ApplicationRoles.HeadLecturer) || User.IsInRole(ApplicationRoles.Lecturer));

    private async Task<bool> CanAccessCourseAsync(int? courseId)
    {
        if (!IsCourseScopedUser())
        {
            return true;
        }

        if (!courseId.HasValue)
        {
            return false;
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        return await courseAccessService.CanStaffAccessCourseAsync(userId, courseId.Value);
    }
}
