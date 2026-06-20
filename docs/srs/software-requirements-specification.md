# Software Requirements Specification: PRN222 RAG Workbench

## Document Control

| Field | Value |
|---|---|
| Document identifier | PRN222-RAG-SRS |
| Document title | Software Requirements Specification for PRN222 RAG Workbench |
| Version | 1.3 |
| Revision date | 2026-06-15 |
| Status | Draft for academic submission |
| Source baseline | Department-governance implementation through commit `7ae9ed8`; SRS alignment changes are part of the same delivery |
| Primary source of truth | Implemented source code, validated requirement catalog, and source evidence map |

### Revision History

| Version | Date | Description |
|---|---|---|
| 1.0 | 2026-06-07 | Initial code-based SRS draft derived from repository evidence and validated requirements. |
| 1.1 | 2026-06-09 | Finalized traceability, diagrams, document generation, and academic-submission metadata. |
| 1.2 | 2026-06-15 | Merged SRS sources into main and aligned login-page Lecturer copy with enforced authorization. |
| 1.3 | 2026-06-15 | Added department-governed staff scope, transactional stale-assignment revocation, Admin governance UI, and legacy assignment cleanup evidence. |

## 1. Introduction

### 1.1 Purpose

This Software Requirements Specification defines the requirements for PRN222 RAG Workbench, an ASP.NET Core MVC web application that supports course-scoped document ingestion, retrieval-augmented chat, test-set generation, benchmarking, and fine-tuning dataset preparation for Vietnamese academic course materials. The document is intended for academic evaluation and is derived from repository evidence, not from unsupported feature assumptions.

### 1.2 Scope

The system enables registered users to authenticate, interact with course materials through RAG chat, and, depending on role, manage departments, courses, staff assignments, documents, model-operation workflows, generated Q and A pairs, benchmark runs, and fine-tuning datasets. Current release scope includes Identity authentication, role-based authorization, department-governed staff provisioning, effective course scope, document upload and processing, RAG chat with citations, test-set generation, benchmark workflows, fine-tuning JSONL export, and role-aware navigation.

Out of scope are detailed class-level design, full framework-managed Identity table documentation, enterprise SSO, institutional compliance certification, verified production response-time or uptime claims, and unsupported AI-quality or benchmark results.

### 1.3 Intended Audience

The primary audience is the course professor or academic evaluator. Secondary audiences include project developers, testers, maintainers, and future team members who need to understand the system behavior without reading every source file.

### 1.4 Definitions, Acronyms, and Abbreviations

| Term | Definition |
|---|---|
| Admin | Application role with full management scope for courses, staff, documents, and model-operation workflows. |
| Department | Administrative boundary that groups staff and courses. Staff access requires matching department membership in addition to explicit course assignment. |
| HeadLecturer | Staff role that may operate document and model workflows for same-department assigned courses only. |
| Lecturer | Staff role with same-department assigned-course read and chat access; it cannot upload documents, view raw chunks, or run model-operation workflows. |
| Student | Publicly registered role that uses RAG chat. The current implementation exposes the current complete course list for Chat. |
| RAG | Retrieval-Augmented Generation, where an LLM answer is generated using retrieved document context. |
| Chunk | A segment of extracted document text stored for retrieval and embedding. |
| Embedding | Vector representation of text used for semantic retrieval. |
| Q and A pair | A generated or manual question-answer pair used for test sets, evaluation, or fine-tuning dataset preparation. |
| MVC | Model-View-Controller web application pattern used by ASP.NET Core MVC. |
| EF Core | Entity Framework Core, the object-relational mapper used for SQL Server persistence. |
| JSONL | Newline-delimited JSON format used for exported fine-tuning data. |
| ASVS | OWASP Application Security Verification Standard. |
| WCAG | Web Content Accessibility Guidelines. |

### 1.5 References

| Reference | Description |
|---|---|
| `docs/srs/source-evidence.md` | Repository-backed evidence map for this SRS. |
| `docs/srs/requirements.json` | Machine-validated functional and non-functional requirement catalog. |
| `docs/superpowers/specs/2026-06-07-code-based-srs-design.md` | Approved document design specification. |
| ISO/IEC/IEEE 29148:2018 | Requirements engineering and requirements information-item guidance. |
| ISO/IEC 25010:2023 | Product quality model used to frame non-functional quality characteristics. |
| OWASP ASVS | Security verification reference used as a production target, not a certified current claim. |
| WCAG 2.2 | Accessibility reference used as a browser-interface target, not a certified current claim. |

### 1.6 Document Organization

Section 2 describes the product context. Section 3 defines roles, course scoping, and authorization. Section 4 lists functional requirements. Sections 5 and 6 describe interfaces and data. Section 7 lists non-functional requirements. Section 8 defines verification, use cases, and traceability. Section 9 records limitations and future scope.

## 2. Overall Description

### 2.1 Product Perspective

PRN222 RAG Workbench is a layered web application. The presentation layer is ASP.NET Core MVC with Razor views. Business logic is organized in BLL services. Persistence is implemented through DAL entities, repositories, EF Core configuration, and SQL Server migrations. Automated tests are located in the test project. Uploaded course documents are converted into extracted text, chunks, embeddings, and searchable evidence used by chat, benchmark, and dataset workflows.

### 2.2 Major Capabilities

- User authentication, registration, logout, and protected endpoint challenge behavior.
- Admin-only department creation, staff account creation, and governed department/course assignment.
- Admin-only course creation, update, and conditional deletion.
- Staff course-scope filtering that requires same-department membership and explicit assignment.
- Admin and HeadLecturer document upload subject to role and course scope.
- Document validation, extraction, chunking, embedding, indexing, reprocessing, and deletion.
- Role-restricted raw text and raw chunk visibility.
- RAG chat with course selection, retrieved evidence, citations, and user-owned sessions.
- Course-scoped test-set generation, benchmark runs, and fine-tuning dataset export.

### 2.3 User Classes

| User class | Summary |
|---|---|
| Admin | Manages departments, courses, staff accounts, assignments, documents, benchmark workflows, test sets, fine-tuning datasets, and full visibility. |
| HeadLecturer | Operates same-department assigned-course document upload, indexing, test-set, benchmark, and fine-tuning workflows. |
| Lecturer | Reads same-department assigned-course resources and uses scoped chat, without upload, model operations, or raw chunk access. |
| Student | Registers through the public flow and uses Chat with the course list exposed by the current implementation. |

### 2.4 Operating Environment

The verified runtime environment is .NET 8. The web layer uses ASP.NET Core MVC and ASP.NET Core Identity. Persistence uses EF Core with SQL Server. Uploaded files are stored below the application data upload directory. The application integrates with external AI services for LLM responses, embeddings, optional embedding model comparison, and fine-tuned inference.

### 2.5 Constraints

- Application and test projects target `net8.0`.
- SQL Server is the configured EF Core database provider.
- The MVC request limit for uploads is 50 MiB.
- Supported upload extensions are PDF, DOCX, PPTX, and legacy PPT.
- Legacy PPT extraction is best-effort readable-string recovery, not structured slide parsing.
- Default document ingestion uses fixed chunking defaults.
- External AI services remain runtime dependencies.
- Development seeding behavior must not be treated as production credential management.

### 2.6 Assumptions and Dependencies

- A compatible .NET 8 SDK and runtime are available.
- SQL Server connectivity is configured.
- Required provider API keys or endpoints are configured for enabled AI providers.
- Uploaded course documents are permitted for academic processing.
- Staff course assignments are maintained by Admin users.
- Browser users have network access to the MVC application.

### 2.7 Out of Scope

- Student enrollment modeling separate from staff course assignment.
- Enterprise SSO or institutional directory synchronization.
- Durable distributed background job processing with restart recovery.
- Certified security or accessibility compliance.
- Verified production response-time, throughput, availability, or AI-quality thresholds.
- External RAGAS framework integration as a confirmed implementation.
- Offline local LLM and embedding fallback.

## 3. System Context and Authorization Model

### 3.1 System Context

The system boundary is the ASP.NET Core MVC application. Human actors interact through browser UI. Internal services coordinate authentication, authorization, course and document management, document processing, retrieval, chat, evaluation, and dataset workflows. SQL Server stores application and Identity data. Uploaded files are stored on the application file system. External AI providers supply LLM generation, embeddings, and configured fine-tuned inference.

