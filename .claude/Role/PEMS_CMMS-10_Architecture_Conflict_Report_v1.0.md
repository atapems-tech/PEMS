# CMMS-10 — Architecture Conflict Report

**Produced per:** `PEMS_CMMS-10_Permit_to_Work_Architecture_Baseline_v1.0.md` §35 (Mandatory Pre-Implementation Audit)
**Status:** Pre-implementation — no CMMS-10 code has been written. This report is the required gate before any domain modeling or coding begins.
**Method:** Direct repository inspection (no assumptions) across the 12 areas §35 lists, using the same evidence-first approach applied to CMMS-00 through CMMS-09.

---

## 0. Headline Finding

Every prior phase this session (CMMS-06 through CMMS-09) discovered that the needed domain schema **already existed**, fully designed but unwired — the job was wiring, not invention. **CMMS-10 is the opposite case.** An exhaustive grep of all 138 files in `PEMS.Domain/Entities/*.cs` for Permit/Isolation/LOTO/GasTest/SIMOPS/ShiftHandover/RiskAssessment/JSA/HSE-related terms found **zero matching entities** — this domain is genuinely greenfield. The only existing artifact is the planning document itself (`PEMS_CMMS-10_Permit_to_Work_Architecture_Baseline_v1.0.md`, 1312 lines) — a design input, not implementation.

This changes the shape of the work: CMMS-00→09 were each a few hours of wiring against a known schema. CMMS-10 requires designing and building ~20 new domain objects, a multi-role approval matrix, a state machine with 20+ states, and five dashboards, largely from scratch. See §12 for a recommendation on decomposing this rather than attempting it as one phase.

---

## 1. Existing HSE / Permit-adjacent entities — none found

Grepped for `permit|isolat|lockout|tagout|gastest|simops|shifthandover|riskassess|jsa|tra|hse|confinedspace|hotwork|coldwork` across `PEMS.Domain/Entities/*.cs`. Only incidental false positives:

- `MOC.cs:51-53` — `RiskAssessmentText`, `RiskAssessedById`, `RiskAssessedDate` (Management-of-Change risk sign-off; unrelated domain, not reusable for work-permit risk assessment).
- `Vendor.cs:26` — unrelated `RiskAssessedDate` (vendor qualification context).
- `MaintenanceTask.cs:7` — a Farsi doc-comment example mentioning "isolate equipment" as a sample task step, not a field.

**No `Permit`, `PermitType`, `Isolation`, `LOTO`, `GasTest`, `SIMOPS`, `ShiftHandover`, `JSA`, `TRA` class exists.** Confirmed greenfield — proceed to design new entities, but see §5 and §7 for two real gaps that must be closed *before* those entities can be meaningfully authored.

---

## 2. WorkOrder lifecycle — no existing permit-gate hook point

`WorkOrderLifecycleService.AdvanceStatusAsync` (`PEMS.Persistence/Servises/WorkOrderLifecycleService.cs:35`) enforces a hardcoded `LegalTransitions` dictionary:

```
Open → Assigned, Cancelled, Rejected
Assigned → InProgress, OnHold, Cancelled
InProgress → Completed, OnHold, Cancelled
OnHold → InProgress, Cancelled
Completed → Closed
```

The `InProgress` transition (line 60) sets `StartDate = DateTime.Now` with **no other gate or extension point** — this is exactly where a "Permit required and Issued" check must intercept, and nothing does today. `IWorkOrderLifecycleService.AdvanceStatusAsync`'s signature has no permit-related parameters.

**Conflict:** Baseline §20 says *"Permit shall not exist without a valid Work Context"* and the Execution Readiness Gate (§27) must block execution when permit conditions aren't met. This requires a **real behavior change** to `AdvanceStatusAsync` (a permit-satisfied check before allowing `Assigned → InProgress`), not just additive columns. This is the single most invasive touch-point CMMS-10 has into existing CMMS-00→09 code, and per baseline §36 ("Claude shall NOT modify previous CMMS phases without documented architectural justification") — **this document is that justification**: the Execution Readiness Gate is an explicit, named CMMS-10 requirement, so this change is in-scope, but it must be additive (e.g., `bool? PermitSatisfied` check that no-ops when a WorkOrder has no associated Permit — see §37 baseline: "A WorkOrder may have: No Permit...").

---

## 3. Asset / FunctionalLocation hierarchy — flatter than the baseline assumes

Baseline §14 wants `Site → Plant → Area → Process Unit → Functional Location → Asset → WorkOrder → Permit`. The actual schema:

- **No `Site`/`Plant`/`Area`/`Unit` entities exist.** `FunctionalLocation` (`PEMS.Domain/Entities/FunctionalLocation.cs`) is a **self-referencing tree** (`ParentId`/`Children`) with `Plant`, `Area`, `Unit` as **plain nullable strings**, not FKs to governed tables.
- `Asset.FunctionalLocationRef` and `Equipment.FunctionalLocationRef` are real FKs into this tree (added CMMS-01).
- `FunctionalLocationService.cs` already provides `GetBreadcrumbAsync`, `GetPickerListAsync` (with cycle-detection) — **reuse these directly** for Permit location selection; do not rebuild tree-walk logic.

**Conflict:** none blocking — the existing tree can represent Site/Plant/Area/Unit informally via nested `FunctionalLocation` rows even without dedicated tables, and Permit can hang off `FunctionalLocationId`/`AssetId`/`EquipmentId` exactly as `WorkRequest` already does (three optional FKs, CMMS-03 pattern). Recommend **not** building dedicated Site/Plant/Unit tables for CMMS-10 — out of scope; use the existing tree.

---

## 4. Maintenance Master Data — reusable for Competency, with one gap

`Skill`/`EmployeeSkill`, `Qualification`/`EmployeeQualification`, `Certification`/`EmployeeCertification` (all in `PEMS.Domain/Entities/`) already carry exactly the shape baseline §23 wants (`Permit Type → Required Competency → Personnel → Validity`): each join table has `ExpiryDate`, `Issuer`, `EvidenceDocumentPath`, `IsActive`, and `Certification`'s due-status already reuses the same Overdue/DueSoon/OK logic as instrument calibration.

**Gap:** every one of these join tables keys on `EmployeeId` only. See §5.

---

## 5. Employee / Contractor structure — **critical gap, blocks §22-23 of baseline**

`Employee.cs` is a flat POCO with no `EmployeeType`/`IsContractor` discriminator. `Vendor.cs` represents a **company**, not personnel (single flat `ContactName`/`ContactEmail`, no personnel collection). Grepping "Contractor" across all entities returns only `WorkOrder.ContractorCost`/`ContractorName` (a cost field pair added in CMMS-08, not a personnel link) and an unrelated `ITPLineItem` hit.

**Contractor personnel cannot currently be represented anywhere in the schema** — no name, no competency record, no digital signature. This directly blocks:
- Baseline §22 (Contractor Integration: `Contractor → Personnel → Competency → Training → Authorization → Permit`)
- Baseline §23 (Competency Management for contractor personnel)
- Baseline §8 (Permit Issuer/Permit Receiver as distinct, potentially-contractor roles)
- The workflow engine's actor model (`WorkflowInstanceStage.AssignedToEmployeeId` is `int? → Employee`, so a contractor cannot be a workflow actor/approver today either — see §8 below)

**This must be resolved before Permit Issuer/Receiver/competency-validation entities can be authored.** Two options, to be decided with the user before design proceeds:
- (a) Add an `EmployeeType`/`PersonnelType` discriminator + `VendorId` (nullable) directly to `Employee`, relaxing it to represent both employees and contractor personnel — minimal schema change, all existing `EmployeeId` FKs (Skill/Qualification/Certification/WorkflowInstanceStage/etc.) become contractor-capable for free.
- (b) A new `ContractorPersonnel` entity mirroring `Employee`'s shape (including `SignaturePath`) with its own `VendorId` FK — cleaner separation, but requires either duplicating the Skill/Qualification/Certification join tables or generalizing them to a polymorphic actor reference, which is a much larger ripple.

**(a) is recommended** — it's additive, reuses every existing competency/workflow join table unchanged, and matches this session's consistent "extend, don't duplicate" discipline. This should be confirmed with the user as an explicit decision before any CMMS-10 entity design starts, since it touches the shared `Employee` entity used across the whole system.

---

## 6. Authorization — two systems, neither covers Permit's action vocabulary as-is

**a. Role arrays** (`SystemRoles.cs` — 9 roles: SystemAdmin, EngineeringManager, EngineeringSupervisor, DisciplineEngineer, DocumentController, MaintenanceEngineer, ContractManager, QAManager, Viewer). No Permit-relevant role exists (Work Supervisor, Senior Shift/Operations, Senior Maintenance Engineer, Senior Production Engineer, HSE/Permit Authority, Permit Issuer/Receiver — baseline §3 — are all new).

