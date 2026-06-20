# PRN222 Razor Pages Gemini

PRN222 Razor Pages Gemini is an ASP.NET Core Razor Pages web application for course document management, document-based chat, RAG retrieval, and Google Gemini integration. The project was built for the PRN222 course and follows a layered architecture so the UI, business logic, data access, and external AI integration stay separated.

## Main Features

- Role-based web application for Admin, Head Lecturer, Lecturer, and Student users.
- Razor Pages UI for account, course, department, document, chat, knowledge, evaluation, test set, and code runner workflows.
- Document upload and processing for private course materials.
- RAG-style chat flow that retrieves relevant document chunks before generating an answer.
- Google Gemini integration for content generation, embeddings, and vision-related document support.
- SQL Server persistence for identity, courses, departments, documents, chunks, embeddings, chat sessions, and benchmark runs.
- Application-owned file storage under `App_Data/uploads` for uploaded documents and extracted chunk images.
- Architecture diagrams under `docs/architecture`.

## Architecture Overview

The solution uses a 3-layer architecture with a Razor Pages presentation layer:

```text
User / Browser
  -> PRN222.Web       Razor Pages UI and PageModel request handlers
  -> PRN222.BLL       Services, workflows, DTOs, RAG, Gemini adapters
  -> PRN222.DAL       EF Core DbContext, entities, repositories, migrations
  -> SQL Server       Application data persistence

PRN222.BLL -> Google Gemini API
```

Key architectural idea:

- `PRN222.Web` handles UI, routing, validation, role authorization, and PageModel coordination.
- `PRN222.BLL` contains application workflows and business services such as document processing, chat, retrieval, benchmarking, and Gemini API adapters.
- `PRN222.DAL` owns EF Core entities, configuration, migrations, repositories, and the database context.
- Google Gemini is an external provider and is accessed through adapter services, not directly from Razor Pages.

The current high-level diagram is available at:

```text
docs/architecture/prn222-three-layer-highlevel-v3.drawio
```

## Solution Structure

```text
PRN222_Assignment1/
|-- src/
|   |-- PRN222.Web/       # Razor Pages presentation layer
|   |-- PRN222.BLL/       # Business logic, DTOs, RAG, AI adapters
|   |-- PRN222.DAL/       # EF Core data access and migrations
|   |-- PRN222.Tests/     # xUnit test project
|   `-- PRN222_Assignment1.sln
|-- docs/
|   `-- architecture/     # draw.io architecture diagrams
|-- Walkthrough.md
|-- Report_RAG_vs_Finetuning.md
`-- README.md
```

## Technology Stack

| Area | Technology |
| --- | --- |
| Web application | ASP.NET Core Razor Pages |
| Runtime | .NET 8 |
| Data access | Entity Framework Core |
| Database | SQL Server |
| AI provider | Google Gemini API |
| Testing | xUnit, Moq, FluentAssertions |
| UI | Razor Pages, Bootstrap, custom CSS, JavaScript |

## Configuration

Do not commit real API keys or private connection strings.

Set the SQL Server connection string and Gemini API key through User Secrets, environment variables, or local configuration that is not committed.

Example with .NET User Secrets:

```powershell
cd src/PRN222.Web
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\\mssqllocaldb;Database=PRN222GeminiDb;Trusted_Connection=True;MultipleActiveResultSets=true"
dotnet user-secrets set "Gemini:ApiKey" "YOUR_GEMINI_API_KEY"
```

The committed `appsettings.json` keeps secret values empty by default.

## Run The Application

Restore, build, and run from the repository root:

```powershell
dotnet restore src/PRN222_Assignment1.sln
dotnet build src/PRN222_Assignment1.sln
dotnet run --project src/PRN222.Web/PRN222.Web.csproj
```

The web app will start on the URL shown by the `dotnet run` output.

## Run Tests

```powershell
dotnet test src/PRN222_Assignment1.sln
```

## Demo Accounts

Development seed accounts are configured for local testing in `src/PRN222.Web/appsettings.Development.json`.

Use them only for local development or classroom demos. Change them before any real deployment.

## Notes For Reviewers

- Gemini is the only external AI provider represented in the current architecture diagram.
- Uploaded files and generated local logs are intentionally ignored by Git.
- The diagram uses a clean white canvas and orthogonal connectors for presentation/report export.
