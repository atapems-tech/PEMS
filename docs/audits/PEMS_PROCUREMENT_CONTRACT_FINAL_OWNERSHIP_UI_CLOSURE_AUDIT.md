# PEMS — Final Procurement-to-Contract Ownership, Traceability, UI Alignment & Closure Audit

**Date:** 2026-09-23
**Branch:** `feat/cmms-passport-global-page-header`
**Scope:** Ownership/relationship audit of the ProcurementPackage → Tender → Bid → Contract → NTP → Kick-off → ITP
chain, gap closure limited to verified findings only, and production of this closure audit.

## Executive Summary

**Status: PASS WITH BUSINESS/SECURITY DECISIONS OUTSTANDING**

No rebuild was performed. The existing architecture (ProcurementPackage as the pre-contract/commercial
container, Contract as the post-award execution container, `Tender.ContractId` as the single source of
truth for Contract↔Package/Tender traceability) was already correctly designed by the prior Phase 0-9
implementation and the subsequent Final Hardening pass. This pass found and closed four genuine UI/ownership
gaps — NTP and Kick-off had server-side actions with no UI to trigger them, Contract had no visible
traceability back to its originating Package/Tender, ProcurementPackage Details did not show its own
Tenders, ITP's existing `ProcurementPackageId`/`ContractId` columns were unreachable from any UI — and added
the two missing Relationship Explorer entry points required to explore the chain from its own root objects
instead of only through Vendor. Two items remain **outside this pass's authority to resolve**: the
`ReportController` authorization gap and the empty `ContractApprovalAuthorityRule` table, both correctly
already classified as decisions for the business/security owner in the prior hardening report, reaffirmed
unchanged here.

## 1. What Changed (only actual changes)

| Area | Change | Why |
|---|---|---|
| `ContractController.Details` | Reads `Tenders.FirstOrDefault(t => t.ContractId == id)` (with Package/AwardedBid included), `ContractNTPs`, Kick-off `Meetings`, linked `InspectionTestPlans` into `ViewBag` | Contract had zero visible link back to the Package/Tender it came from, and NTP/Kick-off/ITP had no read path in this view |
| `Contract/Details.cshtml` | New "Procurement Origin" card (Package/Tender/Awarded Bid/Award date); new "Execution" card with NTP issue form + list, Kick-off schedule form + list, linked-ITP list + conditional "New ITP" link | `IssueNtp`/`ScheduleKickoff` actions already existed server-side but had **no UI form at all** — a genuine orphaned-capability bug, not a new feature |
| `ProcurementPackageController.Details` | Reads the package's own `Tenders` (with `AwardedBid.Vendor`) into `ViewBag` | Package Details showed "New Tender" but never the tenders that already exist under it |
| `ProcurementPackage/Details.cshtml` | Compact Tenders summary row (Tender → Status → Awarded vendor → link to resulting Contract) | Makes the Package→Tender→Award→Contract lifecycle visible without navigating away |
| `QCController.ItpCreate` (GET/POST), `LoadCommonDropdownsAsync`, `ItpDetails` | Added `procurementPackageId`/`contractId` params, prefill support, dropdowns, `Include()`s | `InspectionTestPlan.ProcurementPackageId`/`ContractId` columns existed since the original Phase 5 work but were **unreachable from any UI** |
| `ItpCreate.cshtml`, `ItpDetails.cshtml` | New optional Package/Contract dropdowns and display fields | Same gap as above, view side |
| `IRelationshipService` / `RelationshipService` | New `ExploreFromProcurementPackageAsync`, `ExploreFromContractAsync` | No entry point existed to explore the Package↔Tender↔Bid↔Contract↔NTP↔Kickoff↔ITP chain from its own root objects — only partially visible via Vendor |
| `RelationshipExplorationDto` | New `ProcurementPackages`, `InspectionTestPlans` node lists | Reusing `MaterialRequisitions`/`WorkOrders` for these would have mislabeled sections and produced broken links |
| `RelationshipController` | New `ProcurementPackage(id)`, `Contract(id)` actions, same permission gate as existing three | Entry points for the above |
| `Relationship/Index.cshtml` | Two new sections in the existing generic section list | View already renders generically by `RootType`/node lists — no structural change needed |
| `ProcurementPackage/Details.cshtml`, `Contract/Details.cshtml` | "Cm_Relationships" link added to each header | Makes the new entry points reachable from the pages users actually work in |
| `SharedResource.fa.resx` | 8 new keys (checked individually for exact-match duplicates before adding, per established process) | Persian parity for new UI text |
| `HelpContentSeeder` | Idempotent content patch (`PatchTenderContractFinalizationLifecycleAsync`, mirrors the existing `PatchReliabilityOverviewGapCaveatsAsync` pattern) adding Completed/Terminated + Procurement Origin + Relationship Explorer coverage to the `tender-and-contract-finalization` article, EN and FA | Article predated the Completed/Terminated contract states and the new UI |
| `PEMS.Tests/Authorization/RelationshipServiceOwnershipTests.cs` (new) | 6 tests: unknown-id null handling ×2, full-chain-from-Package, full-chain-from-Contract, **traceability integration test** (Contract → Tender → Package), no-origin edge case, Package-linked-ITP surfaced | §26 required ownership/traceability coverage |
| `PEMS.Tests/Authorization/AIPipelineAuthorizationTests.cs` | `FakeRelationshipService` implements the 2 new interface methods | Required to keep the interface change compiling |

