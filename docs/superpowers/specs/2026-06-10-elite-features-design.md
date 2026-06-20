# Design Specification: Elite Educational Features for PRN222 Chatbot

## 1. Vision-Augmented RAG (Multimodal Indexing)

### 1.1 Objective
Enhance the existing RAG pipeline to comprehend and index visual elements (UML diagrams, architecture charts, screenshots) embedded within course PDF materials. This allows the chatbot to provide contextually rich answers that reference both text and images.

### 1.2 Workflow & Architecture
1. **Extraction (DAL/BLL):** During `ProcessDocumentAsync`, the `PdfPigExtractor` will be upgraded to detect and extract raster images (PNG/JPEG/WebP) embedded in the PDF pages.
2. **AI Captioning (BLL):** Each extracted image is sent to a new `GeminiVisionService` using the Gemini 2.0 Flash vision capabilities. The service generates a detailed, semantic Markdown description of the diagram.
3. **Chunking & Embedding (BLL):** 
   - A new chunk type `ChunkingStrategy = "ImageCaption"` is created.
   - The chunk's `Content` contains the AI-generated description and surrounding context.
   - The image file is saved to local storage (`App_Data/uploads/images`).
4. **Data Model Updates (DAL):**
   - Add an `ImageUrl` (string, nullable) property to the `DocumentChunk` entity.
   - Run EF Core Migration: `AddImageUrlToDocumentChunk`.
5. **Presentation (MVC):** 
   - The Chat UI will parse citations. If a cited chunk has an `ImageUrl`, a thumbnail or modal image viewer is rendered alongside the text to visually guide the student.

### 1.3 Trade-offs
- *Pros:* Drastically improves educational quality for visual subjects like software architecture.
- *Cons:* Slightly increases processing time during document upload. Increases API token usage for the Vision model.

---

## 2. Interactive Code Sandbox (Live .NET Execution)

### 2.1 Objective
Transform passive code snippets into interactive learning tools. Students can execute C# code provided by the chatbot directly within the chat interface, modifying parameters to experiment with concepts.

### 2.2 Workflow & Architecture
1. **UI Integration (MVC):** 
   - Modify the JavaScript markdown parser in the Chat view to detect ` ```csharp ` code blocks.
   - Inject a "▶ Run Code" button overlaid on the top-right of these blocks.
2. **Execution Engine (BLL):**
   - Implement a new `ICodeExecutionService`.
   - Use `Microsoft.CodeAnalysis.CSharp.Scripting` (Roslyn Scripting APIs) to compile and evaluate the snippet in memory.
   - **Security Sandbox:** The script options will restrict access to the file system and dangerous namespaces. A `CancellationToken` will enforce a strict 3-second timeout to prevent infinite loops (e.g., `while(true)`).
3. **API Endpoint (MVC):**
   - Create a `CodeRunnerController` with a `[HttpPost]` endpoint that accepts the raw C# string and returns the Console output or compilation errors.
4. **Presentation (MVC):**
   - Display a lightweight terminal window below the code block to show `Console.WriteLine` outputs or stack traces.

### 2.3 Trade-offs
- *Pros:* Active learning via hands-on experimentation. Reduces friction by eliminating the need to copy-paste into an external IDE.
- *Cons:* Running arbitrary code on the server (even with Roslyn) carries minor security risks. Requires careful restriction of loaded assemblies and execution timeouts.
