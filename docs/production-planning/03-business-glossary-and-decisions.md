# 03 Business glossary and decisions (PPC Stage 0, 2026-11-14)

**Status values:** OWNER-CONFIRMED (written owner confirmation), PROPOSED (analysis, not confirmed), NEEDS OWNER DECISION (open).

## 1. Glossary

| Term | Definition used | Status | Module or capability | Relationship |
|---|---|---|---|---|
| **APP** (Aggregate Production Planning) | The macro planning **process** of production, at product-family level (owner: "فرآیند برنامه‌ریزی کلان"). It produces MPP versions. | OWNER-CONFIRMED (ADR-001) | Process | Input to MPP |
| **MPP** | **Master Production Plan**: the approved, versioned **plan document or entity** that is the output of APP. It is not a computation engine and not a parallel module. | OWNER-CONFIRMED (ADR-001) | Versioned record | Output of APP; source for MPS |
| **MPS** (Master Production Schedule) | The detailed **production schedule of the products** derived from an approved MPP version (owner: "برنامه تفصیلی محصولات"). | OWNER-CONFIRMED (ADR-001) | Calculation and record | Derived from MPP; checked by RCCP |
| RCCP | Checks main resources for the MPS (owner proposal, 2026-11-13). | PROPOSED | Calculation | Checks MPS |
| MRP | Material and component requirements (owner proposal). | PROPOSED | Calculation | Uses MPS or production demand |
| CRP | Detailed capacity of work centres (owner proposal). | PROPOSED | Calculation | Uses planned orders and routing |
| PS | Operation scheduling and machine allocation (owner proposal). | PROPOSED | Scheduling | Uses CRP |
| APS | Advanced planning and scheduling (optimisation). Not a synonym for PS. | NEEDS OWNER DECISION | Engine | Possibly later |
| MRP II | Closed-loop planning with execution feedback (framework). | PROPOSED | Framework | Connects all of the above |
| S&OP | Sales and operations planning. | NEEDS OWNER DECISION | Process | Possibly out of v1 scope |
| ERP, MOM, MES | Enterprise and operations systems (reference only). | Reference | External | Integration boundary |

**Note on "routing":** four different things use this word in PEMS (document routing, letter routing, storage routing, workflow routing). The manufacturing operation sequence needs its own name. The naming decision is open (OD-03).

## 2. Confirmed architecture decision (ADR-001)
- **MPP = Master Production Plan**: the approved, versioned plan, which is the output of the APP process.
- **APP** is the process; **MPP** is the approved document or entity; **MPS** is the detailed product plan.
- **MPP must not create a separate computation engine or a parallel module.**
- Earlier owner wording on 2026-11-13 ("approved, versioned output of the comprehensive production plan") is **superseded** by ADR-001. Owner-confirmed wording: see `06-architecture-decision-proposal.md`.

## 3. Decision register

**Status note (2026-11-16):** the table below preserves the Stage 0 register as it stood on 2026-11-14. Its "NEEDS OWNER DECISION" values are not the current Stage 1 authorisation state. The authoritative later disposition is `audits/stage-00/Owner-Decisions-2026-11-16.md`; unresolved plant-specific parameters and later-scope decisions remain open.

