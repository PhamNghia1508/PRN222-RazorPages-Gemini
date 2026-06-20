# Code-Based Software Requirements Specification Design

## 1. Purpose

Create a professional English Software Requirements Specification (SRS) for the
PRN222 course project. The SRS will describe the system represented by the
current repository and will be suitable for academic submission.

The document will be an independent SRS rather than a complete course project
report. The previous-student `Course Project Report Template.docx` is only a
format and content reference. Its analysis, design, implementation, and
alternative-architecture chapters will not be copied into the SRS.

## 2. Source of Truth

The implemented repository is the primary source of truth. Requirements will be
derived from:

- MVC controllers and authorization attributes;
- application services and service interfaces;
- entities, Entity Framework configurations, and migrations;
- Razor views and user workflows;
- application configuration and dependency registration;
- automated tests;
- existing project documentation, only where it agrees with the code.

When documentation and code disagree, the code takes precedence. The SRS will
not claim that an experiment, quality threshold, or feature is complete unless
there is supporting repository evidence. Planned or incomplete capabilities
will be placed in a clearly labelled Future Scope section.

## 3. Standards and References

The document structure and requirement-writing approach will be aligned with:

- ISO/IEC/IEEE 29148:2018 for requirements engineering and requirements
  information items;
- ISO/IEC 25010:2023 for product quality characteristics;
- WCAG 2.2 Level AA as the accessibility target for the web interface;
- OWASP ASVS 5.0, using risk-appropriate requirements for authentication,
  authorization, input handling, data protection, and auditability.

IEEE 830-1998 may be used as a historical teaching reference for requirement
quality characteristics, but it will not be presented as the current standard.

## 4. Intended Audience

The primary audience is the course professor or evaluator. Secondary audiences
are project team members, developers, testers, and future maintainers.

The document must remain understandable to an academic reviewer without
requiring them to read the source code.

## 5. Document Scope

### 5.1 Included

- System purpose, boundaries, assumptions, dependencies, and constraints.
- Product perspective and supported operating environment.
- User classes and role-based authorization.
- Authentication, registration, login, logout, and access-denied behavior.
- Administrative account and course management.
- Course assignment and course-scoped data access.
- Document upload, extraction, chunking, embedding, status, and retrieval.
- RAG chat, chat sessions, citations, and retrieved evidence.
- Test-set generation and generated question-answer pairs.
- Benchmark and evaluation workflows.
- Fine-tuning dataset preparation and supported model comparison workflows.
- External interfaces, data requirements, and relevant domain entities.
- Verifiable functional and non-functional requirements.
- Use cases and requirements traceability.
- Known limitations and future scope.

### 5.2 Excluded

- Detailed class-by-class design documentation.
- Complete source-code explanation.
- Full database data dictionary for every framework-managed Identity table.
- Unimplemented production infrastructure such as enterprise SSO, distributed
  deployment, billing, or multi-tenant institution management.
- Unsupported benchmark results or invented performance measurements.
- A claim of legal or institutional compliance that has not been assessed.

## 6. Role Model

The SRS will document the current four-role model:

| Role | Intended system scope |
|---|---|
| Admin | Manages courses and staff accounts, assigns lecturers and head lecturers to courses, and has full management visibility. |
| Head Lecturer | Operates model and document workflows only for assigned courses, including course-scoped upload and processing. |
| Lecturer | Has read-only access to assigned course resources and may use course-scoped chat, but cannot upload documents or view raw chunks. |
| Student | Registers through the public registration flow and uses RAG chat within the student-accessible scope. |

Authorization requirements will distinguish UI visibility from server-side
enforcement. Hiding a navigation item will never be treated as sufficient
authorization.

## 7. Proposed SRS Structure

1. Document Control
   - Title page
   - Revision history
   - Approval information
   - Table of contents
2. Introduction
   - Purpose
   - Scope
   - Intended audience
   - Definitions, acronyms, and abbreviations
   - References
   - Document overview
3. Overall Description
   - Product perspective
   - Product functions
   - User classes
   - Operating environment
   - Constraints
   - Assumptions and dependencies
4. System Context and Role Model
   - Context diagram
   - Role-permission matrix
   - Course-scoping rules
5. Functional Requirements
   - Authentication and account lifecycle
   - Administration and staff provisioning
   - Course management and assignment
   - Document ingestion and knowledge-base management
   - RAG retrieval and chat
   - Test-set generation
   - Evaluation and benchmarking
   - Fine-tuning dataset management
