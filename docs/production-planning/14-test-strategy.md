# 14 Test strategy (PPC Stage 0, 2026-11-14)

**Status:** PROPOSED. Not approved. No test was written or run in Stage 0.

## 1. Why this strategy exists
- The repository has 0 tests for planning capabilities (`audits/stage-00/Test-Inventory.md`). Of 168 planning-adjacent tests, all cover production execution, posting, net demand for procurement or replenishment.
- In-memory tests (`PemsDbContextFactory.CreateInMemory`, used by `ProductionRunServiceTests`) do not check SQL Server behaviour such as locks, transactions, row versions and check constraints.
- Therefore planning tests run on SQL Server for every rule that depends on a database feature.

## 2. Test levels
| Level | Purpose | Database | Examples |
|---|---|---|---|
| Unit | Pure calculation (RCCP load, MRP netting, formula yield) | None | Netting with lead time; capacity per bucket |
| Service | Business rules with the real service | SQL Server (Express locally) | Release refused on failed check; hold reaches runs |
| Oracle | Expected results computed by hand or by an independent method, written before the code | SQL Server | MPS from a known MPP version; RCCP for a sample plan |
| Concurrency | Row versions and transactions | SQL Server, two connections | Conflicting update detected; audit rolled back with data |
| Migration | Additive migrations and data migrations apply and roll back as documented | SQL Server | Upgrade test from the SCRUM-280 pipeline |
| Regression | Existing production, posting, idempotency and replenishment behaviour is unchanged | SQL Server | Existing 168 planning-adjacent tests |
| Documentary | Standards statements, plan reconciliation, owner decisions recorded | None | Checks against documents 05, 06, 03 |
| Browser (QA lines) | Visible behaviour in the UI | Running site | Listed as QA checklist lines in Jira, per guideline 07 |

## 3. Oracle rule
- Every planning calculation has at least one oracle test.
- The expected value is computed by hand or by an independent method and recorded in the test with its derivation.
- The test may not call the production calculation to obtain its expected value.
- Rounding and tolerance are set by the owner decision for that finding.

## 4. Test IDs and finding links
| Test ID | Level | Covers | Finding / requirement |
|---|---|---|---|
| T-FORM-01, T-FORM-02 | Unit, Oracle | Formula yield and standard consumption | F1, REQ-09 |
| T-ROUT-01 | Service | Process routing separate from RoutingRule | F1, REQ-10 |
| T-REL-01, T-REL-02 | Service | Release gate and maker-checker | F5, A2, REQ-11 |
| T-CAL-01 | Unit | Calendar with holidays and shift boundary | F6, REQ-12 |
| T-TIME-01 | Unit | UTC storage and DST | X20, REQ-12 |
| T-CLO-01 | Service | Closure tolerance boundary | F7, F9, REQ-13 |
| T-HOLD-01 | Service | Hold and cancel reach runs | F8, REQ-14 |
| T-QC-01, T-RECALL-01 | Service | QC gate on inputs and outputs; recall | F10, F11, F12, REQ-15 |
| T-TRC-01, T-TRC-02 | Service, SQL | Lot balance; genealogy depth | F13, F14, REQ-16 |
| T-REC-01, T-KPI-01 | Unit | Per-material reconciliation; unit-safe KPIs | F15, F16, REQ-17 |
| T-REV-01 | Service, SQL | Reversal nets stock to zero | X15, REQ-18 |
| T-TX-01 | Concurrency | Audit and data in one transaction | A3, REQ-19 |
| T-CONC-01 | Concurrency | Row version conflict | A4, REQ-20 |
| T-IMP-01 | Service | External key idempotent import | A5, REQ-21 |
| T-RUN-01 | Service | Planning run idempotency | A1, REQ-22 |
| T-AUTH-01 | Service | Service-level permission | X17, REQ-23 |
| T-UOM-01 | Unit | Conversion factor; UOM required | X2, REQ-24 |
| T-PLAN-01, T-VER-01, T-VER-02, T-MPS-01 | Oracle, Service | APP run, MPP immutability, MPS derivation | F4a, F4b, REQ-01 to REQ-03 |
| T-MRP-01, T-MRP-02 | Oracle | MRP netting with lead time and incoming supply | F2, REQ-05 |
| T-RCCP-01, T-CRP-01 | Oracle | Rough-cut and detailed capacity | F3, REQ-04, REQ-06 |
| T-PS-01 | Oracle | Scheduling (only if the owner keeps PS in scope) | F6, REQ-07 |
| T-STD-01, T-DOC-01 | Documentary | Standards and plan reconciliation | REQ-25, REQ-27 |
| T-REG-01 | Regression | Existing production suites unchanged | REQ-29 |
| T-OEE-01 | Oracle | OEE = Availability × Effectiveness × Quality; ISO 22400 time model | REQ-32 |
| T-EBR-01, T-EBR-02 | Service | ProductionRunOperationLog created per formula operation; EBR deviation tracking | REQ-34 |
| T-DTF-01 | Service | PlanItem inside Demand Time Fence requires workflow approval before update | REQ-35 |
| T-ATP-01 | Oracle | Discrete Available-to-Promise & PAB match independent manual calculations | REQ-36 |
| T-EXC-01 | Service | MRP generates MrpExceptionMessage (Expedite/Defer/Cancel) for planner review | REQ-37 |
| T-LOT-01 | Unit | Lot-sizing rules: LFL (demand), FOQ (batch multiple), EOQ (formula check) | REQ-38 |
| T-FAM-01 | Service | APP PlanItem disaggregated to MPS items via ProductFamily.HistoricalMixPercent | REQ-39 |
| T-CAT-01 | Service | Catalyst cycle count increment and CycleCountLimit threshold alert | REQ-44 |
| T-TAR-01 | Service | Turnaround CalendarEvent blocks work center capacity in RCCP engine | REQ-45 |

## 5. Environment and gates
- Database: SQL Server. Local Express instance for development. CI status for SQL Server posting tests is **NOT VERIFIED**; no test was run in Stage 0.
- Each wave runs the focused tests first, then the full regression once before the wave closes. This follows the owner's rule from SCRUM-337 (2026-11-02).
- Test gates: `python tools/test-gates/gates.py --base origin/main --run` chooses tests for each change.
- Exit criteria per wave: all oracle tests for that wave pass; no concurrency test is replaced by an in-memory test; regression suite green; QA lines written for browser checks.

## 6. Data
- Fixtures are created per test; no shared production data.
- Oracle fixtures are small (2 to 5 materials, 2 to 3 work centres, 3 buckets) so the expected values can be checked by hand.
- No test uses the real PEMSDB. The shared admin password is not used or stored in tests.

## 7. Open items
- Owner approved the test strategy in principle (X21, 2026-11-14). Writing and running oracle tests needs separate implementation authorisation.
- Owner decides the tolerance and rounding rules needed for oracle expected values (OD-10, OD-11).
- CI execution of SQL Server tests is NOT VERIFIED.
