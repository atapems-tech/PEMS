# PEMS EDMS — Gap Matrix

Companion to `PEMS_EDMS_DEEP_REPOSITORY_AUDIT.md`. Status legend: **FIXED** = re-verified this session as
resolved; **OPEN** = re-verified this session as still a real gap; **PARTIAL** = some real implementation
exists but is incomplete; **UNVERIFIED** = carried from a prior (dated) internal audit, not independently
re-checked this session.

| ID | Capability | Current State | Evidence | Gap | Risk | Priority | Proposed Solution | Dependencies | Status |
|---|---|---|---|---|---|---|---|---|---|
| G-01 | Document numbering | Atomic `UPDATE...OUTPUT` allocator via `NumberingService` | `DocumentNumberingService.cs:45-55` | None | — | — | — | — | **FIXED** |
| G-02 | Document Freeze enforcement | `UpdateAsync` throws `DocumentFrozenException` if `IsFrozen` | `EngineeringDocumentRepository.cs:552-553,609-640` | None | — | — | — | — | **FIXED** |
| G-03 | Document list pagination | `GetPagedAsync`/`GetRegisterPagedAsync` with Skip/Take + `PagedResult<T>` | `EngineeringDocumentRepository.cs:78,413` | None | — | — | — | — | **FIXED** |
| G-04 | Engineering module test coverage | 116 test methods, 20 files | `PEMS.Tests/Engineering/*.cs` (grep count) | Coverage gaps only for genuinely-new features (see G-06/G-07) | — | — | — | — | **FIXED** (general claim) |
| G-05 | Reverse traceability: Equipment → Documents | Real `ICollection<EngineeringDocument>` on `Equipment` | `Equipment.cs:103` | Contract/Vendor reverse-nav not re-checked | Low | P3 | Verify; add if missing | — | **PARTIAL** |
| G-06 | Revision-file binding | `DocumentAttachment.DocumentId` only, no `RevisionId` | `DocumentAttachment.cs` (grep: no `RevisionId` match) | Files cannot be tied to a specific revision | Medium-High | **P1** | Additive nullable `DocumentRevisionId` FK + `IsOfficial`/`FileRole` | EF migration | **OPEN** |
| G-07 | File integrity / hash | No `FileHash`/SHA-256 field on `DocumentAttachment` | grep of `DocumentAttachment.cs` | No tamper-evidence, no duplicate detection | Medium | P2 | Reuse existing `HashingStream` (built this session for Storage Providers) on upload | G-06 migration (bundle together) | **OPEN** |
| G-08 | File download security | `AttachmentId`-only input, 403 not 404, per-parent authorization | `FilesController.cs:44-61` | None | — | — | — | — | **FIXED** (verified strength) |
| G-09 | Comment Register core model | CommentNo/Category/Severity/DueDate/ActionStatus/carry-forward via `TriggeredRevisionId` | `DocumentComment.cs` | `RaisedByName`/`ClosedByName` are free text, not Employee FK | Low | P3 | Add optional `RaisedById`/`ClosedById` FK alongside existing string fields (keep both for compatibility) | — | **PARTIAL** |
| G-10 | Client Review vs internal Review separation | Dedicated `ClientApprovalStatus` field exists, but `RecordClientResponseAsync` also overwrites shared `Document.Status` | `DocumentWorkflowService.cs:349-360` | Not two fully independent state machines | Low-Medium | P2 | Stop writing client decisions into `Document.Status`; drive all client-facing UI off `ClientApprovalStatus` only | Regression testing of anything reading `Document.Status` expecting client outcomes | **OPEN** |
| G-11 | MDR completeness/gap reporting | Import rows are paired with real `EngineeringDocument` records (`pendingRowDocPairs`) | `MDRImportService.cs:392` | Whether a dedicated completeness dashboard (required/missing/overdue) is surfaced in UI is unconfirmed | Unknown | Investigation task | Read MDR-facing controller/view fully before Phase 3 | — | **UNVERIFIED** |
| G-12 | Vendor Document Control (VDR) lifecycle | Real SLA due-date calc, overdue flags, resubmission logic, attachable comment/reply sheets | `VDRSubmission.cs` | None found this session | — | — | — | — | **FIXED** (verified strength) |
| G-13 | Handover of Final Documentation (equipment-level) | Real Part A/B/C tracking, overdue calc | `ManufacturerDataBook.cs` | Scoped to vendor MDBs only, no project-level Handover Package | Medium | P2 | Build general `HandoverPackage` that references `ManufacturerDataBook` as one deliverable type, not a rebuild | — | **PARTIAL** |
| G-14 | Records Retention / Legal Hold / Disposition | No entity found anywhere | Repo-wide grep, zero true hits | Fully absent | Low today, rises with any future archival work | P3 | Build `DocumentRetentionPolicy`/`DocumentLegalHold` before any deletion/archival automation is ever added | Should precede any future auto-archival feature | **OPEN** |
| G-15 | AI non-authoritative gating | Attachments sit in an `IntakeStage` queue before becoming an authoritative `Document` row | `DocumentAttachment.cs` (`IntakeStage`, `RoutedModule`) | Post-routing AI mutation of `Status`/`IsFrozen` not checked | Unknown | Investigation task | Grep for any AI service writing `Document.Status`/`IsFrozen` directly | — | **PARTIAL** (verified strength, one open question) |
| G-16 | Server-side ABAC authorization | Data-driven permission checks (e.g. `"Document","RecordClientResponse"`), replacing older hardcoded role arrays | `DocumentWorkflowService.cs:326`; `.claude/Role/PEMS_RBAC_ABAC_PHASE1/2` reports | Full SoD enumeration (preparer≠approver) not re-confirmed this session | Low | P2 | Enumerate all document permissions; confirm SoD rule exists for self-approval | — | **PARTIAL** |
| G-17 | Discipline-level / vendor-performance dashboards | Not re-checked this session | Carried from prior Three-Pass audit | Possibly still absent | Unknown | Investigation task | Re-audit before Phase 3 dashboard work | — | **UNVERIFIED** |
| G-18 | MDR register UI columns (Vendor/Comment Status/Planned-Actual/Issue Purpose) | Not re-checked this session | Carried from prior Three-Pass audit | Possibly still incomplete | Unknown | Investigation task | Re-audit before Phase 3 | — | **UNVERIFIED** |
| G-19 | First-class nav items (My Discipline/Pending Review/IDC/Client Review) | Not re-checked this session | Carried from prior Three-Pass audit | Possibly still buried in Details page | Unknown | Investigation task | Re-audit during Phase 2 UI work | — | **UNVERIFIED** |
| G-20 | `Details.cshtml` localization consistency / `aria-label` coverage | Not re-checked this session | Carried from prior Three-Pass audit | Possibly still inconsistent | Low | P3 | Re-audit during any Phase 2/3 UI touch | — | **UNVERIFIED** |

## Priority summary

- **P1 (address first):** G-06 (revision-file binding) — the one item that directly contradicts a
  Definition-of-Done requirement in the governing directive ("Files are correctly bound to revisions").
- **P2:** G-07 (file hash, bundle with G-06's migration), G-10 (client review status separation), G-13
  (general Handover Package), G-16 (SoD enumeration).
- **P3 / investigation-only:** G-05, G-09, G-11, G-14, G-15, G-17 through G-20.
