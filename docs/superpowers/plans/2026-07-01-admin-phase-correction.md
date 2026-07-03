# Admin Phase Correction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Present Admin as the RAG/LMS system administrator while preserving routes, authorization behavior, document visibility, and dashboard performance.

**Architecture:** Keep the shared Razor layout and branch only the navigation rendered for `isAdmin`. Change static dashboard copy and the malformed benchmark status literal without adding services, database queries, policies, routes, or schema changes.

**Tech Stack:** ASP.NET Core Razor Pages, ASP.NET Core Identity roles, xUnit, FluentAssertions.

---

### Task 1: Lock Admin dashboard copy with tests

**Files:**
- Modify: `src/PRN222.Tests/Web/HomePageTests.cs`
- Modify: `src/PRN222.Tests/Web/OverviewDashboardFactoryTests.cs`

- [ ] Add a source-level Razor test asserting `Bảng điều khiển quản trị`, the approved description, `Tổng quan hệ thống`, and `Giám sát trải nghiệm sinh viên`, while rejecting `Bảng điều khiển giảng viên`.
- [ ] Add a factory test asserting indexed data produces `Sẵn sàng` and rejecting the malformed `Sáºµn sÃ ng` literal.
- [ ] Run the focused tests and verify they fail because production copy has not changed.

Run:

```powershell
dotnet test src/PRN222.Tests/PRN222.Tests.csproj --no-restore --filter "FullyQualifiedName~HomePageTests|FullyQualifiedName~OverviewDashboardFactoryTests"
```

Expected: FAIL on the new copy and encoding assertions.

### Task 2: Apply the minimal dashboard copy patch

**Files:**
- Modify: `src/PRN222.Web/Pages/Index.cshtml`
- Modify: `src/PRN222.Web/Models/Dashboard/OverviewDashboardFactory.cs`

- [ ] Replace the lecturer eyebrow and demo-oriented description with the approved Admin wording.
- [ ] Rename the first two tabs to `Tổng quan hệ thống` and `Giám sát trải nghiệm sinh viên`.
- [ ] Replace the malformed benchmark status literal with `Sẵn sàng`.
- [ ] Run the focused tests and verify they pass.

Expected: all focused tests PASS without changing dashboard queries.

### Task 3: Lock and implement Admin-only sidebar grouping

**Files:**
- Modify: `src/PRN222.Tests/Web/AuthorizationConfigurationTests.cs`
- Modify: `src/PRN222.Web/Pages/Shared/_Layout.cshtml`

- [ ] Add assertions for the Admin section labels and existing routes `/`, `/Course`, `/Department`, `/Admin/Accounts`, `/Document`, `/Finetune`, `/TestSetGenerator`, `/Evaluation`, and `/Knowledge`.
- [ ] Run the focused authorization test and verify it fails on missing Admin groups/routes.
- [ ] Render the grouped navigation only inside the existing `isAdmin` branch.
- [ ] Preserve the current navigation branch for HeadLecturer and Lecturer.
- [ ] Run the focused authorization test and verify it passes.

Run:

```powershell
dotnet test src/PRN222.Tests/PRN222.Tests.csproj --no-restore --filter "FullyQualifiedName~AuthorizationConfigurationTests"
```

Expected: PASS.

### Task 4: Verify document access and regression safety

**Files:**
- Inspect: `src/PRN222.Web/Pages/Document/Index.cshtml.cs`
- Inspect: `src/PRN222.BLL/Services/DocumentService.cs`
- Inspect: `src/PRN222.Web/Pages/Document/Upload.cshtml.cs`

- [ ] Confirm `/Document` allows `ApplicationRoles.Management`.
- [ ] Confirm Admin bypasses staff course scoping and receives the full document query.
- [ ] Confirm upload remains restricted to `ApplicationRoles.DocumentUpload` (`HeadLecturer`) and the Admin UI does not show an upload action.
- [ ] Do not alter this module when all three conditions already hold.

### Task 5: Full verification

- [ ] Run build:

```powershell
dotnet build src/PRN222_Assignment1.sln --no-restore
```

- [ ] Run tests:

```powershell
dotnet test src/PRN222_Assignment1.sln --no-build --no-restore
```

- [ ] Review `git diff` for unrelated changes, accidental route changes, new queries, and remaining malformed benchmark text.
- [ ] Report manual browser verification separately if the authenticated local app cannot be exercised in the current environment.
