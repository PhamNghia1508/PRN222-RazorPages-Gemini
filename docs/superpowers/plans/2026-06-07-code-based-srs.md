# Code-Based SRS Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce a professional English Software Requirements Specification in DOCX format that accurately describes the implemented PRN222 RAG system and is suitable for academic submission.

**Architecture:** Build the document from auditable intermediate artifacts instead of writing directly into Word. Repository evidence is first summarized into a source map, transformed into a requirement catalog and narrative SRS, then assembled by a focused DOCX builder with generated diagrams and deterministic validation.

**Tech Stack:** ASP.NET Core MVC source inspection, Markdown, JSON, Python 3, `python-docx`, OOXML helpers, Mermaid or Graphviz-compatible diagrams, LibreOffice document rendering when available.

---

## File Structure

- Create: `docs/srs/source-evidence.md`
  - Repository-backed facts, file references, roles, workflows, entities, interfaces, and implementation limitations.
- Create: `docs/srs/requirements.json`
  - Machine-checkable catalog of functional and non-functional requirements.
- Create: `docs/srs/software-requirements-specification.md`
  - Complete English SRS content before Word formatting.
- Create: `docs/srs/diagrams/system-context.mmd`
  - System boundary and external actors/services.
- Create: `docs/srs/diagrams/rag-processing-flow.mmd`
  - Document ingestion and RAG answer flow.
- Create: `docs/srs/diagrams/domain-model.mmd`
  - Simplified entity relationship model.
- Create: `tools/srs/build_srs.py`
  - Converts the approved Markdown, JSON catalog, and rendered diagrams into the final DOCX.
- Create: `tools/srs/validate_srs.py`
  - Checks requirement IDs, mandatory fields, traceability, placeholders, and unsupported status values.
- Create: `artifacts/PRN222_Software_Requirements_Specification.docx`
  - Final submission document.
- Create: `artifacts/srs-render/`
  - Internal page images and optional PDF used only for visual QA.

## Task 1: Establish the Repository Evidence Map

**Files:**
- Create: `docs/srs/source-evidence.md`
- Inspect: `src/PRN222.MVC/Controllers/*.cs`
- Inspect: `src/PRN222.MVC/Infrastructure/*.cs`
- Inspect: `src/PRN222.BLL/Services/**/*.cs`
- Inspect: `src/PRN222.DAL/Entities/*.cs`
- Inspect: `src/PRN222.DAL/Data/Configurations/*.cs`
- Inspect: `src/PRN222.Tests/**/*.cs`

- [ ] **Step 1: Record the verified application identity and technology baseline**

Document the project target framework, MVC architecture, Identity usage, EF Core
database provider, AI providers, file extractors, and test projects. Attach at
least one repository file reference to every factual statement.

- [ ] **Step 2: Record the verified role and authorization matrix**

Use `ApplicationRoles.cs`, controller-level `[Authorize]` attributes, action-level
attributes, course filters, and layout visibility rules. Explicitly distinguish:

```text
Admin: full management scope
HeadLecturer: assigned-course model operations and document upload
Lecturer: assigned-course read/chat access; no upload; no raw chunk access
Student: self-registration and chat access
```

- [ ] **Step 3: Record each implemented workflow**

Create evidence sections for authentication, staff provisioning, course
assignment, document ingestion, extraction, chunking, embedding, RAG chat,
citations, sessions, test-set generation, evaluation, benchmark runs, and
fine-tuning datasets.

- [ ] **Step 4: Record domain entities and external dependencies**

Map the core entities and their relationships. List Gemini, Hugging Face,
embedding services, SQL Server, and uploaded files only when confirmed in code
or configuration.

- [ ] **Step 5: Record limitations and discrepancies**

Capture stale README claims, unverified benchmark values, incomplete production
hardening, and behavior that is represented in UI but not enforced by backend
code. Do not silently normalize contradictions.

- [ ] **Step 6: Review the evidence map**

Run:

```powershell
rg -n "TBD|TODO|unknown|assume|probably" docs/srs/source-evidence.md
```

