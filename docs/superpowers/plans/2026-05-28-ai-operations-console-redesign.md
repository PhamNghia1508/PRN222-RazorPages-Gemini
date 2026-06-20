# AI Operations Console Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Replace the current landing-page style UI with a professional AI Operations Console and RAG Lab dashboard while preserving the existing ASP.NET Core MVC backend.

**Architecture:** Keep the current MVC/BLL/DAL layering. Add small MVC view models for dashboard aggregation, reshape Razor views around a console shell, and consolidate dashboard styling in `site.css` without introducing a frontend framework.

**Tech Stack:** ASP.NET Core MVC Razor, Bootstrap 5, Bootstrap Icons, EF-backed existing services, xUnit/Moq/FluentAssertions, PowerShell, Microsoft Edge headless for visual screenshots.

---

## Scope Check

This plan implements the approved first UI pass only: console shell, Overview, Knowledge Base polish, Chat Playground layout, Evaluation screen, and CSS cleanup. It does not implement benchmark execution, new persistence, authentication, or a JavaScript frontend framework.

## File Structure

- Modify: `src/PRN222.MVC/PRN222.MVC.csproj`
  - Allow `PRN222.Tests` to reference MVC types by keeping MVC types public and testable.
- Modify: `src/PRN222.Tests/PRN222.Tests.csproj`
  - Add a project reference to `PRN222.MVC` so controller/view model tests compile.
- Create: `src/PRN222.MVC/Models/Dashboard/OverviewDashboardViewModel.cs`
  - Holds metric cards, pipeline steps, recent documents, and next actions.
- Create: `src/PRN222.MVC/Models/Dashboard/OverviewDashboardFactory.cs`
  - Builds the Overview model from existing `DocumentDto` and `CourseDto` lists.
- Create: `src/PRN222.Tests/MVC/OverviewDashboardFactoryTests.cs`
  - Verifies dashboard aggregation and next-action behavior.
- Modify: `src/PRN222.MVC/Controllers/HomeController.cs`
  - Injects document/course services and returns `OverviewDashboardViewModel`.
- Modify: `src/PRN222.MVC/Views/Shared/_Layout.cshtml`
  - Replaces top marketing nav with console shell navigation.
- Modify: `src/PRN222.MVC/Views/Home/Index.cshtml`
  - Replaces hero page with Overview dashboard.
- Modify: `src/PRN222.MVC/Controllers/DocumentController.cs`
  - Adds filter parameters for status, file type, and search.
- Modify: `src/PRN222.MVC/Views/Document/Index.cshtml`
  - Renames screen to Knowledge Base and adds compact toolbar/table polish.
- Modify: `src/PRN222.MVC/Views/Document/Upload.cshtml`
  - Frames upload as ingestion pipeline step.
- Modify: `src/PRN222.MVC/Views/Chat/Session.cshtml`
  - Adds Chat Playground layout and retrieved-context side panel.
- Create: `src/PRN222.MVC/Controllers/EvaluationController.cs`
  - Provides Evaluation route for the console nav.
- Create: `src/PRN222.MVC/Views/Evaluation/Index.cshtml`
  - Adds evaluation-ready screen with coming-soon action.
- Modify: `src/PRN222.MVC/wwwroot/css/site.css`
  - Adds dashboard shell, metric, pipeline, table, chat lab, evaluation, and responsive styles.

---

### Task 1: Dashboard View Model And Aggregation Tests

**Files:**
- Create: `src/PRN222.MVC/Models/Dashboard/OverviewDashboardViewModel.cs`
- Create: `src/PRN222.MVC/Models/Dashboard/OverviewDashboardFactory.cs`
- Modify: `src/PRN222.Tests/PRN222.Tests.csproj`
- Create: `src/PRN222.Tests/MVC/OverviewDashboardFactoryTests.cs`

- [x] **Step 1: Add MVC project reference to tests**

In `src/PRN222.Tests/PRN222.Tests.csproj`, add this project reference inside the existing project reference `ItemGroup`:

```xml
<ProjectReference Include="..\PRN222.MVC\PRN222.MVC.csproj" />
```

- [x] **Step 2: Write failing dashboard aggregation tests**

Create `src/PRN222.Tests/MVC/OverviewDashboardFactoryTests.cs`:

```csharp
using FluentAssertions;
using PRN222.BLL.DTOs;
using PRN222.MVC.Models.Dashboard;
using Xunit;

namespace PRN222.Tests.MVC;

public class OverviewDashboardFactoryTests
{
    [Fact]
    public void Build_ShouldCalculateMetricsAndPipelineReadiness()
    {
        var documents = new[]
        {
            new DocumentDto(1, "a.pdf", "A.pdf", "application/pdf", 1024, 4, "Indexed", "PRN222", 1, new DateTime(2026, 5, 28, 10, 0, 0)),
            new DocumentDto(2, "b.docx", "B.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", 2048, 0, "Uploaded", "PRN222", 1, new DateTime(2026, 5, 28, 11, 0, 0)),
            new DocumentDto(3, "c.pdf", "C.pdf", "application/pdf", 4096, 0, "Failed", "SWT301", 2, new DateTime(2026, 5, 28, 12, 0, 0))
        };
        var courses = new[]
        {
            new CourseDto(1, "PRN222", null, 2, new DateTime(2026, 5, 1)),
            new CourseDto(2, "SWT301", null, 1, new DateTime(2026, 5, 2))
        };

        var result = OverviewDashboardFactory.Build(documents, courses);

        result.TotalDocuments.Should().Be(3);
        result.TotalCourses.Should().Be(2);
        result.IndexedChunks.Should().Be(4);
        result.IndexedDocuments.Should().Be(1);
        result.FailedDocuments.Should().Be(1);
        result.RecentDocuments.Should().HaveCount(3);
        result.RecentDocuments.First().OriginalFileName.Should().Be("C.pdf");
        result.PipelineSteps.Should().ContainSingle(s => s.Key == "embed" && s.Status == "Ready");
        result.NextActions.Should().Contain(a => a.Label == "Review failed documents");
    }

    [Fact]
    public void Build_ShouldShowUploadAction_WhenKnowledgeBaseIsEmpty()
    {
        var result = OverviewDashboardFactory.Build(Array.Empty<DocumentDto>(), Array.Empty<CourseDto>());

        result.TotalDocuments.Should().Be(0);
        result.PipelineSteps.Should().ContainSingle(s => s.Key == "upload" && s.Status == "Needs input");
        result.NextActions.Should().ContainSingle(a => a.Label == "Upload first document");
    }
}
```

