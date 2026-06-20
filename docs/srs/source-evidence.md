# SRS Source Evidence Map

## Purpose and Evidence Rules

This document records repository evidence for the Software Requirements Specification (SRS). The implementation is the source of truth. Each factual statement below cites at least one repository path and, where useful, a symbol or line location. README and research-report statements are treated as claims until corroborated by executable code, configuration, migrations, or tests.

## 1. Verified Application Identity and Technology Baseline

| Area | Verified baseline | Repository evidence |
|---|---|---|
| Application identity | The repository implements a Vietnamese academic RAG workbench for course-scoped document ingestion, retrieval-based chat, test-set generation, benchmarking, and fine-tuning dataset preparation. | `src/PRN222.MVC/Views/Shared/_Layout.cshtml` (`RAG Workbench` branding and navigation); `src/PRN222.BLL/Services/DocumentService.cs` (`ProcessDocumentAsync`); `src/PRN222.BLL/Services/ChatService.cs` (`AskQuestionAsync`); `src/PRN222.BLL/Services/TestSetGeneratorService.cs`; `src/PRN222.BLL/Services/BenchmarkService.cs`; `src/PRN222.BLL/Services/FinetuneService.cs` |
| Runtime | All application and test projects target .NET 8 (`net8.0`). | `src/PRN222.MVC/PRN222.MVC.csproj:16`; `src/PRN222.BLL/PRN222.BLL.csproj:17`; `src/PRN222.DAL/PRN222.DAL.csproj:4`; `src/PRN222.Tests/PRN222.Tests.csproj:4` |
| Architecture | The solution is separated into ASP.NET Core MVC presentation, BLL services/DTOs, DAL entities/repositories/EF configurations, and xUnit tests. MVC references BLL and DAL; BLL references DAL. | `src/PRN222.MVC/PRN222.MVC.csproj:4-5`; `src/PRN222.BLL/PRN222.BLL.csproj:4`; `src/PRN222.MVC/Infrastructure/ServiceCollectionExtensions.cs` (`AddApplicationMvc`, `AddBusinessServices`, `AddDataAccess`); `src/PRN222.Tests/PRN222.Tests.csproj:25-27` |
| Web framework | The web application uses ASP.NET Core MVC controllers and Razor views with conventional routing. | `src/PRN222.MVC/Infrastructure/ServiceCollectionExtensions.cs:14` (`AddControllersWithViews`); `src/PRN222.MVC/Program.cs:33-36` (`MapControllerRoute`); `src/PRN222.MVC/Controllers/HomeController.cs`; `src/PRN222.MVC/Views/Home/Index.cshtml` |
| Authentication | Authentication and role storage use ASP.NET Core Identity with `ApplicationUser`, `IdentityRole`, EF stores, cookie paths, and default token providers. | `src/PRN222.DAL/Entities/ApplicationUser.cs`; `src/PRN222.DAL/Data/ChatbotDbContext.cs:10`; `src/PRN222.MVC/Infrastructure/ServiceCollectionExtensions.cs:39-56` |
| Persistence | EF Core 8.0.11 uses the SQL Server provider and migrations in `PRN222.DAL`; query splitting is enabled. | `src/PRN222.DAL/PRN222.DAL.csproj:10-12`; `src/PRN222.MVC/Infrastructure/ServiceCollectionExtensions.cs:29-36`; `src/PRN222.DAL/Migrations/ChatbotDbContextModelSnapshot.cs` |
| Department governance | Staff and courses may belong to a department. Effective staff course scope requires both explicit assignment and matching non-null department identifiers. Department changes reconcile stale assignments transactionally, and a one-time migration deletes legacy invalid rows. | `src/PRN222.BLL/Services/CourseAccessService.cs`; `src/PRN222.BLL/Services/DepartmentService.cs`; `src/PRN222.DAL/Migrations/20260615102155_EnforceDepartmentCourseAssignments.cs`; `src/PRN222.Tests/Services/CourseAccessServiceTests.cs`; `src/PRN222.Tests/BLL/DepartmentAuthorizationTests.cs` |
| Primary LLM | `ILlmService` resolves to `GeminiLlmService`, which calls the Gemini API for answers and Q&A generation. | `src/PRN222.MVC/Infrastructure/ServiceCollectionExtensions.cs:93`; `src/PRN222.BLL/Services/AI/GeminiLlmService.cs` |
| Embedding providers | The implementation contains Google Gemini (`gemini-embedding-001`), Hugging Face multilingual E5 (`multilingual-e5-base`), and OpenAI (`text-embedding-3-small`) embedding adapters. Gemini is the default injected `IEmbeddingService`; the factory supports model-specific selection for benchmarks. | `src/PRN222.MVC/Infrastructure/ServiceCollectionExtensions.cs:83-91`; `src/PRN222.MVC/Infrastructure/ApplicationStartupExtensions.cs:156-166`; `src/PRN222.BLL/Services/EmbeddingServiceFactory.cs`; `src/PRN222.BLL/Services/AI/GeminiEmbeddingService.cs`; `src/PRN222.BLL/Services/AI/MultilingualE5EmbeddingService.cs`; `src/PRN222.BLL/Services/AI/OpenAIEmbeddingService.cs` |
| Fine-tuned provider | Fine-tuned inference is implemented through a configurable Hugging Face inference endpoint/service. | `src/PRN222.MVC/Infrastructure/ServiceCollectionExtensions.cs:94`; `src/PRN222.BLL/Services/AI/HuggingFaceFineTunedModelService.cs` |
| File extraction | PDF extraction uses iText 7; DOCX and PPTX extraction use DocumentFormat.OpenXml. Legacy PPT extraction is a best-effort recovery of readable lines from raw binary bytes decoded as Unicode and UTF-8, rather than structured slide parsing. Supported upload extensions are `.pdf`, `.docx`, `.pptx`, and `.ppt`. | `src/PRN222.BLL/PRN222.BLL.csproj:8-9`; `src/PRN222.BLL/Services/TextExtractors/PdfTextExtractor.cs`; `src/PRN222.BLL/Services/TextExtractors/DocxTextExtractor.cs`; `src/PRN222.BLL/Services/TextExtractors/PptxTextExtractor.cs`; `src/PRN222.BLL/Services/TextExtractors/PptTextExtractor.cs:6-55`; `src/PRN222.MVC/Controllers/DocumentController.cs:148-155` |
| Local file storage | Uploaded files are stored below `App_Data/uploads` using generated filenames; the MVC request limit is 50 MiB. | `src/PRN222.BLL/Services/DocumentService.cs:114-121`; `src/PRN222.MVC/Controllers/DocumentController.cs:135`; `src/PRN222.MVC/Infrastructure/ServiceCollectionExtensions.cs:108-115`; `src/PRN222.MVC/Program.cs:11-14` |
| Background work | Document processing and test-set generation can run through an in-process background queue and hosted service. | `src/PRN222.MVC/Infrastructure/ServiceCollectionExtensions.cs:101-104`; `src/PRN222.MVC/Infrastructure/BackgroundTaskQueue.cs`; `src/PRN222.MVC/Infrastructure/QueuedHostedService.cs`; `src/PRN222.BLL/Services/DocumentService.cs:263`; `src/PRN222.MVC/Controllers/TestSetGeneratorController.cs:69-86` |
| Automated tests | The test project uses xUnit, Moq, FluentAssertions, the .NET test SDK, and Coverlet. Tests cover MVC authorization/configuration and core document, chat, chunking, retrieval, benchmark, fine-tune, test-set, and vector behavior. | `src/PRN222.Tests/PRN222.Tests.csproj:11-16`; `src/PRN222.Tests/MVC/AuthorizationConfigurationTests.cs`; `src/PRN222.Tests/Services/DocumentServiceTests.cs`; `src/PRN222.Tests/Services/ChatServiceTests.cs`; `src/PRN222.Tests/Services/BenchmarkServiceTests.cs`; `src/PRN222.Tests/Services/RagRetrievalServiceTests.cs`; `src/PRN222.Tests/Services/TestSetGeneratorServiceTests.cs` |

