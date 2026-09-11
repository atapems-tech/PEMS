# PEMS EDMS — Phase 3 Final Report: Transmittal Historical Integrity & Acknowledgement

## Phase 3 Verdict
**PASS**

## Repository Findings
A mature, governance-hardened Transmittal/Distribution system was already in production before this phase began — not assumed, verified in full by direct code trace (see `docs/audits/PEMS_EDMS_PHASE3_TRANSMITTAL_AUDIT.md`). It already had: atomic race-safe numbering (two paths — manual+DB-unique-index, and an atomic `INumberingService`-based generator for MR-driven transmittals), immutability-after-issue enforced structurally (no `Edit`/`Update` method exists on `ITransmittalService` at all), a Cancel→Reissue supersession chain identical in spirit to `DocumentRevision.SupersededRevisionId`, `RowVersion` optimistic concurrency, structured (non-free-text) recipients, per-recipient `ResponseRequired`, working-day-aware due-date calculation, Hold-blocking at creation, and 13 pre-existing governance tests — including one that already proves the revision-number half of the mandatory Historical Immutability Test (§19). None of this was rebuilt.

Two genuine, code-verified gaps were the actual scope of this phase:
- **G-21 (P0):** `TransmittalItem` snapshotted only a revision-number string, with no link to the actual `DocumentRevision` row or its official file/hash — "what exactly was transmitted" could not be reconstructed beyond a bare number.
- **G-22 (P1):** No per-recipient acknowledgement existed, only one transmittal-wide "response" field, making partial acknowledgement across multiple recipients invisible.