### 3.2 Role-Permission Matrix

| Capability | Admin | HeadLecturer | Lecturer | Student |
|---|---:|---:|---:|---:|
| Sign in, sign out | Yes | Yes | Yes | Yes |
| Self-register through public registration | Student role only | Student role only | Student role only | Yes |
| Create Lecturer or HeadLecturer accounts | Yes | No | No | No |
| Create departments and assign staff/courses | Yes | No | No | No |
| Assign staff department and courses | Yes | No | No | No |
| Create, update, and conditionally delete courses | Yes | No | No | No |
| View management course list | All courses | Same-department assigned courses | Same-department assigned courses | No management controller scope |
| Select Chat courses | All courses | Same-department assigned courses | Same-department assigned courses | Current complete course list |
| View document metadata | All courses | Same-department assigned courses | Same-department assigned courses | No document-management access |
| View extracted text and raw chunks | Yes | Same-department assigned courses | No | No |
| Upload, process, reprocess, or delete documents | Yes | Same-department assigned courses | No | No |
| Use RAG chat | Yes | Same-department assigned courses | Same-department assigned courses | Current complete course list |
| Generate test sets | Yes | Same-department assigned courses | No | No |
| Generate fine-tuning datasets | Yes | Same-department assigned courses | No | No |
| Run, compare, or export benchmarks | Yes | Same-department assigned courses | No | No |
| See combined staff and department administration navigation | Yes | No | No | No |

### 3.3 Course-Scoping Rules

Admin is not course-limited by staff assignment. For HeadLecturer and Lecturer, effective course scope is the intersection of explicit assignments and courses whose department matches the staff account's non-null department. HeadLecturer may upload documents and run model-operation workflows only inside that effective scope. Lecturer may view management data and use chat inside that scope, but cannot upload, view raw chunks, generate test sets, run benchmarks, or generate fine-tuning datasets. Department changes revoke stale assignments transactionally. Student does not use the staff assignment model; the current Chat course query exposes the current complete course list to Student users.

### 3.4 UI Visibility and Backend Enforcement

The UI hides actions that do not correspond to the authenticated user role. This improves usability, but it is not the security boundary. Server-side controller attributes, Identity role checks, and course-scope validation are authoritative. A hidden navigation item must not be treated as sufficient protection for the corresponding endpoint.

## 4. Functional Requirements

This section renders every functional requirement from the validated requirement catalog. Requirement identifiers are preserved exactly.

### 4.1 Authentication and Session Management

#### FR-AUTH-001 - Authenticate registered users

- Statement: The system shall authenticate a registered user by email and password.
- Type: functional
- Source: `src/PRN222.MVC/Controllers/AccountController.cs`
- Traces to: `UC-AUTH-01`
- Priority: Must
- Roles: Admin, HeadLecturer, Lecturer, Student
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - Valid credentials create an authenticated session.
  - Invalid credentials do not create an authenticated session.

#### FR-AUTH-002 - Register student accounts

- Statement: The system shall assign the Student role to every account created through public registration.
- Type: functional
- Source: `src/PRN222.MVC/Controllers/AccountController.cs`
- Traces to: `UC-AUTH-02`
- Priority: Must
- Roles: Student
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - A successful public registration creates an Identity user.
  - The created user belongs to Student and no staff role.

#### FR-AUTH-003 - Terminate authenticated sessions

- Statement: The system shall sign out an authenticated user when the logout action is submitted.
- Type: functional
- Source: `src/PRN222.MVC/Controllers/AccountController.cs`
- Traces to: `UC-AUTH-03`
- Priority: Must
- Roles: Admin, HeadLecturer, Lecturer, Student
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - Logout invalidates the current authentication session.
  - The user is redirected to the login page.

#### FR-AUTH-004 - Challenge unauthenticated chat access

- Statement: The system shall challenge unauthenticated requests to protected Chat endpoints through the configured cookie login path.
- Type: functional
- Source: `src/PRN222.MVC/Infrastructure/ServiceCollectionExtensions.cs`, `src/PRN222.MVC/Controllers/ChatController.cs`
- Traces to: `UC-AUTH-04`
- Priority: Must
- Roles: Admin, HeadLecturer, Lecturer, Student
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - ChatController is protected for the implemented ChatUsers roles.
  - An unauthenticated request to a protected Chat action is challenged before the action executes.
  - The cookie challenge uses the configured /Account/Login path.

### 4.2 Administration and Staff Provisioning

#### FR-ADMIN-001 - Create staff accounts

- Statement: The system shall allow Admin to create Lecturer and HeadLecturer accounts with a department and at least one course from that department.
- Type: functional
- Source: `src/PRN222.MVC/Controllers/AdminController.cs`, `src/PRN222.BLL/Services/CourseAssignmentService.cs`
- Traces to: `UC-ADMIN-01`
- Priority: Must
- Roles: Admin
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - Admin can submit valid staff account data with a department and at least one same-department course.
  - The resulting account has exactly the selected Lecturer or HeadLecturer role, department, and validated course assignments.
  - An existing email or cross-department course selection is rejected without mutating the existing account.

#### FR-ADMIN-002 - Restrict staff account creation

- Statement: The system shall deny non-Admin users access to staff account creation.
- Type: functional
- Source: `src/PRN222.MVC/Controllers/AdminController.cs`
- Traces to: `UC-ADMIN-01`
- Priority: Must
- Roles: HeadLecturer, Lecturer, Student
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - A non-Admin request cannot execute a staff creation action.
  - No staff account is persisted from the denied request.

#### FR-ADMIN-003 - Assign staff department and courses

- Statement: The system shall allow Admin to replace the department and same-department course assignments of a Lecturer or HeadLecturer.
- Type: functional
- Source: `src/PRN222.MVC/Controllers/AdminController.cs`, `src/PRN222.BLL/Services/CourseAssignmentService.cs`
- Traces to: `UC-ADMIN-02`
- Priority: Must
- Roles: Admin
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - A department and at least one course are required for staff.
  - Every submitted course belongs to the submitted department.
  - After a successful update, the user department and persisted assignments match the submitted values in one transaction.

#### FR-ADMIN-004 - Restrict assignable staff roles

- Statement: The system shall reject an Admin staff-creation request whose selected role is not Lecturer or HeadLecturer.
- Type: functional
- Source: `src/PRN222.MVC/Controllers/AdminController.cs`, `src/PRN222.MVC/Infrastructure/ApplicationRoles.cs`
- Traces to: `UC-ADMIN-01`
- Priority: Must
- Roles: Admin
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - Submitting Admin or Student as the staff role produces validation failure.
  - No user is created for an unsupported staff role.

#### FR-ADMIN-005 - Manage departments

- Statement: The system shall allow Admin to create departments and assign Lecturer, HeadLecturer, and courses to a department.
- Type: functional
- Source: `src/PRN222.MVC/Controllers/DepartmentController.cs`, `src/PRN222.BLL/Services/DepartmentService.cs`
- Traces to: `UC-ADMIN-02`
- Priority: Must
- Roles: Admin
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - Admin can create a department with a non-empty name and code.
  - Admin can move an eligible staff account or course into a selected department.
  - Non-Admin users cannot execute department administration actions.

#### FR-ADMIN-006 - Reconcile stale department assignments

- Statement: The system shall revoke staff-course assignments that become invalid when a staff account or course changes department.
- Type: functional
- Source: `src/PRN222.BLL/Services/DepartmentService.cs`, `src/PRN222.BLL/Services/CourseAccessService.cs`, `src/PRN222.DAL/Migrations/20260615102155_EnforceDepartmentCourseAssignments.cs`
- Traces to: `UC-ADMIN-02`
- Priority: Must
- Roles: Admin
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - A staff or course department change removes assignments whose departments no longer match.
  - The relationship change and stale-assignment deletion commit or roll back together.
  - The one-time migration deletes legacy assignments with missing or mismatched departments.

### 4.3 Course Management and Assignment Scope

#### FR-COURSE-001 - Create courses

- Statement: The system shall allow Admin to create a course with a non-empty name.
- Type: functional
- Source: `src/PRN222.MVC/Controllers/CourseController.cs`, `src/PRN222.BLL/Services/CourseService.cs`
- Traces to: `UC-COURSE-01`
- Priority: Must
- Roles: Admin
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - Admin can persist a valid new course from the standard create action.
  - Admin can persist a valid new course from the AJAX create action.
  - A blank course name is rejected before course creation.

