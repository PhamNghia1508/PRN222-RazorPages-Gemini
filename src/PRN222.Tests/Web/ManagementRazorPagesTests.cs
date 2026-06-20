using FluentAssertions;
using PRN222.Web.Pages.Course;
using AdminAccountsModel = PRN222.Web.Pages.Admin.AccountsModel;
using DepartmentIndexModel = PRN222.Web.Pages.Department.IndexModel;

namespace PRN222.Tests.Web;

public class ManagementRazorPagesTests
{
    [Theory]
    [InlineData("Course", "Index.cshtml")]
    [InlineData("Course", "Create.cshtml")]
    [InlineData("Course", "Edit.cshtml")]
    [InlineData("Course", "_CourseGrid.cshtml")]
    [InlineData("Admin", "Accounts.cshtml")]
    [InlineData("Department", "Index.cshtml")]
    public void CoursePages_ShouldExist(params string[] pathSegments)
    {
        var root = FindRepositoryRoot();

        File.Exists(Path.Combine([root, "src", "PRN222.Web", "Pages", .. pathSegments]))
            .Should().BeTrue();
    }

    [Fact]
    public void CourseIndexPage_ShouldExposeRealtimeAndMutationHandlers()
    {
        typeof(IndexModel).GetMethod(nameof(IndexModel.OnGetAsync)).Should().NotBeNull();
        typeof(IndexModel).GetMethod(nameof(IndexModel.OnGetAssignedCoursesPartialAsync)).Should().NotBeNull();
        typeof(IndexModel).GetMethod(nameof(IndexModel.OnPostDeleteAsync)).Should().NotBeNull();
        typeof(IndexModel).GetMethod(nameof(IndexModel.OnPostCreateAjaxAsync)).Should().NotBeNull();
    }

    [Fact]
    public void CourseFormPages_ShouldExposePostHandlers()
    {
        typeof(CreateModel).GetMethod(nameof(CreateModel.OnGetAsync)).Should().NotBeNull();
        typeof(CreateModel).GetMethod(nameof(CreateModel.OnPostAsync)).Should().NotBeNull();
        typeof(EditModel).GetMethod(nameof(EditModel.OnGetAsync)).Should().NotBeNull();
        typeof(EditModel).GetMethod(nameof(EditModel.OnPostAsync)).Should().NotBeNull();
    }

    [Fact]
    public void CourseMutationHandlers_ShouldUseExplicitAdminGuard()
    {
        var root = FindRepositoryRoot();
        var pageModel = File.ReadAllText(Path.Combine(
            root,
            "src",
            "PRN222.Web",
            "Pages",
            "Course",
            "Index.cshtml.cs"));

        pageModel.Should().Contain("if (!User.IsInRole(ApplicationRoles.Admin))");
        pageModel.Should().Contain("return Forbid();");
        pageModel.Should().NotContain("[Authorize(Roles = ApplicationRoles.Admin)]");
    }

    [Fact]
    public void AdminAccountsPage_ShouldExposeGovernanceHandlers()
    {
        typeof(AdminAccountsModel).GetMethod(nameof(AdminAccountsModel.OnGetAsync)).Should().NotBeNull();
        typeof(AdminAccountsModel).GetMethod(nameof(AdminAccountsModel.OnPostCreateAccountAsync)).Should().NotBeNull();
        typeof(AdminAccountsModel).GetMethod(nameof(AdminAccountsModel.OnPostUpdateAssignmentsAsync)).Should().NotBeNull();
    }

    [Fact]
    public void DepartmentPage_ShouldExposeGovernanceHandlers()
    {
        typeof(DepartmentIndexModel).GetMethod(nameof(DepartmentIndexModel.OnGetAsync)).Should().NotBeNull();
        typeof(DepartmentIndexModel).GetMethod(nameof(DepartmentIndexModel.OnPostCreateAsync)).Should().NotBeNull();
        typeof(DepartmentIndexModel).GetMethod(nameof(DepartmentIndexModel.OnPostAssignUserAsync)).Should().NotBeNull();
        typeof(DepartmentIndexModel).GetMethod(nameof(DepartmentIndexModel.OnPostAssignCourseAsync)).Should().NotBeNull();
        typeof(DepartmentIndexModel).GetMethod(nameof(DepartmentIndexModel.OnPostRemoveUserAsync)).Should().NotBeNull();
    }

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
