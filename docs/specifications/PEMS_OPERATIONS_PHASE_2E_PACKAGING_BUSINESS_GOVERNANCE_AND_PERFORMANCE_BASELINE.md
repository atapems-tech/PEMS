# PEMS Operations Phase 2E — Packaging Business Governance and Performance Baseline

**Status: DOCUMENTATION / DECISION BASELINE ONLY.** No `Package`, `PackagingRun`,
`PackagingInput`, `PackagingOutput`, `PackagingLoss`, `PackagingEvent`, `HandlingUnit`, `Pallet`,
or `QualityTestResult` entity, migration, controller, view, API, permission, workflow, or seed
data was created by this phase. This document is the governance/organizational companion to
Phase 2D's technical specification — together they are the required input to Phase 3
implementation planning, not Phase 3 itself.

---

## 1. Executive Summary

Phase 2D answered *what Packaging is, technically*. Phase 2E answers *who decides, who does,
who checks, who is accountable, and how success/risk is measured* — the organizational layer a
technical specification cannot supply on its own. This document, together with the companion
`PEMS_OPERATIONS_PHASE_2E_KPI_PI_RI_KRI_CATALOG.md` and
`PEMS_OPERATIONS_PHASE_2E_PACKAGING_EXECUTIVE_DECISION_PACK.md`, is the complete Phase 2E
deliverable. It does not resolve Phase 2D's six hard blockers (D1–D6) — no one but the named
business owners can do that (§29) — it makes the *consequences* of each blocker, and the
governance structure Phase 3 must operate inside once they are resolved, explicit and reviewable.

## 2. Relationship to Phase 2D

Phase 2D produced: a technical specification (domain model, boundary decision, lifecycle
models, mass-balance invariant), a business decision register (D1–D12, twelve items, six hard
blockers), and an audit report (Classification B). This phase **does not overturn, reinterpret,
or silently change** any Phase 2D conclusion. Every Phase 2D decision ID (D1–D12) is preserved
under its original number in §29 below, with governance metadata (stakeholder, owner, approver,
impact) added, not new business content substituted. Where this phase's stakeholder/RACI/KPI
analysis surfaces a genuinely new open question Phase 2D did not name, it is recorded as a new,
separately-numbered item (prefixed `E-`) in §31, never folded into an existing D-number.

## 3. Scope

Cross-functional governance for Packaging: stakeholder responsibilities, RACI, segregation of
duties, quality/inventory/financial/HSE/security/maintenance/engineering governance, mass
balance, the KPI/PI/RI/KRI framework (full catalog in the companion document), a risk register,
management dashboard requirements, the carried-forward decision register, and the Phase 3
readiness gate.

## 4. Non-Scope

Everything Phase 2D already declared non-scope, unchanged: no Packaging entity, migration,
controller, view, API, service, repository, permission, workflow, barcode implementation, seed
data, or modification to `InventoryTransaction`, `MaterialStock`, `MaterialItem`,
`ProductionBatch`, or `ProductionOutput`. Additionally, this phase does not: create numeric
accounting rules, invent HSE/legal thresholds, assign risk scores beyond what PEMS's existing
methodology supports (none was found — see §4 of the audit report), or make any of the six hard
blocker decisions on the business's behalf.

## 5. Current Architecture Baseline

Re-verified directly against the live repository this phase (full evidence in the companion
audit report §4) — restated here only as the baseline the governance model below assumes:

```
MaterialItem (authoritative product/material master)
   └── ProductionOrder → ProductionRun → ProductionBatch → ProductionOutput
                             │                  │
                             │                  └── QualityStatus: Produced→AwaitingQC→{Released|Blocked|Rejected}
                             │                       (Phase 2C gate: inventory posting requires Released — CONFIRMED STILL ACTIVE)
                             │
                             └── InventoryTransaction (append-only ledger, nullable ProductionBatchId)
                                       └── MaterialStock (projection, unchanged grain)

BarcodeIdentity (generic TargetType/TargetId: Equipment/MaterialItem/Warehouse/StorageLocation/ProductionBatch)
"Production" permission module (View/Create/Edit/Delete/Approve/Hold/ReleaseHold)
  SoD pairs: Production.Create↔Approve, Production.Edit↔Approve (CONFIRMED ACTIVE)
InspectionRecord: Equipment-scoped only (EquipmentId required, non-nullable) — not reusable for product/package quality
```

No Packaging entity exists anywhere in the solution (confirmed by repo-wide search this phase).
This baseline is the floor every governance statement below is built on.

## 6. Stakeholder Model

Fourteen roles, analyzed per the governing task's own per-role breakdown (§5–§18 there). Summary
table; full narrative per role follows in §7.

| Role | Primary concern | Primary authority |
|---|---|---|
| Complex/Site Manager | Cross-functional governance, final accountability | Escalation resolution, cross-functional conflict resolution |
| Maintenance Manager | Equipment availability/reliability | Maintenance strategy, work order prioritization |
| Finance Manager | Cost, valuation, financial control | Accounting treatment, financial approval thresholds |
| Operations Manager | Production/packaging execution and throughput | Operational scheduling, shift authorization |
| Commissioning Manager | New/changed equipment readiness | Commissioning-vs-operations boundary sign-off |
| Operations Shift Supervisor | Shift-level execution and escalation | Shift authorization, downtime classification |
| Operator | Physical execution | Data entry within defined limits, escalation of abnormal conditions |
| Engineering Manager | Technical governance, change control | Engineering change approval |
| Process Engineering Lead | Process parameters, yield, deviations | Technical investigation ownership |
| Packaging Manager | Packaging process ownership | Packaging plan/execution/reconciliation |
| Laboratory Manager | Product quality authority | Quality release/block/reject decision |
| HSE Manager | Safety and environmental compliance | Stop-work authority |
| Security Manager | Identity/movement/access integrity | Security investigation, audit review |
| Warehouse Manager | Storage, movement, shipment readiness | Inventory location/movement management (not quality release) |

## 7. Role Responsibilities (detail)

### 7.1 Complex / Site Manager
**Owns:** final site-level governance decisions listed in §29's Approval Matrix; cross-functional
conflict resolution when two department heads disagree (e.g., Finance wants tighter loss
tolerance, Operations wants looser); KPI governance sign-off (§26). **Approves:** the six hard
blockers (D1–D6) at final sign-off, after each named functional owner recommends; any KRI
Critical escalation reaching this level. **Needs:** the Complex Manager dashboard (§27).
**Escalation trigger:** any KRI in Critical state; any cross-functional SoD or authority
conflict unresolved at the department-head level.

