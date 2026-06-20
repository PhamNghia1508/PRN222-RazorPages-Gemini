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

public class DashboardRazorPagesTests
{
    [Fact]
    public void DashboardPage_ShouldExistAtRootRoute()
    {
        var page = ReadRepositoryFile("src", "PRN222.Web", "Pages", "Index.cshtml");

        page.Should().Contain("@page \"/\"");
        page.Should().Contain("@model PRN222.Web.Pages.IndexModel");
    }

    [Fact]
    public async Task DashboardPageModel_OnGet_ShouldBuildOverviewDashboard()
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
        var userManager = CreateUserManagerMock();

        documentService.Setup(s => s.GetAllDocumentsAsync()).ReturnsAsync(documents);
        courseService.Setup(s => s.GetAllCoursesAsync()).ReturnsAsync(courses);
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
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        await page.OnGetAsync();

        page.Dashboard.Should().BeOfType<OverviewDashboardViewModel>();
        page.Dashboard.TotalDocuments.Should().Be(1);
        page.Dashboard.IndexedDocuments.Should().Be(1);
        page.Dashboard.IndexedChunks.Should().Be(8);
        page.Dashboard.TotalCourses.Should().Be(1);
    }

    [Fact]
    public void DashboardMigration_ShouldMakeRootDashboardCanonicalForManagementUsers()
    {
        var loginPageModel = ReadRepositoryFile("src", "PRN222.Web", "Pages", "Account", "Login.cshtml.cs");
        var pagesLayout = ReadRepositoryFile("src", "PRN222.Web", "Pages", "Shared", "_Layout.cshtml");

        loginPageModel.Should().Contain("return Redirect(\"/\");");
        loginPageModel.Should().NotContain("RedirectToAction(\"Index\", \"Home\")");
        pagesLayout.Should().Contain("var homeHref = isManagementUser ? \"/\" : \"/Chat/Session\";");
        pagesLayout.Should().Contain("class=\"@NavClass(\"Home\")\" href=\"/\"");
        pagesLayout.Should().NotContain("asp-controller=\"Home\" asp-action=\"Index\"");
    }

    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock()
    {
        return new Mock<UserManager<ApplicationUser>>(
            new Mock<IUserStore<ApplicationUser>>().Object,
            null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private static string ReadRepositoryFile(params string[] pathSegments) =>
        File.ReadAllText(Path.Combine([FindRepositoryRoot(), .. pathSegments]));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git"))
                || File.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
