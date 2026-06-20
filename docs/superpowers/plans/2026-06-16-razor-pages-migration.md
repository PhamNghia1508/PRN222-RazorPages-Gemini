# Razor Pages Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert the MVC presentation layer to Razor Pages in the isolated `codex/razor-pages-migration` worktree without changing the original MVC checkout.

**Architecture:** Razor Pages owns all page rendering and request handlers by the end of the migration. During incremental migration, MVC registration and conventional MVC routing remain active until matching Razor Pages exist, so the worktree app stays routable. Existing BLL, DAL, Identity, SignalR, view models, JavaScript, CSS, and seeded data stay in place unless a small route-facing adaptation is required. Old URLs are preserved with explicit `@page` routes where practical.

**Tech Stack:** ASP.NET Core 8 Razor Pages, Identity, Entity Framework Core, SignalR, xUnit, FluentAssertions, SQL Server.

---

## Worktree

All commands in this plan run from:

```powershell
C:\Users\ADMIN\Documents\FPT\PRN222_Assignment1\.worktrees\razor-pages-migration
```

Do not edit the original MVC checkout while executing this plan.

## File Structure

Create:

- `src/PRN222.MVC/Pages/_ViewImports.cshtml`
- `src/PRN222.MVC/Pages/_ViewStart.cshtml`
- `src/PRN222.MVC/Pages/Index.cshtml`
- `src/PRN222.MVC/Pages/Index.cshtml.cs`
- `src/PRN222.MVC/Pages/Privacy.cshtml`
- `src/PRN222.MVC/Pages/Error.cshtml`
- `src/PRN222.MVC/Pages/Error.cshtml.cs`
- `src/PRN222.MVC/Pages/Account/Login.cshtml`
- `src/PRN222.MVC/Pages/Account/Login.cshtml.cs`
- `src/PRN222.MVC/Pages/Account/Register.cshtml`
- `src/PRN222.MVC/Pages/Account/Register.cshtml.cs`
- `src/PRN222.MVC/Pages/Account/AccessDenied.cshtml`
- `src/PRN222.MVC/Pages/Account/Logout.cshtml`
- `src/PRN222.MVC/Pages/Account/Logout.cshtml.cs`
- `src/PRN222.MVC/Pages/Course/Index.cshtml`
- `src/PRN222.MVC/Pages/Course/Index.cshtml.cs`
- `src/PRN222.MVC/Pages/Course/Create.cshtml`
- `src/PRN222.MVC/Pages/Course/Create.cshtml.cs`
- `src/PRN222.MVC/Pages/Course/Edit.cshtml`
- `src/PRN222.MVC/Pages/Course/Edit.cshtml.cs`
- `src/PRN222.MVC/Pages/Course/_CourseGrid.cshtml`
- `src/PRN222.MVC/Pages/Admin/Accounts.cshtml`
- `src/PRN222.MVC/Pages/Admin/Accounts.cshtml.cs`
- `src/PRN222.MVC/Pages/Department/Index.cshtml`
- `src/PRN222.MVC/Pages/Department/Index.cshtml.cs`
- `src/PRN222.MVC/Pages/Document/Index.cshtml`
- `src/PRN222.MVC/Pages/Document/Index.cshtml.cs`
- `src/PRN222.MVC/Pages/Document/Details.cshtml`
- `src/PRN222.MVC/Pages/Document/Details.cshtml.cs`
- `src/PRN222.MVC/Pages/Document/Upload.cshtml`
- `src/PRN222.MVC/Pages/Document/Upload.cshtml.cs`
- `src/PRN222.MVC/Pages/Chat/Index.cshtml`
- `src/PRN222.MVC/Pages/Chat/Index.cshtml.cs`
- `src/PRN222.MVC/Pages/Chat/Session.cshtml`
- `src/PRN222.MVC/Pages/Chat/Session.cshtml.cs`
- `src/PRN222.MVC/Pages/Evaluation/Index.cshtml`
- `src/PRN222.MVC/Pages/Evaluation/Index.cshtml.cs`
- `src/PRN222.MVC/Pages/Evaluation/RunDetail.cshtml`
- `src/PRN222.MVC/Pages/Evaluation/RunDetail.cshtml.cs`
- `src/PRN222.MVC/Pages/Evaluation/Compare.cshtml`
- `src/PRN222.MVC/Pages/Evaluation/Compare.cshtml.cs`
- `src/PRN222.MVC/Pages/Finetune/Index.cshtml`
- `src/PRN222.MVC/Pages/Finetune/Index.cshtml.cs`
- `src/PRN222.MVC/Pages/Finetune/Dataset.cshtml`
- `src/PRN222.MVC/Pages/Finetune/Dataset.cshtml.cs`
- `src/PRN222.MVC/Pages/TestSetGenerator/Index.cshtml`
- `src/PRN222.MVC/Pages/TestSetGenerator/Index.cshtml.cs`
- `src/PRN222.MVC/Pages/Knowledge/Propose.cshtml.cs`
- `src/PRN222.MVC/Pages/CodeRunner/Execute.cshtml.cs`

