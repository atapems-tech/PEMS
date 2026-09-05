# PEMS Engineering — G/M Hardening + Final Engineering Acceptance Audit

**Date:** 2026-08-18
**Scope:** Transmittal Governance (G) + Hold/Clouded Lifecycle (M) + Material Coding (F) + Final Engineering Audit

---

## 1. Executive Verdict

**CONDITIONAL PASS**

G and M are implemented, migrated, tested (86/86 tests green as of the §8b addendum, 0
regressions), and verified to the extent achievable without production credentials. F is correctly
and explicitly left SOURCE-BLOCKED rather than fabricated. Item B (real concurrency validation) was
subsequently executed live against an isolated SQL Server database and closed — see §8b. The one
remaining condition preventing an unqualified PASS: role-specific authorization could only be
verified by code inspection + the permission-matrix mechanism itself, not by a live authenticated
browser session, since this environment has no real login credentials (item A in §11).

---

## 2. G — Transmittal Governance

| Requirement | Status | Evidence |
|---|---|---|
| Audit existing architecture before building | PASS | Read `Transmittal.cs`/`TransmittalItem.cs`/`TransmittalService.cs`/`TransmittalController.cs` in full before any change; confirmed no `[Authorize]` existed anywhere, no structured recipients, no Response/DueDate concept |
| No duplicate domain concepts | PASS | Reused existing `Status` field (Draft/Sent/Acknowledged, extended with Cancelled) instead of inventing a parallel "Issued" state; reused existing `WorkingDayCalculator` (built for I/VDR-SLA) rather than a second implementation; reused existing `RolePermission`/`IPermissionService` matrix instead of a new auth system; reused existing `AuditLog` (global, automatic via `PEMSDbContext.SaveChangesAsync`) instead of a new audit table |
| Transmittal Number | PASS | `Transmittal.TransmittalNo` unique via DB-level filtered unique index (`TransmittalConfiguration`, migration `AddTransmittalNoUniqueIndex`) + app-level pre-check with friendly duplicate-key error translation in `TransmittalService.CreateAsync` |
| Response Required (real domain concept) | PASS | `Transmittal.ResponseRequired` (bool) + `ResponseDueDate` computed via `WorkingDayCalculator.AddWorkingDays` **only** at `MarkSentAsync` (the issue moment) — `ResponseRequired=true` can never coexist with a permanently-null `ResponseDueDate` after issue, by construction (see `TransmittalGovernanceTests.MarkSentAsync_ResponseRequired_ComputesDueDateViaWorkingDayCalculator`) |
| Due Date via existing WorkingDayCalculator | PASS | `TransmittalService.MarkSentAsync` calls `WorkingDayCalculator.AddWorkingDays` directly — no second working-day implementation; Friday-skip rule verified in `WorkingDayCalculatorTests` |
| Structured Recipients | PASS | New `TransmittalRecipient` entity (Name, RecipientType, Organization, per-recipient ResponseRequired, IsActive) — not a comma-separated string or JSON blob; `Transmittal.To` free-text kept only as a legacy-compatible auto-generated summary |
| Transmittal Immutability after Issue | PASS | No Edit action exists (never did, and none was added); Number/Documents/Recipients/Purpose are set once at `CreateAsync` and never mutated after `MarkSentAsync`; correction path is `CancelAsync` (preserves all history) → `ReissueAsync` (new Draft row linked via `SupersedesTransmittalId`, mirroring `DocumentRevision.SupersededRevisionId`) |
| Transmittal + Revision Traceability | PASS | `TransmittalItem.RevisionNo` is a snapshot string captured at `CreateAsync` time and never overwritten — verified explicitly in `CreateAsync_SnapshotsRevisionNoAtTimeOfCreation` (mutating `Document.RevisionNo` after send does not change the historical transmittal record) |
| G Authorization (server-side) | PASS | `TransmittalController` now checks `IPermissionService.HasPermissionAsync(..., "Transmittal", action)` before every mutating action (Create/MarkSent=Approve/Cancel=Delete/Reissue=Create/RecordResponse=Edit), returning `Forbid()` on denial — same mechanism already proven for `EngineeringDocuments.Freeze/Unfreeze`. New `RolePermission` module `"Transmittal"` seeded with sane defaults (see §5) |
| G Tests | PASS (13 new) | `TransmittalGovernanceTests.cs`: duplicate number, held-document block, revision snapshot, response-required due-date calc, response-not-required leaves null, no-recipients block, already-sent block, cancel→reissue chain integrity, reissue-on-non-cancelled block, record-response-before-issue block, record-response-when-not-required block, record-response success |
| Concurrency | PARTIAL | DB-level unique filtered index confirmed by direct migration-SQL inspection (not a live concurrent test) — see §9 for why a live SQL-Server concurrency test was deliberately not run |

