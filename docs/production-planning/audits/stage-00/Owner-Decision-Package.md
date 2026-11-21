# Owner Decision Package for unresolved OD items (PPC Stage 0, 2026-11-14)

> **Historical record:** this package was prepared for owner review on 2026-11-14. The authorisation boundary below was superseded by `Owner-Decisions-2026-11-16.md`, which authorised Stage 1. This package remains evidence for the questions and conditions that were open at the time.

**Status:** documentation only. Prepared for owner review. No OD item was closed by this package.

**Authorisation boundary at 2026-11-14:** no Stage 1, no source code, no database change, no migration, no configuration, no test code, no Git change, no Jira change.

**Sources and limits**
- OD definitions are taken from the recorded question in `03-business-glossary-and-decisions.md`, section 3, and from the owner's direction in `Owner-Decisions-2026-11-14.md`. No fuller definition is recorded in the repository. Where the owner should confirm the wording, this is marked.
- PEMS capabilities are cited from documents 02 and the audit register (file, class, line where recorded).
- Standards: ISA-95 has an identifier recorded from the publisher (ANSI/ISA-95.00.01-2025) with status from a secondary source. ISA-88 / IEC 61512, ISO 22400, EPCIS and ISA/IEC 62443 are **NOT VERIFIED** (document 05). Any statement about them below is design alignment only, not compliance.
- ASCM CPIM is a certification body and body of knowledge, not a standard (document 05, S12). Its practices are used only as reference vocabulary.
- Plant-specific operating parameters (shift patterns, efficiency, tolerances, horizons, legal traceability times, ERP/MES systems) are **not in the repository**. They are marked **PLANT DATA REQUIRED**. No value is invented.
- Performance was **not measured** in Stage 0 (no benchmark run). Performance statements are NOT ASSESSED.

---

## 1. OD definitions, analysis and recommendations

Each item below has the fields requested: business question, technical implications, related findings, dependencies, existing PEMS capabilities affected, alternatives, advantages and disadvantages, recommended option, acceptance criteria, unresolved questions.

### OD-02 Product structure: BOM or formula, yield, co-products, versioning
- **Recorded question:** "Product structure: BOM or formula, yield, co-products, versioning." (document 03)
- **Business question:** Does the plant make product by a formula (process: proportions, yield, co-products) or by a bill of materials (discrete: parts that assemble)? Are both needed?
- **Technical implications:** A formula is a set of input proportions per output quantity, with yield and co-product lines, versioned by date. A BOM is a parent-child part tree with quantities per assembly. Mixing them in one entity makes both harder to maintain.
- **Related findings:** F1 (Major, Gate Blocking); X12 (yield and loss classes are free text); R01, R14. Owner direction: versioned Process Formula; separate ProcessRoute and OperationRouting; do not reuse RoutingRule.
- **Dependencies:** OD-01 (Process first, decided; Hybrid open); OD-03; OD-13 (data owner); OD-22 (versioning).
- **Existing PEMS capabilities affected:** none for formula or BOM (no type exists in PEMS.Domain). ProductionRunService.RecordInputAsync accepts any active material (no structure check). ProductionOutput.OutputClassification and ProductionLoss.LossCategory are free strings. MaterialUOM is available.
- **Alternatives:**
  - A. BOM only (one structure for all types). Simple to reuse; wrong for process yield and co-products.
  - B. Formula only for all types. Fits process; forces discrete parts into a proportion model.
  - C. Two structures: process formula (process) and discrete BOM (later). Matches owner direction.
- **Advantages and disadvantages:**
  - A: one model to maintain; poor fit for yield and co-products.
  - B: good for process; poor for discrete assemblies.
  - C: two models to maintain; each fits its domain; discrete delayed.
- **Recommended option: C.** Process formula now (versioned, effective-dated, yield and co-product lines); discrete BOM later under its own scope.
- **Standards and practice:** ISA-88 recipe concepts (formula, procedure, phase) are a reference for process structure only. ISA-88 is NOT VERIFIED; no compliance claimed. ISA-95 object model (product definition, material definition) is secondary-verified identifier only.
- **Plant-dependent:** yield standards per product; list of co-products and by-products; whether liquid or bulk products need density-based conversion. **PLANT DATA REQUIRED.**
- **Acceptance criteria (for the decision):** owner names the product types in scope for the first formula release; yield model (expected, minimum, maximum) approved per product class; co-product rule agreed.
- **Unresolved questions:**
  1. Which product types are in the first release (process only, or also hybrid)?
  2. Are yields stored per formula line, per output, or both?
  3. Who approves the formula version (links to OD-13 and OD-15)?

### OD-03 Manufacturing routing (operation sequence) and naming of "routing"
- **Recorded question:** "Manufacturing routing (operation sequence) and naming of 'routing'." (document 03)
- **Business question:** What is the operation sequence called, and does the plant need one for process and for discrete?
- **Technical implications:** "Routing" is used four times in PEMS (document routing, letter routing, storage routing, workflow routing). Reusing RoutingRule (document routing table, seed rules in RoutingRuleService lines 23–39) would mix document and manufacturing meaning.
- **Related findings:** F1; R29 (naming, Moderate, blocks Stage 1 naming). Owner direction: ProcessRoute for process, OperationRouting for discrete, separate from RoutingRule.
- **Dependencies:** OD-02; OD-22 (versioning).
- **Existing PEMS capabilities affected:** RoutingRule (document routing; keep unchanged); ProductionRunService (no operation sequence).
- **Alternatives:**
  - A. Keep the word "routing" everywhere (rejected: four meanings already).
  - B. ProcessRoute for process; OperationRouting for discrete (owner direction).
  - C. One name "Operation sequence" for both types.
- **Advantages and disadvantages:**
  - A: no rename; ambiguous, high risk of misuse.
  - B: clear per domain; two concepts to maintain.
  - C: one term; loses the discrete/process distinction in the name.
- **Recommended option: B.** Matches the owner's direction and keeps RoutingRule as document routing.
- **Standards and practice:** ISA-95 uses operations and segments (secondary identifier only). ISA-88 uses procedure and unit procedure (NOT VERIFIED). No compliance claimed.
- **Plant-dependent:** whether process operations need equipment assignments per step (affects CMMS links). **PLANT DATA REQUIRED.**
- **Acceptance criteria:** glossary entries for ProcessRoute and OperationRouting approved; RoutingRule unchanged in the glossary.
- **Unresolved questions:** Is the owner's name final? Are both names acceptable in user interface text (Persian and English)?

