# Owner decisions on PPC Stage 0 & Stage 1 Authorisation (2026-11-16)

**Decided by:** Hamid Maleki (Project Owner / Member A)  
**Date:** 2026-11-16  
**Status:** **GATE PASSED & STAGE 1 AUTHORISED**

---

## 1. Executive Summary of Owner Decisions

The owner reviewed the five core architectural axes of the PPC (Production Planning & Control) module and explicitly confirmed the strategy on 2026-11-16. All 26 Major findings from Stage 0 audits now have definitive owner disposition. Stage 1 (Domain entities, architecture design, and additive implementation) is **fully authorised**.

---

## 2. Settled Decisions by Axis

### Axis 1: Product Structure & Operation Sequence (OD-02 & OD-03)
- **Status:** **OWNER-CONFIRMED**
- **Process Formula:** The product structure for Process Manufacturing is modeled as `ProcessFormula` with child items `ProcessFormulaItem`. It includes:
  - Code, Name, Description, and Version number.
  - Base batch size and reference unit (`UOM`).
  - Expected yield percentage (`ExpectedYieldPercent`).
  - Standard process loss allowances and loss categories.
  - Definition of Co-products and By-products with allocation percentages.
- **Operation Sequence Naming:** To avoid conflict with existing document routing, letter routing, and storage routing, the manufacturing operation sequence is officially named **`ProcessRoute`** (with operations `ProcessRouteOperation` / `ProcessWorkCenter`).

### Axis 2: Planning Hierarchy & Time Buckets (OD-04)
- **Status:** **OWNER-CONFIRMED**
- **APP (Aggregate Production Planning):**
  - Planning Horizon: **6 to 12 months**.
  - Time Bucket: **Monthly** buckets.
  - Scope: Product families / aggregate capacities.
  - Output: Immutable, versioned **MPP (Master Production Plan)** records.
- **MPS (Master Production Schedule):**
  - Planning Horizon: **4 to 12 weeks**.
  - Time Bucket: **Weekly or Daily** buckets.
  - Scope: Finished and intermediate process products.
  - Execution Rules: Includes a strict **Frozen Zone** where changes require administrative authorization.

### Axis 3: Demand Modeling, Netting & MRP Engine (OD-06 & OD-07)
- **Status:** **OWNER-CONFIRMED**
- **Demand Sources:** Multi-source demand aggregation (Sales Orders, Forecasts, and APP target versions).
- **MRP Engine Behavior:**
  - Net demand calculates gross requirements minus free on-hand stock and incoming approved receipts.
  - The MRP calculation generates **Planning Recommendations (Planned Production Orders & Planned Purchase Requisitions)**.
  - **No automated firm creation:** Orders are never released automatically without planner/supervisor confirmation.
- **Capacity Balancing (RCCP):**
  - Initial implementation uses **Infinite Capacity with Overload Warning** at the work centre, shift, and hourly calendar level.
  - Complex APS (autonomous heuristic scheduling) is deliberately deferred to post-v1.

### Axis 4: Quality Control Gates & Batch Traceability (OD-08 & OD-09)
- **Status:** **OWNER-CONFIRMED**
- **Input Material QC Gate:** Inputs to a production run can only be consumed from inventory batches in `Released` QC status. Unreleased, Quarantined, or Rejected batches are blocked by the service.
- **Output Product QC Gate:** Newly finished goods and intermediates from a production run do not enter free inventory immediately; they are held in **`AwaitingQC` (Quarantine)** until formal inspection release.
- **Genealogy & Traceability:**
  - Multi-level batch genealogy (`ProductionBatchGenealogy`).
  - Forward traceability (from raw material batch to finished goods) and Backward traceability (from finished product batch to specific ingredient lots and supplier tags).

### Axis 5: Platform Architecture & Non-Breaking Governance (OD-15, OD-16, OD-18)
- **Status:** **OWNER-CONFIRMED**
- **Workflow & Maker-Checker:** Plan approval (MPP), Formula publication, and Quarantine release are governed through `WorkflowPlatform` adhering strictly to Maker-Checker (a creator cannot approve their own release).
- **Background Jobs:** Heavy planning runs (MRP netting, MPS regeneration) execute asynchronously via Hangfire as idempotent jobs.
- **Concurrency & Append-Only Safety:**
  - `RowVersion` (optimistic concurrency token) added to mutable aggregate roots (`ProductionOrder`, `ProductionPlan`, `ProcessFormula`).
  - Financial/Stock ledger transactions remain strictly append-only; reversals post compensating entries.

---

## 3. Operational Risk & Code Preservation Statement

As assessed and confirmed on 2026-11-16:
- **Operational Risk Level:** **Minimal / Near Zero**.
- **Additive Implementation:** All Stage 1 models are additive. Existing production execution tables (`ProductionOrder`, `ProductionRun`, `ProductionBatch`) retain 100% backward compatibility via optional/nullable foreign keys.
- **Verification:** All existing tests across the solution must remain green prior to any merge into `main`.
