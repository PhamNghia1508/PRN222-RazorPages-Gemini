# Document Archive Audit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add accountable, non-destructive document archiving with a required reason, authenticated actor, UTC timestamp, previous status, and safe legacy rendering.

**Architecture:** Extend `Document` with nullable audit columns so existing archived rows remain valid, enforce the stronger contract in `DocumentService`, and source the actor only from the authenticated Razor Page claim. Keep the existing soft-archive behavior and all related document data intact. Render one shared archive-reason modal pattern and audit fallback text without adding restore behavior.

**Tech Stack:** ASP.NET Core Razor Pages, EF Core, ASP.NET Core Identity, xUnit, Moq, FluentAssertions, Bootstrap.

---

### Task 1: Lock the archive service contract with failing tests

**Files:**
- Modify: `src/PRN222.Tests/Services/DocumentServiceTests.cs`
- Modify: `src/PRN222.BLL/Services/Interfaces/IDocumentService.cs`
- Modify: `src/PRN222.BLL/Services/DocumentService.cs`

- [ ] Add tests proving a successful archive trims and stores `ArchiveReason`, stores authenticated actor ID, UTC `ArchivedAt`, and the original status in `ArchivedFromStatus`.
- [ ] Add tests proving null/empty/whitespace reasons, reasons longer than 1000 characters, blank actor IDs, `Processing`, and already `Archived` states are rejected.
- [ ] Run the focused service tests and confirm they fail because the new signature and audit fields do not exist.
- [ ] Change the service contract to `ArchiveDocumentAsync(int id, string archivedByUserId, string archiveReason)`.
- [ ] Validate actor/reason before mutation; assign previous status before `Status = Archived`; call `SaveChangesAsync` exactly once and never call delete/file cleanup APIs.
- [ ] Run the focused tests and confirm they pass.

### Task 2: Add the nullable EF Core audit model

**Files:**
- Modify: `src/PRN222.DAL/Entities/Document.cs`
- Modify: `src/PRN222.DAL/Data/Configurations/DocumentConfiguration.cs`
- Create: `src/PRN222.DAL/Migrations/*_AddDocumentArchiveAudit.cs`
- Modify: `src/PRN222.DAL/Migrations/AppDbContextModelSnapshot.cs`

- [ ] Add nullable `ArchivedByUserId`, `ArchivedAt`, `ArchiveReason`, `ArchivedFromStatus`, and `ArchivedByUser`.
- [ ] Configure `ArchiveReason` as `nvarchar(1000)`, enum text as maximum 20 characters, the Identity FK with `DeleteBehavior.Restrict`, and an index on `ArchivedByUserId`.
- [ ] Generate migration `AddDocumentArchiveAudit`; inspect it to ensure every new column is nullable and no data is deleted or rewritten.
- [ ] Apply the migration with the DAL project and Web startup project.

### Task 3: Secure the Razor handler and expose audit details

**Files:**
- Modify: `src/PRN222.BLL/DTOs/DocumentDetailDto.cs`
- Modify: `src/PRN222.DAL/Repositories/DocumentRepository.cs`
- Modify: `src/PRN222.BLL/Services/DocumentService.cs`
- Modify: `src/PRN222.Web/Pages/Document/Details.cshtml.cs`
- Modify: `src/PRN222.Tests/Web/RagRazorPagesTests.cs`
- Modify: `src/PRN222.Tests/Web/AuthorizationConfigurationTests.cs`

- [ ] Add failing tests/source assertions proving the handler reads `ClaimTypes.NameIdentifier`, accepts only the reason from the form, remains Admin-only, and passes actor plus reason to the service.
- [ ] Include `ArchivedByUser` when loading details and project audit fields into `DocumentDetailDto`.
- [ ] Read actor ID from `User.FindFirstValue(ClaimTypes.NameIdentifier)` and return a clear TempData error for a missing/invalid claim.
- [ ] Run focused web and service tests.

### Task 4: Require a reason in the UI and render legacy-safe audit

**Files:**
- Modify: `src/PRN222.Web/Pages/Document/Details.cshtml`
- Modify: `src/PRN222.Web/Pages/Document/Index.cshtml`
- Modify: `src/PRN222.Web/Pages/Document/_DocumentWorkspace.cshtml`
- Modify: `src/PRN222.Tests/Web/RagRazorPagesTests.cs`

- [ ] Add failing assertions for a required, 1000-character archive reason control, audit fallback text, and absence of Restore controls.
- [ ] Replace direct archive submission with an Admin-only Bootstrap modal whose only client input is `archiveReason`.
- [ ] Show actor email, consistent local display time, trimmed reason, and previous status when audit exists; show explicit fallback values for legacy archived rows such as ID 41.
- [ ] Keep archive controls absent for non-Admin roles and keep Restore absent everywhere.
- [ ] Run focused Razor tests.

### Task 5: Verify migration, behavior, and regression safety

**Files:**
- Verify only; no additional production changes unless a failing test identifies a defect.

- [ ] Run `dotnet ef database update --project src/PRN222.DAL --startup-project src/PRN222.Web`.
- [ ] Run `dotnet build -c Release`.
- [ ] Run `dotnet test -c Release`.
- [ ] Run `git diff --check`.
- [ ] Restart the Debug server and manually verify Admin archive with a reason, audit display, old archived fallback, retained chunks/document/file, role restrictions, and no Restore.
- [ ] Inspect the final diff and report results without committing or pushing.