Expected: no unresolved uncertainty marker. Any uncertainty must instead be
written as an explicit limitation with its evidence.

- [ ] **Step 7: Commit the evidence map**

```powershell
git add docs/srs/source-evidence.md
git commit -m "docs: map source evidence for SRS"
```

## Task 2: Build the Requirement Catalog and Validator

**Files:**
- Create: `docs/srs/requirements.json`
- Create: `tools/srs/validate_srs.py`
- Reference: `docs/srs/source-evidence.md`

- [ ] **Step 1: Define the catalog schema**

Each JSON requirement must use this exact shape:

```json
{
  "id": "FR-AUTH-001",
  "title": "Authenticate registered users",
  "statement": "The system shall authenticate a registered user by email and password.",
  "type": "functional",
  "priority": "Must",
  "roles": ["Admin", "HeadLecturer", "Lecturer", "Student"],
  "source": ["src/PRN222.MVC/Controllers/AccountController.cs"],
  "verification": "Test",
  "acceptanceCriteria": [
    "Valid credentials create an authenticated session.",
    "Invalid credentials do not create an authenticated session."
  ],
  "status": "Implemented",
  "tracesTo": ["UC-AUTH-01"]
}
```

Allowed values:

```text
type: functional | non-functional
priority: Must | Should | Could
verification: Test | Inspection | Analysis | Demonstration
status: Implemented | Partially Implemented | Target | Future
```

- [ ] **Step 2: Write the validator before the full catalog**

`tools/srs/validate_srs.py` shall fail when:

- a requirement ID is duplicated;
- an ID does not match `^(FR|NFR)-[A-Z]+-\d{3}$`;
- a mandatory field is empty;
- a statement does not contain `shall`;
- a requirement has no source, verification method, acceptance criterion, or
  trace target;
- an enumerated value is invalid;
- an implemented requirement points to a repository path that does not exist.

- [ ] **Step 3: Verify the validator fails on an intentionally incomplete catalog**

Run:

```powershell
& "<bundled-python>" tools/srs/validate_srs.py docs/srs/requirements.json
```

Expected: non-zero exit and a precise validation message for the incomplete
record.

- [ ] **Step 4: Populate functional requirements**

Add atomic requirements using these families:

```text
FR-AUTH, FR-ADMIN, FR-COURSE, FR-DOC,
FR-CHAT, FR-TESTSET, FR-EVAL, FR-FT
```

Every implemented requirement must cite source files. Course-scope and
role-boundary requirements must include both the allowed and denied behavior.

- [ ] **Step 5: Populate non-functional requirements**

Add requirements using:

```text
NFR-SEC, NFR-PERF, NFR-REL, NFR-UX,
NFR-MAINT, NFR-COMPAT, NFR-AI
```

Mark measurable goals without current evidence as `Target`, not `Implemented`.
Reference WCAG 2.2 Level AA and risk-appropriate OWASP ASVS controls without
claiming certified compliance.

- [ ] **Step 6: Run catalog validation**

Run:

```powershell
& "<bundled-python>" tools/srs/validate_srs.py docs/srs/requirements.json
```

Expected:

```text
PASS: requirement catalog is valid
```

- [ ] **Step 7: Commit the catalog and validator**

```powershell
git add docs/srs/requirements.json tools/srs/validate_srs.py
git commit -m "docs: define validated SRS requirement catalog"
```

## Task 3: Draft the Complete English SRS

**Files:**
- Create: `docs/srs/software-requirements-specification.md`
- Reference: `docs/srs/source-evidence.md`
- Reference: `docs/srs/requirements.json`
- Reference: `docs/superpowers/specs/2026-06-07-code-based-srs-design.md`

- [ ] **Step 1: Write document control and introduction**

Include document identifier, version, revision date, purpose, scope, audience,
definitions, references, and document organization. Do not include student
names, lecturer names, or group identifiers unless they are found in the
current repository or supplied by the user.

- [ ] **Step 2: Write the overall description**

Describe product perspective, major capabilities, operating environment,
constraints, assumptions, dependencies, and explicit out-of-scope items.

