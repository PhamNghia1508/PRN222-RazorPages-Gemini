using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.SignalR;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services.Interfaces;
using PRN222.Web.Hubs;
using PRN222.Web.Infrastructure;

namespace PRN222.Web.Pages.Course;

[Authorize(Roles = ApplicationRoles.Management)]
public class IndexModel(
    ICourseService courseService,
    ICourseAccessService courseAccessService,
    IDepartmentService departmentService,
    ICourseAssignmentService courseAssignmentService,
    IHubContext<AdminHub> hubContext,
    ILogger<IndexModel> logger) : PageModel
{
    public IReadOnlyList<CourseDto> Courses { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Courses = (await GetVisibleCoursesAsync()).ToList();
    }

    public async Task<IActionResult> OnGetAssignedCoursesPartialAsync()
    {
        Response.Headers.CacheControl = "no-store";
        var courses = await GetVisibleCoursesAsync();

        return new PartialViewResult
        {
            ViewName = "/Pages/Course/_CourseGrid.cshtml",
            ViewData = new ViewDataDictionary<IEnumerable<CourseDto>>(ViewData, courses)
        };
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (!User.IsInRole(ApplicationRoles.Admin))
        {
            return Forbid();
        }

        try
        {
            var assignedUserIds = await courseAssignmentService.GetAssignedUserIdsForCourseAsync(id);
            var result = await courseService.DeleteCourseAsync(id);
            if (result)
            {
                await NotifyCourseDeletedAsync(id, assignedUserIds);
            }

            TempData[result ? "SuccessMessage" : "ErrorMessage"] = result
                ? "Xoa mon hoc thanh cong."
                : "Khong tim thay mon hoc de xoa.";
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Course {CourseId} cannot be deleted.", id);
            TempData["ErrorMessage"] = ex.Message;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete course {CourseId}.", id);
            TempData["ErrorMessage"] = "Khong the xoa mon hoc luc nay. Vui long thu lai sau.";
        }

        return Redirect("/Course");
    }

    public async Task<IActionResult> OnPostCreateAjaxAsync([FromBody] CreateCourseRequest request)
    {
        if (!User.IsInRole(ApplicationRoles.Admin))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Name) || !await IsValidDepartmentAsync(request.DepartmentId))
        {
            return BadRequest(new { error = "Vui long nhap ten mon hoc va chon Khoa phu trach hop le." });
        }

        try
        {
            var course = await courseService.CreateCourseAsync(
                request.Name.Trim(),
                request.Description?.Trim(),
                request.DepartmentId!.Value);

            await NotifyCourseAudienceAsync(AdminHub.SubjectCreated, course);
            return new JsonResult(new { id = course.Id, name = course.Name });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create course via AJAX.");
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            return new JsonResult(new { error = "Khong the tao mon hoc luc nay. Vui long thu lai sau." });
        }
    }

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

    private bool IsCourseScopedUser() =>
        User?.Identity?.IsAuthenticated == true &&
        !User.IsInRole(ApplicationRoles.Admin) &&
        (User.IsInRole(ApplicationRoles.HeadLecturer) || User.IsInRole(ApplicationRoles.Lecturer));

    private async Task<bool> IsValidDepartmentAsync(int? departmentId) =>
        departmentId is > 0 && await departmentService.GetByIdAsync(departmentId.Value) is not null;

    private async Task NotifyCourseAudienceAsync(string eventName, CourseDto course)
    {
        var assignedUserIds = await courseAssignmentService.GetAssignedUserIdsForCourseAsync(course.Id);
        var payload = new
        {
            id = course.Id,
            courseId = course.Id,
            name = course.Name,
            description = course.Description,
            documentCount = course.DocumentCount,
            createdAt = course.CreatedAt,
            refreshUrl = Url.Page("/Course/Index", "AssignedCoursesPartial")
        };

        var notifications = new List<Task>
        {
            hubContext.Clients.Group(AdminHub.AdminsGroup).SendAsync(eventName, payload)
        };

        if (assignedUserIds.Count > 0)
        {
            notifications.Add(hubContext.Clients.Users(assignedUserIds).SendAsync(eventName, payload));
        }

        await Task.WhenAll(notifications);
    }

    private async Task NotifyCourseDeletedAsync(int courseId, IReadOnlyList<string> assignedUserIds)
    {
        var payload = new { id = courseId, courseId };
        var notifications = new List<Task>
        {
            hubContext.Clients.Group(AdminHub.AdminsGroup)
                .SendAsync(AdminHub.SubjectDeleted, payload)
        };

        if (assignedUserIds.Count > 0)
        {
            notifications.Add(
                hubContext.Clients.Users(assignedUserIds)
                    .SendAsync(AdminHub.SubjectDeleted, payload));
        }

        await Task.WhenAll(notifications);
    }
}

public sealed class CreateCourseRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? DepartmentId { get; set; }
}
