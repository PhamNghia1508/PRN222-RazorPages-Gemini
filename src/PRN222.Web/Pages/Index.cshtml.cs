using System.Security.Claims;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services.Interfaces;
using PRN222.DAL.Entities;
using PRN222.Web.Infrastructure;
using PRN222.Web.Models.Dashboard;

namespace PRN222.Web.Pages;

[Authorize(Roles = ApplicationRoles.Management)]
public class IndexModel(
    ILogger<IndexModel> logger,
    IDocumentService documentService,
    ICourseService courseService,
    ICourseAccessService courseAccessService,
    IChatService chatService,
    IKnowledgeCurationService curationService,
    IDepartmentService departmentService,
    UserManager<ApplicationUser> userManager) : PageModel
{
    public OverviewDashboardViewModel Dashboard { get; private set; } = new();
    public StudentAnalyticsDto? StudentAnalytics { get; private set; }
    public IEnumerable<KnowledgeAuditLogDto>? PendingProposals { get; private set; }
    public IEnumerable<KnowledgeAuditLogDto>? AuditHistory { get; private set; }
    public IEnumerable<KnowledgeAuditLogDto>? MyProposals { get; private set; }

    public async Task OnGetAsync()
    {
        _ = departmentService;
        var totalStopwatch = Stopwatch.StartNew();

        var blockStopwatch = Stopwatch.StartNew();
        var currentUserId = User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(currentUserId))
        {
            var currentUser = await userManager.Users
                .Include(u => u.Department)
                .FirstOrDefaultAsync(u => u.Id == currentUserId);
            ViewData["UserDepartmentId"] = currentUser?.DepartmentId;
            ViewData["UserDepartmentName"] = currentUser?.Department?.Name;
            logger.LogInformation(
                "PERF_TEMP Dashboard current user/account profile completed in {ElapsedMilliseconds} ms",
                blockStopwatch.ElapsedMilliseconds);
        }

        IReadOnlySet<int>? assignedCourseIds = null;
        if (IsCourseScopedUser())
        {
            blockStopwatch.Restart();
            assignedCourseIds = string.IsNullOrWhiteSpace(currentUserId)
                ? new HashSet<int>()
                : await courseAccessService.GetAccessibleStaffCourseIdsAsync(currentUserId);
            logger.LogInformation(
                "PERF_TEMP Dashboard course access scope completed in {ElapsedMilliseconds} ms",
                blockStopwatch.ElapsedMilliseconds);
        }

        blockStopwatch.Restart();
        var courses = await courseService.GetDashboardSummaryAsync(assignedCourseIds);
        logger.LogInformation(
            "PERF_TEMP Dashboard GetCourseDashboardSummaryAsync completed in {ElapsedMilliseconds} ms",
            blockStopwatch.ElapsedMilliseconds);

        blockStopwatch.Restart();
        var documents = await documentService.GetDashboardSummaryAsync(assignedCourseIds);
        logger.LogInformation(
            "PERF_TEMP Dashboard GetDocumentDashboardSummaryAsync completed in {ElapsedMilliseconds} ms",
            blockStopwatch.ElapsedMilliseconds);

        var visibleCourseIds = courses.CourseIds;
        blockStopwatch.Restart();
        StudentAnalytics = await chatService.GetStudentAnalyticsAsync(visibleCourseIds);
        logger.LogInformation(
            "PERF_TEMP Dashboard GetStudentAnalyticsAsync completed in {ElapsedMilliseconds} ms",
            blockStopwatch.ElapsedMilliseconds);

        if (User?.Identity?.IsAuthenticated == true && (User.IsInRole(ApplicationRoles.Admin) || User.IsInRole(ApplicationRoles.HeadLecturer)))
        {
            blockStopwatch.Restart();
            PendingProposals = await curationService.GetPendingLogsAsync(visibleCourseIds);
            AuditHistory = await curationService.GetAuditHistoryAsync(visibleCourseIds);
            logger.LogInformation(
                "PERF_TEMP Dashboard curation logs completed in {ElapsedMilliseconds} ms",
                blockStopwatch.ElapsedMilliseconds);
        }
        else if (User?.Identity?.IsAuthenticated == true && User.IsInRole(ApplicationRoles.Lecturer))
        {
            blockStopwatch.Restart();
            MyProposals = await curationService.GetProposalsByUserAsync(currentUserId ?? string.Empty);
            logger.LogInformation(
                "PERF_TEMP Dashboard curation logs completed in {ElapsedMilliseconds} ms",
                blockStopwatch.ElapsedMilliseconds);
        }

        blockStopwatch.Restart();
        Dashboard = OverviewDashboardFactory.Build(documents, courses, CanOperateModels());
        logger.LogInformation(
            "PERF_TEMP Dashboard OverviewDashboardFactory completed in {ElapsedMilliseconds} ms",
            blockStopwatch.ElapsedMilliseconds);
        logger.LogInformation(
            "PERF_TEMP Dashboard OnGetAsync total completed in {ElapsedMilliseconds} ms",
            totalStopwatch.ElapsedMilliseconds);
    }

    private bool IsCourseScopedUser() =>
        User?.Identity?.IsAuthenticated == true &&
        !User.IsInRole(ApplicationRoles.Admin) &&
        (User.IsInRole(ApplicationRoles.HeadLecturer) || User.IsInRole(ApplicationRoles.Lecturer));

    private bool CanOperateModels() =>
        User?.Identity?.IsAuthenticated == true &&
        (User.IsInRole(ApplicationRoles.Admin) || User.IsInRole(ApplicationRoles.HeadLecturer));
}
