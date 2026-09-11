# PEMS EDMS — Phase 3 Transmittal, Distribution & Acknowledgement Audit

**Repository:** `PEMS-cmms-edit-empoleeys-master` · **Branch:** `main` · **Baseline commit:** `cbe6f99` (Phase 2)
**Method:** Direct file inspection. Every claim cites an exact file and, where useful, a line number.

---

## 0. Headline Finding

**Phase 3's assumed premise — that a Transmittal/Distribution engine needs to be built — is wrong.** A mature, governance-hardened Transmittal system already exists in production, built in a prior session's "Transmittal Governance" audit (referenced throughout the code as "بخش G ممیزی حاکمیت Transmittal" / "Section G Transmittal Governance audit"). It already implements: atomic race-safe numbering, database-unique transmittal numbers, immutable-after-issue records via a Cancel→Reissue supersession chain (not silent edit), `RowVersion` optimistic concurrency, structured (non-free-text) recipients, per-recipient `ResponseRequired`, working-day-aware response due-date calculation, Hold-blocking at creation time, and 13 existing governance tests. **This entire layer is reused as-is; none of it is rebuilt.**

Two genuine, code-verified gaps were found and are the actual scope of this phase:
1. **G-21 (P0):** `TransmittalItem` snapshots the revision **number as a string** (already correctly immutable — proven by the existing test `CreateAsync_SnapshotsRevisionNoAtTimeOfCreation`), but has no link to the actual `DocumentRevision` row or its official file/hash. "What exactly was transmitted" (§7/§20 of the master prompt) cannot be reconstructed today beyond a bare revision-number string.
2. **G-22 (P1):** Acknowledgement (§14/§15 — "did the recipient receive/handle this") does not exist as a concept distinct from the existing `ResponseRequired`/`ResponseReceivedDate`/`ResponseNote` fields, which model a single transmittal-wide **formal response**, not a per-recipient **receipt acknowledgement**. With multiple recipients, there is no way to know which ones have acknowledged and which haven't.

---

## 1. Source-of-Truth Chain (§2 of the master prompt, answered with evidence)

| Question | Answer | Evidence |
|---|---|---|
| Authoritative document entity | `Document` (TPT base) / `EngineeringDocument` | `PEMS.Domain/Entities/Document.cs`, unchanged since Phase 0 |
| Authoritative revision entity | `DocumentRevision` | `PEMS.Domain/Entities/DocumentRevision.cs`, extended (not replaced) in Phases 1-2 |
| Official file association | `DocumentAttachment.DocumentRevisionId` + `IsOfficial` + `FileHash` | Added in Phase 1 (`PEMS.Domain/Entities/DocumentAttachment.cs:17,22,31`) |
| Document status control | `DocumentWorkflowService` over `IWorkflowRuntime` | Audited in Phase 2, unchanged |
| Client review representation | `DocumentRevision.ClientReviewStatus/Comment/Date/LetterId` (revision-scoped, Phase 2) | `DocumentRevision.cs` |
| Comments/responses | `DocumentComment` (carry-forward via `ParentCommentId`/`TriggeredRevisionId`) | Audited Phase 2, unchanged |
| Recipients/project parties | **No dedicated Recipient/User/Employee FK** — `TransmittalRecipient.Name` is a plain string with a `RecipientType` enum-like field (`Internal/External/Vendor/Client`) | `PEMS.Domain/Entities/TransmittalRecipient.cs:19,22` |
| Authorization | Existing `IPermissionService.HasPermissionAsync(roles, "Transmittal", verb)` — the CRUD-verb `RolePermission` model (`CanView/CanCreate/CanEdit/CanDelete/CanApprove`), seeded per-role in `PermissionService.cs:176-190` | `TransmittalController.cs` (every action gated) |
| Audit | Generic `PEMSDbContext` change-tracker (same mechanism confirmed in Phases 1-2) | No dedicated Transmittal audit table exists or is needed |
| Existing transmittal/distribution mechanism | **Yes — see §2 below.** | — |

**Recipient model finding:** Recipients are deliberately free-text (`Name` + `Organization` + `RecipientType`), not FK'd to `User`/`Employee`. This is evaluated as **intentional, not a gap** — most Transmittal recipients in an EPC/owner context (client DCC, contractor DCC, external vendor) have no PEMS account at all, so an FK would be wrong for the common case. Internal recipients (an internal discipline, another PEMS user) are the minority case and are not currently FK'd either, but no evidence of active harm was found (the free-text field works for internal names too, just without click-through). Documented as a lesser, deferred finding (G-23), not fixed this phase — matches the master prompt's own instruction not to invent a new identity model without a verified need.