Modify:

- `src/PRN222.MVC/Program.cs`
- `src/PRN222.MVC/Infrastructure/ServiceCollectionExtensions.cs`
- `src/PRN222.MVC/Views/Shared/_Layout.cshtml` copied to `src/PRN222.MVC/Pages/Shared/_Layout.cshtml` while MVC remains active
- `src/PRN222.MVC/wwwroot/js/*.js` route references from controller actions to Razor Page handler URLs
- `src/PRN222.Tests/MVC/*.cs` controller tests to Razor Pages route and registration tests

Delete after replacement and verification:

- `src/PRN222.MVC/Controllers`
- `src/PRN222.MVC/Views`

## Task 1: Razor Pages Registration

**Files:**
- Modify: `src/PRN222.MVC/Infrastructure/ServiceCollectionExtensions.cs`
- Modify: `src/PRN222.MVC/Program.cs`
- Create: `src/PRN222.Tests/MVC/RazorPagesConfigurationTests.cs`

- [x] **Step 1: Write failing tests**

Create `src/PRN222.Tests/MVC/RazorPagesConfigurationTests.cs`:

```csharp
using FluentAssertions;

namespace PRN222.Tests.MVC;

public class RazorPagesConfigurationTests
{
    [Fact]
    public void Program_ShouldMapRazorPagesAndKeepDefaultControllerRouteDuringMigration()
    {
        var program = ReadRepositoryFile("src", "PRN222.MVC", "Program.cs");

        program.Should().Contain("app.MapRazorPages()");
        program.Should().Contain("MapControllerRoute");
    }

    [Fact]
    public void ServiceRegistration_ShouldUseRazorPagesAndControllersWithViewsDuringMigration()
    {
        var services = ReadRepositoryFile("src", "PRN222.MVC", "Infrastructure", "ServiceCollectionExtensions.cs");

        services.Should().Contain("AddRazorPages()");
        services.Should().Contain("AddControllersWithViews()");
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
```

- [x] **Step 2: Run the tests and verify failure**

Run:

```powershell
dotnet test src/PRN222_Assignment1.sln --no-restore --filter RazorPagesConfigurationTests
```

Expected: both tests fail until Razor Pages are registered and mapped alongside MVC.

- [x] **Step 3: Update service registration and routing**

In `ServiceCollectionExtensions.cs`, keep MVC registration and add Razor Pages registration:

```csharp
services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
services.AddRazorPages();
```

In `Program.cs`, add Razor Pages mapping while keeping the default controller route:

```csharp
app.MapRazorPages();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
```

Keep:

```csharp
app.MapHub<AdminHub>("/hubs/admin");
```

- [x] **Step 4: Run tests**

Run:

```powershell
dotnet test src/PRN222_Assignment1.sln --no-restore --filter RazorPagesConfigurationTests
```

Expected: pass.

- [x] **Step 5: Commit**

```powershell
git add src/PRN222.MVC/Infrastructure/ServiceCollectionExtensions.cs src/PRN222.MVC/Program.cs src/PRN222.Tests/MVC/RazorPagesConfigurationTests.cs
git commit -m "test: require razor pages routing"
```

