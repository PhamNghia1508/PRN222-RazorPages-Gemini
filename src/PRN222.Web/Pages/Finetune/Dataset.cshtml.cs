using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services.Interfaces;
using PRN222.Web.Infrastructure;

namespace PRN222.Web.Pages.Finetune;

[Authorize(Roles = ApplicationRoles.ModelOperations)]
public class DatasetModel(
    ICourseService courseService,
    ICourseAccessService courseAccessService,
    IFinetuneService finetuneService,
    ILogger<DatasetModel> logger) : PageModel
{
    public CourseDto Course { get; private set; } = null!;
    public IReadOnlyList<QAPairDto> QAPairs { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int courseId)
    {
        var course = await courseService.GetCourseByIdAsync(courseId);
        if (course == null)
        {
            return NotFound();
        }

        if (!await CanAccessCourseAsync(courseId))
        {
            return Forbid();
        }

        Course = course;
        QAPairs = (await finetuneService.GetQAPairsByCourseAsync(courseId)).ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostGenerateAjaxAsync(int courseId, int targetCount = 50)
    {
        if (!await CanAccessCourseAsync(courseId))
        {
            return Forbid();
        }

        try
        {
            var newCount = await finetuneService.GenerateDatasetForCourseAsync(courseId, targetCount);
            return new JsonResult(new
            {
                success = true,
                count = newCount,
                message = $"Da tao thanh cong {newCount} cap Q&A moi."
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate fine-tuning dataset for course {CourseId}.", courseId);
            return new JsonResult(new { success = false, message = "Khong the sinh dataset luc nay. Vui long thu lai sau." });
        }
    }

    public async Task<IActionResult> OnGetExportAsync(int courseId)
    {
        var course = await courseService.GetCourseByIdAsync(courseId);
        if (course == null)
        {
            return NotFound();
        }

        if (!await CanAccessCourseAsync(courseId))
        {
            return Forbid();
        }

        var jsonlContent = await finetuneService.ExportDatasetToJsonlAsync(courseId, "openai");
        if (string.IsNullOrWhiteSpace(jsonlContent))
        {
            TempData["ErrorMessage"] = "Khong co du lieu de xuat.";
            return Redirect($"/Finetune/Dataset?courseId={courseId}");
        }

        var fileName = $"dataset_{course.Name.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.jsonl";
        return File(Encoding.UTF8.GetBytes(jsonlContent), "application/jsonl", fileName);
    }

    private bool IsCourseScopedUser() =>
        User?.Identity?.IsAuthenticated == true &&
        !User.IsInRole(ApplicationRoles.Admin) &&
        (User.IsInRole(ApplicationRoles.HeadLecturer) || User.IsInRole(ApplicationRoles.Lecturer));

    private async Task<bool> CanAccessCourseAsync(int courseId)
    {
        if (!IsCourseScopedUser())
        {
            return true;
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        return await courseAccessService.CanStaffAccessCourseAsync(userId, courseId);
    }
}