## Implemented
- `PEMS.Domain/Entities/TransmittalItem.cs` — added `DocumentRevisionId`/`DocumentRevision` nav, `DocumentAttachmentId`/`DocumentAttachment` nav, `SnapshotFileName`, `SnapshotFileHash`, `SnapshotFileSize`.
- `PEMS.Domain/Entities/TransmittalRecipient.cs` — added `AcknowledgedAt`, `AcknowledgedById`/`AcknowledgedBy` nav, `AcknowledgementNote`.
- `PEMS.Persistence/Configurations/DocumentCoreConfigurations.cs` — new `TransmittalItemConfiguration` (2 `Restrict` FKs) and `TransmittalRecipientConfiguration` (1 `Restrict` FK).
- `PEMS.Persistence/Servises/TransmittalService.cs` — `CreateAsync` now resolves and snapshots the matching `DocumentRevision` + its official `DocumentAttachment` at the same point `RevisionNo` is already captured; `ReissueAsync` copies the new fields verbatim (not re-resolved, matching the existing `RevisionNo` convention); new `AcknowledgeAsync` method with duplicate/draft/idempotency guards and partial-vs-full acknowledgement derivation onto `Transmittal.Status` (reusing the pre-existing but previously-unreachable `"Acknowledged"` status value); `GetByIdAsync` mapping extended.
- `PEMS.Application/Interfaces/ITransmittalService.cs`, `PEMS.Application/DTOs/TransmittalDto.cs` — interface/DTO additions, fully additive.
- `PEMS.Web/Controllers/TransmittalController.cs` — new `Acknowledge` POST action, gated by the existing `"Transmittal"/"Edit"` permission (same tier as `RecordResponse`).
- `PEMS.Web/Views/Transmittal/Details.cshtml` — Documents table gains a "File at time of sending" column (snapshot filename + truncated hash, explicitly labeled as a historical snapshot, not a live link); Recipients table gains an "Acknowledgement" column with inline acknowledge form for unacknowledged recipients on issued transmittals.
- Migration `20260911205849_AddTransmittalHistoricalSnapshotAndAcknowledgement` — 7 new nullable columns, 3 indexes, 3 `Restrict` FKs, purely additive.
- `PEMS.Tests/Engineering/TransmittalHistoricalIntegrityTests.cs` — 7 new tests, including the exact mandatory §19 scenario.
- `docs/audits/PEMS_EDMS_PHASE3_TRANSMITTAL_AUDIT.md` (new), `PEMS_EDMS_GAP_MATRIX.md` (updated: G-06/G-07 marked FIXED from Phase 1, G-10 narrowed per Phase 2, new rows G-21 through G-24), `PEMS_EDMS_IMPLEMENTATION_ROADMAP.md` (updated with an honest execution log reconciling the Phase-0 forecast against what each phase's own fresh audit actually found).

## Reused (explicit, per the master prompt's own requirement)
`IPermissionService`/`RolePermission` (no new permission verb — "Acknowledge" uses the existing `"Edit"` tier), the existing DB-unique-index + atomic-numbering patterns (neither touched), `RowVersion`/`DbUpdateConcurrencyException` handling (same pattern as every other Transmittal mutation), the generic `PEMSDbContext` audit (no new audit table), `HashingStream`'s already-computed `DocumentAttachment.FileHash` (copied, not recomputed), and the existing Cancel→Reissue immutability pattern (extended, not replaced). `IWorkflowRuntime` was deliberately **not** pulled in — Transmittal's own explicit-guard state machine (3 states, no branching approval logic) already satisfies the immutability requirement without it, and forcing it in would be exactly the "duplicate workflow engine" the master prompt forbids. `DocumentComment` was evaluated and deliberately **not** reused for transmittal responses (documented reasoning in the audit §2.8 — different domain scope, a `Document`-scoped comment engine misrepresents a `Transmittal`-scoped, possibly multi-document exchange).

## Historical Integrity
`TransmittalItem.RevisionNo` (string) was already proven immutable by a pre-existing test (`CreateAsync_SnapshotsRevisionNoAtTimeOfCreation`) because `CreateAsync` reads it once at item-creation time and no code path ever re-reads or overwrites it afterward — confirmed by a full trace of `TransmittalService` (no `Edit`/`Update` method exists at all). This phase extends the exact same guarantee to the file/revision identity: `DocumentRevisionId`/`DocumentAttachmentId`/`SnapshotFileName`/`SnapshotFileHash`/`SnapshotFileSize` are resolved and copied at the identical moment, then never touched again — `MarkSentAsync` doesn't touch `Items`, and `ReissueAsync` copies them field-for-field rather than re-resolving. The new test `MandatoryScenario_IssueRevisionA_ThenCreateRevisionB_TransmittalStillShowsRevisionAFileIdentity` is the master prompt's exact §19 acceptance scenario and passes.

## Security / Authorization
Every existing `TransmittalController` action was already server-side gated (verified, not assumed, in the audit). The new `Acknowledge` action follows the identical pattern: `IPermissionService.HasPermissionAsync(_currentUser.Roles, "Transmittal", "Edit")` checked in the controller body before any service call, returning `Forbid()` on failure — bypassing the UI cannot skip it. No new permission verb was invented. `AcknowledgeAsync` additionally validates business-rule authorization at the service layer independent of the controller: the transmittal must be issued (`IsIssued`), and duplicate acknowledgement is explicitly rejected — this is enforced in the service, not just the UI, so a direct API call is equally protected.

## Auditability
No new audit table. The generic `PEMSDbContext` change-tracker (confirmed in Phases 1-2 to automatically capture entity-level changes with actor/timestamp) now naturally covers the new `TransmittalItem`/`TransmittalRecipient` columns, keyed by their specific row IDs, with no additional code required.

## Database
- Migration `20260911205849_AddTransmittalHistoricalSnapshotAndAcknowledgement` — repro-validated on disposable `PEMSDB_Phase3Repro` (full 62-migration chain applied cleanly, all 7 new columns confirmed nullable, database dropped after validation), `dotnet ef migrations has-pending-model-changes` confirmed clean before and after, then applied to live **PEMSDB**.
- Verified after live apply: all 7,290 existing `TransmittalItems` rows preserved with `DocumentRevisionId` null (no data fabricated for historical items — an honest, documented limitation, not a silent gap); `TransmittalRecipients` had 0 pre-existing rows.

## Tests
```
New tests:  7  (PEMS.Tests/Engineering/TransmittalHistoricalIntegrityTests.cs)
Regression: 1093 (Phase 2 baseline, all still passing)
Total:      1100 / 1100 passed, 0 failed, 0 skipped, 7m46s
```
Also independently re-verified: `dotnet test --filter "FullyQualifiedName~Transmittal"` → 23/23 (12 pre-existing `TransmittalGovernanceTests` + 1 `TransmittalNumberingConcurrencyTests` + 7 new + 3 matching by name substring), confirming zero regression to the pre-existing Transmittal governance suite specifically.
`dotnet build PEMS.slnx --configuration Debug` → **Build succeeded**, 0 errors (only pre-existing unrelated nullable warnings).

## Remaining Findings
- **G-23 (business decision, not a defect):** Transmittal recipients remain free-text, not FK'd to PEMS `User`/`Employee`. Judged intentional — most recipients are external parties (client/contractor DCC) with no PEMS account; adding an FK without a demonstrated need would be speculative.
- **G-24 (documented limitation, not a defect):** `TransmittalLinker` (the separate MDR/VDR-import item-creation path) does not populate the new G-21 snapshot fields — items created via Excel import will have `DocumentRevisionId`/snapshot fields `null`. Extending that path was explicitly out of this phase's scope per its own §32 ("do not expand into MDR/VDR redesign").
- **Future enhancement (not a defect):** G-21's revision resolution matches by `DocumentId + RevisionNo` string equality, which works correctly today but is one level less direct than storing the exact `DocumentRevisionId` at the point of *user selection* in the UI (currently the UI picks a document, and the service infers "its current revision" server-side, matching §22's "convenience" allowance but not exposing an explicit revision-picker per document in the Create form). A future UI enhancement could let the user see and confirm the exact revision before adding it, closing this small gap between "selected" and "resolved."

## Commit Recommendation
```
feat(edms): implement controlled transmittal historical integrity and per-recipient acknowledgement
```

Per this phase's explicit final instruction, **nothing has been pushed to GitHub**, and nothing has been committed either — the recommended message above is provided for your review; let me know when you'd like this committed (and pushed).