## 2. Verified Authorization and Visibility Matrix

Role constants and aggregate policies are defined in `src/PRN222.MVC/Infrastructure/ApplicationRoles.cs:3-20`:

- `Management`: Admin, HeadLecturer, Lecturer.
- `DocumentUpload`: Admin, HeadLecturer.
- `ModelOperations`: Admin, HeadLecturer.
- `ChatUsers`: Student, Lecturer, HeadLecturer, Admin.
- Only HeadLecturer and Lecturer are staff roles that Admin may create and assign to a department and same-department courses.

| Capability | Admin | HeadLecturer | Lecturer | Student | Enforcement evidence |
|---|---|---|---|---|---|
| Sign in/out | Yes | Yes | Yes | Yes | `src/PRN222.MVC/Controllers/AccountController.cs:20-51,93-98` |
| Self-register | Registration creates only a Student account | Registration creates only a Student account | Registration creates only a Student account | Yes | `src/PRN222.MVC/Controllers/AccountController.cs:53-91`, especially `AddToRoleAsync(... Student)` |
| Create staff accounts | Creates HeadLecturer and Lecturer accounts with one department and at least one same-department course | No | No | No | `src/PRN222.MVC/Controllers/AdminController.cs`; `src/PRN222.BLL/Services/CourseAssignmentService.cs`; `src/PRN222.MVC/Infrastructure/ApplicationRoles.cs` (`StaffCreatableByAdmin`) |
| Manage departments | Creates departments and moves eligible staff or courses between departments | No | No | No | `src/PRN222.MVC/Controllers/DepartmentController.cs`; `src/PRN222.BLL/Services/DepartmentService.cs` |
| Assign staff to courses | Replaces the staff department and validated same-department course set transactionally | No | No | No | `src/PRN222.MVC/Controllers/AdminController.cs`; `src/PRN222.BLL/Services/CourseAssignmentService.cs`; `src/PRN222.BLL/Services/CourseAccessService.cs` |
| Create/edit/delete courses | Yes | No | No | No | `src/PRN222.MVC/Controllers/CourseController.cs:33,41,64,73,100,126`; `src/PRN222.MVC/Views/Course/Index.cshtml` (`isAdmin` action visibility) |
| View courses | All courses | Same-department assigned courses only | Same-department assigned courses only | Courses are available through Chat, not the management Course controller | `src/PRN222.BLL/Services/CourseAccessService.cs`; `src/PRN222.MVC/Controllers/CourseController.cs`; `src/PRN222.MVC/Controllers/ChatController.cs` |
| View document list/metadata | All courses | Same-department assigned courses only | Same-department assigned courses only | No document-management access | `src/PRN222.BLL/Services/CourseAccessService.cs`; `src/PRN222.MVC/Controllers/DocumentController.cs` |
| View extracted text and raw chunks | Yes | Yes, same-department assigned courses only | No; controller removes preview and chunks and view suppresses both sections | No | `src/PRN222.MVC/Controllers/DocumentController.cs`; `src/PRN222.MVC/Views/Document/Details.cshtml` (`canViewChunks`) |
| Upload/process/delete documents | Yes, all courses | Yes, same-department assigned courses only | No | No | `src/PRN222.BLL/Services/CourseAccessService.cs`; `src/PRN222.MVC/Controllers/DocumentController.cs`; `src/PRN222.MVC/Infrastructure/ApplicationRoles.cs` (`DocumentUpload`) |
| Chat | Yes, all courses | Yes, same-department assigned courses only | Yes, same-department assigned courses only | Yes, all courses returned by the current Chat course query | `src/PRN222.BLL/Services/CourseAccessService.cs`; `src/PRN222.MVC/Controllers/ChatController.cs` |
| Own chat sessions | Yes | Yes | Yes | Yes | `src/PRN222.MVC/Controllers/ChatController.cs:34-61`; `src/PRN222.BLL/Services/ChatService.cs:34-73,155-173`; `src/PRN222.DAL/Repositories/ChatSessionRepository.cs` |
| Generate test sets | Yes | Yes, same-department assigned courses only | No | No | `src/PRN222.BLL/Services/CourseAccessService.cs`; `src/PRN222.MVC/Controllers/TestSetGeneratorController.cs`; `src/PRN222.MVC/Infrastructure/ApplicationRoles.cs` (`ModelOperations`) |
| Generate/export fine-tuning datasets | Yes | Yes, same-department assigned courses only | No | No | `src/PRN222.BLL/Services/CourseAccessService.cs`; `src/PRN222.MVC/Controllers/FinetuneController.cs`; `src/PRN222.MVC/Infrastructure/ApplicationRoles.cs` (`ModelOperations`) |
| Run/view/export benchmarks | Yes | Yes, same-department assigned courses only | No | No | `src/PRN222.BLL/Services/CourseAccessService.cs`; `src/PRN222.MVC/Controllers/EvaluationController.cs`; `src/PRN222.MVC/Infrastructure/ApplicationRoles.cs` (`ModelOperations`) |