**b. Granular permission matrix** (`IPermissionService`/`PermissionService.cs`) — `HasPermissionAsync(roles, module, action)` where `action` is a hardcoded switch over exactly `View/Create/Edit/Delete/Approve` (+ a bolt-on `ManualNumber`). Baseline §25 needs 14 distinct permit actions (Create/Request/Assess/Approve/Issue/Receive/Suspend/Resume/Extend/Revoke/Close/VerifyIsolation/Deisolate/EmergencyOverride).

**Conflict:** the fixed-boolean-per-action model cannot express this action set without either (i) growing `RolePermission` with more boolean columns and more switch cases — invasive, and sets a bad precedent since every future module would keep growing this one row shape — or (ii) a generic `(RoleName, ModuleName, ActionName)` permission-row model, additive and scoped to Permit without touching the other 12 existing modules' schema. **(ii) is recommended**, introduced as a parallel table/mechanism rather than a retrofit of the existing one (matches baseline §36: don't modify prior phases without justification — this is additive, not a modification).

---

## 7. Workflow engine — genuinely reusable as the Approval Matrix, with scope caveats

`PEMS.WorkflowPlatform`/`IWorkflowRuntime` is a real, general-purpose, already-proven engine — in active use by **six** independent modules (EngineeringDocument, NCR, VDR, TechnicalQuery, MOC, WorkOrderReview), with its own doc-comment stating *"No module is allowed to implement its own approval engine."* It supports role-based multi-stage approval, 4 approval strategies (including MinimumCount/FirstResponse — useful for "any one authorized approver" permit scenarios), dynamic per-instance participant lists (already used by VDR for discipline-based routing — directly transferable to permit-type-based approver routing), and immutable history with actor/IP/timestamp capture.

**This should be reused as the Approval Matrix engine (baseline §7), not rebuilt.** Two caveats to design around:
1. The engine drives only the **approval sub-flow**. The full Permit lifecycle state machine (Draft→...→Closed, baseline §6, 20+ states) must be owned by the Permit module itself and mapped onto its own `Permit.Status` field — exactly the pattern `QCController` already uses for NCR (`_runtime.ExecuteActionAsync(...)` returns a decision; the calling controller updates its own business-status separately). `IWorkflowRuntime` should be invoked only for the discrete approval gates within the lifecycle (e.g. the "PendingApproval" stage), not asked to model Suspend/Resume/Extend/Emergency states.
2. Workflow actor identity is `int? → Employee` — reinforces §5: contractor personnel cannot be workflow actors until that gap is closed.
3. Workflow stage authorization (`ResponsibleRole`, matched against `SystemRoles`) and the granular permission matrix (§6) are two separate, currently-uncoordinated checks. Design should be explicit about which gates which action (e.g., stage-level actions go through `IWorkflowRuntime` role-matching; direct actions like Emergency Override that bypass the workflow entirely need `IPermissionService`-style gating instead).

---

## 8. Audit trail — reusable pattern, one real gap

The `{FromStatus, ToStatus, ChangedById→Employee, ChangedDate, Reason}` shape (`WorkOrderStatusHistory`, `WorkRequestStatusHistory`, `StoreRequisitionStatusHistory` — same shape, used three times already) is the established idiom and should be reused verbatim for `PermitStatusHistory`.

