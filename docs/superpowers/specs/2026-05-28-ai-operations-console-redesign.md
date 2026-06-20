# AI Operations Console Redesign

## Context

The current PRN222 assignment UI works functionally, but it reads more like a landing page than a product workspace. The project is a Vietnamese RAG and evaluation application, so the interface should feel like an AI operations console: focused on document readiness, retrieval, chat quality, and evaluation flow.

This redesign keeps the existing ASP.NET Core MVC architecture and Warm Sand & Ink visual identity, but changes the information architecture and screen composition so the app presents as a professional dashboard.

## Goals

- Make the first screen a usable operations dashboard instead of a marketing hero.
- Emphasize the RAG pipeline: upload, extract, chunk, embed, ask, evaluate.
- Make document status, indexed chunks, course scope, and next actions visible.
- Turn chat into a playground that explains retrieved context and citations.
- Provide a credible Evaluation screen even before benchmark execution is fully implemented.
- Improve mobile behavior, footer contrast, and dashboard polish without overbuilding.

## Non-Goals

- Do not rewrite the backend or data model for this redesign.
- Do not introduce a frontend framework; continue using Razor, Bootstrap, and CSS.
- Do not implement full benchmark execution unless requested as a separate feature.
- Do not replace the existing document upload, processing, chat, or course services.

## Proposed Information Architecture

Replace the current top-nav-first layout with a product console shell.

Primary navigation:

- Overview
- Knowledge Base
- Chat Playground
- Evaluation
- Courses

The shell should use a left sidebar on desktop. On mobile, the navigation collapses into a compact menu or drawer so the content remains readable.

The top area of the shell should be small and operational: workspace name, current environment/status, and a primary `Upload document` action. It should not behave like a landing-page header.

## Overview Screen

The home page becomes `Vietnamese RAG Workbench`.

Main sections:

- Compact page header with one-line product description.
- Metric cards for documents, indexed chunks, courses, and evaluation status.
- RAG pipeline card showing `Upload -> Extract -> Chunk -> Embed -> Ask -> Evaluate`.
- Recent documents table showing file name, course, chunks, status, last updated, and primary action.
- Right-side action panel with context-aware next steps, such as uploading a document, processing failed files, or opening the chat playground.

This page should help a user understand whether the knowledge base is ready for chat in under five seconds.

## Knowledge Base Screen

The existing document list becomes the Knowledge Base.

Improvements:

- Add a compact filter toolbar with course, status, file type, and search.
- Keep the table dense but readable, with consistent status badges and icon-only actions with tooltips.
- Show an empty state that says the knowledge base has no indexed documents yet and points users to upload.
- Keep upload and process actions prominent.

The upload screen should remain simple but be framed as a step in the ingestion pipeline: choose course, choose file, upload, then process.

## Chat Playground

The chat page should communicate that answers are generated from retrieved course material.

Layout:

- Main chat column for messages.
- Header with course selector and current session title.
- Right panel titled `Retrieved context`.
- Context panel shows top chunks, file source, similarity/relevance score, and snippet.
- Citation chips in assistant messages should visually connect to the right panel.
- Empty state should provide sample questions based on the selected course when possible.

This keeps the chat useful for students while making the RAG behavior demonstrable for grading and presentation.

## Evaluation Screen

The benchmark area can be introduced as an evaluation dashboard even if execution is not complete.

Sections:

- Metric placeholders for Faithfulness, Answer Relevancy, Context Precision, and Context Recall.
- Empty state for no evaluation runs yet.
- Disabled or secondary `Run benchmark` action with a clear coming-soon state.
- Table structure for future benchmark runs: name, embedding model, chunking strategy, status, started at, result summary.

This makes the RAG vs Finetuning roadmap visible without pretending the feature is complete.

## Visual System

Keep the Warm Sand & Ink direction, but tune it toward a dashboard:

- Reduce card radius to about 8px for operational surfaces.
- Use serif type sparingly for page titles, not large marketing hero text.
- Prefer compact toolbars, panels, tables, and status chips.
- Avoid nested cards where a section or simple panel is enough.
- Fix footer contrast so all text is readable on the light background.
- Make mobile typography and spacing stable so headings and buttons do not crop or overflow.

## Data Usage

Use existing services where possible:

- Courses from `ICourseService`.
- Documents and chunk counts from `IDocumentService`.
- Chat sessions/messages from `IChatService`.
- Evaluation screen can use placeholder values until benchmark execution is implemented.

If a view model is needed for Overview, it should aggregate existing data and avoid new persistence.

## Error And Empty States

Required states:

- No courses yet.
- No documents uploaded.
- Documents uploaded but not processed.
- Processing in progress.
- Processing failed.
- No indexed chunks available for chat.
- No evaluation runs yet.

Each state should include one clear next action.

## Testing And Verification

Verification should include:

- `dotnet build PRN222.MVC/PRN222.MVC.csproj`
- `dotnet test PRN222_Assignment1.sln`
- Manual browser check for Overview, Knowledge Base, Upload, Chat Playground, and Evaluation.
- Desktop viewport around 1440px wide.
- Mobile viewport around 390px wide.

Visual checks:

- Sidebar/top navigation is readable.
- Hero text no longer crops on mobile because the hero is removed.
- Tables do not overflow incoherently.
- Primary actions remain visible.
- Footer contrast is fixed.

## Implementation Boundaries

The first implementation pass should focus on UI and view composition:

- Shared layout and navigation shell.
- Home/Overview redesign.
- Document list and upload polish.
- Chat layout with context panel using existing citations where available.
- Evaluation placeholder screen.
- CSS cleanup for dashboard polish and mobile responsiveness.

Backend feature work for benchmark execution should be planned separately.
