# 04 Requirements traceability matrix (PPC Stage 0, 2026-11-14)

**Status of every row:** OPEN. No requirement is implemented and no planned test has run. Stage 0 records requirements and their links; it does not verify them.
**Sources:** OWNER = written owner instruction or confirmed decision (ADR-001 in document 06); DERIVED = follows from a finding in `audits/stage-00/Critical-Major-Findings-Register.md`; PROPOSED = owner proposal, not confirmed.
**Test IDs** refer to `14-test-strategy.md`. **Stage** refers to `16-implementation-roadmap.md`. The test strategy and roadmap remain planning documents; Stage 1 design and additive implementation were authorised by the owner on 2026-11-16. No requirement is implemented or verified by that authorisation alone.

| Req ID | Requirement | Source | Finding / risk link | Architecture decision | Planned verification | Roadmap wave | Status |
|---|---|---|---|---|---|---|---|
| REQ-01 | APP is the planning process that produces MPP versions | OWNER (ADR-001) | F4a, R04 | AD-01 (run model) | T-PLAN-01 | W4 | OPEN |
| REQ-02 | MPP is an approved, immutable, version-controlled plan; it is not a calculation engine | OWNER (ADR-001) | F4b, R05 | AD-10 | T-VER-01, T-VER-02 | W4 | OPEN |
| REQ-03 | MPS is the detailed product schedule derived from an approved MPP version | OWNER (ADR-001) | F4a | ADR-001 | T-MPS-01 | W4 | OPEN |
| REQ-04 | RCCP checks main resources against the MPS | PROPOSED (owner definition) | F3, R03 | AD-01 | T-RCCP-01 | W5 | OPEN |
| REQ-05 | MRP computes material requirements from production demand, with lead times and incoming supply | PROPOSED (owner definition) | F2, R02 | Extend NetDemandCalculator (no fork) | T-MRP-01, T-MRP-02 | W6 | OPEN |
| REQ-06 | CRP computes detailed capacity load of work centres | PROPOSED (owner definition) | F3 | AD-01 | T-CRP-01 | W7 | OPEN |
| REQ-07 | PS schedules operations and allocates machines | PROPOSED (owner definition) | F6, R07 | Conditional on OD-07 | T-PS-01 | W7 (conditional) | OPEN |
| REQ-08 | Process is the first production type; discrete follows | OWNER (2026-11-13) | OD-01 | AD-09 | Scope check (document 03) | W1 | OPEN |
| REQ-09 | Process formula: versioned, effective-dated, with yield and co-products | DERIVED | F1, X12, R01, R14 | AD-09 | T-FORM-01, T-FORM-02 | W2 | OPEN |
| REQ-10 | Process routing is a separate contract from RoutingRule (document routing) | DERIVED (RoutingRule-Assessment) | F1, R29 | AD-09 | T-ROUT-01 | W2 | OPEN |
| REQ-11 | Plan and release pass a material and capacity check and a maker-checker approval | DERIVED | F5, A2, R06 | AD-02, AD-11 | T-REL-01, T-REL-02 | W2 | OPEN |
| REQ-12 | Calendar with shifts, holidays and shutdowns; UTC time basis | DERIVED | F6, X20, R07, R26 | AD-08 | T-CAL-01, T-TIME-01 | W3 | OPEN |
| REQ-13 | Quantity closure with owner tolerance; no output beyond tolerance | DERIVED | F7, F9, R08 | Closure rule | T-CLO-01 | W2 | OPEN |
| REQ-14 | Hold and cancel reach runs | DERIVED | F8, R09 | Status propagation | T-HOLD-01 | W2 | OPEN |
| REQ-15 | QC status gates inputs and outputs; quarantine and recall path | DERIVED | F10, F11, F12, R10 | Quality gate | T-QC-01, T-RECALL-01 | W2 | OPEN |
| REQ-16 | Lot traceability and genealogy depth as decided | DERIVED | F13, F14, X24, R11 | OD-09 | T-TRC-01, T-TRC-02 | W2 (lot), W8 (genealogy) | OPEN |
| REQ-17 | Reconciliation per material; KPIs unit-safe | DERIVED | F15, F16, X2, R12, R13 | AD-07 | T-REC-01, T-KPI-01 | W1 | OPEN |
| REQ-18 | Cancelling a released run reverses posted stock | DERIVED | X15, R15 | AD-10 | T-REV-01 | W2 | OPEN |
| REQ-19 | Data and audit written in one transaction | DERIVED | A3, R16 | AD-03 | T-TX-01 | W0 (prerequisite) | OPEN |
| REQ-20 | Row versions on order, stock and transaction tables | DERIVED | A4, X16, R17 | AD-04 | T-CONC-01 | W0 (prerequisite) | OPEN |
| REQ-21 | External keys with idempotent import | DERIVED | A5, R18 | AD-05 | T-IMP-01 | W8 | OPEN |
| REQ-22 | Planning runs are idempotent Hangfire jobs with a stored input version | DERIVED | A1, R19 | AD-01 | T-RUN-01 | W3 | OPEN |
| REQ-23 | Service-level permission checks on planning and production actions | DERIVED | X17, R24 | AD-11 | T-AUTH-01 | W1 | OPEN |
| REQ-24 | Unit conversion policy; UOM required on new lines | DERIVED | X2, R13 | AD-07 | T-UOM-01 | W1 | OPEN |
| REQ-25 | Standards statements carry evidence and a state; no conformance claim without evidence | OWNER (principle) | R28, R32 | — | T-STD-01 (documentary review) | Every wave | OPEN |
| REQ-26 | Every planning calculation has an independent expected result | OWNER (governing document) | X21, R20 | — | Oracle tests (document 14) | Every wave | OPEN |
| REQ-27 | Plan reconciliation: 0227 coordinated with PPC capacity; 0250 not Verified without accepted tests; 0092 and 0095 outside PPC unless a dependency is proven; 0033 Gantt checked | OWNER (reconciliation) | R27 and section 5 of the reconciliation report | — | T-DOC-01 (documentary) | Before W3 | OPEN |
| REQ-28 | No parallel calculation module for MPP or MRP II | OWNER (2026-11-14) | F4a | ADR-001 | Architecture check (no MPP calculation class) | Every wave | OPEN |
| REQ-29 | Production execution regression: existing batch, QC, posting and idempotency behaviour is preserved | DERIVED | Test-Inventory (168 tests) | — | Existing suites plus T-REG-01 | Every wave | OPEN |
| REQ-30 | Every KPI must be unit-safe; numerator and denominator must share the same UOM or use registered conversion | STANDARDS | ISO 22400-2, F16 | OD-11, OD-25 | T-KPI-01, T-UOM-01 | W1 | OPEN |
| REQ-31 | ProductionCalendar models Planned Busy Time per shift; CalendarEvent records downtime with EventType | STANDARDS | ISO 22400-2 §4.2, F6 | OD-25 | T-CAL-01, T-TIME-01 | W3 | OPEN |
| REQ-32 | OEE = Availability × Effectiveness × Quality Ratio calculated per work center per shift | STANDARDS | ISO 22400-2 §6.5 | OD-25 | T-OEE-01 | W3 | OPEN |
| REQ-33 | ProcessFormula must include ProcessFormulaOperation records (ordered, named operations with duration/parameters) | STANDARDS | ISA-88 §4, F1 | OD-24 | T-FORM-01, T-ROUT-01 | W2 | OPEN |
| REQ-34 | Each ProductionRun must produce a ProductionRunOperationLog (EBR entry with start/end UTC and operator) | STANDARDS | ISA-88 §5.6 EBR | OD-24 | T-EBR-01, T-EBR-02 | W2 | OPEN |
| REQ-35 | PlanVersion must have DemandTimeFenceDays and PlanningTimeFenceDays; inside DTF requires workflow approval | STANDARDS | APICS ASCM CPIM, F5 | OD-26 | T-DTF-01 | W4 | OPEN |
| REQ-36 | PlanItem must carry AvailableToPromise (calculated) and ProjectedAvailableBalance; ATP service oracle-tested | STANDARDS | APICS ASCM CPIM | OD-26 | T-ATP-01 | W4 | OPEN |
| REQ-37 | MRP must generate MrpExceptionMessage records (Expedite/Defer/Cancel) for planner review without auto-action | STANDARDS | APICS ASCM CPIM | OD-29 | T-EXC-01 | W6 | OPEN |
| REQ-38 | Each MaterialItem must have a LotSizingPolicy (LFL/FOQ/EOQ); MRP respects policy when creating recommendations | STANDARDS | APICS ASCM CPIM | OD-29 | T-LOT-01 | W6 | OPEN |
| REQ-39 | APP PlanItems reference ProductFamily; MPS PlanItems reference MaterialItem; disaggregation via mix % | STANDARDS | ISA-95 Part 1 §4.3 | OD-27 | T-FAM-01 | W4 | OPEN |
| REQ-40 | ProductionWorkCenter must have ProductionCapabilityStatement (max batches per shift, formula codes, validity) | STANDARDS | ISA-95 Part 2 §5, F3 | — | T-RCCP-01 | W5 | OPEN |
| REQ-41 | Multi-level ProductionBatchGenealogy must record input→output events compatible with GS1 EPCIS 2.0 | STANDARDS | GS1 EPCIS 2.0, F14 | OD-09 | T-TRC-01, T-TRC-02 | W2, W8 | OPEN |
| REQ-42 | Production actuals (output vs MPS PlanItem) feed back variance to active PlanVersion automatically | STANDARDS | APICS Closed-Loop | — | T-CLO-01 | W8 | OPEN |
| REQ-43 | ProcessFormula selection supports attribute-based filtering for feedstock variability (petrochemical) | STANDARDS | Industry (Petrochemical) | — | Documentary | Future | DEFERRED |
| REQ-44 | ProcessRouteOperation supports CycleCountLimit for catalyst life tracking with alert threshold | STANDARDS | Industry (Petrochemical) | OD-28 | T-CAT-01 | W5 | OPEN |
| REQ-45 | CalendarEvent of type Turnaround has ScopeFreezeDate and links to CMMS WorkOrder; blocks RCCP capacity | STANDARDS | Industry TAR Best Practice | OD-28 | T-TAR-01 | W3 | OPEN |
| REQ-46 | Co-product batches from ProductionRun added to MRP as scheduled receipts; reduce net requirements | STANDARDS | APICS Co-Product Netting | — | T-MRP-01 | W6 | OPEN |

## Coverage summary
- Requirements: 46 (REQ-01 to REQ-46).
- Sourced: Owner 8, Derived/Gaps 21, International Standards 17.
- Traced to a planned test ID: 45 (1 deferred).
- Traced to a passing test: **0** (Implementation phase).