### OD-04 Plan hierarchy: APP process, MPP versions, MPS buckets and horizon
- **Recorded question:** "Plan hierarchy: APP process, MPP versions, MPS buckets and horizon." APP and MPP meaning: OWNER-CONFIRMED (ADR-001). Buckets and horizon open. (document 03)
- **Business question:** What time bucket and planning horizon does each level use?
- **Technical implications:** APP (aggregate, product family) typically uses longer buckets; MPS (product detail) uses shorter buckets. The horizon length sets the data volume of every planning run and of the capacity model.
- **Related findings:** F4a (Major, Gate Blocking); F4b (MPP versioning, approved).
- **Dependencies:** OD-05 (capacity buckets); OD-06 (demand); OD-07.
- **Existing PEMS capabilities affected:** none (no APP or MPS exists; word search 0 matches).
- **Alternatives:**
  - A. One bucket for all levels (simple; too coarse for MPS).
  - B. APP in monthly buckets; MPS in weekly buckets (common planning practice; ASCM reference only).
  - C. Horizon and bucket size as configuration per plant (no fixed values in code).
- **Advantages and disadvantages:**
  - A: simple; mismatched to MPS detail.
  - B: matches usual planning cadence; fixed values may not fit every plant.
  - C: flexible; more configuration to test.
- **Recommended option: C with B as the default structure.** The structure is APP and MPS; bucket size and horizon are configurable per plant, and no value is hard-coded.
- **Standards and practice:** ASCM practice (reference only). No standard value is cited.
- **Plant-dependent:** horizon length, bucket size, freeze period, plant calendar. **PLANT DATA REQUIRED.**
- **Acceptance criteria:** owner states the bucket and horizon defaults per level, and the freeze rule; oracle tests (document 14) use the owner's values.
- **Unresolved questions:** What is the planning horizon in weeks or months? Is there a frozen period, and how long is it?

### OD-05 Capacity unit per work centre; calendar; shifts; efficiency; time basis
- **Recorded question:** "Capacity unit per work centre; calendar; shifts; efficiency; time basis." (document 03)
- **Business question:** How is capacity measured (hours, units per hour, batches per day), what calendar applies, and is time stored in UTC?
- **Technical implications:** Capacity unit must match the planning unit. Calendar needs shifts, holidays and shutdowns. Time basis: AD-08 proposes UTC storage; production code uses DateTime.Now (11 uses in ProductionRunService).
- **Related findings:** F3 (capacity, Gate Blocking); F6 (calendar); X20 (local time, Minor); R26.
- **Dependencies:** OD-07 (finite or infinite); OD-04; row 0227 (maintenance scheduling board, capacity overlap).
- **Existing PEMS capabilities affected:** ProductionWorkCenter (no capacity, hours or calendar); ShiftDefinition (start and end time only); ProductionRunService (DateTime.Now); CMMS equipment (possible capacity source for maintenance).
- **Alternatives:**
  - A. Hours per bucket per work centre, with an efficiency factor.
  - B. Units per hour per work centre, with a calendar of available hours.
  - C. Batches per bucket per work centre (process-friendly, coarse).
- **Advantages and disadvantages:**
  - A: standard for RCCP and CRP; needs reliable hour data.
  - B: suits continuous and high-rate process; needs a rate standard.
  - C: simple; too coarse for detail.
- **Recommended option: A for v1 (hours per bucket, efficiency factor), with UTC storage (AD-08).** Option B is added where a plant has a verified rate standard.
- **Standards and practice:** ISA-95 / ISO 22400 KPI definitions for capacity are NOT VERIFIED (ISO 22400 NOT VERIFIED). No compliance claimed.
- **Plant-dependent:** shift patterns, efficiency per work centre, holiday calendar, planned maintenance windows, time zone of each site. **PLANT DATA REQUIRED.**
- **Acceptance criteria:** unit per work centre defined; calendar rules tested (shift boundary, holiday, DST); UTC storage confirmed.
- **Unresolved questions:** Which capacity unit does each work centre use? Is the efficiency factor per work centre or per product? Do multiple sites exist?

### OD-06 Production demand source; netting with procurement; release gate
- **Recorded question:** "Production demand source; netting with procurement; release gate." (document 03)
- **Business question:** Where does production demand come from (MR, forecast, planned orders), and does production demand net against procurement demand?
- **Technical implications:** Netting must be time-phased, with lead time offset and incoming supply. The existing calculator computes a snapshot, not time-phased. Reusing it for MRP would change procurement results.
- **Related findings:** F2 (Major, Gate Blocking); F5 (release gate).
- **Dependencies:** OD-14 (sales order or forecast); OD-05 (capacity); lead-time data owner (not recorded); OD-15 (release approval).
- **Existing PEMS capabilities affected:** NetDemandCalculator (procurement, CMMS WorkOrderMaterial only; no lead time; ReorderPolicyNotEvaluated near L140); NetDemandCollector (L18–20, L50); MaterialItem (no lead-time field); InventoryTransaction and MaterialStock (read for available stock). Evidence: NetDemandCalculator is not time-phased, so reuse of its calculation is not supported by the evidence. Reuse of NetDemandCollector and stock reads is possible.
- **Alternatives:**
  - A. Extend NetDemandCalculator to time-phased MRP. Risk: changes procurement semantics; existing 23+23+9+4+2 tests must still pass.
  - B. New time-phased MRP calculator that reuses the collector and stock reads, with its own tests. Procurement unchanged.
  - C. Separate MRP in a planning context with a read-only link to procurement. Highest isolation; more code.
- **Advantages and disadvantages:**
  - A: least new code; highest regression risk to procurement.
  - B: isolates change; some duplication of the net formula.
  - C: strongest isolation; most code and integration work.
- **Recommended option: B.** Reuse collector and stock reads; do not change the procurement calculation. This follows the evidence that the calculator is not time-phased, and the owner's "reuse only if semantics support time-phased MRP" test fails.
- **Standards and practice:** MRP as a method (ASCM reference only). No standard cited.
- **Plant-dependent:** lead time per material and supplier; safety stock policy; lot sizing rules. **PLANT DATA REQUIRED.**
- **Acceptance criteria:** oracle tests with hand-computed time-phased net requirements; procurement NetDemand tests unchanged; output is recommendations only.
- **Unresolved questions:** Which demand sources are in v1 (MR only, forecast, planned production orders)? Who owns lead times? Is lot sizing in v1?

