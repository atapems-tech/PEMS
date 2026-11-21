# 16 Implementation roadmap (PPC Stage 0, 2026-11-14)

**Status:** Stage 1 design and additive implementation were authorised by the owner on 2026-11-16 (`audits/stage-00/Owner-Decisions-2026-11-16.md`). This roadmap remains the proposed sequencing and acceptance plan; authorisation does not mark any wave complete or waive its test and release gates.

**Naming:** waves W0 to W8 are capability groups in this roadmap. They map to the earlier batch names E1 to E10 in the planning notes. The governing document defines the stage sequence; this roadmap assigns capabilities to it and does not redefine it.

## 1. Entry conditions and standing constraints
1. **Met:** Stage 0, ADR-001 and the five core PPC axes were approved on 2026-11-16.
2. **Met for Stage 1:** the 26 Major findings received owner disposition. Their corrective actions remain open until implemented and verified.
3. **Settled for initial Stage 1:** Process first; ProcessFormula and ProcessRoute; APP/MPP/MPS horizons and buckets; recommendation-only MRP; infinite capacity with overload warning; QC gates; multi-level genealogy; WorkflowPlatform maker-checker; Hangfire runs; and optimistic concurrency on mutable aggregate roots.
4. **Still constrained:** plant-specific values, standards verification, external-key design, plan rows 0227 and 0250, APS/S&OP scope, and later discrete/hybrid scope must be resolved before the wave that depends on them.

## 2. Waves

> [!NOTE]
> The wave mapping below represents the baseline plan. The revised and expanded wave mapping incorporating international standards (REQ-30 to REQ-46, OD-24 to OD-29, and associated oracle test IDs) is detailed in **`11-revised-wave-map.md`**.
| Wave | Scope | Findings and requirements | Depends on | Acceptance (exit) | Gate |
|---|---|---|---|---|---|
| W0 | Data and audit prerequisites: one transaction for data and audit; row versions on order, stock and transaction tables; service-level permissions | A3, A4, X17 (AD-03, AD-04, AD-11); REQ-19, REQ-20, REQ-23 | Entry conditions | Concurrency and transaction tests pass on SQL Server; regression green | Owner approves W0 |
| W1 | Reference data: UOM conversion and required UOM; reconciliation per material; unit-safe KPIs | X2, F15, F16 (AD-07); REQ-17, REQ-24 | W0 | Oracle tests T-UOM-01, T-REC-01, T-KPI-01 pass | Owner approves W1 |
| W2 | Process execution gaps: process formula and process routing contract; release gate with WorkflowPlatform; quantity closure; hold and cancel propagation; QC gates; quarantine and recall; reversal of posted stock; lot balance | F1, F5, A2, F7, F8, F9, F10, F11, F12, F13, X15, X12 (AD-02, AD-09, AD-10); REQ-09 to REQ-11, REQ-13 to REQ-16, REQ-18 | W1 | Focused tests T-FORM, T-ROUT, T-REL, T-CLO, T-HOLD, T-QC, T-RECALL, T-TRC-01, T-REV-01 pass; regression green | Owner approves W2 |
| W3 | Calendar and run model: calendar with shifts and holidays; UTC time; Hangfire planning run with idempotency and stored input version; capacity per work centre | F6, A1, X20, F3 (AD-01, AD-08); REQ-12, REQ-22 | W2 | T-CAL-01, T-TIME-01, T-RUN-01 pass | Owner approves W3 |
| W4 | APP, MPP versions and MPS: APP run produces an immutable MPP version; MPS derived from the approved version | F4a, F4b (ADR-001, AD-10); REQ-01 to REQ-03, REQ-28 | W3 | T-PLAN-01, T-VER-01, T-VER-02, T-MPS-01 pass; no MPP calculation class | Owner approves W4 |
| W5 | RCCP | F3; REQ-04 | W4 (MPS), W3 (capacity) | T-RCCP-01 oracle pass | Owner approves W5 |
| W6 | Production MRP: extend NetDemandCalculator with production demand, lead times and incoming supply; recommendations only | F2, F1; REQ-05 | W2 (formula), W3 (lead times) | T-MRP-01, T-MRP-02 oracle pass; existing NetDemand tests unchanged | Owner approves W6 |
| W7 | CRP and scheduling (conditional on OD-07 and OD-01): detailed capacity, PS, Gantt or timeline views (row 0033) | F3, F6; REQ-06, REQ-07 | W5, W6 | T-CRP-01, T-PS-01 pass (if in scope) | Owner decides scope before W7 |
| W8 | Discrete and integration (conditional): serial genealogy, multi-level genealogy, external keys and idempotent import | F14, A5; REQ-16, REQ-21 | W2 and owner scope decision | T-TRC-02, T-IMP-01 pass | Owner decides scope before W8 |

## 3. Dependencies on other work
- **Row 0227 (maintenance scheduling board):** overlaps capacity and scheduling. Any PPC capacity model in W3 and W5 must be the one row 0227 uses, or row 0227 is scoped to CMMS only. Owner decides before W3.
- **Row 0033 (views incl. Gantt):** dependency for W7 views. Owner (2026-11-14): DEFER final decision until existing Gantt capabilities and PS scope are reviewed. No Gantt component found in PEMS.UI; the four `.cshtml` views that match "gantt" or "timeline" are calendar or timeline views and were not checked for Gantt behaviour (`audits/stage-00/Stage-00-Gate-Assessment.md`).
- **Rows 0092 and 0095:** DEFER from PPC scope unless an actual dependency is demonstrated (owner, 2026-11-14).

## 4. Plan row reconciliation (decisions required from owner; no plan row edited in Stage 0)
- **0250 (Done):** a row marked Done without accepted tests is not Verified. Owner direction (2026-11-14): DEFER acceptance until required acceptance tests have been executed. Until then the status is recorded as "Done, not verified".
- **0227:** coordinate with W3 and W5 before any capacity work.

## 5. Risks to the roadmap
- W0 changes shared persistence code (PEMSDbContext, BaseEntity children). Regression risk is high; full regression before W0 closes.
- W2 touches production execution that is already in use. Existing batch, QC and posting tests must stay green.
- W4 uses the owner-confirmed APP monthly horizon and MPS weekly/daily horizon; its implementation still needs oracle tests and frozen-zone rules.

## 6. Not in this roadmap
- Stage 1 work is authorised; this document does not itself constitute completion, release approval or a waiver of the wave acceptance criteria.
- No Jira issue is created or changed by Stage 0. Jira issues SCRUM-561 to 564 are the Stage 0 tracking issues already created; no change is made in this pass.