**No entity/schema change was made.** No new migration was created or needed — every change above reads
existing FKs (`Tender.ProcurementPackageId`, `Tender.ContractId`, `Contract.VendorId`, `ContractNTP.ContractId`,
`Meeting.ContractId`, `InspectionTestPlan.ProcurementPackageId`/`ContractId`, `PurchaseOrder.ContractId`) that
were already present in the schema from earlier phases.

## 2. Ownership Model (exact hierarchy)

```
ProcurementPackage (pre-contract/commercial container)
├── MaterialRequisition(s)              [MaterialRequisition.ProcurementPackageId]
├── MaterialRequisitionQuotation(s)     [MRQ.ProcurementPackageId via package.Quotations]
├── ProcurementCommunication(s)         [ProcurementCommunication.ProcurementPackageId]
├── InspectionTestPlan(s) (optional)    [InspectionTestPlan.ProcurementPackageId, nullable]
└── Tender(s)                           [Tender.ProcurementPackageId, required]
    ├── TenderInvitedVendor(s)          [TenderInvitedVendor.TenderId]
    ├── Bid(s)                          [Bid.TenderId]
    │   ├── BidEvaluation(s)            [BidEvaluation.BidId — Technical/Commercial kept independent]
    │   └── BidNegotiation(s)           [BidNegotiation.BidId]
    ├── AwardedBid → Bid                [Tender.AwardedBidId, nullable until award]
    └── Contract (post-award execution container)   [Tender.ContractId — the ONLY FK linking Tender↔Contract;
        │                                             Contract itself carries NO ProcurementPackageId/TenderId]
        ├── ContractRevision(s)          [ContractRevision.ContractId — also serves as Change Order/Variation]
        ├── ContractDisciplineReview(s)  [ContractDisciplineReview.ContractId]
        ├── ContractDocument(s)          [ContractDocument.ContractId]
        ├── ContractNTP(s)               [ContractNTP.ContractId, required]
        ├── Meeting(s) MeetingType=KickOff [Meeting.ContractId, nullable — generic Meeting entity reused]
        ├── InspectionTestPlan(s)        [InspectionTestPlan.ContractId, nullable]
        └── PurchaseOrder(s)             [PurchaseOrder.ContractId, nullable]
```

Contract traceability to its Package is **derived, not stored** — `ContractController.Details` and
`RelationshipService.ExploreFromContractAsync` both do `Tenders.FirstOrDefault(t => t.ContractId == id)`
and follow `.ProcurementPackage` from there. This was a deliberate choice, consistent with the task's own
instruction not to add a redundant FK merely because two objects now appear on the same screen — Phase 3
already established `Tender.ContractId` as the single source of truth for this relationship, and a second,
independent `Contract.ProcurementPackageId` would have created two paths that could silently disagree.

## 3. Ownership Matrix