6. External Interface Requirements
   - User interfaces
   - Software and AI-provider interfaces
   - Database and file interfaces
   - Communication interfaces
7. Data Requirements
   - Core entities and relationships
   - Ownership and course isolation
   - Validation, retention, and audit considerations
8. Non-Functional Requirements
   - Functional suitability
   - Performance efficiency
   - Reliability
   - Security
   - Interaction capability and accessibility
   - Maintainability
   - Compatibility and flexibility
   - AI/RAG quality and reproducibility
9. Verification and Acceptance
   - Verification methods
   - Acceptance criteria
   - Requirements traceability matrix
10. Limitations and Future Scope
11. Appendices
   - Glossary
   - Use-case specifications
   - Supporting diagrams

## 8. Requirement Format

Each normative requirement will contain:

- a unique identifier;
- a concise `shall` statement;
- rationale or source where useful;
- priority;
- verification method;
- measurable acceptance criteria;
- related role, use case, or data entity;
- implementation status when evidence is not conclusive.

Example identifier families:

- `FR-AUTH-###`: authentication and identity;
- `FR-ADMIN-###`: administrative account management;
- `FR-COURSE-###`: course management and assignments;
- `FR-DOC-###`: document ingestion and processing;
- `FR-CHAT-###`: chat, retrieval, and citations;
- `FR-TESTSET-###`: test-set generation;
- `FR-EVAL-###`: evaluation and benchmark;
- `FR-FT-###`: fine-tuning dataset workflows;
- `NFR-SEC-###`: security;
- `NFR-PERF-###`: performance;
- `NFR-REL-###`: reliability;
- `NFR-UX-###`: usability and accessibility;
- `NFR-AI-###`: RAG quality, provenance, and reproducibility.

Requirements must be atomic, unambiguous, feasible, consistent, traceable, and
verifiable. Subjective words such as "fast", "secure", "easy", or "accurate"
will not appear without a metric or observable acceptance condition.

## 9. AI and RAG Requirements

The SRS will explicitly cover concerns that a generic CRUD SRS would miss:

- answers grounded in retrieved course documents;
- source citation and evidence display;
- behavior when relevant context is unavailable;
- isolation of retrieval by authorized course scope;
- traceability of the embedding model and retrieval configuration;
- failure handling when an LLM or embedding provider is unavailable;
- prompt and document input validation;
- protection against unauthorized corpus access;
- reproducibility of benchmark runs and generated datasets;
- clear separation between implemented behavior and proposed quality targets.

No numeric retrieval-quality or answer-quality result will be asserted without
test or benchmark evidence. Where a threshold is required for acceptance, it
will be presented as a target requirement to be verified.

## 10. Diagrams and Tables

The SRS will use diagrams only when they reduce ambiguity:

- system context diagram;
- role and course-scope authorization matrix;
- high-level RAG processing flow;
- simplified domain/entity relationship diagram;
- selected use-case diagram;
- requirements traceability matrix.

Detailed class, package, communication, and alternative-architecture diagrams
belong to design documentation and will not be added merely to increase page
count.

## 11. Document Presentation

The final deliverable will be an English `.docx` document of approximately
25-35 pages, depending on the number of verified requirements and diagrams.

Presentation requirements:

- professional academic cover page;
- restrained, consistent typography;
- numbered headings and captions;
- automatic table of contents;
- readable tables with repeated headers where needed;
- page numbers, document identifier, and revision metadata;
- consistent terminology and requirement IDs;
- no unresolved placeholders, sample content, or previous-student data.

The document will be rendered to page images and visually inspected before
delivery when the local document toolchain supports rendering.

## 12. Validation Strategy

Before delivery:

1. Map every documented feature to repository evidence.
2. Check the role matrix against controller authorization and course filters.
3. Check entities and relationships against the EF Core model and migrations.
4. Confirm requirement identifiers are unique.
5. Confirm every normative requirement has a verification method.
6. Scan for unsupported claims, contradictions, placeholders, and stale
   technology references.
7. Validate terminology against the actual .NET target, database provider, AI
   providers, and supported file types.
8. Render and visually inspect the final document for pagination, table
   overflow, clipped content, and inconsistent formatting.

## 13. Deliverable

The final deliverable will be a new SRS file. The original previous-student
template will remain unchanged. Supporting intermediate files and rendered QA
images will not be included in the final submission package unless requested.