### 7.2 Maintenance Manager
**Owns:** packaging equipment availability, PM/CM execution, spare parts for packaging lines,
MTBF/MTTR/backlog. **Interaction boundary with Packaging** (governing task §6):
```
Packaging Line Down → Operator/Shift Supervisor reports → Work Request → Work Order
                                                                              ↓
                                                              Maintenance Manager prioritizes
                                                                              ↓
                                                    Equipment restored → PackagingRun resumes
```
This reuses PEMS's existing CMMS Work Request → Work Order flow unchanged — Packaging does not
get a parallel maintenance-request mechanism. **Owns metric:** KPI-MNT-01/02/03, RI-MNT-01,
KRI-MNT-01, jointly RI-MNT-02 with Packaging Manager.

### 7.3 Finance Manager
**Owns:** which Packaging events have financial consequences (§8 below), inventory valuation
treatment, cost-per-unit/ton definitions, write-off approval, period-close reconciliation.
**Does not own:** production/packaging execution decisions, quality release. **Explicit
non-invention:** this document does not define an accounting treatment for packaging
material consumption, loss valuation, or rework cost — every such rule is `BUSINESS DECISION
REQUIRED` (§29, item E-1) until Finance defines it. **Owns metric:** KPI-FIN-01/02, RI-FIN-01/02,
KRI-FIN-01, PI-FIN-01.

### 7.4 Operations Manager
**Owns:** production-to-packaging handoff scheduling, shift-level operational authority,
packaging capacity/bottleneck escalation. **Approval authority:** day-to-day schedule changes;
escalates SoD conflicts and KRI Warning-level events to the Complex Manager only if
department-level resolution fails. **Escalation rules:** shift-level issues resolved by the
Shift Supervisor first; anything requiring cross-department resourcing goes to Operations
Manager; anything requiring capital/policy goes to the Complex Manager.

### 7.5 Commissioning Manager
Packaging requirements during commissioning/startup/ramp-up are **explicitly separated** from
normal operations and from maintenance, per the governing task's own instruction:
```
Commissioning (new/modified packaging line, startup losses, initial product quality)
   — owned by Commissioning Manager, time-boxed, ends at formal handover
       ↓ (handover sign-off)
Normal Operations (PackagingRun under Operations/Packaging Manager)
Maintenance (ongoing reliability, under Maintenance Manager, begins at handover)
```
Startup losses/quality data recorded during commissioning **must not be commingled with
permanent PackagingLoss/quality history** used for KPI-PKG-02/RI-PKG-01 trend reporting — a
commissioning-vs-production flag (or equivalent boundary marker) on any future PackagingRun is
`BUSINESS DECISION REQUIRED` (item E-2), since Phase 2D's specification does not currently
define one and none should be assumed.

### 7.6 Operations Shift Supervisor
**Owns:** shift-level authorization of packaging runs, operator assignment, shift handover
completeness, first-line downtime classification, first-line quantity verification before
Packaging Manager/Laboratory sign-off. **Minimum shift-level data required:** PackagingRun
start/stop times and operator, quantities entered per PackagingInput/Output/Loss, any downtime
event with a preliminary cause tag (later confirmed/reclassified by Maintenance if
equipment-related), any quality-hold flag raised during the shift.

### 7.7 Operator
**May enter:** PackagingInput/Output/Loss quantities, downtime start/stop with a reason tag,
sample identification for laboratory handoff, abnormal-condition reports, HSE observations.
**May edit:** only their own not-yet-confirmed entries within the same shift (append-only
correction, never overwrite — per Phase 2D's append-only philosophy, §19/§22 of the technical
specification). **May approve:** nothing requiring a separate authorization — an Operator is
never the sole actor on a controlled decision. **May not modify:** any confirmed/posted
transaction, any quality status, any barcode identity once printed (may request a reprint,
never edit the underlying record). **Must escalate:** any quality anomaly, any safety
observation, any equipment abnormality, any quantity discrepancy beyond a defined tolerance
(D11). **Hard boundary, restated from the governing task and enforced by the SoD model in §9:**
an Operator cannot release quality, cannot approve a financial adjustment, cannot bypass an HSE
control, and cannot independently authorize an inventory movement outside a normal, permitted
transaction.

### 7.8 Engineering Manager
**Owns:** technical governance and change control for packaging equipment/process/product
specifications. **Approval required when:** a packaging-line engineering change affects a
product specification, a safety interlock, or a quality-relevant process parameter — mirrors
the existing Engineering-change discipline already implied by `ProductionOrder`/`ProductionRun`
governance, extended to Packaging by role, not by a new mechanism.

### 7.9 Process Engineering Lead
**Owns:** packaging process parameters, yield, loss investigation, deviation root cause.
Required relationship (governing task §13), confirmed consistent with Phase 2D's own data
model:
```
ProductionOutput (bulk) → PackagingInput → PackagingRun → PackagingOutput → Package.QualityStatus
                                                                                    ↓
                                                                    Deviation / Investigation
                                                                    (RI-PRC-01, RI-QUA-03)
```

### 7.10 Packaging Manager
**Central role.** Owns: packaging process definition, packaging plan (if D8 confirms an Order
tier), packaging execution (`PackagingRun`), package identification (`Package`/barcode),
packaging equipment operational readiness (jointly with Maintenance), package quantity
(`PackagingOutput`), package status (process + physical lifecycle, Phase 2D §30 — **not**
quality status, see §7.11), packaging losses, package reconciliation (mass balance, §10 below),
package genealogy accuracy, packaging performance (KPI-PKG-*), packaging deviations (feeds
Process Engineering).

**Distinguished explicitly from adjacent roles** (governing task §14's requirement — no
overlapping approval authority without justification):

| Decision | Packaging Manager | Operations Manager | Laboratory Manager | Warehouse Manager |
|---|---|---|---|---|
| Packaging schedule/capacity | **A** | C | I | I |
| PackagingRun execution sign-off | **A** | I | I | I |
| Package quality release/block/reject | C (requests) | I | **A** | I |
| Package storage location/movement | I | I | I | **A** |
| Cross-department scheduling conflict | R | **A** | I | I |

(R/A/C/I per §8's methodology — this table is a focused excerpt; the full 30-activity RACI is
§8.)

