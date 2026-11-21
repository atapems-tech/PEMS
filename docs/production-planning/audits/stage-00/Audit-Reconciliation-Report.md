# PPC Stage 0: Audit reconciliation report (final working version, 2026-11-14)

**Baseline:** main 1185588f. Branch `feature/SCRUM-561-ppc-stage0-audit-saber` (local only). Changes: documentation and plan notes; no source, database, migration, configuration, commit or PR.
**Method:** three reviews in separate agent contexts. Pass 3 saw repository evidence and its brief only. **INDEPENDENT THIRD-PARTY AUDIT NOT PERFORMED — REVIEWS ARE ROLE-BASED INTERNAL REVIEWS.**
**Audit IDs:** pass 1 `PPC-S0-P1-Fn`; pass 2 `PPC-S0-P2-An`; pass 3 `PPC-S0-P3-Xn`.
**Pass reports are kept unchanged** to preserve their independence. Corrections are recorded here.

## 1. Corrections to the pass reports

| # | Report | Problem | Correction (evidence) |
|---|---|---|---|
| K1 | Pass 3 | Tally "17 confirmed, 6 refuted, 3 not verified" does not match its table | Recount by row: 15 fully confirmed, 4 partly confirmed (X8, X11, X17, X22), 4 refuted or partly refuted (X3, X6, X13, X23), 3 not verified (X10, X25, X26). **Confirmed with any element: 19.** |
| K2 | Pass 3 | Test evidence: "production services tested in memory" with no `UseInMemoryDatabase` string | Substance confirmed: `ProductionRunServiceTests` line 24 uses `PemsDbContextFactory.CreateInMemory`. Evidence path corrected. |
| K3 | Pass 2 | ISO 9001 row: "FDIS closed 2026-07-09, publication conflicting" | Superseded by the official ISO page: **published, Edition 6, 16 September 2026**. |
| K4 | Pass 2 | ISO 19011 row: "2018 edition current or replacement not identified" | Superseded by the official ISO page: **ISO 19011:2026, Edition 4, published (2026-05); 2018 edition withdrawn.** |
| K5 | Pass 3 | X13: "genealogy one level" | Stands. Refined by `InventoryBatchTraceabilityTests` (9 tests): batch identity is kept on ledger rows; batch balances are derived. Stock records have no batch dimension. |
| K6 | Plan (not a report) | Epic named as SCRUM-6 | Wrong. Parent is **SCRUM-11** (M6), as confirmed in Jira for SCRUM-561 to 564 and in plan row 0092. Planning file corrected. |
| K7 | Pass 1 F13 | "No batch dimension on stock" | Refined: batch balances exist at ledger level (see K5). Severity kept Major; evidence changed. |

## 2. Critical and Major classification (final)

The owner asked for severity and gate effect to be recorded separately. Full register: `Critical-Major-Findings-Register.md`.

| Finding | Severity | Gate effect | Verification | Status |
|---|---|---|---|---|
| **F1** BOM, formula and manufacturing routing | **Major** | Blocks Stage 0 gate (owner, 2026-11-14) | BOM and formula: CONFIRMED. Routing: resolved by `RoutingRule-Assessment.md`, not a manufacturing routing | OPEN |
| **F2** Production MRP | Major | Blocks Stage 0 gate | CONFIRMED (pass 2 A1, pass 3 X1, X4, X5) | OPEN |
| **F3** RCCP and CRP capacity model | Major | Blocks Stage 0 gate | CONFIRMED (pass 2 A1, pass 3 X9) | OPEN |
| **F4a** APP and MPS layer | Major | Blocks Stage 0 gate (owner, 2026-11-14) | CONFIRMED absent by search (0 matches). See `APP-MPS-Assessment.md` | OPEN |
| **F4b** Plan versioning | Major | MPP Design Blocking (owner, 2026-11-14) | CONFIRMED (pass 3 X14) | OPEN |

**Disagreement resolved (superseded 2026-11-14):** the owner instructed that F1 is Major with gate blocking. Pass 2 and this review now agree on the rating. See section 8.

## 3. Pass 3: the 19 confirmed items with final status