### OD-07 Finite or infinite capacity; PS and APS scope
- **Recorded question:** "Finite or infinite capacity; PS and APS scope." (document 03)
- **Business question:** Does the plant need schedules that respect capacity limits (finite) or capacity warnings only (infinite)? Is basic scheduling in scope?
- **Technical implications:** Finite scheduling is a search problem with sequencing rules. Infinite loading with warnings is a sum per bucket. Owner direction: basic production scheduling in PPC scope; advanced APS deferred.
- **Related findings:** F6 (scheduling and calendar); F3.
- **Dependencies:** OD-05; OD-04; plan row 0033 (Gantt views, deferred).
- **Existing PEMS capabilities affected:** ProductionWorkCenter, ShiftDefinition (no scheduling); no Gantt component in PEMS.UI.
- **Alternatives:**
  - A. Infinite loading with warnings (RCCP and CRP as load per bucket).
  - B. Finite scheduling per work centre with simple sequencing (basic PS).
  - C. Advanced APS with optimisation (owner: deferred).
- **Advantages and disadvantages:**
  - A: simple, transparent; planner resolves conflicts manually.
  - B: gives feasible schedules; sequencing rules needed from the plant.
  - C: most powerful; large cost; deferred.
- **Recommended option: A for v1 (infinite loading with warnings), with B as a later increment after OD-05 data exists.** C is deferred per owner direction.
- **Standards and practice:** APS and PS terms per owner glossary (PROPOSED). No standard cited.
- **Plant-dependent:** sequencing rules, changeover times, and whether operators can override the plan. **PLANT DATA REQUIRED.**
- **Acceptance criteria:** owner confirms infinite loading for v1; warnings tested; PS scope recorded.
- **Unresolved questions:** Is finite scheduling needed for the first release, or after data quality is proven? Are changeover times available?

### OD-08 Quality gate for inputs, outputs and recall
- **Recorded question:** "Quality gate for inputs, outputs, recall." (document 03)
- **Business question:** Which batch states allow input and output, and how does recall work?
- **Technical implications:** The current status set is Produced, AwaitingQC, Released, Blocked and Rejected. The owner's direction names **Quarantined**, which does not appear in that set. A new state needs a transition table change.
- **Related findings:** F10 (MODIFIED), F11 (APPROVED), F12 (APPROVED); R10.
- **Dependencies:** OD-09 (traceability for recall depth); OD-10 (reversal).
- **Existing PEMS capabilities affected:** ProductionBatch QualityStatus; BatchQualityTransitions (no edge out of Released); RecordInputAsync (existence only); RecordOutputAsync (no status check); QC release flow.
- **Alternatives:**
  - A. Map Quarantined onto the existing Blocked state.
  - B. Add a Quarantined state with its own transitions (owner direction).
  - C. Keep Blocked for QC failure and add Quarantined for post-release holds only.
- **Advantages and disadvantages:**
  - A: no new state; mixes QC failure with post-release hold.
  - B: clear meaning; one more state to test.
  - C: separates failure and hold; most precise; two states to maintain.
- **Recommended option: C.** It fits the owner's direction and keeps the QC failure meaning of Blocked intact. This needs owner confirmation.
- **Standards and practice:** none verified. ISO 9001 QMS is out of product scope (document 05).
- **Plant-dependent:** the quality hold rules, recall time target, and who may release a quarantined batch. **PLANT DATA REQUIRED.**
- **Acceptance criteria:** transition table written and owner approved; tests for each state pair; recall report lists consuming runs.
- **Unresolved questions:** Does Quarantined apply after release only, or also before QC? Who can release from quarantine?

### OD-09 Traceability: lot or serial; batch-level stock; genealogy depth
- **Recorded question:** "Traceability: lot or serial; batch-level stock; genealogy depth." (document 03)
- **Business question:** Which products need lot tracking, which need serial tracking, and how many levels of genealogy are required for recall?
- **Technical implications:** Batch balances can be derived from the ledger (existing) or stored (new). Multi-level genealogy needs recursive queries. Serial tracking adds one entity per unit.
- **Related findings:** F13 (refined: batch balances derived from ledger, InventoryBatchTraceabilityTests 9 tests); F14 (discrete traceability absent); X24 (no lot or serial entities).
- **Dependencies:** OD-08; OD-01 (Hybrid scope); OD-20 (attribution).
- **Existing PEMS capabilities affected:** InventoryTransaction (batch link); MaterialStock (no batch dimension); ProductionBatch.Quantity (decimal); SourceProductionBatchId (one level); ConsumedAsSourceIn hook; MaterialItem.IsBatchControlled and IsSerialized (flags only).
- **Alternatives:**
  - A. Lot-first; ledger remains the source; batch balance as a read projection (owner direction for F13).
  - B. Serial-first for all discrete products.
  - C. Stored batch balances as an independent table.
- **Advantages and disadvantages:**
  - A: keeps one source of truth; read cost on large data.
  - B: exact unit tracking; large data volume and new entities.
  - C: fast reads; two sources to reconcile.
- **Recommended option: A** for process and lot-level discrete now; serial configurable per product later (owner direction F14). Genealogy multi-level (owner direction).
- **Standards and practice:** EPCIS (ISO/IEC 19987 identifier secondary; EPCIS 2.0.1 NOT VERIFIED). No compliance claimed.
- **Plant-dependent:** legal traceability requirement (which products, how many levels, time to trace). **PLANT DATA REQUIRED.** Serial requirement per product.
- **Acceptance criteria:** two-level and multi-level genealogy queries return all consumed lots (oracle test); balance per lot matches ledger.
- **Unresolved questions:** Which products need serial tracking? What is the maximum recall time required? Is the traceability requirement regulatory or internal?

### OD-10 Closure: tolerance, hold and cancel propagation, reconciliation tolerance, reversal
- **Recorded question:** "Closure: tolerance, hold and cancel propagation, reconciliation tolerance, reversal." (document 03)
- **Business question:** How much over or under production is allowed, and what tolerance applies to mass balance?
- **Technical implications:** Tolerance values per material, unit, production mode and site. Owner has approved hold and cancel propagation and reversal (F8, X15). Remaining: values and the concurrent-run rule.
- **Related findings:** F7 (no closure), F9 (over-production, several runs), F15 (per-material reconciliation approved), X15 (reversal approved), F8 (approved).
- **Dependencies:** OD-11 (units); OD-08.
- **Existing PEMS capabilities affected:** ProductionOrderService.CompleteAsync (unfinished runs only); RecordOutputAsync (no cap); CreateAsync (several runs); BuildReconciliation (sums across materials, about L142–177).
- **Alternatives:**
  - A. Zero tolerance by default (no over-production allowed without approval).
  - B. Tolerance as a percentage per material, set by plant data.
  - C. Tolerance as an absolute quantity per material and unit.
- **Advantages and disadvantages:**
  - A: safe default; blocks legitimate yield variance.
  - B: simple; poor for small quantities.
  - C: precise; needs unit per material.