MVC registration/routing remains during Tasks 1-6 and is removed only in Task 7 after Razor Pages equivalents exist.

## Task 2: Shared Razor Page Infrastructure

**Files:**
- Create: `src/PRN222.MVC/Pages/_ViewImports.cshtml`
- Create: `src/PRN222.MVC/Pages/_ViewStart.cshtml`
- Copy: `src/PRN222.MVC/Views/Shared` to `src/PRN222.MVC/Pages/Shared`
- Modify: `src/PRN222.MVC/Pages/Shared/_Layout.cshtml` only if Razor Pages compilation requires scoped copy-only changes
- Test: `src/PRN222.Tests/MVC/RazorPagesConfigurationTests.cs`

- [x] **Step 1: Add failing test for shared page infrastructure**

Append this test to `RazorPagesConfigurationTests`:

```csharp
[Fact]
public void RazorPages_ShouldHaveSharedImportsStartAndLayout()
{
    var root = FindRepositoryRoot();

    File.Exists(Path.Combine(root, "src", "PRN222.MVC", "Pages", "_ViewImports.cshtml")).Should().BeTrue();
    File.Exists(Path.Combine(root, "src", "PRN222.MVC", "Pages", "_ViewStart.cshtml")).Should().BeTrue();
    File.Exists(Path.Combine(root, "src", "PRN222.MVC", "Pages", "Shared", "_Layout.cshtml")).Should().BeTrue();
}
```

Run:

```powershell
dotnet test src/PRN222_Assignment1.sln --no-restore --filter RazorPages_ShouldHaveSharedImportsStartAndLayout
```

Expected: fail because `Pages` infrastructure is missing.

- [x] **Step 2: Create Razor Pages imports**

Create `src/PRN222.MVC/Pages/_ViewImports.cshtml`:

```cshtml
@using PRN222.MVC
@using PRN222.MVC.Models
@using PRN222.MVC.Models.Admin
@using PRN222.MVC.Models.Auth
@using PRN222.MVC.Models.Course
@using PRN222.MVC.Models.Dashboard
@using PRN222.MVC.Models.Department
@using PRN222.MVC.Models.Evaluation
@using PRN222.MVC.Infrastructure
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
```

Create `src/PRN222.MVC/Pages/_ViewStart.cshtml`:

```cshtml
@{
    Layout = "_Layout";
}
```

- [x] **Step 3: Copy shared files**

Copy the files from `src/PRN222.MVC/Views/Shared` into `src/PRN222.MVC/Pages/Shared`. Do not delete the originals during Tasks 1-6.

Keep copied shared files compatible with the active MVC surface during the hybrid migration. Only make scoped changes under `Pages/Shared` if Razor Pages compilation requires them.

- [x] **Step 4: Run tests**

Run:

```powershell
dotnet test src/PRN222_Assignment1.sln --no-restore --filter RazorPages_ShouldHaveSharedImportsStartAndLayout
```

Expected: pass.

- [x] **Step 5: Commit**

```powershell
git add src/PRN222.MVC/Pages src/PRN222.Tests/MVC/RazorPagesConfigurationTests.cs
git commit -m "feat: add shared razor pages shell"
```

## Task 3: Account Pages

**Files:**
- Create: `src/PRN222.MVC/Pages/Account/Login.cshtml`
- Create: `src/PRN222.MVC/Pages/Account/Login.cshtml.cs`
- Create: `src/PRN222.MVC/Pages/Account/Register.cshtml`
- Create: `src/PRN222.MVC/Pages/Account/Register.cshtml.cs`
- Create: `src/PRN222.MVC/Pages/Account/AccessDenied.cshtml`
- Create: `src/PRN222.MVC/Pages/Account/Logout.cshtml`
- Create: `src/PRN222.MVC/Pages/Account/Logout.cshtml.cs`
- Modify: `src/PRN222.MVC/Infrastructure/ServiceCollectionExtensions.cs`
- Test: `src/PRN222.Tests/MVC/AccountRazorPagesTests.cs`

- [x] **Step 1: Write failing tests**