#### FR-COURSE-002 - Restrict course mutation

- Statement: The system shall deny HeadLecturer, Lecturer, and Student users access to course creation, editing, and deletion.
- Type: functional
- Source: `src/PRN222.MVC/Controllers/CourseController.cs`
- Traces to: `UC-COURSE-01`
- Priority: Must
- Roles: HeadLecturer, Lecturer, Student
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - A non-Admin request cannot execute a course mutation action.
  - Denied requests do not change persisted course data.

#### FR-COURSE-003 - Scope staff course lists

- Statement: The system shall show HeadLecturer and Lecturer users only courses that are explicitly assigned and belong to the same department as the staff account.
- Type: functional
- Source: `src/PRN222.MVC/Controllers/CourseController.cs`, `src/PRN222.BLL/Services/CourseAccessService.cs`
- Traces to: `UC-COURSE-02`
- Priority: Must
- Roles: HeadLecturer, Lecturer
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - A same-department assigned course appears in the staff course list.
  - An unassigned course does not appear in the staff course list.
  - A legacy cross-department assignment does not appear in any staff-scoped workflow.

#### FR-COURSE-004 - Expose chat courses to students

- Statement: The system shall provide Student users with the current complete course list when selecting a Chat scope.
- Type: functional
- Source: `src/PRN222.MVC/Controllers/ChatController.cs`
- Traces to: `UC-CHAT-01`
- Priority: Must
- Roles: Student
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - A Student can retrieve the courses returned by the course service for Chat.
  - Student course selection is not filtered through staff course assignments.

#### FR-COURSE-005 - Update courses

- Statement: The system shall allow Admin to update the name and description of an existing course.
- Type: functional
- Source: `src/PRN222.MVC/Controllers/CourseController.cs`, `src/PRN222.BLL/Services/CourseService.cs`
- Traces to: `UC-COURSE-01`
- Priority: Must
- Roles: Admin
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - Admin can load an existing course for editing.
  - Admin can persist a valid updated course name and description.
  - A blank updated course name is rejected before course update.

#### FR-COURSE-006 - Delete document-free courses

- Statement: The system shall allow Admin to delete an existing course only when the course has no documents.
- Type: functional
- Source: `src/PRN222.MVC/Controllers/CourseController.cs`, `src/PRN222.BLL/Services/CourseService.cs`
- Traces to: `UC-COURSE-01`
- Priority: Must
- Roles: Admin
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - Admin can delete an existing course with no documents.
  - Deletion returns a not-found outcome when the course does not exist.
  - Deletion is rejected when the course has one or more documents.

### 4.4 Document Ingestion and Knowledge Base Management

#### FR-DOC-001 - Upload supported documents

- Statement: The system shall allow Admin to upload supported course documents and HeadLecturer to upload supported documents within effective course scope.
- Type: functional
- Source: `src/PRN222.MVC/Controllers/DocumentController.cs`, `src/PRN222.BLL/Services/DocumentService.cs`
- Traces to: `UC-DOC-01`
- Priority: Must
- Roles: Admin, HeadLecturer
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - PDF, DOCX, PPTX, and PPT are accepted when extension, content type, signature, size, and course scope are valid.
  - Uploaded metadata is persisted with status Uploaded.

#### FR-DOC-002 - Reject unauthorized document upload

- Statement: The system shall deny document uploads by Lecturer or Student users and by HeadLecturer users for unassigned courses.
- Type: functional
- Source: `src/PRN222.MVC/Controllers/DocumentController.cs`, `src/PRN222.MVC/Infrastructure/ApplicationRoles.cs`
- Traces to: `UC-DOC-01`
- Priority: Must
- Roles: HeadLecturer, Lecturer, Student
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - Lecturer and Student requests cannot execute upload.
  - A HeadLecturer upload for an unassigned course is rejected.
  - A denied upload does not persist a document.

#### FR-DOC-003 - Process uploaded documents

- Statement: The system shall extract, chunk, embed, and persist searchable content from an uploaded supported document.
- Type: functional
- Source: `src/PRN222.BLL/Services/DocumentService.cs`, `src/PRN222.BLL/Services/ChunkingService.cs`
- Traces to: `UC-DOC-02`
- Priority: Must
- Roles: Admin, HeadLecturer
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - Successful processing stores ordered chunks and an embedding for each chunk.
  - Successful processing marks the document indexed.
  - Processing failure records a failed status and error message.

#### FR-DOC-004 - Restrict raw chunk access

- Statement: The system shall expose extracted text and raw chunks only to Admin and to HeadLecturer within effective course scope.
- Type: functional
- Source: `src/PRN222.MVC/Controllers/DocumentController.cs`, `src/PRN222.MVC/Views/Document/Details.cshtml`
- Traces to: `UC-DOC-03`
- Priority: Must
- Roles: Admin, HeadLecturer, Lecturer, Student
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - Admin can inspect raw content.
  - An assigned HeadLecturer can inspect raw content.
  - Lecturer receives neither extracted preview nor chunk records.
  - Student has no document-details access.

#### FR-DOC-005 - Validate uploaded file signatures

- Statement: The system shall reject an uploaded file when its binary signature does not match the supported submitted format.
- Type: functional
- Source: `src/PRN222.BLL/Services/DocumentService.cs`
- Traces to: `UC-DOC-01`
- Priority: Must
- Roles: Admin, HeadLecturer
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - A mismatched PDF or OpenXML signature is rejected before file persistence.
  - A valid supported signature proceeds to normal upload handling.

### 4.5 RAG Chat, Retrieval, Sessions, and Citations

#### FR-CHAT-001 - Answer course-scoped questions

- Statement: The system shall generate an answer using context retrieved from indexed documents in the selected course.
- Type: functional
- Source: `src/PRN222.BLL/Services/ChatService.cs`, `src/PRN222.BLL/Services/Rag/RagRetrievalService.cs`
- Traces to: `UC-CHAT-01`
- Priority: Must
- Roles: Admin, HeadLecturer, Lecturer, Student
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - The user question is embedded.
  - Retrieval searches indexed documents belonging to the selected course.
  - The LLM receives the retrieved source-labelled context.

#### FR-CHAT-002 - Enforce staff chat course scope

- Statement: The system shall reject a HeadLecturer or Lecturer chat request outside effective course scope.
- Type: functional
- Source: `src/PRN222.MVC/Controllers/ChatController.cs`
- Traces to: `UC-CHAT-01`
- Priority: Must
- Roles: HeadLecturer, Lecturer
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - A chat request for an assigned course proceeds.
  - A chat request for an unassigned course is rejected before answer generation.

#### FR-CHAT-003 - Persist chat citations

- Statement: The system shall associate generated assistant messages with citations for retrieved chunks.
- Type: functional
- Source: `src/PRN222.BLL/Services/ChatService.cs`
- Traces to: `UC-CHAT-01`
- Priority: Must
- Roles: Admin, HeadLecturer, Lecturer, Student
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - Each citation identifies its source chunk.
  - Each citation stores a similarity value and source snippet.
  - No fabricated citation is stored when no chunk is retrieved.

#### FR-CHAT-004 - Protect chat session ownership

- Statement: The system shall allow a user to retrieve and continue only chat sessions owned by that user.
- Type: functional
- Source: `src/PRN222.BLL/Services/ChatService.cs`, `src/PRN222.DAL/Repositories/ChatSessionRepository.cs`
- Traces to: `UC-CHAT-02`
- Priority: Must
- Roles: Admin, HeadLecturer, Lecturer, Student
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - An owned session can be loaded.
  - A session owned by another user is not returned through the user-scoped query.

### 4.6 Test-Set Generation

#### FR-TESTSET-001 - Generate course test sets

- Statement: The system shall allow Admin and HeadLecturer users within effective course scope to generate Q and A pairs from indexed course chunks.
- Type: functional
- Source: `src/PRN222.MVC/Controllers/TestSetGeneratorController.cs`, `src/PRN222.BLL/Services/TestSetGeneratorService.cs`
- Traces to: `UC-TESTSET-01`
- Priority: Must
- Roles: Admin, HeadLecturer
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - Eligible indexed chunks are selected from the chosen course.
  - Generated pairs are persisted with course and source-chunk references.
  - Progress is reported for the active job.