---

## 2. Existing Transmittal Capabilities (verified in code, not assumed)

### 2.1 Entities
- `Transmittal` (`PEMS.Domain/Entities/Transmittal.cs`) — `TransmittalNo`, `ProjectId`, `From/To`, `Purpose`, `Status` (`Draft/Sent/Acknowledged/Cancelled`), `TransmittalDate` vs `SentDate` (deliberately separate — draft-creation time vs. actual-issue time), `ResponseRequired`/`ResponseDueDate`/`ResponseReceivedDate`/`ResponseNote`, `CancelledDate/Reason/By`, `SupersedesTransmittalId` (self-referencing chain, same pattern as `DocumentRevision.SupersededRevisionId`), `RowVersion` (optimistic concurrency), computed `IsIssued`/`IsResponseOverdue`.
- `TransmittalItem` (`TransmittalItem.cs`) — `DocumentId` + `Document` nav (works for both `EngineeringDocument` and `VDRItem` via the shared `Document` TPT core, per its own comment), `RevisionNo` (string snapshot), `Remarks`.
- `TransmittalRecipient` (`TransmittalRecipient.cs`) — `Name`, `RecipientType`, `Organization`, per-recipient `ResponseRequired`, `IsActive`.

### 2.2 Numbering (§5)
Two numbering paths exist, both already safe, neither needs rebuilding:
- **Manual entry + DB-unique-index race protection**, used by the general `TransmittalController.Create` (`PEMS.Web/Controllers/TransmittalController.cs:72-127`): `TransmittalService.CreateAsync` pre-checks `AnyAsync`, then catches the SQL unique-constraint violation (`IsUniqueConstraintViolation`, `TransmittalService.cs:244-248`) from the real DB index (`TransmittalConfiguration.HasIndex(x => x.TransmittalNo).IsUnique()`, `DocumentCoreConfigurations.cs:65-67`) as the actual race-safety boundary — same proven pattern as `Document.DocumentNo`.
- **Atomic auto-generated numbering** for MR/vendor-driven transmittals: `TransmittalNumberingService.GenerateNextNumberAsync` (`PEMS.Persistence/Servises/TransmittalNumberingService.cs`), built on the pre-existing `INumberingService.GetNextNumberAsync` (the same atomic `UPDATE...OUTPUT` allocator used for `DocumentNo`), format `TV-{DisciplineCode}-{UnitCode}-{MRLast4}-{Sequence}`.

Neither path needed to be touched this phase.