- [x] **Step 3: Run tests to verify they fail**

Run:

```powershell
dotnet test PRN222.Tests\PRN222.Tests.csproj --no-restore --nologo
```

Expected: compile failure because `PRN222.MVC.Models.Dashboard.OverviewDashboardFactory` and `OverviewDashboardViewModel` do not exist.

- [x] **Step 4: Add dashboard view models**

Create `src/PRN222.MVC/Models/Dashboard/OverviewDashboardViewModel.cs`:

```csharp
using PRN222.BLL.DTOs;

namespace PRN222.MVC.Models.Dashboard;

public class OverviewDashboardViewModel
{
    public int TotalDocuments { get; init; }
    public int IndexedDocuments { get; init; }
    public int FailedDocuments { get; init; }
    public int IndexedChunks { get; init; }
    public int TotalCourses { get; init; }
    public string EvaluationStatus { get; init; } = "Ready to configure";
    public IReadOnlyList<PipelineStepViewModel> PipelineSteps { get; init; } = Array.Empty<PipelineStepViewModel>();
    public IReadOnlyList<DocumentDto> RecentDocuments { get; init; } = Array.Empty<DocumentDto>();
    public IReadOnlyList<NextActionViewModel> NextActions { get; init; } = Array.Empty<NextActionViewModel>();
}

public record PipelineStepViewModel(
    string Key,
    string Label,
    string Description,
    string Status,
    string IconCssClass);

public record NextActionViewModel(
    string Label,
    string Description,
    string Controller,
    string Action,
    string IconCssClass,
    string CssClass);
```

- [x] **Step 5: Add dashboard factory**

Create `src/PRN222.MVC/Models/Dashboard/OverviewDashboardFactory.cs`:

```csharp
using PRN222.BLL.DTOs;

namespace PRN222.MVC.Models.Dashboard;

public static class OverviewDashboardFactory
{
    public static OverviewDashboardViewModel Build(IEnumerable<DocumentDto> documents, IEnumerable<CourseDto> courses)
    {
        var documentList = documents.ToList();
        var courseList = courses.ToList();
        var indexedCount = documentList.Count(d => d.Status == "Indexed");
        var failedCount = documentList.Count(d => d.Status == "Failed");
        var processingCount = documentList.Count(d => d.Status == "Processing");
        var uploadedCount = documentList.Count(d => d.Status == "Uploaded");
        var indexedChunks = documentList.Where(d => d.Status == "Indexed").Sum(d => d.ChunkCount);

        return new OverviewDashboardViewModel
        {
            TotalDocuments = documentList.Count,
            IndexedDocuments = indexedCount,
            FailedDocuments = failedCount,
            IndexedChunks = indexedChunks,
            TotalCourses = courseList.Count,
            PipelineSteps = BuildPipelineSteps(documentList.Count, uploadedCount, processingCount, indexedCount),
            RecentDocuments = documentList
                .OrderByDescending(d => d.CreatedAt)
                .Take(5)
                .ToList(),
            NextActions = BuildNextActions(documentList.Count, uploadedCount, failedCount, indexedCount)
        };
    }

    private static IReadOnlyList<PipelineStepViewModel> BuildPipelineSteps(
        int totalDocuments,
        int uploadedCount,
        int processingCount,
        int indexedCount)
    {
        return new[]
        {
            new PipelineStepViewModel("upload", "Upload", "Add course material", totalDocuments > 0 ? "Ready" : "Needs input", "bi bi-cloud-upload"),
            new PipelineStepViewModel("extract", "Extract", "Read PDF and DOCX text", totalDocuments > 0 ? "Ready" : "Waiting", "bi bi-file-text"),
            new PipelineStepViewModel("chunk", "Chunk", "Split knowledge into retrievable units", indexedCount > 0 ? "Ready" : uploadedCount > 0 ? "Queued" : "Waiting", "bi bi-grid-3x3-gap"),
            new PipelineStepViewModel("embed", "Embed", "Generate vectors for retrieval", indexedCount > 0 ? "Ready" : processingCount > 0 ? "Running" : "Waiting", "bi bi-cpu"),
            new PipelineStepViewModel("ask", "Ask", "Use indexed material in chat", indexedCount > 0 ? "Ready" : "Waiting", "bi bi-chat-dots"),
            new PipelineStepViewModel("evaluate", "Evaluate", "Compare answer quality", indexedCount > 0 ? "Ready to configure" : "Waiting", "bi bi-graph-up")
        };
    }

    private static IReadOnlyList<NextActionViewModel> BuildNextActions(
        int totalDocuments,
        int uploadedCount,
        int failedCount,
        int indexedCount)
    {
        var actions = new List<NextActionViewModel>();

        if (totalDocuments == 0)
        {
            actions.Add(new NextActionViewModel(
                "Upload first document",
                "Start the knowledge base with a PDF or DOCX file.",
                "Document",
                "Upload",
                "bi bi-cloud-upload",
                "btn-primary"));
            return actions;
        }

        if (uploadedCount > 0)
        {
            actions.Add(new NextActionViewModel(
                "Process uploaded documents",
                "Run extraction, chunking, and embedding for files waiting in the queue.",
                "Document",
                "Index",
                "bi bi-gear",
                "btn-primary"));
        }

        if (failedCount > 0)
        {
            actions.Add(new NextActionViewModel(
                "Review failed documents",
                "Open the knowledge base and inspect files that need attention.",
                "Document",
                "Index",
                "bi bi-exclamation-triangle",
                "btn-outline-danger"));
        }

        if (indexedCount > 0)
        {
            actions.Add(new NextActionViewModel(
                "Open Chat Playground",
                "Ask questions against indexed course material.",
                "Chat",
                "New",
                "bi bi-chat-dots",
                "btn-outline-primary"));
        }

        actions.Add(new NextActionViewModel(
            "Prepare evaluation",
            "Review the evaluation dashboard structure for RAG quality metrics.",
            "Evaluation",
            "Index",
            "bi bi-graph-up",
            "btn-outline-secondary"));

        return actions;
    }
}
```

