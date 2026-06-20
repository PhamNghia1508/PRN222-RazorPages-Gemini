using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services.Interfaces;
using PRN222.Web.Infrastructure;

namespace PRN222.Web.Pages.Finetune;

[Authorize(Roles = ApplicationRoles.ModelOperations)]
public class IndexModel(
    ICourseService courseService,
    ICourseAccessService courseAccessService) : PageModel
{
    public IReadOnlyList<CourseDto> Courses { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Courses = (await GetVisibleCoursesAsync()).ToList();
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
}