### UI Visibility

- The shared layout shows dashboard, document, and course navigation to management users; model-operation navigation appears only for Admin and HeadLecturer; the combined `Nhân sự & Khoa` administration workspace appears only for Admin. (`src/PRN222.MVC/Views/Shared/_Layout.cshtml`; `src/PRN222.MVC/Views/Shared/_StaffGovernanceTabs.cshtml`)
- The top bar shows account/course administration actions to Admin and document upload to HeadLecturer; Lecturer receives neither action. (`src/PRN222.MVC/Views/Shared/_Layout.cshtml:112-132`)
- Course create/edit/delete controls are rendered only for Admin, while upload is rendered for Admin and HeadLecturer. (`src/PRN222.MVC/Views/Course/Index.cshtml`)
- Document process/delete controls are rendered only for Admin and HeadLecturer, and raw content/chunks are hidden when `CanViewChunks` is false. (`src/PRN222.MVC/Views/Document/Details.cshtml`)
- UI hiding is supplementary: controller role attributes and course checks provide server-side enforcement. (`src/PRN222.MVC/Controllers/AdminController.cs:12`; `src/PRN222.MVC/Controllers/CourseController.cs:10,33-126`; `src/PRN222.MVC/Controllers/DocumentController.cs:10,123-260,266-314`; `src/PRN222.MVC/Controllers/EvaluationController.cs:15,251-294`; `src/PRN222.MVC/Controllers/FinetuneController.cs:11,106-144`; `src/PRN222.MVC/Controllers/TestSetGeneratorController.cs:10,164-202`)