- [x] **Step 6: Run tests to verify they pass**

Run:

```powershell
dotnet test PRN222.Tests\PRN222.Tests.csproj --no-restore --nologo
```

Expected: tests pass, including `OverviewDashboardFactoryTests`.

- [x] **Step 7: Commit**

```powershell
git add src\PRN222.MVC\Models\Dashboard src\PRN222.Tests\PRN222.Tests.csproj src\PRN222.Tests\MVC\OverviewDashboardFactoryTests.cs
git commit -m "test: add overview dashboard model coverage"
```

---

### Task 2: Console Shell Layout

**Files:**
- Modify: `src/PRN222.MVC/Views/Shared/_Layout.cshtml`
- Modify: `src/PRN222.MVC/wwwroot/css/site.css`

- [x] **Step 1: Replace shared layout with console shell**

In `src/PRN222.MVC/Views/Shared/_Layout.cshtml`, replace the `<body>` content with this structure while preserving the existing `<head>` links and script section:

```cshtml
<body>
    <div class="app-shell">
        <aside class="app-sidebar">
            <a class="app-brand" asp-controller="Home" asp-action="Index">
                <span class="app-brand-icon"><i class="bi bi-cpu"></i></span>
                <span>
                    <strong>RAG Workbench</strong>
                    <small>PRN222 Console</small>
                </span>
            </a>

            <nav class="app-nav" aria-label="Main navigation">
                <a class="app-nav-link" asp-controller="Home" asp-action="Index">
                    <i class="bi bi-speedometer2"></i><span>Overview</span>
                </a>
                <a class="app-nav-link" asp-controller="Document" asp-action="Index">
                    <i class="bi bi-database"></i><span>Knowledge Base</span>
                </a>
                <a class="app-nav-link" asp-controller="Chat" asp-action="New">
                    <i class="bi bi-chat-dots"></i><span>Chat Playground</span>
                </a>
                <a class="app-nav-link" asp-controller="Evaluation" asp-action="Index">
                    <i class="bi bi-graph-up"></i><span>Evaluation</span>
                </a>
                <a class="app-nav-link" asp-controller="Course" asp-action="Index">
                    <i class="bi bi-journal-bookmark"></i><span>Courses</span>
                </a>
            </nav>
        </aside>

        <div class="app-main">
            <header class="app-topbar">
                <div>
                    <span class="topbar-eyebrow">Vietnamese RAG vs Finetuning</span>
                    <strong>AI Operations Console</strong>
                </div>
                <div class="topbar-actions">
                    <span class="system-status"><span></span> Local environment</span>
                    <a asp-controller="Document" asp-action="Upload" class="btn btn-primary btn-sm">
                        <i class="bi bi-cloud-upload"></i> Upload document
                    </a>
                </div>
            </header>

            <main class="app-content" role="main">
                @RenderBody()
            </main>
        </div>
    </div>

    <script src="~/lib/jquery/dist/jquery.min.js"></script>
    <script src="~/lib/bootstrap/dist/js/bootstrap.bundle.min.js"></script>
    <script src="~/js/site.js" asp-append-version="true"></script>
    @await RenderSectionAsync("Scripts", required: false)
</body>
```

- [x] **Step 2: Add console shell CSS**

Append this section near the top of `src/PRN222.MVC/wwwroot/css/site.css`, after `:root` and base typography:

```css
.app-shell {
  min-height: 100vh;
  display: grid;
  grid-template-columns: 260px minmax(0, 1fr);
  background: var(--sand-100);
}

.app-sidebar {
  position: sticky;
  top: 0;
  height: 100vh;
  padding: 20px 16px;
  background: var(--sand-50);
  border-right: 1px solid var(--sand-300);
}

.app-brand {
  display: flex;
  gap: 12px;
  align-items: center;
  color: var(--ink-900);
  padding: 8px 10px 20px;
}

.app-brand:hover {
  color: var(--ink-900);
}

.app-brand-icon {
  width: 36px;
  height: 36px;
  border-radius: 8px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  color: var(--sand-50);
  background: var(--ink-800);
}

.app-brand small {
  display: block;
  font-size: 0.75rem;
  color: var(--ink-600);
  font-family: var(--font-body);
}

.app-nav {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.app-nav-link {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 12px;
  color: var(--ink-600);
  border-radius: 8px;
  font-weight: 600;
}

.app-nav-link:hover {
  color: var(--ink-900);
  background: var(--sand-200);
}

.app-main {
  min-width: 0;
  display: flex;
  flex-direction: column;
}

.app-topbar {
  height: 64px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 0 28px;
  background: rgba(253, 250, 245, 0.92);
  border-bottom: 1px solid var(--sand-300);
  position: sticky;
  top: 0;
  z-index: 20;
  backdrop-filter: blur(12px);
}

.topbar-eyebrow {
  display: block;
  font-size: 0.72rem;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: var(--ink-600);
  font-weight: 700;
}

.topbar-actions {
  display: flex;
  align-items: center;
  gap: 12px;
}

.system-status {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  color: var(--ink-600);
  font-size: 0.85rem;
}

.system-status span {
  width: 8px;
  height: 8px;
  border-radius: 999px;
  background: var(--status-success-text);
}

.app-content {
  width: min(1280px, calc(100vw - 260px));
  margin: 0 auto;
  padding: 28px;
}
```

- [x] **Step 3: Add responsive shell CSS**

Append below the shell CSS:

```css
@media (max-width: 900px) {
  .app-shell {
    grid-template-columns: 1fr;
  }

  .app-sidebar {
    position: static;
    height: auto;
    border-right: none;
    border-bottom: 1px solid var(--sand-300);
    padding: 12px;
  }

  .app-brand {
    padding-bottom: 12px;
  }

  .app-nav {
    flex-direction: row;
    overflow-x: auto;
    padding-bottom: 4px;
  }

  .app-nav-link {
    white-space: nowrap;
    flex: 0 0 auto;
  }

  .app-topbar {
    position: static;
    height: auto;
    align-items: flex-start;
    flex-direction: column;
    padding: 16px;
  }

  .topbar-actions {
    width: 100%;
    justify-content: space-between;
  }

  .app-content {
    width: 100%;
    padding: 18px 14px;
  }
}
```

