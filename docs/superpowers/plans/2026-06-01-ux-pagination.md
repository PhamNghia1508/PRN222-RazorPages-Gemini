# UX Pagination Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add consistent server-side pagination, result counts, preserved filters, and compact preview paging to the Document, Evaluation, Run Detail, and Test Set Generator screens.

**Architecture:** Keep pagination in the MVC layer for list pages that already load enumerable data, and add one paged BLL method for the Test Set preview JSON endpoint. A shared `PagedResult<T>` model and `_Pager.cshtml` partial provide the common UI contract.

**Tech Stack:** ASP.NET Core MVC, Razor views, EF Core LINQ, xUnit, Moq, FluentAssertions, Bootstrap icons, existing Warm Sand & Ink CSS.

---

## File Structure

- Create: `src/PRN222.BLL/DTOs/PagedResult.cs`
  - Shared immutable pagination model for Razor views and JSON responses.
- Create: `src/PRN222.MVC/Models/Pagination/PagerRouteValueBuilder.cs`
  - Builds route values while preserving query string filters for pager links.
- Create: `src/PRN222.MVC/Views/Shared/_Pager.cshtml`
  - Shared pager partial for server-rendered MVC pages.
- Modify: `src/PRN222.MVC/Controllers/DocumentController.cs`
  - Adds `page/pageSize`, applies existing filters before paging, passes `PagedResult<DocumentDto>`.
- Modify: `src/PRN222.MVC/Views/Document/Index.cshtml`
  - Uses `PagedResult<DocumentDto>`, renders result count, clear filters, and pager.
- Modify: `src/PRN222.MVC/Controllers/EvaluationController.cs`
  - Adds filters and pagination for `Index`, pagination for `RunDetail`.
- Modify: `src/PRN222.MVC/Views/Evaluation/Index.cshtml`
  - Uses `PagedResult<BenchmarkRunDto>`, adds filter form and pager.
- Modify: `src/PRN222.MVC/Views/Evaluation/RunDetail.cshtml`
  - Pages `Model.Results` while keeping metric cards and CSV export unchanged.
- Modify: `src/PRN222.BLL/Services/Interfaces/ITestSetGeneratorService.cs`
  - Adds paged preview method.
- Modify: `src/PRN222.BLL/Services/TestSetGeneratorService.cs`
  - Implements paged preview query with total count.
- Modify: `src/PRN222.MVC/Controllers/TestSetGeneratorController.cs`
  - Changes preview endpoint to return `PagedResult<QAPairDto>`.
- Modify: `src/PRN222.MVC/Views/TestSetGenerator/Index.cshtml`
  - Adds preview page-size control, result count, Previous/Next pager, and clamp behavior.
- Modify: `src/PRN222.MVC/wwwroot/css/site.css`
  - Adds pager and filter summary styles matching the existing design.
- Test: `src/PRN222.Tests/MVC/PagedResultTests.cs`
  - Verifies page-size validation, page clamping, total page count, and index ranges.
- Test: `src/PRN222.Tests/MVC/PagerRouteValueBuilderTests.cs`
  - Verifies query preservation and page-size changes reset to page 1.
- Test: `src/PRN222.Tests/MVC/TestSetGeneratorControllerTests.cs`
  - Verifies preview JSON includes paged shape.

---

### Task 1: Add Shared Pagination Model

**Files:**
- Create: `src/PRN222.BLL/DTOs/PagedResult.cs`
- Test: `src/PRN222.Tests/MVC/PagedResultTests.cs`

- [ ] **Step 1: Write failing tests for pagination normalization**

Create `src/PRN222.Tests/MVC/PagedResultTests.cs`:

```csharp
using FluentAssertions;
using PRN222.BLL.DTOs;
using Xunit;

namespace PRN222.Tests.MVC;

public class PagedResultTests
{
    [Fact]
    public void Create_ShouldClampInvalidPageAndPageSize()
    {
        var source = Enumerable.Range(1, 60).ToList();

        var result = PagedResult<int>.Create(source, page: -3, pageSize: 999);

        result.Page.Should().Be(1);
        result.PageSize.Should().Be(25);
        result.TotalItems.Should().Be(60);
        result.TotalPages.Should().Be(3);
        result.Items.Should().Equal(Enumerable.Range(1, 25));
        result.FirstItemIndex.Should().Be(1);
        result.LastItemIndex.Should().Be(25);
    }

    [Fact]
    public void Create_ShouldClampPageBeyondTotalPages()
    {
        var source = Enumerable.Range(1, 12).ToList();

        var result = PagedResult<int>.Create(source, page: 10, pageSize: 10);

        result.Page.Should().Be(2);
        result.TotalPages.Should().Be(2);
        result.Items.Should().Equal(11, 12);
        result.FirstItemIndex.Should().Be(11);
        result.LastItemIndex.Should().Be(12);
        result.HasPreviousPage.Should().BeTrue();
        result.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public void Create_ShouldUsePageOneForEmptySource()
    {
        var result = PagedResult<int>.Create(Array.Empty<int>(), page: 5, pageSize: 10);

        result.Page.Should().Be(1);
        result.TotalItems.Should().Be(0);
        result.TotalPages.Should().Be(0);
        result.Items.Should().BeEmpty();
        result.FirstItemIndex.Should().Be(0);
        result.LastItemIndex.Should().Be(0);
    }
}
```

