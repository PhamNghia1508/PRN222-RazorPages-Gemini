using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services.Interfaces;
using PRN222.Web.Infrastructure;

namespace PRN222.Web.Pages.Document;

[Authorize(Roles = ApplicationRoles.Management)]
public class IndexModel(
    IDocumentService documentService,
    ICourseService courseService,
    ICourseAccessService courseAccessService) : PageModel
{
    public PagedResult<DocumentDto> Documents { get; private set; } = PagedResult<DocumentDto>.Create([], 1, PagedResult<DocumentDto>.DefaultPageSize);
    public IReadOnlyList<CourseDto> Courses { get; private set; } = [];
    public int? SelectedCourseId { get; private set; }
    public string? SelectedStatus { get; private set; }
    public string? SelectedFileType { get; private set; }
    public string? Search { get; private set; }
    public bool HasActiveFilters { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        int? courseId,
        string? status,
        string? fileType,
        string? search,
        int page = 1,
        int pageSize = 25)
    {
        var blockedResult = await LoadWorkspaceAsync(courseId, status, fileType, search, page, pageSize);
        return blockedResult ?? Page();
    }

    public async Task<IActionResult> OnGetWorkspacePartialAsync(
        int? courseId,
        string? status,
        string? fileType,
        string? search,
        int page = 1,
        int pageSize = 25)
    {
        var blockedResult = await LoadWorkspaceAsync(courseId, status, fileType, search, page, pageSize);
        if (blockedResult is not null)
        {
            return blockedResult;
        }

        Response.Headers.CacheControl = "no-store";
        return new PartialViewResult
        {
            ViewName = "/Pages/Document/_DocumentWorkspace.cshtml",
            ViewData = new ViewDataDictionary<IndexModel>(ViewData, this)
        };
    }

    private async Task<IActionResult?> LoadWorkspaceAsync(
        int? courseId,
        string? status,
        string? fileType,
        string? search,
        int page,
        int pageSize)
    {
        var visibleCourses = (await GetVisibleCoursesAsync()).ToList();
        var visibleCourseIds = visibleCourses.Select(course => course.Id).ToHashSet();

        if (IsCourseScopedUser() && courseId.HasValue && !visibleCourseIds.Contains(courseId.Value))
        {
            return Forbid();
        }

        IEnumerable<DocumentDto> documents = courseId.HasValue
            ? await documentService.GetDocumentsByCourseAsync(courseId.Value)
            : await documentService.GetAllDocumentsAsync();

        if (IsCourseScopedUser())
        {
            documents = documents.Where(document => visibleCourseIds.Contains(document.CourseId));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            documents = documents.Where(d => d.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(fileType))
        {
            documents = fileType.ToLowerInvariant() switch
            {
                "pdf" => documents.Where(d => d.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase)),
                "docx" => documents.Where(d => d.ContentType.Contains("word", StringComparison.OrdinalIgnoreCase)),
                "ppt" or "pptx" => documents.Where(d =>
                    d.ContentType.Contains("presentation", StringComparison.OrdinalIgnoreCase) ||
                    d.ContentType.Contains("powerpoint", StringComparison.OrdinalIgnoreCase)),
                _ => documents
            };
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            documents = documents.Where(d =>
                d.OriginalFileName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                d.CourseName.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var orderedDocuments = documents
            .OrderByDescending(d => d.CreatedAt)
            .ThenBy(d => d.OriginalFileName);

        Courses = visibleCourses;
        SelectedCourseId = courseId;
        SelectedStatus = status;
        SelectedFileType = fileType;
        Search = search;
        HasActiveFilters = courseId.HasValue ||
            !string.IsNullOrWhiteSpace(status) ||
            !string.IsNullOrWhiteSpace(fileType) ||
            !string.IsNullOrWhiteSpace(search);
        Documents = PagedResult<DocumentDto>.Create(orderedDocuments, page, pageSize);

        return null;
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
}