- [x] **Step 4: Build MVC**

Run:

```powershell
dotnet build PRN222.MVC\PRN222.MVC.csproj --nologo
```

Expected: build succeeds.

- [x] **Step 5: Commit**

```powershell
git add src\PRN222.MVC\Views\Shared\_Layout.cshtml src\PRN222.MVC\wwwroot\css\site.css
git commit -m "feat: introduce AI operations console shell"
```

---

### Task 3: Overview Dashboard

**Files:**
- Modify: `src/PRN222.MVC/Controllers/HomeController.cs`
- Modify: `src/PRN222.MVC/Views/Home/Index.cshtml`
- Modify: `src/PRN222.MVC/wwwroot/css/site.css`

- [x] **Step 1: Write failing HomeController test**

Create `src/PRN222.Tests/MVC/HomeControllerTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services.Interfaces;
using PRN222.MVC.Controllers;
using PRN222.MVC.Models.Dashboard;
using Xunit;

namespace PRN222.Tests.MVC;

public class HomeControllerTests
{
    [Fact]
    public async Task Index_ShouldReturnOverviewDashboardViewModel()
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
        documentService.Setup(s => s.GetAllDocumentsAsync()).ReturnsAsync(documents);
        courseService.Setup(s => s.GetAllCoursesAsync()).ReturnsAsync(courses);

        var controller = new HomeController(
            new Mock<ILogger<HomeController>>().Object,
            documentService.Object,
            courseService.Object);

        var result = await controller.Index();

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<OverviewDashboardViewModel>().Subject;
        model.TotalDocuments.Should().Be(1);
        model.IndexedChunks.Should().Be(8);
    }
}
```

- [x] **Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test PRN222.Tests\PRN222.Tests.csproj --no-restore --nologo
```

Expected: compile failure because `HomeController` does not have the constructor and async `Index` action used by the test.

- [x] **Step 3: Update HomeController**

Replace `src/PRN222.MVC/Controllers/HomeController.cs` with:

```csharp
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PRN222.BLL.Services.Interfaces;
using PRN222.MVC.Models;
using PRN222.MVC.Models.Dashboard;

