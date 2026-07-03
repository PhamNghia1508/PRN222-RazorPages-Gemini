using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services.Interfaces;
using PRN222.Web.Infrastructure;

namespace PRN222.Web.Pages.Document;

[Authorize(Roles = ApplicationRoles.Management)]
public class DetailsModel(
    IDocumentService documentService,
    ICourseAccessService courseAccessService,
    ILogger<DetailsModel> logger) : PageModel
{
    public DocumentDetailDto Document { get; private set; } = new();
    public bool CanViewChunks { get; private set; } = true;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var document = await documentService.GetDocumentByIdAsync(id);
        if (document == null)
        {
            return NotFound();
        }

        if (!await CanAccessCourseAsync(document.CourseId))
        {
            return Forbid();
        }

        CanViewChunks = !IsLecturerWithoutChunkAccess();
        if (!CanViewChunks)
        {
            document.ExtractedTextPreview = null;
            document.Chunks = [];
        }

        Document = document;
        return Page();
    }

    public async Task<IActionResult> OnPostProcessAsync(int id)
    {
        if (!CanUploadDocuments())
        {
            return Forbid();
        }

        try
        {
            var document = await documentService.GetDocumentByIdAsync(id);
            if (document == null)
            {
                return NotFound();
            }

            if (!await CanAccessCourseAsync(document.CourseId))
            {
                return Forbid();
            }

            await documentService.EnqueueProcessDocumentAsync(id);
            TempData["Success"] = "Yeu cau xu ly tai lieu da duoc gui va dang chay trong nen.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error triggering background processing for document {DocumentId}", id);
            TempData["Error"] = "Khong the kich hoat xu ly tai lieu luc nay. Vui long thu lai sau.";
        }

        return Redirect($"/Document/Details/{id}");
    }

    public async Task<IActionResult> OnGetStatusAsync(int id)
    {
        var document = await documentService.GetDocumentByIdAsync(id);
        if (document == null)
        {
            return NotFound();
        }

        if (!await CanAccessCourseAsync(document.CourseId))
        {
            return Forbid();
        }

        return new JsonResult(new
        {
            status = document.Status,
            chunkCount = document.ChunkCount,
            errorMessage = document.ErrorMessage
        });
    }

    public async Task<IActionResult> OnPostArchiveAsync(int id, string? archiveReason)
    {
        if (!User.IsInRole(ApplicationRoles.Admin))
        {
            return Forbid();
        }

        var archivedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(archivedByUserId))
        {
            TempData["Error"] = "Không xác định được tài khoản thực hiện tạm ẩn.";
            return Redirect($"/Document/Details/{id}");
        }

        try
        {
            var document = await documentService.GetDocumentByIdAsync(id);
            if (document == null)
            {
                return NotFound();
            }

            if (!await CanAccessCourseAsync(document.CourseId))
            {
                return Forbid();
            }

            await documentService.ArchiveDocumentAsync(id, archivedByUserId, archiveReason!);
            TempData["Success"] = "Đã tạm ẩn tài liệu khỏi RAG. Tài liệu vẫn được giữ lại để truy vết.";
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid archive request for document {DocumentId}", id);
            TempData["Error"] = ex.Message;
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Cannot archive document {DocumentId}", id);
            TempData["Error"] = ex.Message;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error archiving document {DocumentId}", id);
            TempData["Error"] = "Không thể tạm ẩn tài liệu lúc này. Vui lòng thử lại sau.";
        }

        return Redirect("/Document");
    }

    public async Task<IActionResult> OnPostCancelUploadAsync(int id, string? cancellationReason)
    {
        if (!CanUploadDocuments())
        {
            return Forbid();
        }

        var currentUserId = CurrentUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            TempData["Error"] = "Khong xac dinh duoc tai khoan thuc hien huy tai lieu.";
            return Redirect($"/Document/Details/{id}");
        }

        try
        {
            var document = await documentService.GetDocumentByIdAsync(id);
            if (document == null)
            {
                return NotFound();
            }

            if (!await CanAccessCourseAsync(document.CourseId))
            {
                return Forbid();
            }

            if (!string.Equals(document.UploadedByUserId, currentUserId, StringComparison.Ordinal))
            {
                return Forbid();
            }

            await documentService.CancelMistakenUploadAsync(id, currentUserId, cancellationReason!);
            TempData["Success"] = "Da huy tai lieu tai nham. Tai lieu va du lieu lien quan van duoc giu lai de truy vet.";
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid cancellation request for document {DocumentId}", id);
            TempData["Error"] = ex.Message;
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Cannot cancel mistaken upload for document {DocumentId}", id);
            TempData["Error"] = ex.Message;
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Unauthorized cancellation request for document {DocumentId}", id);
            return Forbid();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error cancelling mistaken upload for document {DocumentId}", id);
            TempData["Error"] = "Khong the huy tai lieu tai nham luc nay. Vui long thu lai sau.";
        }

        return Redirect($"/Document/Details/{id}");
    }

    private bool CanUploadDocuments() =>
        User?.Identity?.IsAuthenticated == true &&
        User.IsInRole(ApplicationRoles.HeadLecturer);

    private bool CanSeeAllCourses() =>
        User?.Identity?.IsAuthenticated == true &&
        User.IsInRole(ApplicationRoles.Admin);

    private bool IsCourseScopedUser() =>
        User?.Identity?.IsAuthenticated == true &&
        !CanSeeAllCourses() &&
        (User.IsInRole(ApplicationRoles.HeadLecturer) || User.IsInRole(ApplicationRoles.Lecturer));

    private bool IsLecturerWithoutChunkAccess() =>
        User?.Identity?.IsAuthenticated == true &&
        User.IsInRole(ApplicationRoles.Lecturer) &&
        !User.IsInRole(ApplicationRoles.Admin) &&
        !User.IsInRole(ApplicationRoles.HeadLecturer);

    private string? CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

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
