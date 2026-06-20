using FluentAssertions;

namespace PRN222.Tests.Web;

public class RazorPagesConfigurationTests
{
    [Fact]
    public void Program_ShouldMapRazorPagesWithoutMvcControllerRoute()
    {
        var program = ReadRepositoryFile("src", "PRN222.Web", "Program.cs");

        program.Should().Contain(".AddApplicationWeb()");
        program.Should().Contain("app.MapRazorPages()");
        program.Should().NotContain("MapControllerRoute");
    }

    [Fact]
    public void ServiceRegistration_ShouldUseRazorPagesWithoutControllersWithViews()
    {
        var services = ReadRepositoryFile("src", "PRN222.Web", "Infrastructure", "ServiceCollectionExtensions.cs");

        services.Should().Contain("AddRazorPages()");
        services.Should().NotContain("AddApplicationMvc");
        services.Should().NotContain("AddControllersWithViews()");
        services.Should().Contain(".AddJsonOptions(options =>");
        services.Should().Contain("PropertyNameCaseInsensitive = true");
        services.Should().Contain("PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase");
    }

    [Fact]
    public void RazorPages_ShouldHaveSharedImportsStartAndLayout()
    {
        var root = FindRepositoryRoot();
        var pagesViewImports = Path.Combine(root, "src", "PRN222.Web", "Pages", "_ViewImports.cshtml");
        var pagesViewStart = Path.Combine(root, "src", "PRN222.Web", "Pages", "_ViewStart.cshtml");
        var pagesSharedLayout = Path.Combine(root, "src", "PRN222.Web", "Pages", "Shared", "_Layout.cshtml");

        File.Exists(pagesViewImports).Should().BeTrue();
        File.Exists(pagesViewStart).Should().BeTrue();
        File.Exists(pagesSharedLayout).Should().BeTrue();

        var pagesLayout = File.ReadAllText(pagesSharedLayout);

        File.ReadAllText(pagesViewStart).Should().Contain("Layout = \"_Layout\";");
        File.ReadAllText(pagesViewImports).Should().Contain("@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers");
        pagesLayout.Should().Contain("ViewContext.RouteData.Values[\"page\"]");
        pagesLayout.Should().Contain("currentPage.Equals(\"/Index\", StringComparison.OrdinalIgnoreCase)");
        pagesLayout.Should().Contain("currentPage.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries)");
    }

    [Fact]
    public void Project_ShouldNotContainMvcControllersOrViews()
    {
        var root = FindRepositoryRoot();

        Directory.Exists(Path.Combine(root, "src", "PRN222.Web", "Controllers")).Should().BeFalse();
        Directory.Exists(Path.Combine(root, "src", "PRN222.Web", "Views")).Should().BeFalse();
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