| Audit ID | Item | Pass 3 result | Final status | Severity | Evidence (path, class, method) | Finding link |
|---|---|---|---|---|---|---|
| PPC-S0-P3-X1 | Lead time in MRP | CONFIRMED | Confirmed | Major | `MaterialItem.cs` no lead-time field; `NetDemandCalculator.cs` ~L140 `ReorderPolicyNotEvaluated` | F2 |
| PPC-S0-P3-X2 | Unit conversion not used | CONFIRMED | Confirmed | Major | `MaterialUOM.cs` comment; `ProductionRunService` display only | F16, F17 |
| PPC-S0-P3-X4 | Incoming supply not netted | CONFIRMED | Confirmed | Major | `NetDemandCalculator.cs` ~L84–124 | F2 |
| PPC-S0-P3-X5 | Net demand does not cover production | CONFIRMED | Confirmed | Major (F1 and F2, both Gate Blocking) | `NetDemandCollector.cs` L18–20, L50 | F1, F2 |
| PPC-S0-P3-X7 | Posting lock tested only on SQL Server | CONFIRMED | Confirmed (test gap) | Moderate | `PostingScope.cs` L25; `ProductionPostingIdempotencyTests` 2 SQL tests | R21 |
| PPC-S0-P3-X8 | Multiple Planned runs per order | PARTLY | Partly confirmed | Moderate | `ProductionRunService.CreateAsync` ~L179–208 | F9 |
| PPC-S0-P3-X9 | No capacity or calendar fields | CONFIRMED | Confirmed | Major | `ProductionWorkCenter.cs`; `ShiftDefinition.cs` | F3 |
| PPC-S0-P3-X11 | No campaign entity (partly) | PARTLY | Partly confirmed | Moderate | grep: no Campaign type | R29 |
| PPC-S0-P3-X12 | Free-text yield and output classes | CONFIRMED | Confirmed | Major | `ProductionOutput.OutputClassification`; `ProductionLoss.LossCategory` | R14 |
| PPC-S0-P3-X14 | No plan versions | CONFIRMED | Confirmed | Major | `ProductionOrderService.cs` no update method; `ProductionOrder.cs` no version | F4b |
| PPC-S0-P3-X15 | No reversal of posted stock | CONFIRMED | Confirmed | Major | `ProductionRunService.CancelAsync` ~L274–290 | R15 |
| PPC-S0-P3-X16 | Child production entities lack RowVersion and TenantId | CONFIRMED | Confirmed | Moderate | `BaseEntity.cs` L24–27 | R17 |
| PPC-S0-P3-X17 | Services lack permission checks (partly) | PARTLY | Partly confirmed | Moderate | Controllers 11/11, 20/20, 1/1; services have no `IPermissionService` | R24 |
| PPC-S0-P3-X18 | AuditLog not written by production | CONFIRMED | Confirmed | Moderate | No AuditLog in production services | R25 |
| PPC-S0-P3-X19 | Hangfire not used for production | CONFIRMED | Confirmed | Minor | Hangfire users list | R19 |
| PPC-S0-P3-X20 | Local time basis | CONFIRMED | Confirmed | Minor | `ProductionRunService.cs` 11 uses of `DateTime.Now` | R26 |
| PPC-S0-P3-X21 | No planning regression tests | CONFIRMED | Confirmed | Major | `Test-Inventory.md`: 0 tests for planning capabilities of 168 planning-adjacent | R20 |
| PPC-S0-P3-X22 | Row 0250 marked Done | PARTLY | Partly confirmed | Moderate | `plan/rows/0250.md` status Done; SM-24 "Supported (unverified)" | R27 |
| PPC-S0-P3-X24 | No lot or serial entities | CONFIRMED | Confirmed | Moderate | `MaterialItem.IsSerialized`, `IsBatchControlled` flags only | F14 |

Not confirmed (for completeness): X3 (refuted for the calculator; cross-source overlap not verified), X6 (refuted: reserved stock protected), X13 (refuted as absent; one-level only), X23 (refuted: WorkOrder and ProductionOrder separated), X10, X25, X26 (not verified).

## 4. Standards (official check, 2026-11-14)
- **ISO 9001:2026:** published, Edition 6 (16 September 2026). Official page read.
- **ISO 19011:2026:** published, Edition 4 (2026-05). 2018 edition withdrawn. Official page read.
- Others (IEC 61512, ISO 22400, EPCIS 2.0.1, ISA/IEC 62443 parts): still not verified from official sources. See `05-standards-register-and-mapping.md`.
- Publication does not show PEMS compliance. No conformity claim is made.

