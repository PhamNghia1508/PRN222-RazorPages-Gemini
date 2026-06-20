using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using PRN222.Web.Hubs;
using PRN222.Web.Infrastructure;

namespace PRN222.Tests.Web;

public class SignalRConfigurationTests
{
    [Fact]
    public void AdminHub_ShouldRequireManagementRole()
    {
        var authorize = typeof(AdminHub)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .OfType<AuthorizeAttribute>()
            .SingleOrDefault();

        authorize.Should().NotBeNull();
        authorize!.Roles.Should().Be(ApplicationRoles.Management);
    }

    [Fact]
    public void Program_ShouldMapOnlyTheCanonicalAdminHubEndpoint()
    {
        var program = ReadRepositoryFile("src", "PRN222.Web", "Program.cs");

        CountOccurrences(program, "MapHub<AdminHub>").Should().Be(1);
        program.Should().Contain("MapHub<AdminHub>(\"/hubs/admin\")");
        program.Should().NotContain("MapHub<AdminHub>(\"/adminhub\")");
    }

    [Fact]
    public void RazorPages_ShouldNotBroadcastManagementEventsToAllClients()
    {
        var adminPage = ReadRepositoryFile("src", "PRN222.Web", "Pages", "Admin", "Accounts.cshtml.cs");
        var courseIndexPage = ReadRepositoryFile("src", "PRN222.Web", "Pages", "Course", "Index.cshtml.cs");
        var courseCreatePage = ReadRepositoryFile("src", "PRN222.Web", "Pages", "Course", "Create.cshtml.cs");
        var departmentPage = ReadRepositoryFile("src", "PRN222.Web", "Pages", "Department", "Index.cshtml.cs");

        adminPage.Should().NotContain("Clients.All");
        courseIndexPage.Should().NotContain("Clients.All");
        courseCreatePage.Should().NotContain("Clients.All");
        departmentPage.Should().NotContain("Clients.All");
        adminPage.Should().Contain("Clients.Group(AdminHub.AdminsGroup)");
        courseIndexPage.Should().Contain("Clients.Group(AdminHub.AdminsGroup)");
        courseCreatePage.Should().Contain("Clients.Group(AdminHub.AdminsGroup)");
        departmentPage.Should().Contain("Clients.Group(AdminHub.AdminsGroup)");
        departmentPage.Should().Contain("AdminHub.UserAssignmentsUpdated");
        departmentPage.Should().Contain("AdminHub.SubjectUpdated");
        departmentPage.Should().Contain("NotifyRemovedCourseAssignmentUsersAsync");
        departmentPage.Should().Contain("GetAssignedUsersForCourseAsync");
    }

    [Fact]
    public void DashboardRealtime_ShouldSubscribeToDocumentChangesWithoutFullPageReload()
    {
        var hub = ReadRepositoryFile("src", "PRN222.Web", "Hubs", "AdminHub.cs");
        var indexPage = ReadRepositoryFile("src", "PRN222.Web", "Pages", "Index.cshtml");
        var realtimeScript = ReadRepositoryFile("src", "PRN222.Web", "wwwroot", "js", "admin-realtime.js");
        var serviceRegistration = ReadRepositoryFile("src", "PRN222.Web", "Infrastructure", "ServiceCollectionExtensions.cs");
        var documentService = ReadRepositoryFile("src", "PRN222.BLL", "Services", "DocumentService.cs");

        hub.Should().Contain("public const string ManagementGroup");
        hub.Should().Contain("public const string DocumentChanged = \"DocumentChanged\";");
        hub.Should().Contain("Groups.AddToGroupAsync(Context.ConnectionId, ManagementGroup)");
        indexPage.Should().Contain("id=\"dashboardRealtimeWorkspace\"");
        indexPage.Should().Contain("aria-live=\"polite\"");
        indexPage.Should().Contain("data-refresh-url=\"@Url.Page(\"/Index\")\"");
        indexPage.Should().Contain("data-dashboard-total-documents");
        indexPage.Should().Contain("data-dashboard-indexed-documents");
        indexPage.Should().Contain("data-dashboard-indexed-chunks");
        indexPage.Should().Contain("data-dashboard-recent-documents");
        indexPage.Should().Contain("data-document-id=\"@doc.Id\"");
        indexPage.Should().NotContain("window.location.reload()");
        indexPage.Should().NotContain("location.reload()");
        realtimeScript.Should().Contain("connection.on('DocumentChanged'");
        realtimeScript.Should().Contain("refreshDashboardWorkspace");
        realtimeScript.Should().Contain("refreshDocumentWorkspace");
        realtimeScript.Should().Contain("updateDashboardDocumentRow");
        realtimeScript.Should().Contain("refreshDashboardMetrics");
        realtimeScript.Should().Contain("refreshDashboardWorkspaceFromServer");
        realtimeScript.Should().Contain("refreshHtmlFragmentFromPage");
        realtimeScript.Should().Contain("parser.parseFromString");
        realtimeScript.Should().Contain("refreshDashboardWorkspaceFromServer('assignment-updated')");
        realtimeScript.Should().NotContain("Dashboard refresh failed");
        realtimeScript.Should().NotContain("window.location.reload()");
        realtimeScript.Should().NotContain("location.reload()");
        serviceRegistration.Should().Contain("IDocumentRealtimeNotifier");
        serviceRegistration.Should().Contain("SignalRDocumentRealtimeNotifier");
        documentService.Should().Contain("NotifyDocumentChangedAsync");
    }

