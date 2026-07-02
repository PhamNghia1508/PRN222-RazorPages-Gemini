using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Moq;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services.Interfaces;
using PRN222.DAL.Entities;
using PRN222.Web.Models.Dashboard;
using PRN222.Web.Pages;

namespace PRN222.Tests.Web;

public class HomePageTests
{
    [Fact]
    public void IndexView_ShouldUseSystemAdministratorCopy()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "src", "PRN222.Web", "Pages", "Index.cshtml"));

        source.Should().Contain("var isAdmin = User.IsInRole(ApplicationRoles.Admin);");
        source.Should().Contain("@if (isAdmin)");
        source.Should().Contain("Bảng điều khiển quản trị");
        source.Should().Contain("Theo dõi toàn bộ học liệu, môn học, tài khoản, trạng thái index và chất lượng RAG trong hệ thống.");
        source.Should().Contain("isAdmin ? \"Tổng quan hệ thống\" : \"Hệ thống RAG\"");
        source.Should().Contain("isAdmin ? \"Giám sát trải nghiệm sinh viên\" : \"Trải nghiệm sinh viên\"");
        source.Should().Contain("\"archived\" => \"Đã tạm ẩn\"");
    }

    [Fact]
    public async Task IndexPage_ShouldBuildOverviewDashboardViewModel()
    {
        var documents = new[]
        {
            new DocumentDto(1, "a.pdf", "A.pdf", "application/pdf", 1000, 8, "Indexed", "PRN222", 1, DateTime.UtcNow)
        };
        var courses = new[]
        {
            new CourseDto(1, "PRN222", null, 1, DateTime.UtcNow)
        };

        var documentService = new Mock<IDocumentService>();
        var courseService = new Mock<ICourseService>();
        var courseAccessService = new Mock<ICourseAccessService>();
        var chatService = new Mock<IChatService>();
        var curationService = new Mock<IKnowledgeCurationService>();
        var departmentService = new Mock<IDepartmentService>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            new Mock<IUserStore<ApplicationUser>>().Object,
            null!, null!, null!, null!, null!, null!, null!, null!);
        documentService.Setup(s => s.GetDashboardSummaryAsync(null))
            .ReturnsAsync(new DocumentDashboardSummaryDto(1, 1, 0, 0, 0, 8, documents));
        courseService.Setup(s => s.GetDashboardSummaryAsync(null))
            .ReturnsAsync(new CourseDashboardSummaryDto(1, [1]));
        chatService.Setup(s => s.GetStudentAnalyticsAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(new StudentAnalyticsDto(0, 0.0, 0.0, new List<FailedQueryDto>(), new List<TopDocumentDto>()));

        var page = new IndexModel(
            new Mock<ILogger<IndexModel>>().Object,
            documentService.Object,
            courseService.Object,
            courseAccessService.Object,
            chatService.Object,
            curationService.Object,
            departmentService.Object,
            userManager.Object)
        {
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() }
        };

        await page.OnGetAsync();

        page.Dashboard.Should().BeOfType<OverviewDashboardViewModel>();
        page.Dashboard.TotalDocuments.Should().Be(1);
        page.Dashboard.IndexedDocuments.Should().Be(1);
        page.Dashboard.IndexedChunks.Should().Be(8);
        page.Dashboard.TotalCourses.Should().Be(1);
    }

    private static string FindRepositoryFile(params string[] pathParts)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var candidate = Path.Combine([current.FullName, .. pathParts]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException($"Could not locate repository file: {Path.Combine(pathParts)}");
    }
}