- [ ] **Step 2: Run the failing tests**

Run:

```powershell
dotnet test src\PRN222_Assignment1.sln --filter PagedResultTests
```

Expected: FAIL because `PRN222.BLL.DTOs.PagedResult` does not exist.

- [ ] **Step 3: Implement `PagedResult<T>`**

Create `src/PRN222.BLL/DTOs/PagedResult.cs`:

```csharp
namespace PRN222.MVC.Models.Pagination;

public sealed class PagedResult<T>
{
    public static readonly int[] AllowedPageSizes = { 10, 25, 50, 100 };
    public const int DefaultPageSize = 25;

    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages { get; init; }
    public int FirstItemIndex { get; init; }
    public int LastItemIndex { get; init; }
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => TotalPages > 0 && Page < TotalPages;

    public static PagedResult<T> Create(IEnumerable<T> source, int page, int pageSize)
    {
        var normalizedPageSize = NormalizePageSize(pageSize);
        var items = source.ToList();
        return CreateFromList(items, page, normalizedPageSize, items.Count);
    }

    public static PagedResult<T> CreateFromPage(
        IReadOnlyList<T> pageItems,
        int page,
        int pageSize,
        int totalItems)
    {
        return CreateFromList(pageItems.ToList(), page, NormalizePageSize(pageSize), totalItems);
    }

    public static int NormalizePageSize(int pageSize) =>
        AllowedPageSizes.Contains(pageSize) ? pageSize : DefaultPageSize;

    private static PagedResult<T> CreateFromList(List<T> source, int page, int pageSize, int totalItems)
    {
        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)pageSize);

        var normalizedPage = page < 1 ? 1 : page;
        if (totalPages > 0 && normalizedPage > totalPages)
        {
            normalizedPage = totalPages;
        }

        var pageItems = source.Count == totalItems
            ? source.Skip((normalizedPage - 1) * pageSize).Take(pageSize).ToList()
            : source.ToList();

        var firstIndex = totalItems == 0 ? 0 : ((normalizedPage - 1) * pageSize) + 1;
        var lastIndex = totalItems == 0 ? 0 : Math.Min(firstIndex + pageItems.Count - 1, totalItems);

        return new PagedResult<T>
        {
            Items = pageItems,
            Page = normalizedPage,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages,
            FirstItemIndex = firstIndex,
            LastItemIndex = lastIndex
        };
    }
}
```

- [ ] **Step 4: Run the tests**

Run:

```powershell
dotnet test src\PRN222_Assignment1.sln --filter PagedResultTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src\\PRN222.BLL\\DTOs\\PagedResult.cs src\PRN222.Tests\MVC\PagedResultTests.cs
git commit -m "feat: add shared paged result model"
```

---

### Task 2: Add Shared Pager Route Builder And Partial

**Files:**
- Create: `src/PRN222.MVC/Models/Pagination/PagerRouteValueBuilder.cs`
- Create: `src/PRN222.MVC/Views/Shared/_Pager.cshtml`
- Modify: `src/PRN222.MVC/wwwroot/css/site.css`
- Test: `src/PRN222.Tests/MVC/PagerRouteValueBuilderTests.cs`

- [ ] **Step 1: Write failing tests for route preservation**

Create `src/PRN222.Tests/MVC/PagerRouteValueBuilderTests.cs`:

```csharp
using FluentAssertions;
using PRN222.MVC.Models.Pagination;
using Xunit;

namespace PRN222.Tests.MVC;

public class PagerRouteValueBuilderTests
{
    [Fact]
    public void Build_ShouldPreserveFiltersAndSetPage()
    {
        var current = new Dictionary<string, string?>
        {
            ["search"] = "rag",
            ["status"] = "Completed",
            ["pageSize"] = "25"
        };

        var result = PagerRouteValueBuilder.Build(current, page: 3);

        result["search"].Should().Be("rag");
        result["status"].Should().Be("Completed");
        result["pageSize"].Should().Be("25");
        result["page"].Should().Be("3");
    }

    [Fact]
    public void BuildForPageSize_ShouldResetToFirstPage()
    {
        var current = new Dictionary<string, string?>
        {
            ["courseId"] = "1",
            ["page"] = "4",
            ["pageSize"] = "25"
        };

        var result = PagerRouteValueBuilder.BuildForPageSize(current, pageSize: 50);

        result["courseId"].Should().Be("1");
        result["page"].Should().Be("1");
        result["pageSize"].Should().Be("50");
    }
}
```

- [ ] **Step 2: Run the failing tests**

Run:

```powershell
dotnet test src\PRN222_Assignment1.sln --filter PagerRouteValueBuilderTests
```

Expected: FAIL because `PagerRouteValueBuilder` does not exist.

- [ ] **Step 3: Implement route builder**

Create `src/PRN222.MVC/Models/Pagination/PagerRouteValueBuilder.cs`:

```csharp
namespace PRN222.MVC.Models.Pagination;

public static class PagerRouteValueBuilder
{
    public static Dictionary<string, string?> Build(
        IReadOnlyDictionary<string, string?> currentValues,
        int page)
    {
        var values = new Dictionary<string, string?>(currentValues, StringComparer.OrdinalIgnoreCase)
        {
            ["page"] = page.ToString()
        };

        return values
            .Where(kvp => kvp.Value is not null && !string.IsNullOrWhiteSpace(kvp.Value.ToString()))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
    }

    public static Dictionary<string, string?> BuildForPageSize(
        IReadOnlyDictionary<string, string?> currentValues,
        int pageSize)
    {
        var values = new Dictionary<string, string?>(currentValues, StringComparer.OrdinalIgnoreCase)
        {
            ["page"] = "1",
            ["pageSize"] = pageSize.ToString()
        };

        return values
            .Where(kvp => kvp.Value is not null && !string.IsNullOrWhiteSpace(kvp.Value.ToString()))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 4: Add shared pager partial**

Create `src/PRN222.MVC/Views/Shared/_Pager.cshtml`:

```cshtml
@using PRN222.BLL.DTOs
@using PRN222.MVC.Models.Pagination
@model dynamic

@{
    var paged = Model.Paged;
    var routeValues = (IReadOnlyDictionary<string, string?>)Model.RouteValues;
    string? label = Model.Label;
    int startPage = Math.Max(1, paged.Page - 2);
    int endPage = paged.TotalPages == 0 ? 1 : Math.Min(paged.TotalPages, paged.Page + 2);
}

<div class="pager-shell" aria-label="Pagination">
    <div class="result-count">
        @if (paged.TotalItems == 0)
        {
            <span>No @label found</span>
        }
        else
        {
            <span>Showing @paged.FirstItemIndex-@paged.LastItemIndex of @paged.TotalItems @label</span>
        }
    </div>

    <div class="pager-links">
        <a class="btn btn-outline-secondary btn-sm @(paged.HasPreviousPage ? "" : "disabled")"
           asp-all-route-data="PagerRouteValueBuilder.Build(routeValues, paged.Page - 1)">
            <i class="bi bi-chevron-left"></i> Previous
        </a>

        @for (int i = startPage; i <= endPage; i++)
        {
            <a class="btn btn-sm @(i == paged.Page ? "btn-primary" : "btn-outline-secondary")"
               asp-all-route-data="PagerRouteValueBuilder.Build(routeValues, i)"
               aria-current="@(i == paged.Page ? "page" : null)">
                @i
            </a>
        }

        <a class="btn btn-outline-secondary btn-sm @(paged.HasNextPage ? "" : "disabled")"
           asp-all-route-data="PagerRouteValueBuilder.Build(routeValues, paged.Page + 1)">
            Next <i class="bi bi-chevron-right"></i>
        </a>
    </div>

    <div class="page-size-control">
        <span>Rows</span>
        @foreach (var size in PagedResult<object>.AllowedPageSizes)
        {
            <a class="@(size == paged.PageSize ? "active" : "")"
               asp-all-route-data="PagerRouteValueBuilder.BuildForPageSize(routeValues, size)">
                @size
            </a>
        }
    </div>
</div>
```

- [ ] **Step 5: Add pager CSS**

Append to `src/PRN222.MVC/wwwroot/css/site.css`:

```css
.pager-shell {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
  margin-top: 14px;
  padding: 12px;
  border: 1px solid var(--sand-300);
  border-radius: 8px;
  background: var(--sand-50);
}

.result-count {
  color: var(--ink-600);
  font-size: 0.88rem;
  font-weight: 600;
}