Create `src/PRN222.Tests/MVC/AccountRazorPagesTests.cs`:

```csharp
using FluentAssertions;
using PRN222.MVC.Pages.Account;

namespace PRN222.Tests.MVC;

public class AccountRazorPagesTests
{
    [Fact]
    public void AccountPages_ShouldExist()
    {
        var root = FindRepositoryRoot();

        File.Exists(Path.Combine(root, "src", "PRN222.MVC", "Pages", "Account", "Login.cshtml")).Should().BeTrue();
        File.Exists(Path.Combine(root, "src", "PRN222.MVC", "Pages", "Account", "Register.cshtml")).Should().BeTrue();
        File.Exists(Path.Combine(root, "src", "PRN222.MVC", "Pages", "Account", "AccessDenied.cshtml")).Should().BeTrue();
    }

    [Fact]
    public void LoginPage_ShouldExposeReturnUrl()
    {
        typeof(LoginModel).GetProperty("ReturnUrl").Should().NotBeNull();
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
```

Run:

```powershell
dotnet test src/PRN222_Assignment1.sln --no-restore --filter AccountRazorPagesTests
```

Expected: fail because Account page models do not exist.

- [x] **Step 2: Convert Login**

Copy the markup from `Views/Account/Login.cshtml` to `Pages/Account/Login.cshtml`.

Set the page directive:

```cshtml
@page "/Account/Login"
@model PRN222.MVC.Pages.Account.LoginModel
```

Create `Login.cshtml.cs`:

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.DAL.Entities;
using PRN222.MVC.Infrastructure;
using PRN222.MVC.Models.Auth;

namespace PRN222.MVC.Pages.Account;

public class LoginModel(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty]
    public LoginViewModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        Input.ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ReturnUrl = Input.ReturnUrl;
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Email hoáº·c máº­t kháº©u khÃ´ng Ä‘Ãºng.");
            return Page();
        }

        var user = await userManager.FindByEmailAsync(Input.Email);
        return await RedirectAfterSignInAsync(user, Input.ReturnUrl);
    }

    private async Task<IActionResult> RedirectAfterSignInAsync(ApplicationUser? user, string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        if (user is not null && await userManager.IsInRoleAsync(user, ApplicationRoles.Admin))
        {
            return RedirectToPage("/Admin/Accounts");
        }

        if (user is not null && await userManager.IsInRoleAsync(user, ApplicationRoles.HeadLecturer))
        {
            return RedirectToPage("/Index");
        }

        if (user is not null && await userManager.IsInRoleAsync(user, ApplicationRoles.Lecturer))
        {
            return RedirectToPage("/Index");
        }

        return RedirectToPage("/Chat/Index");
    }
}
```

- [x] **Step 3: Convert Register, AccessDenied, Logout**

Use the same controller logic from `AccountController.Register`, `AccountController.AccessDenied`, and `AccountController.Logout`.

Configure cookie paths in `ServiceCollectionExtensions.cs`:

```csharp
options.LoginPath = "/Account/Login";
options.LogoutPath = "/Account/Logout";
options.AccessDeniedPath = "/Account/AccessDenied";
```

Create `Logout.cshtml` as a handler-only Razor Page:

```cshtml
@page "/Account/Logout"
@model PRN222.MVC.Pages.Account.LogoutModel
```

Create `Logout.cshtml.cs` with:

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.DAL.Entities;

namespace PRN222.MVC.Pages.Account;

public class LogoutModel(SignInManager<ApplicationUser> signInManager) : PageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        await signInManager.SignOutAsync();
        return RedirectToPage("/Account/Login");
    }
}
```

- [x] **Step 4: Run tests**

Run:

```powershell
dotnet test src/PRN222_Assignment1.sln --no-restore --filter AccountRazorPagesTests
```

Expected: pass.

- [x] **Step 5: Commit**

```powershell
git add src/PRN222.MVC/Pages/Account src/PRN222.MVC/Infrastructure/ServiceCollectionExtensions.cs src/PRN222.Tests/MVC/AccountRazorPagesTests.cs
git commit -m "feat: migrate account flow to razor pages"
```

