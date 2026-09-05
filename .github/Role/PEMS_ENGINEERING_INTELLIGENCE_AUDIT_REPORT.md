# PEMS Engineering Intelligence — Master Audit Report

**Status:** Phase 0 (Repository Audit) — per directive Section 49, no implementation code has been written for this initiative. This report is the deliverable that gates approval to begin Phase 1.

**Scope of audit:** 4 parallel read-only research passes over the live repository (AI/document pipeline, entity/tag/revision model, search/AI assistant, MDR/VDR/CMMS integration + migration history), cross-checked against direct file reads and one orientation `graphify query`.

---

## 1. Current Architecture

PEMS is a Clean-Architecture ASP.NET Core MVC (.NET 10) app: `PEMS.Domain` (103 entity files) → `PEMS.Application` (DTOs/interfaces) → `PEMS.Persistence` (EF Core, 67 total controllers' worth of services under `Servises/`, SQL Server) → `PEMS.AI` (Ollama, Tesseract OCR, text extractors) → `PEMS.Web` (58 root controllers + 9 Area controllers). A separate `PEMS.WorkflowPlatform` handles generic workflow instances/history, used for MOC and document approval flows.

## 2. Current Engineering Document Architecture

Documents use a shared base class **`Document`** (`PEMS.Domain/Entities/Document.cs`) carrying `DocumentNo`, `Title`, `ProjectId`, `DisciplineId`, `Area`, `RevisionNo` (string), `Status`, `AIWorkflowStage`, `CommentStatus`, `IsFrozen`, plus `Comments`/`TagLinks`. Two concrete specializations inherit it: **`EngineeringDocument`** (MDR-side: Equipment/Contract/Vendor/MR links, Prepared/Checked/Approved chain, `ClientApprovalStatus`) and **`VDRItem`** (Vendor-doc workflow: `VendorId`, `PoId`, `MRId`, `EquipmentId`, `WorkflowStatus`, `Submissions`). This shared-base design is already close to the directive's "Engineering Register Layer" concept — it is a genuine strength, not a gap.

## 3. Existing Entities (relevant to this initiative)

- **Document core**: `Document`, `EngineeringDocument`, `VDRItem`, `DocumentRevision`, `DocumentAttachment`, `DocumentComment` (has `PageNumber`/`PinX`/`PinY` — human PDF-pin comments only), `DocumentTagLink`.
- **AI staging**: `AIDocumentClassification`, `AIExtractedTag`, `AIExtractedSpecification`, `AIConfidenceSettings`.
- **Tag-shaped entities (fragmented, see §12)**: `EquipmentTag`, `AIExtractedTag`, `DocumentTagLink`, unrelated `Tag`/`LetterTag` (generic Letter labels).
- **Relationship**: `EngineeringRelationship` (generic `SourceType/SourceId/TargetType/TargetId/RelationshipType/Confidence/IsAIProposed/ReviewStatus` — a usable building block, currently only feeding AI-proposed links).
- **MDR/VDR/Transmittal**: `MDRImportBatch`, `MDRImportRow`, `VDRImportBatch`, `VDRImportRow`, `VDRSubmission`, `Transmittal`, `TransmittalItem` (links to base `Document`, so covers both MDR and VDR docs).
- **Audit**: `AuditLog` (generic, `ChangedFieldsSummary` string, `IsAIAssisted` bool).
- **CMMS**: `Equipment`, `Instrument` (now inherits shared fields from `Equipment`, this session's earlier fix), `CalibrationHistory`, `MOC`.

## 4. Existing Services

- `IntakeService` — upload → classify (via `IDocumentAIService`) → route pipeline (`PEMS.Persistence/Servises/IntakeService.cs`).
- `DocumentAIIngestionService` — a **second, separate** ingestion path used for re-analysis once a `DocumentId` already exists (e.g. OCR "Retry"). Not unified with `IntakeService`.
- `EngineeringDocumentIntelligenceService` — pure review/workflow orchestration (approve classification, approve tag link, advance stage); calls no AI itself.
- `RelationshipService` — hand-built FK-chain traversal (Equipment→Document/VDR→Vendor→PO→Contract, etc.) plus `EngineeringRelationship` merge; the **sole point** where AI-proposed CMMS↔Document links become real FKs, always human-confirmed (`ConfirmSuggestedLinkAsync`, explicitly commented as the only such point).
- `MDRImportService` (~1000 lines) — stage → validate → commit Excel import, creates `EngineeringDocument` rows directly (no standalone MDR entity).
- `ChatAssistantService` — RAG-lite AI assistant (see §5/§6).
- `GlobalSearchService` + 19 `ISearchableEntityProvider` implementations (see §6).

## 5. Existing AI Pipeline

Flow: `ITextExtractor` (PDF/Word/Excel/Image, PdfPig + Tesseract OCR fallback) → `EngineeringExtractionPromptBuilder` (text truncated to 6000 chars) → `OllamaService.GenerateAsync` (JSON mode, temp 0.1) → `AIJsonExtractor.TryParse<ExtractedEngineeringData>` (strips markdown fences, brace-matches, deserializes) → `AIDocumentClassification` + `AIExtractedTag`/`AIExtractedSpecification` rows (Draft).

**Confidence is synthetic, not model-derived and not per-field**: `DocumentAIService` computes one heuristic score per document (base 0.4, +0.2 tags found, +0.2 specs found, +0.1 if no OCR used, capped at 0.9) and copies that identical number onto every tag/spec row for that document. The LLM is never asked to return per-item confidence.

**No provenance**: no page number, region, or bounding box is retained anywhere for an AI-extracted value. Tesseract output is flattened to plain text; the PDF-OCR loop discards `page.Number` after concatenation. `DocumentComment.PageNumber/PinX/PinY` exists but is for manual human pin-comments, unrelated to AI extraction.

**Two unreconciled workflow-stage strings**: `Document.AIWorkflowStage` (`Uploaded→AIProcessing→Classified→DisciplineAssigned→UnderReview→CommentOpen→Revision→Approved→KnowledgeBase`, enforced only as a whitelist membership check, not an ordered FSM) and `DocumentAttachment.IntakeStage` (`Uploaded→Classifying→PendingEngineeringReview→PendingManualClassification→Routed→Rejected`) — these are separate state machines on separate entities that are not unified.

Auto-routing only fires for High confidence + routing-rule allow + target MDR/VDR + extracted doc number; everything else (including all MR/MRQ/Contract/CMMS targets) requires a human click, and non-MDR/VDR targets don't even create a record (`entityId=0`, tagged for manual completion only).

## 6. Existing Search

Two **uncoordinated** entry points: `GlobalSearchController` (direct-DbContext, 6 hardcoded LIKE queries, its own comment says "not Semantic") and `SearchApiController` → `IGlobalSearchService` → 19 `ISearchableEntityProvider` implementations (proper provider pattern). Of those 19, 16 are plain `.Contains()` LIKE; 3 (`Equipment`, `EngineeringDocument`, `VDRItem`) use SQL Server Full-Text Search with LIKE fallback; 2 (`EngineeringDocument`, `Letter`) additionally merge in embedding-based semantic hits from `KnowledgeBaseService` (cosine similarity computed **in-memory in C#** over a JSON column — no SQL Server vector type, no external vector DB). "Fuzzy fallback" in `EngineeringDocumentSearchProvider` is not real fuzzy matching — it just returns the latest 500 rows when LIKE returns fewer than 5 hits.

`ChatAssistantService` (AI Assistant): entity-detection via `RelationshipService.ExploreByFreeTextAsync` → focused structured context, else `KnowledgeBaseService` embedding search, else a general aggregate snapshot → fed into an Ollama prompt instructed to answer "using ONLY the data below." Returns a **plain string** — no structured citation objects reach the UI, only inline text like `[EntityType #Id, relevance X%]` inside the prompt data. The anti-hallucination guard is prompt-text only, not code-enforced.

**No provider abstraction exists.** Grep for `IAIProvider`/`IEmbeddingProvider` returns zero code hits — only mentions inside the directive markdown itself. `IOllamaService` is the sole abstraction and is Ollama-shaped by name (`GenerateAsync`/`GenerateEmbeddingAsync` hitting `api/generate`/`api/embeddings`); swapping to OpenAI/Azure would require a new adapter or breaking the interface.

## 7. Existing MDR/VDR Integration

`VDRItem : Document` carries an explicit `EquipmentId` FK "for Traceability/Digital Equipment Passport" (already wired, rarely populated automatically — see §8). `Transmittal`/`TransmittalItem.DocumentId` points at the shared `Document` base, so one Transmittal model already serves both MDR and VDR documents — no duplication here. MDR has no standalone entity; `MDRImportService` commits staged rows directly into `EngineeringDocument`.

## 8. Existing CMMS Integration

Linking an AI-extracted tag or a VDR item to a real `Equipment`/`Instrument` row is **always human-confirmed, never automatic** — confirmed by explicit code comment in `RelationshipService.ConfirmSuggestedLinkAsync`: *"the only place a real FK is established — always via explicit user confirmation, never automatically during import/explore."* It guards against silently overwriting an existing link unless `force=true`. `Equipment.cs` corroborates: earlier auto-guessed MR/Vendor/VDR links were removed; import now stores only raw text (`MRNumberRaw`/`PONumberRaw`) pending Relationship Explorer confirmation. This is a sound, already-correct pattern — the directive's "human-in-the-loop" requirement is already satisfied here and should be **reused**, not rebuilt.

## 9. Existing Revision Handling

`DocumentRevision` has `PreviousRevisionNo` (populated by a plain lookup in `MDRImportService.cs:502`) and an unused `AIRevisionSummary` free-text field intended for an AI-generated change summary — **no code populates it**. No structural diff/comparison logic exists anywhere in `PEMS.Persistence/Servises/` (confirmed via targeted grep for "Diff"/"CompareRevision"). Revision-to-revision impact analysis does not exist.

## 10. Existing Traceability

`RelationshipService` provides real but narrow traceability: hand-coded FK-chain traversal (Equipment→Document/VDR→Vendor→PO→Contract; Equipment→MR/WorkOrder/NCR/MOC/TechnicalQuery; Equipment→P&ID via text matching), returning a DTO with one hard-coded property list per relationship type (`RelationshipExplorationDto`) rather than a generic edge list — adding a new relationship type today means adding a new property and new service code, not just a new row. `GapReport` surfaces equipment missing documents/VDR/MR/work orders. The generic `EngineeringRelationship` entity (typed source/target/relationship-type/confidence) already exists as an escape hatch for AI-proposed, non-FK-derivable links.

## 11. Existing Migration State

4 real migrations in `PEMS.Persistence/Migrations/`: `InitialCreate` (116 ops, 5,467 lines — a single squashed schema snapshot, not incremental history), `Phase4_Reliability` (0 table/column ops, index/FK-only), and this session's two `Instrument`-passport migrations (32 ops, 4 ops). **All four were introduced in one git commit** ("claude start", 2026-08-13) — there is no real incremental migration history in source control; the schema was generated/squashed at once. Going forward, new work for this initiative should establish genuine incremental migration discipline (one migration per logical schema change, never re-squashed).

## 12. Duplicate Components

- **Tag concept is split across two unrelated hierarchies**: `EquipmentTag`/`AIExtractedTag`/`DocumentTagLink` (engineering equipment tags) vs. generic `Tag`/`LetterTag` (free-form Bootstrap-badge labels on Letters). Same class name (`Tag`), completely different domains — a real naming collision risk for anyone extending "Tag."
- **Two parallel AI ingestion paths**: `IntakeService` (new-document upload→classify→route) and `DocumentAIIngestionService` (re-analysis of an existing document, e.g. OCR retry). They share concepts (classification, confidence, extracted tags/specs) but are separately implemented and not unified.
- **Two unrelated workflow-stage strings** on two different entities (`Document.AIWorkflowStage` vs `DocumentAttachment.IntakeStage`) tracking overlapping but not identical lifecycles.
- **Two uncoordinated search entry points** (`GlobalSearchController` page vs `SearchApiController` widget), each with its own hardcoded provider logic.

## 13. Architectural Weaknesses

1. Confidence scores are document-level heuristics, not model-derived per-field signal — anything built on top (reconciliation, conflict detection) inherits low-quality input until the extraction step itself is upgraded.
2. Zero provenance (page/region/bbox) for any AI-extracted value — cannot build a "click to see source" viewer without re-processing already-ingested documents.
3. `RelationshipService`'s hard-coded-DTO-per-type design does not scale to a general relationship graph without a larger refactor.
4. No revision diff engine — `AIRevisionSummary` is a dead field.
5. AI provider is hardcoded to Ollama with no swap-out interface.
6. Audit trail stores a single "old→new" string per change, not structured per-field values, and only a boolean `IsAIAssisted` flag rather than typed AI-event detail.
7. Search is fragmented across 2 controllers and 19 ad hoc providers with inconsistent matching strategy (LIKE vs FTS vs embedding) and no single ranked/merged result set.

## 14. Missing Components (confirmed absent, not just weak)

Grep across `PEMS.Domain`/`PEMS.Application`/`PEMS.Persistence` for `Conflict`, `Reconcil`, `Orphan`, `GoldenRecord` returns **zero domain hits** (one unrelated TempData variable name in `RelationshipController`). None of the following exist in any form: Conflict Detection, Cross-Document Reconciliation, Golden Engineering Record, Orphan Detection (beyond the unrelated `GapReport`), Revision Diff/Impact Engine, `IAIProvider`/`IEmbeddingProvider` abstraction, per-attribute source provenance, a unified Tag entity, a real relationship-graph engine, a unified status machine, a structured citation-bearing AI Assistant response.

## 15. Tagsight Benchmark Comparison

| Tagsight-inspired capability | PEMS today |
|---|---|
| First-class Tag w/ Discipline/System/Loop/Confidence/Original-vs-Normalized | Fragmented across 4 classes, none complete |
| Document/Page/Region/BBox provenance | Absent |
| Per-attribute confidence | Column exists, value is synthetic/document-level |
| Human-in-the-loop review | Present and correctly enforced (reuse as-is) |
| Cross-document reconciliation | Absent |
| Golden Engineering Record | Absent |
| Attribute-level conflict detection | Absent |
| Orphan detection | Absent (only unrelated equipment-gap report) |
| Typed relationship graph | Partial — generic entity exists, traversal is hardcoded per type |
| Revision diff/impact engine | Absent (dead placeholder field only) |
| Engineering Register Layer (shared model) | **Already implemented** via `Document` TPH base — a genuine strength |
| Semantic + exact + FTS + metadata search combined | Partially present, fragmented, inconsistent per entity type |
| AI Assistant with citations + anti-hallucination | RAG-lite present, no structured citations, prompt-only guard |
| Swappable AI/embedding provider | Absent — Ollama hardcoded |
| Standardized status machine | Two unreconciled whitelist-based stage strings |
| Comprehensive audit trail | Generic but coarse-grained |
| Review Workspace UI / Source Viewer | Per-feature review screens exist; no unified workspace; no source/region viewer (blocked by missing provenance) |

## 16. Target Architecture (proposed, pending approval)

Extend the existing `Document`/`EngineeringDocument`/`VDRItem` register layer rather than replacing it. Add: a unified `EngineeringTag` entity (consolidating `EquipmentTag`+`AIExtractedTag`'s useful fields, keeping `DocumentTagLink` as the join, leaving the unrelated Letter `Tag` alone); an `ExtractionProvenance` entity (Document/Attachment → Page → BoundingBox → extracted field name/value); a real per-field confidence pipeline (prompt the LLM for confidence per extracted item, stop synthesizing one document-level number); a `ReconciliationService` + `ConflictRecord` + `GoldenRecordSnapshot` layer built on top of the existing `EngineeringRelationship`/`DocumentTagLink` confirmation pattern (reusing the proven human-confirmation gate from `RelationshipService`); a `RevisionDiffService` populating the already-existing but dead `AIRevisionSummary` field plus a structured diff table; an `IAIProvider`/`IEmbeddingProvider` interface pair with `OllamaProvider` as the first implementation (wrapping, not replacing, `IOllamaService`); a single unified document-lifecycle status machine replacing the two unreconciled stage strings; a merged search layer wrapping the existing 19 providers behind one ranked/combined result contract; a structured, citation-bearing `AskAsync` response DTO for the AI Assistant.

## 17. Target Domain Model

New/changed entities only — see §16 for the "Reuse/Refactor/New" classification per component, expanded in the Implementation Phases table (§19). No entity in this list replaces or removes `Equipment`, `WorkOrder`, `MaintenancePlan`, `Instrument`, `CalibrationHistory`, `VDRItem`, `EngineeringDocument`, `Transmittal`, or `MOC` — all are additive.

## 18. Target Data Flow

Ingest (unify `IntakeService`+`DocumentAIIngestionService`) → Extract with per-field confidence + page/bbox provenance → Stage (existing Draft/Approved/Rejected pattern, reused) → Human confirm (existing `RelationshipService` gate, reused) → Reconcile against existing tags/equipment (new) → Update Golden Record (new) → Index for search (existing `KnowledgeBaseService` embeddings + FTS, extended) → Serve via unified search + citation-bearing AI Assistant (refactored).

## 19. Migration Strategy

Because the current schema is a single squashed `InitialCreate` with no real incremental git history, and the project is explicitly in a testing phase where breaking changes are acceptable per prior user direction, new schema work should nonetheless establish genuine incremental discipline going forward: one focused, reviewable migration per phase, additive-first (new tables/nullable columns), never touching existing `EngineeringDocument`/`VDRItem`/`Equipment`/`Instrument`/`WorkOrder`/`MOC` columns destructively. Provenance/confidence upgrades apply only to newly-ingested documents — no retroactive backfill is possible without re-OCR, which is out of scope unless explicitly requested.

## 20. Implementation Phases (Reuse / Refactor / New, per directive §47)

1. Unified Tag entity — **Refactor** (consolidate `EquipmentTag`+`AIExtractedTag`, keep `DocumentTagLink`)
2. Per-field confidence in extraction pipeline — **Refactor** (`DocumentAIService`, `EngineeringExtractionPromptBuilder`, `AIJsonExtractor`)
3. Extraction provenance (page/region/bbox) — **New**
4. Unify `IntakeService`/`DocumentAIIngestionService` — **Refactor**
5. Unify `AIWorkflowStage`/`IntakeStage` into one status machine — **Refactor**
6. Reconciliation / Conflict Detection / Golden Record — **New**, built on existing human-confirmation gate (**Reuse** `RelationshipService.ConfirmSuggestedLinkAsync` pattern)
7. Revision Diff/Impact Engine — **New**, populates existing dead `AIRevisionSummary` field (**Reuse** field, **New** logic)
8. Relationship graph generalization — **Refactor** `RelationshipService`, **Reuse** `EngineeringRelationship` entity
9. `IAIProvider`/`IEmbeddingProvider` abstraction — **New**, wraps existing `IOllamaService` (**Reuse**)
10. Unified search layer — **Refactor** (wrap existing 19 providers + `GlobalSearchService`, **Reuse** all provider logic)
11. AI Assistant structured citations — **Refactor** `ChatAssistantService`
12. Review Workspace UI + Source Viewer — **New** (blocked on Phase 3 provenance data)

Each phase ships with: Build, migration (if any), unit tests, integration tests, regression check against existing MDR/VDR/CMMS/Intake flows, manual verification.

## 21. Risk Register

- Two parallel ingestion paths must be unified carefully — regression risk to both intake and OCR-retry flows.
- The two Tag hierarchies share a class name; renaming/consolidating risks breaking the unrelated Letter-tagging feature if done carelessly.
- Confidence values are currently synthetic — reconciliation/conflict logic built before Phase 2 lands will be low-quality; sequencing matters.
- No provenance exists for already-ingested documents — cannot retrofit without re-OCR.
- `RelationshipService` is deeply hardcoded per entity type; generalizing it touches procurement/CMMS traceability screens already in production use.
- AI Assistant's hallucination guard is prompt-only; structured citations reduce but don't eliminate fabrication risk.
- Migration history was squashed once already; must not repeat that for this initiative's changes.

## 22. Test Strategy

Build must pass after every phase. Unit tests for new services (reconciliation, diff, provider abstraction). Integration tests against a real (not mocked) SQL Server instance for anything touching `SaveChanges`/migrations. Regression pass over existing MDR import, VDR workflow, Relationship Explorer confirmation, CMMS Instrument/Calibration flows (all built this session) and Intake/OCR review screens. Manual verification limited to `dotnet build` + anonymous-route smoke tests via curl, since no working dev-DB login credentials currently exist (noted limitation from earlier phases of this engagement).

## 23. Definition of Done (per phase)

Build succeeds; migration (if any) applies cleanly with `database update`; existing MDR/VDR/Correspondence/CMMS/Intake flows spot-checked unaffected; new capability has at minimum a working controller action + view (no dead-end backend-only features); this report's relevant section updated to reflect what shipped; `graphify update .` run.

---

**Next step per directive §49**: this report is presented for approval of the target architecture (§16–§20) before any implementation code is written. No code has been changed as part of this audit.
