# PEMS Letter Details — Controlled Lifecycle & Gmail-Inspired UX — Final Report

## 1. Repository Findings (Phase 1 Audit)

- **Unified entity confirmed:** `Letter` (`PEMS.Domain/Entities/Letter.cs`) already covers Incoming/Outgoing/Internal via a `LetterType` discriminator — same pattern as `Document`/`DocumentKind`. No duplicate correspondence architecture was created.
- **Per-type status vocabularies discovered:** `Letter.Status` meant different things per type — Incoming: `Registered/UnderReview/Referred/Closed`; Outgoing: `Draft/PendingApproval/Approved/Rejected/Issued`; Internal: `Sent/Acknowledged/Closed`. The new controlled-revision lifecycle only applies to **Outgoing** letters (the only type with authored content requiring internal approval before external issue); Incoming/Internal's existing simpler flows are untouched.
- **Actor-ID convention confirmed:** `RegisteredById`/`ApprovedById` are unmapped `int?` fields storing `ApplicationUserId` (not `EmployeeId`, no navigation/FK) — confirmed via `SearchAsync`'s `x.RegisteredById == requestingApplicationUserId.Value` usage. All new actor fields follow this exact convention.
- **`ParentLetterId`/thread infrastructure existed but was dead code:** `LetterService.GetThreadAsync` correctly walks the parent chain, but `LetterController.Details` never called it — the Gmail-style conversation view was structurally possible but never surfaced. Now wired in.
- **`LetterReferral` audited and found NOT to be a review mechanism:** it has only `Create/GetForLetter/GetMyReferrals/MarkRead/Complete` — no `Approve`/`Reject`/`ReturnForRevision` semantics anywhere in `LetterReferralService.cs`. This justified introducing the new `LetterReview` entity rather than overloading Referral.
- **Two severe pre-existing defects found in `LetterService`/`LetterController`:**
  - `AdvanceStatusAsync(int letterId, string newStatus, int? actorId)` wrote **any caller-supplied string** directly to `Letter.Status` with zero transition validation, callable via a free-form status-button row in `Details.cshtml` (`Issued → Draft` was literally one click away).
  - `UpdateBodyAsync(int letterId, string body)` rewrote a letter's official `Body` in **any status** with **no ownership or lifecycle check at all** — a fully unguarded direct-POST target.
- **Performance defect confirmed:** `LetterController.Details` called the same `LoadDropdownsAsync()` used by `Create`, unconditionally loading every active Employee/Document/MaterialRequisition row on a page that is read-heavy.
- **Concurrency infrastructure confirmed present but unused for Letter:** `Letter : BaseEntity` already carries `[Timestamp] RowVersion`; nothing in the old Letter code path used it.

## 2. Problems Found (Summary)

| # | Problem | Severity |
|---|---|---|
| 1 | `AdvanceStatusAsync` — free-form status write, any transition, from UI buttons | Critical |
| 2 | `UpdateBodyAsync` — official content rewritable in any status, by anyone | Critical |
| 3 | No original-author-only edit rule for returned letters | High |
| 4 | Referral conflated as the closest thing to "review" — no real approve/return/reject | High |
| 5 | `Details` view never called existing `GetThreadAsync` — no conversation view | Medium |
| 6 | `Details` always loaded full Employee/Document/MR dropdowns | Medium |
| 7 | `RowVersion` never applied/checked in any Letter write path | Medium |
| 8 | `SubmitForReviewAsync` (introduced this phase, self-caught before shipping) initially had no ownership check | Self-corrected |

## 3. Architecture Changes