**Gap:** this pattern captures state-transition audit only (who/when/from/to/why) — it does **not** capture arbitrary field-level old-value/new-value diffs, which baseline §26 explicitly wants ("Old Value / New Value" per transition, e.g. isolation point changes, extension validity changes). If field-level diffing is required beyond status, it needs a supplementary mechanism (e.g. a generic `PermitFieldChangeHistory` row or a JSON diff column) — this is additive, not a conflict, just a gap to design for explicitly (baseline §18's Extension audit, in particular, wants Previous Validity/New Validity captured — this needs its own dedicated fields, not just the generic history pattern).

---

## 9. Document integration — clear precedent to follow

Documents use EF Table-Per-Type inheritance from a shared `Document` base (`EngineeringDocument`, `VDRItem`, `MaintenanceDocument` all derive from it) — **not** a generic polymorphic link table. Physical file attachments (`DocumentAttachment`) already follow a "one nullable FK per consuming module" pattern (`LetterId`, `MOCId`, `TechnicalQueryId` all exist side-by-side on the same table).

**Recommendation:** `PermitDocument` should be a new `Document`-derived subtype (same pattern as `MaintenanceDocument`), with `PermitId`/`Permit?`. If raw permit attachments (photos, signed copies) are needed, add a nullable `PermitId` column to the existing `DocumentAttachment` table, following the `LetterId`/`MOCId` precedent — do not build a second attachment table. No conflict; a clear, established pattern to extend.

---

## 10. Notifications — real channel exists, but no scheduler

`INotificationService` + `Notification` entity + a SignalR-based `INotificationBroadcaster` for live push already exist and work (currently called from only 2 places — `LetterReferralService`, `TaskService` — so it's lightly proven but real, not vaporware). `RelatedType`/`RelatedId` are already generic string+int, so `RelatedType="Permit"` needs no schema change.

**Gap:** there is **no background job / scheduled task infrastructure anywhere in the codebase**. Time-based alerts (Permit Near Expiry, Gas Test Expired — baseline §30) have nothing to trigger them proactively. Two paths: (a) add a new hosted background service (a first for this codebase — needs its own justification/design), or (b) compute these as dashboard-pull on page load, consistent with **every** other "Alert" feature built this session (Reliability, Inspection Alert→WorkRequest, KPI dashboard all compute on read, nothing pushes). **(b) is recommended** for the initial CMMS-10 slice, with (a) flagged as a legitimate future enhancement once a real scheduler is justified for the whole platform, not introduced solely for this one feature.

---

## 11. Numbering & migrations — no conflict, straightforward extension

`NumberingService`'s `Defaults` dictionary trivially accepts a new entry (`["Permit"] = new("PTW", 4, true, () => year)` → `PTW-2026-0001`), reusing the existing atomic `UPDATE...OUTPUT` sequence generator. 23 migrations exist today; `PEMSDbContext` already exposes 145 `DbSet<>` properties. CMMS-10's ~20 new entities (~14% growth) should follow the same incremental-additive-migration pattern already used all session — small, named migrations per sub-slice, never consolidated, never touching prior migrations.

---

## 12. Summary — Decisions Needed Before Design, and Recommended Decomposition

**Two decisions need the user's input before any entity design starts** (both flagged above, not silently assumed):
1. **How to represent contractor personnel** (§5) — recommend extending `Employee` with a type discriminator + `VendorId`, since it's additive and every downstream competency/workflow join table gains contractor support for free.
2. **How to extend authorization for Permit's ~14 actions** (§6) — recommend a new generic `(Role, Module, Action)` permission table used by the Permit module specifically, rather than growing the existing fixed-boolean `RolePermission` shape.

**Reusable, no new design needed:** FunctionalLocation breadcrumb/picker service (§3), Skill/Qualification/Certification competency chain (§4), the WorkflowPlatform engine as the Approval Matrix (§7), the StatusHistory audit pattern (§8), the Document TPT + DocumentAttachment pattern (§9), the Notification service (§10), the NumberingService (§11).

**Given the scope** — baseline's own Definition of Done (§38) lists ~20 major capabilities (full Permit lifecycle, Assessment Engine, configurable Approval Matrix, LOTO lifecycle, Isolation Verification, Gas Test lifecycle, SIMOPS, Shift Handover, Emergency workflow, Suspend/Resume/Extend, controlled Closure, Competency validation, Contractor integration, RBAC, Audit Trail, Execution Readiness Gate, 5 dashboards, Critical Alerts, and an 18-step End-to-End Acceptance Scenario) — this is substantially larger than CMMS-00 through CMMS-09 combined. Recommend **not** treating "CMMS-10" as a single phase the way CMMS-06/07/08/09 were. A natural decomposition, each independently plan-mode-scoped and live-tested the same way every prior phase was:

- **CMMS-10a** — Core Permit domain + lifecycle state machine + Permit Types + WorkOrder linkage (the minimum for a Permit to exist and move through states, without Isolation/GasTest/SIMOPS yet).
- **CMMS-10b** — Approval Matrix wiring (reusing `IWorkflowRuntime`) + the contractor-personnel and permission-action decisions from above.
- **CMMS-10c** — Isolation/LOTO lifecycle + Isolation Point/Verification.
- **CMMS-10d** — Gas Testing.
- **CMMS-10e** — SIMOPS conflict detection + Shift Handover.
- **CMMS-10f** — Execution Readiness Gate + Emergency workflow + Suspension/Extension/Closure controls.
- **CMMS-10g** — KPIs, Critical Alerts, Dashboards.

This mirrors the CMMS-00→09 governance model this whole session has followed: each slice gets its own plan-mode scope proposal, explicit Scope Boundary, live smoke test, and cleanup — rather than one large, harder-to-verify implementation pass.