### 7.11 Laboratory Manager
**The product-quality authority.** Owns: sample collection protocol, laboratory testing,
product specification conformance, test result recording, product release/block/reject/retest
decisions, quality-status inheritance rules (Phase 2D §25's Model D), package quality status.

**Mandatory principle, restated and enforced:** Production or Packaging personnel **must not**
be able to declare a product "Released" merely by changing an operational status. This is
already true today for `ProductionBatch` (the Phase 2C gate enforces `QualityStatus ==
"Released"` server-side before inventory posting, verified still active — §5 of the audit
report) and Phase 2D's Model D design explicitly carries the same principle forward to
`Package`. The quality lifecycle, confirmed:
```
Produced
   ↓
Awaiting Laboratory / Quality Decision  (PendingQC)
   ↓
Released  |  Blocked (→ PendingQC, recoverable)  |  Rejected
```
**Authorized quality-release role:** per Phase 2D §26, the recommended default is to reuse the
existing `"Production"` permission module's `Approve` action and its `Create↔Approve` SoD pair
for package release, unless the business confirms a distinct Packaging QC role is needed — this
is Decision D7, carried forward unchanged (§29).

### 7.12 HSE Manager
**Owns:** packaging hazard analysis (dust, pressure, chemical exposure, lifting, forklift
interaction, loading/unloading, package integrity, spill/leak/fire/explosion risk, safety
interlocks, permits, environmental requirements), stop-work authority, HSE KPI/KRI ownership
(§25 of the KPI catalog), incident escalation. **Stop-work authority:** any HSE Manager,
Complex Manager, or trained/authorized role per site policy may invoke stop-work — the exact
authorized-role list is `SITE / LEGAL CONFIRMATION REQUIRED` (item E-3), not assumed by this
document. **Legal/regulatory requirements** (PPE standards, permit-to-work scope, environmental
discharge limits) are explicitly **not invented** here — every such requirement is marked `SITE
/ LEGAL CONFIRMATION REQUIRED` rather than guessed.

### 7.13 Security Manager
**Owns:** package/barcode identity integrity, material-movement control, warehouse gate control,
unauthorized-movement detection, shipment/loading control (once a Shipment module exists — out
of scope today), access control for Packaging/Warehouse functions, barcode reprint/replace
anomaly monitoring, suspicious inventory-adjustment detection, audit review. **Explicit
boundary, restated from the governing task:** Security does not substitute for operational or
quality authorization — a Security Manager can flag, investigate, and escalate an unauthorized
movement, but cannot itself release a quality hold or approve a financial adjustment; those stay
with Laboratory Manager and Finance Manager respectively.

### 7.14 Warehouse Manager
**Owns:** receipt of packaged product, storage/location assignment, stock ownership records,
inventory movement execution, blocked/released/rejected stock handling (execution only, not the
quality decision itself), shipment/picking/loading execution, reconciliation, cycle counting,
inventory adjustment execution (with Finance sign-off per KRI-FIN-01/KRI-INV-01 thresholds).
**Explicit boundary** (governing task §18, and this document's own hard rule): Warehouse **must
not** independently release product quality. The only path from packaged product to available
warehouse stock is:
```
Production / Packaging  →  Quality Release (Laboratory Manager)  →  Inventory Availability  →  Warehouse
```
A Warehouse Manager may hold/quarantine stock *physically* on their own authority (a safety/
integrity action), but may not *change* a `Package.QualityStatus` from Blocked to Released — that
remains Laboratory Manager's exclusive authority, mirroring the existing
`PostOutputToInventoryAsync` gate's own separation of concerns.

## 8. RACI — Packaging Activities

R = Responsible, A = Accountable (exactly one per row unless a documented SoD split requires
otherwise, flagged below), C = Consulted, I = Informed. Roles abbreviated: CM=Complex Manager,
MM=Maintenance Manager, FM=Finance Manager, OM=Operations Manager, CommM=Commissioning Manager,
SS=Shift Supervisor, Op=Operator, EM=Engineering Manager, PEL=Process Engineering Lead,
PM=Packaging Manager, LM=Laboratory Manager, HSE=HSE Manager, SEC=Security Manager, WM=Warehouse
Manager.

| # | Activity | CM | MM | FM | OM | CommM | SS | Op | EM | PEL | PM | LM | HSE | SEC | WM |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | Production completion | I | I | I | A | I | R | R | I | C | I | I | I | I | I |
| 2 | Packaging planning | I | C | C | C | I | I | I | I | I | **A** | I | I | I | C |
| 3 | Packaging order authorization | I | I | I | C | I | I | I | I | I | **A** | I | I | I | I |
| 4 | Packaging run start | I | I | I | I | I | **A** | R | I | I | R | I | I | I | I |
| 5 | Material issue | I | I | I | I | I | C | R | I | I | **A** | I | I | I | C |
| 6 | Package creation | I | I | I | I | I | C | R | I | I | **A** | I | I | I | I |
| 7 | Quantity recording | I | I | I | I | I | C | R | I | I | **A** | I | I | I | I |
| 8 | Barcode assignment | I | I | I | I | I | I | R | I | I | **A** | I | I | C | I |
| 9 | Sampling | I | I | I | I | I | C | R | I | I | C | **A** | I | I | I |
| 10 | Laboratory testing | I | I | I | I | I | I | I | I | C | I | **A** | I | I | I |
| 11 | Product release | I | I | I | I | I | I | I | I | I | I | **A** | I | I | I |
| 12 | Product blocking | I | I | I | I | I | I | I | I | I | I | **A** | I | I | I |
| 13 | Product rejection | I | I | I | I | I | I | I | I | C | I | **A** | I | I | I |
| 14 | Inventory posting | I | I | C | I | I | I | I | I | I | R | I | I | I | **A** |
| 15 | Package storage | I | I | I | I | I | I | R | I | I | I | I | I | I | **A** |
| 16 | Package movement | I | I | I | I | I | I | R | I | I | I | I | I | C | **A** |
| 17 | Package shipment *(future)* | I | I | I | I | I | I | R | I | I | I | I | I | C | **A** |
| 18 | Loss recording | I | I | C | I | I | C | R | I | C | **A** | I | I | I | I |
| 19 | Variance investigation | I | C | C | C | I | I | I | C | **A** | R | I | I | I | I |
| 20 | Equipment breakdown | I | **A** | I | I | I | R | R | I | I | C | I | I | I | I |
| 21 | Maintenance work request | I | **A** | I | I | I | R | R | I | I | C | I | I | I | I |
| 22 | Maintenance work order | I | **A** | I | I | I | I | I | I | I | I | I | I | I | I |
| 23 | HSE stop-work | I | I | I | I | I | R | R | I | I | I | I | **A** | I | I |
| 24 | Security investigation | I | I | C | I | I | I | I | I | I | I | I | I | **A** | I |
| 25 | Financial reconciliation | I | I | **A** | C | I | I | I | I | I | C | I | I | I | C |
| 26 | Period closing | I | I | **A** | I | I | I | I | I | I | C | I | I | I | C |
| 27 | Audit review | I | I | C | I | I | I | I | I | I | I | I | I | **A** | I |
| 28 | KPI review | **A** | C | C | C | I | I | I | C | C | C | C | C | C | C |
| 29 | KRI review | **A** | C | C | C | I | I | I | C | C | C | C | C | C | C |
| 30 | Management escalation | **A** | R | R | R | I | R | I | R | R | R | R | R | R | R |

**SoD-driven A-split, explicitly flagged** (per the RACI instruction's own carve-out — "exactly
one accountable owner unless a documented SoD requirement requires otherwise"): rows 4-8
(Packaging execution) are Accountable to Packaging Manager, and rows 9-13 (sampling through
rejection) are Accountable to Laboratory Manager — this split **is** the SoD control itself
(§9, first conflict), not an oversight. Row 30 lists every role as R because escalation is a
distributed duty (anyone can/must escalate); the Complex Manager remains the single ultimate
Accountable party for what happens with an escalation once received.

## 9. Segregation of Duties

Evaluated against the existing PEMS `AccessControlService`/`SoDPolicy` architecture (verified
active this phase, §5 of the audit report), not against invented permissions. PEMS's
`SoDPolicy` model is a generic pair-of-`Permission` structure — the same mechanism already
seeds `Production.Create↔Approve` and `Production.Edit↔Approve` can seed every pair below
**once the Packaging module/permission decisions in §29 (D7, and the new item E-4, module-name
choice) are made** — no new SoD *mechanism* is proposed, only new *pairs* registered in it.

| # | Conflict | Enforced today for Production? | Packaging equivalent (Phase 3, once built) |
|---|---|---|---|
| 1 | Operator ≠ Laboratory Release Authority | Yes, structurally — `Approve` is a distinct action from any Operator-level action, and `ProductionBatch.QualityStatus` transitions are gated server-side | `Package.QualityStatus` release must be gated identically (Phase 2D §28's duplicated-check recommendation) |
| 2 | Operator ≠ Financial Adjustment Authority | Yes — Operators have no `InventoryMasterData`/Finance module permissions | Unchanged; Packaging introduces no new financial-adjustment path outside existing `IMaterialStockService` |
| 3 | Warehouse ≠ Product Quality Release Authority | Yes, by module separation (`InventoryMasterData` vs. `"Production"`'s `Approve`) | Same separation must extend to `Package.QualityStatus` — Warehouse Manager role must not be granted `"Production"."Approve"` (or its Packaging-module equivalent) by default |
| 4 | Packaging Operator ≠ Unauthorized Inventory Approval | N/A today (no Packaging module exists) | New pair required once built: `Packaging.Create↔Approve` (mirroring `Production.Create↔Approve` exactly, if a distinct `"Packaging"` module is chosen — Decision E-4) |
| 5 | Production ≠ Independent Quality Release | Yes — `Production.Create↔Approve` already prevents a batch's creator from also releasing it | Same pairing pattern must apply to whichever role creates a `PackagingRun` vs. whoever releases the resulting `Package` |
| 6 | Security ≠ Operational Quantity Approval | N/A (Security has no operational-quantity permissions today) | Preserved by construction — Security's role is investigation/audit, never granted a `Packaging.Approve`-equivalent permission |

**No new SoD conflict beyond these six was identified.** No permission was created by this
phase — this table documents intended future pairings for Phase 3, consistent with the existing
`AccessControlSeeder` pattern, not a request to seed them now.

## 10. Quality Governance

Restates and confirms Phase 2D §25's Model D as the governance-level quality lifecycle (not a
re-decision — Phase 2D already resolved the *model*; this section adds *who does each step*):

```
Produced
   ↓  (creates the sample — Operator, per Laboratory Manager's sampling protocol)
Awaiting Laboratory / Quality Decision
   ↓  (performs testing — Laboratory technician, under Laboratory Manager)
   ↓  (records results — Laboratory technician)
   ↓  (reviews results — Laboratory Manager)
   ↓  (releases / blocks / rejects — Laboratory Manager, exclusively)
Released | Blocked (→ reopen path: Laboratory Manager only, back to PendingQC) | Rejected
```
**Who can override:** no role overrides a Laboratory Manager's release/block/reject decision
unilaterally; a disputed decision escalates to the Engineering Manager (technical dispute) or
Complex Manager (business-impact dispute), never self-overridden by Operations/Packaging/
Warehouse. **Audit requirement:** every transition is captured automatically by the existing
`PEMSDbContext.SaveChangesAsync` interceptor (confirmed active for every `BaseEntity`-derived
entity, per Phase 2D §31) plus a human-readable `PackagingEvent` narrative (Phase 2D §16) — no
new audit mechanism required, once built.

## 11. Inventory Governance

The Packaging/Inventory boundary, confirmed from Phase 2D §27 and restated in
operational-ownership terms:
```
Production Batch (Released)
        ↓  owned by: Packaging Manager (consumption tracked against the batch's own running total)
Packaging Input
        ↓  owned by: Packaging Manager
Packaging Process
        ↓  owned by: Packaging Manager
Package Output
        ↓  owned by: Packaging Manager (creation) / Laboratory Manager (quality)
Quality Release
        ↓  owned by: Laboratory Manager, exclusively
Inventory Availability
        ↓  owned by: Warehouse Manager (execution), gated by Laboratory Manager's decision
Warehouse
        owned by: Warehouse Manager
```
**Double-counting invariant** (Phase 2D §27, confirmed unchanged): a batch's material must never
be simultaneously available as raw bulk stock and as packaged stock. This is a **resolved
technical design**, not a business decision — the invariant holds by construction if the source
batch was never itself posted to inventory, or is explicitly deducted via a corresponding
`Issue`/`Adjustment` transaction if it was. Governance responsibility for verifying this
invariant holds in practice (not just in design) sits with the Warehouse Manager (execution) and
Finance Manager (periodic reconciliation, KRI-INV-01).

## 12. Financial Governance

Which Packaging events should have financial consequences — **explicitly not decided here**,
per the governing task's own instruction not to invent accounting rules. The candidate event
list, for Finance's decision:

| Event | Candidate financial consequence | Status |
|---|---|---|
| PackagingInput consumption | Cost-of-goods accrual against the resulting Package | `BUSINESS DECISION REQUIRED` (item E-1) |
| PackagingLoss | Loss/variance expense | `BUSINESS DECISION REQUIRED` (item E-1) |
| Package rejection | Write-off | `BUSINESS DECISION REQUIRED` (item E-1) |
| Package split/merge | Internal transformation, net-neutral (Phase 2D's own recommendation, §22-23) — but the exact `InventoryTransaction.TransactionType` vocabulary needed is itself `BUSINESS DECISION REQUIRED`, carried forward from D5 |
| Inventory adjustment for a bulk batch consumed by Packaging | Standard existing adjustment treatment (unchanged mechanism, per §27) | Resolved technically; financial materiality thresholds for review are `BUSINESS DECISION REQUIRED` (KRI-FIN-01) |

**SoD note:** Finance's approval authority for any of the above must not be held by the same
person who is Accountable for the originating Packaging activity (RACI rows 18-19, 25-26) —
this is the existing `Production.Edit↔Approve`-style separation, extended by decision E-4.

## 13. HSE Governance

Mandatory controls, stop-work authority, and escalation are owned by the HSE Manager (§7.12).
This document does not invent site-specific legal/regulatory requirements — every such item is
explicitly marked `SITE / LEGAL CONFIRMATION REQUIRED` in §29/§31, not assumed. HSE KPIs/KRIs
are cataloged in the companion KPI document §9; all currently lack a data source in PEMS (no
incident-logging entity exists anywhere in the solution, confirmed by this phase's audit) —
this is noted as a gap, not silently worked around with a placeholder mechanism.

## 14. Security Governance

Security-sensitive events (unauthorized movement, barcode replacement anomalies, suspicious
adjustments, access violations) and their escalation paths are defined in the companion KPI
catalog §10 (KRI-SEC-01/02, RI-SEC-01/02). Security's role is investigative and auditing, never
a substitute for operational or quality authorization (§7.13, restated as a hard rule, not a
preference).

## 15. Maintenance Governance

Packaging-equipment maintenance reuses the existing CMMS Work Request → Work Order flow
unchanged (§7.2). No parallel maintenance-request mechanism is proposed for Packaging. The one
required cross-functional metric (RI-MNT-02) is jointly owned by Maintenance Manager and
Packaging Manager, reflecting that downtime attribution is inherently a shared determination
(the Packaging side reports the downtime; the Maintenance side confirms/reclassifies the cause).

## 16. Engineering Governance

Engineering approval is required for any packaging-line change affecting a product
specification, a safety interlock, or a quality-relevant process parameter (§7.8). No new
change-control mechanism is proposed — this extends the existing engineering-change discipline
by role assignment only.

## 17. Operations Governance

Operations Manager owns the production-to-packaging scheduling handoff and shift-level
operational authority (§7.4). Shift-level authorization and first-line escalation sit with the
Shift Supervisor (§7.6). No overlapping approval authority exists between Operations Manager and
Packaging Manager for packaging execution itself (§7.10's distinguishing table) — Operations
owns *when/whether* packaging happens at the schedule level; Packaging Manager owns *how* it is
executed and reconciled.

## 18. Packaging Governance

Fully specified in §7.10. The central accountable role for Packaging's own execution, quantity,
identity, loss, and reconciliation — explicitly not overlapping with Laboratory Manager's
quality authority or Warehouse Manager's storage/movement authority.

## 19. Laboratory Governance

Fully specified in §7.11 and §10. The sole quality-release authority; no other role — not
Operations, not Packaging, not Warehouse — can independently declare a package Released.

## 20. Warehouse Governance

Fully specified in §7.14. Execution-only role for stock movement/storage; explicitly forbidden
from independently releasing product quality (§7.14's hard boundary, restated from the
governing task).

## 21. Mass Balance

Restates Phase 2D §32's invariant as a governance-owned control, not merely a technical formula:
```
Total PackagingInput Quantity
  =
Total PackagingOutput Net Quantity
  +
Total PackagingLoss Quantity
  ±
Permitted Process Difference (tolerance — Decision D11, not yet defined)
```
**Ownership:** Packaging Manager is Accountable for the invariant holding at the run level
(RI-PKG-02); Finance Manager is Accountable for its financial materiality review at the period
level (KRI-FIN-01/KRI-INV-01). **Governance rule, restated as a hard requirement:** no report or
dashboard may force this invariant to display as balanced when it is not — the raw difference
must always be shown, per Phase 2D's own explicit instruction and this document's Section 26
KPI-governance rule against silently "fixing" a metric's output.

## 22. KPI / PI / RI / KRI Framework

Full catalog (56 indicators: 15 KPI, 16 PI, 17 RI, 8 KRI — corrected in Phase 2F; the first version of this line said 48) in the companion document
`PEMS_OPERATIONS_PHASE_2E_KPI_PI_RI_KRI_CATALOG.md`. That document is authoritative for every
individual metric's definition, formula, ownership, and threshold status — this section states
only the governance *rule*, not the metric list itself (avoiding duplication between the two
documents).

## 23. Metric Data Lineage

Every metric in the catalog states its actual source against the authoritative chain
(`ProductionOrder → ProductionRun → ProductionBatch → ProductionOutput → PackagingInput →
PackagingRun → PackagingOutput → Package → Quality Decision → InventoryTransaction →
MaterialStock`), per §2 of the companion catalog. No metric is defined against a manually
re-keyed duplicate quantity where an authoritative transactional source exists or is planned.
16 of 56 metrics have no source entity in PEMS (11 explicitly labelled `Metric Definition Gap`, corrected in Phase 2F; first version said 19 of 48) — their data source does not exist in PEMS
today (concentrated in HSE incident logging, laboratory test results, and structured process
parameter capture) — these are documented as target-state definitions, not implementation
claims.

## 24. Risk Register

| ID | Risk | Cause | Consequence | Existing control | Proposed control | Owner | Detection | RI/KRI | Trigger | Escalation | Residual risk | Business decision dependency |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| R1 | Wrong product packaged | Mislabeled bulk material, operator error | Customer complaint, recall risk | None specific to Packaging (doesn't exist yet) | `Package.MaterialItemId` mandatory single-item design (Phase 2D §11 #4) + barcode scan verification at input | Packaging Manager | Barcode mismatch at scan, or post-hoc audit | KRI-INV-02 | Any traceability gap | Packaging Manager → Complex Manager | Low, once built and barcode-verified | D1, D6 |
| R2 | Wrong batch packaged | Multiple batches staged simultaneously, mix-up | Traceability failure, recall risk | None yet | `PackagingInput.SourceProductionBatchId` required (not optional) | Packaging Manager | Genealogy chain check | KRI-INV-02 | Chain break | Packaging Manager → Complex Manager | Low once built | D1, D3 |
| R3 | Wrong quantity recorded | Scale error, data entry error | Financial variance, mass-balance failure | None yet | Gross/Tare/Net model with append-only correction (Phase 2D §19) | Packaging Manager / Operator | RI-PKG-02 variance | RI-PKG-02 | Above tolerance (D11) | Packaging Manager → Finance | Depends on tolerance definition | D4, D11 |
| R4 | Wrong label / identity mismatch | Barcode misprint, reuse of old label | Traceability failure, wrong product shipped | `BarcodeIdentity` immutability + supersession chain (existing, proven) | Extend `SupportedTargetTypes` to `Package` (zero schema change) | Packaging Manager / Security Manager | Scan-time verification | RI-SEC-01 | Anomalous replacement rate | Security Manager | Low, mechanism already proven for other target types | None — technically resolved |
| R5 | Quality release bypass | Operational status change mistaken for or misused as a release | Unreleased/unsafe product shipped | Phase 2C gate on `ProductionBatch` (confirmed active) | Duplicate gate on `Package.QualityStatus` at posting (Phase 2D §28) | Laboratory Manager | Automated gate + audit | — (control, not itself a metric) | Any bypass attempt | Laboratory Manager → Complex Manager | Low if gate is built and tested per Phase 2D's recommendation | None — technically resolved, contingent on Phase 3 implementing the gate |
| R6 | Inventory double counting | Bulk batch posted AND packaged output posted without deduction | Inflated stock, financial misstatement | Phase 2D §27's resolved design (deduct-on-consumption if bulk was posted) | Same | Warehouse Manager / Finance Manager | KRI-INV-01 pattern review | KRI-INV-01 | Any unreferenced large adjustment | Finance Manager → Security Manager | Low — technically resolved by design | None — technically resolved |
| R7 | Inventory loss (unexplained) | Theft, mis-shipment, data error | Financial loss, control failure | Existing `InventoryTransaction` audit trail | KRI-INV-01 monitoring | Finance Manager / Security Manager | KRI-INV-01 | Any flagged event | Finance → Security → Complex Manager | Depends on materiality threshold (undefined) | E-1 (thresholds) |
| R8 | Package identity loss | Damaged/illegible label, no reprint taken | Traceability gap, cannot verify release status | `RecordPrintAsync` reprint mechanism (existing) | Same, extended to Package | Packaging Manager | Manual detection at handling | KRI-INV-02 | Any gap | Packaging Manager | Low if reprint discipline followed | None — technically resolved |
| R9 | Unauthorized movement | Bypassed transaction, access control gap | Loss, security incident | Existing `AccessControlService`/`AuditLog` | KRI-SEC-01 monitoring | Security Manager | Cross-reference movement to authorized transaction | KRI-SEC-01 | Any event | Security Manager → Complex Manager | Low if monitored actively | None — technically resolved |
| R10 | Financial adjustment abuse | SoD bypass, collusion | Financial loss, fraud exposure | `Production.Create↔Approve` pattern (existing, extensible) | SoD pair 4/5 (§9), KRI-FIN-01/KRI-SEC-02 | Finance Manager / Security Manager | Pattern-rule flagging | KRI-SEC-02, KRI-FIN-01 | Any flagged pattern | Finance + Security → Complex Manager | Depends on rule definition (undefined) | E-4 (module/SoD registration), KRI-SEC-02 rule definition |
| R11 | Equipment failure (packaging line) | Reliability gap, PM slippage | Downtime, packaging shortfall | Existing CMMS WorkOrder/PM system | RI-MNT-02, KRI-MNT-01 | Maintenance Manager | Existing CMMS monitoring | KRI-MNT-01 | Aging, criticality-weighted backlog | Maintenance Manager → Complex Manager | Depends on existing CMMS maturity (not re-audited beyond confirming it exists) | None new |
| R12 | Unsafe operation | Missing PPE, bypassed interlock, procedural gap | Injury, regulatory exposure | Not designed in PEMS today | HSE stop-work authority, KPI-HSE-02 | HSE Manager | Observation, near-miss reporting | RI-HSE-01/02, KRI-HSE-01 | Any incident; aging open action | HSE Manager → Complex Manager | High — no HSE data system exists yet | E-3 (legal/site confirmation), Metric Definition Gap |
| R13 | Product contamination | Cross-contamination during packaging/repack | Recall, safety risk | Package quality re-evaluation on split/merge (Phase 2D §22-23, not automatically inherited) | Same | Laboratory Manager / Packaging Manager | Quality re-test | RI-QUA-01/02 | Any rejection following a split/merge | Laboratory Manager → Engineering Manager | Depends on D5's final re-release rule | D5 |
| R14 | Mixed products/batches | Blending not properly tracked, or disallowed blending occurring informally | Traceability failure, quality risk | `PackagingInput` multi-row design supports blending *if* confirmed (Phase 2D §21) | Confirm D3; if "no," physically enforce single-batch input at execution | Packaging Manager | Genealogy audit | KRI-INV-02 | Any multi-batch package without D3 confirmation | Packaging Manager → Process Engineering Lead | Depends entirely on D3 | D3 |
| R15 | Excessive loss | Process instability, equipment wear, operator technique | Financial loss, yield decline | RI-PKG-01 monitoring (once built) | Warning/Critical thresholds (undefined) | Packaging Manager / Process Engineering Lead | RI-PKG-01 trend | RI-PKG-01 | Threshold breach | Packaging Manager → Process Engineering Lead | Depends on threshold definition | E-1 / D11-adjacent (loss thresholds, distinct from mass-balance tolerance) |
| R16 | Unexplained variance | Weighing error, moisture loss, undefined tolerance | Cannot distinguish real loss from noise | None — explicitly undefined per D11 | RI-PKG-02 monitoring, never forced to zero | Packaging Manager / Finance Manager | RI-PKG-02 | RI-PKG-02 | Any variance above tolerance (once set) | Packaging Manager → Finance Manager | High until D11 is resolved | D11 |
| R17 | Barcode duplication | Generation logic failure, manual override misuse | Two physical items sharing one identity | `BarcodeIdentity.Code` immutable/non-guessable (existing, proven) | Same, extended to Package target type | Security Manager | Scan collision detection | RI-SEC-01 | Any duplicate detected | Security Manager | Low — existing mechanism already proven safe for 5 target types | None — technically resolved |
| R18 | Package substitution | Deliberate or accidental swap after release | Wrong product shipped, fraud/safety risk | Barcode + genealogy chain (once built) | KRI-INV-02 monitoring | Security Manager / Packaging Manager | Chain-break detection | KRI-INV-02 | Any gap | Security Manager → Complex Manager | Low if chain is enforced | None — technically resolved, contingent on Phase 3 build quality |
| R19 | Data-entry error | Manual quantity/reason entry mistakes | Downstream reporting/financial inaccuracy | Append-only correction pattern (Phase 2D, proven elsewhere in PEMS) | Same, applied to Packaging | Packaging Manager / Operator | Supervisor confirmation step | RI-PKG-02 (indirect) | Any large single-entry correction | Shift Supervisor | Moderate — depends on discipline, not purely technical | None new |
| R20 | System outage during packaging execution | Infrastructure failure | Lost/incomplete shift data, no offline capture | PWA confirmed network-only by deliberate design (every prior phase) | None proposed — explicitly deferred | Operations Manager / IT | N/A | — | N/A | Operations Manager | High during an outage, by design choice (not a Packaging-specific gap) | Not in Packaging scope — a platform-level decision, noted not owned here |

No numerical risk score is assigned to any item — PEMS's existing risk methodology (as
confirmed by this phase's re-audit) does not define one, and the governing task explicitly
forbids inventing scores absent that support.

## 25. Management Dashboards

Content requirement per role (data source per the companion KPI catalog; **no dashboard is built
by this phase** — this is a specification of what each role needs to see):

| Role | Dashboard content |
|---|---|
| Complex Manager | Production + Packaging attainment, quality status distribution, inventory value/variance, financial summary, HSE incident/near-miss trend, security exception count, all KRIs at a glance |
| Operations Manager | KPI-PRD-01, KPI-PKG-01, PI-PKG-04, downtime summary (RI-MNT-02), RI-PKG-01/02, blocked/rejected product counts |
| Packaging Manager | Active PackagingRuns, PI-PKG-01/03/04, RI-PKG-01/02, KPI-PKG-01/02, package reconciliation status |
| Laboratory Manager | Pending samples/tests, KPI-QUA-02 (release cycle time), RI-QUA-01/02, KRI-QUA-01 (once built) |
| Warehouse Manager | PI-INV-01 (released stock), RI-INV-01 (blocked stock), package movement log, KRI-INV-01/02 |
| Maintenance Manager | KPI-MNT-01/02/03, RI-MNT-01, KRI-MNT-01, RI-MNT-02 (packaging-attributed share) |
| Finance Manager | Inventory value, KRI-FIN-01, RI-FIN-01/02, KPI-FIN-01/02, PI-FIN-01 |
| HSE Manager | Incident/near-miss count (once built), KRI-HSE-01, RI-HSE-01/02, KPI-HSE-02 |
| Security Manager | KRI-SEC-01/02, RI-SEC-01/02, access violation log |

## 26. KPI Governance Rule

For every indicator in the catalog, per-metric Owner/Validator/Reviewer/Escalation is already
specified individually (companion document §0/per-metric). The cross-cutting governance rule,
stated once here rather than repeated 56 times: **a metric's definition may only be changed by
its named Owner, with Reviewer sign-off; no department may create a conflicting local definition
of a metric already in this catalog.** A future `Metric Definition Authority` (a formal,
cross-functional body or role empowered to arbitrate competing metric definitions) is named
conceptually here, per the governing task's explicit instruction, but is **not implemented,
staffed, or chartered by this phase** — its creation, if pursued, is itself a future business
decision, not assumed to exist.

## 27. No Duplicate Master Data

Explicitly preserved, restated as a governance rule binding on Phase 3:
```
MaterialItem            = authoritative material/product master
ProductionBatch         = authoritative production batch
InventoryTransaction    = authoritative inventory movement ledger
MaterialStock           = inventory projection (derived, not authoritative)
BarcodeIdentity         = identity/identification layer
```
No Phase 3 Packaging entity may become a second Product Master, a second inventory ledger, or a
second source of quality truth. `Package` is a *new* physical-identity concept (Phase 2D §11),
not a competing product master — it always references an existing `MaterialItem`, never defines
one.

## 28. Phase 3 Readiness Gate

Phase 3 must **not** begin until:
- D1, D2, D3, D4, D5, D6 are formally resolved by their named business owners (§29).
- The following are formally acknowledged by the roles named against them:

| Item | Acknowledged by |
|---|---|
| Package quality authority (Laboratory Manager, exclusive) | Laboratory Manager, Packaging Manager, Operations Manager |
| Inventory boundary (§11) | Warehouse Manager, Finance Manager, Packaging Manager |
| RACI (§8) | All fourteen roles, or their department heads on their behalf |
| SoD (§9) | Security Manager, Finance Manager, the named module/permission decision owner (E-4) |
| Mass balance (§21) | Packaging Manager, Finance Manager, Process Engineering Lead |
| KPI/PI/RI/KRI definitions (companion catalog) | Each metric's named Owner |
| Metric ownership (§26) | Complex Manager |
| Loss/variance treatment (D11) | Operations Manager, Finance Manager, Process Engineering Lead |
| Barcode identity policy (Phase 2D §29, confirmed technically resolved) | Security Manager (acknowledgment only — no open decision here) |

## 29. Decision Register — Carried Forward from Phase 2D, with Governance Metadata

Original IDs, wording, and technical content preserved exactly from
`PEMS_OPERATIONS_PHASE_2D_BUSINESS_DECISION_REGISTER.md`. Governance metadata added by this
phase; no business decision is made or changed here.

| ID | Topic | Primary owner | Approver(s) | Quality impact | Inventory impact | Financial impact | HSE impact | Security impact | KPI/RI impact | Blocks Phase 3? |
|---|---|---|---|---|---|---|---|---|---|---|
| D1 | What a Package is | Packaging Manager | Complex Manager, Laboratory Manager | High — defines what "released" applies to | High — defines the unit of stock | Moderate — defines the costed unit | None directly | Moderate — defines the barcode target | High — most Packaging metrics assume this | **Yes** |
| D2 | Mixed-product packages | Packaging Manager | Engineering Manager, Complex Manager | High if allowed — complicates release logic | High if allowed | Moderate | None | Low | Moderate | **Yes** |
| D3 | Blended-batch packages | Process Engineering Lead | Packaging Manager, Laboratory Manager | High — affects genealogy/recall scope | High | Low | None | Low | High — KRI-INV-02 depends on this | **Yes** |
| D4 | Gross/Tare/Net model | Finance Manager | Laboratory Manager (if certificate-relevant), Complex Manager | Moderate | Low | **High** — determines auditability of costed weight | None | Low | High — PI-PKG-02, KPI-FIN-01/02 depend on this | **Yes** |
| D5 | Split/merge quality & inventory rules | Laboratory Manager | Packaging Manager, Finance Manager | **High** | High | Moderate | None | Low | Moderate | **Yes** |
| D6 | Packaged-vs-bulk product identity | Finance Manager | Packaging Manager, Complex Manager | Low | High | **High** | None | None | Moderate | **Yes** |
| D7 | Package release authority | Laboratory Manager | Engineering Manager, HSE Manager (if a distinct QC role affects staffing) | High | Low | Low | Low | Low | Low | No — safe default (reuse `"Production"`) |
| D8 | Packaging Order tier | Operations Manager | Packaging Manager, Complex Manager | None | Low | Low | None | None | Moderate — PI-PKG-01's denominator depends on this | No — safe default (no tier) |
| D9 | Pallets / handling units | Warehouse Manager | Operations Manager | None | Moderate | Low | Low (lifting/handling) | Low | Low | No — deferred |
| D10 | Repacking | Packaging Manager | Laboratory Manager, Security Manager | Moderate | Moderate | Low | None | Low | Low | No — deferred |
| D11 | Loss/variance tolerance | Process Engineering Lead | Finance Manager, Operations Manager | Low | Low | Moderate | None | None | **High** — gates RI-PKG-02/RI-PRD-01 usability | Partial — reporting reliability only |
| D12 | Rounding | Finance Manager | Packaging Manager | Low | Low | Low | None | None | Low | No — deferred |

## 30. Final Business Decision Matrix

Per the governing task's required column set, restating D1–D6 (the hard blockers) in the exact
format requested:

| Decision | Business Question | Primary Owner | Required Approvers | Quality Impact | Inventory Impact | Financial Impact | HSE Impact | Security Impact | KPI/RI Impact | Phase 3 Blocking |
|---|---|---|---|---|---|---|---|---|---|---|
| D1 | What exactly is a "Package"? | Packaging Manager | Complex Manager, Laboratory Manager | High | High | Moderate | None | Moderate | High | **Yes** |
| D2 | Can one package contain more than one product? | Packaging Manager | Engineering Manager, Complex Manager | High | High | Moderate | None | Low | Moderate | **Yes** |
| D3 | Can a package be made from more than one production batch? | Process Engineering Lead | Packaging Manager, Laboratory Manager | High | High | Low | None | Low | High | **Yes** |
| D4 | Gross/Tare/Net model | Finance Manager | Laboratory Manager, Complex Manager | Moderate | Low | High | None | Low | High | **Yes** |
| D5 | Split/merge quality & inventory rules | Laboratory Manager | Packaging Manager, Finance Manager | High | High | Moderate | None | Low | Moderate | **Yes** |
| D6 | Does packaging create a different product identity? | Finance Manager | Packaging Manager, Complex Manager | Low | High | High | None | None | Moderate | **Yes** |

## 31. Open Decisions (new items surfaced by Phase 2E, not present in Phase 2D)

| ID | Item | Raised by (section) | Business decision required |
|---|---|---|---|
| E-1 | Which Packaging events carry financial consequences, and their accounting treatment | §12 | Finance Manager |
| E-2 | Commissioning-vs-production boundary marker on PackagingRun records | §7.5 | Commissioning Manager, Packaging Manager |
| E-3 | Authorized stop-work role list and site/legal HSE requirements | §7.12 | HSE Manager, site legal/compliance |
| E-4 | Distinct `"Packaging"` permission module vs. reuse of `"Production"` (technical direction recommended in Phase 2D §35, not yet a business-confirmed choice) | §9, §29 (D7-adjacent) | Complex Manager, Laboratory Manager, Engineering Manager |

These are **new**, not hidden reinterpretations of D1–D12 — each is a governance-layer question
Phase 2D's technical specification did not need to ask, surfaced only by this phase's
stakeholder/RACI/financial analysis. None blocks Phase 3 as independently as D1–D6 do, but E-1
and E-4 should be resolved alongside D1–D6 for Phase 3 to start with a complete financial and
permission model rather than a partial one.

## 32. Approval Matrix

| Document | Approver | Approval confirms |
|---|---|---|
| This governance baseline | Complex Manager | Organizational structure, RACI, SoD, risk register accepted |
| KPI/PI/RI/KRI Catalog | Complex Manager, plus each metric's named Owner | Metric definitions, ownership, and (once set) targets accepted |
| Executive Decision Pack | Complex Manager, Laboratory Manager, Finance Manager, Packaging Manager (D1-D6 owners) | The six hard blockers are formally decided |
| Phase 2D Technical Specification | (Already produced — no re-approval requested by this phase) | Technical shape, unchanged |

## 33. Audit Requirements

Unchanged from Phase 2D §31: automatic via the existing `PEMSDbContext.SaveChangesAsync`
interceptor for every `BaseEntity`-derived entity, once built — zero new audit code required.
This phase adds one governance-level audit requirement: **every KPI/PI/RI/KRI target change
(once targets are business-approved) must itself be an audited event**, attributable to the
named metric Owner, per §26's rule against silent redefinition. No mechanism for this is built
by this phase; it is a requirement statement for Phase 3's eventual KPI-storage design.

## 34. Final Recommendation

Approve this governance baseline and the companion KPI catalog for formal cross-functional
review. Route the Executive Decision Pack to the six named decision owners for D1–D6. Do not
begin Phase 3 coding until D1–D6 are resolved and the Phase 3 Readiness Gate (§28) items are
formally acknowledged. E-1 through E-4 should be resolved alongside D1–D6 where practical, but
are not, individually, hard blockers in the same sense as D1–D6.