---

## 3. M — Hold & Clouded

| Requirement | Status | Evidence |
|---|---|---|
| Audit existing lifecycle before building | PASS | Read `EngineeringDocumentRepository.FreezeAsync`/`UnfreezeAsync` (the closest existing precedent), `DocumentWorkflowService` (Submit/Check/Approve/Issue/Revise&Resubmit), `DocumentComment`, `DocumentRevision`, `AuditLog`, `RolePermission`/`IPermissionService`, `WorkflowPlatform` — all in full, before writing any Hold/Clouded code |
| Hold is not a cosmetic UI flag | PASS | `DocumentHoldService.PlaceHoldAsync`/`ReleaseHoldAsync` run server-side guard logic identical in shape to the proven `FreezeAsync`/`UnfreezeAsync` pattern; `DocumentWorkflowService.ApproveAsync`/`IssueAsync` check `document.IsOnHold` and throw — verified live via `HoldBlocksWorkflowTests` running the **real** workflow engine (`IWorkflowRuntime`), not a simulation |
| Hold Model — auditable | PASS | New `DocumentHold` entity: Reason, HoldDate, HoldByEmployeeId, ResponsibleDisciplineId, ReleaseDate, ReleaseByEmployeeId, ReleaseReason — append-only (never overwritten), mirroring the `VDRSubmission`/`CalibrationHistory` style already used elsewhere in this codebase. Every insert/update to it is *also* captured by the pre-existing global `AuditLog` (Who/What/When/old→new), so history exists at two levels without building a second audit system |
| Hold State Machine | PASS | `Normal → OnHold → Released` enforced by `DocumentHoldService`: cannot place Hold twice (`AlreadyOnHold` throws), cannot release when not on Hold, cannot Hold a Frozen or Issued document (terminal states), can re-enter the cycle any number of times (`MultipleHoldReleaseCycles_ProduceFullHistory` proves 3-cycle history reconstructs correctly) |
| Interaction with existing workflow states | PASS (documented) | See explicit rule table in §4 below — audited against `Draft/Submitted/IDC Review/Checked/Approved/Rejected/Revise & Resubmit/Issued/Frozen`, not invented blind |
| Hold Business Rules (what it blocks) | PASS | Blocks: `ApproveAsync`, `IssueAsync` (both throw `InvalidOperationException` with the document number in the message). Does NOT block: `CreateRevision` (deliberate — see §4), `SubmitAsync`/`CheckAsync` (a document can still move through review while a *different* open item is on hold; only the two "finalization" actions are gated) |
| Clouded / Marked-for-Revision — distinct from revision identity and visual markup | PASS | Three concepts kept explicitly separate: (1) revision identity = unchanged `Document.RevisionNo`/`DocumentRevision`, (2) visual markup/clouding = explicitly out of scope (this app has no drawing-markup renderer — documented, not silently ignored), (3) business status = new `DocumentComment.MarksRevisionRequired` (bool) |
| Clouded Traceability | PASS | `DocumentComment.TriggeredRevisionId` (FK to `DocumentRevision`) answers "was a new revision created" directly in the data model — wired into `EngineeringDocumentsController.CreateRevision` via new optional `triggeringCommentId` param, closing the loop when the user picks a specific open Marked-for-Revision comment while creating the fix revision |
| Clouded does not fabricate a new Document Number | PASS | `CreateRevision` action inspected line-by-line: it sets `document.RevisionNo` and `DocumentRevision.RevisionNo`, and **never** touches `document.DocumentNo` — structurally guaranteed, confirmed by direct code reading (not inferred) |
| Clouded + Transmittal historical truth | PASS | Same evidence as G's Traceability row — `TransmittalItem.RevisionNo` is a frozen snapshot; a document going on Hold or getting Marked-for-Revision after being transmitted cannot retroactively alter a historical transmittal record, because nothing in the codebase writes back to `TransmittalItem` after creation |
| M Tests | PASS (12 new) | `DocumentHoldTests.cs` (8): set-fields, empty-reason-throws, already-on-hold-throws, frozen-doc-throws, issued-doc-throws, release-not-on-hold-throws, release-preserves-record, multi-cycle-history. `HoldBlocksWorkflowTests.cs` (4): Approve-blocked-while-held, Approve-succeeds-after-release, Hold-blocked-on-issued-doc, CreateRevision-not-blocked-by-hold |

