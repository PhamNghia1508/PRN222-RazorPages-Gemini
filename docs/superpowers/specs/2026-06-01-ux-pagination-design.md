# UX Pagination Design

## Goal

Improve the day-to-day usability of the high-volume MVC screens without turning the assignment into a custom data-grid project. The work covers Document, Evaluation, Run Detail, and Test Set Generator surfaces using server-side pagination, consistent result counts, preserved filters, and clear empty states.

## Selected Approach

Use server-side pagination with a shared pager UI.

This fits the current ASP.NET Core MVC three-layer structure. Controllers will accept query-string parameters, prepare one page of data, and pass a paged view model to Razor views. Views will render stable controls for search, filters, page size, and navigation while keeping the existing Warm Sand & Ink design language.

## Scope

### Document List

`/Document` will support:

- `page`, `pageSize`
- existing `courseId`, `status`, `fileType`, `search`
- result summary such as `Showing 1-25 of 137 documents`
- page-size choices: `10`, `25`, `50`, `100`
- clear-filter action when filters are active

The list should render only the current page. Existing actions such as Details, Process, and Delete remain unchanged.

### Evaluation History

`/Evaluation` will support:

- `page`, `pageSize`
- search by benchmark run name
- filter by status
- filter by experiment type
- filter by embedding model
- shared pager and result count

The compare checkbox behavior applies to visible rows on the current page only. This avoids unclear cross-page selection state.

### Run Detail Results

`/Evaluation/RunDetail/{runId}` will keep the metric cards and run metadata at the top. The question-level results table will support:

- `page`, `pageSize`
- result count for question rows
- shared pager
- existing CSV export for the full run

The table can still truncate long generated answers, but it should not render all result rows at once.

### Test Set Generator Preview

The generated Q&A preview will change from a fixed `limit=50` request to paged JSON:

- request: `courseId`, `page`, `pageSize`
- response: `items`, `totalItems`, `page`, `pageSize`, `totalPages`

The page will show a compact preview pager and `Showing x-y of z Q&A pairs`. Stats and job progress polling remain active. Preview refresh should keep the current page when possible and clamp to a valid page if data is cleared or regenerated.

## Shared Components

### Paged Model

Introduce a small shared model for MVC views:

- `Items`
- `Page`
- `PageSize`
- `TotalItems`
- `TotalPages`
- `HasPreviousPage`
- `HasNextPage`
- `FirstItemIndex`
- `LastItemIndex`

The model should clamp invalid page and page-size values. Page size should be limited to `10`, `25`, `50`, and `100`.

### Pager Partial

Create a reusable Razor partial, for example `_Pager.cshtml`, that:

- renders Previous/Next controls
- renders nearby page numbers
- preserves existing route/query values
- includes a page-size selector
- shows a concise result count
- handles empty result sets without broken controls

### Styling

Add focused CSS classes that match the current design system:

- `.pager-shell`
- `.pager-links`
- `.page-size-control`
- `.result-count`
- `.filter-summary`

No new design framework or JavaScript grid library is introduced.

## Controller Behavior

Controllers should normalize input:

- `page < 1` becomes `1`
- unsupported `pageSize` becomes default `25`
- page beyond `TotalPages` becomes the last available page
- empty result sets keep `page = 1`

Query strings must be preserved when navigating pages so users do not lose search/filter context.

## Empty And Error States

Filtered empty states should make the recovery action obvious:

- Documents: `No matching documents` with clear filters and upload action
- Evaluation: `No benchmark runs match these filters` with clear filters and run-new-benchmark action
- Run Detail: `No question-level results available`
- Test Set Preview: `No document-grounded Q&A generated yet`

If background generation or benchmark state changes reduce the item count, pagination should clamp gracefully instead of showing an empty invalid page.

## Testing

Add focused tests for:

- page-size validation
- page clamping
- total page calculation
- query preservation in generated pager links where practical
- Test Set preview paged JSON shape

Run the full suite:

```powershell
dotnet test src\PRN222_Assignment1.sln
```

Manual verification:

- Document filters and pagination combine correctly
- Evaluation filters and compare button still work
- Run Detail paginates results while CSV export still exports all rows
- Test Set preview updates while job status polling remains active

## Out Of Scope

- AJAX data-grid replacement for Document and Evaluation tables
- cross-page compare selection
- database schema changes
- changing benchmark, embedding, or generation algorithms
- redesigning the app navigation or visual theme