#### FR-TESTSET-002 - Restrict test-set generation

- Statement: The system shall deny test-set operations by Lecturer or Student users and by HeadLecturer users for unassigned courses.
- Type: functional
- Source: `src/PRN222.MVC/Controllers/TestSetGeneratorController.cs`
- Traces to: `UC-TESTSET-01`
- Priority: Must
- Roles: HeadLecturer, Lecturer, Student
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - Lecturer and Student cannot execute test-set endpoints.
  - An unassigned HeadLecturer course request is rejected.
  - A rejected request creates no generation job.

#### FR-TESTSET-003 - Prevent duplicate course jobs

- Statement: The system shall prevent more than one active test-set generation job for the same course in the current application process.
- Type: functional
- Source: `src/PRN222.BLL/Services/TestSetGenerationJobManager.cs`
- Traces to: `UC-TESTSET-01`
- Priority: Should
- Roles: Admin, HeadLecturer
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - The first job for a course can become active.
  - A second concurrent request for that course is rejected while the first remains active.

#### FR-TESTSET-004 - Manage generated pairs

- Statement: The system shall provide authorized users with statistics, paginated preview, and clearing of automatically generated Q and A pairs for an accessible course.
- Type: functional
- Source: `src/PRN222.MVC/Controllers/TestSetGeneratorController.cs`, `src/PRN222.BLL/Services/TestSetGeneratorService.cs`
- Traces to: `UC-TESTSET-02`
- Priority: Should
- Roles: Admin, HeadLecturer
- Verification: Demonstration
- Implementation status: Implemented
- Acceptance criteria:
  - Statistics and a requested preview page can be retrieved.
  - Clearing removes auto-generated pairs without claiming removal of manual pairs.

### 4.7 Evaluation and Benchmarking

#### FR-EVAL-001 - Create benchmark runs

- Statement: The system shall allow Admin and HeadLecturer users within effective course scope to create a course-scoped benchmark run with supported experiment configuration.
- Type: functional
- Source: `src/PRN222.MVC/Controllers/EvaluationController.cs`, `src/PRN222.BLL/Services/BenchmarkService.cs`
- Traces to: `UC-EVAL-01`
- Priority: Must
- Roles: Admin, HeadLecturer
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - A run persists the selected experiment type and model configuration.
  - The run fails with an explanatory state when no eligible Q and A pairs exist.

#### FR-EVAL-002 - Restrict benchmark operations

- Statement: The system shall deny benchmark operations by Lecturer or Student users and by HeadLecturer users for unassigned courses.
- Type: functional
- Source: `src/PRN222.MVC/Controllers/EvaluationController.cs`
- Traces to: `UC-EVAL-01`
- Priority: Must
- Roles: HeadLecturer, Lecturer, Student
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - Lecturer and Student cannot execute benchmark endpoints.
  - An unassigned HeadLecturer course request is rejected.
  - Denied requests create no benchmark run.

#### FR-EVAL-003 - Persist benchmark results

- Statement: The system shall persist per-question results and internal approximation metrics produced by a benchmark run.
- Type: functional
- Source: `src/PRN222.BLL/Services/BenchmarkService.cs`
- Traces to: `UC-EVAL-01`
- Priority: Must
- Roles: Admin, HeadLecturer
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - Each evaluated Q and A pair produces a stored result or an explicit run failure.
  - Stored heuristic metrics are not labelled as certified external RAGAS results.

#### FR-EVAL-004 - Review and export benchmark runs

- Statement: The system shall allow authorized users to view accessible benchmark summaries and details, compare runs, and export results as CSV.
- Type: functional
- Source: `src/PRN222.MVC/Controllers/EvaluationController.cs`
- Traces to: `UC-EVAL-02`
- Priority: Should
- Roles: Admin, HeadLecturer
- Verification: Demonstration
- Implementation status: Implemented
- Acceptance criteria:
  - Only accessible runs are displayed or compared.
  - CSV export returns result rows for the selected accessible run.

### 4.8 Fine-Tuning Dataset Workflow

#### FR-FT-001 - Generate fine-tuning datasets

- Statement: The system shall allow Admin and HeadLecturer users within effective course scope to generate course-scoped fine-tuning Q and A pairs from document chunks.
- Type: functional
- Source: `src/PRN222.MVC/Controllers/FinetuneController.cs`, `src/PRN222.BLL/Services/FinetuneService.cs`
- Traces to: `UC-FT-01`
- Priority: Must
- Roles: Admin, HeadLecturer
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - Generation uses chunks belonging to the selected course.
  - Exact duplicate questions are not added.
  - Generated records retain source-chunk linkage.

#### FR-FT-002 - Restrict fine-tuning operations

- Statement: The system shall deny fine-tuning dataset operations by Lecturer or Student users and by HeadLecturer users for unassigned courses.
- Type: functional
- Source: `src/PRN222.MVC/Controllers/FinetuneController.cs`
- Traces to: `UC-FT-01`
- Priority: Must
- Roles: HeadLecturer, Lecturer, Student
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - Lecturer and Student cannot execute fine-tuning endpoints.
  - An unassigned HeadLecturer course request is rejected.
  - Denied requests create no generated pairs.

#### FR-FT-003 - Export JSONL datasets

- Statement: The system shall export accessible fine-tuning pairs as OpenAI-style JSONL.
- Type: functional
- Source: `src/PRN222.MVC/Controllers/FinetuneController.cs`, `src/PRN222.BLL/Services/FinetuneService.cs`
- Traces to: `UC-FT-02`
- Priority: Should
- Roles: Admin, HeadLecturer
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - Each exported line is valid JSON.
  - Each line represents the supported chat-training structure.
  - Export is limited to the authorized course.

#### FR-FT-004 - Invoke configured fine-tuned inference

- Statement: The system shall invoke the configured Hugging Face fine-tuned inference service when a fine-tuned benchmark is executed.
- Type: functional
- Source: `src/PRN222.BLL/Services/AI/HuggingFaceFineTunedModelService.cs`, `src/PRN222.BLL/Services/BenchmarkService.cs`
- Traces to: `UC-EVAL-01`
- Priority: Should
- Roles: Admin, HeadLecturer
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - A configured supported endpoint receives the benchmark prompt.
  - Missing or unsupported endpoint configuration produces an explicit error rather than a fabricated answer.

## 5. External Interface Requirements

### 5.1 Browser User Interface

The browser UI is delivered through ASP.NET Core MVC controllers and Razor views. It provides role-appropriate navigation, authentication pages, management dashboards, chat pages, course management, document management, test-set generation, benchmark views, fine-tuning dataset views, and a combined Admin staff/department governance workspace according to role permission.

### 5.2 MVC Endpoint Interface

The MVC endpoint surface includes controllers for Account, Admin, Course, Document, Chat, TestSetGenerator, Evaluation, Finetune, and Home workflows. Protected endpoints require authenticated users and the applicable role or course scope before performing state changes or returning protected data.

### 5.3 Persistence Interface

The persistence interface is SQL Server through EF Core. The application persists Identity users and roles, staff course assignments, courses, documents, chunks, embeddings, chat sessions, messages, citations, Q and A pairs, benchmark runs, benchmark results, and embedding model records through the DAL model.

### 5.4 Uploaded Document Interface

The uploaded document interface accepts PDF, DOCX, PPTX, and legacy PPT files within the configured upload size limit. Submitted files undergo format-support and binary-signature validation before normal persistence. PDF, DOCX, and PPTX have structured extractors. Legacy PPT is supported with best-effort readable text recovery only.

### 5.5 AI and Embedding Provider Interfaces

The system integrates with Google Gemini for default LLM and embedding behavior. It contains adapters for Hugging Face multilingual E5 embeddings, OpenAI text embeddings, and Hugging Face fine-tuned inference. Provider availability, credentials, endpoint support, rate limits, and response failures are runtime dependencies; failures are surfaced rather than represented as successful AI output.

### 5.6 Configuration Dependencies

Configuration provides database connection strings, provider API keys or endpoints for enabled AI services, upload limits, Identity options, and development-only seed data where applicable. Demo seed credentials in development configuration describe the development environment and are not production credential requirements.

## 6. Data Requirements

### 6.1 Core Entities

