# PEMS — Package Procurement Contract Finalization — Implementation Progress

Baseline: `Package Procurement Contract Finalization — Gap Analysis` (audit artifact, 2026-09-23).
Execution mode: autonomous sequential (master gap-closure prompt), no per-phase approval gate.
This record is updated as each phase completes and is the resume point if execution is interrupted.

---

## Phase 0 — Baseline and Pre-flight

**Status:** DONE

- Verified repository state matches the audit's baseline (`git status --short`, no material drift).
- Killed a stray `PEMS.Web.exe` process that would have locked EF migration output (established gotcha from the parallel Material Management workstream).
- Confirmed the `docs/audits/PEMS_*.md` naming convention and created this progress record accordingly.

## Phase 1 — Domain and Data Foundation

**Status:** DONE

**Files Changed (new):**
- `PEMS.Domain/Entities/VendorQualificationHistory.cs`, `ContractRevision.cs`, `ContractDisciplineReview.cs`, `Tender.cs`, `TenderInvitedVendor.cs`, `Bid.cs`, `BidEvaluation.cs`, `BidNegotiation.cs`, `ContractNTP.cs`
- `PEMS.Persistence/Configurations/TenderConfigurations.cs`

**Files Changed (extended):**
- `Vendor.cs` — QualificationStatus/Category/EffectiveDate/ExpiryDate/EvidenceReference/ReviewedBy/Notes + `QualificationHistory` collection
- `Contract.cs` — CurrentValue (separate from immutable `ContractValue`), ApprovedDate/By, SignedDate/By, EffectiveDate, `Revisions`/`DisciplineReviews` collections
- `ProcurementPackage.cs` — CriticalityClass/RiskLevel (reuses `EquipmentCriticality` vocabulary), RequiresInspection/InspectionNotes
- `Meeting.cs` — MeetingType, optional ContractId/ProcurementPackageId (Kick-off wiring, no new entity)
- `PEMS.Persistence/Identity/SystemRoles.cs` — added `Legal`, `Finance`, `HSE` roles (picked up automatically by `IdentitySeeder`, which iterates `SystemRoles.All`)
- `PEMS.Persistence/Configurations/ContractConfiguration.cs` — new FK/precision config for the above

**Migrations:**
- `20260923080004_AddProcurementContractFinalization` — 9 new tables, 21 new columns, 8 FKs, 29 indexes. Verified `Up()` contains only `CreateTable`/`AddColumn`/`AddForeignKey`/`CreateIndex` (zero `DropTable`/`DropColumn`/`AlterColumn`).
- `20260923084317_AddTenderContractLink` — `Tender.ContractId` nullable FK (added during Phase 3 once the Winner Selection → Contract Draft link was designed).

**Database Verification:** Applied to `PEMSDB` on `localhost\SQLEXPRESS`. Pre-migration row counts (517 Vendors, 0 Contracts, 2 ProcurementPackages) unchanged after migration.

**Build:** Full solution builds clean (0 errors).

## Phase 2 — Workflow and Security / SoD

**Status:** DONE

**The live gap fixed:** `ContractManager` held Create+Edit+Approve on both `Contract` and `ProcurementPackage` with no SoD block, and `AccessControlService.DecideForUserAsync`'s SoD check was structurally unreachable because both controllers called the role-only `AuthorizeForRolesAsync` path.

