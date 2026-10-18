# PEMS Operations Phase 2E — KPI / PI / RI / KRI Catalog

**Status:** Documentation only. No indicator listed here is implemented, calculated, stored, or
displayed anywhere in PEMS today. No numeric target is invented — every target/warning/critical
threshold not derivable from a fixed, universal formula (e.g., mass balance = 100%) is marked
`TARGET — BUSINESS APPROVAL REQUIRED`.

## 0. How to read this catalog

Every indicator carries, at minimum: ID, Name, Type, Definition, Formula, Unit, Grain,
Frequency, Data Source, Owner, Validator, Reviewer, Target, Warning, Critical, Direction,
Leading/Lagging, Domain, Decision Trigger, Escalation, Phase 3 status. Field meanings, used
identically for every metric below rather than re-explained per row:

| Field | Meaning |
|---|---|
| **Owner** | Accountable for the metric's definition and business use |
| **Validator** | Confirms the underlying data before it's reported (first line of defense against bad data) |
| **Reviewer** | Reviews the metric periodically and acts on deviation |
| **Direction** | Whether higher or lower is the desired direction |
| **Leading/Lagging** | Leading = warns before failure; Lagging = describes an already-occurred result |
| **Decision Trigger** | What action a breach of Warning/Critical is meant to cause |
| **Phase 3 status** | Whether the metric's data source exists today, or depends on unbuilt Packaging entities |

Metric-quality rules applied to every candidate (per Section 37 of the governing task): a
metric was **rejected** and is not listed if it lacked a reliable/authoritative data source
without duplicate manual entry, had no clear owner, had no decision use, or duplicated another
listed metric. Where two candidates measured essentially the same thing, they were consolidated
— see §9.

## 1. Metric Hierarchy

```
Enterprise / Site KPI        (Complex Manager view — cross-domain)
        ↓
Functional KPI                (Packaging Manager / Quality / Finance / Maintenance / HSE / Security — domain-owned)
        ↓
Operational PI                (Shift Supervisor / Operator-facing, day-to-day)
        ↓
Risk Indicator (RI)           (early/late warning of a specific operational risk)
        ↓
Key Risk Indicator (KRI)      (enterprise-level risk requiring management escalation)
```

Not every metric is forced into "KPI" — most of the catalog is intentionally PI/RI/KRI, per the
task's own instruction not to over-classify.

## 2. Data Lineage — the authoritative chain every metric must trace to

```
ProductionOrder → ProductionRun → ProductionBatch → ProductionOutput
                                                          ↓
                                          PackagingInput (proposed, Phase 3)
                                                          ↓
                                          PackagingRun → PackagingOutput (proposed, Phase 3)
                                                          ↓
                                                       Package (proposed, Phase 3)
                                                          ↓
                                          Quality Decision (Package.QualityStatus, proposed)
                                                          ↓
                                          InventoryTransaction (existing, ReferenceType extension)
                                                          ↓
                                          MaterialStock (existing, unchanged grain)
                                                          ↓
                                                        KPI
```

Every metric below states its actual source against this chain. **No metric in this catalog is
defined against a manually re-keyed duplicate quantity** where an authoritative transactional
source already exists or is planned (per §27 of the governing task) — where today's PEMS has no
such source yet (all Packaging-side metrics), the metric is marked `Phase 3 status: Future —
requires <entity>` rather than silently assuming manual entry is acceptable.

---

## 3. Production (existing scope — context for Packaging metrics, not new)

### KPI-PRD-01 — Production Attainment %
- **Type:** KPI · **Domain:** Production · **Leading/Lagging:** Lagging
- **Definition:** Actual production quantity vs. the planned quantity for the period.
- **Formula:** `Actual Production Quantity / Planned Production Quantity × 100`
- **Unit:** % · **Grain:** Per Production Order, rolled up per shift/day/period · **Frequency:** Daily
- **Data source:** `ProductionOutput` (actual) vs. `ProductionOrder.PlannedQuantity` (existing entities, unchanged)
- **Owner:** Operations Manager · **Validator:** Shift Supervisor · **Reviewer:** Operations Manager, Complex Manager (period roll-up)
- **Target:** `TARGET — BUSINESS APPROVAL REQUIRED` · **Warning:** — · **Critical:** —
- **Direction:** Higher is better
- **Required dimensions:** Product, Work Center, Shift, Date
- **Business purpose:** Core production planning-vs-actual signal.
- **Decision triggered:** Sustained shortfall → schedule/capacity review.
- **Escalation:** Shift Supervisor → Operations Manager → Complex Manager
- **Phase 3 status:** Data source exists today; metric is not currently calculated/displayed anywhere in PEMS (a reporting gap, not a Packaging gap).

### PI-PRD-01 — Production Throughput
- **Type:** PI · **Domain:** Production · **Leading/Lagging:** Lagging
- **Definition:** Quantity produced per unit time.
- **Formula:** `Total ProductionOutput Quantity / Operating Time`
- **Unit:** kg/hr or ton/hr (per material UOM) · **Grain:** Per Work Center, per shift · **Frequency:** Per shift
- **Data source:** `ProductionOutput`, `ShiftDefinition` (existing)
- **Owner:** Operations Manager · **Validator:** Shift Supervisor · **Reviewer:** Operations Manager
- **Target:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Higher is better
- **Required dimensions:** Work Center, Shift
- **Business purpose:** Capacity utilization visibility.
- **Decision triggered:** Persistent low throughput → bottleneck investigation (feeds RI-PRC-01).
- **Escalation:** Shift Supervisor → Operations Manager
- **Phase 3 status:** Data source exists; not currently calculated.

### KPI-PRD-02 — Production Yield %
- **Type:** KPI · **Domain:** Production · **Leading/Lagging:** Lagging
- **Definition:** Good output as a fraction of total input consumed.
- **Formula:** `ProductionOutput Quantity / ProductionInput Quantity × 100`
- **Unit:** % · **Grain:** Per Production Run · **Frequency:** Per run, rolled up daily
- **Data source:** `ProductionInput`, `ProductionOutput` (existing)
- **Owner:** Process Engineering Lead · **Validator:** Shift Supervisor · **Reviewer:** Engineering Manager
- **Target:** `TARGET — BUSINESS APPROVAL REQUIRED` (process-specific)
- **Direction:** Higher is better
- **Required dimensions:** Product, Work Center
- **Business purpose:** Process efficiency / mass-balance health signal, feeds RI-PRD-01.
- **Decision triggered:** Below-target yield → process deviation investigation.
- **Escalation:** Process Engineering Lead → Engineering Manager
- **Phase 3 status:** Data source exists; not currently calculated.

### RI-PRD-01 — Production Loss %
- **Type:** RI · **Domain:** Production · **Leading/Lagging:** Lagging
- **Definition:** Recorded loss as a fraction of total input.
- **Formula:** `ProductionLoss Quantity / ProductionInput Quantity × 100`
- **Unit:** % · **Grain:** Per run · **Frequency:** Per run, rolled up daily
- **Data source:** `ProductionLoss`, `ProductionInput` (existing)
- **Owner:** Process Engineering Lead · **Validator:** Shift Supervisor · **Reviewer:** Operations Manager
- **Target:** — · **Warning:** `TARGET — BUSINESS APPROVAL REQUIRED` · **Critical:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Lower is better
- **Required dimensions:** Loss Category, Product, Work Center
- **Business purpose:** Early signal of a process problem before it shows up as a financial variance.
- **Decision triggered:** Warning → supervisor review; Critical → Process Engineering investigation.
- **Escalation:** Shift Supervisor → Process Engineering Lead → Engineering Manager
- **Phase 3 status:** Data source exists; not currently calculated.

---

## 4. Packaging (new — depends entirely on Phase 3 entities)

Every metric in this section is `Phase 3 status: Future — requires PackagingRun/PackagingInput/PackagingOutput/PackagingLoss/Package`
and cannot be calculated until Phase 3 is built **and** D1–D6 are resolved (the formulas below
assume Phase 2D's recommended shapes; a different resolution of D1–D6 could change a formula's
exact terms, not its intent).