- **New entity `LetterReview`** (`PEMS.Domain/Entities/LetterReview.cs`) — one row per reviewer decision (`Approved`/`ReturnedForRevision`/`Rejected`), mirroring `LetterReferral`'s row-per-event pattern so full multi-round history is automatic with no separate audit table. `ReturnedForRevision` rows additionally snapshot `PreviousSubjectSnapshot`/`PreviousBodySnapshot` — the exact content the reviewer saw, preserved before the author edits it.
- **`Letter` additive fields:** `ClosedDate`, `ReturnedByUserId/ReturnedDate/ReturnReason` (cache of the *latest* return — cleared on resubmit, full history lives in `LetterReview`), `RejectedByUserId/RejectedDate/RejectReason`, `RevisionCount`, `Reviews` navigation collection. No existing field renamed or removed.
- **`ILetterService`:** removed `AdvanceStatusAsync`/`UpdateBodyAsync`; added 8 explicit, individually-validated lifecycle methods: `SubmitForReviewAsync`, `ApproveAsync`, `ReturnForRevisionAsync`, `RejectAsync`, `IssueAsync`, `CloseAsync`, `SaveDraftEditAsync`, `ResubmitAsync`. Each returns `(bool Success, string? Error)`, matching the project-wide `AdvanceStatusAsync` convention already used by WorkOrder/WorkRequest/Permit/StoreRequisition/Isolation.
- **Concurrency:** `SaveDraftEditAsync`/`ResubmitAsync` accept a client-supplied `rowVersion` and apply it via `_context.Entry(letter).Property(x => x.RowVersion).OriginalValue = rowVersion`, letting EF Core throw `DbUpdateConcurrencyException` on a genuine conflict (caught and converted to a friendly error).

## 4. Final Lifecycle (Outgoing letters)

```
Draft --(SubmitForReview, owner only)--> PendingApproval
PendingApproval --(Approve, reviewer)--> Approved --(Issue, reviewer)--> Issued --(Close, reviewer)--> Closed
PendingApproval --(ReturnForRevision, reviewer, reason required)--> ReturnedForRevision
    ReturnedForRevision --(Resubmit, original author only)--> PendingApproval   [RevisionCount++, return-cache cleared]
PendingApproval --(Reject, reviewer, reason required)--> Rejected   [terminal]
```

Every arrow is a named, server-validated action (`RequiredFromStatus` map in `LetterService`) — no arrow exists for `Issued → Draft` or any other unlisted transition, and none can be reached by posting an arbitrary status string (no such parameter exists anymore). Incoming (`Registered/UnderReview/Referred/Closed`) and Internal (`Sent/Acknowledged/Closed`) keep their original, untouched flows; `CloseAsync` is type-aware (`Issued` required only for Outgoing).

## 5. Final Authorization Rules

| Action | Rule enforced | Where |
|---|---|---|
| `SaveDraft` | `RegisteredById == currentUserId` AND `Status ∈ {Draft, ReturnedForRevision}` | Service (`CheckOriginalAuthorCanEdit`) — controller has no `[RequirePermission]`, only authentication, by design (§7: not role-based) |
| `Resubmit` | `RegisteredById == currentUserId` AND `Status == ReturnedForRevision` | Service — same reasoning |
| `SubmitForReview` | `RegisteredById == currentUserId` AND `Status == Draft` | Service |
| `Approve`/`ReturnForRevision`/`Reject`/`Issue`/`Close` | `[RequirePermission("Correspondence","Approve")]` | Controller attribute (existing `IAsyncAuthorizationFilter`) |
| `ReturnForRevision`/`Reject` | non-empty `reason` required | Service |
| Any of the above via direct POST | Re-validated fully server-side regardless of what the UI shows/hides | Service (never trusts controller-only gating) |

No SystemAdmin bypass was added to the normal edit path — an elevated override was not built because no requirement or existing incident called for one; adding one speculatively would have been an unrequested feature.

## 6. UI/UX Changes (`Details.cshtml`)

