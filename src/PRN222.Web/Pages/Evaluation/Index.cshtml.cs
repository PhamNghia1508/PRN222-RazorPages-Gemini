using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services.Interfaces;
using PRN222.DAL.Entities;
using PRN222.DAL.Repositories.Interfaces;
using PRN222.Web.Infrastructure;

namespace PRN222.Web.Pages.Evaluation;

[Authorize(Roles = ApplicationRoles.ModelOperations)]
public class IndexModel(
    IBenchmarkService benchmarkService,
    ICourseService courseService,
    ICourseAccessService courseAccessService,
    IRepository<EmbeddingModel> embeddingModelRepository,
    ILogger<IndexModel> logger) : PageModel
{
    public PagedResult<BenchmarkRunDto> Runs { get; private set; } = PagedResult<BenchmarkRunDto>.Create([], 1, 25);
    public BenchmarkSummaryDto? LatestSummary { get; private set; }
    public IReadOnlyList<CourseDto> Courses { get; private set; } = [];
    public IReadOnlyList<EmbeddingModel> EmbeddingModels { get; private set; } = [];
    public string? Search { get; private set; }
    public string? SelectedStatus { get; private set; }
    public string? SelectedExperimentType { get; private set; }
    public int? SelectedEmbeddingModelId { get; private set; }
    public IReadOnlyDictionary<string, string?> RouteValues { get; private set; } = new Dictionary<string, string?>();
    public bool HasActiveFilters { get; private set; }

    public async Task OnGetAsync(string? search, string? status, string? experimentType, int? embeddingModelId, int page = 1, int pageSize = 25)
    {
        var runs = (await benchmarkService.GetAllRunsAsync()).ToList();
        var visibleCourses = (await GetVisibleCoursesAsync()).ToList();
        var visibleCourseIds = visibleCourses.Select(course => course.Id).ToHashSet();
        if (IsCourseScopedUser())
        {
            runs = runs
                .Where(run => run.CourseId.HasValue && visibleCourseIds.Contains(run.CourseId.Value))
                .ToList();
        }

        var lastCompleted = runs.FirstOrDefault(r => r.Status == "Completed");
        if (lastCompleted is not null)
        {
            LatestSummary = await benchmarkService.GetRunSummaryAsync(lastCompleted.Id);
        }

        IEnumerable<BenchmarkRunDto> filteredRuns = runs;
        if (!string.IsNullOrWhiteSpace(search))
        {
            filteredRuns = filteredRuns.Where(r => r.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filteredRuns = filteredRuns.Where(r => r.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(experimentType))
        {
            filteredRuns = filteredRuns.Where(r => r.ExperimentType.Equals(experimentType, StringComparison.OrdinalIgnoreCase));
        }

        if (embeddingModelId.HasValue)
        {
            filteredRuns = filteredRuns.Where(r => r.EmbeddingModelId == embeddingModelId.Value);
        }

        var orderedRuns = filteredRuns
            .OrderByDescending(r => r.StartedAt)
            .ThenByDescending(r => r.Id);

        Runs = PagedResult<BenchmarkRunDto>.Create(orderedRuns, page, pageSize);
        Courses = visibleCourses;
        EmbeddingModels = await embeddingModelRepository.GetQueryable()
            .Where(m => m.IsActive)
            .OrderBy(m => m.Provider)
            .ThenBy(m => m.Name)
            .ToListAsync();
        Search = search;
        SelectedStatus = status;
        SelectedExperimentType = experimentType;
        SelectedEmbeddingModelId = embeddingModelId;
        RouteValues = new Dictionary<string, string?>
        {
            ["search"] = search,
            ["status"] = status,
            ["experimentType"] = experimentType,
            ["embeddingModelId"] = embeddingModelId?.ToString(),
            ["pageSize"] = Runs.PageSize.ToString()
        };
        HasActiveFilters = !string.IsNullOrWhiteSpace(search) ||
            !string.IsNullOrWhiteSpace(status) ||
            !string.IsNullOrWhiteSpace(experimentType) ||
            embeddingModelId.HasValue;
    }

    public async Task<IActionResult> OnGetRunStatusesAsync()
    {
        var runs = await benchmarkService.GetAllRunsAsync();
        var activeRuns = runs
            .Where(r => r.Status == "Running" || r.Status == "Pending")
            .Select(r => new { id = r.Id, status = r.Status })
            .ToList();
        return new JsonResult(activeRuns);
    }

    public async Task<IActionResult> OnPostCreateRunAsync([FromBody] CreateBenchmarkRunRequest request)
    {
        if (!await CanAccessCourseAsync(request.CourseId))
        {
            return Forbid();
        }

        try
        {
            var dto = new CreateBenchmarkRunDto
            {
                Name = request.Name,
                ExperimentType = request.ExperimentType,
                ChunkingStrategy = request.ChunkingStrategy,
                EmbeddingModelId = request.EmbeddingModelId,
                ChunkSize = request.ChunkSize,
                ChunkOverlap = request.ChunkOverlap,
                CourseId = request.CourseId
            };

            int runId = await benchmarkService.CreateAndRunBenchmarkAsync(dto, request.CourseId);
            var summary = await benchmarkService.GetRunSummaryAsync(runId);

            return new JsonResult(new
            {
                success = true,
                runId,
                message = $"Benchmark '{request.Name}' da hoan thanh. Run ID: {runId}.",
                avgFaithfulness = summary?.AvgFaithfulness ?? 0,
                avgAnswerRelevancy = summary?.AvgAnswerRelevancy ?? 0,
                avgContextPrecision = summary?.AvgContextPrecision ?? 0,
                avgContextRecall = summary?.AvgContextRecall ?? 0
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create benchmark run for course {CourseId}.", request.CourseId);
            return new JsonResult(new { success = false, message = "Khong the chay benchmark luc nay. Vui long thu lai sau." });
        }
    }

    private bool IsCourseScopedUser() =>
        User?.Identity?.IsAuthenticated == true &&
        !User.IsInRole(ApplicationRoles.Admin) &&
        (User.IsInRole(ApplicationRoles.HeadLecturer) || User.IsInRole(ApplicationRoles.Lecturer));

    private async Task<IEnumerable<CourseDto>> GetVisibleCoursesAsync()
    {
        var courses = (await courseService.GetAllCoursesAsync()).ToList();
        if (!IsCourseScopedUser())
        {
            return courses;
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return [];
        }

        var assignedCourseIds = await courseAccessService.GetAccessibleStaffCourseIdsAsync(userId);
        return courses.Where(course => assignedCourseIds.Contains(course.Id)).ToList();
    }

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

public sealed class CreateBenchmarkRunRequest
{
    public string Name { get; set; } = "";
    public string ExperimentType { get; set; } = "RAG";
    public string ChunkingStrategy { get; set; } = "FixedSize";
    public int EmbeddingModelId { get; set; }
    public int ChunkSize { get; set; } = 512;
    public int ChunkOverlap { get; set; } = 50;
    public int CourseId { get; set; }
}
