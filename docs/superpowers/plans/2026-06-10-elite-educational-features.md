# Elite Educational Features Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement Vision-Augmented RAG (image indexing) and an Interactive Code Sandbox (live C# execution) to enhance the educational value of the PRN222 chatbot.

**Architecture:** 
1. **Vision RAG:** Extend `PdfTextExtractor` to extract images, use Gemini 2.0 Flash Vision to describe them, and store descriptions as searchable chunks with an `ImageUrl`.
2. **Code Sandbox:** Implement a BLL service using `Microsoft.CodeAnalysis.CSharp.Scripting` to execute code in a restricted memory space and expose it via an MVC Controller to a "Run Code" button in the chat UI.

**Tech Stack:** .NET 8.0, iText7, Gemini 2.0 Flash Vision, Microsoft.CodeAnalysis.CSharp.Scripting, Vanilla JS.

---

### Task 1: Database Schema Update

**Files:**
- Modify: `src/PRN222.DAL/Entities/DocumentChunk.cs`
- Create: Migration using `dotnet ef migrations add AddImageUrlToDocumentChunk`

- [ ] **Step 1: Add ImageUrl property to DocumentChunk**

```csharp
// src/PRN222.DAL/Entities/DocumentChunk.cs
public string? ImageUrl { get; set; }
```

- [ ] **Step 2: Add migration**

Run: `dotnet ef migrations add AddImageUrlToDocumentChunk --project src/PRN222.DAL --startup-project src/PRN222.MVC`
Expected: Migration file created.

- [ ] **Step 3: Update database**

Run: `dotnet ef database update --project src/PRN222.DAL --startup-project src/PRN222.MVC`
Expected: SUCCESS

- [ ] **Step 4: Commit**

```bash
git add src/PRN222.DAL/Entities/DocumentChunk.cs src/PRN222.DAL/Migrations/*
git commit -m "db: add ImageUrl to DocumentChunk"
```

---

### Task 2: Implement Gemini Vision Service

**Files:**
- Create: `src/PRN222.BLL/Services/Interfaces/IGeminiVisionService.cs`
- Create: `src/PRN222.BLL/Services/AI/GeminiVisionService.cs`
- Modify: `src/PRN222.MVC/Infrastructure/ServiceCollectionExtensions.cs`

- [ ] **Step 1: Define IGeminiVisionService**

```csharp
namespace PRN222.BLL.Services.Interfaces;
public interface IGeminiVisionService {
    Task<string> DescribeImageAsync(byte[] imageBytes, string contextHint);
}
```

- [ ] **Step 2: Implement GeminiVisionService**
Use `Gemini 2.0 Flash` model. Reuse logic from `GeminiLlmService.cs` for API calls.

- [ ] **Step 3: Register service**
Modify `src/PRN222.MVC/Infrastructure/ServiceCollectionExtensions.cs` to add `services.AddScoped<IGeminiVisionService, GeminiVisionService>();`.

- [ ] **Step 4: Commit**
```bash
git add src/PRN222.BLL/Services/Interfaces/IGeminiVisionService.cs src/PRN222.BLL/Services/AI/GeminiVisionService.cs src/PRN222.MVC/Infrastructure/ServiceCollectionExtensions.cs
git commit -m "feat: add GeminiVisionService"
```

---

### Task 3: Enhance PDF Extraction to Capture Images

**Files:**
- Modify: `src/PRN222.BLL/Services/TextExtractors/PdfTextExtractor.cs`
- Create: `src/PRN222.BLL/DTOs/ExtractedContentDto.cs`

- [ ] **Step 1: Create ExtractedContentDto**
To handle both text and image metadata.

- [ ] **Step 2: Update PdfTextExtractor**
Use `iText.Kernel.Pdf.Canvas.Parser.PdfCanvasProcessor` or `IEventListener` to extract images. Save images to `App_Data/uploads/images`.

- [ ] **Step 3: Commit**
```bash
git add src/PRN222.BLL/Services/TextExtractors/PdfTextExtractor.cs src/PRN222.BLL/DTOs/ExtractedContentDto.cs
git commit -m "feat: enhance PDF extractor to capture images"
```

---

### Task 4: UI: Integrated Interactive Code Sandbox

**Files:**
- Modify: `src/PRN222.BLL/PRN222.BLL.csproj` (Add Package)
- Create: `src/PRN222.BLL/Services/RoslynCodeExecutionService.cs`
- Create: `src/PRN222.MVC/Controllers/CodeRunnerController.cs`
- Modify: `src/PRN222.MVC/Views/Chat/Session.cshtml` (JS)

- [ ] **Step 1: Add Microsoft.CodeAnalysis.CSharp.Scripting package**
Run: `dotnet add src/PRN222.BLL/PRN222.BLL.csproj package Microsoft.CodeAnalysis.CSharp.Scripting`

- [ ] **Step 2: Implement RoslynCodeExecutionService**
Include a 3-second timeout and restricted assemblies.

- [ ] **Step 3: Create CodeRunnerController**
Expose a POST endpoint `/CodeRunner/Execute`.

- [ ] **Step 4: Add JS to Chat UI**
Modify `src/PRN222.MVC/Views/Chat/Session.cshtml` to find `code.language-csharp` blocks and append a "Run" button + output terminal.

- [ ] **Step 5: Commit**
```bash
git add .
git commit -m "feat: add interactive code sandbox"
```