## 3. Implemented Workflows

### 3.1 Authentication and Account Provisioning

1. A user signs in through Identity `PasswordSignInAsync`; staff are redirected to the dashboard and non-staff to a new chat. (`src/PRN222.MVC/Controllers/AccountController.cs:28-51,106-122`)
2. Public registration creates an `ApplicationUser`, assigns only the Student role, signs the user in, and redirects to Chat. (`src/PRN222.MVC/Controllers/AccountController.cs:53-91`)
3. Admin can create only HeadLecturer or Lecturer users, with a department and at least one same-department course required, and can later replace the governed department/course assignment set. (`src/PRN222.MVC/Controllers/AdminController.cs`; `src/PRN222.BLL/Services/CourseAssignmentService.cs`; `src/PRN222.BLL/Services/CourseAccessService.cs`)
4. Admin can create departments and move eligible staff or courses. Each move and stale-assignment reconciliation share one transaction; the UI reports the number of revoked assignments. (`src/PRN222.MVC/Controllers/DepartmentController.cs`; `src/PRN222.BLL/Services/DepartmentService.cs`)
4. In Development only, startup applies migrations, seeds roles, optional configured demo accounts, and configured embedding-model records. (`src/PRN222.MVC/Program.cs:16-19`; `src/PRN222.MVC/Infrastructure/ApplicationStartupExtensions.cs:10-94`)

### 3.2 Course and Document Ingestion

1. Admin creates and maintains courses; HeadLecturer and Lecturer receive read access only when a course is explicitly assigned and belongs to the same department. (`src/PRN222.BLL/Services/CourseAccessService.cs`; `src/PRN222.MVC/Controllers/CourseController.cs`)
2. Admin or HeadLecturer selects an accessible course and uploads a PDF, DOCX, PPTX, or PPT file of at most 50 MiB. (`src/PRN222.MVC/Controllers/DocumentController.cs:123-181`)
3. `UploadDocumentAsync` validates extractor support and the binary file signature, saves the file under `App_Data/uploads`, and persists document metadata with `DocumentStatus.Uploaded`. (`src/PRN222.BLL/Services/DocumentService.cs:98-152`; `src/PRN222.DAL/Entities/Enums/DocumentStatus.cs`)
4. Processing reads the file with the selected extractor, chunks extracted text, generates one embedding per chunk through the default embedding provider, stores chunks/vectors, and marks the document indexed; errors produce a failed status and error message. (`src/PRN222.BLL/Services/DocumentService.cs:154-261`)
5. The fixed-size chunker defaults to size 512 and overlap 50. (`src/PRN222.BLL/Services/ChunkingService.cs:20-77`)
6. Reprocessing and deletion remove dependent citations/Q&A where needed before replacing or deleting chunk data. (`src/PRN222.BLL/Services/DocumentService.cs:194-201,322-405`)

