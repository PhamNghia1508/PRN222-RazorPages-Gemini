using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services.Interfaces;
using PRN222.Web.Infrastructure;

namespace PRN222.Web.Pages.Evaluation;

[Authorize(Roles = ApplicationRoles.ModelOperations)]
public class CompareModel(
    IBenchmarkService benchmarkService,
    ICourseAccessService courseAccessService) : PageModel
{
    public IReadOnlyList<BenchmarkSummaryDto> Summaries { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync([FromQuery] string runIds)
    {
        if (string.IsNullOrWhiteSpace(runIds))
        {
            return Redirect("/Evaluation");
        }

        var ids = runIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s.Trim(), out var id) ? id : -1)
            .Where(id => id > 0)
            .ToList();

        var summaries = new List<BenchmarkSummaryDto>();
        foreach (var id in ids)
        {
            var summary = await benchmarkService.GetRunSummaryAsync(id);
            if (summary is not null && await CanAccessCourseAsync(summary.Run.CourseId))
            {
                summaries.Add(summary);
            }
        }

        if (summaries.Count == 0)
        {
            return Redirect("/Evaluation");
        }

        Summaries = summaries;
        return Page();
    }

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