### 2.3 Immutability After Issue (§3, §10, §11 — the core Phase 3 concern)
Confirmed by direct code trace of `TransmittalService`:
- `MarkSentAsync` (line 250-273) only flips `Status`/`SentDate`/computes `ResponseDueDate` — it never touches `Items` or `Recipients`.
- **No `EditAsync`/`UpdateAsync` method exists at all** on `ITransmittalService` — there is structurally no code path that can silently mutate a `Transmittal`'s items, recipients, or revision references after creation, issued or not.
- Correction path is `CancelAsync` (new `Cancelled` status, reason required, original record's `Sent`/`SentDate`/items/recipients left untouched — `CancelAsync`, line 275-294) → `ReissueAsync` (creates a **new** `Transmittal` row copying items/recipients, linked via `SupersedesTransmittalId`, forcing a new number — line 296-352). This is exactly the master prompt's own required pattern ("explicit controlled mechanism... Cancel and then Reissue that creates a new record").
- **Existing proof test:** `TransmittalGovernanceTests.CreateAsync_SnapshotsRevisionNoAtTimeOfCreation` (`PEMS.Tests/Engineering/TransmittalGovernanceTests.cs:75-90`) already creates a transmittal, then changes `doc.RevisionNo`, and asserts the transmittal's item still shows the original revision number. **This is Section 19's mandatory historical-immutability test, already passing, for the revision-number dimension.**

### 2.4 What "historical snapshot" actually captures today vs. what's missing
`RevisionNo` (string) is captured at item-creation time (`TransmittalService.CreateAsync:198-207`, reading `doc.RevisionNo` — the `Document`'s current revision number at that moment) and never re-read afterward. This satisfies the revision-*number* immutability requirement. It does **not** capture:
- A `DocumentRevisionId` FK to the actual `DocumentRevision` row (so there's no stable join to that revision's own data — `ClientReviewStatus`, `IssuePurpose`, etc. — without a fragile `DocumentId + RevisionNo string` re-lookup that could match zero or multiple rows if numbering conventions ever changed).
- Which `DocumentAttachment` (official file) was current for that revision at the time, nor its `FileName`/`FileHash`/`FileSize`.

This is **Finding G-21** — see §4.

### 2.5 Authorization (§17)
Every `TransmittalController` action is gated server-side via `IPermissionService.HasPermissionAsync(_currentUser.Roles, "Transmittal", verb)` before calling the service — `Create`/`Create`, `MarkSent`/`Approve`, `Cancel`/`Delete`, `Reissue`/`Create`, `RecordResponse`/`Edit`, `Details`(view flags only, read is open per existing pattern). Confirmed **not** UI-only: the permission check happens in the controller action body, before any service call, and returns `Forbid()` on failure — bypassing the UI (e.g., a raw POST) cannot skip it.

### 2.6 Held-Document Integration (existing business rule, reused not rebuilt)
`CreateAsync` blocks any document with `IsOnHold == true` from being added to a transmittal at all (`TransmittalService.cs:175-182`), with a friendly error listing the blocked document numbers — confirmed by the existing test `CreateAsync_DocumentOnHold_Throws`.

### 2.7 Import Integration
`TransmittalLinker.FindOrCreateAsync` (`PEMS.Persistence/Servises/TransmittalLinker.cs`) is a **second, separate** `TransmittalItem`-creation path used by `MDRImportService`/`VDRImportService` when a transmittal number is discovered during MDR/VDR Excel import — it finds-or-creates a `Transmittal` and adds an item with just `DocumentId`/`RevisionNo` (string), bypassing `TransmittalService.CreateAsync` entirely. **This path is documented, not modified** — extending it to also populate the new snapshot fields (§4) would require attachment context that may not exist at import time; leaving it to populate what it always has (the string) is the correct minimal-risk choice, and is called out explicitly as a known limitation rather than silently left inconsistent.

### 2.8 Comment Register Reuse Assessment (§26)
Phase 2 confirmed `DocumentComment` already has carry-forward traceability (`ParentCommentId`, `MarksRevisionRequired`/`TriggeredRevisionId`) for internal review comments. **It is not reused for Transmittal responses in this phase.** Reason: `DocumentComment` is scoped to a `Document`/`DocumentRevision` (an authoring/review concept — "what's wrong with this drawing"), while a Transmittal response/acknowledgement is scoped to a `Transmittal`/`TransmittalRecipient` (a delivery/receipt concept — "did you get this package, and what's your feedback on the package as a whole, which may contain several documents"). Forcing a `DocumentId`-scoped comment onto a transmittal that carries multiple documents would misrepresent which document the comment belongs to. The existing `Transmittal.ResponseNote`/new `TransmittalRecipient.AcknowledgementNote` (this phase) are kept as their own lightweight fields, not a duplicate comment engine — this is a data-model decision, not a workflow engine, so it does not violate "do not create a duplicate Comment Register."

---

## 3. Distribution vs. Client Review vs. Acknowledgement (§13 — verified already separate)

Confirmed no confusion exists in the current code: `Document.ClientApprovalStatus`/`DocumentRevision.ClientReviewStatus` (Phase 2) belong to `DocumentWorkflowService`, entirely separate code path from `TransmittalService`. Nothing in `TransmittalService` reads or writes any client-approval field, and nothing in `DocumentWorkflowService` reads or writes any Transmittal field. These are already two independent systems answering two independent questions, exactly as §13 requires. No change was needed to preserve this separation — it already existed.

---

## 4. Findings

### Finding G-21 (P0) — `TransmittalItem` cannot reconstruct the historically-transmitted revision/file identity beyond a bare number string
**Evidence:** `TransmittalItem.cs` has no `DocumentRevisionId`, `DocumentAttachmentId`, `FileName`, `FileHash`, or `FileSize` field. `TransmittalService.CreateAsync:198-207` only ever sets `DocumentId` and `RevisionNo` (string). Phase 1 built exactly the data needed to close this (`DocumentAttachment.DocumentRevisionId`/`IsOfficial`/`FileHash`) but Phase 3 is the first phase to actually wire it into the Transmittal record.
**Concrete failure scenario:** A dispute arises over whether the file actually delivered to a client in Transmittal T-001 matched what's now in the system. Today, `T-001`'s `TransmittalItem` only proves "Revision 00 was referenced" — it cannot prove *which physical file* (by hash) was the official one at that moment, because that link was never captured.
**Applicable requirement:** Master prompt §7 (Historical Snapshot Requirements), §8 (File Integrity), §20 (Historical File Availability), §19 (mandatory Historical Immutability Test — file/hash half of it).
**Risk:** High in a disputed-delivery scenario; the revision-number half of this already works (§2.3), so this is a completeness gap, not a currently-observed incorrect behavior.
**Priority:** P0 — this is the direct, verified target of this phase.

### Finding G-22 (P1) — No per-recipient/per-item acknowledgement; only one transmittal-wide "response"
**Evidence:** `Transmittal.ResponseReceivedDate`/`ResponseNote` are single fields on the `Transmittal` itself — `TransmittalService.RecordResponseAsync` (line 354-369) writes them once, with no recipient or item parameter at all. `TransmittalRecipient` has `ResponseRequired` (bool) but no `AcknowledgedAt`/`AcknowledgedBy`/acknowledgement-status field of any kind.
**Concrete failure scenario:** A Transmittal sent to 3 recipients (Client DCC, Contractor DCC, Internal Archive) receives a response from the Client. `RecordResponseAsync` marks the *entire transmittal* as responded — there is no way to see that the Contractor DCC has not yet acknowledged receipt at all.
**Applicable requirement:** §14 (Acknowledgement), §15 (Partial Acknowledgement — explicitly mandatory acceptance scenario).
**Risk:** Medium — delivery-confirmation ambiguity across multi-recipient transmittals, a real operational blind spot for an EPC document controller tracking outstanding acknowledgements.
**Priority:** P1.

### Finding G-23 (P3, documented, not fixed) — Recipients are free-text, not linked to PEMS identity
Evaluated in §1 above and judged intentional for the common case (external recipients with no PEMS account). No active-harm evidence found for internal recipients either. Deferred — adding an optional FK without a demonstrated reporting/filtering need would be exactly the "add fields because they sound useful" the master prompt forbids.

### Finding G-24 (P3, documented, not fixed) — `TransmittalLinker` (MDR/VDR import path) will not populate the new G-21 snapshot fields
See §2.7. Transmittals created via MDR/VDR import will have `DocumentRevisionId`/`DocumentAttachmentId`/file-snapshot fields left `null` on their items (the general `TransmittalService.CreateAsync` path populates them; the import-linker path does not). This is a real, honest limitation of this phase's scope, not a silent gap — the alternative (extending the import path too) was judged out of scope because it touches MDR/VDR import logic that Phase 3's own §32 explicitly says not to expand into ("Do NOT expand this phase into... MDR redesign... VDR redesign").

---

## 5. Reused Infrastructure (explicit list, per §33 requirement)

- `IWorkflowRuntime` / `DocumentWorkflowService` — **not touched**, Transmittal has never used it and doesn't need to (its own Draft/Sent/Cancelled state machine, enforced by explicit code guards in `TransmittalService`, already satisfies §25's "reuse existing controlled transition pattern if a parallel one for this concept doesn't already exist and adding the general engine would be overkill for 3 states with no branching approval logic").
- `INumberingService` (atomic `UPDATE...OUTPUT`) — reused via `TransmittalNumberingService` for MR-driven transmittals; general transmittals reuse the DB-unique-index pattern. Neither rebuilt.
- `IPermissionService` / `RolePermission` — reused unchanged; "Acknowledge" (new, §7 implementation) is gated under the existing `"Edit"` verb, the same tier as the existing `RecordResponse` action, rather than inventing a 6th permission verb.
- `HashingStream` (built for Enterprise File Storage Management, reused again in Phase 1) — reused a third time: the new snapshot copies an **already-computed** `DocumentAttachment.FileHash`, it does not hash anything itself.
- Generic `PEMSDbContext` audit — reused, no new audit table.
- `RowVersion`/`DbUpdateConcurrencyException` pattern — reused for the new `AcknowledgeAsync` method, same as every other Transmittal mutation.
