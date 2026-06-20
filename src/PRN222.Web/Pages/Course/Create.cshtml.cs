using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services.Interfaces;
using PRN222.Web.Hubs;
using PRN222.Web.Infrastructure;
using PRN222.Web.Models.Course;

namespace PRN222.Web.Pages.Course;

[Authorize(Roles = ApplicationRoles.Admin)]
public class CreateModel(
    ICourseService courseService,
    IDepartmentService departmentService,
    ICourseAssignmentService courseAssignmentService,
    IHubContext<AdminHub> hubContext,
    ILogger<CreateModel> logger) : PageModel
{
    [BindProperty]
    public CourseFormViewModel Input { get; set; } = new();

    public async Task OnGetAsync()
    {
        Input = await BuildCourseFormAsync(new CourseFormViewModel());
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Input.Name))
        {
            ModelState.AddModelError(nameof(Input.Name), "Vui long nhap ten mon hoc.");
        }

        if (!await IsValidDepartmentAsync(Input.DepartmentId))
        {
            ModelState.AddModelError(nameof(Input.DepartmentId), "Vui long chon Khoa phu trach hop le.");
        }

        if (!ModelState.IsValid)
        {
            Input = await BuildCourseFormAsync(Input);
            return Page();
        }

        try
        {
            var course = await courseService.CreateCourseAsync(
                Input.Name.Trim(),
                Input.Description?.Trim(),
                Input.DepartmentId!.Value);

            await NotifyCourseAudienceAsync(AdminHub.SubjectCreated, course);
            TempData["SuccessMessage"] = "Them mon hoc thanh cong.";
            return Redirect("/Course");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create course.");
            ModelState.AddModelError(string.Empty, "Khong the tao mon hoc luc nay. Vui long thu lai sau.");
            Input = await BuildCourseFormAsync(Input);
            return Page();
        }
    }

    private async Task<bool> IsValidDepartmentAsync(int? departmentId) =>
        departmentId is > 0 && await departmentService.GetByIdAsync(departmentId.Value) is not null;

    private async Task<CourseFormViewModel> BuildCourseFormAsync(CourseFormViewModel form) =>
        new()
        {
            Id = form.Id,
            Name = form.Name,
            Description = form.Description,
            DepartmentId = form.DepartmentId,
            DocumentCount = form.DocumentCount,
            Departments = (await departmentService.GetAllAsync())
                .OrderBy(department => department.Name)
                .ToList()
        };

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
}
