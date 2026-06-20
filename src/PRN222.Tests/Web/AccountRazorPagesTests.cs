using FluentAssertions;
using PRN222.Web.Models.Auth;
using PRN222.Web.Pages.Account;

namespace PRN222.Tests.Web;

public class AccountRazorPagesTests
{
    [Theory]
    [InlineData("Login.cshtml", "@page \"/Account/Login\"")]
    [InlineData("Register.cshtml", "@page \"/Account/Register\"")]
    [InlineData("AccessDenied.cshtml", "@page \"/Account/AccessDenied\"")]
    [InlineData("Logout.cshtml", "@page \"/Account/Logout\"")]
    public void AccountPages_ShouldExistWithExplicitRoutes(string fileName, string pageDirective)
    {
        var page = ReadRepositoryFile("src", "PRN222.Web", "Pages", "Account", fileName);

        page.Should().Contain(pageDirective);
    }

    [Fact]
    public void LoginPageModel_ShouldExposeInputAndReturnUrl()
    {
        typeof(LoginModel).GetProperty(nameof(LoginModel.Input))!
            .PropertyType.Should().Be(typeof(LoginViewModel));
        typeof(LoginModel).GetProperty(nameof(LoginModel.ReturnUrl))
            .Should().NotBeNull();
    }

    [Fact]
    public void LoginPageModel_OnGet_ShouldCopyReturnUrlIntoInput()
    {
        var page = new LoginModel(null!, null!);

        page.OnGet("/Document");

        page.ReturnUrl.Should().Be("/Document");
        page.Input.ReturnUrl.Should().Be("/Document");
    }

    [Fact]
    public void RegisterPageModel_ShouldExposeInputAndReturnUrl()
    {
        typeof(RegisterModel).GetProperty(nameof(RegisterModel.Input))!
            .PropertyType.Should().Be(typeof(RegisterViewModel));
        typeof(RegisterModel).GetProperty(nameof(RegisterModel.ReturnUrl))
            .Should().NotBeNull();
    }

    [Fact]
    public void RegisterPageModel_OnGet_ShouldCopyReturnUrlIntoInput()
    {
        var page = new RegisterModel(null!, null!);

        page.OnGet("/Chat/New");

        page.ReturnUrl.Should().Be("/Chat/New");
        page.Input.ReturnUrl.Should().Be("/Chat/New");
    }

    [Fact]
    public void LogoutPageModel_ShouldExposePostHandler()
    {
        typeof(LogoutModel).GetMethod(nameof(LogoutModel.OnPostAsync))
            .Should().NotBeNull();
    }

    [Fact]
    public void AccountPageModels_ShouldKeepHybridRedirectTargets()
    {
        var loginModel = ReadRepositoryFile("src", "PRN222.Web", "Pages", "Account", "Login.cshtml.cs");
        var registerModel = ReadRepositoryFile("src", "PRN222.Web", "Pages", "Account", "Register.cshtml.cs");
        var logoutModel = ReadRepositoryFile("src", "PRN222.Web", "Pages", "Account", "Logout.cshtml.cs");

        loginModel.Should().Contain("return Redirect(\"/\");");
        registerModel.Should().Contain("return Redirect(\"/\");");
        loginModel.Should().Contain("return Redirect(\"/Chat/Session\");");
        registerModel.Should().Contain("return Redirect(\"/Chat/Session\");");
        loginModel.Should().NotContain("RedirectToPage(\"/Index\")");
        registerModel.Should().NotContain("RedirectToPage(\"/Index\")");
        logoutModel.Should().Contain("await signInManager.SignOutAsync();");
        logoutModel.Should().Contain("return RedirectToPage(\"/Account/Login\");");
    }

    [Fact]
    public void AccountForms_ShouldPostToRazorPages()
    {
        var login = ReadRepositoryFile("src", "PRN222.Web", "Pages", "Account", "Login.cshtml");
        var register = ReadRepositoryFile("src", "PRN222.Web", "Pages", "Account", "Register.cshtml");

        login.Should().Contain("<form method=\"post\"");
        login.Should().NotContain("asp-action=\"Login\"");
        register.Should().Contain("<form method=\"post\"");
        register.Should().NotContain("asp-action=\"Register\"");
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