### 3.3 RAG Chat, Citations, and Sessions

1. The authenticated user opens a new or existing user-owned session and chooses a visible course. (`src/PRN222.MVC/Controllers/ChatController.cs:29-62`; `src/PRN222.BLL/Services/ChatService.cs:34-73`)
2. The controller validates the question/course and enforces same-department explicit-assignment scope for HeadLecturer and Lecturer. (`src/PRN222.BLL/Services/CourseAccessService.cs`; `src/PRN222.MVC/Controllers/ChatController.cs`)
3. The service creates or resolves a user-owned session, saves the user message, embeds the question, and searches indexed documents in the selected course. (`src/PRN222.BLL/Services/ChatService.cs:89-102,155-197`; `src/PRN222.BLL/Services/Rag/RagRetrievalService.cs:32-54`)
4. Retrieval ranks stored vectors by similarity, takes qualifying top chunks, and builds source-labelled context. (`src/PRN222.BLL/Services/Rag/RagRetrievalService.cs:57-117,145-173`; `src/PRN222.BLL/Services/ChatService.cs:95-104`)
5. Gemini generates an answer from the prompt and retrieved context. The assistant message's `ConfidenceScore` is populated from the highest-ranked retrieved chunk's similarity score, or zero when no chunk is retrieved; it is not calibrated answer or model confidence. Citations contain chunk, similarity, and snippet data. (`src/PRN222.BLL/Services/ChatService.cs:104-151`; `src/PRN222.BLL/Services/AI/GeminiLlmService.cs:37-116`)
6. Session and message queries are constrained by user ID, preventing one user from opening another user's session through normal service/repository paths. (`src/PRN222.BLL/Services/ChatService.cs:34-69,171-172`; `src/PRN222.DAL/Repositories/ChatSessionRepository.cs`)

### 3.4 Test-Set Generation

1. Admin or HeadLecturer selects an accessible course and requests questions per chunk and a maximum chunk count. (`src/PRN222.MVC/Controllers/TestSetGeneratorController.cs:37-67`)
2. A per-course job manager prevents duplicate concurrent jobs; work runs in the background queue and supports status polling and cooperative stop requests. (`src/PRN222.MVC/Controllers/TestSetGeneratorController.cs:58-111`; `src/PRN222.BLL/Services/TestSetGenerationJobManager.cs`)
3. The generator selects indexed course chunks with at least 150 characters, asks the LLM for Q&A JSON, persists generated `QAPair` records linked to source chunks, and reports progress. (`src/PRN222.BLL/Services/TestSetGeneratorService.cs:15,54-155`)
4. The UI endpoints expose statistics, paginated preview, clearing of auto-generated pairs, and job status. (`src/PRN222.MVC/Controllers/TestSetGeneratorController.cs:113-159`; `src/PRN222.BLL/Services/TestSetGeneratorService.cs:157-239`)

### 3.5 Benchmark and Evaluation

1. Admin or HeadLecturer creates a course-scoped run with experiment type, chunk strategy, embedding model, chunk size, and overlap. (`src/PRN222.MVC/Controllers/EvaluationController.cs:179-220`)
2. The benchmark prefers Q&A pairs generated from uploaded course chunks and fails the run when none exist. (`src/PRN222.BLL/Services/BenchmarkService.cs:147-166`)
3. RAG runs generate or reuse embeddings for the selected model, retrieve top chunks, generate an answer, calculate internal approximation metrics, and persist per-question results. Fine-tuned runs call the configured fine-tuned model provider. (`src/PRN222.BLL/Services/BenchmarkService.cs:119-245,257-333,335-493`)
4. Users with model-operation access can view summaries/details, compare accessible runs, and export run results as CSV. (`src/PRN222.MVC/Controllers/EvaluationController.cs:37-177,225-249`)

### 3.6 Fine-Tuning Dataset

1. Admin or HeadLecturer opens an accessible course dataset and requests a target Q&A count. (`src/PRN222.MVC/Controllers/FinetuneController.cs:27-78`)
2. The service uses course chunks and Gemini Q&A generation, avoids exact duplicate questions, links generated pairs to source chunks, and applies retry/delay configuration. (`src/PRN222.BLL/Services/FinetuneService.cs:68-183`)
3. Generated pairs can be exported as OpenAI-style JSONL. (`src/PRN222.MVC/Controllers/FinetuneController.cs:81-97`; `src/PRN222.BLL/Services/FinetuneService.cs:185-234`)