### KPI-PKG-01 — Packaging Attainment %
- **Type:** KPI · **Leading/Lagging:** Lagging
- **Definition:** Actual packaged quantity vs. planned (if a `PackagingOrder` tier is confirmed, D8) or vs. available released bulk (if not).
- **Formula:** `Actual PackagingOutput Quantity / Planned (or Available) Quantity × 100`
- **Unit:** % · **Grain:** Per Packaging Run, per line/shift · **Frequency:** Per shift, daily roll-up
- **Data source:** `PackagingOutput` vs. `PackagingOrder` or `ProductionBatch.Quantity` (Released)
- **Owner:** Packaging Manager · **Validator:** Operations Shift Supervisor · **Reviewer:** Operations Manager
- **Target:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Higher is better
- **Required dimensions:** Line, Shift, Product, Date
- **Business purpose:** Primary packaging planning-vs-actual signal, mirrors KPI-PRD-01.
- **Decision triggered:** Sustained shortfall → packaging capacity/scheduling review.
- **Escalation:** Shift Supervisor → Packaging Manager → Operations Manager

### KPI-PKG-02 — Packaging Yield %
- **Type:** KPI · **Leading/Lagging:** Lagging
- **Definition:** Good packaged output as a fraction of packaging input consumed.
- **Formula:** `Good Packaged Output / Packaging Input × 100`
- **Unit:** % · **Grain:** Per Packaging Run · **Frequency:** Per run, daily roll-up
- **Data source:** `PackagingInput`, `PackagingOutput`
- **Owner:** Packaging Manager · **Validator:** Operations Shift Supervisor · **Reviewer:** Process Engineering Lead
- **Target:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Higher is better
- **Required dimensions:** Product, Line
- **Business purpose:** Core packaging efficiency signal; also a mass-balance health check.
- **Decision triggered:** Below-target → investigate underfill/overfill and loss categories.
- **Escalation:** Packaging Manager → Process Engineering Lead

### RI-PKG-01 — Packaging Loss %
- **Type:** RI · **Leading/Lagging:** Lagging
- **Definition:** Recorded packaging loss as a fraction of packaging input.
- **Formula:** `Packaging Loss / Packaging Input × 100`
- **Unit:** % · **Grain:** Per run · **Frequency:** Per run, daily roll-up
- **Data source:** `PackagingLoss`, `PackagingInput`
- **Owner:** Packaging Manager · **Validator:** Operator (entry), Shift Supervisor (confirmation) · **Reviewer:** Process Engineering Lead
- **Target:** — · **Warning/Critical:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Lower is better
- **Required dimensions:** Loss Category (packaging-specific whitelist, per Phase 2D §15), Line, Product
- **Business purpose:** Early operational signal, feeds RI-FIN-02.
- **Decision triggered:** Warning → supervisor review; Critical → line stop / Packaging Manager review.
- **Escalation:** Operator → Shift Supervisor → Packaging Manager

### RI-PKG-02 — Packaging Mass-Balance Variance %
- **Type:** RI · **Leading/Lagging:** Lagging
- **Definition:** Unexplained difference between packaging input and (output + loss) — the
  portion the mass-balance invariant (Phase 2D §32) does not account for.
- **Formula:** `(Packaging Input − Packaging Output − Packaging Loss) / Packaging Input × 100`
- **Unit:** % · **Grain:** Per run · **Frequency:** Per run
- **Data source:** `PackagingInput`, `PackagingOutput`, `PackagingLoss`
- **Owner:** Packaging Manager · **Validator:** Operations Shift Supervisor · **Reviewer:** Finance Manager (financial impact), Process Engineering Lead (process cause)
- **Target:** 0% (invariant) · **Warning/Critical:** `TARGET — BUSINESS APPROVAL REQUIRED` (tolerance = Decision D11)
- **Direction:** Lower magnitude is better (never forced to zero — Phase 2D §32/D11)
- **Required dimensions:** Product, Line, Run
- **Business purpose:** Direct implementation of Phase 2D's mass-balance invariant as a monitored, never-silently-zeroed number.
- **Decision triggered:** Above tolerance (once D11 sets one) → variance investigation, feeds KRI-INV-01/KRI-FIN-01.
- **Escalation:** Packaging Manager → Operations Manager → Finance Manager (if above financial materiality)

### PI-PKG-01 — Package Count
- **Type:** PI · **Leading/Lagging:** Lagging
- **Definition:** Number of packages created.
- **Formula:** `COUNT(PackagingOutput rows resulting in a new/updated Package)`
- **Unit:** count · **Grain:** Per run, per shift · **Frequency:** Per shift
- **Data source:** `PackagingOutput`/`Package`
- **Owner:** Packaging Manager · **Validator:** Operator · **Reviewer:** Operations Shift Supervisor
- **Target:** — (operational count, not a target metric)
- **Direction:** N/A (informational)
- **Required dimensions:** Line, Product, Shift
- **Business purpose:** Basic operational volume count feeding KPI-PKG-01/PI-PKG-02.
- **Decision triggered:** None directly — input to other metrics.
- **Escalation:** N/A