| ID | Question | Status at 2026-11-14 | Source |
|---|---|---|---|
| OD-01 | Manufacturing mode: Process first (decided); Hybrid scope and order after Process | Process first: **OWNER-CONFIRMED**; Hybrid NEEDS OWNER DECISION | Owner 2026-11-13 |
| OD-02 | Product structure: BOM or formula, yield, co-products, versioning | NEEDS OWNER DECISION | Critical-Major register F1 |
| OD-03 | Manufacturing routing (operation sequence) and naming of "routing" | NEEDS OWNER DECISION | RoutingRule-Assessment |
| OD-04 | Plan hierarchy: APP process, MPP versions, MPS buckets and horizon | APP and MPP meaning: OWNER-CONFIRMED (ADR-001). Buckets and horizon: NEEDS OWNER DECISION | F4a, F4b |
| OD-05 | Capacity unit per work centre; calendar; shifts; efficiency; time basis | NEEDS OWNER DECISION | F3, F6, AD-08 |
| OD-06 | Production demand source; netting with procurement; release gate | NEEDS OWNER DECISION | F2, F5 |
| OD-07 | Finite or infinite capacity; PS and APS scope | NEEDS OWNER DECISION | F6 |
| OD-08 | Quality gate for inputs, outputs, recall | NEEDS OWNER DECISION | F10–F12 |
| OD-09 | Traceability: lot or serial; batch-level stock; genealogy depth | NEEDS OWNER DECISION | F13, F14 |
| OD-10 | Closure: tolerance, hold and cancel propagation, reconciliation tolerance, reversal | NEEDS OWNER DECISION | F7–F9, F15, X15 |
| OD-11 | Unit conversion policy; unit-safe variance | NEEDS OWNER DECISION | F16, F17, X2 |
| OD-12 | Method ownership: site, line, product, route or order | NEEDS OWNER DECISION | Owner 2026-11-13 |
| OD-13 | BOM and recipe data owner (engineering or PPC) | NEEDS OWNER DECISION | Owner 2026-11-13 |
| OD-14 | Sales order entity in scope, or only MR and forecast | NEEDS OWNER DECISION | Owner 2026-11-13 |
| OD-15 | BOM and plan release through WorkflowPlatform, with maker-checker | NEEDS OWNER DECISION (AD-02) | A2 |
| OD-16 | Planning engine boundary and Hangfire run model | NEEDS OWNER DECISION (AD-01) | A1 |
| OD-17 | Transactional audit | NEEDS OWNER DECISION (AD-03) | A3 |
| OD-18 | Row versions on production and stock | NEEDS OWNER DECISION (AD-04) | A4 |
| OD-19 | External keys for ERP and MES | NEEDS OWNER DECISION (AD-05) | A5 |
| OD-20 | Attribution rule for shared inventory transactions | NEEDS OWNER DECISION (AD-06) | A7 |
| OD-21 | UOM required on production lines | NEEDS OWNER DECISION (AD-07) | A9 |
| OD-22 | Effectivity and routing versioning | NEEDS OWNER DECISION | A11 (now partly resolved by RoutingRule assessment) |
| OD-23 | Compliance scope for PPC (any claim needs its own decision) | NEEDS OWNER DECISION | Audit-03 section 4 |
| OD-24 | Lightweight Operation Model: `ProcessFormulaOperation` + `ProductionRunOperationLog` (EBR) | STANDARDS-ALIGNED | ISA-88 §4 & §5.6 |
| OD-25 | ISO 22400 Time Model & OEE: PBT = POT - PDOT; Setup/Cleaning inside PBT | STANDARDS-ALIGNED | ISO 22400-2 §4.2, §6.5 |
| OD-26 | ATP-ready design: `DemandTimeFenceDays`, `PlanningTimeFenceDays`, `AvailableToPromise` | STANDARDS-ALIGNED | APICS ASCM CPIM |
| OD-27 | Product Family: APP targets `ProductFamily`; MPS targets `MaterialItem` | STANDARDS-ALIGNED | ISA-95 Part 1 §4.3 |
| OD-28 | Shared ProductionCalendar with CMMS; Turnaround blocks RCCP capacity | STANDARDS-ALIGNED | ISO 22400 + Petrochemical TAR |
| OD-29 | Three Lot-Sizing methods for v1 (LotForLot, FOQ, EOQ); `LotSizingPolicy` | STANDARDS-ALIGNED | APICS ASCM CPIM |

Crosswalk from the earlier pass report numbering: `audits/stage-00/Audit-Reconciliation-Report.md` section 5.

## 4. Terminology register (owner terms, 2026-11-14)
| Term | Owner meaning | Status | Note |
|---|---|---|---|
| APP | Aggregate Production Planning: macro planning at product-family level | OWNER-CONFIRMED (ADR-001) | Process that produces MPP versions |
| MPP | Master Production Plan: approved and version-controlled plan resulting from APP | OWNER-CONFIRMED (ADR-001) | Not a computation engine; must not create a parallel module |
| MPS | Master Production Schedule: detailed product schedule derived from an approved MPP version | OWNER-CONFIRMED (ADR-001) | Expansion as stated by the owner, 2026-11-14 |
| RCCP | Rough-Cut Capacity Planning: checks main resources for the MPS | OWNER TERM; definition PROPOSED | Not confirmed as final definition |
| MRP | Material Requirements Planning | OWNER TERM; definition PROPOSED | Not confirmed as final definition |
| CRP | Capacity Requirements Planning: detailed work-centre capacity | OWNER TERM; definition PROPOSED | Not confirmed as final definition |
| PS | Production Scheduling: operation scheduling and machine allocation | OWNER TERM; definition PROPOSED | Not confirmed as final definition |
| MRP II | Manufacturing Resource Planning: closed-loop framework | OWNER TERM; definition PROPOSED | Framework; not a module |
| APS | Advanced Planning and Scheduling | NEEDS OWNER DECISION | Scope undecided |
| S&OP | Sales and Operations Planning | NEEDS OWNER DECISION | Scope undecided |

**Rule (owner, 2026-11-14):** no independent computation engine is built for MPP or MRP II. MPP holds approved versions; calculations belong to the APP, MPS, RCCP, MRP, CRP and PS capabilities.