## Task 4: Dashboard, Course, Admin, and Department Pages

**Files:**
- Create: `src/PRN222.MVC/Pages/Index.cshtml`
- Create: `src/PRN222.MVC/Pages/Index.cshtml.cs`
- Create: `src/PRN222.MVC/Pages/Course/*.cshtml`
- Create: `src/PRN222.MVC/Pages/Course/*.cshtml.cs`
- Create: `src/PRN222.MVC/Pages/Admin/Accounts.cshtml`
- Create: `src/PRN222.MVC/Pages/Admin/Accounts.cshtml.cs`
- Create: `src/PRN222.MVC/Pages/Department/Index.cshtml`
- Create: `src/PRN222.MVC/Pages/Department/Index.cshtml.cs`
- Modify: `src/PRN222.MVC/wwwroot/js/admin-realtime.js`
- Test: `src/PRN222.Tests/MVC/ManagementRazorPagesTests.cs`

- [x] **Step 1: Write failing tests**

Create tests that assert each target page exists and that the old controller files are not required by routing:

```csharp
using FluentAssertions;

namespace PRN222.Tests.MVC;

public class ManagementRazorPagesTests
{
    [Theory]
    [InlineData("Index.cshtml")]
    [InlineData("Course/Index.cshtml")]
    [InlineData("Course/Create.cshtml")]
    [InlineData("Course/Edit.cshtml")]
    [InlineData("Admin/Accounts.cshtml")]
    [InlineData("Department/Index.cshtml")]
    public void ManagementPages_ShouldExist(string page)
    {
        var root = FindRepositoryRoot();
        File.Exists(Path.Combine(root, "src", "PRN222.MVC", "Pages", page.Replace('/', Path.DirectorySeparatorChar))).Should().BeTrue();
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
```

- [x] **Step 2: Convert `HomeController.Index`**

Move `Views/Home/Index.cshtml` to `Pages/Index.cshtml`.

Use:

```cshtml
@page "/"
@model PRN222.MVC.Pages.IndexModel
```

Create `Index.cshtml.cs` with `[Authorize(Roles = ApplicationRoles.Management)]` and the same dashboard loading logic from `HomeController.Index`.

- [x] **Step 3: Convert Course pages**

Move:

- `Views/Course/Index.cshtml` to `Pages/Course/Index.cshtml` with `@page "/Course"`
- `Views/Course/Create.cshtml` to `Pages/Course/Create.cshtml` with `@page "/Course/Create"`
- `Views/Course/Edit.cshtml` to `Pages/Course/Edit.cshtml` with `@page "/Course/Edit/{id:int}"`
- `Views/Course/_CourseGrid.cshtml` to `Pages/Course/_CourseGrid.cshtml`

Convert controller actions:

- `Index` to `IndexModel.OnGetAsync`
- `AssignedCoursesPartial` to `IndexModel.OnGetAssignedCoursesPartialAsync`
- `Create` GET/POST to `CreateModel.OnGetAsync` and `CreateModel.OnPostAsync`
- `Edit` GET/POST to `EditModel.OnGetAsync` and `EditModel.OnPostAsync`
- `Delete` to `IndexModel.OnPostDeleteAsync`
- `CreateAjax` to `IndexModel.OnPostCreateAjaxAsync`

Update JavaScript URLs:

```javascript
fetch('/Course?handler=CreateAjax', ...)
fetch('/Course?handler=AssignedCoursesPartial', ...)
```

Keep the encoding-safe realtime strings:

```html
<h3>Ch\u01b0a c\u00f3 m\u00f4n h\u1ecdc n\u00e0o</h3>
<p>B\u1ea1n ch\u01b0a \u0111\u01b0\u1ee3c Admin ph\u00e2n c\u00f4ng m\u00f4n h\u1ecdc.</p>
```

- [x] **Step 4: Convert Admin and Department pages**

Move `Views/Admin/Accounts.cshtml` to `Pages/Admin/Accounts.cshtml` with:

```cshtml
@page "/Admin/Accounts"
@model PRN222.MVC.Pages.Admin.AccountsModel
```