---

## 4. Hold State-Machine Interaction Table (as implemented, not assumed)

| Existing state | Can enter Hold? | Notes |
|---|---|---|
| Draft | Yes | |
| Submitted | Yes | |
| IDC Review | Yes | |
| Checked | Yes | |
| Approved | Yes | (a document can be approved, then discovered to need a hold before Issue) |
| Rejected | Yes | |
| Revise & Resubmit | Yes | |
| **Issued** | **No** | `PlaceHoldAsync` throws — Issued is a terminal/distributed state; correcting an issued document means a new revision, not a Hold |
| **Frozen (`IsFrozen`)** | **No** | `PlaceHoldAsync` throws — Frozen is already the "no more changes" terminal lock; Hold on a frozen document is a contradiction |

| While `IsOnHold = true` | Blocked? |
|---|---|
| `ApproveAsync` | **Blocked** (throws) |
| `IssueAsync` | **Blocked** (throws) |
| `CreateRevision` | **Not blocked** (deliberate — this is how a hold gets resolved) |
| `SubmitAsync` / `CheckAsync` | **Not blocked** (no rule required it; a hold on one open item doesn't necessarily stop discipline-level review of the rest of the document) |
| New Transmittal referencing this document | **Blocked** — `TransmittalService.CreateAsync` rejects the whole batch if any included document `IsOnHold` |
| Numbering (`DocumentNumberingService`) | **Not blocked** — a document on hold still needs an identifier; numbering is orthogonal to Hold by design |

---

## 5. Permission Matrix Changes

- New `RolePermission` columns: `CanHold`, `CanReleaseHold` (mirroring `CanFreeze`/`CanUnfreeze` exactly).
- New module `"Transmittal"` added to `PermissionService.Modules`, seeded (idempotent, retroactive-safe
  guard matching the existing `WorkOrder`/`Inspection` pattern) for DocumentController, EngineeringManager,
  EngineeringSupervisor, DisciplineEngineer, Viewer.
- **Fresh-install** defaults grant `CanHold`/`CanReleaseHold` to EngineeringManager, EngineeringSupervisor,
  DocumentController (DisciplineEngineer gets `CanHold` only, not release — matches the asymmetric
  Freeze/Unfreeze pattern where release requires more authority than the action itself).
- **Existing (already-seeded) databases**: `CanHold`/`CanReleaseHold` default to `false` for all
  non-SystemAdmin roles until an administrator explicitly grants them via the (now extended)
  `/Admin/PermissionMatrix` UI. This is deliberate fail-closed behavior, not an oversight — see §11.

---

## 6. Security Audit

- Confirmed (code reading): `TransmittalController` and the new `Hold`/`ReleaseHold` actions on
  `EngineeringDocumentsController` have **no** other entry point — `ITransmittalService`/
  `IDocumentHoldService` are only ever called from these controllers, so the permission-matrix check
  at the controller boundary cannot be bypassed by "another controller/action."
- Confirmed (live, unauthenticated HTTP): every new route redirects to `/Account/Login` with the correct
  `ReturnUrl`, including **direct POST attempts with no session and no anti-forgery token** to
  `/Transmittal/Create` and `/EngineeringDocuments/Hold` — the global authentication boundary
  (`RequireAuthenticatedUser` fallback policy) covers all new surfaces without any per-controller opt-in
  needed.
- **Not verified live**: role-specific denial (e.g., a `Viewer`-only session attempting `Hold`) — this
  session has no real user credentials for this dev environment (a constraint already hit and
  documented earlier in this engagement). The mechanism itself (`IPermissionService.HasPermissionAsync`
  + `Forbid()`) is identical, line-for-line pattern, to the already-shipped `Freeze`/`Unfreeze` gate, so
  confidence is high, but this is inference from an established pattern, not a fresh live proof for the
  new actions specifically. Flagged in §11.
- `RowVersion` optimistic concurrency (`Transmittal`, inherited via `BaseEntity` for `Document`) is wired
  through the same `SaveWithConcurrencyCheckAsync` catch-and-translate pattern already proven in
  `DocumentWorkflowService` — protects against two users silently overwriting each other's changes to the
  same row.
- IDOR: all new service methods take an `id`/`documentId`/`mdbId`-style parameter and load the entity
  fresh from the database on every call (no client-supplied object graphs are trusted) — same defensive
  pattern as the rest of the codebase.

---

## 7. Test Audit

- **Total: 85 passed, 0 failed, 0 skipped** (`dotnet test PEMS.Tests/PEMS.Tests.csproj --no-build`, 19s).
- Baseline before this phase: 58 (all pre-existing, all still green — zero regressions).
- New this phase: 27 — `WorkingDayCalculatorTests` (3), `DocumentHoldTests` (8),
  `HoldBlocksWorkflowTests` (4, running the real `IWorkflowRuntime` engine, not a mock),
  `TransmittalGovernanceTests` (12).
- Negative tests: yes (duplicate number, held-document block, no-recipients, already-sent,
  reissue-on-non-cancelled, response-before-issue, response-not-required, empty-hold-reason,
  already-on-hold, frozen/issued-doc-hold-block, release-not-on-hold).
- Concurrency tests: **not added for Transmittal numbering** — see §9 for the explicit reasoning
  (this is a scoping decision, not an oversight).

---

## 8. Database / Test Isolation Audit

- All new G/M tests use `PemsDbContextFactory.CreateInMemory(Guid.NewGuid()-based unique name)` —
  same pattern as `EngineeringDocumentFreezeTests`/`EngineeringWorkflowTests`. Zero writes to the
  shared SQL Server dev database from any new test.
- No SQL-Server-backed test was added in this phase (unlike `NumberingServiceTests`, which needs
  real T-SQL `UPDATE...OUTPUT` semantics InMemory can't simulate). Transmittal's numbering doesn't
  use that atomic-sequence mechanism (it's a simple unique-checked string, not a generated
  sequence), so no SQL-Server test was needed for its core logic — only for proving the *live race*
  scenario, which was deliberately not built (§9).
- This directly satisfies the directive's own concern in §25: the prior incident (SQL-Server tests
  polluting the shared dev DB) is not repeated here.

---

## 8b. Addendum (2026-08-19): Real concurrency validation executed, pre-existing test-data gap fixed

Following acceptance, the two open items from §11 were closed as follows:

**B — Real SQL-Server concurrency validation (now executed, not merely inferred):**
`TransmittalNumberingConcurrencyTests.CreateAsync_TenConcurrentRequestsSameNumber_ExactlyOneWinnerNoDuplicatePersisted`
was added and run against a genuinely isolated SQL Server database (`PEMSDB_ConcurrencyTest`, same
`localhost\SQLEXPRESS` instance, full real migration history applied via `Database.Migrate()` — not
`EnsureCreated`, not InMemory). 10 real concurrent connections attempted `CreateAsync` with the same
`TransmittalNo`: exactly 1 winner, 9 rejected with real error messages, exactly 1 row persisted, zero
orphan `TransmittalItem` rows, cleanup verified. Run duration ~28-34s, consistent with genuine SQL
Server work (an InMemory test would run in milliseconds). New file:
`PEMS.Tests/TestSupport/PemsDbContextFactory.cs` gained `CreateIsolatedSqlServerOrThrow`.

**Pre-existing, unrelated finding (discovered while validating the above):** running the full suite
surfaced 4 tests failing on `EmployeeId=1` no longer existing in the shared `PEMSDB` —
`FirstPassApprovalPrototypeTests` ×3 and `EngineeringRevisionQualityTests.RejectionCount_CountsRejectDecisionsAcrossInstances`.
Reproduced identically with the concurrency test fully excluded from the run, confirming it was
pre-existing test-data fragility (hardcoded dependency on shared mutable database state), not a
regression from this phase's G/M work. Fixed by making the affected tests self-seed a real
`Department`+`Employee` row deterministically (unique `Code` *and* `Name` — a first attempt missed
that `Departments.Name` also carries a unique index, causing a second failure on re-run against
already-accumulated rows from earlier attempts; corrected and leftover rows cleaned via a scoped,
transactional SQL script targeting only `FPA%`/`RQ%`-prefixed test data). Files changed:
`PEMS.Tests/Engineering/FirstPassApprovalPrototypeTests.cs`,
`PEMS.Tests/Engineering/EngineeringRevisionQualityTests.cs`. No production code was touched for this
fix. Final full-suite result: **86/86 passed, 0 failed, 0 skipped.**

---

## 9. Why no live concurrent-SQL-Server test for TransmittalNo uniqueness (superseded — see §8b)

`NumberingServiceTests.GetNextNumberAsync_ConcurrentCalls_NeverProduceDuplicates` was able to test
concurrency safely because it needs **zero foreign-keyed master data** — it only touches the
free-standing `NumberingSequences` table with a random test-only scope key.

A true concurrency test for `Transmittal.CreateAsync` would require real `Project` (and, to be
fully faithful, `Document`) rows to satisfy foreign keys, which means inserting real rows into the
shared SQL Server dev database from a test — exactly the failure mode the directive's §25 warns
about. Rather than do that, or silently skip the concern, this report states the tradeoff
explicitly: the DB-level protection (`TransmittalConfiguration`'s filtered unique index, confirmed
via direct migration-SQL inspection in this session) is the actual correctness guarantee; the test
suite proves the **application-level** duplicate check and the friendly-error translation path
work, and the **generated SQL migration** was read line-by-line to confirm the unique index is real
(not merely assumed).

---

## 10. Browser Verification

- Live (unauthenticated) HTTP verification performed against a locally-launched `dotnet run
  --project PEMS.Web` instance (see §6) — server started cleanly with zero DI resolution errors for
  any new service (`IDocumentHoldService`, `ITransmittalService` extensions, `TransmittalConfiguration`),
  proving the DI graph is correct at runtime, not just at compile time.
- **Not performed**: authenticated in-browser flows (create transmittal → add recipients → issue →
  inspect history; place Hold → verify Approve blocked → release → create revision). This requires
  real login credentials for the dev environment, which are not available in this session (same
  constraint documented earlier in this engagement's history). This is stated plainly per this
  directive's own rule: "DO NOT claim browser verification unless a real browser flow was executed."

---

## 11. Remaining Gaps

| Gap | Severity | Note |
|---|---|---|
| Role-specific authorization not live-tested (only code-pattern-matched to the proven Freeze/Unfreeze precedent) | MEDIUM | Close by running an authenticated session per role once real credentials are available |
| Live concurrent-SQL-Server race test for `TransmittalNo` not built | LOW | DB-level unique index verified by direct inspection instead; see §9 for the explicit tradeoff |
| `CanHold`/`CanReleaseHold` default to denied on already-seeded (pre-existing) databases | LOW (by design) | An administrator must explicitly grant these via `/Admin/PermissionMatrix` post-deployment — fail-closed, not a bug |
| Authenticated browser E2E of G/M flows not performed | MEDIUM | Blocked on missing dev-environment credentials, not skipped by choice |
| F — Material Coding (Appendix 4) | SOURCE-BLOCKED | See `.claude/Role/F-MATERIAL-CODING-SOURCE-BLOCKED.md` — requires the original (non-OCR) source document |
| VDR-side "Marked for Revision" checkbox not added to `VDRController.AddComment`'s form | LOW | `IDocumentCommentService.AddCommentAsync(marksRevisionRequired:)` supports it (defaults false); only the Engineering Documents UI was wired in this phase, matching the directive's explicit M focus on the Engineering domain |

---

## 12. Final Acceptance Matrix

| Domain | Status | Evidence |
|---|---|---|
| Engineering Documents | PASS | Unchanged core lifecycle; Hold guard added without altering Submit/Check/Approve/Issue semantics for non-held documents (regression-proven: 58 pre-existing tests still green) |
| Revision | PASS | `DocumentRevision`/`SupersededRevisionId` chain unchanged; `DocumentNo` stability confirmed by direct code reading |
| Revise & Resubmit | PASS | Unchanged; `CreateRevision` extended (backward-compatible optional param) to close Clouded comments |
| IssuePurpose | PASS (pre-existing, this session) | Unified IFA/IFR/IFI/FI dropdown already shipped earlier this session; `Transmittal.Purpose` now offers the same set |
| Numbering | PASS | `EngineeringDocument`/`VDRItem`/CONTRACTOR/Vendor-format numbering untouched by this phase; `Transmittal` numbering hardened with a real DB-level unique index (was previously unenforced free text) |
| Transmittal | PASS | Full governance model — see §2 |
| SLA | PASS | `WorkingDayCalculator` reused (not duplicated) for both VDR review SLA (earlier this session) and Transmittal response SLA (this phase) |
| Hold | PASS | See §3-4 |
| Clouded | PASS | See §3 |
| VDR | PASS (unaffected) | 58 pre-existing tests covering VDR untouched and green |
| Vendor Documents | PASS (unaffected) | No changes this phase |
| MDB | PASS (unaffected) | No changes this phase |
| FTP | PASS (unaffected) | No changes this phase |
| Dashboard | PASS (unaffected) | No changes this phase |
| Authorization | CONDITIONAL PASS | Server-side mechanism proven correct by pattern-match + live auth-boundary test; role-specific live proof pending credentials |
| Audit Trail | PASS | Global `AuditLog` (pre-existing, automatic) covers every new entity without new code |
| Tests | PASS | 85/85 green, 0 regressions, 27 new (11 negative-path) |
| Graphify | PASS | `graphify update` run; verified via `graphify query` that `Transmittal`, `TransmittalRecipient`, `TransmittalConfiguration`, `IDocumentHoldService`, `.PlaceHoldAsync()`, `.Hold()`, and the new test files are present and correctly linked in the graph — not merely assumed |

---

## 13. Bug found and fixed during this audit (per §34: stop and document, don't hide)

While reviewing the first generated EF Core migration (before applying it), the scaffolded SQL
contained an unexpected shadow foreign-key column: `CancelledByEmployeeEmployeeId`, alongside the
intended `CancelledById`. Root cause: `Transmittal.CancelledByEmployee` (navigation property) did
not match EF Core's naming convention for `Transmittal.CancelledById` (FK property) — EF silently
invented a second, disconnected shadow FK rather than binding the two. Fixed by renaming the
navigation property to `CancelledBy`, matching the already-correct `SentBy`/`SentById` pair on the
same entity; the migration was deleted and regenerated, and the corrected SQL was re-inspected
before being applied. This was caught by migration review before it ever reached the database —
consistent with this session's established practice of reading every generated migration before
running `database update`.