| Entity | Data requirement |
|---|---|
| ApplicationUser | Stores Identity user data and participates in staff course assignment. |
| Department | Groups staff and courses into an administrative authorization boundary. |
| Course | Represents a department-aware scope for documents, chat, Q and A pairs, and benchmarks. |
| ApplicationUserCourse | Represents explicit staff assignment to courses with a composite relationship; effective access additionally requires matching staff/course departments. |
| Document | Stores uploaded document metadata, status, file path, course ownership, and processing information. |
| DocumentChunk | Stores ordered extracted text segments associated with one document. |
| ChunkEmbedding | Stores embedding vectors for chunks and model names. |
| EmbeddingModel | Stores configured embedding model metadata such as provider and dimensions. |
| ChatSession | Stores user-owned conversation sessions. |
| ChatMessage | Stores user and assistant messages within a session. |
| ChatCitation | Stores chunk citations, snippets, and similarity values for assistant messages. |
| QAPair | Stores generated or manual question-answer pairs scoped to a course and optionally linked to a chunk. |
| BenchmarkRun | Stores benchmark configuration, scope, status, timestamps, and selected model data. |
| BenchmarkResult | Stores per-question benchmark outputs and internal metrics. |

### 6.2 Ownership and Isolation

Documents, chunks, Q and A pairs, and benchmark runs are course-scoped. Staff course assignments define HeadLecturer and Lecturer management visibility. Chat sessions are owned by individual users and are queried by user ID. Raw chunks and extracted text are not exposed to Lecturer or Student users.

### 6.3 Validation and Retention Considerations

The system currently validates supported file formats, upload size, course scope, roles, and key workflow inputs. The repository does not define a full retention schedule, malware-scanning process, or institutional data-classification policy. Those topics are documented as limitations or targets rather than implemented requirements.

## 7. Non-Functional Requirements

This section renders every non-functional requirement from the validated catalog. Implemented items describe current repository behavior. Target and Future items describe acceptance expectations that require additional verification before they can be claimed as implemented.

### 7.1 Security

#### NFR-SEC-001 - Enforce server-side authorization

- Statement: The system shall enforce role and course authorization on the server independently of navigation visibility.
- Type: non-functional
- Source: `src/PRN222.MVC/Controllers/DocumentController.cs`, `src/PRN222.MVC/Controllers/ChatController.cs`, `src/PRN222.MVC/Infrastructure/ApplicationRoles.cs`
- Traces to: `UC-AUTH-04`, `UC-DOC-01`, `UC-CHAT-01`
- Priority: Must
- Roles: Admin, HeadLecturer, Lecturer, Student
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - Direct requests are subject to the same role checks as visible UI actions.
  - Course-scoped staff requests are validated before protected data or operations are returned.

#### NFR-SEC-002 - Harden application security controls

- Statement: The system shall satisfy the risk-appropriate OWASP ASVS controls selected and documented for production deployment.
- Type: non-functional
- Source: `docs/srs/source-evidence.md`, `https://owasp.org/www-project-application-security-verification-standard/`
- Traces to: `SEC-ASVS`
- Priority: Should
- Roles: Admin, HeadLecturer, Lecturer, Student
- Verification: Inspection
- Implementation status: Target
- Acceptance criteria:
  - The project documents an applicable ASVS control set and target level.
  - Each selected control has verification evidence or a tracked remediation.
  - Demo credentials are absent from production configuration and UI.

### 7.2 Performance Efficiency

#### NFR-PERF-001 - Measure interactive response time

- Statement: The system shall define and verify response-time thresholds for interactive pages and chat under a documented workload before production acceptance.
- Type: non-functional
- Source: `docs/srs/source-evidence.md`
- Traces to: `QA-PERF-01`
- Priority: Should
- Roles: Admin, HeadLecturer, Lecturer, Student
- Verification: Analysis
- Implementation status: Target
- Acceptance criteria:
  - The workload, dataset size, concurrency, environment, percentile, and threshold are documented.
  - Measured results are retained without substituting unverified benchmark-report values.
  - A failed threshold produces a tracked performance finding.

#### NFR-PERF-002 - Bound document processing resources

- Statement: The system shall reject uploads larger than 50 MiB at the MVC request boundary.
- Type: non-functional
- Source: `src/PRN222.MVC/Infrastructure/ServiceCollectionExtensions.cs`, `src/PRN222.MVC/Controllers/DocumentController.cs`
- Traces to: `UC-DOC-01`
- Priority: Must
- Roles: Admin, HeadLecturer
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - A request exceeding 50 MiB is not accepted for document upload.
  - A request within the limit remains subject to format and authorization validation.

### 7.3 Reliability

#### NFR-REL-001 - Record processing failures

- Statement: The system shall preserve an explicit failed document status and diagnostic message when document processing cannot complete.
- Type: non-functional
- Source: `src/PRN222.BLL/Services/DocumentService.cs`
- Traces to: `UC-DOC-02`
- Priority: Must
- Roles: Admin, HeadLecturer
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - A processing exception results in Failed status.
  - A diagnostic error message is persisted for authorized review.
  - The document is not reported as indexed after failure.

#### NFR-REL-002 - Provide durable background jobs

- Statement: The system shall recover document-processing and test-set jobs after an application restart without duplicating completed work.
- Type: non-functional
- Source: `docs/srs/source-evidence.md`
- Traces to: `QA-REL-01`
- Priority: Could
- Roles: Admin, HeadLecturer
- Verification: Test
- Implementation status: Future
- Acceptance criteria:
  - Queued job state is stored outside application process memory.
  - Restart testing demonstrates recovery of an interrupted job.
  - Idempotency prevents duplicate persisted outputs.

### 7.4 Usability and Accessibility

#### NFR-UX-001 - Provide role-appropriate navigation

- Statement: The system shall display navigation and primary actions that correspond to the authenticated user's role.
- Type: non-functional
- Source: `src/PRN222.MVC/Views/Shared/_Layout.cshtml`
- Traces to: `UC-NAV-01`
- Priority: Should
- Roles: Admin, HeadLecturer, Lecturer, Student
- Verification: Demonstration
- Implementation status: Implemented
- Acceptance criteria:
  - Admin sees account and course administration actions.
  - HeadLecturer sees assigned model operations and upload actions.
  - Lecturer does not see upload, chunk, or model-operation actions.
  - Student sees Chat without management navigation.

#### NFR-UX-002 - Meet accessibility targets

- Statement: The system shall meet WCAG 2.2 Level AA success criteria applicable to the browser interface.
- Type: non-functional
- Source: `docs/srs/source-evidence.md`, `https://www.w3.org/TR/WCAG22/`
- Traces to: `QA-UX-01`
- Priority: Should
- Roles: Admin, HeadLecturer, Lecturer, Student
- Verification: Inspection
- Implementation status: Target
- Acceptance criteria:
  - Automated accessibility checks report no unresolved applicable Level A or AA violations.
  - Keyboard-only review covers every primary workflow.
  - Manual checks verify labels, focus visibility, contrast, status announcements, and error identification.

### 7.5 Maintainability

#### NFR-MAINT-001 - Maintain layered responsibilities

- Statement: The system shall keep presentation, business-service, persistence, and test responsibilities in their established projects.
- Type: non-functional
- Source: `src/PRN222.MVC/PRN222.MVC.csproj`, `src/PRN222.BLL/PRN222.BLL.csproj`, `src/PRN222.DAL/PRN222.DAL.csproj`, `src/PRN222.Tests/PRN222.Tests.csproj`
- Traces to: `ARCH-LAYERS`
- Priority: Should
- Roles: Admin, HeadLecturer, Lecturer, Student
- Verification: Inspection
- Implementation status: Implemented
- Acceptance criteria:
  - MVC contains presentation composition and controllers.
  - BLL contains application services and AI integrations.
  - DAL contains entities, EF configuration, and repositories.
  - Automated tests remain in the test project.

#### NFR-MAINT-002 - Validate requirements traceability

- Statement: The system documentation shall maintain unique machine-validated requirement identifiers with source, verification, acceptance, and trace references.
- Type: non-functional
- Source: `tools/srs/validate_srs.py`, `docs/srs/requirements.json`
- Traces to: `DOC-TRACE-01`
- Priority: Must
- Roles: Admin, HeadLecturer, Lecturer, Student
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - Catalog validation rejects duplicate or malformed IDs.
  - Catalog validation rejects missing source, verification, acceptance criteria, or trace-reference fields.
  - Implemented requirements cannot cite absent repository paths.