- **Recommended option: A as the safe default in code (no value invented), with B or C set per material by the plant.** The default must be a configuration, not a hard-coded number.
- **Standards and practice:** none verified.
- **Plant-dependent:** tolerance values per material, unit, mode and site; reconciliation tolerance; whether concurrent runs are allowed and under what allocation. **PLANT DATA REQUIRED.** No value is proposed here.
- **Acceptance criteria:** tolerance configuration exists per material; boundary tests pass; concurrent-run rule written.
- **Unresolved questions:** Who sets tolerance values, and how often are they reviewed? Are concurrent runs needed in v1?

### OD-11 Unit conversion policy; unit-safe variance
- **Recorded question:** "Unit conversion policy; unit-safe variance." (document 03)
- **Business question:** Where do conversion factors come from, and which conversions are allowed?
- **Technical implications:** A conversion needs a factor that is valid for the material and dimensionally compatible (mass to mass, volume to volume). Density is needed for mass to volume and is plant or product specific.
- **Related findings:** F16 (approved: no mixed units; conversion only with explicit valid factor); X2 (UOM nullable; ConversionFactor unused); F17.
- **Dependencies:** OD-21 (UOM required); OD-10.
- **Existing PEMS capabilities affected:** MaterialUOM.ConversionFactor (present, unused); OperationsReportService.GetSummaryAsync (sums across UoM); ProductionRunService display only.
- **Alternatives:**
  - A. Material-specific factors only, in MaterialUOM (existing table).
  - B. Global dimension-based conversions (mass, volume, count) plus material-specific density.
  - C. No conversion; reject all mixed-unit lines.
- **Advantages and disadvantages:**
  - A: uses existing table; factor per material must be entered.
  - B: fewer entries; risk of wrong global factors.
  - C: safest; blocks valid cases.
- **Recommended option: A, with C enforced for any pair without a valid factor.** No global factor is assumed.
- **Standards and practice:** none verified.
- **Plant-dependent:** density values and the sources that certify them; which units each material uses. **PLANT DATA REQUIRED.**
- **Acceptance criteria:** conversion uses material factor; missing factor rejects the line; tests with known factors.
- **Unresolved questions:** Which materials are liquids or bulk (need density)? Who maintains factors?

### OD-12 Method ownership: site, line, product, route or order
- **Recorded question:** "Method ownership: site, line, product, route or order." (document 03; owner 2026-11-13)
- **Business question:** At what level is a method (formula or route version) owned and reused?
- **Technical implications:** Ownership level sets reuse and the number of versions. Order-level ownership creates many copies; product-level ownership needs a site and line rule.
- **Related findings:** F1; F4b (immutable versions).
- **Dependencies:** OD-02; OD-03; OD-13.
- **Existing PEMS capabilities affected:** none (no formula or route entity).
- **Alternatives:**
  - A. Product-level, valid for all sites.
  - B. Product and site (most common for multi-site process plants).
  - C. Order-level copies.
- **Advantages and disadvantages:**
  - A: one method per product; ignores site differences.
  - B: matches site practice; more versions.
  - C: flexible; hard to control; poor traceability.
- **Recommended option: B.** Product and site own the versioned method; the order references one approved version.
- **Plant-dependent:** number of sites and lines; whether lines differ in method. **PLANT DATA REQUIRED.**
- **Acceptance criteria:** owner names the owner level; order references an immutable version.
- **Unresolved questions:** How many sites and production lines are in scope?

### OD-13 BOM and recipe data owner (engineering or PPC)
- **Recorded question:** "BOM and recipe data owner (engineering or PPC)." (document 03; owner 2026-11-13)
- **Business question:** Who creates and changes formulas and BOMs: engineering, PPC, or both with approval?
- **Technical implications:** Ownership defines who can edit, who approves, and which workflow applies.
- **Related findings:** F1; A2 (maker-checker).
- **Dependencies:** OD-15 (workflow); OD-12.
- **Existing PEMS capabilities affected:** WorkflowPlatform (used by PurchaseOrderApprovalService for approvals); EDMS and engineering document modules (document control, not formula); PermissionService (role grants).
- **Alternatives:**
  - A. Engineering owns; PPC approves through workflow.
  - B. PPC owns; engineering approves.
  - C. Shared: creator is one role, approver another, workflow enforces separation.
- **Advantages and disadvantages:**
  - A: technical control; PPC depends on engineering speed.
  - B: fast planning changes; weaker technical control.
  - C: separation of duties (matches owner direction for maker-checker).
- **Recommended option: C,** with the data owner named per product class. This is an organisational decision and depends on the plant.
- **Plant-dependent:** organisation of engineering and PPC roles. **PLANT DATA REQUIRED.**
- **Acceptance criteria:** owner names the data owner role and the approver role; permissions designed (not implemented).
- **Unresolved questions:** Which roles exist in the plant (engineer, process engineer, planner)?

### OD-14 Sales order entity in scope, or only MR and forecast
- **Recorded question:** "Sales order entity in scope, or only MR and forecast." (document 03; owner 2026-11-13)
- **Business question:** Does PPC need customer orders inside PEMS, or only material requests and a forecast?
- **Technical implications:** A sales order entity adds customer, price and delivery data; it may already exist in an external ERP.
- **Related findings:** F2 (demand source); A5 (external keys).
- **Dependencies:** OD-06; OD-19.
- **Existing PEMS capabilities affected:** no sales order entity is recorded in the capability inventory (document 02). Material requests (MR) exist in PEMS.
- **Alternatives:**
  - A. MR and forecast only in v1.
  - B. Sales order entity inside PEMS.
  - C. Sales orders received from external ERP through an import (depends on OD-19).
- **Advantages and disadvantages:**
  - A: smallest scope; planning uses forecast; weaker link to customer orders.
  - B: complete in one system; large scope; duplicates ERP if one exists.
  - C: no duplication; needs integration and external keys.
- **Recommended option: A for v1,** with C as the later route if an ERP holds customer orders. B is not recommended unless PEMS is the system of record for sales.
- **Plant-dependent:** whether an ERP holds customer orders. **PLANT DATA REQUIRED.**
- **Acceptance criteria:** owner states the demand inputs for v1 (MR, forecast, ERP orders).
- **Unresolved questions:** Which system holds customer orders today? Is a forecast maintained anywhere?