- [ ] **Step 3: Write the system context and authorization model**

Include a role-permission table and precise course-scoping rules. State that UI
visibility is supplementary and that backend authorization is authoritative.

- [ ] **Step 4: Render functional requirements from the catalog**

For every requirement, include:

```text
ID, title, shall statement, priority, roles,
verification, acceptance criteria, implementation status
```

Group by capability without renumbering requirement IDs.

- [ ] **Step 5: Write interfaces and data requirements**

Cover browser UI, MVC endpoints, SQL Server persistence, uploaded documents,
AI/embedding providers, configuration dependencies, and core entities.

- [ ] **Step 6: Render non-functional requirements from the catalog**

Clearly separate current implementation constraints from acceptance targets.
Do not state invented uptime, latency, RAGAS, accuracy, or concurrency results.

- [ ] **Step 7: Write verification, use cases, and traceability**

Define use cases with actor, trigger, preconditions, normal flow, alternatives,
postconditions, and related requirement IDs. Build a traceability matrix:

```text
Requirement ID -> Use Case -> Repository Evidence -> Verification Method
```

- [ ] **Step 8: Write limitations and future scope**

Keep future features non-normative and clearly separate from requirements for
the current release.

- [ ] **Step 9: Run content checks**

Run:

```powershell
rg -n "TBD|TODO|<<|Sample:|Cafeteria|MD5|\\.NET 9|MySQL" docs/srs/software-requirements-specification.md
```

Expected: no matches except a deliberate security explanation that explicitly
rejects MD5, if such an explanation is retained.

- [ ] **Step 10: Commit the SRS source**

```powershell
git add docs/srs/software-requirements-specification.md
git commit -m "docs: draft code-based software requirements specification"
```

## Task 4: Create the Supporting Diagrams

**Files:**
- Create: `docs/srs/diagrams/system-context.mmd`
- Create: `docs/srs/diagrams/rag-processing-flow.mmd`
- Create: `docs/srs/diagrams/domain-model.mmd`
- Create: `artifacts/srs-diagrams/*.png`

- [ ] **Step 1: Create the system context diagram**

Show the four user roles, the ASP.NET Core application boundary, SQL Server,
uploaded course documents, Gemini/Hugging Face services, and data flows. Do not
show classes or implementation-only details.

- [ ] **Step 2: Create the RAG processing diagram**

Show:

```text
Upload -> Extract -> Chunk -> Embed -> Persist
Question -> Course-scoped retrieval -> Context -> LLM -> Answer + Citations
```

- [ ] **Step 3: Create the simplified domain model**

Include only the entities needed to explain requirements:

```text
ApplicationUser, Course, ApplicationUserCourse,
Document, DocumentChunk, ChunkEmbedding,
ChatSession, ChatMessage, ChatCitation,
QAPair, BenchmarkRun, BenchmarkResult, EmbeddingModel
```

- [ ] **Step 4: Render diagrams to PNG**

Use an available Mermaid or Graphviz renderer. Store source diagrams in Git and
rendered images under `artifacts/srs-diagrams/`.

- [ ] **Step 5: Inspect all rendered diagrams**

Confirm readable labels, no clipped nodes, no crossing text, and sufficient
resolution for a Word document.

- [ ] **Step 6: Commit diagram sources**

```powershell
git add docs/srs/diagrams
git commit -m "docs: add SRS system diagrams"
```

## Task 5: Build the Professional DOCX

**Files:**
- Create: `tools/srs/build_srs.py`
- Create: `artifacts/PRN222_Software_Requirements_Specification.docx`
- Read: `docs/srs/software-requirements-specification.md`
- Read: `docs/srs/requirements.json`
- Read: `artifacts/srs-diagrams/*.png`

- [ ] **Step 1: Load the document design preset**

Use the Documents skill's formal report guidance and resolve page geometry,
font sizes, heading hierarchy, paragraph spacing, table geometry, headers,
footers, captions, and colors into explicit numeric values.

- [ ] **Step 2: Implement the DOCX builder**

The builder shall create:

- an academic cover page;
- document metadata and revision history;
- an automatic table-of-contents field;
- numbered heading styles;
- page headers and footers;
- requirement tables with repeated header rows;
- figure captions and diagram images;
- traceability tables with controlled column widths;
- appendices and references.

Use real Word styles and numbering. Do not simulate headings or bullets with
manually formatted text.

- [ ] **Step 3: Generate the document**

Run:

```powershell
& "<bundled-python>" tools/srs/build_srs.py `
  --source docs/srs/software-requirements-specification.md `
  --requirements docs/srs/requirements.json `
  --diagram-dir artifacts/srs-diagrams `
  --output artifacts/PRN222_Software_Requirements_Specification.docx
```

Expected: exit code 0 and the final DOCX exists.

- [ ] **Step 4: Run structural checks**

Open the generated DOCX with `python-docx` and verify:

```text
Title present
All mandatory level-one sections present
All requirement IDs present exactly once in the catalog tables
All three diagrams embedded
Header and footer parts present
No empty table rows used as page layout
```

- [ ] **Step 5: Commit the builder and generated document**

```powershell
git add tools/srs/build_srs.py artifacts/PRN222_Software_Requirements_Specification.docx
git commit -m "docs: generate professional PRN222 SRS"
```

## Task 6: Verify Content and Visual Quality

**Files:**
- Verify: `artifacts/PRN222_Software_Requirements_Specification.docx`
- Create: `artifacts/srs-render/page-*.png`
- Create: `artifacts/srs-render/PRN222_Software_Requirements_Specification.pdf`

- [ ] **Step 1: Re-run requirement validation**

Run:

```powershell
& "<bundled-python>" tools/srs/validate_srs.py docs/srs/requirements.json
```

Expected: `PASS: requirement catalog is valid`.

- [ ] **Step 2: Run repository tests**

Run:

```powershell
dotnet test
```

Expected: all existing tests pass. Record the exact pass/fail count in the
completion report; do not place the transient count into the SRS unless it is
labelled with the execution date.

- [ ] **Step 3: Render the DOCX**

Run the Documents skill renderer with the bundled Python runtime:

```powershell
& "<bundled-python>" "<documents-skill>/render_docx.py" `
  artifacts/PRN222_Software_Requirements_Specification.docx `
  --output_dir artifacts/srs-render `
  --emit_pdf
```

If LibreOffice is unavailable, record that limitation and perform structural
DOCX checks instead of claiming visual verification passed.

- [ ] **Step 4: Inspect every rendered page**

Check cover-page balance, table of contents, heading hierarchy, page breaks,
table wrapping, repeated headers, captions, image resolution, headers, footers,
requirement tables, and the traceability matrix.

- [ ] **Step 5: Correct and re-render all defects**

Modify only `tools/srs/build_srs.py` or source artifacts, regenerate the DOCX,
and repeat the full render inspection. Never patch the generated DOCX manually.

- [ ] **Step 6: Run final content scans**

Run:

```powershell
rg -n "TBD|TODO|<<|Cafeteria|SWD392|MD5 hashing|MySQL naming" docs/srs
git diff --check
git status --short
```

Expected: no template residue, no whitespace errors, and only intentional
generated artifacts or changes.

- [ ] **Step 7: Commit final QA corrections**

```powershell
git add docs/srs tools/srs artifacts/PRN222_Software_Requirements_Specification.docx
git commit -m "docs: finalize and verify PRN222 SRS"
```

## Task 7: Deliver the Submission Artifact

**Files:**
- Deliver: `artifacts/PRN222_Software_Requirements_Specification.docx`

- [ ] **Step 1: Confirm the final file opens and has non-zero size**

Run:

```powershell
Get-Item artifacts/PRN222_Software_Requirements_Specification.docx |
  Select-Object FullName, Length, LastWriteTime
```

Expected: the file exists, has non-zero length, and carries the current build
time.

- [ ] **Step 2: Report evidence-based completion**

Provide the clickable DOCX path, requirement validation result, test result,
page count, and whether visual render QA was completed. Mention any remaining
limitation without weakening or concealing it.
