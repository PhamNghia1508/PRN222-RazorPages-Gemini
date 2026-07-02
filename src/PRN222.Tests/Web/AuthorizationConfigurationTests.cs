using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using PRN222.Web.Infrastructure;
using PRN222.Web.Models.Auth;
using AccountLoginModel = PRN222.Web.Pages.Account.LoginModel;
using AdminAccountsModel = PRN222.Web.Pages.Admin.AccountsModel;
using ChatSessionModel = PRN222.Web.Pages.Chat.SessionModel;
using CodeRunnerModel = PRN222.Web.Pages.CodeRunner.IndexModel;
using CourseCreateModel = PRN222.Web.Pages.Course.CreateModel;
using CourseEditModel = PRN222.Web.Pages.Course.EditModel;
using CourseIndexModel = PRN222.Web.Pages.Course.IndexModel;
using DepartmentIndexModel = PRN222.Web.Pages.Department.IndexModel;
using DocumentDetailsModel = PRN222.Web.Pages.Document.DetailsModel;
using DocumentIndexModel = PRN222.Web.Pages.Document.IndexModel;
using DocumentUploadModel = PRN222.Web.Pages.Document.UploadModel;
using EvaluationIndexModel = PRN222.Web.Pages.Evaluation.IndexModel;
using FinetuneDatasetModel = PRN222.Web.Pages.Finetune.DatasetModel;
using FinetuneIndexModel = PRN222.Web.Pages.Finetune.IndexModel;
using HomeIndexModel = PRN222.Web.Pages.IndexModel;
using KnowledgeIndexModel = PRN222.Web.Pages.Knowledge.IndexModel;
using TestSetGeneratorIndexModel = PRN222.Web.Pages.TestSetGenerator.IndexModel;
using TestSetIndexModel = PRN222.Web.Pages.TestSet.IndexModel;

namespace PRN222.Tests.Web;

public class AuthorizationConfigurationTests
{
    [Fact]
    public void ApplicationRoles_ShouldExposeApplicationRoles()
    {
        ApplicationRoles.Admin.Should().Be("Admin");
        ApplicationRoles.HeadLecturer.Should().Be("HeadLecturer");
        ApplicationRoles.Lecturer.Should().Be("Lecturer");
        ApplicationRoles.Student.Should().Be("Student");
        ApplicationRoles.Management.Should().Be("Admin,HeadLecturer,Lecturer");
        ApplicationRoles.DocumentUpload.Should().Be("HeadLecturer");
        ApplicationRoles.ModelOperations.Should().Be("Admin,HeadLecturer");
        ApplicationRoles.ChatUsers.Should().Be("Student,Lecturer,HeadLecturer,Admin");
        ApplicationRoles.All.Should().Equal("Admin", "HeadLecturer", "Lecturer", "Student");
        ApplicationRoles.StaffCreatableByAdmin.Should().Equal("HeadLecturer", "Lecturer");
        ApplicationRoles.CourseAssignableByAdmin.Should().Equal("HeadLecturer", "Lecturer");
        ApplicationRoles.CanBeAssignedCourses(ApplicationRoles.HeadLecturer).Should().BeTrue();
        ApplicationRoles.CanBeAssignedCourses(ApplicationRoles.Lecturer).Should().BeTrue();
        ApplicationRoles.CanBeAssignedCourses(ApplicationRoles.Admin).Should().BeFalse();
        ApplicationRoles.CanBeAssignedCourses(ApplicationRoles.Student).Should().BeFalse();
    }

