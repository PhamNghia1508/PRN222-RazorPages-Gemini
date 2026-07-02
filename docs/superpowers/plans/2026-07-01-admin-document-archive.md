# Admin Document Archive Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace destructive document deletion with an Admin-only archive operation that preserves records and excludes archived sources from new RAG retrieval.

**Architecture:** Reuse the string-backed `Document.Status` column by adding `DocumentStatus.Archived`; no schema migration is required. Replace the public delete service/handler with archive semantics, block archived documents from processing, and render role-specific archive UI while preserving HeadLecturer upload.

**Tech Stack:** ASP.NET Core Razor Pages, EF Core, ASP.NET Core Identity roles, xUnit, Moq, FluentAssertions.

---

### Task 1: Add archive status and service behavior

**Files:**
- Modify: `src/PRN222.DAL/Entities/Enums/DocumentStatus.cs`
- Modify: `src/PRN222.BLL/Services/Interfaces/IDocumentService.cs`
- Modify: `src/PRN222.BLL/Services/DocumentService.cs`
- Test: `src/PRN222.Tests/Services/DocumentServiceTests.cs`

- [ ] Add failing tests proving archive sets `Status = Archived`, updates `UpdatedAt`, saves once, emits an `archived` notification, and never calls repository delete.
- [ ] Add failing tests proving `Processing` cannot be archived and `Archived` is idempotent.
- [ ] Add failing tests proving archived documents cannot be processed or enqueued.
- [ ] Run focused service tests and confirm RED.
- [ ] Add `Archived` to the enum and replace `DeleteDocumentAsync` with `ArchiveDocumentAsync`.
- [ ] Guard `ProcessDocumentAsync` and `EnqueueProcessDocumentAsync` against archived documents.
- [ ] Run focused service tests and confirm GREEN.

### Task 2: Replace delete handler with Admin-only archive handler

**Files:**
- Modify: `src/PRN222.Web/Pages/Document/Details.cshtml.cs`
- Modify: `src/PRN222.Tests/Web/RagRazorPagesTests.cs`
- Modify: `src/PRN222.Tests/Web/AuthorizationConfigurationTests.cs`

- [ ] Add failing reflection/source assertions for `OnPostArchiveAsync`, `ApplicationRoles.Admin`, `Forbid()`, and absence of `OnPostDeleteAsync`.
- [ ] Run focused web tests and confirm RED.
- [ ] Implement the Admin-only archive handler with friendly success/error messages.
- [ ] Run focused web tests and confirm GREEN.

### Task 3: Add archive UI and status labels

**Files:**
- Modify: `src/PRN222.Web/Pages/Document/Details.cshtml`
- Modify: `src/PRN222.Web/Pages/Document/_DocumentWorkspace.cshtml`
- Test: `src/PRN222.Tests/Web/RagRazorPagesTests.cs`

- [ ] Add failing source assertions for `Tạm ẩn khỏi RAG`, `Đã tạm ẩn`, the approved confirmation message, Admin-only rendering, and absence of the old Delete handler.
- [ ] Run focused tests and confirm RED.
- [ ] Render archive action only for Admin and only for terminal non-archived documents.
- [ ] Add archived badge mapping to detail and list views.
- [ ] Ensure HeadLecturer retains process/upload but no delete/archive action.
- [ ] Run focused tests and confirm GREEN.

### Task 4: Verify retrieval safety

**Files:**
- Modify: `src/PRN222.Tests/Services/RagRetrievalServiceTests.cs`
- Inspect: `src/PRN222.BLL/Services/Rag/RagRetrievalService.cs`

- [ ] Extend retrieval test data with an archived document and verify its ID is never requested from the chunk repository.
- [ ] Run the focused retrieval test; retain production code unchanged when the existing `Status == Indexed` filter passes.

### Task 5: Full and manual verification

- [ ] Run `dotnet build src/PRN222_Assignment1.sln -c Release --no-restore`.
- [ ] Run `dotnet test src/PRN222_Assignment1.sln -c Release --no-build --no-restore`.
- [ ] Restart Debug server.
- [ ] Login as Admin, archive one eligible local document, confirm record/detail remains and badge becomes `Đã tạm ẩn`.
- [ ] Confirm archived document is absent from a new retrieval candidate set without deleting chunks, citations, history, or files.
- [ ] Login as HeadLecturer and verify upload remains available while archive/delete is absent.
- [ ] Verify Lecturer/Student cannot access archive action.
