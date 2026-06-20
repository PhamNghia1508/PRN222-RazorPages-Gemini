using System.Security.Claims;
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
        _ = logger;
        _ = departmentService;

        var courses = (await courseService.GetAllCoursesAsync()).ToList();
        var documents = (await documentService.GetAllDocumentsAsync()).ToList();

        var currentUserId = User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(currentUserId))
        {
            var currentUser = await userManager.Users
                .Include(u => u.Department)
                .FirstOrDefaultAsync(u => u.Id == currentUserId);
            ViewData["UserDepartmentId"] = currentUser?.DepartmentId;
            ViewData["UserDepartmentName"] = currentUser?.Department?.Name;
        }

        if (IsCourseScopedUser())
        {
            IReadOnlySet<int> assignedCourseIds = string.IsNullOrWhiteSpace(currentUserId)
                ? new HashSet<int>()
                : await courseAccessService.GetAccessibleStaffCourseIdsAsync(currentUserId);

            courses = courses.Where(course => assignedCourseIds.Contains(course.Id)).ToList();
            documents = documents.Where(document => assignedCourseIds.Contains(document.CourseId)).ToList();
        }

        var visibleCourseIds = courses.Select(c => c.Id).ToList();
        StudentAnalytics = await chatService.GetStudentAnalyticsAsync(visibleCourseIds);

        if (User?.Identity?.IsAuthenticated == true && (User.IsInRole(ApplicationRoles.Admin) || User.IsInRole(ApplicationRoles.HeadLecturer)))
        {
            PendingProposals = await curationService.GetPendingLogsAsync(visibleCourseIds);
            AuditHistory = await curationService.GetAuditHistoryAsync(visibleCourseIds);
        }
        else if (User?.Identity?.IsAuthenticated == true && User.IsInRole(ApplicationRoles.Lecturer))
        {
            MyProposals = await curationService.GetProposalsByUserAsync(currentUserId ?? string.Empty);
        }

        Dashboard = OverviewDashboardFactory.Build(documents, courses, CanOperateModels());
    }

    private bool IsCourseScopedUser() =>
        User?.Identity?.IsAuthenticated == true &&
        !User.IsInRole(ApplicationRoles.Admin) &&
        (User.IsInRole(ApplicationRoles.HeadLecturer) || User.IsInRole(ApplicationRoles.Lecturer));

    private bool CanOperateModels() =>
        User?.Identity?.IsAuthenticated == true &&
        (User.IsInRole(ApplicationRoles.Admin) || User.IsInRole(ApplicationRoles.HeadLecturer));
}