### 7.6 Compatibility and Platform Support

#### NFR-COMPAT-001 - Support documented source formats

- Statement: The system shall accept PDF, DOCX, PPTX, and legacy PPT uploads subject to documented extraction limitations.
- Type: non-functional
- Source: `src/PRN222.MVC/Controllers/DocumentController.cs`, `src/PRN222.BLL/Services/TextExtractors/PptTextExtractor.cs`
- Traces to: `UC-DOC-01`, `UC-DOC-02`
- Priority: Must
- Roles: Admin, HeadLecturer
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - PDF, DOCX, and PPTX use their structured extractors.
  - Legacy PPT uses only best-effort readable-string recovery and is not represented as structured slide parsing.
  - Unsupported extensions are rejected.

#### NFR-COMPAT-002 - Run on the verified platform

- Statement: The system shall build and execute on .NET 8 with the configured SQL Server EF Core provider.
- Type: non-functional
- Source: `src/PRN222.MVC/PRN222.MVC.csproj`, `src/PRN222.DAL/PRN222.DAL.csproj`
- Traces to: `ENV-PLATFORM-01`
- Priority: Must
- Roles: Admin, HeadLecturer, Lecturer, Student
- Verification: Test
- Implementation status: Implemented
- Acceptance criteria:
  - All application projects target net8.0.
  - The configured EF Core provider is SQL Server.
  - The solution build succeeds using a compatible .NET 8 SDK.

### 7.7 AI/RAG Quality and Reproducibility

#### NFR-AI-001 - Disclose retrieval relevance semantics

- Statement: The system shall describe the assistant ConfidenceScore as the highest retrieved-chunk similarity rather than calibrated model or answer confidence.
- Type: non-functional
- Source: `src/PRN222.BLL/Services/ChatService.cs`
- Traces to: `UC-CHAT-01`
- Priority: Must
- Roles: Admin, HeadLecturer, Lecturer, Student
- Verification: Inspection
- Implementation status: Partially Implemented
- Acceptance criteria:
  - A no-retrieval assistant message stores zero for ConfidenceScore.
  - When retrieved chunks exist, the assistant message stores the first ranked chunk similarity as ConfidenceScore.
  - Stored citations retain each retrieved chunk similarity separately as RelevanceScore.

#### NFR-AI-002 - Establish reproducible AI quality gates

- Statement: The system shall define reproducible groundedness, citation, retrieval, and answer-quality acceptance thresholds before claiming production AI quality.
- Type: non-functional
- Source: `docs/srs/source-evidence.md`
- Traces to: `QA-AI-01`, `UC-EVAL-01`
- Priority: Should
- Roles: Admin, HeadLecturer
- Verification: Analysis
- Implementation status: Target
- Acceptance criteria:
  - The evaluation dataset version and course scope are recorded.
  - Metric definitions distinguish internal approximations from external RAGAS evaluation.
  - Thresholds and results are recorded from reproducible exported runs.
  - No value from the unverified research report is treated as achieved evidence.

## 8. Verification, Use Cases, and Traceability

### 8.1 Verification Methods

| Method | Meaning in this SRS |
|---|---|
| Test | Automated or manual execution can confirm the requirement result. |
| Inspection | Review of source code, configuration, generated output, or documentation can confirm the requirement. |
| Analysis | Evidence requires measured evaluation, comparison, or formal assessment. |
| Demonstration | A reviewer can observe the behavior through the browser UI or exported artifact. |

### 8.2 Use-Case Specifications

#### UC-AUTH-01

- Actor: Registered user
- Trigger: Submit email and password
- Preconditions: Account exists in Identity
- Normal flow:
  1. The registered user submits an email address and password.
  2. The system validates the credentials through Identity.
  3. The system creates an authenticated session.
  4. The system redirects the user according to the assigned role.
- Alternatives:
  - Invalid credentials return validation feedback
- Postconditions: User is authenticated or remains unauthenticated.
- Related requirement IDs: FR-AUTH-001

#### UC-AUTH-02

- Actor: Student
- Trigger: Submit public registration form
- Preconditions: Registration input is valid
- Normal flow:
  1. The student submits the public registration form.
  2. The system creates an Identity user.
  3. The system assigns only the Student role to the new account.
  4. The system signs in the student and redirects to Chat.
- Alternatives:
  - Invalid data returns validation messages
- Postconditions: A Student account exists.
- Related requirement IDs: FR-AUTH-002

#### UC-AUTH-03

- Actor: Authenticated user
- Trigger: Submit logout
- Preconditions: Authenticated session exists
- Normal flow:
  1. The authenticated user submits the logout action.
  2. The system signs out the user through Identity.
  3. The system redirects the user to Login.
- Alternatives:
  - Already unauthenticated users receive no protected state
- Postconditions: Session is terminated.
- Related requirement IDs: FR-AUTH-003

#### UC-AUTH-04

- Actor: Unauthenticated requester
- Trigger: Open protected Chat endpoint
- Preconditions: No valid authentication cookie
- Normal flow:
  1. The unauthenticated requester opens a protected Chat endpoint.
  2. The system evaluates endpoint authorization.
  3. The system issues the configured cookie challenge.
  4. The system redirects the requester to Login without executing the protected action.
- Alternatives:
  - Authenticated users proceed only with authorized role
- Postconditions: Protected action does not execute.
- Related requirement IDs: FR-AUTH-004, NFR-SEC-001

#### UC-ADMIN-01

- Actor: Admin
- Trigger: Create staff account
- Preconditions: Admin is authenticated; departments and courses exist
- Normal flow:
  1. The Admin enters staff account data, selects a department, and selects courses from that department.
  2. The system validates that the selected role is Lecturer or HeadLecturer.
  3. The system validates the email and same-department course set.
  4. The system creates the staff user with the selected department and role.
  5. The system persists the selected course assignments in the same transaction.
- Alternatives:
  - Unsupported role, existing email, missing department, empty assignment, or cross-department course is rejected
- Postconditions: Staff account exists with a department and validated assignments.
- Related requirement IDs: FR-ADMIN-001, FR-ADMIN-002, FR-ADMIN-004

#### UC-ADMIN-02

- Actor: Admin
- Trigger: Manage staff, department, or course assignment
- Preconditions: Target user is staff
- Normal flow:
  1. The Admin opens the combined staff and department governance workspace.
  2. The system displays departments, staff, courses, configuration warnings, and effective assignment state.
  3. The Admin creates a department, replaces a staff department/course set, or moves staff/course ownership.
  4. The system validates department membership and course identifiers.
  5. The system persists the requested relationship change and revokes stale cross-department assignments in one transaction.
- Alternatives:
  - Invalid target, empty assignment, or cross-department selection is rejected
  - Reconciliation failure rolls back the relationship change
- Postconditions: Department ownership and effective staff scope are consistent.
- Related requirement IDs: FR-ADMIN-003, FR-ADMIN-005, FR-ADMIN-006

#### UC-COURSE-01

- Actor: Admin
- Trigger: Create, update, or delete a course
- Preconditions: Admin is authenticated
- Normal flow:
  1. The Admin selects a create, update, or delete course action.
  2. For create or update, the Admin submits valid course data.
  3. The system persists the new or updated course.
  4. For deletion, the system confirms that the course has no documents before removing it.
- Alternatives:
  - Blank names and deletion with documents are rejected
- Postconditions: Course data changes only when authorized and valid.
- Related requirement IDs: FR-COURSE-001, FR-COURSE-002, FR-COURSE-005, FR-COURSE-006

#### UC-COURSE-02

- Actor: HeadLecturer or Lecturer
- Trigger: Open management course list
- Preconditions: Staff has a department and course assignments
- Normal flow:
  1. The staff user opens the management course list.
  2. The system identifies the current user.
  3. The system intersects explicit assignments with courses in the same non-null department.
  4. The system renders only the effective scoped course list.
- Alternatives:
  - Unassigned, departmentless, and cross-department courses are not returned
- Postconditions: Staff sees only same-department assigned courses.
- Related requirement IDs: FR-COURSE-003

#### UC-DOC-01