.pager-links,
.page-size-control,
.filter-summary {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.page-size-control {
  color: var(--ink-600);
  font-size: 0.85rem;
}

.page-size-control a {
  min-width: 32px;
  padding: 3px 8px;
  border: 1px solid var(--sand-300);
  border-radius: 6px;
  color: var(--ink-600);
  text-align: center;
}

.page-size-control a.active,
.page-size-control a:hover {
  background: var(--ink-800);
  border-color: var(--ink-800);
  color: var(--sand-50);
}

.filter-summary {
  margin-top: 10px;
  color: var(--ink-600);
  font-size: 0.88rem;
}

@media (max-width: 700px) {
  .pager-shell {
    align-items: stretch;
    flex-direction: column;
  }

  .pager-links,
  .page-size-control {
    justify-content: flex-start;
  }
}
```

- [ ] **Step 6: Run tests**

Run:

```powershell
dotnet test src\PRN222_Assignment1.sln --filter "PagedResultTests|PagerRouteValueBuilderTests"
```

Expected: PASS.

- [ ] **Step 7: Commit**

```powershell
git add src\PRN222.MVC\Models\Pagination\PagerRouteValueBuilder.cs src\PRN222.MVC\Views\Shared\_Pager.cshtml src\PRN222.MVC\wwwroot\css\site.css src\PRN222.Tests\MVC\PagerRouteValueBuilderTests.cs
git commit -m "feat: add shared pager UI"
```

---

### Task 3: Paginate Document List

**Files:**
- Modify: `src/PRN222.MVC/Controllers/DocumentController.cs`
- Modify: `src/PRN222.MVC/Views/Document/Index.cshtml`

- [ ] **Step 1: Update Document controller signature and paging**

Modify `DocumentController.Index` signature:

```csharp
public async Task<IActionResult> Index(
    int? courseId,
    string? status,
    string? fileType,
    string? search,
    int page = 1,
    int pageSize = 25)
```

Add this using:

```csharp
using PRN222.BLL.DTOs;
```

Replace the final `return View(documents);` with:

```csharp
var orderedDocuments = documents
    .OrderByDescending(d => d.CreatedAt)
    .ThenBy(d => d.OriginalFileName);

var pagedDocuments = PagedResult<DocumentDto>.Create(orderedDocuments, page, pageSize);

ViewBag.Courses = await _courseService.GetAllCoursesAsync();
ViewBag.SelectedCourseId = courseId;
ViewBag.SelectedStatus = status;
ViewBag.SelectedFileType = fileType;
ViewBag.Search = search;
ViewBag.RouteValues = new Dictionary<string, string?>
{
    ["courseId"] = courseId?.ToString(),
    ["status"] = status,
    ["fileType"] = fileType,
    ["search"] = search,
    ["pageSize"] = pagedDocuments.PageSize.ToString()
};
ViewBag.HasActiveFilters = courseId.HasValue ||
    !string.IsNullOrWhiteSpace(status) ||
    !string.IsNullOrWhiteSpace(fileType) ||
    !string.IsNullOrWhiteSpace(search);

return View(pagedDocuments);
```

Remove the earlier duplicate `ViewBag` assignments if present so route values are assigned once after filtering.

- [ ] **Step 2: Update Document view model and loops**

Change the first line of `src/PRN222.MVC/Views/Document/Index.cshtml`:

```cshtml
@model PRN222.BLL.DTOs.PagedResult<PRN222.BLL.DTOs.DocumentDto>
```

Add after existing view variables:

```cshtml
@{
    var routeValues = ViewBag.RouteValues as IReadOnlyDictionary<string, string?> 
        ?? new Dictionary<string, string?>();
    var hasActiveFilters = ViewBag.HasActiveFilters as bool? ?? false;
}
```

Replace `@Model.Count() documents visible` with:

```cshtml
@Model.TotalItems documents matched
```

Replace `@if (Model.Any())` with:

```cshtml
@if (Model.Items.Any())
```

Replace the row index block:

```cshtml
@{ var index = 1; }
@foreach (var doc in Model)
```

with:

```cshtml
@{ var index = Model.FirstItemIndex; }
@foreach (var doc in Model.Items)
```

Below the table responsive block, add:

```cshtml
<partial name="_Pager" model="@(new { Paged = Model, RouteValues = routeValues, Label = "documents" })" />
```

Inside the empty state, add a clear-filter link before the upload button:

```cshtml
@if (hasActiveFilters)
{
    <a asp-action="Index" class="btn btn-outline-secondary">
        <i class="bi bi-x-circle"></i> Clear filters
    </a>
}
```

Add a clear-filter summary below the filter form:

```cshtml
@if (hasActiveFilters)
{
    <div class="filter-summary">
        <span>Filters active.</span>
        <a asp-action="Index">Clear all</a>
    </div>
}
```

- [ ] **Step 3: Build and manually smoke-check Document page**

Run:

```powershell
dotnet build src\PRN222_Assignment1.sln
```

Expected: build succeeds.

Manual URLs:

```text
/Document?page=1&pageSize=10
/Document?status=Indexed&page=2&pageSize=10
/Document?search=prn&pageSize=25
```

Expected: rows are paged, filters are preserved, and clear filters returns `/Document`.

- [ ] **Step 4: Commit**

```powershell
git add src\PRN222.MVC\Controllers\DocumentController.cs src\PRN222.MVC\Views\Document\Index.cshtml
git commit -m "feat: paginate document list"
```

---

### Task 4: Paginate And Filter Evaluation History

**Files:**
- Modify: `src/PRN222.MVC/Controllers/EvaluationController.cs`
- Modify: `src/PRN222.MVC/Views/Evaluation/Index.cshtml`

- [ ] **Step 1: Update Evaluation index action**

Add using:

```csharp
using PRN222.BLL.DTOs;
```

Change `Index` signature:

```csharp
public async Task<IActionResult> Index(
    string? search,
    string? status,
    string? experimentType,
    int? embeddingModelId,
    int page = 1,
    int pageSize = 25)
```

After loading `runs`, apply filters:

```csharp
IEnumerable<BenchmarkRunDto> filteredRuns = runs;

if (!string.IsNullOrWhiteSpace(search))
{
    filteredRuns = filteredRuns.Where(r =>
        r.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
}

if (!string.IsNullOrWhiteSpace(status))
{
    filteredRuns = filteredRuns.Where(r =>
        r.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
}

if (!string.IsNullOrWhiteSpace(experimentType))
{
    filteredRuns = filteredRuns.Where(r =>
        r.ExperimentType.Equals(experimentType, StringComparison.OrdinalIgnoreCase));
}

if (embeddingModelId.HasValue)
{
    filteredRuns = filteredRuns.Where(r => r.EmbeddingModelId == embeddingModelId.Value);
}

var orderedRuns = filteredRuns
    .OrderByDescending(r => r.StartedAt)
    .ThenByDescending(r => r.Id);

var pagedRuns = PagedResult<BenchmarkRunDto>.Create(orderedRuns, page, pageSize);
```

Set filter view data before return:

```csharp
ViewBag.Search = search;
ViewBag.SelectedStatus = status;
ViewBag.SelectedExperimentType = experimentType;
ViewBag.SelectedEmbeddingModelId = embeddingModelId;
ViewBag.RouteValues = new Dictionary<string, string?>
{
    ["search"] = search,
    ["status"] = status,
    ["experimentType"] = experimentType,
    ["embeddingModelId"] = embeddingModelId?.ToString(),
    ["pageSize"] = pagedRuns.PageSize.ToString()
};
ViewBag.HasActiveFilters = !string.IsNullOrWhiteSpace(search) ||
    !string.IsNullOrWhiteSpace(status) ||
    !string.IsNullOrWhiteSpace(experimentType) ||
    embeddingModelId.HasValue;

return View(pagedRuns);
```

Keep `latestSummary` based on all completed runs, not filtered runs.

- [ ] **Step 2: Update Evaluation index view model and filter form**

Change first line:

```cshtml
@model PRN222.BLL.DTOs.PagedResult<PRN222.BLL.DTOs.BenchmarkRunDto>
```

Add view variables:

```cshtml
@{
    var search = ViewBag.Search as string;
    var selectedStatus = ViewBag.SelectedStatus as string;
    var selectedExperimentType = ViewBag.SelectedExperimentType as string;
    var selectedEmbeddingModelId = ViewBag.SelectedEmbeddingModelId as int?;
    var routeValues = ViewBag.RouteValues as IReadOnlyDictionary<string, string?>
        ?? new Dictionary<string, string?>();
    var hasActiveFilters = ViewBag.HasActiveFilters as bool? ?? false;
}
```

Insert a filter panel before the benchmark history panel:

```cshtml
<section class="panel filter-panel">
    <form method="get" class="filter-grid">
        <div>
            <label class="form-label">Search runs</label>
            <input name="search" value="@search" class="form-control" />
        </div>
        <div>
            <label class="form-label">Status</label>
            <select name="status" class="form-select">
                <option value="">All statuses</option>
                <option value="Completed" selected="@(selectedStatus == "Completed")">Completed</option>
                <option value="CompletedWithErrors" selected="@(selectedStatus == "CompletedWithErrors")">Completed with errors</option>
                <option value="Running" selected="@(selectedStatus == "Running")">Running</option>
                <option value="Failed" selected="@(selectedStatus == "Failed")">Failed</option>
            </select>
        </div>
        <div>
            <label class="form-label">Type</label>
            <select name="experimentType" class="form-select">
                <option value="">All types</option>
                <option value="RAG" selected="@(selectedExperimentType == "RAG")">RAG</option>
                <option value="FineTuned" selected="@(selectedExperimentType == "FineTuned")">Fine-tuned</option>
            </select>
        </div>
        <div>
            <label class="form-label">Embedding</label>
            <select name="embeddingModelId" class="form-select">
                <option value="">All models</option>
                @if (embeddingModels != null)
                {
                    foreach (var embeddingModel in embeddingModels)
                    {
                        <option value="@embeddingModel.Id" selected="@(selectedEmbeddingModelId == embeddingModel.Id)">
                            @embeddingModel.Name
                        </option>
                    }
                }
            </select>
        </div>
        <div class="filter-actions">
            <button type="submit" class="btn btn-outline-secondary">
                <i class="bi bi-funnel"></i> Apply
            </button>
        </div>
    </form>
    @if (hasActiveFilters)
    {
        <div class="filter-summary">
            <span>Filters active.</span>
            <a asp-action="Index">Clear all</a>
        </div>
    }
</section>
```

Replace `Model.Count >= 2`, `Model.Any()`, and `foreach (var run in Model)` with:

```cshtml
Model.TotalItems >= 2
Model.Items.Any()
foreach (var run in Model.Items)
```

After the table, add:

```cshtml
<partial name="_Pager" model="@(new { Paged = Model, RouteValues = routeValues, Label = "runs" })" />
```

Update empty state text:

```cshtml
<h3>@(hasActiveFilters ? "No benchmark runs match these filters" : "No evaluation runs yet")</h3>
```

Add clear filters button in empty state when filters are active:

```cshtml
@if (hasActiveFilters)
{
    <a asp-action="Index" class="btn btn-outline-secondary">
        <i class="bi bi-x-circle"></i> Clear filters
    </a>
}
```

- [ ] **Step 3: Build and manually smoke-check Evaluation page**

Run:

```powershell
dotnet build src\PRN222_Assignment1.sln
```

Manual URLs:

```text
/Evaluation?page=1&pageSize=10
/Evaluation?status=Completed&page=2&pageSize=10
/Evaluation?experimentType=RAG&search=gemini
```

Expected: filters combine, page links keep filters, compare selected uses visible checked rows.

- [ ] **Step 4: Commit**

```powershell
git add src\PRN222.MVC\Controllers\EvaluationController.cs src\PRN222.MVC\Views\Evaluation\Index.cshtml
git commit -m "feat: paginate evaluation history"
```

---

### Task 5: Paginate Run Detail Results

**Files:**
- Create: `src/PRN222.MVC/Models/Evaluation/BenchmarkRunDetailViewModel.cs`
- Modify: `src/PRN222.MVC/Controllers/EvaluationController.cs`
- Modify: `src/PRN222.MVC/Views/Evaluation/RunDetail.cshtml`

- [ ] **Step 1: Create Run Detail MVC view model**

Create `src/PRN222.MVC/Models/Evaluation/BenchmarkRunDetailViewModel.cs`:

```csharp
using PRN222.BLL.DTOs;

namespace PRN222.MVC.Models.Evaluation;

public sealed class BenchmarkRunDetailViewModel
{
    public required BenchmarkSummaryDto Summary { get; init; }
    public required PagedResult<BenchmarkResultDto> Results { get; init; }
}
```

- [ ] **Step 2: Update RunDetail action**

Add using:

```csharp
using PRN222.MVC.Models.Evaluation;
```

Change action signature:

```csharp
public async Task<IActionResult> RunDetail(int runId, int page = 1, int pageSize = 25)
```

Before return:

```csharp
var pagedResults = PagedResult<BenchmarkResultDto>.Create(
    summary.Results.OrderBy(r => r.Id),
    page,
    pageSize);

ViewBag.RouteValues = new Dictionary<string, string?>
{
    ["runId"] = runId.ToString(),
    ["pageSize"] = pagedResults.PageSize.ToString()
};

return View(new BenchmarkRunDetailViewModel
{
    Summary = summary,
    Results = pagedResults
});
```

- [ ] **Step 3: Update RunDetail view**

Change model:

```cshtml
@model PRN222.MVC.Models.Evaluation.BenchmarkRunDetailViewModel
```

Add view variables:

```cshtml
@{
    var summary = Model.Summary;
    var routeValues = ViewBag.RouteValues as IReadOnlyDictionary<string, string?>
        ?? new Dictionary<string, string?>();
}
```

Replace `Model.Run`, `Model.AvgFaithfulness`, `Model.Results` references with `summary.Run`, `summary.AvgFaithfulness`, and `Model.Results`.

Examples:

```cshtml
ViewData["Title"] = "Benchmark Detail: " + summary.Run.Name;
```

```cshtml
@if (!Model.Results.Items.Any())
```

```cshtml
@foreach (var result in Model.Results.Items)
```

After the table responsive block, add:

```cshtml
<partial name="_Pager" model="@(new { Paged = Model.Results, RouteValues = routeValues, Label = "questions" })" />
```

Keep CSV link:

```cshtml
<a asp-action="ExportCsv" asp-route-runId="@summary.Run.Id" class="btn btn-outline-primary">
```

- [ ] **Step 4: Build and manually smoke-check Run Detail**

Run:

```powershell
dotnet build src\PRN222_Assignment1.sln
```

Manual URLs:

```text
/Evaluation/RunDetail/1?page=1&pageSize=10
/Evaluation/RunDetail/1?page=3&pageSize=10
```

Expected: metric cards stay unchanged, question table paginates, CSV export still downloads all results.

- [ ] **Step 5: Commit**

```powershell
git add src\PRN222.MVC\Models\Evaluation\BenchmarkRunDetailViewModel.cs src\PRN222.MVC\Controllers\EvaluationController.cs src\PRN222.MVC\Views\Evaluation\RunDetail.cshtml
git commit -m "feat: paginate benchmark run details"
```

---

### Task 6: Paginate Test Set Preview JSON And UI

**Files:**
- Modify: `src/PRN222.BLL/Services/Interfaces/ITestSetGeneratorService.cs`
- Modify: `src/PRN222.BLL/Services/TestSetGeneratorService.cs`
- Modify: `src/PRN222.MVC/Controllers/TestSetGeneratorController.cs`
- Modify: `src/PRN222.MVC/Views/TestSetGenerator/Index.cshtml`
- Test: `src/PRN222.Tests/MVC/TestSetGeneratorControllerTests.cs`

- [ ] **Step 1: Add failing controller test for paged preview shape**

Extend `src/PRN222.Tests/MVC/TestSetGeneratorControllerTests.cs` with:

```csharp
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services.Interfaces;
using PRN222.MVC.Controllers;
using PRN222.BLL.DTOs;
using Xunit;

namespace PRN222.Tests.MVC;

public class TestSetGeneratorControllerTests
{
    [Fact]
    public async Task Preview_ShouldReturnPagedQaPairs()
    {
        var generator = new Mock<ITestSetGeneratorService>();
        var jobs = new Mock<ITestSetGenerationJobManager>();
        var scopeFactory = new Mock<IServiceScopeFactory>();

        var page = PagedResult<QAPairDto>.CreateFromPage(
            new[]
            {
                new QAPairDto
                {
                    Id = 10,
                    CourseId = 1,
                    DocumentChunkId = 99,
                    Question = "What is RAG?",
                    Answer = "Retrieval augmented generation.",
                    CreatedAt = DateTime.UtcNow
                }
            },
            page: 2,
            pageSize: 10,
            totalItems: 11);

        generator.Setup(s => s.GetAutoGeneratedPreviewAsync(1, 2, 10))
            .ReturnsAsync(page);

        var controller = new TestSetGeneratorController(
            generator.Object,
            jobs.Object,
            scopeFactory.Object,
            new Mock<ILogger<TestSetGeneratorController>>().Object);

        var result = await controller.Preview(courseId: 1, page: 2, pageSize: 10);

        var json = result.Should().BeOfType<JsonResult>().Subject;
        var model = json.Value.Should().BeAssignableTo<PagedResult<QAPairDto>>().Subject;
        model.Page.Should().Be(2);
        model.TotalItems.Should().Be(11);
        model.Items.Should().ContainSingle(q => q.Id == 10);
    }
}
```

If this file already contains another `TestSetGeneratorControllerTests` class, merge the test method and using directives into that class rather than creating a duplicate class.

- [ ] **Step 2: Run failing test**

Run:

```powershell
dotnet test src\PRN222_Assignment1.sln --filter Preview_ShouldReturnPagedQaPairs
```

Expected: FAIL because the interface/controller still use `limit`.

- [ ] **Step 3: Update interface**

Modify `src/PRN222.BLL/Services/Interfaces/ITestSetGeneratorService.cs`:

```csharp
using PRN222.BLL.DTOs;
```

Replace:

```csharp
Task<IReadOnlyList<QAPairDto>> GetAutoGeneratedPreviewAsync(int courseId, int limit = 50);
```

with:

```csharp
Task<PagedResult<QAPairDto>> GetAutoGeneratedPreviewAsync(int courseId, int page = 1, int pageSize = 25);
```

- [ ] **Step 4: Implement paged service query**

Modify `TestSetGeneratorService.GetAutoGeneratedPreviewAsync`:

```csharp
public async Task<PagedResult<QAPairDto>> GetAutoGeneratedPreviewAsync(int courseId, int page = 1, int pageSize = 25)
{
    var normalizedPageSize = PagedResult<QAPairDto>.NormalizePageSize(pageSize);
    var query = _qaPairRepository.GetQueryable()
        .Where(q => q.CourseId == courseId && q.DocumentChunkId != null)
        .OrderByDescending(q => q.CreatedAt)
        .ThenByDescending(q => q.Id);

    var totalItems = await query.CountAsync();
    var totalPages = totalItems == 0
        ? 0
        : (int)Math.Ceiling(totalItems / (double)normalizedPageSize);
    var normalizedPage = page < 1 ? 1 : page;
    if (totalPages > 0 && normalizedPage > totalPages)
    {
        normalizedPage = totalPages;
    }

    var items = await query
        .Skip((normalizedPage - 1) * normalizedPageSize)
        .Take(normalizedPageSize)
        .Select(q => new QAPairDto
        {
            Id = q.Id,
            CourseId = q.CourseId,
            DocumentChunkId = q.DocumentChunkId,
            Question = q.Question,
            Answer = q.Answer,
            RelevanceScore = q.RelevanceScore,
            CreatedAt = q.CreatedAt
        })
        .ToListAsync();

    return PagedResult<QAPairDto>.CreateFromPage(items, normalizedPage, normalizedPageSize, totalItems);
}
```

- [ ] **Step 5: Update controller preview endpoint**

Modify `TestSetGeneratorController.Preview`:

```csharp
[HttpGet]
public async Task<IActionResult> Preview(int courseId = 1, int page = 1, int pageSize = 25)
{
    var qaPairs = await _testSetGeneratorService.GetAutoGeneratedPreviewAsync(courseId, page, pageSize);
    return Json(qaPairs);
}
```

- [ ] **Step 6: Update Test Set Generator view JavaScript**

In `src/PRN222.MVC/Views/TestSetGenerator/Index.cshtml`, add controls above the preview table:

```cshtml
<div class="d-flex align-items-center gap-2 flex-wrap">
    <span id="previewResultCount" class="result-count">Loading preview...</span>
    <select id="previewPageSize" class="form-select form-select-sm" style="width: auto;">
        <option value="10">10</option>
        <option value="25" selected>25</option>
        <option value="50">50</option>
        <option value="100">100</option>
    </select>
</div>
```

Add JS state near existing constants:

```javascript
let previewPage = 1;
let previewPageSize = 25;
let previewTotalPages = 0;
```

Replace `refreshPreview` with:

```javascript
async function refreshPreview() {
    const response = await fetch(`/TestSetGenerator/Preview?courseId=${courseId}&page=${previewPage}&pageSize=${previewPageSize}`);
    if (!response.ok) return;

    const page = await response.json();
    const qaPairs = page.items || [];
    const body = document.getElementById('qaPreviewBody');
    previewPage = page.page || 1;
    previewPageSize = page.pageSize || previewPageSize;
    previewTotalPages = page.totalPages || 0;

    document.getElementById('previewResultCount').textContent = page.totalItems > 0
        ? `Showing ${page.firstItemIndex}-${page.lastItemIndex} of ${page.totalItems} Q&A pairs`
        : 'No document-grounded Q&A generated yet';

    if (!qaPairs.length) {
        body.innerHTML = '<tr><td colspan="4" class="text-muted text-center py-4">No document-grounded Q&A generated yet.</td></tr>';
        renderPreviewPager();
        return;
    }

    body.innerHTML = qaPairs.map((qa, index) => `
        <tr>
            <td class="text-muted">${page.firstItemIndex + index}</td>
            <td><strong>${escapeHtml(qa.question)}</strong></td>
            <td class="text-muted">${escapeHtml(qa.answer)}</td>
            <td><span class="badge text-bg-light">#${escapeHtml(qa.documentChunkId)}</span></td>
        </tr>
    `).join('');

    renderPreviewPager();
}
```

Add `renderPreviewPager` after `refreshPreview`:

```javascript
function renderPreviewPager() {
    let pager = document.getElementById('previewPager');
    if (!pager) {
        pager = document.createElement('div');
        pager.id = 'previewPager';
        pager.className = 'pager-shell';
        document.querySelector('#qaPreviewBody').closest('.card-body').appendChild(pager);
    }

    const previousDisabled = previewPage <= 1 ? 'disabled' : '';
    const nextDisabled = previewTotalPages === 0 || previewPage >= previewTotalPages ? 'disabled' : '';

    pager.innerHTML = `
        <div class="pager-links">
            <button type="button" class="btn btn-outline-secondary btn-sm" ${previousDisabled} data-preview-page="${previewPage - 1}">
                <i class="bi bi-chevron-left"></i> Previous
            </button>
            <span class="result-count">Page ${previewTotalPages === 0 ? 0 : previewPage} of ${previewTotalPages}</span>
            <button type="button" class="btn btn-outline-secondary btn-sm" ${nextDisabled} data-preview-page="${previewPage + 1}">
                Next <i class="bi bi-chevron-right"></i>
            </button>
        </div>
    `;

    pager.querySelectorAll('[data-preview-page]').forEach(button => {
        button.addEventListener('click', async () => {
            previewPage = Number(button.getAttribute('data-preview-page'));
            await refreshPreview();
        });
    });
}
```

Add page-size event listener near existing listeners:

```javascript
document.getElementById('previewPageSize').addEventListener('change', async (event) => {
    previewPageSize = Number(event.target.value);
    previewPage = 1;
    await refreshPreview();
});
```

After clear auto-generated succeeds, set:

```javascript
previewPage = 1;
```

- [ ] **Step 7: Run tests**

Run:

```powershell
dotnet test src\PRN222_Assignment1.sln --filter Preview_ShouldReturnPagedQaPairs
dotnet test src\PRN222_Assignment1.sln
```

Expected: both PASS.

- [ ] **Step 8: Commit**

```powershell
git add src\PRN222.BLL\Services\Interfaces\ITestSetGeneratorService.cs src\PRN222.BLL\Services\TestSetGeneratorService.cs src\PRN222.MVC\Controllers\TestSetGeneratorController.cs src\PRN222.MVC\Views\TestSetGenerator\Index.cshtml src\PRN222.Tests\MVC\TestSetGeneratorControllerTests.cs
git commit -m "feat: paginate test set preview"
```

---

### Task 7: Full Verification

**Files:**
- No required source edits unless verification finds a defect.

- [ ] **Step 1: Run full automated test suite**

Run:

```powershell
dotnet test src\PRN222_Assignment1.sln
```

Expected: all tests pass.

- [ ] **Step 2: Start MVC app**

Run:

```powershell
dotnet run --project src\PRN222.MVC\PRN222.MVC.csproj
```

Expected: app starts and prints a localhost URL.

- [ ] **Step 3: Manual browser verification**

Check these URLs:

```text
/Document?page=1&pageSize=10
/Document?status=Indexed&page=1&pageSize=10
/Evaluation?page=1&pageSize=10
/Evaluation?status=Completed&experimentType=RAG&page=1&pageSize=10
/Evaluation/RunDetail/1?page=1&pageSize=10
/TestSetGenerator?courseId=1
```

Expected:

- Document list filters and pagination work together.
- Evaluation list filters and pagination work together.
- Compare selected still opens `/Evaluation/Compare?runIds=...` for checked visible rows.
- Run Detail metric cards remain visible and result rows paginate.
- Export CSV still exports all results for the run.
- Test Set preview changes page and page size without stopping stats/job polling.
- Empty states show clear recovery actions.

- [ ] **Step 4: Commit any verification fixes**

If verification required fixes:

```powershell
git add <fixed-files>
git commit -m "fix: polish pagination verification issues"
```

If no fixes were needed, do not create an empty commit.


