using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services.Interfaces;
using PRN222.Web.Infrastructure;

namespace PRN222.Web.Pages.Document;

[Authorize(Roles = ApplicationRoles.DocumentUpload)]
[RequestSizeLimit(50 * 1024 * 1024)]
public class UploadModel(
    IDocumentService documentService,
    ICourseService courseService,
    ICourseAccessService courseAccessService,
    ILogger<UploadModel> logger) : PageModel
{
    public IReadOnlyList<CourseDto> Courses { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Courses = (await GetVisibleCoursesAsync()).ToList();
    }

    public async Task<IActionResult> OnPostAsync(IFormFile? file, int courseId)
    {
        if (!await CanAccessCourseAsync(courseId))
        {
            return Forbid();
        }

        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Vui long chon file de upload.";
            Courses = (await GetVisibleCoursesAsync()).ToList();
            return Page();
        }

        var allowedExtensions = new[] { ".pdf", ".docx", ".pptx", ".ppt" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
        {
            TempData["Error"] = "Chi ho tro file PDF, DOCX, PPTX va PPT.";
            Courses = (await GetVisibleCoursesAsync()).ToList();
            return Page();
        }

        try
        {
            var uploadDto = new DocumentUploadDto
            {
                CourseId = courseId,
                OriginalFileName = file.FileName,
                ContentType = file.ContentType,
                FileSize = file.Length
            };

            using var stream = file.OpenReadStream();
            var document = await documentService.UploadDocumentAsync(uploadDto, stream);

            TempData["Success"] = $"File '{file.FileName}' da duoc upload thanh cong.";
            return Redirect($"/Document/Details/{document.Id}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading file: {FileName}", file.FileName);
            TempData["Error"] = "Khong the upload file luc nay. Vui long kiem tra dinh dang file va thu lai.";
            Courses = (await GetVisibleCoursesAsync()).ToList();
            return Page();
        }
    }

    private bool CanSeeAllCourses() =>
        User?.Identity?.IsAuthenticated == true &&
        User.IsInRole(ApplicationRoles.Admin);

    private bool IsCourseScopedUser() =>
        User?.Identity?.IsAuthenticated == true &&
        !CanSeeAllCourses() &&
        (User.IsInRole(ApplicationRoles.HeadLecturer) || User.IsInRole(ApplicationRoles.Lecturer));

    private string? CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private async Task<IEnumerable<CourseDto>> GetVisibleCoursesAsync()
    {
        var courses = (await courseService.GetAllCoursesAsync()).ToList();
        if (!IsCourseScopedUser())
        {
            return courses;
        }

        var userId = CurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return [];
        }

        var assignedCourseIds = await courseAccessService.GetAccessibleStaffCourseIdsAsync(userId);
        return courses.Where(course => assignedCourseIds.Contains(course.Id)).ToList();
    }

    private async Task<bool> CanAccessCourseAsync(int courseId)
    {
        if (!IsCourseScopedUser())
        {
            return true;
        }

        var userId = CurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        return await courseAccessService.CanStaffAccessCourseAsync(userId, courseId);
    }
}