- Actor: Admin or HeadLecturer
- Trigger: Upload course document
- Preconditions: User has upload permission for target course
- Normal flow:
  1. The actor selects a permitted course and submits a document.
  2. The system validates the actor's role and course scope.
  3. The system validates the file extension, size, content type, and binary signature.
  4. The system saves the accepted file.
  5. The system persists document metadata with Uploaded status.
- Alternatives:
  - Unauthorized scope or invalid file is rejected
- Postconditions: Valid document is stored.
- Related requirement IDs: FR-DOC-001, FR-DOC-002, FR-DOC-005, NFR-SEC-001, NFR-PERF-002, NFR-COMPAT-001

#### UC-DOC-02

- Actor: Admin or HeadLecturer
- Trigger: Process uploaded document
- Preconditions: Document is uploaded and accessible
- Normal flow:
  1. The actor starts processing for an accessible uploaded document.
  2. The system extracts readable text from the document.
  3. The system divides the text into chunks.
  4. The system generates embeddings for the chunks.
  5. The system stores the chunks and embedding vectors.
  6. The system marks the document as indexed.
- Alternatives:
  - Processing errors store Failed status and diagnostic message
- Postconditions: Document is indexed or explicitly failed.
- Related requirement IDs: FR-DOC-003, NFR-REL-001, NFR-COMPAT-001

#### UC-DOC-03

- Actor: Admin, HeadLecturer, or Lecturer
- Trigger: Open document details
- Preconditions: Document is in accessible course
- Normal flow:
  1. The actor opens details for a document in an accessible course.
  2. The system loads the document metadata.
  3. For an Admin or authorized HeadLecturer, the system includes extracted text and raw chunk sections.
  4. For a Lecturer, the system omits the raw-content sections.
- Alternatives:
  - Student has no document-management access
- Postconditions: Detail visibility matches authorization.
- Related requirement IDs: FR-DOC-004

#### UC-CHAT-01

- Actor: Chat user
- Trigger: Submit question for selected course
- Preconditions: User is authenticated and course is visible
- Normal flow:
  1. The chat user selects a visible course and submits a question.
  2. The system validates the question and enforces staff course scope.
  3. The system embeds the question and retrieves relevant document chunks.
  4. The system sends the question and retrieved context to the configured answer service.
  5. The system stores the generated answer.
  6. The system associates citations with the retrieved chunks used for the answer.
- Alternatives:
  - Unassigned staff course is rejected; no retrieved chunks produce no fabricated citation
- Postconditions: Session stores question, answer, and citations.
- Related requirement IDs: FR-COURSE-004, FR-CHAT-001, FR-CHAT-002, FR-CHAT-003, NFR-SEC-001, NFR-AI-001

#### UC-CHAT-02

- Actor: Authenticated user
- Trigger: Open existing chat session
- Preconditions: Session is owned by current user
- Normal flow:
  1. The authenticated user requests an existing chat session.
  2. The system queries sessions by the current user ID.
  3. The system loads the selected session when it belongs to that user.
  4. The user submits another message and the system continues the owned session.
- Alternatives:
  - Other users sessions are not returned
- Postconditions: Only owner-visible history is shown.
- Related requirement IDs: FR-CHAT-004

#### UC-TESTSET-01

- Actor: Admin or HeadLecturer
- Trigger: Start test-set generation
- Preconditions: Model-operation access and indexed chunks exist
- Normal flow:
  1. The actor selects an accessible course and submits generation parameters.
  2. The system checks for an active generation job for that course in the current process.
  3. The system starts the job when no duplicate job is active.
  4. The system generates Q and A pairs from indexed chunks.
  5. The system persists the generated pairs and their source links.
- Alternatives:
  - Unauthorized or duplicate requests are rejected
- Postconditions: Generated pairs are available.
- Related requirement IDs: FR-TESTSET-001, FR-TESTSET-002, FR-TESTSET-003

#### UC-TESTSET-02

- Actor: Admin or HeadLecturer
- Trigger: Review generated Q and A pairs
- Preconditions: Generated pairs exist for accessible course
- Normal flow:
  1. The actor opens generated pairs for an accessible course.
  2. The system returns generation statistics.
  3. The system returns a paginated preview of the pairs.
  4. When the actor requests clearing, the system removes the automatically generated pairs in scope.
- Alternatives:
  - Out-of-scope requests are rejected
- Postconditions: Generated pairs are inspected or cleared.
- Related requirement IDs: FR-TESTSET-004

#### UC-EVAL-01

- Actor: Admin or HeadLecturer
- Trigger: Create benchmark run
- Preconditions: Accessible course has eligible Q and A pairs
- Normal flow:
  1. The actor selects an accessible course and experiment configuration.
  2. The system creates a course-scoped benchmark run.
  3. The system executes the selected RAG or fine-tuned workflow for eligible Q and A pairs.
  4. The system persists a result row for each evaluated question.
  5. The system persists the produced internal approximation metrics and run status.
- Alternatives:
  - No pairs or unsupported endpoint produces explicit failure
- Postconditions: Benchmark run exists with results or failure reason.
- Related requirement IDs: FR-EVAL-001, FR-EVAL-002, FR-EVAL-003, FR-FT-004, NFR-AI-002

#### UC-EVAL-02

- Actor: Admin or HeadLecturer
- Trigger: Review or export benchmark results
- Preconditions: Accessible run exists
- Normal flow:
  1. The actor opens benchmark results.
  2. The system filters runs to the actor's accessible course scope.
  3. The system displays summaries or details for the selected runs.
  4. The actor selects comparison or CSV export.
  5. The system returns the authorized comparison or export data.
- Alternatives:
  - Inaccessible runs are not shown or exported
- Postconditions: Benchmark data is reviewed in authorized scope.
- Related requirement IDs: FR-EVAL-004

#### UC-FT-01

- Actor: Admin or HeadLecturer
- Trigger: Generate fine-tuning pairs
- Preconditions: Accessible course chunks exist
- Normal flow:
  1. The actor selects an accessible course and target pair count.
  2. The system generates Q and A pairs from course document chunks.
  3. The system excludes exact duplicate pairs.
  4. The system stores the accepted pairs with their source links.
- Alternatives:
  - Unauthorized scope is denied
- Postconditions: Fine-tuning pairs are persisted.
- Related requirement IDs: FR-FT-001, FR-FT-002

#### UC-FT-02

- Actor: Admin or HeadLecturer
- Trigger: Export fine-tuning dataset
- Preconditions: Accessible pairs exist
- Normal flow:
  1. The actor requests export for an accessible course.
  2. The system loads the authorized fine-tuning pairs.
  3. The system serializes the pairs as OpenAI-style JSONL.
  4. The system returns the course-scoped export.
- Alternatives:
  - Unauthorized scope is denied
- Postconditions: JSONL export is returned.
- Related requirement IDs: FR-FT-003

#### UC-NAV-01

- Actor: Authenticated user
- Trigger: Load application layout
- Preconditions: User has authenticated role
- Normal flow:
  1. The authenticated user loads the application layout.
  2. The system evaluates the user's role.
  3. The system renders the navigation and primary actions associated with that role.
  4. Backend authorization continues to enforce access independently of navigation visibility.
- Alternatives:
  - Hidden actions do not imply permission changes
- Postconditions: Navigation matches role without replacing authorization.
- Related requirement IDs: NFR-UX-001

### 8.3 Quality and Documentation Trace Targets

- ARCH-LAYERS: Non-use-case trace target for NFR-MAINT-001. Verification is performed through the method specified by each requirement.
- DOC-TRACE-01: Non-use-case trace target for NFR-MAINT-002. Verification is performed through the method specified by each requirement.
- ENV-PLATFORM-01: Non-use-case trace target for NFR-COMPAT-002. Verification is performed through the method specified by each requirement.
- QA-AI-01: Non-use-case trace target for NFR-AI-002. Verification is performed through the method specified by each requirement.
- QA-PERF-01: Non-use-case trace target for NFR-PERF-001. Verification is performed through the method specified by each requirement.
- QA-REL-01: Non-use-case trace target for NFR-REL-002. Verification is performed through the method specified by each requirement.
- QA-UX-01: Non-use-case trace target for NFR-UX-002. Verification is performed through the method specified by each requirement.
- SEC-ASVS: Non-use-case trace target for NFR-SEC-002. Verification is performed through the method specified by each requirement.