namespace PRN222.MVC.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IDocumentService _documentService;
    private readonly ICourseService _courseService;

    public HomeController(
        ILogger<HomeController> logger,
        IDocumentService documentService,
        ICourseService courseService)
    {
        _logger = logger;
        _documentService = documentService;
        _courseService = courseService;
    }

    public async Task<IActionResult> Index()
    {
        var documents = await _documentService.GetAllDocumentsAsync();
        var courses = await _courseService.GetAllCoursesAsync();
        var model = OverviewDashboardFactory.Build(documents, courses);
        return View(model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
```

- [x] **Step 4: Replace Home view**

Replace `src/PRN222.MVC/Views/Home/Index.cshtml` with:

```cshtml
@model PRN222.MVC.Models.Dashboard.OverviewDashboardViewModel
@{
    ViewData["Title"] = "Overview";
}

<section class="page-heading">
    <div>
        <span class="section-eyebrow">Overview</span>
        <h1>Vietnamese RAG Workbench</h1>
        <p>Monitor document readiness, retrieval coverage, chat quality, and evaluation flow from one console.</p>
    </div>
    <div class="page-heading-actions">
        <a asp-controller="Document" asp-action="Upload" class="btn btn-primary">
            <i class="bi bi-cloud-upload"></i> Upload document
        </a>
        <a asp-controller="Chat" asp-action="New" class="btn btn-outline-primary">
            <i class="bi bi-chat-dots"></i> Open playground
        </a>
    </div>
</section>

<section class="metric-grid">
    <article class="metric-card">
        <span>Documents</span>
        <strong>@Model.TotalDocuments</strong>
        <small>@Model.IndexedDocuments indexed</small>
    </article>
    <article class="metric-card">
        <span>Indexed chunks</span>
        <strong>@Model.IndexedChunks</strong>
        <small>Available for retrieval</small>
    </article>
    <article class="metric-card">
        <span>Courses</span>
        <strong>@Model.TotalCourses</strong>
        <small>Knowledge scopes</small>
    </article>
    <article class="metric-card">
        <span>Evaluation</span>
        <strong>@Model.EvaluationStatus</strong>
        <small>RAGAS dashboard prepared</small>
    </article>
</section>

<section class="dashboard-grid">
    <div class="dashboard-main">
        <article class="panel">
            <div class="panel-header">
                <div>
                    <span class="section-eyebrow">Pipeline</span>
                    <h2>RAG readiness</h2>
                </div>
            </div>
            <div class="pipeline-steps">
                @foreach (var step in Model.PipelineSteps)
                {
                    <div class="pipeline-step pipeline-@step.Status.ToLowerInvariant().Replace(" ", "-")">
                        <span class="pipeline-icon"><i class="@step.IconCssClass"></i></span>
                        <strong>@step.Label</strong>
                        <small>@step.Description</small>
                        <em>@step.Status</em>
                    </div>
                }
            </div>
        </article>

        <article class="panel">
            <div class="panel-header">
                <div>
                    <span class="section-eyebrow">Knowledge Base</span>
                    <h2>Recent documents</h2>
                </div>
                <a asp-controller="Document" asp-action="Index" class="btn btn-outline-primary btn-sm">View all</a>
            </div>

            @if (Model.RecentDocuments.Any())
            {
                <div class="table-responsive table-shell">
                    <table class="table ops-table">
                        <thead>
                            <tr>
                                <th>File</th>
                                <th>Course</th>
                                <th>Chunks</th>
                                <th>Status</th>
                                <th>Uploaded</th>
                            </tr>
                        </thead>
                        <tbody>
                            @foreach (var doc in Model.RecentDocuments)
                            {
                                <tr>
                                    <td><a asp-controller="Document" asp-action="Details" asp-route-id="@doc.Id">@doc.OriginalFileName</a></td>
                                    <td>@doc.CourseName</td>
                                    <td class="text-mono">@doc.ChunkCount</td>
                                    <td><span class="status-badge status-@doc.Status.ToLowerInvariant()">@doc.Status</span></td>
                                    <td>@doc.CreatedAt.ToString("dd/MM/yyyy HH:mm")</td>
                                </tr>
                            }
                        </tbody>
                    </table>
                </div>
            }
            else
            {
                <div class="empty-state compact">
                    <i class="bi bi-database"></i>
                    <h3>No documents indexed yet</h3>
                    <p>Upload PDF or DOCX material to start building the knowledge base.</p>
                    <a asp-controller="Document" asp-action="Upload" class="btn btn-primary">Upload first document</a>
                </div>
            }
        </article>
    </div>

    <aside class="dashboard-side">
        <article class="panel">
            <div class="panel-header">
                <div>
                    <span class="section-eyebrow">Next actions</span>
                    <h2>Recommended</h2>
                </div>
            </div>
            <div class="action-list">
                @foreach (var action in Model.NextActions)
                {
                    <a asp-controller="@action.Controller" asp-action="@action.Action" class="action-item">
                        <i class="@action.IconCssClass"></i>
                        <span>
                            <strong>@action.Label</strong>
                            <small>@action.Description</small>
                        </span>
                    </a>
                }
            </div>
        </article>
    </aside>
</section>
```

- [x] **Step 5: Add Overview CSS**

Append to `site.css`:

```css
.page-heading {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  gap: 20px;
  margin-bottom: 24px;
}

.page-heading h1 {
  margin-bottom: 6px;
  font-size: 2rem;
}

.page-heading p {
  max-width: 760px;
  margin: 0;
  color: var(--ink-600);
}

.page-heading-actions {
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
}

.section-eyebrow {
  display: inline-block;
  margin-bottom: 6px;
  color: var(--accent-color);
  font-size: 0.72rem;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  font-weight: 800;
}

.metric-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 14px;
  margin-bottom: 18px;
}

.metric-card,
.panel {
  background: var(--sand-50);
  border: 1px solid var(--sand-300);
  border-radius: 8px;
  box-shadow: 0 4px 14px rgba(28, 20, 9, 0.03);
}

.metric-card {
  padding: 18px;
}

.metric-card span,
.metric-card small {
  color: var(--ink-600);
  display: block;
}

.metric-card strong {
  display: block;
  color: var(--ink-900);
  font-size: 1.75rem;
  line-height: 1.2;
  margin: 8px 0;
}

.dashboard-grid {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 340px;
  gap: 18px;
}

.dashboard-main {
  display: grid;
  gap: 18px;
}

.panel {
  padding: 18px;
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  margin-bottom: 16px;
}

.panel-header h2 {
  font-size: 1.2rem;
  margin: 0;
}

.pipeline-steps {
  display: grid;
  grid-template-columns: repeat(6, minmax(0, 1fr));
  gap: 10px;
}

.pipeline-step {
  border: 1px solid var(--sand-300);
  border-radius: 8px;
  padding: 12px;
  min-height: 150px;
  background: var(--sand-100);
}

.pipeline-icon {
  width: 34px;
  height: 34px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 8px;
  color: var(--sand-50);
  background: var(--ink-800);
  margin-bottom: 10px;
}

.pipeline-step strong,
.pipeline-step small,
.pipeline-step em {
  display: block;
}

.pipeline-step small {
  color: var(--ink-600);
  min-height: 42px;
}

.pipeline-step em {
  margin-top: 10px;
  font-style: normal;
  font-size: 0.78rem;
  font-weight: 800;
  color: var(--accent-color);
}

.table-shell {
  border-radius: 8px;
}

.ops-table th {
  white-space: nowrap;
}

.action-list {
  display: grid;
  gap: 10px;
}

.action-item {
  display: flex;
  gap: 12px;
  padding: 12px;
  border: 1px solid var(--sand-300);
  border-radius: 8px;
  color: var(--ink-800);
  background: var(--sand-100);
}

.action-item:hover {
  color: var(--ink-900);
  background: var(--sand-200);
}

.action-item i {
  color: var(--accent-color);
  margin-top: 2px;
}

.action-item small {
  display: block;
  color: var(--ink-600);
}

.empty-state {
  text-align: center;
  padding: 48px 20px;
  color: var(--ink-600);
}

.empty-state.compact {
  padding: 32px 16px;
}

.empty-state i {
  font-size: 2.4rem;
  color: var(--sand-400);
}
```

- [x] **Step 6: Add responsive Overview CSS**

Append below the Overview CSS:

```css
@media (max-width: 1100px) {
  .metric-grid,
  .pipeline-steps {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .dashboard-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 700px) {
  .page-heading {
    align-items: flex-start;
    flex-direction: column;
  }

  .metric-grid,
  .pipeline-steps {
    grid-template-columns: 1fr;
  }
}
```

- [x] **Step 7: Run tests**

Run:

```powershell
dotnet test PRN222.Tests\PRN222.Tests.csproj --no-restore --nologo
```

Expected: all tests pass.

- [x] **Step 8: Commit**

```powershell
git add src\PRN222.MVC\Controllers\HomeController.cs src\PRN222.MVC\Views\Home\Index.cshtml src\PRN222.MVC\wwwroot\css\site.css src\PRN222.Tests\MVC\HomeControllerTests.cs
git commit -m "feat: redesign home as RAG operations overview"
```

---

### Task 4: Knowledge Base List And Upload Polish

**Files:**
- Modify: `src/PRN222.MVC/Controllers/DocumentController.cs`
- Modify: `src/PRN222.MVC/Views/Document/Index.cshtml`
- Modify: `src/PRN222.MVC/Views/Document/Upload.cshtml`
- Modify: `src/PRN222.MVC/wwwroot/css/site.css`

- [x] **Step 1: Add filter parameters in DocumentController**

Change `Index` signature in `DocumentController`:

```csharp
public async Task<IActionResult> Index(int? courseId, string? status, string? fileType, string? search)
```

Replace the body after document retrieval with:

```csharp
if (!string.IsNullOrWhiteSpace(status))
{
    documents = documents.Where(d => d.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
}

if (!string.IsNullOrWhiteSpace(fileType))
{
    documents = fileType.Equals("pdf", StringComparison.OrdinalIgnoreCase)
        ? documents.Where(d => d.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
        : documents.Where(d => d.ContentType.Contains("word", StringComparison.OrdinalIgnoreCase));
}

if (!string.IsNullOrWhiteSpace(search))
{
    documents = documents.Where(d =>
        d.OriginalFileName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
        d.CourseName.Contains(search, StringComparison.OrdinalIgnoreCase));
}

ViewBag.Courses = await _courseService.GetAllCoursesAsync();
ViewBag.SelectedCourseId = courseId;
ViewBag.SelectedStatus = status;
ViewBag.SelectedFileType = fileType;
ViewBag.Search = search;
return View(documents);
```

- [x] **Step 2: Build MVC**

Run:

```powershell
dotnet build PRN222.MVC\PRN222.MVC.csproj --nologo
```

Expected: build succeeds.

- [x] **Step 3: Replace Document Index with Knowledge Base UI**

Replace the top heading/filter section in `src/PRN222.MVC/Views/Document/Index.cshtml` with:

```cshtml
@model IEnumerable<PRN222.BLL.DTOs.DocumentDto>
@{
    ViewData["Title"] = "Knowledge Base";
    var courses = ViewBag.Courses as IEnumerable<PRN222.BLL.DTOs.CourseDto>;
    var selectedCourseId = ViewBag.SelectedCourseId as int?;
    var selectedStatus = ViewBag.SelectedStatus as string;
    var selectedFileType = ViewBag.SelectedFileType as string;
    var search = ViewBag.Search as string;
}

<section class="page-heading">
    <div>
        <span class="section-eyebrow">Knowledge Base</span>
        <h1>Document ingestion</h1>
        <p>Manage course material, indexing state, chunks, and files available to the RAG chat playground.</p>
    </div>
    <a asp-action="Upload" class="btn btn-primary">
        <i class="bi bi-cloud-upload"></i> Upload document
    </a>
</section>

@if (TempData["Success"] != null)
{
    <div class="alert alert-success alert-dismissible fade show" role="alert">
        <i class="bi bi-check-circle me-1"></i>@TempData["Success"]
        <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
    </div>
}
@if (TempData["Error"] != null)
{
    <div class="alert alert-danger alert-dismissible fade show" role="alert">
        <i class="bi bi-exclamation-triangle me-1"></i>@TempData["Error"]
        <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
    </div>
}

<section class="panel filter-panel">
    <form method="get" class="filter-grid">
        <div>
            <label class="form-label">Search</label>
            <input name="search" value="@search" class="form-control" aria-label="Search by file or course name" />
        </div>
        <div>
            <label class="form-label">Course</label>
            <select name="courseId" class="form-select">
                <option value="">All courses</option>
                @if (courses != null)
                {
                    foreach (var course in courses)
                    {
                        <option value="@course.Id" selected="@(selectedCourseId == course.Id)">@course.Name</option>
                    }
                }
            </select>
        </div>
        <div>
            <label class="form-label">Status</label>
            <select name="status" class="form-select">
                <option value="">All statuses</option>
                <option value="Uploaded" selected="@(selectedStatus == "Uploaded")">Uploaded</option>
                <option value="Processing" selected="@(selectedStatus == "Processing")">Processing</option>
                <option value="Indexed" selected="@(selectedStatus == "Indexed")">Indexed</option>
                <option value="Failed" selected="@(selectedStatus == "Failed")">Failed</option>
            </select>
        </div>
        <div>
            <label class="form-label">Type</label>
            <select name="fileType" class="form-select">
                <option value="">All types</option>
                <option value="pdf" selected="@(selectedFileType == "pdf")">PDF</option>
                <option value="docx" selected="@(selectedFileType == "docx")">DOCX</option>
            </select>
        </div>
        <div class="filter-actions">
            <button type="submit" class="btn btn-outline-secondary">
                <i class="bi bi-funnel"></i> Apply
            </button>
        </div>
    </form>
</section>
```

Keep the existing helper functions, but update status labels to English operational labels:

```csharp
var (cssClass, label) = status switch
{
    "Uploaded" => ("status-uploaded", "Uploaded"),
    "Processing" => ("status-processing", "Processing"),
    "Indexed" => ("status-indexed", "Indexed"),
    "Failed" => ("status-failed", "Failed"),
    _ => ("status-uploaded", status)
};
```

- [x] **Step 4: Replace empty state in Document Index**

Replace the existing empty-state markup with:

```cshtml
<div class="empty-state">
    <i class="bi bi-database"></i>
    <h3>No matching documents</h3>
    <p>Upload course material or adjust filters to build the searchable knowledge base.</p>
    <a asp-action="Upload" class="btn btn-primary">
        <i class="bi bi-cloud-upload"></i> Upload document
    </a>
</div>
```

- [x] **Step 5: Update Upload page heading**

In `src/PRN222.MVC/Views/Document/Upload.cshtml`, replace the breadcrumb and card header area with:

```cshtml
<section class="page-heading">
    <div>
        <span class="section-eyebrow">Ingestion</span>
        <h1>Upload document</h1>
        <p>Choose a course and add PDF or DOCX material. After upload, process the document to extract, chunk, and embed it.</p>
    </div>
</section>

<div class="ingestion-steps">
    <span class="active">1. Select course</span>
    <span>2. Upload file</span>
    <span>3. Process document</span>
    <span>4. Ready for chat</span>
</div>
```

Keep the form and modal behavior.

- [x] **Step 6: Add Knowledge Base CSS**

Append to `site.css`:

```css
.filter-panel {
  margin-bottom: 18px;
}

.filter-grid {
  display: grid;
  grid-template-columns: minmax(220px, 1.4fr) repeat(3, minmax(160px, 1fr)) auto;
  gap: 12px;
  align-items: end;
}

.filter-actions {
  display: flex;
  align-items: end;
}

.btn-group .btn {
  min-width: 38px;
}

.ingestion-steps {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 8px;
  margin-bottom: 16px;
}

.ingestion-steps span {
  padding: 10px 12px;
  border-radius: 8px;
  background: var(--sand-50);
  border: 1px solid var(--sand-300);
  color: var(--ink-600);
  font-size: 0.85rem;
  font-weight: 700;
}

.ingestion-steps span.active {
  color: var(--sand-50);
  background: var(--ink-800);
  border-color: var(--ink-800);
}

@media (max-width: 1100px) {
  .filter-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .filter-actions {
    grid-column: 1 / -1;
  }
}

@media (max-width: 700px) {
  .filter-grid,
  .ingestion-steps {
    grid-template-columns: 1fr;
  }
}
```

- [x] **Step 7: Run MVC build**

Run:

```powershell
dotnet build PRN222.MVC\PRN222.MVC.csproj --nologo
```

Expected: build succeeds.

- [x] **Step 8: Commit**

```powershell
git add src\PRN222.MVC\Controllers\DocumentController.cs src\PRN222.MVC\Views\Document\Index.cshtml src\PRN222.MVC\Views\Document\Upload.cshtml src\PRN222.MVC\wwwroot\css\site.css
git commit -m "feat: polish knowledge base ingestion workflow"
```

---

### Task 5: Chat Playground With Retrieved Context Panel

**Files:**
- Modify: `src/PRN222.MVC/Views/Chat/Session.cshtml`
- Modify: `src/PRN222.MVC/wwwroot/css/site.css`

- [x] **Step 1: Inspect current chat view**

Run:

```powershell
Get-Content src\PRN222.MVC\Views\Chat\Session.cshtml
```

Expected: identify the message loop, input form, course selector, and JavaScript `Ask` request. Preserve the existing JavaScript endpoint call to `/Chat/Ask`.

- [x] **Step 2: Wrap chat view in lab shell**

In `Session.cshtml`, wrap the existing chat history and input with this structure:

```cshtml
<section class="page-heading compact-heading">
    <div>
        <span class="section-eyebrow">Chat Playground</span>
        <h1>@(ViewBag.SessionTitle ?? "New RAG session")</h1>
        <p>Ask questions against indexed course material and inspect retrieved context.</p>
    </div>
</section>

<section class="chat-lab-layout">
    <div class="chat-workspace">
        <div class="chat-lab-toolbar">
            <div>
                <label class="form-label">Knowledge scope</label>
                <select id="courseId" class="form-select">
                    @foreach (var course in ViewBag.Courses)
                    {
                        <option value="@course.Id">@course.Name</option>
                    }
                </select>
            </div>
            <a asp-controller="Chat" asp-action="Index" class="btn btn-outline-secondary">
                <i class="bi bi-clock-history"></i> Sessions
            </a>
        </div>

        <div class="chat-history-container">
            <!-- keep current message rendering here -->
        </div>

        <div class="chat-input-area">
            <!-- keep current input form here -->
        </div>
    </div>

    <aside class="retrieval-panel">
        <div class="panel-header">
            <div>
                <span class="section-eyebrow">Retrieved context</span>
                <h2>Top evidence</h2>
            </div>
        </div>
        <div id="retrievedContextList" class="retrieved-context-list">
            <div class="empty-state compact">
                <i class="bi bi-search"></i>
                <h3>No context selected</h3>
                <p>Ask a question to inspect matching chunks, relevance, and source files.</p>
            </div>
        </div>
    </aside>
</section>
```

- [x] **Step 3: Update chat JavaScript to fill context panel**

In the existing successful Ask response handler, after rendering the assistant message, add:

```javascript
const contextList = document.getElementById('retrievedContextList');
if (contextList) {
    if (data.citations && data.citations.length > 0) {
        contextList.innerHTML = data.citations.map(citation => `
            <article class="retrieved-context-item">
                <div class="retrieved-context-score">${Math.round((citation.relevanceScore || 0) * 100)}%</div>
                <strong>${citation.documentName || 'Tài liệu'}</strong>
                <p>${citation.snippetText || ''}</p>
            </article>
        `).join('');
    } else {
        contextList.innerHTML = `
            <div class="empty-state compact">
                <i class="bi bi-search"></i>
                <h3>No matching chunks</h3>
                <p>The answer did not include retrieved citations from the selected course.</p>
            </div>
        `;
    }
}
```

- [x] **Step 4: Add Chat Playground CSS**

Append to `site.css`:

```css
.compact-heading {
  margin-bottom: 16px;
}

.chat-lab-layout {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 360px;
  gap: 18px;
  align-items: stretch;
}

.chat-workspace,
.retrieval-panel {
  background: var(--sand-50);
  border: 1px solid var(--sand-300);
  border-radius: 8px;
  overflow: hidden;
}

.chat-lab-toolbar {
  display: flex;
  justify-content: space-between;
  gap: 16px;
  align-items: end;
  padding: 16px;
  border-bottom: 1px solid var(--sand-300);
}

.chat-lab-toolbar > div {
  min-width: 260px;
}

.retrieval-panel {
  padding: 18px;
}

.retrieved-context-list {
  display: grid;
  gap: 10px;
}

.retrieved-context-item {
  border: 1px solid var(--sand-300);
  border-radius: 8px;
  background: var(--sand-100);
  padding: 12px;
}

.retrieved-context-score {
  display: inline-flex;
  padding: 3px 8px;
  border-radius: 999px;
  background: var(--status-info-bg);
  color: var(--status-info-text);
  font-size: 0.78rem;
  font-weight: 800;
  margin-bottom: 8px;
}

.retrieved-context-item strong {
  display: block;
  color: var(--ink-900);
}

.retrieved-context-item p {
  color: var(--ink-600);
  font-size: 0.88rem;
  margin: 6px 0 0;
}

@media (max-width: 1100px) {
  .chat-lab-layout {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 700px) {
  .chat-lab-toolbar {
    align-items: stretch;
    flex-direction: column;
  }

  .chat-lab-toolbar > div {
    min-width: 0;
  }
}
```

- [x] **Step 5: Run MVC build**

Run:

```powershell
dotnet build PRN222.MVC\PRN222.MVC.csproj --nologo
```

Expected: build succeeds.

- [x] **Step 6: Commit**

```powershell
git add src\PRN222.MVC\Views\Chat\Session.cshtml src\PRN222.MVC\wwwroot\css\site.css
git commit -m "feat: add RAG chat playground context panel"
```

---

### Task 6: Evaluation Dashboard Route

**Files:**
- Create: `src/PRN222.MVC/Controllers/EvaluationController.cs`
- Create: `src/PRN222.MVC/Views/Evaluation/Index.cshtml`
- Modify: `src/PRN222.MVC/wwwroot/css/site.css`

- [x] **Step 1: Add controller**

Create `src/PRN222.MVC/Controllers/EvaluationController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;

namespace PRN222.MVC.Controllers;

public class EvaluationController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
```

- [x] **Step 2: Add Evaluation view**

Create `src/PRN222.MVC/Views/Evaluation/Index.cshtml`:

```cshtml
@{
    ViewData["Title"] = "Evaluation";
}

<section class="page-heading">
    <div>
        <span class="section-eyebrow">Evaluation</span>
        <h1>RAG quality dashboard</h1>
        <p>Prepare the workspace for RAGAS-style comparison across retrieval, generation, and answer quality metrics.</p>
    </div>
    <button class="btn btn-outline-secondary" disabled>
        <i class="bi bi-play-circle"></i> Run benchmark
    </button>
</section>

<section class="metric-grid">
    <article class="metric-card evaluation-card">
        <span>Faithfulness</span>
        <strong>--</strong>
        <small>Awaiting benchmark run</small>
    </article>
    <article class="metric-card evaluation-card">
        <span>Answer relevancy</span>
        <strong>--</strong>
        <small>Awaiting benchmark run</small>
    </article>
    <article class="metric-card evaluation-card">
        <span>Context precision</span>
        <strong>--</strong>
        <small>Awaiting benchmark run</small>
    </article>
    <article class="metric-card evaluation-card">
        <span>Context recall</span>
        <strong>--</strong>
        <small>Awaiting benchmark run</small>
    </article>
</section>

<section class="panel">
    <div class="panel-header">
        <div>
            <span class="section-eyebrow">Runs</span>
            <h2>Benchmark history</h2>
        </div>
    </div>
    <div class="empty-state compact">
        <i class="bi bi-graph-up"></i>
        <h3>No evaluation runs yet</h3>
        <p>The dashboard is ready for benchmark results once evaluation execution is connected.</p>
    </div>
</section>
```

- [x] **Step 3: Add Evaluation CSS**

Append to `site.css`:

```css
.evaluation-card strong {
  font-family: var(--font-mono);
  letter-spacing: 0;
}
```

- [x] **Step 4: Build MVC**

Run:

```powershell
dotnet build PRN222.MVC\PRN222.MVC.csproj --nologo
```

Expected: build succeeds.

- [x] **Step 5: Commit**

```powershell
git add src\PRN222.MVC\Controllers\EvaluationController.cs src\PRN222.MVC\Views\Evaluation\Index.cshtml src\PRN222.MVC\wwwroot\css\site.css
git commit -m "feat: add evaluation dashboard shell"
```

---

### Task 7: Visual Cleanup And Verification

**Files:**
- Modify: `src/PRN222.MVC/wwwroot/css/site.css`
- No source changes if visual checks pass.

- [x] **Step 1: Normalize dashboard card radius and footer contrast**

In `site.css`, update the existing `.card` radius from `12px` to `8px`:

```css
.card {
  border-radius: 8px !important;
}
```

Add this footer correction:

```css
footer .text-white {
  color: var(--ink-900) !important;
}
```

- [x] **Step 2: Start app if not running**

Run:

```powershell
dotnet run --project PRN222.MVC\PRN222.MVC.csproj --urls http://localhost:5263
```

Expected: app listens on `http://localhost:5263`. If another PRN222.MVC process is already running, stop only that process and rerun.

- [x] **Step 3: Capture desktop screenshots**

Run from repository root:

```powershell
$edge = Get-ChildItem 'C:\Program Files*\Microsoft\Edge\Application\msedge.exe' -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
& $edge --headless=new --disable-gpu --window-size=1440,1000 --screenshot="$PWD\ui-overview-desktop.png" http://localhost:5263/
& $edge --headless=new --disable-gpu --window-size=1440,1000 --screenshot="$PWD\ui-knowledge-desktop.png" http://localhost:5263/Document
& $edge --headless=new --disable-gpu --window-size=1440,1000 --screenshot="$PWD\ui-upload-desktop.png" http://localhost:5263/Document/Upload
& $edge --headless=new --disable-gpu --window-size=1440,1000 --screenshot="$PWD\ui-chat-desktop.png" http://localhost:5263/Chat/New
& $edge --headless=new --disable-gpu --window-size=1440,1000 --screenshot="$PWD\ui-evaluation-desktop.png" http://localhost:5263/Evaluation
```

Expected: five PNG files are created in the repository root.

- [x] **Step 4: Capture mobile screenshots**

Run from repository root:

```powershell
$edge = Get-ChildItem 'C:\Program Files*\Microsoft\Edge\Application\msedge.exe' -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
& $edge --headless=new --disable-gpu --window-size=390,844 --screenshot="$PWD\ui-overview-mobile.png" http://localhost:5263/
& $edge --headless=new --disable-gpu --window-size=390,844 --screenshot="$PWD\ui-knowledge-mobile.png" http://localhost:5263/Document
& $edge --headless=new --disable-gpu --window-size=390,844 --screenshot="$PWD\ui-chat-mobile.png" http://localhost:5263/Chat/New
```

Expected: three mobile PNG files are created in the repository root.

- [x] **Step 5: Run automated verification**

Run:

```powershell
dotnet build PRN222.MVC\PRN222.MVC.csproj --nologo
dotnet test PRN222_Assignment1.sln --nologo
```

Expected: MVC build succeeds and the full test suite passes.

- [x] **Step 6: Commit final polish**

```powershell
git add src\PRN222.MVC\wwwroot\css\site.css ui-*.png
git commit -m "chore: verify AI operations console visuals"
```

If screenshots should not be committed, use this commit instead:

```powershell
git add src\PRN222.MVC\wwwroot\css\site.css
git commit -m "style: finalize AI operations console polish"
```

---

## Final Verification Checklist

- [x] `dotnet build PRN222.MVC\PRN222.MVC.csproj --nologo` succeeds.
- [x] `dotnet test PRN222_Assignment1.sln --nologo` passes.
- [x] Overview no longer has a landing hero.
- [x] Sidebar or compact horizontal nav is readable on mobile.
- [x] Knowledge Base table remains usable at 1440px.
- [x] Upload form communicates ingestion steps.
- [x] Chat Playground shows a retrieved-context panel.
- [x] Evaluation route opens at `/Evaluation`.
- [x] Footer brand text is readable on the light background.
- [x] No unrelated user changes are reverted.