### OD-15 BOM and plan release through WorkflowPlatform, with maker-checker
- **Recorded question:** "BOM and plan release through WorkflowPlatform, with maker-checker." (document 03)
- **Business question:** Must formula, plan and release approvals go through the workflow engine with separate creator and approver?
- **Technical implications:** The workflow platform already runs purchase-order routing rules and approver roles (PurchaseOrderApprovalService, RouteAsync). Using it for production adds stages and rules; it does not require a new engine.
- **Related findings:** A2 (Major: release bypasses WorkflowPlatform; creator may approve; ProductionOrderService.ReleaseAsync L290).
- **Dependencies:** OD-13 (roles); OD-16 (run model); owner direction on master-plan versus execution gates (F5).
- **Existing PEMS capabilities affected:** WorkflowPlatform (PEMS.WorkflowPlatform); PurchaseOrderApprovalService (rules, overlap check, approver roles); RoleCatalog; ProductionOrderService.ReleaseAsync (status change only).
- **Alternatives:**
  - A. WorkflowPlatform with maker-checker and creator exclusion (existing pattern).
  - B. Service-level check only (creator cannot approve), no workflow.
  - C. Separate approval module for production.
- **Advantages and disadvantages:**
  - A: reuses tested platform; needs stage design.
  - B: simple; no audit trail of routing or history.
  - C: most isolated; duplicates workflow.
- **Recommended option: A.** It reuses the existing platform, as the owner directs.
- **Plant-dependent:** approval chain per product and site (who approves). **PLANT DATA REQUIRED.**
- **Acceptance criteria:** owner names stages; creator exclusion tested; exceptions controlled and audited.
- **Unresolved questions:** How many approval levels are needed for formula versus plan versus release?

### OD-16 Planning engine boundary and Hangfire run model
- **Recorded question:** "Planning engine boundary and Hangfire run model." (document 03)
- **Business question:** Are planning runs long jobs that run in the background, or immediate calculations? What is the boundary between planning and execution?
- **Technical implications:** Hangfire already runs jobs (licensing, letters, OCR, tenders, uploads, document upload; no production job). Owner direction: reuse Hangfire; no new scheduler; idempotent requests; controlled concurrency.
- **Related findings:** A1 (Major: no planning engine or run model; LicensingExtensions.cs L56).
- **Dependencies:** OD-04, OD-05, OD-06 (inputs to the run).
- **Existing PEMS capabilities affected:** Hangfire (infrastructure exists); PEMSDbContext; NetDemandService (synchronous calculation).
- **Alternatives:**
  - A. Hangfire job per planning run, idempotency key from scope and input version.
  - B. Synchronous service call from the web request.
  - C. New scheduler component.
- **Advantages and disadvantages:**
  - A: fits existing infrastructure; needs retry and idempotency design.
  - B: simple; long runs block the request; no retry.
  - C: rejected by owner direction.
- **Recommended option: A,** with synchronous calls allowed only for short checks.
- **Standards and practice:** none verified.
- **Plant-dependent:** expected run size (number of materials, work centres, buckets). Not measured. **PLANT DATA REQUIRED; performance NOT ASSESSED.**
- **Acceptance criteria:** same input version gives same result on retry; concurrent runs for the same scope are controlled.
- **Unresolved questions:** How often must the plan run (daily, weekly, on demand)? What is the largest expected data set?

### OD-17 Transactional audit
- **Recorded question:** "Transactional audit." (document 03)
- **Business question:** Must a business change and its audit row always be saved together?
- **Technical implications:** Owner direction approved A3: one transaction for business change and audit row; transactional outbox for cross-system delivery where needed.
- **Related findings:** A3 (Major; approved).
- **Dependencies:** OD-19 (outbox targets).
- **Existing PEMS capabilities affected:** PEMSDbContext SaveChanges (L582–600) and SaveChangesAsync (L605–622), audit written in a second save; NumberingService pattern (execution strategy) is a reference.
- **Alternatives:**
  - A. One transaction for data and audit (approved).
  - B. Audit after commit with retry queue.
  - C. Keep current two-step save.
- **Recommended option: A,** outbox only where a second system needs the event.
- **Plant-dependent:** none for the decision itself.
- **Acceptance criteria:** fault-injection test: audit failure rolls back data (SQL Server).
- **Unresolved questions:** Which events must reach external systems (outbox scope)?
- **Status:** direction approved; implementation scope open.

### OD-18 Row versions on production and stock
- **Recorded question:** "Row versions on production and stock." (document 03)
- **Business question:** Which records must detect concurrent changes?
- **Technical implications:** Optimistic concurrency on mutable aggregate roots. Immutable append-only ledger rows do not need row versions. Owner direction: mutable aggregate roots only.
- **Related findings:** A4 (Major; modified).
- **Dependencies:** OD-17.
- **Existing PEMS capabilities affected:** BaseEntity has RowVersion and TenantId (L24–27); child production entities do not derive from BaseEntity; ProductionOrder, MaterialStock and InventoryTransaction have no concurrency token.
- **Alternatives:**
  - A. Row versions on all mutable entities (including child entities).
  - B. Aggregate roots only (owner direction).
  - C. None.
- **Recommended option: B.** Concurrency checked at aggregate roots; child lines are protected by the root.
- **Plant-dependent:** none.
- **Acceptance criteria:** list of aggregate roots approved; concurrent update conflict test on SQL Server.
- **Unresolved questions:** Which entities are aggregate roots (order, run, batch, stock)?
- **Status:** direction approved; list open.

### OD-19 External keys for ERP and MES
- **Recorded question:** "External keys for ERP and MES." (document 03)
- **Business question:** Which ERP and MES systems exchange data with PEMS, and when?
- **Technical implications:** Nullable source system and external key, unique per source (owner direction: SourceSystem plus ExternalId; internal keys kept). Integration design depends on the target systems, which are not named in the repository.
- **Related findings:** A5 (Major; modified).
- **Dependencies:** OD-14; OD-17; Stage 6 integration timing.
- **Existing PEMS capabilities affected:** none (no external key on order, transaction or material). Offline product: no external call is permitted in the product (AGENTS.md).
- **Alternatives:**
  - A. Add nullable keys now.
  - B. Defer keys until a target system is chosen.
  - C. Use a separate mapping table per integration.
- **Advantages and disadvantages:**
  - A: ready for later; unused columns now.
  - B: no unused schema; migration later.
  - C: isolates integrations; more tables.
- **Recommended option: B** for schema, with the principle recorded now. Keys are added in the migration that first needs them, not before. Note the offline product rule: any external system needs an owner decision on the channel.
- **Plant-dependent:** ERP and MES names, versions and interfaces. **PLANT DATA REQUIRED.**
- **Acceptance criteria:** target system named; key uniqueness per source tested on re-import.
- **Unresolved questions:** Which ERP and MES are in use? Is integration in scope for this PPC release?