Convert:

- `AdminController.Accounts` to `AccountsModel.OnGetAsync`
- `AdminController.CreateAccount` to `AccountsModel.OnPostCreateAccountAsync`
- `AdminController.UpdateAssignments` to `AccountsModel.OnPostUpdateAssignmentsAsync`

Move `Views/Department/Index.cshtml` to `Pages/Department/Index.cshtml` with:

```cshtml
@page "/Department"
@model PRN222.MVC.Pages.Department.IndexModel
```

Convert:

- `DepartmentController.Index` to `OnGetAsync`
- `Create` to `OnPostCreateAsync`
- `AssignUser` to `OnPostAssignUserAsync`
- `AssignCourse` to `OnPostAssignCourseAsync`
- `RemoveUser` to `OnPostRemoveUserAsync`

- [x] **Step 5: Run tests and commit**

Run:

```powershell
dotnet test src/PRN222_Assignment1.sln --no-restore --filter ManagementRazorPagesTests
```

Expected: pass.

Commit:

```powershell
git add src/PRN222.MVC/Pages src/PRN222.MVC/wwwroot/js src/PRN222.Tests/MVC/ManagementRazorPagesTests.cs
git commit -m "feat: migrate management pages to razor pages"
```

## Task 5: Document and Chat Pages

**Files:**
- Create: `src/PRN222.MVC/Pages/Document/*.cshtml`
- Create: `src/PRN222.MVC/Pages/Document/*.cshtml.cs`
- Create: `src/PRN222.MVC/Pages/Chat/*.cshtml`
- Create: `src/PRN222.MVC/Pages/Chat/*.cshtml.cs`
- Move: `src/PRN222.MVC/Views/Chat/_*.cshtml` to `src/PRN222.MVC/Pages/Chat`
- Test: `src/PRN222.Tests/MVC/RagRazorPagesTests.cs`

- [x] **Step 1: Write failing page existence tests**

Test these files:

- `Pages/Document/Index.cshtml`
- `Pages/Document/Details.cshtml`
- `Pages/Document/Upload.cshtml`
- `Pages/Chat/Index.cshtml`
- `Pages/Chat/Session.cshtml`

- [x] **Step 2: Convert Document**

Routes:

```cshtml
@page "/Document"
@page "/Document/Details/{id:int}"
@page "/Document/Upload"
```

Handler mapping:

- `DocumentController.Index` to `Document/Index.OnGetAsync`
- `Details` to `Document/Details.OnGetAsync`
- `Upload` GET/POST to `Document/Upload.OnGetAsync` and `OnPostAsync`
- `Process` to `Document/Details.OnPostProcessAsync`
- `Delete` to `Document/Details.OnPostDeleteAsync`
- `GetStatus` to `Document/Details.OnGetStatusAsync`

Update polling JavaScript from `/Document/GetStatus/${id}` to:

```javascript
fetch(`/Document/Details/${id}?handler=Status`)
```

- [x] **Step 3: Convert Chat**

Routes:

```cshtml
@page "/Chat"
@page "/Chat/Session/{id:int?}"
```

Handler mapping:

- `ChatController.Index` to `Chat/Index.OnGetAsync`
- `Session` to `Chat/Session.OnGetAsync`
- `New` to `Chat/Session.OnGetNewAsync` or redirect to `/Chat/Session`
- `Ask` to `Chat/Session.OnPostAskAsync`
- `GetCourseDocuments` to `Chat/Session.OnGetCourseDocumentsAsync`
- `CitationImage` to `Chat/Session.OnGetCitationImageAsync`
- `Feedback` to `Chat/Session.OnPostFeedbackAsync`

Update JavaScript URLs:

```javascript
fetch('/Chat/Session?handler=Ask', ...)
fetch(`/Chat/Session?handler=CourseDocuments&courseId=${courseId}`)
fetch(`/Chat/Session?handler=CitationImage&chunkId=${chunkId}`)
fetch('/Chat/Session?handler=Feedback', ...)
```

- [x] **Step 4: Run tests and commit**