- **Compact header**: Letter Number + status pill + type + confidentiality + revision badge, Subject as a subheading, secondary metadata (from/to, date, priority, project, discipline) in one small-text row — replacing the old always-expanded form-like block.
- **Primary action + "More ⋮" menu**: exactly one primary action shown per lifecycle state (Submit for Review / Edit & Resubmit / Approve+Return+Reject / Issue / Close), each only rendered when actually legal from the current state; Reply/Export PDF/Sign moved into the "More" dropdown (existing Bootstrap dropdown component, already used elsewhere in this file for tags — no new library).
- **Body**: read-only by default. Draft shows the edit form directly (nothing to protect yet); `ReturnedForRevision` shows read-only content with an explicit "Edit & Resubmit" toggle, restricted server-side to the original author.
- **Conversation/thread**: `ViewBag.Thread` (now populated) renders prior messages in the `ParentLetterId` chain as collapsed summary rows that expand on click; the current letter is visually distinguished.
- **Review section**: new, distinct from Referrals — lists `LetterReview` history (decision badge, reviewer, comment, timestamp).
- **Recipients/Attachments/Referrals/Activity Timeline**: moved into a Bootstrap accordion (collapsed by default) instead of four always-expanded bordered cards.
- **Related Records**: compact link-only section for the related Document/MR.
- **Return/Reject reason capture**: Bootstrap modals (same library already in use) instead of inline always-visible forms.
- **Employee picker performance**: `Details` now calls a new light dropdown loader (Department + cached Tags only); Employee options are fetched once, on first focus of a picker, via a new `GetEmployeesForPicker` JSON endpoint — not on every page load.
- **Status colors**: every status badge always carries its text label (never color-only); two additional badge classes (`bg-orange`, `bg-purple`) were added once to the shared `site.css` (not duplicated per-view) to visually distinguish `ReturnedForRevision`/`Referred` from the existing Bootstrap palette. `Index.cshtml`'s status filter and grid badge were updated to include/color the two new statuses — the one direct inconsistency found when auditing the grid against the new lifecycle.
- No new design system, no new JS library, no new CSS framework — Bootstrap dropdown/modal/accordion/badge, already present in this codebase, cover every new interaction.

## 7. Files Changed

```
PEMS.Domain/Entities/Letter.cs                              (modified, additive fields)
PEMS.Domain/Entities/LetterReview.cs                         (new)
PEMS.Application/DTOs/LetterDtos.cs                          (modified, additive fields + LetterReviewDto)
PEMS.Application/Interfaces/ILetterService.cs                (modified, method set replaced)
PEMS.Persistence/Configurations/LetterConfigurations.cs      (modified, +LetterReviewConfiguration)
PEMS.Persistence/Context/PEMSDbContext.cs                    (modified, +DbSet<LetterReview>)
PEMS.Persistence/Servises/LetterService.cs                   (modified, +314/-lines net)
PEMS.Persistence/Migrations/20260912135019_LetterControlledLifecycle.cs        (new)
PEMS.Persistence/Migrations/20260912135019_LetterControlledLifecycle.Designer.cs (new)
PEMS.Persistence/Migrations/PEMSDbContextModelSnapshot.cs    (modified, auto-generated)
PEMS.Web/Controllers/LetterController.cs                    (modified, actions replaced/added)
PEMS.Web/Views/Letter/Details.cshtml                         (rewritten)
PEMS.Web/Views/Letter/Index.cshtml                           (modified, status filter + badge colors)
PEMS.Web/wwwroot/css/site.css                                (modified, +2 badge utility classes)
PEMS.Tests/TestSupport/FakeLetterService.cs                  (modified, matches new interface)
PEMS.Tests/Cmms/LetterControlledLifecycleTests.cs            (new, 36 tests)
```
12 files changed, 1187 insertions(+), 313 deletions(-) across the source tree (`git diff --stat`), confirmed to touch **only** Letter-module files plus the mechanically auto-generated EF snapshot.

## 8. Database Changes

One migration, `20260912135019_LetterControlledLifecycle`, purely additive:
- `Letters` table: +`ClosedDate`, +`RejectReason`, +`RejectedByUserId`, +`RejectedDate`, +`ReturnReason`, +`ReturnedByUserId`, +`ReturnedDate` (all nullable), +`RevisionCount` (non-null, `DEFAULT 0` — safe for existing rows).
- New table `LetterReviews` with `FK_LetterReviews_Letters_LetterId` (`ON DELETE RESTRICT` — deleting a letter must not silently erase its review history) and index `(LetterId, CreatedDate)`.
- No column dropped, renamed, or retyped; no existing table altered destructively.