### OD-20 Attribution rule for shared inventory transactions
- **Recorded question:** "Attribution rule for shared inventory transactions." (document 03; A7, R22 Moderate)
- **Business question:** When one stock movement belongs to both a maintenance work order and a production batch, which one is the owner of the cost?
- **Technical implications:** A transaction carrying both links double-attributes material use. Proposal AD-06: maintenance sets only the work order; production sets only the batch, with a check constraint.
- **Related findings:** A7 (Moderate); R22.
- **Dependencies:** OD-09.
- **Existing PEMS capabilities affected:** InventoryTransaction (work order and batch fields); CMMS WorkOrderMaterial; ProductionRunService.RecordInputAsync.
- **Alternatives:**
  - A. Exclusive: one of the two per transaction (AD-06).
  - B. Both allowed; precedence rule.
  - C. Separate transaction types per use.
- **Recommended option: A,** with a check constraint. Needs owner confirmation that no case needs both links.
- **Plant-dependent:** cost-centre rules for shared material. **PLANT DATA REQUIRED.**
- **Acceptance criteria:** the constraint rejects both links; tests for maintenance and production.
- **Unresolved questions:** Is there any legitimate case where one movement serves both a maintenance job and a batch?

### OD-21 UOM required on production lines
- **Recorded question:** "UOM required on production lines." (document 03; A9)
- **Business question:** Must every material line carry a unit?
- **Technical implications:** Owner direction: UOM required on new material and transaction lines; legacy backfill only from verified source.
- **Related findings:** X2 (MODIFIED); F16.
- **Dependencies:** OD-11.
- **Existing PEMS capabilities affected:** production UOM fields nullable; MaterialUOM; ProductionInput, ProductionOutput.
- **Alternatives:**
  - A. Required on new lines; backfill from verified source only (owner direction).
  - B. Default to the material base unit.
  - C. Keep nullable.
- **Recommended option: A.** Option B only where the base unit is verified for that material.
- **Plant-dependent:** base unit per material. **PLANT DATA REQUIRED.**
- **Acceptance criteria:** new lines without UOM rejected; legacy list reviewed; no assumed units.
- **Unresolved questions:** How many legacy lines lack a unit, and who verifies them?
- **Status:** direction approved; backfill rule open.

### OD-22 Effectivity and routing versioning
- **Recorded question:** "Effectivity and routing versioning." (document 03; A11, partly resolved by RoutingRule assessment)
- **Business question:** How does a changed formula or route take effect: by date, by quantity, or by order?
- **Technical implications:** Immutable versions (F4b, approved) need an effective date or a quantity range. Orders must reference the version in force at release.
- **Related findings:** F1; F4b; A2.
- **Dependencies:** OD-02; OD-03; OD-12.
- **Existing PEMS capabilities affected:** none for formula or route. ProductionOrder has no version reference.
- **Alternatives:**
  - A. Date effectivity: version in force on the release date.
  - B. Quantity effectivity: version for quantity ranges.
  - C. Both.
- **Recommended option: A,** with an explicit rule for orders released before a change (keep the old version). Option C only if the plant needs it.
- **Plant-dependent:** change-control practice (how often changes are made, notice periods). **PLANT DATA REQUIRED.**
- **Acceptance criteria:** order stores the version reference; change does not alter released orders (tested).
- **Unresolved questions:** Are changes applied to running orders or only to new orders?