## 5. Plan rows reconciliation (0092, 0095, 0227, 0033, 0250)

No plan row was edited in this pass. The table records how each row relates to PPC.

| Row | Title (short) | Status and milestone | Relation to PPC | Reconciliation decision |
|---|---|---|---|---|
| 0092 | Wave 6: production and projects (Production, ProductionOrder, ProductionRun, QC, Projects) | Not Started, P3, M6, depends #89 | Covers the **migration of existing production pages** to the dynamic module. Not planning. | Keep separate. PPC is a new scope; 0092 may need to depend on the PPC Stage 1 model for production pages. Owner decides. |
| 0095 | Production data migration and user training | Not Started, P1, M4, doer User, depends #86 | Data migration of production records. Not planning. | Keep separate. Note that its P1 priority is higher than PPC's current stage. |
| 0227 | Maintenance scheduling board (capacity, skills, materials, conflicts) | Not Started, P2, M5, depends #33 | **Overlaps PPC capacity and scheduling (CRP, PS).** Risk of two capacity models. | Coordinate. Any PPC capacity model must be the one 0227 uses, or 0227 must be scoped to CMMS only. Owner decision (OD-05, OD-07). |
| 0033 | View types incl. calendar and Gantt | In Progress, P1, M4, depends #32 | Gantt and calendar views are needed by PS and CRP screens. | Dependency for later PPC UI. No action now. |
| 0250 | Domain breadth (CMMS, PTW, inventory, production, ...) | **Done**, area "estimated from code", confirmed_by_pm No | Claims production capability as Done. Production planning is absent (X22). Acceptance tests not run. | Status overstated. Owner decides whether to reopen or re-verify. Not changed in this pass. |

## 6. Owner decisions (crosswalk)
The pass reports used OD-01 to OD-11 with different meanings. The single register is `03-business-glossary-and-decisions.md` (OD-01 to OD-23). Crosswalk:

| Pass report ID | Register ID | Question |
|---|---|---|
| Pass 2 OD-01 (A1) | OD-16 | Planning engine and run model |
| Pass 2 OD-02 (A2) | OD-15 | Workflow routing and maker-checker |
| Pass 2 OD-03 (A3) | OD-17 | Transactional audit |
| Pass 2 OD-04 (A4) | OD-18 | Row versions |
| Pass 2 OD-05 (A5) | OD-19 | External keys |
| Pass 2 OD-06 (A7) | OD-20 | Attribution rule |
| Pass 2 OD-07 (A9) | OD-21 | UOM required |
| Pass 2 OD-08 (A11) | OD-22 | Effectivity and routing versioning |
| Pass 1 F1 | OD-02, OD-03 | BOM, formula, routing |
| Pass 1 F2 | OD-06 | Production demand and release |
| Pass 1 F3 | OD-05, OD-07 | Capacity and scheduling |
| Pass 1 F4 | OD-04 | Plan hierarchy (APP meaning now confirmed) |
| Pass 1 F10–F12 | OD-08 | Quality gate |
| Pass 1 F13–F14 | OD-09 | Traceability |
| Pass 1 F7–F9, F15 | OD-10 | Closure and tolerance |
| Pass 1 F16–F17 | OD-11 | Unit conversion |
| Pass 1 F23 | OD-01 | Mode (Process decided) |

## 7. Gate status
**Historical status as of 2026-11-14: NOT PASSED.** This was superseded by the owner decision of 2026-11-16: the Stage 0 gate passed and Stage 1 was authorised. See `Owner-Decisions-2026-11-16.md` and the updated `Stage-00-Gate-Assessment.md`.

## 8. Historical update 2026-11-14 (owner instruction)
- F1 moved from Critical to Major, Gate Blocking. Register now has 0 Critical and 26 Major rows (the earlier figure of 25 excluded F1).
- F4a (APP and MPS) is Major, Gate Blocking. F4b (versioning) is Major, MPP Design Blocking.
- Plan row 0033: no Gantt component in PEMS.UI; four cshtml views match gantt or timeline and were not checked for Gantt behaviour.
- Plan row 0250: recorded as Done, not verified; owner decides.
- Plan row 0227: coordinate with PPC capacity (waves W3 and W5 in 16); owner decides.
- Plan rows 0092 and 0095: outside PPC unless a dependency is proven.
- Historical gate state: NOT PASSED. Superseded on 2026-11-16 by the recorded owner authorisation.