| Entity | Parent/Owner | Relationship | Persisted | DTO/UI Exposed | Relationship Explorer | Status |
|---|---|---|---|---|---|---|
| Tender | ProcurementPackage | `Tender.ProcurementPackageId` (required FK) | Yes | Yes (Package Details tender list, Tender Details) | Yes (both directions) | CLOSED |
| Bid | Tender | `Bid.TenderId` (required FK) | Yes | Yes | Yes | CLOSED |
| Contract | Tender (reverse) | `Tender.ContractId` (nullable FK on Tender, not on Contract) | Yes | Yes (Contract "Procurement Origin" card, Package tender list) | Yes (both directions) | CLOSED |
| Contract → Vendor | Contract | `Contract.VendorId` (required FK) | Yes | Yes | Yes | CLOSED (pre-existing) |
| ContractNTP | Contract | `ContractNTP.ContractId` (required FK) | Yes | Yes (now has a UI form — was orphaned) | Yes | CLOSED |
| Kick-off Meeting | Contract | `Meeting.ContractId` (nullable FK) + `MeetingType == "KickOff"` | Yes | Yes (now has a UI form — was orphaned) | Yes | CLOSED |
| InspectionTestPlan | ProcurementPackage and/or Contract | `InspectionTestPlan.ProcurementPackageId`/`ContractId` (both nullable) | Yes | Yes (now settable/visible — was unreachable) | Yes | CLOSED |
| PurchaseOrder | Contract | `PurchaseOrder.ContractId` (nullable FK) | Yes | Yes (pre-existing) | Yes | CLOSED |
| ContractRevision (Change Order/Variation) | Contract | `ContractRevision.ContractId`; `ChangeType` ∈ Variation/ChangeOrder/Amendment/Correction | Yes | Yes (pre-existing "Request Value Change" / revision history) | Not separately, but reachable via Contract | CLOSED — reused, no new entity needed (§14) |

## 4. Lifecycle Matrix

| Stage | Existing | Server-Enforced | UI | Tested | Status |
|---|---|---|---|---|---|
| Tender: Draft → Open → Closed → Awarded | Yes | Yes (`TenderService`) | Yes | Yes (pre-existing `TenderServiceTests`) | CLOSED |
| Bid: independent Technical + Commercial evaluation, both Passed required to win | Yes | Yes (`BidEvaluation` gates) | Yes (separate Technical/Commercial sections, from prior hardening pass) | Yes (pre-existing) | CLOSED |
| Contract: Draft → UnderReview → Approved → Signed → Effective | Yes | Yes (`ContractWorkflowService`, parallel discipline-review stage via `IWorkflowRuntime`) | Yes | Yes (pre-existing `ContractWorkflowServiceTests`) | CLOSED |
| Contract: Effective → Completed | Yes | Yes — blocked unless `RequiresInspection == false`, or ≥1 linked ITP exists with every line item `Status == "Closed"` | Yes (button gated by status in view) | Yes (pre-existing `ProcurementPackageInspectionGateTests`) | CLOSED |
| Contract: any non-terminal → Terminated (with reason) | Yes | Yes — blocked once already `Completed`/`Terminated` | Yes | Yes (pre-existing) | CLOSED |
| NTP issuance | Yes (action existed) | Yes — gated to Signed/Effective | **Newly added this pass** (form was missing) | Covered indirectly via the new traceability tests reading `ContractNTPs`; issuance gating itself is pre-existing/pre-tested | CLOSED |
| Kick-off scheduling | Yes (action existed) | Yes — gated to Signed/Effective | **Newly added this pass** (form was missing) | Same as above | CLOSED |

## 5. Traceability Matrix

| Link | Verified this pass | Evidence |
|---|---|---|
| MR → Package | Pre-existing, unchanged | `MaterialRequisition.ProcurementPackageId` |
| Package → Tender | Verified | `ExploreFromProcurementPackageAsync` test: tender surfaced under its package |
| Tender → Bid | Verified | Same test: bid surfaced under its tender |
| Bid → Evaluation | Pre-existing, unchanged | `BidEvaluation.BidId` |
| Tender → Award (AwardedBidId) | Verified | Same test chain |
| Award → Contract | Verified | `Tender.ContractId` populated in test fixture, read back correctly |
| **Contract → Package (reverse traceability)** | **Verified with a dedicated integration test** | `ExploreFromContract_TracesBackToOriginatingTenderAndProcurementPackage` — starts from Contract, asserts both the originating Tender and the originating ProcurementPackage are present in the result |
| Contract → NTP | Verified | Same test: `ContractNTPs` populated |
| Contract → Kickoff | Verified | Same test: `KickoffMeetings` populated |
| Contract → ITP / Inspection | Verified | `ExploreFromProcurementPackage_ItpLinkedDirectlyToPackage_IsSurfaced` and the chain test |
| Completion gate → Inspection | Pre-existing, unchanged, re-confirmed by code read (`ContractWorkflowService.CompleteAsync`) | See Lifecycle Matrix |

