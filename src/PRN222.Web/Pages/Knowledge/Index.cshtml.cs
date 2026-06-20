using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services.Interfaces;
using PRN222.Web.Infrastructure;

namespace PRN222.Web.Pages.Knowledge;

[Authorize(Roles = ApplicationRoles.Management)]
public class IndexModel(
    IKnowledgeCurationService curationService,
    ICourseAccessService courseAccessService,
    ILogger<IndexModel> logger) : PageModel
{
    public async Task<IActionResult> OnPostProposeAsync([FromBody] ProposeCorrectionDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var isAdmin = User.IsInRole(ApplicationRoles.Admin);
        IEnumerable<int> allowedCourseIds = isAdmin
            ? Array.Empty<int>()
            : await courseAccessService.GetAccessibleStaffCourseIdsAsync(userId);

        var result = await curationService.ProposeCorrectionAsync(dto, userId, allowedCourseIds, isAdmin);
        if (result)
        {
            return new JsonResult(new { success = true, message = "De xuat sua doi tri thuc thanh cong. Dang cho duyet." });
        }

        return new JsonResult(new { success = false, message = "Yeu cau khong hop le hoac du lieu bi thieu." });
    }

    public async Task<IActionResult> OnPostApproveAsync(int logId)
    {
        if (!CanModerateKnowledge())
        {
            return Forbid();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        try
        {
            var (allowedCourseIds, isAdmin) = await GetCourseScopeAsync(userId);
            var result = await curationService.ApproveCorrectionAsync(logId, userId, allowedCourseIds, isAdmin);
            if (result)
            {
                return new JsonResult(new { success = true, message = "Duyet va va tri thuc thanh cong." });
            }

            return new JsonResult(new { success = false, message = "Khong tim thay de xuat hoac de xuat khong o trang thai Pending." });
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "User {UserId} attempted to approve knowledge log {LogId} outside scope.", userId, logId);
            return Forbid();
        }
    }

    public async Task<IActionResult> OnPostRejectAsync(int logId, string reason)
    {
        if (!CanModerateKnowledge())
        {
            return Forbid();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return new JsonResult(new { success = false, message = "Ly do tu choi khong duoc de trong." });
        }

        try
        {
            var (allowedCourseIds, isAdmin) = await GetCourseScopeAsync(userId);
            var result = await curationService.RejectCorrectionAsync(logId, reason, userId, allowedCourseIds, isAdmin);
            if (result)
            {
                return new JsonResult(new { success = true, message = "Da tu choi de xuat va tri thuc." });
            }

            return new JsonResult(new { success = false, message = "Khong tim thay de xuat hoac de xuat khong o trang thai Pending." });
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "User {UserId} attempted to reject knowledge log {LogId} outside scope.", userId, logId);
            return Forbid();
        }
    }

    public async Task<IActionResult> OnPostRollbackAsync(int logId)
    {
        if (!CanModerateKnowledge())
        {
            return Forbid();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        try
        {
            var (allowedCourseIds, isAdmin) = await GetCourseScopeAsync(userId);
            var result = await curationService.RollbackCorrectionAsync(logId, userId, allowedCourseIds, isAdmin);
            if (result)
            {
                return new JsonResult(new { success = true, message = "Da thu hoi tri thuc thanh cong." });
            }

            return new JsonResult(new { success = false, message = "Khong the thu hoi tri thuc nay." });
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "User {UserId} attempted to rollback knowledge log {LogId} outside scope.", userId, logId);
            return Forbid();
        }
    }

    private bool CanModerateKnowledge() =>
        User.IsInRole(ApplicationRoles.Admin) || User.IsInRole(ApplicationRoles.HeadLecturer);

    private async Task<(IEnumerable<int> CourseIds, bool IsAdmin)> GetCourseScopeAsync(string userId)
    {
        var isAdmin = User.IsInRole(ApplicationRoles.Admin);
        if (isAdmin)
        {
            return ([], true);
        }

        var courseIds = await courseAccessService.GetAccessibleStaffCourseIdsAsync(userId);
        return (courseIds, false);
    }
}