## 4. Core Domain Model and Relationships

| Entity/relationship | Verified meaning | Repository evidence |
|---|---|---|
| `Department` one-to-many `ApplicationUser` and `Course` | Staff and courses may belong to a department. A staff-course assignment is effective only when both department identifiers are non-null and equal. | `src/PRN222.DAL/Entities/Department.cs`; `src/PRN222.DAL/Entities/ApplicationUser.cs`; `src/PRN222.DAL/Entities/Course.cs`; `src/PRN222.BLL/Services/CourseAccessService.cs` |
| `ApplicationUser` many-to-many `Course` | Explicit staff course assignment is represented by join entity `ApplicationUserCourse`, with a composite key and assignment timestamp. Department equality is an additional service-level authorization condition. | `src/PRN222.DAL/Entities/ApplicationUserCourse.cs`; `src/PRN222.DAL/Data/Configurations/ApplicationUserCourseConfiguration.cs`; `src/PRN222.BLL/Services/CourseAccessService.cs` |
| `Course` one-to-many `Document` | Every document belongs to one course; a course owns its document collection. | `src/PRN222.DAL/Entities/Course.cs`; `src/PRN222.DAL/Entities/Document.cs`; `src/PRN222.DAL/Data/Configurations/DocumentConfiguration.cs:40-43` |
| `Document` one-to-many `DocumentChunk` | Extracted content is divided into ordered chunks owned by the source document. | `src/PRN222.DAL/Entities/Document.cs`; `src/PRN222.DAL/Entities/DocumentChunk.cs`; `src/PRN222.DAL/Data/Configurations/DocumentChunkConfiguration.cs:20-23` |
| `DocumentChunk` one-to-many `ChunkEmbedding` | A chunk may store vectors for multiple embedding model names. | `src/PRN222.DAL/Entities/DocumentChunk.cs`; `src/PRN222.DAL/Entities/ChunkEmbedding.cs`; `src/PRN222.DAL/Data/Configurations/ChunkEmbeddingConfiguration.cs:20-23` |
| `Document` to `EmbeddingModel` | A processed document can reference its embedding model; configured model records include provider, dimensions, and active state. | `src/PRN222.DAL/Entities/Document.cs`; `src/PRN222.DAL/Entities/EmbeddingModel.cs`; `src/PRN222.DAL/Data/Configurations/DocumentConfiguration.cs:45-48` |
| `ApplicationUser` one-to-many `ChatSession` | Each chat session has a required user owner. | `src/PRN222.DAL/Entities/ChatSession.cs`; `src/PRN222.DAL/Data/Configurations/ChatSessionConfiguration.cs:23-27` |
| `ChatSession` one-to-many `ChatMessage` | Sessions contain ordered user/assistant messages. | `src/PRN222.DAL/Entities/ChatSession.cs`; `src/PRN222.DAL/Entities/ChatMessage.cs`; `src/PRN222.DAL/Data/Configurations/ChatMessageConfiguration.cs:20-23` |
| `ChatMessage` one-to-many `ChatCitation` | Assistant messages can cite retrieved chunks with similarity and snippet evidence. | `src/PRN222.DAL/Entities/ChatMessage.cs`; `src/PRN222.DAL/Entities/ChatCitation.cs`; `src/PRN222.DAL/Data/Configurations/ChatCitationConfiguration.cs` |
| `Course`/`DocumentChunk` to `QAPair` | Q&A pairs are course-scoped; generated pairs may reference the source chunk, while manual pairs have no chunk reference. | `src/PRN222.DAL/Entities/QAPair.cs`; `src/PRN222.DAL/Data/Configurations/QAPairConfiguration.cs`; `src/PRN222.BLL/Services/TestSetGeneratorService.cs:157-203` |
| `BenchmarkRun` one-to-many `BenchmarkResult` | A run stores configuration, selected embedding model, optional course scope, status/timestamps, and result rows. | `src/PRN222.DAL/Entities/BenchmarkRun.cs`; `src/PRN222.DAL/Entities/BenchmarkResult.cs`; `src/PRN222.DAL/Data/Configurations/BenchmarkRunConfiguration.cs`; `src/PRN222.DAL/Data/Configurations/BenchmarkResultConfiguration.cs` |