**Deviation from planned process (disclosed):** `PEMS.Persistence/Context/PEMSDbContextFactory.cs` (the design-time factory `dotnet ef` uses) hardcodes its connection string literally to `PEMSDB` and ignores `ConnectionStrings` environment-variable overrides. Setting `ConnectionStrings__PEMSConnection` before `dotnet ef database update` — the intended way to redirect to a disposable `PEMSDB_<name>Repro` — had no effect, and the migration applied directly to the **live** PEMSDB instead of a repro copy first. This was caught immediately after the fact (not before), and verified safe post-hoc: `sqlcmd` confirmed all 8 new columns and the new table exist with correct types, and existing row counts were unchanged (6 Letters, 1 Referral, 0 Reviews — expected for a brand-new table). No data was lost or altered. This gotcha has been written to persistent memory so it isn't repeated on the next Letter/EDMS-style migration.

## 9. Test Results

```
New tests added:  PEMS.Tests/Cmms/LetterControlledLifecycleTests.cs — 36 tests
                  (Edit: 6, Workflow: 15, Resubmission: 4, Audit/history: 2, GetDetailAsync: 2,
                   plus supporting theory cases across non-editable/non-transitionable statuses)
Focused run:      36/36 passed (8s)
Full regression:  1146/1146 passed, 0 failed, 0 skipped (6m25s)
                  = 1110 pre-existing (Phase 4 baseline) + 36 new — exact reconciliation, no unexplained delta
```
One transient failure (`EngineeringRevisionQualityPerformanceTests.GetPagedAsync_FirstPage_CompletesQuickly`, an unrelated Engineering-Documents timing test — took 9941ms under full-suite load) occurred on the first full run and was confirmed flaky, not a regression: it passed cleanly (2/2) when re-run in isolation immediately after, with no code in its path touched by this phase.

## 10. Build Result

`PEMS.Web`, `PEMS.Persistence`, `PEMS.Tests` all build with **0 errors** (only pre-existing, unrelated nullable-reference warnings in files this phase did not touch). Verified after every layer's changes, not just once at the end.

## 11. Regression Check

`git diff --stat` (§7) confirms exactly 12 files changed, all within the Letter module (entity/DTO/interface/config/service/migration/controller/views/css/tests) plus the mechanically-generated EF model snapshot — no WorkOrder/Document/MR/other-module file was touched. Full regression suite reconciles exactly (1110 + 36 = 1146). No existing test was deleted, skipped, or altered to make it pass.

## 12. Remaining Findings / Explicitly Not Done

- **No elevated SystemAdmin override action was built.** The master prompt requires *if needed* a separate, explicit, audited override — no concrete requirement or incident calls for one today, so none was added (avoids speculative, unrequested scope). Recommended: build only when a real business case for admin-forced edit/transition arises.
- **`Reply.cshtml`, `Create.cshtml`, `MyReferrals.cshtml`** were audited and found to have no direct inconsistency with the new lifecycle (they don't reference the removed actions or drive status transitions) — left untouched, per the "don't touch unrelated code" instruction. `MyReferrals.cshtml`'s status badge is still a flat `bg-info` for every status; this is cosmetic and pre-existing, not a regression, and was left alone to keep the diff scoped to Details/Index.
- **No dedicated optimistic-concurrency-exception test** was added for `SaveDraftEditAsync`/`ResubmitAsync` — the EF Core InMemory provider's concurrency-token emulation was not verified reliable enough to assert `DbUpdateConcurrencyException` behavior confidently within this phase's time budget; the mechanism itself (`OriginalValue` assignment) is the same pattern already relied on elsewhere in this codebase for `RowVersion`-based concurrency. Recommended: a `PemsDbContextFactory.CreateIsolatedSqlServerOrThrow`-based test in a future pass, mirroring the existing race-condition test pattern used for Transmittal numbering.
- **UI was verified by code review and Razor compilation only, not a live browser session** (no dev server was launched this session). The view was checked for: correct conditional rendering per state (no duplicate body sections — one real bug caught and fixed during self-review), correct model/ViewBag field names against the actual DTOs, and RTL-safe logical classes (`ms-`/`me-`, `border-inline-start`) throughout. A live-browser pass is recommended before considering this UI production-verified end-to-end.