### OD-23 Compliance scope for PPC
- **Recorded question:** "Compliance scope for PPC (any claim needs its own decision)." (document 03; Audit-03 section 4)
- **Business question:** Does PEMS claim alignment or conformance to any standard, and if so which one?
- **Technical implications:** Eight references are NOT VERIFIED; two management-system standards (ISO 9001:2026, ISO 19011:2026) are official but out of product scope (organisation's QMS). Any claim needs evidence.
- **Related findings:** R28 (standards not verified); R32 (publication does not show compliance).
- **Dependencies:** verification of the eight references from official or licensed copies.
- **Existing PEMS capabilities affected:** none for compliance evidence.
- **Alternatives:**
  - A. No compliance or alignment claim in v1.
  - B. Design-alignment statements only, each with a verified source and a state (ALIGNED, NOT ASSESSED).
  - C. Certification or formal conformance claim.
- **Recommended option: A now, moving to B only after the eight references are verified.** C is not recommended for software without a certification decision.
- **Plant-dependent:** customer or regulatory requirements. **PLANT DATA REQUIRED.**
- **Acceptance criteria:** owner states the scope in writing; no claim is made without a verified source.
- **Unresolved questions:** Does any customer or regulator require a named standard?

---

## 2. Decision groups and priority

| Group | Items | Why this order |
|---|---|---|
| A. Manufacturing domain | OD-02, OD-03 | Everything in F1, X12, OD-22 and OD-12 depends on the structure and naming. Decide first |
| B. Production planning and capacity | OD-04, OD-05, OD-06, OD-07 | Needed for F2, F3, F6 and the plan rows (0227, 0033) |
| C. Traceability and quantity control | OD-09, OD-10, OD-11, OD-21 | Needed for F7, F9, F13, F14, F16, X2 |
| D. Workflow and execution architecture | OD-15, OD-16 | Needed for A1, A2, F5 release gates |
| E. Data integrity and integration | OD-17, OD-18, OD-19 | Prerequisites for W0; OD-19 can wait for the integration stage |
| F. Remaining decisions | OD-08, OD-12, OD-13, OD-14, OD-20, OD-22, OD-23 | Mostly organisational or lower-risk; OD-08 and OD-22 are linked to group A and C |

## 3. OD decision matrix (six separate fields per item)

Fields: **Original Audit Recommendation** (what the audit proposed); **Owner Decision** (what the owner has said); **Current Status**; **Open Conditions**; **Verification Evidence**; **Final Closure Approval**.

| OD | Original Audit Recommendation | Owner Decision | Current Status | Open Conditions | Verification Evidence | Final Closure Approval |
|---|---|---|---|---|---|---|
| OD-01 | Process first | Process first decided (2026-11-13) | Hybrid scope OPEN | Hybrid scope and order | Owner message 2026-11-13 | NOT GIVEN (Hybrid open) |
| OD-02 | Formula or BOM; yield; co-products | Direction: versioned process formula; separate discrete later | OPEN | Q1, Q2, Q3 (section 9) | F1 evidence; no structure type in PEMS.Domain | NOT GIVEN |
| OD-03 | Name operation sequence; RoutingRule separate | Direction: ProcessRoute and OperationRouting | OPEN | Name final; UI text | RoutingRule-Assessment | NOT GIVEN |
| OD-04 | Monthly APP, weekly MPS (reference) | Direction: horizon and buckets configurable | OPEN | Default values from owner | APP-MPS-Assessment (0 matches) | NOT GIVEN |
| OD-05 | Hours per bucket; UTC | Direction: pending (not in owner's list) | OPEN | Unit per work centre; efficiency; calendar | ProductionWorkCenter; ShiftDefinition | NOT GIVEN |
| OD-06 | Time-phased MRP, reuse collector | Direction: recommendations only; reuse only if semantics fit | OPEN | Decision on new calculator (B) | NetDemandCalculator (no time phase, no lead time) | NOT GIVEN |
| OD-07 | Infinite loading with warnings for v1 | Direction: basic PS in scope; APS deferred | OPEN | Finite or infinite confirmed | Owner direction 2026-11-14 (scheduling policy) | NOT GIVEN |
| OD-08 | Quarantined as separate state | Direction: Quarantined state in owner direction | OPEN | Confirm state model (C) | ProductionBatch states (no Quarantined) | NOT GIVEN |
| OD-09 | Lot-first; ledger source | Direction: lot-first; multi-level genealogy | OPEN | Serial scope; recall time | InventoryBatchTraceabilityTests (9) | NOT GIVEN |
| OD-10 | Zero tolerance default; per-material values | Direction: closure items approved; values pending | OPEN | Values; concurrent-run rule | BuildReconciliation L142–177 | NOT GIVEN |
| OD-11 | Material factor only | Direction: explicit valid factor | OPEN | Density sources | MaterialUOM.ConversionFactor (unused) | NOT GIVEN |
| OD-12 | Product and site | Owner 2026-11-13 question open | OPEN | Sites and lines | No formula entity | NOT GIVEN |
| OD-13 | Engineering owns, PPC approves (maker-checker) | Owner 2026-11-13 question open | OPEN | Roles | WorkflowPlatform exists | NOT GIVEN |
| OD-14 | MR and forecast for v1 | Owner 2026-11-13 question open | OPEN | Where customer orders are held | Capability inventory (no sales order entity) | NOT GIVEN |
| OD-15 | WorkflowPlatform maker-checker | Direction: WorkflowPlatform; creator cannot approve | OPEN | Approval chain | PurchaseOrderApprovalService | NOT GIVEN |
| OD-16 | Hangfire run, idempotent | Direction: reuse Hangfire; no new scheduler | OPEN | Run frequency and size | LicensingExtensions L56 | NOT GIVEN |
| OD-17 | One transaction for data and audit | Direction: approved (A3) | Direction approved; scope OPEN | Outbox scope | PEMSDbContext L582–622 | NOT GIVEN |
| OD-18 | Row versions on aggregate roots | Direction: modified; ledger excluded | Direction given; list OPEN | Aggregate root list | BaseEntity L24–27 | NOT GIVEN |
| OD-19 | Nullable source key | Direction: add only where integration needs it | OPEN | Target ERP and MES | No external key | NOT GIVEN |
| OD-20 | Exclusive attribution (AD-06) | Not yet given | OPEN | Confirm no double case | A7, R22 | NOT GIVEN |
| OD-21 | UOM required on new lines | Direction: approved for new lines | Direction given; backfill OPEN | Backfill rule | X2 evidence | NOT GIVEN |
| OD-22 | Date effectivity | Not yet given | OPEN | Change-control practice | A11; RoutingRule-Assessment | NOT GIVEN |
| OD-23 | No claim in v1 | Not yet given | OPEN | Scope in writing | Document 05 (8 NOT VERIFIED) | NOT GIVEN |

## 4. Dependency map

Arrows read "must be decided before".

- **OD-01 (Hybrid)** -> OD-02, OD-14 (scope of product types).
- **OD-02** -> OD-03, OD-12, OD-22, F1, X12.
- **OD-03** -> OD-22.
- **OD-13** -> OD-02, OD-15 (who approves).
- **OD-12** -> OD-02, OD-22.
- **OD-04** -> OD-05, OD-06, OD-07, OD-16.
- **OD-05** -> OD-06, OD-07, plan row 0227.
- **OD-06** -> OD-14, OD-16, F2 release gate.
- **OD-07** -> OD-05 (finite needs calendar data), plan row 0033.
- **OD-08** -> OD-09 (recall depth), OD-10.
- **OD-09** -> OD-20.
- **OD-10** -> OD-11 (units), OD-08.
- **OD-11** -> OD-21.
- **OD-15** -> OD-16 (release gate).
- **OD-17** -> OD-18 (same transaction), OD-19 (outbox targets).
- **OD-19** -> OD-14 (external orders), integration stage.
- **OD-23** -> standards verification (eight NOT VERIFIED references).

Cycle check: no cycle found. OD-02 and OD-12 are linked through the method owner; decide OD-12 with OD-02.

## 5. Prioritized decision roadmap (for decisions only, not implementation)

1. **Round 1 (business):** OD-02, OD-03, OD-12, OD-13. Unlocks F1, X12 and the method model.
2. **Round 2 (planning):** OD-04, OD-05, OD-07, OD-14, then OD-06. Unlocks F2, F3, F6 and plan row 0227.
3. **Round 3 (quality and traceability):** OD-08, OD-09, OD-10, OD-11, OD-21, OD-20. Unlocks F7, F9, F13, F14, X2.
4. **Round 4 (workflow and run):** OD-15, OD-16, OD-22. Unlocks A1, A2, F5 gates.
5. **Round 5 (integrity and integration):** OD-17 scope, OD-18 list, OD-19 target systems. OD-19 may wait for the integration stage.
6. **Round 6 (governance):** OD-23 compliance scope. Can run in parallel with any round after the eight standards are verified.

Each round ends with an owner decision record. Nothing starts automatically.

## 6. Architecture trade-off analysis

| Area | Process | Discrete | Hybrid | Existing PEMS fit | ISA-95 (secondary identifier only) | ISA-88 (NOT VERIFIED) | ASCM practice (reference) | Data integrity | Performance (NOT ASSESSED) | Maintainability | ERP/MES integration |
|---|---|---|---|---|---|---|---|---|---|---|---|
| OD-02 two structures | Formula fits | BOM later | Needs both | New types; no reuse | Product definition (identifier only) | Recipe concepts, unverified | Standard practice | Versioned, immutable | Not measured | Two models | Needs external IDs later |
| OD-06 new MRP calculator | Time phase | Time phase | Same | Reuses collector; procurement untouched | Not applicable | Not applicable | MRP method | Procurement unchanged | Not measured | Some duplication | Neutral |
| OD-05 hours capacity | Rate standard may be needed | Hours fits | Mixed | ProductionWorkCenter extended | Resource model | Not applicable | Capacity planning | UTC storage | Not measured | Clear unit | Neutral |
| OD-16 Hangfire | Long runs | Long runs | Same | Reuses infrastructure | Not applicable | Not applicable | Not applicable | Idempotent | Not measured | Single pattern | Good |
| OD-17 one transaction | Required | Required | Required | Replaces two-step save | Not applicable | Not applicable | Not applicable | Strong | Slight cost | Single pattern | Outbox needed later |
| OD-18 aggregate roots | Required | Required | Required | Extends BaseEntity pattern | Not applicable | Not applicable | Not applicable | Strong for mutable data | Low cost | Limited scope | Neutral |
| OD-19 keys later | Later | Later | Later | Schema not changed now | Not applicable | Not applicable | Not applicable | Unique per source | Not applicable | Avoids unused schema | Required later |

Where a decision depends on plant parameters (capacity units, efficiencies, tolerances, horizons, traceability times, ERP and MES names), the trade-off above is incomplete until **PLANT DATA REQUIRED** is supplied.

## 7. Owner decision questions (explicit options, with recommendation)

**Q1 (OD-02): Product structure.**
Options: (A) one BOM for all; (B) one formula for all; (C) process formula now, discrete BOM later.
**Recommendation: C.**

**Q2 (OD-03): Operation sequence name.**
Options: (A) keep "routing"; (B) ProcessRoute for process and OperationRouting for discrete; (C) one name "Operation sequence".
**Recommendation: B.**

**Q3 (OD-12): Who owns a method.**
Options: (A) product for all sites; (B) product and site; (C) per order.
**Recommendation: B.** Need: number of sites and lines.

**Q4 (OD-13): Who maintains formulas and BOMs.**
Options: (A) engineering owns, PPC approves; (B) PPC owns, engineering approves; (C) creator and approver separated by workflow, owner named per product class.
**Recommendation: C.** Need: the plant's engineering and PPC roles.

**Q5 (OD-14): Sales orders.**
Options: (A) MR and forecast only for v1; (B) sales order entity in PEMS; (C) customer orders imported from an ERP.
**Recommendation: A now, C later if an ERP holds orders.** Need: where customer orders live today.

**Q6 (OD-04): Horizon and buckets.**
Options: (A) one bucket for all levels; (B) monthly APP and weekly MPS as defaults; (C) configurable per plant, with no fixed value.
**Recommendation: C, with B as the starting configuration.** Need: horizon length in weeks or months and the freeze period.

**Q7 (OD-05): Capacity unit.**
Options: (A) hours per bucket with efficiency; (B) units per hour; (C) batches per bucket.
**Recommendation: A for v1.** Need: unit and efficiency per work centre.

**Q8 (OD-07): Finite or infinite capacity.**
Options: (A) infinite loading with warnings; (B) finite basic scheduling; (C) advanced APS.
**Recommendation: A for v1, B later.** C deferred per owner direction. Need: sequencing and changeover data.

**Q9 (OD-06): Demand netting.**
Options: (A) extend NetDemandCalculator; (B) new time-phased MRP reusing the collector; (C) separate planning context with read link.
**Recommendation: B.** Need: lead times per material and lot sizing rule.

**Q10 (OD-08): Quarantined state.**
Options: (A) map to Blocked; (B) new Quarantined state for all holds; (C) Blocked for QC failure, Quarantined for post-release hold.
**Recommendation: C.** Need: who may release from quarantine.

**Q11 (OD-09): Traceability.**
Options: (A) lot-first with ledger as source; (B) serial-first; (C) stored batch balances.
**Recommendation: A, serial configurable per product.** Need: legal traceability requirement and maximum recall time.

**Q12 (OD-10): Tolerance values.**
Options: (A) zero tolerance default; (B) percentage per material; (C) absolute quantity per material and unit.
**Recommendation: A as default, with B or C set by the plant.** No value is proposed. Need: plant values.

**Q13 (OD-11): Conversion factors.**
Options: (A) material-specific factors only; (B) global dimension factors plus density; (C) no conversion.
**Recommendation: A, reject when no valid factor exists.** Need: density sources.

**Q14 (OD-15): Approval chain.**
Options: (A) WorkflowPlatform with creator exclusion; (B) service check only; (C) separate production approval module.
**Recommendation: A.**

**Q15 (OD-16): Run model.**
Options: (A) Hangfire job per run with idempotency key; (B) synchronous request; (C) new scheduler.
**Recommendation: A.** Need: run frequency and expected size.

**Q16 (OD-17 to OD-21): Integrity and integration.**
- OD-17: confirm one transaction; outbox scope later. **Recommendation: confirm.**
- OD-18: approve aggregate-root list once written. **Recommendation: aggregate roots only.**
- OD-19: name the ERP and MES; **recommendation: defer schema until a target is named.**
- OD-20: confirm no legitimate double-attribution case; **recommendation: exclusive attribution.**
- OD-21: confirm UOM required on new lines; **recommendation: confirm, backfill from verified source only.**

**Q17 (OD-22): Effectivity.**
Options: (A) date; (B) quantity range; (C) both.
**Recommendation: A.** Need: change-control practice.

**Q18 (OD-23): Compliance scope.**
Options: (A) no claim in v1; (B) alignment statements with verified sources; (C) formal certification.
**Recommendation: A now, B after the eight references are verified.**

**Q19 (OD-08 and OD-01): Hybrid scope.**
Options: (A) process only in v1; (B) process and hybrid in v1; (C) process first, hybrid in a later phase.
**Recommendation: C.** Need: which products are hybrid today.

## 8. Items that cannot be decided from the repository
- Plant operating parameters (capacity, efficiency, calendars, tolerances, horizons, lead times, legal traceability times, density, change-control notice): **PLANT DATA REQUIRED**.
- ERP and MES systems and interfaces: **PLANT DATA REQUIRED**.
- Organisational roles: **PLANT DATA REQUIRED**.
- Performance under plant load: **NOT ASSESSED**.
- Standards: eight NOT VERIFIED (ISA-88 / IEC 61512, ISO 22400-1 and -2, EPCIS 2.0.1, ISA/IEC 62443 parts, ISA-95 parts 2 to 5). No compliance claimed.
- Discrepancy found in this review: the owner's direction names **Quarantined**, which is not a current ProductionBatch state (Produced, AwaitingQC, Released, Blocked, Rejected). This is handled as a question (Q10).

## 9. Closure rule
No OD item is closed by this document. Final Closure Approval is NOT GIVEN for all 23 items. Each closure needs the owner's written approval in the matrix in section 3.