    [Theory]
    [InlineData("SeedAccounts:Admin:Email", "admin@demo.local", "SeedAccounts:Admin:Password", "Admin@123", typeof(SeededAdminOptions))]
    [InlineData("SeedAccounts:HeadLecturer:Email", "headlecturer@demo.local", "SeedAccounts:HeadLecturer:Password", "HeadLecturer@123", typeof(SeededHeadLecturerOptions))]
    [InlineData("SeedAccounts:Lecturer:Email", "lecturer@demo.local", "SeedAccounts:Lecturer:Password", "Lecturer@123", typeof(SeededLecturerOptions))]
    [InlineData("SeedAccounts:Student:Email", "student@demo.local", "SeedAccounts:Student:Password", "Student@123", typeof(SeededStudentOptions))]
    public void SeededAccountOptions_ShouldReadDemoConfig(string emailKey, string email, string passwordKey, string password, Type optionsType)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [emailKey] = email, [passwordKey] = password })
            .Build();

        dynamic options = optionsType.GetMethod("FromConfiguration")!.Invoke(null, [configuration])!;

        ((bool)options.IsConfigured).Should().BeTrue();
        ((string)options.Email).Should().Be(email);
        ((string)options.Password).Should().Be(password);
    }

    [Theory]
    [InlineData(typeof(HomeIndexModel), ApplicationRoles.Management)]
    [InlineData(typeof(DocumentIndexModel), ApplicationRoles.Management)]
    [InlineData(typeof(CourseIndexModel), ApplicationRoles.Management)]
    [InlineData(typeof(FinetuneIndexModel), ApplicationRoles.ModelOperations)]
    [InlineData(typeof(FinetuneDatasetModel), ApplicationRoles.ModelOperations)]
    [InlineData(typeof(EvaluationIndexModel), ApplicationRoles.ModelOperations)]
    [InlineData(typeof(TestSetGeneratorIndexModel), ApplicationRoles.ModelOperations)]
    [InlineData(typeof(AdminAccountsModel), ApplicationRoles.Admin)]
    [InlineData(typeof(DepartmentIndexModel), ApplicationRoles.Admin)]
    [InlineData(typeof(ChatSessionModel), ApplicationRoles.ChatUsers)]
    [InlineData(typeof(KnowledgeIndexModel), ApplicationRoles.Management)]
    public void RazorPageModels_ShouldRequireExpectedRoles(Type pageModelType, string expectedRoles)
    {
        var authorize = pageModelType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .OfType<AuthorizeAttribute>()
            .SingleOrDefault();

        authorize.Should().NotBeNull();
        authorize!.Roles.Should().Be(expectedRoles);
    }

    [Theory]
    [InlineData(typeof(CourseCreateModel))]
    [InlineData(typeof(CourseEditModel))]
    public void CourseMutationPages_ShouldRequireAdminRole(Type pageModelType)
    {
        var authorize = pageModelType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .OfType<AuthorizeAttribute>()
            .SingleOrDefault();

        authorize.Should().NotBeNull();
        authorize!.Roles.Should().Be(ApplicationRoles.Admin);
    }

    [Theory]
    [InlineData(typeof(DocumentUploadModel))]
    public void DocumentMutationPages_ShouldRequireDocumentUploadRole(Type pageModelType)
    {
        var authorize = pageModelType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .OfType<AuthorizeAttribute>()
            .SingleOrDefault();

        authorize.Should().NotBeNull();
        authorize!.Roles.Should().Be(ApplicationRoles.DocumentUpload);
    }

    [Fact]
    public void RegisterViewModel_ShouldNotAllowClientSelectedRole()
    {
        typeof(RegisterViewModel).GetProperty("Role").Should().BeNull("public registration must not allow users to self-assign privileged roles");
    }

    [Theory]
    [InlineData(typeof(ChatSessionModel), "OnPostAskAsync")]
    [InlineData(typeof(CourseIndexModel), "OnPostCreateAjaxAsync")]
    [InlineData(typeof(FinetuneDatasetModel), "OnPostGenerateAjaxAsync")]
    [InlineData(typeof(EvaluationIndexModel), "OnPostCreateRunAsync")]
    [InlineData(typeof(TestSetGeneratorIndexModel), "OnPostGenerateAsync")]
    [InlineData(typeof(TestSetGeneratorIndexModel), "OnPostStopAsync")]
    [InlineData(typeof(TestSetGeneratorIndexModel), "OnPostClearAutoGeneratedAsync")]
    [InlineData(typeof(TestSetIndexModel), "OnPostSeedAsync")]
    [InlineData(typeof(DocumentDetailsModel), "OnPostProcessAsync")]
    [InlineData(typeof(DocumentDetailsModel), "OnPostArchiveAsync")]
    [InlineData(typeof(AdminAccountsModel), "OnPostCreateAccountAsync")]
    [InlineData(typeof(AdminAccountsModel), "OnPostUpdateAssignmentsAsync")]
    [InlineData(typeof(ChatSessionModel), "OnPostFeedbackAsync")]
    [InlineData(typeof(CodeRunnerModel), "OnPostExecuteAsync")]
    [InlineData(typeof(KnowledgeIndexModel), "OnPostProposeAsync")]
    [InlineData(typeof(KnowledgeIndexModel), "OnPostApproveAsync")]
    [InlineData(typeof(KnowledgeIndexModel), "OnPostRejectAsync")]
    [InlineData(typeof(KnowledgeIndexModel), "OnPostRollbackAsync")]
    [InlineData(typeof(DepartmentIndexModel), "OnPostCreateAsync")]
    [InlineData(typeof(DepartmentIndexModel), "OnPostAssignUserAsync")]
    [InlineData(typeof(DepartmentIndexModel), "OnPostAssignCourseAsync")]
    [InlineData(typeof(DepartmentIndexModel), "OnPostRemoveUserAsync")]
    public void UnsafeRazorPageHandlers_ShouldExist(Type pageModelType, string handlerName)
    {
        pageModelType.GetMethod(handlerName).Should().NotBeNull();
    }

    [Fact]
    public void LoginView_ShouldDescribeLecturerCapabilitiesConsistentlyWithAuthorization()
    {
        var loginView = File.ReadAllText(FindRepositoryFile("src", "PRN222.Web", "Pages", "Account", "Login.cshtml"));

        loginView.Should().Contain("Giang vien theo doi cac mon duoc phan cong va dung Chat RAG.");
        loginView.Should().Contain("Hint = \"Theo doi mon duoc phan cong va dung Chat RAG\"");
        loginView.Should().NotContain("Giảng viên theo dõi dashboard, sinh test set, benchmark và dataset fine-tune.");
        typeof(AccountLoginModel).GetProperty("ReturnUrl").Should().NotBeNull();
    }

    [Fact]
    public void Layout_ShouldExposeUnifiedAdminGovernanceNavigation()
    {
        var layoutView = File.ReadAllText(FindRepositoryFile("src", "PRN222.Web", "Pages", "Shared", "_Layout.cshtml"));
        var tabsView = File.ReadAllText(FindRepositoryFile("src", "PRN222.Web", "Pages", "Shared", "_StaffGovernanceTabs.cshtml"));
        var accountsView = File.ReadAllText(FindRepositoryFile("src", "PRN222.Web", "Pages", "Admin", "Accounts.cshtml"));

        layoutView.Should().Contain("<span>Nhân sự &amp; Khoa</span>");
        layoutView.Should().Contain("aria-current=\"@(governanceNavActive ? \"page\" : null)\"");
        layoutView.Should().NotContain("<span>Quản lý Khoa</span>");
        tabsView.Should().Contain("aria-current=");
        tabsView.Should().Contain("Tài khoản giảng viên");
        tabsView.Should().Contain("Khoa và phạm vi môn học");
        accountsView.Should().Contain("js-department-picker");
        accountsView.Should().Contain("data-governance-page=\"accounts\"");
        accountsView.Should().Contain("data-governance-ajax=\"true\"");
    }

    [Fact]
    public void GovernanceViews_ShouldUseAjaxToastEnhancementsInsteadOfWindowConfirm()
    {
        var accountsView = File.ReadAllText(FindRepositoryFile("src", "PRN222.Web", "Pages", "Admin", "Accounts.cshtml"));
        var departmentView = File.ReadAllText(FindRepositoryFile("src", "PRN222.Web", "Pages", "Department", "Index.cshtml"));
        var governanceScript = File.ReadAllText(FindRepositoryFile("src", "PRN222.Web", "wwwroot", "js", "governance.js"));

        accountsView.Should().Contain("js-governance-root");
        departmentView.Should().Contain("js-governance-root");
        governanceScript.Should().Contain("showGovernanceConfirmToast");
        governanceScript.Should().Contain("fetch(form.action");
        governanceScript.Should().NotContain("window.confirm");
    }

    [Fact]
    public void OperationalViews_ShouldUseStructuredFeedbackInsteadOfBrowserDialogs()
    {
        var homeView = File.ReadAllText(FindRepositoryFile("src", "PRN222.Web", "Pages", "Index.cshtml"));
        var documentDetailsView = File.ReadAllText(FindRepositoryFile("src", "PRN222.Web", "Pages", "Document", "Details.cshtml"));

        homeView.Should().Contain("GovernancePage.showGovernanceConfirmToast");
        homeView.Should().Contain("GovernancePage.showStatusToast");
        homeView.Should().NotContain("alert(");
        homeView.Should().NotContain("confirm(");
        documentDetailsView.Should().Contain("id=\"archiveDocumentModal\"");
        documentDetailsView.Should().Contain("name=\"archiveReason\"");
        documentDetailsView.Should().NotContain("alert(");
        documentDetailsView.Should().NotContain("confirm(");
    }

    private static string FindRepositoryFile(params string[] pathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. pathParts]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate repository file: {Path.Combine(pathParts)}");
    }
}