```powershell
dotnet test src/PRN222_Assignment1.sln --no-restore --filter RagRazorPagesTests
git add src/PRN222.MVC/Pages src/PRN222.MVC/wwwroot/js src/PRN222.Tests/MVC/RagRazorPagesTests.cs
git commit -m "feat: migrate document and chat pages"
```

## Task 6: Evaluation, Finetune, Test Set, Knowledge, and Code Runner

**Files:**
- Create: `src/PRN222.MVC/Pages/Evaluation/*.cshtml`
- Create: `src/PRN222.MVC/Pages/Evaluation/*.cshtml.cs`
- Create: `src/PRN222.MVC/Pages/Finetune/*.cshtml`
- Create: `src/PRN222.MVC/Pages/Finetune/*.cshtml.cs`
- Create: `src/PRN222.MVC/Pages/TestSetGenerator/Index.cshtml`
- Create: `src/PRN222.MVC/Pages/TestSetGenerator/Index.cshtml.cs`
- Create: `src/PRN222.MVC/Pages/Knowledge/Index.cshtml.cs`
- Create: `src/PRN222.MVC/Pages/CodeRunner/Index.cshtml.cs`
- Test: `src/PRN222.Tests/MVC/ModelOperationsRazorPagesTests.cs`

- [x] **Step 1: Write failing tests for page existence and handler names**

Assert these pages exist:

- `Pages/Evaluation/Index.cshtml`
- `Pages/Evaluation/RunDetail.cshtml`
- `Pages/Evaluation/Compare.cshtml`
- `Pages/Finetune/Index.cshtml`
- `Pages/Finetune/Dataset.cshtml`
- `Pages/TestSetGenerator/Index.cshtml`

- [x] **Step 2: Convert Evaluation**

Routes:

```cshtml
@page "/Evaluation"
@page "/Evaluation/RunDetail/{runId:int}"
@page "/Evaluation/Compare"
```

Handlers:

- `GetRunStatuses` to `Evaluation/Index.OnGetRunStatusesAsync`
- `CreateRun` to `Evaluation/Index.OnPostCreateRunAsync`
- `ExportCsv` to `Evaluation/RunDetail.OnGetExportCsvAsync`

- [x] **Step 3: Convert Finetune**

Routes:

```cshtml
@page "/Finetune"
@page "/Finetune/Dataset"
```

Handlers:

- `GenerateAjax` to `Dataset.OnPostGenerateAjaxAsync`
- `Export` to `Dataset.OnGetExportAsync`

- [x] **Step 4: Convert TestSetGenerator**

Route:

```cshtml
@page "/TestSetGenerator"
```

Handlers:

- `Generate` to `OnPostGenerateAsync`
- `Stop` to `OnPostStopAsync`
- `ClearAutoGenerated` to `OnPostClearAutoGeneratedAsync`
- `Stats` to `OnGetStatsAsync`
- `JobStatus` to `OnGetJobStatusAsync`
- `Preview` to `OnGetPreviewAsync`

- [x] **Step 5: Convert Knowledge and CodeRunner API-style endpoints**

Create handler-only Razor Pages:

`Pages/Knowledge/Index.cshtml`:

```cshtml
@page "/Knowledge"
@model PRN222.MVC.Pages.Knowledge.IndexModel
```

Map:

- `Propose` to `OnPostProposeAsync`
- `Approve` to `OnPostApproveAsync`
- `Reject` to `OnPostRejectAsync`
- `Rollback` to `OnPostRollbackAsync`

Create `Pages/CodeRunner/Index.cshtml`:

```cshtml
@page "/CodeRunner"
@model PRN222.MVC.Pages.CodeRunner.IndexModel
```

Map:

- `Execute` to `OnPostExecuteAsync`

- [x] **Step 6: Run tests and commit**

```powershell
dotnet test src/PRN222_Assignment1.sln --no-restore --filter ModelOperationsRazorPagesTests
git add src/PRN222.MVC/Pages src/PRN222.MVC/wwwroot/js src/PRN222.Tests/MVC/ModelOperationsRazorPagesTests.cs
git commit -m "feat: migrate model operation pages"
```

## Task 7: Remove MVC Surface