### PI-PKG-02 — Average Package Net Quantity
- **Type:** PI · **Leading/Lagging:** Lagging
- **Definition:** Mean net quantity per package, vs. nominal/labeled quantity.
- **Formula:** `SUM(Package Net Quantity) / COUNT(Package)` compared to nominal fill target
- **Unit:** kg (or product UOM) · **Grain:** Per line, per product, per shift · **Frequency:** Per shift
- **Data source:** `Package` (Net, per Gross/Tare/Net model — Decision D4)
- **Owner:** Packaging Manager · **Validator:** Operator · **Reviewer:** Quality/Process Engineering
- **Target:** Nominal fill weight (product-specific) — `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Should track nominal within tolerance, not simply "higher/lower is better" (over-fill is a cost issue, under-fill is a compliance issue)
- **Required dimensions:** Product, Line
- **Business purpose:** Detects systematic over/under-filling — direct link to RI-PKG-01 and KPI-FIN-01.
- **Decision triggered:** Drift from nominal → scale calibration check, feeds a Maintenance work request.
- **Escalation:** Operator → Shift Supervisor → Packaging Manager

### PI-PKG-03 — Packaging Cycle Time
- **Type:** PI · **Leading/Lagging:** Lagging
- **Definition:** Elapsed time from `PackagingRun` start to completion.
- **Formula:** `PackagingRun.CompletedDate − PackagingRun.StartDate`
- **Unit:** minutes · **Grain:** Per run · **Frequency:** Per run
- **Data source:** `PackagingRun`
- **Owner:** Packaging Manager · **Validator:** Operations Shift Supervisor · **Reviewer:** Operations Manager
- **Target:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Lower is better (within a safe/quality-compliant range)
- **Required dimensions:** Product, Line
- **Business purpose:** Scheduling and capacity planning input.
- **Decision triggered:** Sustained increase → line/equipment investigation.
- **Escalation:** Shift Supervisor → Packaging Manager

### PI-PKG-04 — Packaging Line Utilization %
- **Type:** PI · **Leading/Lagging:** Lagging
- **Definition:** Time a packaging line actively ran vs. scheduled available time.
- **Formula:** `PackagingRun Active Time / Scheduled Available Time × 100`
- **Unit:** % · **Grain:** Per line, per shift · **Frequency:** Per shift, daily roll-up
- **Data source:** `PackagingRun`, `ShiftDefinition`
- **Owner:** Packaging Manager · **Validator:** Operations Shift Supervisor · **Reviewer:** Operations Manager
- **Target:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Higher is better
- **Required dimensions:** Line, Shift
- **Business purpose:** Capacity/bottleneck visibility, complements RI-MNT-02 (downtime attribution).
- **Decision triggered:** Persistent low utilization → scheduling or equipment reliability review.
- **Escalation:** Shift Supervisor → Packaging Manager → Operations Manager

### PI-PKG-05 — Rework Rate % *(deferred data source — see §9)*
- **Type:** PI · **Leading/Lagging:** Lagging
- **Definition:** Packages requiring rework (repack, relabel) as a fraction of total packaged.
- **Formula:** `COUNT(Package with rework event) / COUNT(Package) × 100`
- **Unit:** % · **Grain:** Per line, per shift · **Frequency:** Weekly
- **Data source:** Rework event — **not designed** in Phase 2D (explicitly deferred, §32/§42 of the technical specification)
- **Owner:** Packaging Manager · **Validator:** Operations Shift Supervisor · **Reviewer:** Quality/Finance
- **Target:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Lower is better
- **Required dimensions:** Product, Line, Reason
- **Business purpose:** Cost-of-quality and process-stability signal.
- **Decision triggered:** Rising trend → root-cause investigation (feeds RI-PRC-02).
- **Escalation:** Packaging Manager → Quality/Engineering
- **Phase 3 status:** `Metric Definition Gap` — data source does not exist and is explicitly out of scope until rework re-entry is designed (Phase 2D §32). Listed here as a placeholder definition only, not ready for Phase 3.

---

## 5. Quality

### KPI-QUA-01 — First-Pass Quality Rate %
- **Type:** KPI · **Domain:** Quality · **Leading/Lagging:** Lagging
- **Definition:** Packages/batches released on first quality decision, without a Blocked/Rejected/Retest detour.
- **Formula:** `COUNT(first decision = Released) / COUNT(total quality decisions) × 100`
- **Unit:** % · **Grain:** Per product, per period · **Frequency:** Daily, weekly roll-up
- **Data source:** `ProductionBatch.QualityStatus` transitions (existing) + `Package.QualityStatus` (Phase 3)
- **Owner:** Laboratory Manager · **Validator:** Laboratory technician (data entry) · **Reviewer:** Laboratory Manager, Quality leadership
- **Target:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Higher is better
- **Required dimensions:** Product, Line/Work Center
- **Business purpose:** Core quality-system health indicator.
- **Decision triggered:** Sustained decline → process/quality investigation.
- **Escalation:** Laboratory Manager → Engineering Manager / Complex Manager

### KPI-QUA-02 — Quality Release Cycle Time
- **Type:** KPI · **Leading/Lagging:** Lagging
- **Definition:** Time from "Produced"/"PendingQC" to a final Released/Blocked/Rejected decision.
- **Formula:** `QualityStatusChangedDate(final) − QualityStatusChangedDate(Produced/PendingQC)`
- **Unit:** hours · **Grain:** Per batch/package · **Frequency:** Per decision, daily roll-up
- **Data source:** `ProductionBatch.QualityStatusChangedDate` (existing field) + equivalent for `Package` (Phase 3)
- **Owner:** Laboratory Manager · **Validator:** Laboratory technician · **Reviewer:** Operations Manager (throughput impact)
- **Target:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Lower is better (without compromising thoroughness)
- **Required dimensions:** Product, Test type
- **Business purpose:** Directly affects how fast packaged product becomes shippable — a real operational cost of slow testing.
- **Decision triggered:** Sustained increase → lab resourcing/scheduling review.
- **Escalation:** Laboratory Manager → Operations Manager

### RI-QUA-01 — Rejection Rate %
- **Type:** RI · **Leading/Lagging:** Lagging
- **Definition:** Batches/packages rejected as a fraction of total decided.
- **Formula:** `COUNT(QualityStatus = Rejected) / COUNT(total quality decisions) × 100`
- **Unit:** % · **Grain:** Per product, per period · **Frequency:** Daily, weekly
- **Data source:** `ProductionBatch.QualityStatus` / `Package.QualityStatus` (Phase 3)
- **Owner:** Laboratory Manager · **Validator:** Laboratory technician · **Reviewer:** Quality leadership, Finance (cost impact via RI-FIN-01/02)
- **Target:** — · **Warning/Critical:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Lower is better
- **Required dimensions:** Product, Rejection reason
- **Business purpose:** Direct financial and reputational risk signal.
- **Decision triggered:** Critical → immediate process hold review.
- **Escalation:** Laboratory Manager → Engineering Manager → Complex Manager

### RI-QUA-02 — Blocked Product Rate %
- **Type:** RI · **Leading/Lagging:** Leading (a block precedes a final rejection or delayed release)
- **Definition:** Batches/packages currently in `Blocked` status as a fraction of total awaiting/recent decisions.
- **Formula:** `COUNT(QualityStatus = Blocked, currently) / COUNT(active batches/packages) × 100`
- **Unit:** % · **Grain:** Site-wide, per product · **Frequency:** Daily (point-in-time)
- **Data source:** `ProductionBatch.QualityStatus` / `Package.QualityStatus`
- **Owner:** Laboratory Manager · **Validator:** Laboratory technician · **Reviewer:** Warehouse Manager (stock availability impact)
- **Target:** — · **Warning/Critical:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Lower is better
- **Required dimensions:** Product, Age of block
- **Business purpose:** Early warning that inventory is becoming unavailable to ship; feeds PI-INV-01.
- **Decision triggered:** Aging blocks (open longer than a threshold) → Laboratory Manager escalation.
- **Escalation:** Laboratory Manager → Operations Manager → Warehouse Manager

### KRI-QUA-01 — Out-of-Specification (OOS) Rate %
- **Type:** KRI · **Leading/Lagging:** Leading (an early systemic-quality warning, distinct from the lagging Rejection Rate)
- **Definition:** Laboratory test results outside specification as a fraction of total tests.
- **Formula:** `COUNT(test result = OOS) / COUNT(total tests) × 100`
- **Unit:** % · **Grain:** Per product, per period · **Frequency:** Daily, weekly, monthly trend
- **Data source:** Laboratory test result — **does not exist in PEMS today** (no `QualityTestResult` entity, confirmed absent by Phase 2D §3/§7)
- **Owner:** Laboratory Manager · **Validator:** Laboratory technician · **Reviewer:** Engineering Manager, Complex Manager
- **Target:** — · **Warning/Critical:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Lower is better
- **Required dimensions:** Product, Test parameter
- **Business purpose:** Key enterprise-level risk signal — a rising OOS rate predicts rejection/recall risk before it materializes financially.
- **Decision triggered:** Critical → management escalation, process investigation, potential production hold.
- **Escalation:** Laboratory Manager → Complex Manager (KRI-level, direct escalation)
- **Phase 3 status:** `Metric Definition Gap` today — requires a `QualityTestResult` entity that is explicitly out-of-scope for both Phase 2D and Phase 2E (documentation only). Listed as a target-state KRI definition for when that entity is designed.

### PI-QUA-01 — Retest Rate %
- **Type:** PI · **Leading/Lagging:** Lagging
- **Definition:** Tests requiring a retest as a fraction of total tests.
- **Formula:** `COUNT(retest events) / COUNT(total tests) × 100`
- **Unit:** % · **Grain:** Per product · **Frequency:** Weekly
- **Data source:** Same gap as KRI-QUA-01 — depends on a future `QualityTestResult` entity
- **Owner:** Laboratory Manager · **Validator:** Laboratory technician · **Reviewer:** Laboratory Manager
- **Target:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Lower is better
- **Required dimensions:** Product, Test type
- **Business purpose:** Lab process-quality signal (distinct from product quality).
- **Decision triggered:** Rising trend → lab method/equipment review.
- **Escalation:** Laboratory Manager
- **Phase 3 status:** `Metric Definition Gap`, same reason as KRI-QUA-01.

### RI-QUA-03 — Quality Deviation Rate
- **Type:** RI · **Leading/Lagging:** Leading
- **Definition:** Formally logged quality deviations (process or product) per period.
- **Formula:** `COUNT(logged deviations) / period`
- **Unit:** count/period · **Grain:** Per product line · **Frequency:** Weekly
- **Data source:** No deviation-log entity exists today — conceptually mapped to a future `PackagingEvent`/quality-event mechanism (Phase 2D §16)
- **Owner:** Process Engineering Lead · **Validator:** Shift Supervisor · **Reviewer:** Engineering Manager
- **Target:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Lower is better
- **Required dimensions:** Product, Deviation category
- **Business purpose:** Feeds RI-PRC-01/RI-PRC-02 root-cause tracking.
- **Decision triggered:** Recurrence → formal investigation.
- **Escalation:** Process Engineering Lead → Engineering Manager
- **Phase 3 status:** Future — data source not yet designed at entity level.

---

## 6. Maintenance

### KPI-MNT-01 — Equipment Availability %
- **Type:** KPI · **Domain:** Maintenance · **Leading/Lagging:** Lagging
- **Formula:** `Available Time / Planned Production Time × 100`
- **Unit:** % · **Grain:** Per equipment/line · **Frequency:** Daily, monthly roll-up
- **Data source:** Existing CMMS Equipment/WorkOrder/downtime data (`PEMS.Domain` CMMS entities, unchanged by this phase)
- **Owner:** Maintenance Manager · **Validator:** Shift Supervisor (downtime logging) · **Reviewer:** Maintenance Manager, Operations Manager
- **Target:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Higher is better
- **Required dimensions:** Equipment, Line
- **Business purpose:** Foundational reliability metric; directly gates PI-PKG-04.
- **Decision triggered:** Decline → maintenance strategy review.
- **Escalation:** Maintenance Manager → Operations Manager

### KPI-MNT-02 — MTBF
- **Type:** KPI · **Leading/Lagging:** Lagging
- **Formula:** `Operating Time / Number of Relevant Failures`
- **Unit:** hours · **Grain:** Per equipment · **Frequency:** Monthly
- **Data source:** Existing CMMS WorkOrder/FailureRecord data
- **Owner:** Maintenance Manager · **Validator:** Maintenance planner · **Reviewer:** Maintenance Manager
- **Target:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Higher is better
- **Required dimensions:** Equipment, Failure mode
- **Business purpose:** Reliability engineering baseline; feeds Bad Actor identification (PI-MNT-02).
- **Decision triggered:** Declining trend → reliability program review.
- **Escalation:** Maintenance Manager → Engineering Manager

### KPI-MNT-03 — MTTR
- **Type:** KPI · **Leading/Lagging:** Lagging
- **Formula:** `Total Corrective Maintenance Time / Number of Relevant Failures`
- **Unit:** hours · **Grain:** Per equipment · **Frequency:** Monthly
- **Data source:** Existing CMMS WorkOrder data
- **Owner:** Maintenance Manager · **Validator:** Maintenance planner · **Reviewer:** Maintenance Manager
- **Target:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Lower is better
- **Required dimensions:** Equipment
- **Business purpose:** Repair-responsiveness signal, directly affects packaging downtime (RI-MNT-02).
- **Decision triggered:** Rising trend → spares/resourcing review.
- **Escalation:** Maintenance Manager

### PI-MNT-01 — Planned Maintenance Compliance %
- **Type:** PI · **Leading/Lagging:** Leading
- **Formula:** `Completed PM Work Orders (on time) / Scheduled PM Work Orders × 100`
- **Unit:** % · **Grain:** Per equipment class · **Frequency:** Weekly, monthly
- **Data source:** Existing CMMS MaintenancePlan/WorkOrder data
- **Owner:** Maintenance Manager · **Validator:** Maintenance planner · **Reviewer:** Maintenance Manager
- **Target:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Higher is better
- **Required dimensions:** Equipment class
- **Business purpose:** Leading indicator — PM slippage predicts future breakdowns.
- **Decision triggered:** Below target → planning capacity review.
- **Escalation:** Maintenance planner → Maintenance Manager

### RI-MNT-01 — Breakdown Rate
- **Type:** RI · **Leading/Lagging:** Lagging
- **Formula:** `COUNT(unplanned corrective Work Orders) / period`
- **Unit:** count/period · **Grain:** Per equipment/line · **Frequency:** Weekly
- **Data source:** Existing CMMS WorkOrder data
- **Owner:** Maintenance Manager · **Validator:** Shift Supervisor (reporting), Maintenance planner (confirmation) · **Reviewer:** Maintenance Manager
- **Target:** — · **Warning/Critical:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Lower is better
- **Required dimensions:** Equipment, Failure mode
- **Business purpose:** Direct packaging-continuity risk signal.
- **Decision triggered:** Critical → reliability escalation.
- **Escalation:** Maintenance Manager → Operations Manager

### KRI-MNT-01 — Maintenance Backlog (age-weighted)
- **Type:** KRI · **Leading/Lagging:** Leading
- **Definition:** Open corrective work orders, weighted by age and criticality of the affected equipment.
- **Formula:** `Σ (Open WO age in days × Equipment Criticality Weight)`
- **Unit:** weighted-days · **Grain:** Site-wide, per line · **Frequency:** Weekly
- **Data source:** Existing CMMS WorkOrder + EquipmentCriticality data
- **Owner:** Maintenance Manager · **Validator:** Maintenance planner · **Reviewer:** Maintenance Manager, Complex Manager
- **Target:** — · **Warning/Critical:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Lower is better
- **Required dimensions:** Equipment criticality class, Line
- **Business purpose:** Enterprise risk signal — an aging, criticality-weighted backlog predicts future packaging-line unavailability before a breakdown occurs.
- **Decision triggered:** Critical → resourcing/outsourcing decision by management.
- **Escalation:** Maintenance Manager → Complex Manager

### PI-MNT-02 — Bad Actor Contribution %
- **Type:** PI · **Leading/Lagging:** Lagging
- **Formula:** `Downtime from top N "bad actor" equipment / Total downtime × 100`
- **Unit:** % · **Grain:** Site-wide · **Frequency:** Monthly
- **Data source:** Existing CMMS reliability data (Bad Actor analysis already referenced in prior Operations phases)
- **Owner:** Maintenance Manager · **Validator:** Maintenance planner · **Reviewer:** Maintenance Manager, Engineering Manager
- **Target:** — (diagnostic, not a target metric)
- **Direction:** N/A (diagnostic prioritization)
- **Required dimensions:** Equipment
- **Business purpose:** Prioritizes reliability investment.
- **Decision triggered:** Identifies capital/engineering-change candidates.
- **Escalation:** Maintenance Manager → Engineering Manager

### RI-MNT-02 — Maintenance-Related Packaging Downtime %
- **Type:** RI · **Leading/Lagging:** Lagging
- **Definition:** The fraction of packaging line downtime attributable to equipment/maintenance causes (as opposed to material, quality-hold, or scheduling causes).
- **Formula:** `Packaging Downtime (Maintenance cause) / Total Packaging Downtime × 100`
- **Unit:** % · **Grain:** Per line · **Frequency:** Per shift, weekly roll-up
- **Data source:** `PackagingRun` downtime/pause events (Phase 3) cross-referenced with CMMS WorkOrder cause codes (existing)
- **Owner:** Maintenance Manager and Packaging Manager (joint — see RACI) · **Validator:** Shift Supervisor · **Reviewer:** Operations Manager
- **Target:** — · **Warning/Critical:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Lower is better
- **Required dimensions:** Line, Cause code
- **Business purpose:** This is the single required Maintenance↔Packaging integration metric (governing task §6). It is the **only** downtime-attribution metric in this catalog — see §9 for the consolidation this avoided.
- **Decision triggered:** Rising trend → joint Maintenance/Packaging reliability review.
- **Escalation:** Packaging Manager + Maintenance Manager → Operations Manager
- **Phase 3 status:** Future — requires `PackagingRun` downtime capture.

---

## 7. Inventory

### KPI-INV-01 — Inventory Accuracy %
- **Type:** KPI · **Domain:** Inventory · **Leading/Lagging:** Lagging
- **Formula:** `1 − (|System Quantity − Physical Count Quantity| / System Quantity) × 100`, averaged across cycle counts
- **Unit:** % · **Grain:** Per warehouse/location · **Frequency:** Per cycle count, monthly roll-up
- **Data source:** `MaterialStock` (existing) vs. physical cycle count (existing process if any; count-capture mechanism outside this phase's scope)
- **Owner:** Warehouse Manager · **Validator:** Warehouse clerk (count entry) · **Reviewer:** Warehouse Manager, Finance Manager
- **Target:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Higher is better
- **Required dimensions:** Warehouse, Location, Material
- **Business purpose:** Core inventory-integrity signal.
- **Decision triggered:** Below target → cycle-count frequency increase, process review.
- **Escalation:** Warehouse Manager → Finance Manager

### PI-INV-01 — Released-Stock %
- **Type:** PI · **Leading/Lagging:** Lagging
- **Formula:** `Released Quantity / Total On-Hand Quantity × 100`
- **Unit:** % · **Grain:** Per material, per warehouse · **Frequency:** Daily (point-in-time)
- **Data source:** `MaterialStock` cross-referenced with `ProductionBatch.QualityStatus`/`Package.QualityStatus` (once posted per-batch/package, requires the traceability chain in §2)
- **Owner:** Warehouse Manager · **Validator:** Laboratory Manager (quality-status accuracy) · **Reviewer:** Operations Manager
- **Target:** — (informational; complements RI-QUA-02)
- **Direction:** Higher is better (more sellable stock available)
- **Required dimensions:** Material, Warehouse
- **Business purpose:** Direct visibility of shippable stock — the Warehouse Manager's primary daily number (governing task §18/§32).
- **Decision triggered:** Sudden drop → investigate a quality hold spike.
- **Escalation:** Warehouse Manager → Operations Manager
- **Phase 3 status:** Partially available today for `ProductionBatch`-level stock; Package-level requires Phase 3.

### RI-INV-01 — Blocked-Stock %
- **Type:** RI · **Leading/Lagging:** Leading
- **Formula:** `Blocked Quantity / Total On-Hand Quantity × 100`
- **Unit:** % · **Grain:** Per material, per warehouse · **Frequency:** Daily
- **Data source:** Same as PI-INV-01
- **Owner:** Warehouse Manager · **Validator:** Laboratory Manager · **Reviewer:** Warehouse Manager, Laboratory Manager
- **Target:** — · **Warning/Critical:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Lower is better
- **Required dimensions:** Material, Warehouse, Block age
- **Business purpose:** Capital tied up in unshippable stock; complements RI-QUA-02 from the Warehouse side.
- **Decision triggered:** Aging blocked stock → Laboratory Manager escalation for disposition.
- **Escalation:** Warehouse Manager → Laboratory Manager → Finance Manager (write-off risk)

### KRI-INV-01 — Inventory Variance Value (Unexplained)
- **Type:** KRI · **Leading/Lagging:** Lagging
- **Definition:** The financial value of inventory adjustments that lack a linked, approved
  business reason (i.e., not tied to a normal receipt/issue/return/transfer with a valid
  reference).
- **Formula:** `Σ (|Adjustment Quantity| × Unit Cost)` for adjustments without an approved linked reference
- **Unit:** currency · **Grain:** Site-wide · **Frequency:** Weekly, monthly
- **Data source:** `InventoryTransaction` (existing, `TransactionType = Adjustment`)
- **Owner:** Finance Manager · **Validator:** Warehouse Manager (adjustment entry) · **Reviewer:** Finance Manager, Security Manager
- **Target:** — · **Warning/Critical:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Lower is better (ideally zero)
- **Required dimensions:** Material, Warehouse, Adjustment reason
- **Business purpose:** Enterprise risk signal for both error and potential abuse — directly required by the governing task's SoD/Security sections (§7, §20).
- **Decision triggered:** Any Critical breach → Security Manager investigation per the SoD boundary in §8.
- **Escalation:** Finance Manager → Security Manager → Complex Manager

### KRI-INV-02 — Traceability Completeness %
- **Type:** KRI · **Leading/Lagging:** Leading
- **Definition:** Fraction of released/shipped packages whose full genealogy chain
  (`ProductionOrder → ProductionRun → ProductionBatch → PackagingInput → PackagingOutput → Package`)
  resolves without a broken link.
- **Formula:** `COUNT(Package with unbroken genealogy chain) / COUNT(total Package) × 100`
- **Unit:** % · **Grain:** Site-wide · **Frequency:** Weekly
- **Data source:** Full chain in §2 (Phase 3)
- **Owner:** Packaging Manager · **Validator:** Operations Shift Supervisor · **Reviewer:** Quality, Security Manager
- **Target:** 100% · **Warning/Critical:** `TARGET — BUSINESS APPROVAL REQUIRED` (any gap below 100% should be treated seriously — this is a recall-readiness metric)
- **Direction:** Higher is better (100% is the only acceptable steady state)
- **Required dimensions:** Product, Line
- **Business purpose:** Direct recall-readiness / product-safety risk indicator — the single most important KRI in this catalog for regulatory/customer-safety exposure.
- **Decision triggered:** Any gap → immediate investigation, not a routine review.
- **Escalation:** Packaging Manager → Complex Manager (direct — this KRI bypasses the normal chain given its severity)
- **Phase 3 status:** Future — the entire chain must exist and be enforced (Phase 2D's genealogy design, §21-23).

### PI-INV-02 — Stock Reconciliation Completion %
- **Type:** PI · **Leading/Lagging:** Lagging
- **Formula:** `Reconciled Cycle Counts Completed / Cycle Counts Scheduled × 100`
- **Unit:** % · **Grain:** Per warehouse · **Frequency:** Monthly
- **Data source:** Existing cycle-count process (outside this phase's scope to define further)
- **Owner:** Warehouse Manager · **Validator:** Warehouse clerk · **Reviewer:** Finance Manager
- **Target:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Higher is better
- **Required dimensions:** Warehouse
- **Business purpose:** Process-compliance signal feeding KPI-INV-01's reliability.
- **Decision triggered:** Below target → count-scheduling review.
- **Escalation:** Warehouse Manager

### PI-INV-03 — Inventory Posting Latency
- **Type:** PI · **Leading/Lagging:** Lagging
- **Definition:** Time between `PackagingOutput` (or `ProductionOutput`) creation and its corresponding `InventoryTransaction` posting.
- **Formula:** `InventoryTransaction.CreatedDate − PackagingOutput.CreatedDate` (average)
- **Unit:** hours · **Grain:** Per line · **Frequency:** Daily
- **Data source:** `PackagingOutput`/`ProductionOutput`, `InventoryTransaction` (opt-in posting model, Phase 2D §27-28)
- **Owner:** Warehouse Manager · **Validator:** Operations Shift Supervisor · **Reviewer:** Warehouse Manager
- **Target:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Lower is better
- **Required dimensions:** Line, Product
- **Business purpose:** Since posting is opt-in and quality-gated (Phase 2C/2D), a large latency signals a bottleneck in the release-to-post chain, not just a reporting delay.
- **Decision triggered:** Rising latency → investigate the release/posting workflow, not just data entry.
- **Escalation:** Warehouse Manager → Operations Manager

---

## 8. Finance

### KPI-FIN-01 — Packaging Cost per Unit
- **Type:** KPI · **Domain:** Finance · **Leading/Lagging:** Lagging
- **Formula:** `Total Packaging Cost (material + loss + labor, as defined by Finance) / Total Units Packaged`
- **Unit:** currency/unit · **Grain:** Per product, per period · **Frequency:** Monthly (period close)
- **Data source:** `PackagingInput`/`PackagingLoss` quantities × standard/actual cost (existing Finance costing mechanism — cost rates are a Finance master data concern, not defined by this phase)
- **Owner:** Finance Manager · **Validator:** Packaging Manager (quantity accuracy) · **Reviewer:** Finance Manager, Complex Manager
- **Target:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Lower is better (without compromising quality)
- **Required dimensions:** Product, Period
- **Business purpose:** Core packaging profitability input.
- **Decision triggered:** Above target → cost driver investigation (loss, labor, material).
- **Escalation:** Finance Manager → Complex Manager
- **Phase 3 status:** Depends on Phase 3 quantities and a Finance accounting-treatment decision — explicitly `BUSINESS DECISION REQUIRED` (governing task §7); this catalog does not invent the accounting rule.

### KPI-FIN-02 — Packaging Cost per Ton/Kg
- **Type:** KPI · **Leading/Lagging:** Lagging
- **Formula:** `Total Packaging Cost / Total Net Quantity Packaged (ton or kg)`
- **Unit:** currency/ton or currency/kg · **Grain:** Per product, per period · **Frequency:** Monthly
- **Data source:** Same as KPI-FIN-01
- **Owner:** Finance Manager · **Validator:** Packaging Manager · **Reviewer:** Finance Manager
- **Target:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Lower is better
- **Required dimensions:** Product, Period
- **Business purpose:** Weight-normalized cost comparability across package sizes.
- **Decision triggered:** Same as KPI-FIN-01
- **Escalation:** Finance Manager
- **Phase 3 status:** Same as KPI-FIN-01.

### RI-FIN-01 — Production Loss Cost
- **Type:** RI · **Leading/Lagging:** Lagging
- **Formula:** `ProductionLoss Quantity × Standard Cost`
- **Unit:** currency · **Grain:** Per period · **Frequency:** Monthly
- **Data source:** `ProductionLoss` (existing) × Finance cost rate
- **Owner:** Finance Manager · **Validator:** Process Engineering Lead · **Reviewer:** Finance Manager, Operations Manager
- **Target:** — · **Warning/Critical:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Lower is better
- **Required dimensions:** Product, Loss category
- **Business purpose:** Financial translation of RI-PRD-01.
- **Decision triggered:** Critical → joint Finance/Operations review.
- **Escalation:** Finance Manager → Operations Manager

### RI-FIN-02 — Packaging Loss Cost
- **Type:** RI · **Leading/Lagging:** Lagging
- **Formula:** `PackagingLoss Quantity × Standard Cost`
- **Unit:** currency · **Grain:** Per period · **Frequency:** Monthly
- **Data source:** `PackagingLoss` (Phase 3) × Finance cost rate
- **Owner:** Finance Manager · **Validator:** Packaging Manager · **Reviewer:** Finance Manager, Operations Manager
- **Target:** — · **Warning/Critical:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Lower is better
- **Required dimensions:** Product, Loss category
- **Business purpose:** Financial translation of RI-PKG-01.
- **Decision triggered:** Critical → joint Finance/Packaging review.
- **Escalation:** Finance Manager → Packaging Manager

### KRI-FIN-01 — Large/Unusual Inventory Adjustment Value
- **Type:** KRI · **Leading/Lagging:** Leading
- **Definition:** Same underlying transactions as KRI-INV-01, viewed from a financial-materiality
  threshold rather than an "unexplained reference" test — **intentionally not a duplicate**: an
  adjustment can be properly referenced (passes KRI-INV-01) and still be unusually large in
  value (fails this KRI), or vice versa. See §9 for why these two are kept distinct.
- **Formula:** `Adjustment Value > Materiality Threshold` (flag, not a ratio)
- **Unit:** currency · **Grain:** Per transaction · **Frequency:** Real-time flagging, weekly review
- **Data source:** `InventoryTransaction`
- **Owner:** Finance Manager · **Validator:** Warehouse Manager · **Reviewer:** Finance Manager, Security Manager
- **Target:** — · **Warning/Critical:** `TARGET — BUSINESS APPROVAL REQUIRED` (materiality threshold is a Finance policy decision)
- **Direction:** Fewer/lower-value flagged events is better
- **Required dimensions:** Material, Warehouse, Approver
- **Business purpose:** SoD-relevant financial control (governing task §7/§20).
- **Decision triggered:** Any flagged event → Finance Manager review before period close.
- **Escalation:** Finance Manager → Security Manager (if pattern suggests SoD bypass)

### PI-FIN-01 — Cost of Quality
- **Type:** PI · **Leading/Lagging:** Lagging
- **Formula:** `Rejection Cost + Rework Cost + Blocked-Stock Carrying Cost` (components defined by Finance)
- **Unit:** currency · **Grain:** Per period · **Frequency:** Monthly
- **Data source:** RI-QUA-01 (Rejection Rate) × cost, PI-PKG-05 (Rework, gapped), RI-INV-01 (Blocked Stock) × carrying cost
- **Owner:** Finance Manager · **Validator:** Laboratory Manager, Warehouse Manager · **Reviewer:** Finance Manager, Complex Manager
- **Target:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Lower is better
- **Required dimensions:** Product, Period
- **Business purpose:** Single rolled-up quality-cost number for management review.
- **Decision triggered:** Rising trend → cross-functional quality review.
- **Escalation:** Finance Manager → Complex Manager
- **Phase 3 status:** Partially gapped — depends on PI-PKG-05's rework data source, which does not exist yet.

---

## 9. HSE

### KPI-HSE-01 — Recordable Incident Rate
- **Type:** KPI · **Domain:** HSE · **Leading/Lagging:** Lagging
- **Formula:** `Recordable Incidents × 200,000 / Total Hours Worked` (standard OSHA-style rate — `SITE / LEGAL CONFIRMATION REQUIRED` if a different site/legal standard applies)
- **Unit:** rate · **Grain:** Site-wide, per area · **Frequency:** Monthly
- **Data source:** No HSE incident-logging entity exists in PEMS today — conceptual, not built
- **Owner:** HSE Manager · **Validator:** Shift Supervisor (initial report) · **Reviewer:** HSE Manager, Complex Manager
- **Target:** `SITE / LEGAL CONFIRMATION REQUIRED`
- **Direction:** Lower is better
- **Required dimensions:** Area, Incident type
- **Business purpose:** Core HSE performance indicator.
- **Decision triggered:** Any recordable → formal incident investigation.
- **Escalation:** HSE Manager → Complex Manager
- **Phase 3 status:** `Metric Definition Gap` — no data source exists; out of scope for Phase 2E/3 packaging work specifically, listed for completeness per governing task §16/§25.

### RI-HSE-01 — Near-Miss Count
- **Type:** RI · **Leading/Lagging:** Leading
- **Formula:** `COUNT(logged near-miss reports) / period`
- **Unit:** count/period · **Grain:** Per area · **Frequency:** Weekly
- **Data source:** Same gap as KPI-HSE-01
- **Owner:** HSE Manager · **Validator:** Operator (reporting), Shift Supervisor · **Reviewer:** HSE Manager
- **Target:** — (higher near-miss reporting is often desired — a proxy for safety-culture engagement, not simply "lower is better")
- **Direction:** Context-dependent — `BUSINESS DECISION REQUIRED` whether the target is to increase reporting (culture) or decrease occurrence (outcome); the two require different interpretation
- **Required dimensions:** Area, Hazard type
- **Business purpose:** Leading safety-culture signal.
- **Decision triggered:** Sudden drop in reporting → investigate under-reporting; sudden rise in a specific hazard type → targeted intervention.
- **Escalation:** HSE Manager
- **Phase 3 status:** `Metric Definition Gap`.

### KRI-HSE-01 — Open HSE Action Aging
- **Type:** KRI · **Leading/Lagging:** Leading
- **Formula:** `COUNT(open HSE corrective actions older than threshold)`
- **Unit:** count · **Grain:** Site-wide · **Frequency:** Weekly
- **Data source:** Same gap as KPI-HSE-01
- **Owner:** HSE Manager · **Validator:** HSE Manager · **Reviewer:** Complex Manager
- **Target:** 0 · **Warning/Critical:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Lower is better
- **Required dimensions:** Area, Action type
- **Business purpose:** Enterprise risk signal — unresolved HSE actions are a direct escalation trigger per the governing task's HSE governance requirement (§16).
- **Decision triggered:** Any aging beyond threshold → Complex Manager escalation.
- **Escalation:** HSE Manager → Complex Manager
- **Phase 3 status:** `Metric Definition Gap`.

### RI-HSE-02 — Packaging-Related Incidents/Near-Misses
- **Type:** RI · **Leading/Lagging:** Lagging (incidents) / Leading (near-misses component)
- **Formula:** `COUNT(HSE events tagged to a Packaging area or PackagingRun)`
- **Unit:** count/period · **Grain:** Per line · **Frequency:** Monthly
- **Data source:** Same HSE gap, cross-tagged to Packaging (Phase 3)
- **Owner:** HSE Manager and Packaging Manager (joint) · **Validator:** Operator · **Reviewer:** HSE Manager
- **Target:** — · **Warning/Critical:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Lower is better
- **Required dimensions:** Line, Hazard type
- **Business purpose:** The specific Packaging↔HSE integration point required by governing task §16.
- **Decision triggered:** Any packaging-tagged incident → HSE Manager + Packaging Manager joint review.
- **Escalation:** HSE Manager + Packaging Manager → Complex Manager
- **Phase 3 status:** `Metric Definition Gap`.

### KPI-HSE-02 — Stop-Work Events
- **Type:** KPI · **Leading/Lagging:** Leading
- **Formula:** `COUNT(stop-work invocations) / period`
- **Unit:** count/period · **Grain:** Site-wide, per area · **Frequency:** Monthly
- **Data source:** No stop-work logging exists today — conceptual
- **Owner:** HSE Manager · **Validator:** Whoever invoked stop-work (any authorized role, per governing task §16) · **Reviewer:** HSE Manager, Complex Manager
- **Target:** — (informational — a stop-work event is inherently a positive safety action, not something to minimize as a target)
- **Direction:** N/A (event log, not optimized toward a direction)
- **Required dimensions:** Area, Trigger reason
- **Business purpose:** Confirms the stop-work authority (governing task §16) is actually being used, and tracks resolution.
- **Decision triggered:** Every event → mandatory review, regardless of count.
- **Escalation:** HSE Manager → Complex Manager
- **Phase 3 status:** `Metric Definition Gap`.

### PI-HSE-01 — Unsafe Condition Closure Time
- **Type:** PI · **Leading/Lagging:** Lagging
- **Formula:** `Closure Date − Report Date`, averaged
- **Unit:** days · **Grain:** Per area · **Frequency:** Monthly
- **Data source:** Same HSE gap
- **Owner:** HSE Manager · **Validator:** HSE Manager · **Reviewer:** HSE Manager
- **Target:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Lower is better
- **Required dimensions:** Area, Severity
- **Business purpose:** Process-responsiveness signal, complements KRI-HSE-01.
- **Decision triggered:** Rising average → resourcing review.
- **Escalation:** HSE Manager
- **Phase 3 status:** `Metric Definition Gap`.

---

## 10. Security

### KRI-SEC-01 — Unauthorized Movement Events
- **Type:** KRI · **Domain:** Security · **Leading/Lagging:** Leading
- **Definition:** Inventory or package movements recorded without a matching authorized transaction/approval.
- **Formula:** `COUNT(movement events without a linked authorized InventoryTransaction/approval)`
- **Unit:** count/period · **Grain:** Per warehouse · **Frequency:** Weekly
- **Data source:** `InventoryTransaction` cross-referenced with authorization audit trail (existing `AuditLog`)
- **Owner:** Security Manager · **Validator:** Warehouse Manager · **Reviewer:** Security Manager, Finance Manager
- **Target:** 0 · **Warning/Critical:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Lower is better
- **Required dimensions:** Warehouse, Material
- **Business purpose:** Core security-control signal for the material/package movement boundary (governing task §17).
- **Decision triggered:** Any event → immediate Security Manager investigation.
- **Escalation:** Security Manager → Complex Manager

### RI-SEC-01 — Barcode Replacement Rate
- **Type:** RI · **Leading/Lagging:** Leading
- **Formula:** `COUNT(BarcodeIdentity.Status = Replaced events) / COUNT(total active identities) × 100`
- **Unit:** % · **Grain:** Per target type (Package, once Phase 3 registers it) · **Frequency:** Weekly
- **Data source:** `BarcodeIdentity` (existing, `ReplacedByBarcodeIdentityId`/`ReplacesBarcodeIdentityId` chain)
- **Owner:** Security Manager · **Validator:** Packaging Manager (physical relabeling confirmation) · **Reviewer:** Security Manager
- **Target:** — · **Warning/Critical:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Lower is better (an elevated rate can indicate label failure quality issues or, in the extreme, identity fraud)
- **Required dimensions:** Target type, Line
- **Business purpose:** Anomaly-detection input for the identity-integrity boundary (governing task §17).
- **Decision triggered:** Anomalous spike → Security Manager pattern review.
- **Escalation:** Security Manager
- **Phase 3 status:** Mechanism exists today (`BarcodeIdentity`); metric becomes meaningful for Packaging once `Package` is a registered target type.

### KRI-SEC-02 — Suspicious Inventory Adjustment Events
- **Type:** KRI · **Leading/Lagging:** Leading
- **Definition:** Adjustments matching a defined suspicious pattern (e.g., same user repeatedly
  adjusting the same material, adjustments immediately before/after a shift change, adjustments
  bypassing the SoD pairing).
- **Formula:** Rule-based flag count (rules to be defined by Security/Finance jointly — not invented here)
- **Unit:** count/period · **Grain:** Site-wide · **Frequency:** Weekly
- **Data source:** `InventoryTransaction`, `AuditLog` (existing)
- **Owner:** Security Manager · **Validator:** Finance Manager (pattern definition) · **Reviewer:** Security Manager, Complex Manager
- **Target:** 0 · **Warning/Critical:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Lower is better
- **Required dimensions:** User, Material, Time pattern
- **Business purpose:** Direct implementation of governing task §17's "suspicious inventory adjustment" requirement.
- **Decision triggered:** Any flag → Security Manager investigation, independent of Finance's own KRI-FIN-01 review.
- **Escalation:** Security Manager → Complex Manager
- **Phase 3 status:** Rule definition is a `BUSINESS DECISION REQUIRED` item, not a data-availability gap — `AuditLog`/`InventoryTransaction` already capture the raw data needed.

### PI-SEC-01 — Shipment Discrepancy Rate *(forward-looking — Shipment is out of scope)*
- **Type:** PI · **Leading/Lagging:** Lagging
- **Formula:** `COUNT(shipments with quantity/identity discrepancy) / COUNT(total shipments) × 100`
- **Unit:** % · **Grain:** Per period · **Frequency:** Monthly
- **Data source:** No Shipment/Dispatch entity exists in PEMS (explicitly out of scope, Phase 2D §44)
- **Owner:** Security Manager and Warehouse Manager (joint) · **Validator:** Warehouse Manager · **Reviewer:** Security Manager
- **Target:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Lower is better
- **Required dimensions:** Customer/destination, Material
- **Business purpose:** Listed for completeness per governing task §17; genuinely out of scope until a Shipment/Dispatch module exists.
- **Decision triggered:** N/A until built
- **Escalation:** N/A until built
- **Phase 3 status:** `Metric Definition Gap` — out of Phase 3 Packaging scope entirely, dependent on a future Shipment module not designed anywhere in PEMS.

### RI-SEC-02 — Access Violation Count
- **Type:** RI · **Leading/Lagging:** Leading
- **Formula:** `COUNT(unauthorized access attempts to Packaging/Warehouse functions)`
- **Unit:** count/period · **Grain:** Site-wide · **Frequency:** Weekly
- **Data source:** Existing `AuditLog`/authentication/authorization audit records (`AccessControlService`)
- **Owner:** Security Manager · **Validator:** Security Manager · **Reviewer:** Security Manager
- **Target:** — · **Warning/Critical:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Lower is better
- **Required dimensions:** Module, User
- **Business purpose:** Standard access-control monitoring, extended to the new Packaging module once it exists.
- **Decision triggered:** Pattern of violations → Security Manager investigation.
- **Escalation:** Security Manager
- **Phase 3 status:** Mechanism exists today; Packaging-specific coverage depends on the module being registered (Decision D-Packaging-Module, governance baseline §8).

---

## 11. Process / Engineering

### RI-PRC-01 — Process Deviation Rate
- **Type:** RI · **Domain:** Process/Engineering · **Leading/Lagging:** Leading
- **Formula:** `COUNT(process parameters outside acceptable range) / COUNT(total parameter readings) × 100`
- **Unit:** % · **Grain:** Per process/line · **Frequency:** Daily
- **Data source:** Process parameter capture — not currently a structured entity in PEMS for Packaging; for Production, depends on whatever process-data capture exists today (not audited further, out of this phase's entity scope)
- **Owner:** Process Engineering Lead · **Validator:** Shift Supervisor · **Reviewer:** Engineering Manager
- **Target:** — · **Warning/Critical:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Lower is better
- **Required dimensions:** Process/line, Parameter
- **Business purpose:** Direct link required by governing task §13 (Production → Packaging → Quality → Process Performance → Deviation/Investigation chain).
- **Decision triggered:** Critical → technical investigation, potential production/packaging hold.
- **Escalation:** Process Engineering Lead → Engineering Manager
- **Phase 3 status:** `Metric Definition Gap` for structured parameter capture.

### PI-PRC-01 — Parameter Excursion Count
- **Type:** PI · **Leading/Lagging:** Leading
- **Formula:** `COUNT(individual out-of-range parameter events)`
- **Unit:** count/period · **Grain:** Per process/line · **Frequency:** Daily
- **Data source:** Same gap as RI-PRC-01
- **Owner:** Process Engineering Lead · **Validator:** Shift Supervisor · **Reviewer:** Process Engineering Lead
- **Target:** — (diagnostic detail feeding RI-PRC-01)
- **Direction:** Lower is better
- **Required dimensions:** Parameter, Line
- **Business purpose:** Granular diagnostic detail behind RI-PRC-01.
- **Decision triggered:** Feeds RI-PRC-01's aggregate review.
- **Escalation:** Process Engineering Lead

### KPI-PRC-01 — Technical Deviation Closure Time
- **Type:** KPI · **Leading/Lagging:** Lagging
- **Formula:** `Closure Date − Deviation Logged Date`, averaged
- **Unit:** days · **Grain:** Per line · **Frequency:** Monthly
- **Data source:** Same gap as RI-QUA-03 (deviation log)
- **Owner:** Engineering Manager · **Validator:** Process Engineering Lead · **Reviewer:** Engineering Manager, Complex Manager
- **Target:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Lower is better
- **Required dimensions:** Deviation category
- **Business purpose:** Engineering-change-control responsiveness.
- **Decision triggered:** Rising average → engineering resourcing review.
- **Escalation:** Engineering Manager → Complex Manager

### RI-PRC-02 — Root-Cause Recurrence Rate
- **Type:** RI · **Leading/Lagging:** Lagging
- **Formula:** `COUNT(deviations with a repeat root cause within period) / COUNT(total deviations) × 100`
- **Unit:** % · **Grain:** Per line, per root-cause category · **Frequency:** Quarterly
- **Data source:** Same deviation-log gap
- **Owner:** Engineering Manager · **Validator:** Process Engineering Lead · **Reviewer:** Engineering Manager
- **Target:** — · **Warning/Critical:** `TARGET — BUSINESS APPROVAL REQUIRED`
- **Direction:** Lower is better
- **Required dimensions:** Root-cause category
- **Business purpose:** Signals whether corrective actions are actually effective, not just closed on paper.
- **Decision triggered:** High recurrence → escalate to a formal CAPA-style review (mechanism not designed here).
- **Escalation:** Engineering Manager → Complex Manager

---

## 12. Consolidation Notes (Section 37/36 compliance)

Two potential duplications were identified during metric development and deliberately resolved,
per the governing task's explicit instruction to identify and recommend consolidation rather
than list near-duplicate metrics:

1. **Packaging downtime.** A candidate `RI-PKG-03 — Packaging Downtime %` (packaging-side view)
   and `RI-MNT-02 — Maintenance-Related Packaging Downtime %` (maintenance-side view) would have
   measured overlapping ground. **Consolidated into RI-MNT-02 alone**, scoped specifically to
   the maintenance-attributable share of downtime (the integration point the governing task's
   Maintenance section actually asks for), while general line utilization is covered separately
   and non-redundantly by `PI-PKG-04 — Packaging Line Utilization %` (a distinct concept:
   scheduled-vs-run time, not downtime-by-cause).
2. **Inventory adjustment risk.** `KRI-INV-01` (unexplained/unreferenced adjustments) and
   `KRI-FIN-01` (materially large adjustments) were checked for overlap and kept **separate,
   deliberately** — they test different conditions (a small, well-referenced adjustment can
   still be a control failure if unreferenced; a large, fully-referenced adjustment can still be
   a financial-materiality concern) and have different primary owners (Security vs. Finance),
   satisfying the "different decision use" bar for keeping both.

## 13. Summary Counts

> **CORRECTION (Phase 2F re-audit).** The first version of this section stated 48 metrics
> (12 KPI / 15 PI / 13 RI / 8 KRI), 34 needing a target and 19 lacking a source. Recounting the
> catalog's own headings and Data-source lines showed those figures were wrong. The counts below
> are reproducible from this file (count the `### <TYPE>-<DOMAIN>-<n>` headings; read each metric's
> "Data source" line). Nothing else in the catalog changed.

