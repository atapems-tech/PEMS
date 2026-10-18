# PEMS Packaging — Final Decision Workshop Report

Date: 2026-09-25 · **Status: C — IMPLEMENTATION BLOCKED** (not Production Ready; nothing is implemented)
This phase was documentation and decision preparation only.

## 1. Current State

- Phase 2G and 2H complete; Phase 3A–3H blocked; 3I not applicable.
- No Packaging class, table or migration exists (repo search and `sys.tables`). Latest migration
  applied in `PEMSDB`: `20260923141435_AddContractApprovalRulePriority`.
- Approvals recorded: **0 of 16** (gate command returns 0). Nothing was marked APPROVED, REJECTED or
  DEFERRED.
- The Phase 2C production quality gate is intact.
- Live roles: **20**. Phase 2E KPI catalog: **56** metrics.

## 2. Decisions Requiring Approval

| ID | Decision | Owner | Approver | Status |
|---|---|---|---|---|
| D1 | Package definition | Packaging Manager | Complex Mgr, Laboratory Mgr | OPEN |
| D2 | Mixed-product package (+ are packaging materials recorded?) | Packaging Manager | Engineering Mgr, Complex Mgr | OPEN |
| D3 | Multi-batch package | Process Engineering Lead | Packaging Mgr, Laboratory Mgr | OPEN |
| D4 | Gross / Tare / Net (+ tare source) | Finance Manager | Laboratory Mgr, Complex Mgr | OPEN |
| D5 | Acknowledge split/merge/repack is out of initial scope | Laboratory Manager | Packaging Mgr, Operations Mgr | OPEN |
| D6 | Bulk vs packaged product identity | Finance Manager | Packaging Mgr, Complex Mgr | OPEN |
| D7 | Quality release authority (+ Hold/ReleaseHold answer) | Laboratory Manager | Engineering Manager | OPEN |
| E-4 | Packaging permission module | Complex Manager | Laboratory Mgr, Engineering Mgr | OPEN |
| E-5 | Role mapping / roles to create | Complex Manager | Engineering Mgr, Finance Mgr | OPEN |

Still open but outside this workshop's required list: D12 / E-1 financial treatment (needed before
packaging stock is posted), D8–D11, E-2, E-3 (defaults exist).

## 3. Technical Recommendations (kept separate; none is an approval)

D1 → B (package unit of one product; a handling layer can be added later) · D2 → A for content;
packaging materials are the business's call · D3 → B if blending happens, else A; not C · D4 → A,
stored as append-only weighing events · D5 → exclude from first release · D6 → schema-neutral design;
B needs the fewest changes to existing structures · D7 → B if `QAManager` is confirmed as the
laboratory authority · E-4 → C (release stays on Production.Approve; execution separable) · E-5 →
approve the seven fits, decide the five new roles, resolve the two undetermined.

Change control: D1's options were re-cut in this workshop; the earlier "filled container"
recommendation corresponds to the new option B. The earlier D6 recommendation (schema-neutral) is
unchanged; only its consequences were expanded.

## 4. Actual Role Baseline (live database)

20 roles, 24 assignments. Twelve are code constants; eight are work-order governance roles
(`MNT.Manager`, `MNT.PlanningHead`, `MNT.PlanningExpert`, `MNT.DisciplineHead`, `MNT.SeniorExpert`,
`MNT.Expert`, `maintenance planning`, `OPS.IssueAuthority`). Production grants exist for four roles only:
EngineeringManager (all), MaintenanceEngineer (View/Create/Edit/Hold/ReleaseHold), QAManager
(View/Approve), Viewer (View). Full per-role matrix with quality, inventory and workflow authority:
decision pack §3. No role is named for Warehouse, Packaging, Laboratory or Operations management.

## 5. Role Mapping

| Result | Roles |
|---|---|
| Fits an existing role (7) | Complex Manager (Viewer) · Maintenance Manager (MNT.Manager) · Finance Manager (Finance, needs View grants) · Engineering Manager (EngineeringManager) · Process Engineering Lead (EngineeringSupervisor, needs grants) · Laboratory Manager (QAManager) · HSE Manager (HSE, needs View) |
| New role likely required (5) | Operations Manager · Operations Shift Supervisor · Operator · Packaging Manager · Warehouse Manager |
| Undetermined (2) | Commissioning Manager (depends on E-2) · Security Manager (audit-view rights not mapped) |

Nothing was created or changed; "new role" is a least-privilege assessment.

## 6. Quality Authority Issue