**Files:**
- Delete: `src/PRN222.MVC/Controllers`
- Delete: `src/PRN222.MVC/Views`
- Modify: `src/PRN222.MVC/Infrastructure/ServiceCollectionExtensions.cs`
- Modify: `src/PRN222.MVC/Program.cs`
- Modify: existing tests that import controller classes
- Test: `src/PRN222.Tests/MVC/RazorPagesConfigurationTests.cs`

- [x] **Step 1: Add failing test that MVC surface is gone**

Add to `RazorPagesConfigurationTests`:

```csharp
[Fact]
public void Project_ShouldNotContainMvcControllersOrViews()
{
    var root = FindRepositoryRoot();

    Directory.Exists(Path.Combine(root, "src", "PRN222.MVC", "Controllers")).Should().BeFalse();
    Directory.Exists(Path.Combine(root, "src", "PRN222.MVC", "Views")).Should().BeFalse();
}
```

Also update the registration/routing tests in this task so they assert MVC registration and conventional routing are gone:

```csharp
program.Should().Contain("app.MapRazorPages()");
program.Should().NotContain("MapControllerRoute");

services.Should().Contain("AddRazorPages()");
services.Should().NotContain("AddControllersWithViews()");
```

- [x] **Step 2: Remove MVC registration and routing**

Remove `AddControllersWithViews()` from `ServiceCollectionExtensions.cs` after all Razor Pages equivalents exist.

Remove the default `MapControllerRoute` from `Program.cs`, leaving `app.MapRazorPages()` and `app.MapHub<AdminHub>("/hubs/admin")`.

- [x] **Step 3: Delete controllers and views**

Delete:

```powershell
Remove-Item -Recurse -Force src/PRN222.MVC/Controllers
Remove-Item -Recurse -Force src/PRN222.MVC/Views
```

- [x] **Step 4: Update tests**

Replace controller unit tests with PageModel tests or route tests. Do not delete behavioral coverage unless an equivalent Razor Pages test exists.

- [x] **Step 5: Run full tests**

```powershell
dotnet test src/PRN222_Assignment1.sln --no-restore
```

Expected: all tests pass.

- [x] **Step 6: Commit**

```powershell
git add -A
git commit -m "refactor: remove mvc controllers and views"
```

## Task 8: Browser Smoke Test

**Files:**
- No production files expected unless smoke test finds a bug.

- [x] **Step 1: Start the Razor Pages app on a non-conflicting port**

```powershell
dotnet run --project src/PRN222.MVC/PRN222.MVC.csproj --urls http://localhost:5273
```

- [x] **Step 2: Smoke test these pages**

Open:

- `http://localhost:5273/Account/Login`
- `http://localhost:5273/`
- `http://localhost:5273/Course`
- `http://localhost:5273/Admin/Accounts`
- `http://localhost:5273/Department`
- `http://localhost:5273/Document`
- `http://localhost:5273/Chat`
- `http://localhost:5273/TestSetGenerator`

Expected:

- No 404.
- Role redirects are correct.
- Sidebar links route to Razor Pages.
- SignalR connects to `/hubs/admin`.
- Course assignment realtime does not show mojibake text.

- [x] **Step 3: Run final verification**

```powershell
dotnet test src/PRN222_Assignment1.sln --no-restore
git status --short --branch
```

Expected:

- Tests pass.
- Branch is `codex/razor-pages-migration`.
- Working tree is clean after final commit.

---

## Completion Checkpoint

Completed on 2026-06-16 in worktree `C:\Users\ADMIN\Documents\FPT\PRN222_Assignment1\.worktrees\razor-pages-migration`.

Verification evidence:

- `dotnet test src/PRN222_Assignment1.sln --no-restore` passed: 220/220 tests.
- Browser/HTTP smoke on `http://localhost:5273` confirmed `/Account/Login` returns 200 and protected pages redirect to `/Account/Login` with `ReturnUrl`.
- Branch: `codex/razor-pages-migration`.
- MVC folders removed from the worktree: `src/PRN222.MVC/Controllers`, `src/PRN222.MVC/Views`.
