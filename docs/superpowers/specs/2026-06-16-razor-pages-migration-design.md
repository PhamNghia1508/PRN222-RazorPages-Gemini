# Razor Pages Migration Design

## Goal

Convert the PRN222 RAG Workbench presentation layer from ASP.NET Core MVC to ASP.NET Core Razor Pages while keeping the original MVC source safe in the main checkout.

The migration work lives only in:

`C:\Users\ADMIN\Documents\FPT\PRN222_Assignment1\.worktrees\razor-pages-migration`

The original MVC source remains in:

`C:\Users\ADMIN\Documents\FPT\PRN222_Assignment1`

## Scope

The final Razor Pages branch must not depend on MVC controllers or MVC views for runtime behavior.

In scope:

- Replace `Controllers/*.cs` with Razor Page models under `src/PRN222.MVC/Pages`.
- Replace `Views/**/*.cshtml` with Razor Pages and shared Razor assets.
- Add `MapRazorPages()` immediately, but keep conventional MVC routing during the incremental migration.
- Preserve existing BLL, DAL, Identity, role authorization, SignalR hub, CSS, JavaScript, and seeded demo accounts.
- Preserve old user-facing URLs where practical by using explicit `@page` routes.
- Convert JSON, file, and form endpoints to Razor Page handlers.
- Keep anti-forgery behavior for form posts and authenticated JSON handlers.
- Keep the recent realtime empty-state encoding fix in the Razor Pages branch.

Out of scope:

- Rewriting BLL or DAL business logic.
- Changing the database schema unless Razor Pages exposes an existing hidden bug that requires a small fix.
- Redesigning the UI beyond route and handler adjustments needed by the migration.
- Pushing or merging the branch without explicit approval.

## Architecture

Razor Pages becomes the only page rendering mechanism by the end of the migration. During incremental migration, MVC controller/view registration and the default controller route remain active so existing routes keep working until their Razor Pages equivalents exist.

Each former MVC controller area maps to a folder under `Pages`, with page-specific `PageModel` classes containing request handling logic.

Service dependencies stay the same. Page models inject BLL services, `UserManager<ApplicationUser>`, `SignInManager<ApplicationUser>`, `IHubContext<AdminHub>`, and access services just like the controllers did.

SignalR remains mapped with `app.MapHub<AdminHub>("/hubs/admin")` because the hub is not an MVC controller.

## Route Strategy

Old URLs should remain stable where this reduces UI and JavaScript churn.

| Current MVC URL | Razor Pages target |
| --- | --- |
| `/` and `/Home` | `Pages/Index.cshtml` with `@page "/"` plus compatibility route if needed |
| `/Account/Login` | `Pages/Account/Login.cshtml` |
| `/Account/Register` | `Pages/Account/Register.cshtml` |
| `/Account/AccessDenied` | `Pages/Account/AccessDenied.cshtml` |
| `/Admin/Accounts` | `Pages/Admin/Accounts.cshtml` |
| `/Department` | `Pages/Department/Index.cshtml` |
| `/Course` | `Pages/Course/Index.cshtml` |
| `/Course/Create` | `Pages/Course/Create.cshtml` |
| `/Course/Edit/{id}` | `Pages/Course/Edit.cshtml` |
| `/Document` | `Pages/Document/Index.cshtml` |
| `/Document/Details/{id}` | `Pages/Document/Details.cshtml` |
| `/Document/Upload` | `Pages/Document/Upload.cshtml` |
| `/Chat` | `Pages/Chat/Index.cshtml` |
| `/Chat/Session/{id}` | `Pages/Chat/Session.cshtml` |
| `/Chat/New` | `Pages/Chat/New.cshtml` or `/Chat/Session` with new-session handler |
| `/Evaluation` | `Pages/Evaluation/Index.cshtml` |
| `/Evaluation/RunDetail/{runId}` | `Pages/Evaluation/RunDetail.cshtml` |
| `/Evaluation/Compare` | `Pages/Evaluation/Compare.cshtml` |
| `/Finetune` | `Pages/Finetune/Index.cshtml` |
| `/Finetune/Dataset` | `Pages/Finetune/Dataset.cshtml` |
| `/TestSetGenerator` | `Pages/TestSetGenerator/Index.cshtml` |

Handler routes can preserve old API-like paths either with explicit pages or query handlers. The preferred option is explicit pages when JavaScript already calls a fixed URL.

## Authorization

Authorization moves from controller attributes to page model attributes:

- `[Authorize(Roles = ApplicationRoles.Admin)]`
- `[Authorize(Roles = ApplicationRoles.Management)]`
- `[Authorize(Roles = ApplicationRoles.DocumentUpload)]`
- `[Authorize(Roles = ApplicationRoles.ModelOperations)]`
- `[Authorize(Roles = ApplicationRoles.ChatUsers)]`

Role-based UI visibility remains in shared layout and partials.

## Testing Strategy

Tests should prove three things:

1. Razor Pages are registered and mapped while MVC registration/routing remains during the interim migration phase.
2. Important Razor Pages routes exist and return expected status codes for authorized demo users.
3. Former JSON/file endpoints still work through Razor Page handlers.

The assertion that MVC controllers/views, MVC registration, and conventional MVC routing are removed belongs only to the final MVC surface removal task, after Razor Pages equivalents exist.

Existing service tests should stay unchanged. MVC-specific controller tests should be rewritten as Razor Pages routing, authorization, or integration-style tests.

## Completion Criteria

The migration is complete when:

- `src/PRN222.MVC/Controllers` is removed or empty.
- `src/PRN222.MVC/Views` is removed or contains no runtime MVC views.
- `Program.cs` maps Razor Pages and no longer maps the default controller route.
- `ServiceCollectionExtensions.AddApplicationMvc()` is renamed or updated to register Razor Pages only.
- `dotnet test src/PRN222_Assignment1.sln --no-restore` passes.
- A browser smoke test reaches login, dashboard, course management, document upload, chat, and test set generator pages.