The governance principle is that the laboratory decides Released, Blocked or Rejected and packaging,
warehouse, security and operations cannot release. Today the release/reject authority is
`Production.Approve`, held by `QAManager` (1 user, also approves incoming receipts) and
`EngineeringManager` (2 users, who also hold Create, separated by a SoD pair). Whether the package
authority is `QAManager`, a laboratory role, or a new role is decision D7.

## 7. QAManager Hold / ReleaseHold Issue

`QAManager` has Approve but neither Hold nor ReleaseHold. Blocking a batch needs Hold and unblocking
needs ReleaseHold (`ProductionRunController`), so the role that releases and rejects cannot block.
Only `EngineeringManager` and `MaintenanceEngineer` can. **No permission was changed.** The owner
decides (D7) whether the release authority needs them. Consequence if not: "Blocked" stays a state
that engineering/maintenance roles, not the release authority, place.

## 8. Permission Module Issue

Reusing `"Production"` (E-4 option A) would let any holder of Production.Create — today
EngineeringManager and MaintenanceEngineer — create packaging records and would not let operators be
separated from production. A dedicated module (B) separates them but risks a second quality
authority. The hybrid (C) keeps one quality authority and separates execution. Nothing was created.

## 9. Phase 3 Activation Requirements

Fourteen mandatory gates (`PEMS_OPERATIONS_PHASE_3_PACKAGING_ACTIVATION_CHECKLIST.md`): D1, D2, D3,
D4, D5 acknowledgement, D6, D7, E-4, E-5, architecture freeze activated, migration strategy, inventory
boundary, barcode strategy, security/SoD validated. **0 of 14 satisfied** *(update 2026-09-25: a fifteenth gate, configurability architecture, was added by the Configurability Guide; now 0 of 15)*. D12 is a Phase 3C gate.

## 10. Exact Management Decisions Required

1. **D1** — choose A/B/C/D. 2. **D2** — choose A/B/C/D and answer whether packaging materials are
recorded. 3. **D3** — choose A/B/C. 4. **D4** — choose A/B/C/D and the tare source. 5. **D5** —
acknowledge exclusion (three owners). 6. **D6** — choose A/B/C/D. 7. **D7** — choose A/B/C/D and answer
Hold/ReleaseHold. 8. **E-4** — choose A/B/C. 9. **E-5** — approve the mapping and name the roles to
create. Each with approver, date and evidence, in the sign-off sheet, then mirrored into the sign-off
baseline.

## 11. Implementation-Surface Validation (run after all documents were written)

| Check | Result |
|---|---|
| `.cs`, `.cshtml`, `.csproj`, `.resx`, `.css`, `.js` files modified after the Phase 2F baseline document | none |
| `PEMS.Persistence/Migrations` changes | none |
| Latest migration applied in `PEMSDB` | unchanged (`20260923141435_AddContractApprovalRulePriority`) |
| Database writes | none — read-only `SELECT`s only |
| Permission / role / workflow change | none (roles and grants were only read) |
| Modified tracked `.cs` files in the working tree | 13, all pre-existing from earlier unrelated work (theme redesign, procurement audit), unchanged by this phase |
| Approvals recorded | 0 of 16 |

## 12. Files

**Created:** `docs/specifications/PEMS_OPERATIONS_PACKAGING_FINAL_BUSINESS_DECISION_PACK.md`,
`docs/specifications/PEMS_OPERATIONS_PACKAGING_FINAL_SIGNOFF_SHEET.md`,
`docs/specifications/PEMS_OPERATIONS_PHASE_3_PACKAGING_ACTIVATION_CHECKLIST.md`,
`docs/audits/PEMS_OPERATIONS_PACKAGING_FINAL_DECISION_READINESS_AUDIT.md`, this report.
**Updated:** `PEMS_OPERATIONS_PHASE_2G_BUSINESS_SIGNOFF_BASELINE.md` (D5 approver aligned; change log).
Unrelated changes in the working tree were not touched; nothing was committed.

## 13. Addendum 2026-09-25 — Architecture decisions M-1…M-5

Recorded in `docs/specifications/PEMS_OPERATIONS_PACKAGING_CONFIGURABILITY_ARCHITECTURE_DECISIONS.md` (architecture decisions, **not** business approvals).
M-1 through M-5 are CLOSED — ACCEPTED as architecture decisions; residual technical items TI-1…TI-4 and
per-mode release-1 scope remain open. None of the D1–D12 / E-1…E-5 rows changed: still **0 of 16**
business approvals; activation checklist **0 of 15**; classification **C — IMPLEMENTATION BLOCKED**.
This report's decision table (§2) is unchanged.