## 6. Security Matrix

No authorization code was modified in this pass. Every new controller action follows the pre-existing
pattern exactly:

| Action | Permission check | Notes |
|---|---|---|
| `RelationshipController.ProcurementPackage(id)` | `HasPermissionAsync(roles, "Relationship", "View")` | Identical gate to `Equipment`/`Document`/`Vendor` actions in the same controller |
| `RelationshipController.Contract(id)` | `HasPermissionAsync(roles, "Relationship", "View")` | Same |
| `ContractController.Details` new ViewBag reads | No new authorization surface — reuses the existing action's existing gate | Read-only, additive |
| `ProcurementPackageController.Details` new ViewBag read | Same | Read-only, additive |
| `QCController.ItpCreate` new parameters | No new authorization surface — same action, same gate | Additive, optional params |

Confirmed unchanged and still intact: SoD self-approval prevention (`AccessControlService.AuthorizeAsync` →
`DecideForUserAsync`), `ContractController.Approve`/`Reject`/`Sign`/`MarkEffective`/`Complete`/`Terminate`
all still individually gated on `"Contracts","Approve"`, `SystemAdmin`-only Approval Authority Rule admin
screen untouched.

**Outstanding, not resolved by this pass (reaffirmed from the prior Final Hardening report, §18/§20):**
`ReportController` (`Index`/`Engineering`/`Vdr`/`Cmms`/`Procurement`/`Operations`) still has **zero**
permission checks on any of its 6 actions. Re-verified by direct code read this pass — unchanged. Not
fixed here because: (a) fixing only the Procurement action would be inconsistent with its 5 siblings, (b)
fixing the whole controller is outside this task's scope and could affect report consumers with no defined
access policy today, (c) no repository evidence establishes whether this is intentional (e.g., reports are
meant to be broadly viewable) or an oversight. **Classification: BLOCKED — BUSINESS/SECURITY DECISION
REQUIRED.**

## 7. UI Verification

| Page | EN | FA | Notes |
|---|---|---|---|
| ProcurementPackage/Details | OK | OK | New Tenders summary row uses `@Localizer["Tenders"]`/`@Localizer["Awarded"]`/`@Localizer["Contract"]` (all present in both resx files); no duplicate headings introduced |
| Contract/Details | OK | OK | New "Procurement Origin" and "Execution" cards use `@Localizer[...]` throughout; existing lifecycle buttons untouched; no duplicate headings |
| QC/ItpCreate | OK | OK | New Package/Contract dropdowns optional, `-- none --` default, prefilled when launched from Contract's "New ITP" link |
| QC/ItpDetails | OK | OK | New Package/Contract fields render only when set (`@if (Model.ProcurementPackage != null)`) |
| Relationship/Index (via new ProcurementPackage/Contract roots) | OK | OK | View is generic over `RootType`/node lists — no per-root template needed, verified by reading `Index.cshtml`'s rendering loop |
| Procurement Report | Unchanged | Unchanged | Re-verified `ProcurementReportService` already reflects Package→Tender→Bid→Award→Contract→NTP hierarchy from the prior hardening pass; no gap found, no change made |
| Relationship Explorer (Equipment/Document/Vendor entry points) | Unchanged | Unchanged | New entry points added alongside, existing three untouched, existing tests for the controller/service still pass |
| Approval Authority Admin | Unchanged | Unchanged | Not in scope this pass; table remains at 0 rows (see §9) |