    [Fact]
    public void DocumentRealtimeNotifier_ShouldTargetAdminsAndAssignedCourseUsers()
    {
        var notifier = ReadRepositoryFile("src", "PRN222.Web", "Infrastructure", "SignalRDocumentRealtimeNotifier.cs");

        notifier.Should().NotContain("Group(AdminHub.ManagementGroup)");
        notifier.Should().Contain("ICourseAssignmentService");
        notifier.Should().Contain("GetAssignedUserIdsForCourseAsync(notification.CourseId)");
        notifier.Should().Contain("Group(AdminHub.AdminsGroup)");
        notifier.Should().Contain("Users(assignedUserIds)");
    }

    [Fact]
    public void Layout_ShouldLoadManagementRealtimeClientOnEveryManagementPage()
    {
        var layout = ReadRepositoryFile("src", "PRN222.Web", "Pages", "Shared", "_Layout.cshtml");
        var courseIndex = ReadRepositoryFile("src", "PRN222.Web", "Pages", "Course", "Index.cshtml");
        var documentIndex = ReadRepositoryFile("src", "PRN222.Web", "Pages", "Document", "Index.cshtml");
        var documentIndexModel = ReadRepositoryFile("src", "PRN222.Web", "Pages", "Document", "Index.cshtml.cs");
        var realtimeScript = ReadRepositoryFile("src", "PRN222.Web", "wwwroot", "js", "admin-realtime.js");

        layout.Should().Contain("@if (isManagementUser)");
        layout.Should().Contain("signalr.min.js");
        layout.Should().Contain("admin-realtime.js");
        layout.Should().Contain("data-current-controller=\"@currentController\"");
        layout.Should().NotContain("@if (isAdmin && string.Equals(currentController, \"Admin\", StringComparison.OrdinalIgnoreCase))");
        courseIndex.Should().NotContain("headlecturer-course-realtime.js");
        realtimeScript.Should().Contain("currentController === 'document'");
        realtimeScript.Should().NotContain("window.location.reload()");
        realtimeScript.Should().NotContain("location.reload()");
        realtimeScript.Should().Contain("refreshDocumentWorkspace");
        realtimeScript.Should().Contain("fetch(refreshUrl");
        documentIndex.Should().Contain("id=\"documentRealtimeWorkspace\"");
        documentIndex.Should().Contain("data-refresh-url=");
        documentIndexModel.Should().Contain("OnGetWorkspacePartialAsync");
    }

    [Fact]
    public void CourseRealtimeClient_ShouldUseCanonicalHubEndpoint()
    {
        var script = ReadRepositoryFile(
            "src",
            "PRN222.Web",
            "wwwroot",
            "js",
            "headlecturer-course-realtime.js");

        script.Should().Contain(".withUrl('/hubs/admin')");
        script.Should().NotContain(".withUrl('/adminhub')");
        script.Should().Contain("connection.on('SubjectCreated'");
        script.Should().Contain("connection.on('SubjectUpdated'");
        script.Should().Contain("connection.on('SubjectDeleted'");
        script.Should().Contain("connection.on('UserAssignmentsUpdated'");
    }

    [Fact]
    public void SignalRChange_ShouldPreserveNet8TargetFramework()
    {
        var projectFiles = Directory.GetFiles(
            FindRepositoryRoot(),
            "*.csproj",
            SearchOption.AllDirectories);

        projectFiles.Should().NotBeEmpty();
        foreach (var projectFile in projectFiles)
        {
            var content = File.ReadAllText(projectFile);
            content.Should().Contain("<TargetFramework>net8.0</TargetFramework>", projectFile);
            content.Should().NotContain("<TargetFramework>net9.0</TargetFramework>", projectFile);
        }
    }

    private static int CountOccurrences(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;

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