## 5. Confirmed External Dependencies

- SQL Server is the configured relational database provider. (`src/PRN222.DAL/PRN222.DAL.csproj:11`; `src/PRN222.MVC/Infrastructure/ServiceCollectionExtensions.cs:31`)
- Google Gemini supplies the default LLM and default embedding service; operation requires `Gemini:ApiKey`. (`src/PRN222.MVC/Infrastructure/ServiceCollectionExtensions.cs:89-93`; `src/PRN222.BLL/Services/AI/GeminiLlmService.cs:24-32`; `src/PRN222.BLL/Services/AI/GeminiEmbeddingService.cs:18-24`)
- Hugging Face supplies multilingual E5 embeddings and configurable fine-tuned inference; a Bearer token is added only when configured, and unsupported hosted fine-tuned models require a dedicated endpoint URL. (`src/PRN222.BLL/Services/AI/MultilingualE5EmbeddingService.cs:26-54`; `src/PRN222.BLL/Services/AI/HuggingFaceFineTunedModelService.cs:30-41,130-146`)
- OpenAI supplies the optional `text-embedding-3-small` adapter; operation requires `OpenAI:ApiKey`. (`src/PRN222.BLL/Services/AI/OpenAIEmbeddingService.cs:11-43`)
- iText 7 and DocumentFormat.OpenXml parse supported PDF, DOCX, and PPTX source documents. (`src/PRN222.BLL/PRN222.BLL.csproj:8-9`; `src/PRN222.BLL/Services/TextExtractors/PdfTextExtractor.cs`; `src/PRN222.BLL/Services/TextExtractors/DocxTextExtractor.cs`; `src/PRN222.BLL/Services/TextExtractors/PptxTextExtractor.cs`)
- Bootstrap, Bootstrap Icons, Google Fonts, jQuery, and jQuery validation are loaded by the Razor UI. (`src/PRN222.MVC/Views/Shared/_Layout.cshtml:37-41,148-149`; `src/PRN222.MVC/Views/Shared/_ValidationScriptsPartial.cshtml:1-2`)

## 6. Limitations and Discrepancies