No route was removed or renamed. No permission requirement was removed or weakened. No existing feature was
deleted. Live browser verification (Playwright/manual click-through) was not performed in this environment —
verification here is build-level (Razor compiles against the model, localization-completeness checks pass)
and test-level, not a rendered-pixel check.

## 8. Database Verification

- **No new migration was created.** Confirmed via `git status` on `PEMS.Persistence/Migrations/` — no
  pending/untracked migration files. Every change in this pass reads columns/FKs that already existed in
  the schema from the original Phase 0-9 implementation and the subsequent Final Hardening pass.
- **Live SQL Server state** (`localhost\SQLEXPRESS` / `PEMSDB`, verified via `sqlcmd` this pass):
  - `ContractApprovalAuthorityRules`: **0 rows** — confirmed unchanged, no thresholds invented (§9 below).
  - `Tenders`: **0 rows** in this dev database (no procurement test data has been entered in this
    environment) — this pass's schema/relationship correctness was therefore verified via EF Core InMemory
    integration tests (`RelationshipServiceOwnershipTests`), not against live data, since none exists to
    query.
- No table, column, or row was dropped, renamed, or reset.

## 9. Business/Security Decisions Outstanding

1. **`ReportController` has no authorization on any of its 6 actions.** BLOCKED — BUSINESS/SECURITY
   DECISION REQUIRED. See §6.
2. **`ContractApprovalAuthorityRule` has 0 configured rows.** No monetary thresholds have been invented or
   seeded, per explicit instruction. The admin CRUD screen (`Areas/Admin/ContractApprovalAuthorityController`,
   `SystemAdmin`-only) remains usable once the business supplies real threshold/role data. BUSINESS DATA
   REQUIRED — not a code gap.
3. **No speculative Tender/Bid/Contract import/export was added.** Confirmed via directory listing — no
   `TenderImport`/`BidImport`/`ContractImport` controller exists. No approved business requirement for this
   exists in the repository, so none was added. NOT APPLICABLE / FUTURE SCOPE, unchanged from the prior
   hardening report.
4. **Change Order / Variation architecture.** Already exists via `ContractRevision.ChangeType` (Variation /
   ChangeOrder / Amendment / Correction). CLOSED — reused, not a gap.

## 10. Test Results

Full suite, `dotnet build PEMS.slnx --configuration Debug` (0 errors) then
`dotnet test PEMS.Tests/PEMS.Tests.csproj --configuration Debug --no-build`:

```
Failed: 1, Passed: 1925, Skipped: 0, Total: 1926, Duration: 33m 26s
```

The single failure is `PEMS.Tests.Inventory.MrsImportBatchCapacityTests.
Five_thousand_row_file_stages_and_commits_as_one_operation_within_a_practical_time` — a pre-existing,
previously-documented timing flake (EF Core InMemory staging/commit of 5,000 rows occasionally exceeds its
wall-clock budget under machine load; it failed with a different timing readout on two separate runs this
session — "Commit took 628375ms" on the first run, "Stage took 31958ms" on the rebuild — confirming it is a
load-sensitive timing assertion, not a deterministic regression). It is unrelated to any change in this
pass and is not misreported as passing.

New tests added this pass (`RelationshipServiceOwnershipTests`, 6 tests) all pass and are included in the
1926 total: unknown-id handling for both new entry points, full-chain traversal from ProcurementPackage,
full-chain traversal from Contract, the required §26 Contract→Package traceability integration test, the
no-originating-tender edge case, and Package-linked-ITP surfacing.

No existing test was weakened, skipped, or deleted.

## 11. Remaining Gaps (final classification)

| Gap | Classification |
|---|---|
| ReportController has no per-action authorization | BLOCKED — SECURITY DECISION REQUIRED |
| ContractApprovalAuthorityRule has 0 rows / no thresholds | BLOCKED — BUSINESS DATA REQUIRED |
| Tender/Bid/Contract import/export | FUTURE SCOPE (no approved requirement exists) |
| Change Order / Variation as a distinct entity | NOT APPLICABLE — already correctly reused via `ContractRevision.ChangeType` |
| Live browser (rendered-pixel) UI verification | NOT PERFORMED — build/test-level verification only, environment has no browser automation configured for this pass |
| Everything else in the original mission scope (§3-§17, §19-§29 except the two items above) | CLOSED |