**Files Changed:**
- `PEMS.Persistence/Seed/ContractWorkflowSeeder.cs` (new) — `IWorkflowRuntime`-based two-stage workflow: `DisciplineReview` (Parallel, dynamic participant roles, `AllApprovalsRequired` — same pattern as the existing IDC stage) → `FinalApproval` (Sequential, ContractManager/EngineeringManager)
- `PEMS.Application/Interfaces/IContractWorkflowService.cs` + `PEMS.Persistence/Servises/ContractWorkflowService.cs` (new) — replaces `ContractService.ChangeStatusAsync`'s free-text status setter for all real lifecycle transitions
- `PEMS.Web/Controllers/ContractController.cs` — rewritten: inline permission checks (matching `ProcurementPackageController`'s testable pattern, not `[RequirePermission]` which only runs through the MVC filter pipeline), new actions (SubmitForReview/RecordDisciplineReview/Approve/Reject/Sign/MarkEffective/RequestRevision/ApproveRevision), `ChangeStatus` now rejects any status string except the one guarded transition it still supports (`UnderReview`)
- `PEMS.Web/Controllers/ProcurementPackageController.cs` — `AddApproval` and `Create` now call the resource-aware `AuthorizeAsync` path so SoD is actually evaluated; `Create` had no permission check at all before this
- `PEMS.Persistence/Seed/AccessControlSeeder.cs` — 3 new `SoDPolicy` rows (`Contracts.Create/Edit ↔ Approve`, `ProcurementPackage.Create ↔ Approve`)
- `PEMS.Persistence/Servises/PermissionService.cs` — Legal/Finance/HSE/QAManager granted `Contracts.Edit` (discipline-review capability; each user can still only record their own discipline's decision, enforced in `ContractWorkflowService`)
- `PEMS.Web/Views/Contract/Details.cshtml` — rewritten: removed the free-text status `<select>` (the exact vulnerable pattern the audit flagged), added guarded action buttons, discipline-review table, revision-history table

**Tests:** `ContractWorkflowServiceTests` (14), `ContractControllerAuthorizationTests` (6), 2 new SoD tests in `ProcurementPackageControllerAuthorizationTests`. All 29 passing, including both self-approval-blocked tests (proven against the real `AccessControlService`, not a mock).

**Build:** Full solution builds clean.

**Regression:** Ran after Phase 1 (1847/1848 passed — 1 pre-existing, documented MRS timing flake, unrelated). Phase 2 regression run in progress at time of writing this record; see next update.

## Phase 3 — Tender / RFQ / Bid / Evaluation

**Status:** DONE (service layer + tests); UI deferred to Phase 7 per the phase plan

**Files Changed (new):**
- `PEMS.Application/Interfaces/ITenderService.cs` + `PEMS.Persistence/Servises/TenderService.cs` — full Tender→Invite→Open→Bid→Evaluate→Negotiate→Award→Contract chain
- `PEMS.Application/Interfaces/IVendorQualificationService.cs` + `PEMS.Persistence/Servises/VendorQualificationService.cs` — Phase 1 built the qualification data model with no service to actually change it; this closes that gap
- `PEMS.Web/Controllers/VendorController.cs` — `SetQualification` action
- `PEMS.Tests/Authorization/TenderServiceTests.cs` (9 tests)

**Business rules enforced in code (not just documented):**
- Bid immutability: `Bid.OriginalValue` is written once in `SubmitBidAsync` and never touched again; negotiation only ever updates `NegotiatedValue`
- Technical ≠ Commercial: `SelectWinnerAsync` requires an independent `Passed` `BidEvaluation` row of each type — one does not satisfy the other
- Vendor qualification gate: `InviteVendorAsync`/`SubmitBidAsync`/`SelectWinnerAsync` all check `TenderInvitedVendor.QualificationConfirmed`, a snapshot taken at invite time (not a live reference that could be gamed by a later status change)
- Winner Selection → Contract Draft: `CreateContractFromWinningBidAsync` requires `Tender.Status == "Awarded"`, creates the Contract with the winning bid's `FinalValue`, and links back via `Tender.ContractId`

**Numbering:** `Tender`, `Bid`, `NTP` series added to `NumberingService.Defaults` (NTP deliberately not named "IRN" — see Risk 9 in the audit).

**Migration:** `Tender.ContractId` added via `20260923084317_AddTenderContractLink`.

**Not yet done in Phase 3:** Tender/Bid controller + views (UI is explicitly Phase 7 in the master prompt's own sequencing); Help content (Phase 8).

## Phase 4 — Contract Finalization (partial)

**Status:** DONE (architecture + gate); admin UI for configuring rules deferred to Phase 7

- `ContractApprovalAuthorityRule` (new entity, migration `20260923092414_AddContractApprovalMatrix`) — Min/Max value + optional CriticalityClass + RequiredApproverRole. Zero rows seeded (no invented thresholds, per master prompt §3.3/§4.4). `ContractWorkflowService.ApproveAsync` only consults it when at least one active rule exists; with none configured, behavior is unchanged (existing role-gated approval). Once configured, an unmatched value/criticality combination fails closed.
- `Contract.CriticalityClass` added (same migration) so the matrix works for contracts created directly, not only via Tender/Package.
- `ContractDocument` (previously scaffolded, zero references anywhere) now has a real create path: `ContractController.AddDocument` + a section on the Details page.

## Phase 5 — Inspection Control Integration

**Status:** DONE

- `InspectionTestPlan` extended with nullable `ProcurementPackageId`/`ContractId` (mirrors the existing nullable `EquipmentId` pattern), migration `20260923092728_AddItpPackageContractScope`.
- `ProcurementPackageService.CloseoutAsync` now has a real server-side gate: if `package.RequiresInspection`, closeout is blocked (with a specific error, not a generic 500) unless every linked ITP has `Status == "Closed"`. Packages with `RequiresInspection = false` (the default) are unaffected — verified no regression on existing Closeout tests.

## Phase 6 — NTP and Kick-off

**Status:** DONE (service + controller actions; dedicated NTP/Kickoff list views deferred to Phase 7)

- `ContractWorkflowService.IssueNtpAsync` — gated on `Contract.Status ∈ {Signed, Effective}`, blocks a second active NTP per contract, numbered via the `NTP` series.
- `ContractWorkflowService.ScheduleKickoffMeetingAsync` — same gate, reuses the existing `Meeting` entity (via the `MeetingType`/`ContractId` fields added in Phase 1) rather than a new `KickOffMeeting` entity.
- Both wired into `ContractController` (`IssueNtp`, `ScheduleKickoff` actions).

## Regression-fix cycle (after Phases 4-6 landed)

A full-suite run surfaced 10 failures beyond the pre-existing MRS timing flake; all diagnosed and fixed:
- **8× `TenderServiceTests`** — root cause: `NumberingService`'s atomic `UPDATE...OUTPUT` requires a real transaction, which EF Core's InMemory provider doesn't support (this is documented in `PemsDbContextFactory`'s own XML comments). Fixed by adding `PEMS.Tests/TestSupport/FakeNumberingService.cs` and using it in all InMemory-backed tests that touch numbering (`TenderServiceTests`, `ContractWorkflowServiceTests`, `ContractControllerAuthorizationTests`).
- **1× `LanguagePurityTests.HardCodedUiStrings_InViews_NeverGrowBeyondTheBaseline`** — the new Contract Details.cshtml sections (Phase 2/4 UI) added un-localized English literals. Fixed by wrapping every new string in `@Localizer[...]`, reusing 9 already-existing resx keys and adding 18 new ones to `SharedResource.fa.resx`.
- **4× `PermissionSeedHardeningTests`** (already noted above) — legitimate baseline update, not a bug.

Full suite rerun after fixes: **CLEAN** — 1878/1879 passed, only the pre-existing MRS timing flake remains. Phases 0-6 fully verified.

## Phase 7 — UI / UX (partial, the two explicitly-named gaps)

**Status:** DONE for Tender/Bid and PurchaseOrder; Relationship Explorer / Procurement report / approval-matrix admin UI deferred

- `TenderController` + Index/Details/Create views — full invite/open/bid/evaluate/negotiate/award/create-contract flow
- `PurchaseOrderController` + Index/Details/Create views — the audit's explicitly named "entity/service exist, zero UI" gap
- Sidebar navigation, permission modules (`Tender`, `PurchaseOrder`), full bilingual localization (28 new resx keys across both areas)
- 13 new authorization tests + 1 inspection-gate test file (4 tests) closing a Phase 5 coverage gap

## Phase 8 — Help

**Status:** DONE

- `tender-and-contract-finalization` bilingual article added to `HelpContentSeeder` (order 18, engineering category), covering the full lifecycle. Article count test updated 59→60.

## Phase 9 — Test & Closure Audit

**Status:** DONE — see the published closure report: https://claude.ai/artifact/513eYPsmsrCHa7ec5Ksv4V

Final regression: 1889/1890 passed, only the pre-existing MRS timing flake. Database verified directly against the live instance. Full requirement-closure matrix against the original audit's Process Coverage Matrix, 15 Business Rules, and 10 Risks is in the closure report, not duplicated here.

## Final Hardening Pass (post-closure-report)

Closed all 6 gaps the closure report had documented as limitations:

- **Gap 1 — Contract completion lifecycle**: confirmed from the pre-existing Status comment vocabulary ("Draft, Active, Completed, Terminated" — present before this session touched the file) that a post-Effective state was already intended, not invented. Added `Effective→Completed` (inspection-gated, mirrors the Package gate) and `→Terminated`. Migration `AddContractCompletionLifecycle`. 8 new tests.
- **Gap 2 — Commercial/Contractual clarification**: extended `ProcurementCommunication` (now inherits `BaseEntity` for audit trail) with new Type values and a real append-only `ProcurementCommunicationResponse` history table fixing the response-overwrite bug. Wired into Tender Details. Migration `AddProcurementClarificationHardening`. 22 tests (3 new + 19 existing rerun).
- **Gap 3 — Approval-authority administration**: new `Admin/ContractApprovalAuthority` screen (SystemAdmin-only, matches `RoutingRulesController`'s pattern), plus a `Priority` field the original architecture lacked for disambiguating overlapping rules. Migration `AddContractApprovalRulePriority`. Also closed a real gap: the matrix gate had zero tests until this pass — 6 new gate tests + 6 CRUD/validation tests.
- **Gap 4 — Relationship Explorer**: extended `ExploreFromVendorAsync`/`RelationshipExplorationDto` with Tenders/Bids/NTPs/Kick-off meetings. No migration needed, no second explorer.
- **Gap 5 — Procurement reporting**: extended `ProcurementReportDto` with Tender/Bid/Contract/PO/NTP counts, added a paginated `GetPipelineAsync` projection query for the full lifecycle table. **Flagged, not fixed**: `ReportController` has zero authorization on any of its ~6 report actions, not just Procurement — this is a pre-existing, uniform condition; adding auth to only this one action would be inconsistent, and broadening it to the whole controller is outside this gap's scope. Documented as a business/architecture decision, not guessed at.
- **Gap 6 — Negative test hardening**: reviewed the master prompt's explicit list against existing coverage; found and closed the one real gap (`SelectWinner_WithOnlyCommercialPassed_IsRejected`). Everything else in the list was already covered.

**Self-caught bug during this pass**: added a duplicate `<data name="Inactive">` key to `SharedResource.fa.resx` (Gap 3), which broke 3 tests that load the resx into a `Dictionary` (`ToDictionary` throws on duplicate keys). Found via the regression run, root-caused, fixed by removing the duplicate (identical translation already existed at line 134). All 3 tests confirmed passing after the fix.

**Import/Export (master prompt §10)**: inspected `ImportCatalog.cs` — no procurement/contract/vendor import registered, and no business requirement was ever stated for Tender/Bid import/export. Documented as **Not currently required**, nothing implemented — per the explicit instruction not to add speculative functionality.

---

## Known Exceptions / Deferred Decisions

- No monetary approval-threshold values invented (master prompt §3.3) — the approval-matrix architecture (Phase 4.4) will be built as configuration with no seeded thresholds, per the explicit instruction to fail closed rather than invent a business value.
- Discipline-review required-role set (`Engineering`/`TechnicalInspection`/`Legal`/`Finance`/`Procurement`/`HSE`) is a code-level default (`ContractWorkflowService.RequiredDisciplines`), mirroring the existing `ProcurementPackageService.StageOrder` configuration pattern — not a per-contract database setting. Revisit if a future requirement needs it to vary per contract.