1. **README runtime is stale.** README advertises .NET 9, while every project targets `net8.0` and EF/Identity packages are 8.0.11. The SRS must state .NET 8. (`README.md:3,70,107`; `src/PRN222.MVC/PRN222.MVC.csproj:9,16`; `src/PRN222.BLL/PRN222.BLL.csproj:17`; `src/PRN222.DAL/PRN222.DAL.csproj:4,10-12`; `src/PRN222.Tests/PRN222.Tests.csproj:4`)
2. **README Lecturer permissions are stale.** README says Lecturer can manage assigned courses, upload materials, and view evaluations. Code permits Lecturer only assigned-course dashboard/course/document metadata/chat access; `DocumentUpload` and `ModelOperations` exclude Lecturer, and raw extracted text/chunks are removed. (`README.md:98-99`; `src/PRN222.MVC/Infrastructure/ApplicationRoles.cs:10-13`; `src/PRN222.MVC/Controllers/DocumentController.cs:113-119`; `src/PRN222.MVC/Controllers/EvaluationController.cs:15`; `src/PRN222.MVC/Controllers/FinetuneController.cs:11`; `src/PRN222.MVC/Controllers/TestSetGeneratorController.cs:10`)
3. **Student course scope differs from staff course scope.** Chat explicitly exempts Student from assignment filtering, so Student currently receives all courses from `ICourseService`; there is no student enrollment model in the repository. (`src/PRN222.MVC/Controllers/ChatController.cs:99-120`; `src/PRN222.DAL/Entities/ApplicationUserCourse.cs`)
4. **Reported benchmark values are not repository-verifiable evidence.** `Report_RAG_vs_Finetuning.md` contains fixed result tables, but those values are not fixtures or asserted outputs in source/tests. They must not become SRS acceptance baselines without exported run records or reproducible evidence. (`Report_RAG_vs_Finetuning.md:65-80`; `src/PRN222.Tests/Services/BenchmarkServiceTests.cs`)
5. **“RAGAS” is an internal approximation, not a confirmed RAGAS framework integration.** `BenchmarkService` calculates keyword/vector/similarity heuristics labelled as approximate RAGAS metrics; no RAGAS package or external evaluator is referenced by the project files. (`src/PRN222.BLL/Services/BenchmarkService.cs:285-411`; `src/PRN222.BLL/PRN222.BLL.csproj`)
6. **Chunking strategy configuration is only partially operational.** Benchmark runs persist requested strategy/size/overlap, but document ingestion uses `ChunkText(extractedText)` defaults, and benchmark retrieval evaluates already stored chunks rather than rechunking per run. Results therefore do not demonstrate a true runtime comparison of arbitrary chunking configurations. (`src/PRN222.BLL/Services/DocumentService.cs:181-182`; `src/PRN222.BLL/Services/ChunkingService.cs:20`; `src/PRN222.BLL/Services/BenchmarkService.cs:133-137,170-179`)
7. **Production database initialization is incomplete.** Migrations, role seeding, demo-account seeding, and embedding-model activation run only in Development. The repository does not show an equivalent production deployment migration step. (`src/PRN222.MVC/Program.cs:16-19`; `src/PRN222.MVC/Infrastructure/ApplicationStartupExtensions.cs:10-94`)
8. **Identity policy is demo-oriented.** Password requirements disable digits, lowercase, uppercase, and non-alphanumeric characters and require only six characters; sign-in does not enable lockout on failure; confirmed accounts are not required. (`src/PRN222.MVC/Infrastructure/ServiceCollectionExtensions.cs:39-49`; `src/PRN222.MVC/Controllers/AccountController.cs:35-39`)
9. **Development seed credentials are present in tracked configuration and displayed by the login view.** This supports demonstrations but is unsuitable for production credential handling. (`src/PRN222.MVC/appsettings.Development.json:3-24`; `src/PRN222.MVC/Views/Account/Login.cshtml:8-37`)
10. **Background jobs are process-local.** The queue and test-set job manager are singleton in-memory services; the repository contains no durable job store, distributed coordination, or restart recovery. (`src/PRN222.MVC/Infrastructure/ServiceCollectionExtensions.cs:101-104`; `src/PRN222.MVC/Infrastructure/BackgroundTaskQueue.cs`; `src/PRN222.BLL/Services/TestSetGenerationJobManager.cs`)
11. **File validation does not include malware scanning or isolated parsing.** The controller checks filename extension, the service verifies PDF/OpenXML/PPT magic numbers against the submitted content type before saving, and extractor selection uses that content type. The repository contains no malware-scanning integration or isolated parser process. (`src/PRN222.MVC/Controllers/DocumentController.cs:141-155`; `src/PRN222.BLL/Services/DocumentService.cs:98-113,422-469`; `src/PRN222.BLL/Services/TextExtractors/TextExtractorFactory.cs`)
12. **External AI availability and rate limits remain runtime dependencies.** Services contain retries and error handling, but no offline model or durable retry queue is implemented. (`src/PRN222.BLL/Services/AI/GeminiLlmService.cs`; `src/PRN222.BLL/Services/AI/GeminiEmbeddingService.cs`; `src/PRN222.BLL/Services/AI/MultilingualE5EmbeddingService.cs`; `src/PRN222.BLL/Services/AI/OpenAIEmbeddingService.cs`)
13. **Authorization tests are configuration-focused rather than full HTTP integration tests.** The repository verifies role constants/attributes and service/controller logic with unit tests, but no browser-level or `WebApplicationFactory` authorization suite is present in `src/PRN222.Tests`. (`src/PRN222.Tests/MVC/AuthorizationConfigurationTests.cs`; `src/PRN222.Tests/PRN222.Tests.csproj`)

## 7. SRS Drafting Consequences

- The SRS shall describe implemented behavior in Sections 1-5 and place the items in Section 6 under constraints, known limitations, security considerations, or future scope.
- Requirements shall use the four implemented roles exactly as defined in `ApplicationRoles`; Lecturer shall not be assigned upload, raw-chunk, test-set, benchmark, or fine-tuning operations.
- Quantitative benchmark scores from the research report shall not be specified as achieved system performance until supported by reproducible run exports.
- Benchmark metrics shall be named internal approximation metrics unless the implementation is changed to integrate and verify an external RAGAS evaluator.
- Course-scoped requirements shall distinguish staff assignment scope from the current Student all-course chat behavior.