| Type | Count |
|---|---|
| KPI | 15 |
| PI | 16 |
| RI | 17 |
| KRI | 8 |
| **Total** | **56** |

By domain: Production 4, Packaging 9, Quality 7, Maintenance 8, Inventory 7, Finance 6, HSE 6,
Security 5, Process/Engineering 4 (sum 56; each metric is counted once, in the domain of its ID).

**Business target / confirmation:** 50 of 56 carry `TARGET — BUSINESS APPROVAL REQUIRED` or
`SITE / LEGAL CONFIRMATION REQUIRED`. Six do not need a numeric target (informational or
diagnostic): PI-PKG-01, PI-MNT-02, PI-INV-01, RI-HSE-01 (direction itself needs a decision),
KPI-HSE-02, PI-PRC-01. No target value is invented anywhere.

**Data-source readiness (reproducible classification from each metric's Data-source line):**

| Class | Count | Metrics |
|---|---|---|
| E — source entities exist today | 19 | KPI-PRD-01, PI-PRD-01, KPI-PRD-02, RI-PRD-01, KPI-MNT-01/02/03, PI-MNT-01, RI-MNT-01, KRI-MNT-01, PI-MNT-02, RI-FIN-01, KRI-FIN-01, KRI-INV-01, KRI-SEC-01, KRI-SEC-02, RI-SEC-02, KPI-INV-01, PI-INV-02 (several still need a Finance cost rate, a cycle-count capture mechanism, a materiality threshold or a rule definition — the *entities* exist, the *inputs* may not) |
| PB — batch-level today, package-level after Phase 3 | 8 | KPI-QUA-01, KPI-QUA-02, RI-QUA-01, RI-QUA-02, PI-INV-01, RI-INV-01, PI-INV-03, RI-SEC-01 |
| P — needs Packaging entities | 13 | KPI-PKG-01, KPI-PKG-02, RI-PKG-01, RI-PKG-02, PI-PKG-01..04, RI-MNT-02, KRI-INV-02, RI-FIN-02, KPI-FIN-01, KPI-FIN-02 (the two FIN KPIs also need decision E-1) |
| G — no source entity exists and none is planned in Phase 3 | 16 | PI-PKG-05, KRI-QUA-01, PI-QUA-01, RI-QUA-03, KPI-HSE-01, RI-HSE-01, KRI-HSE-01, RI-HSE-02, KPI-HSE-02, PI-HSE-01, PI-SEC-01, RI-PRC-01, PI-PRC-01, KPI-PRC-01, RI-PRC-02, PI-FIN-01 |
| **Total** | **56** | |

Only 11 of the 16 class-G metrics carry an explicit `Metric Definition Gap` label in their text
(the five that do not: RI-QUA-03, PI-PRC-01, KPI-PRC-01, RI-PRC-02, PI-FIN-01 — internal
inconsistency recorded here rather than silently rewritten). These are target-state definitions,
not claims that they can be built today.