### 8.4 Requirements Traceability Matrix

| Requirement ID | Use case or trace target | Evidence / reference | Verification method |
|---|---|---|---|
| FR-AUTH-001 | UC-AUTH-01 | AccountController.cs | Test |
| FR-AUTH-002 | UC-AUTH-02 | AccountController.cs | Test |
| FR-AUTH-003 | UC-AUTH-03 | AccountController.cs | Test |
| FR-AUTH-004 | UC-AUTH-04 | ServiceCollectionExtensions.cs; ChatController.cs | Test |
| FR-ADMIN-001 | UC-ADMIN-01 | AdminController.cs; CourseAssignmentService.cs | Test |
| FR-ADMIN-002 | UC-ADMIN-01 | AdminController.cs | Test |
| FR-ADMIN-003 | UC-ADMIN-02 | AdminController.cs; CourseAssignmentService.cs; CourseAccessService.cs | Test |
| FR-ADMIN-004 | UC-ADMIN-01 | AdminController.cs; ApplicationRoles.cs | Test |
| FR-ADMIN-005 | UC-ADMIN-02 | DepartmentController.cs; DepartmentService.cs | Test |
| FR-ADMIN-006 | UC-ADMIN-02 | DepartmentService.cs; CourseAccessService.cs; cleanup migration | Test |
| FR-COURSE-001 | UC-COURSE-01 | CourseController.cs; CourseService.cs | Test |
| FR-COURSE-002 | UC-COURSE-01 | CourseController.cs | Test |
| FR-COURSE-003 | UC-COURSE-02 | CourseController.cs; CourseAccessService.cs | Test |
| FR-COURSE-004 | UC-CHAT-01 | ChatController.cs | Test |
| FR-COURSE-005 | UC-COURSE-01 | CourseController.cs; CourseService.cs | Test |
| FR-COURSE-006 | UC-COURSE-01 | CourseController.cs; CourseService.cs | Test |
| FR-DOC-001 | UC-DOC-01 | DocumentController.cs; DocumentService.cs | Test |
| FR-DOC-002 | UC-DOC-01 | DocumentController.cs; ApplicationRoles.cs | Test |
| FR-DOC-003 | UC-DOC-02 | DocumentService.cs; ChunkingService.cs | Test |
| FR-DOC-004 | UC-DOC-03 | DocumentController.cs; Views/Document/Details.cshtml | Test |
| FR-DOC-005 | UC-DOC-01 | DocumentService.cs | Test |
| FR-CHAT-001 | UC-CHAT-01 | ChatService.cs; Rag/RagRetrievalService.cs | Test |
| FR-CHAT-002 | UC-CHAT-01 | ChatController.cs | Test |
| FR-CHAT-003 | UC-CHAT-01 | ChatService.cs | Test |
| FR-CHAT-004 | UC-CHAT-02 | ChatService.cs; ChatSessionRepository.cs | Test |
| FR-TESTSET-001 | UC-TESTSET-01 | TestSetGeneratorController.cs; TestSetGeneratorService.cs | Test |
| FR-TESTSET-002 | UC-TESTSET-01 | TestSetGeneratorController.cs | Test |
| FR-TESTSET-003 | UC-TESTSET-01 | TestSetGenerationJobManager.cs | Test |
| FR-TESTSET-004 | UC-TESTSET-02 | TestSetGeneratorController.cs; TestSetGeneratorService.cs | Demonstration |
| FR-EVAL-001 | UC-EVAL-01 | EvaluationController.cs; BenchmarkService.cs | Test |
| FR-EVAL-002 | UC-EVAL-01 | EvaluationController.cs | Test |
| FR-EVAL-003 | UC-EVAL-01 | BenchmarkService.cs | Test |
| FR-EVAL-004 | UC-EVAL-02 | EvaluationController.cs | Demonstration |
| FR-FT-001 | UC-FT-01 | FinetuneController.cs; FinetuneService.cs | Test |
| FR-FT-002 | UC-FT-01 | FinetuneController.cs | Test |
| FR-FT-003 | UC-FT-02 | FinetuneController.cs; FinetuneService.cs | Test |
| FR-FT-004 | UC-EVAL-01 | AI/HuggingFaceFineTunedModelService.cs; BenchmarkService.cs | Test |
| NFR-SEC-001 | UC-AUTH-04, UC-DOC-01, UC-CHAT-01 | DocumentController.cs; ChatController.cs; ApplicationRoles.cs | Test |
| NFR-SEC-002 | SEC-ASVS | source-evidence.md; OWASP ASVS [R6] | Inspection |
| NFR-PERF-001 | QA-PERF-01 | source-evidence.md | Analysis |
| NFR-PERF-002 | UC-DOC-01 | ServiceCollectionExtensions.cs; DocumentController.cs | Test |
| NFR-REL-001 | UC-DOC-02 | DocumentService.cs | Test |
| NFR-REL-002 | QA-REL-01 | source-evidence.md | Test |
| NFR-UX-001 | UC-NAV-01 | Views/Shared/_Layout.cshtml | Demonstration |
| NFR-UX-002 | QA-UX-01 | source-evidence.md; WCAG 2.2 [R7] | Inspection |
| NFR-MAINT-001 | ARCH-LAYERS | MVC, BLL, DAL, and Tests project files | Inspection |
| NFR-MAINT-002 | DOC-TRACE-01 | validate_srs.py; requirements.json | Test |
| NFR-COMPAT-001 | UC-DOC-01, UC-DOC-02 | DocumentController.cs; TextExtractors/PptTextExtractor.cs | Test |
| NFR-COMPAT-002 | ENV-PLATFORM-01 | MVC and DAL project files | Test |
| NFR-AI-001 | UC-CHAT-01 | ChatService.cs | Inspection |
| NFR-AI-002 | QA-AI-01, UC-EVAL-01 | source-evidence.md | Analysis |

## 9. Limitations and Future Scope

This section is non-normative. It records limitations that affect interpretation of the implemented system and future work that may be considered after the current release.

### 9.1 Current Limitations

- Student Chat course selection currently uses the complete course list returned by the course service; there is no implemented student enrollment model.
- Development startup handles migrations, role seeding, demo accounts, and embedding-model records only in the Development environment.
- Identity settings are suitable for demonstration and require hardening before production deployment.
- Development seed credentials are tracked in development configuration and displayed by the login view for demonstration convenience; this is not production credential handling.
- Background queues and test-set job tracking are process-local and do not provide restart recovery.
- File validation includes extension, content-type, size, and binary-signature checks, but no malware scanning or isolated parser process.
- External AI service availability and rate limits remain runtime dependencies.
- Benchmark metrics labelled as internal approximations are not certified external RAGAS evaluation.
- Stored assistant `ConfidenceScore` represents retrieved-chunk similarity semantics, not calibrated model confidence.
- Benchmark chunking configuration does not prove arbitrary runtime rechunking of already indexed documents.
- Automated tests cover important services and authorization configuration, but a full browser-level authorization suite is not present.

### 9.2 Future Scope

- Student enrollment and student-specific course authorization.
- Production migration and seed workflows separate from development startup.
- Stronger Identity policy, lockout behavior, confirmed-account policy, and credential handling.
- Durable background job storage with restart recovery and idempotency.
- Malware scanning and isolated parsing for uploaded documents.
- Documented response-time, concurrency, availability, and AI-quality gates based on reproducible measurements.
- External evaluator integration if formal RAGAS-compatible reporting is required.
- Browser-level end-to-end tests for all role and course authorization paths.
- Accessibility audit and remediation against the selected WCAG 2.2 Level AA target.

## References

- [R6] [OWASP Application Security Verification Standard](https://owasp.org/www-project-application-security-verification-standard/)
- [R7] [Web Content Accessibility Guidelines (WCAG) 2.2](https://www.w3.org/TR/WCAG22/)

## Appendix A. Requirement Catalog Summary

| Category | Count |
|---|---:|
| functional | 35 |
| non-functional | 14 |
| Total | 49 |

## Appendix B. Source Evidence Summary

This SRS is based on the repository evidence map and requirement catalog. Concise entries in the traceability matrix identify source files or reference labels used to derive each requirement; the requirement blocks and References section retain the detailed locations. When existing prose documentation disagrees with source code, implemented code and tests take precedence.
