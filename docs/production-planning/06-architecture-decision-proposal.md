# 06 Architecture decision records (PPC Stage 0, 2026-11-14)

**Status values:** **OWNER-CONFIRMED** (the owner has explicitly confirmed it in writing, 2026-11-14) or **PROPOSED** (analysis only; the owner has not confirmed it). No PROPOSED item may be read as approved.

---

## ADR-001 MPP, APP, MPS definition and the no-parallel-engine rule
**Status: OWNER-CONFIRMED (2026-11-14).**

**Decision**
- **MPP** = Master Production Plan: the approved, versioned plan that is the **output of the APP process**. It is a document or entity, not a calculation engine.
- **APP** = Aggregate Production Planning: the macro planning process.
- **MPS** = Master Production Schedule: the detailed schedule of products, derived from an approved MPP version.
- **MPP must not create a separate computation engine or a parallel module.** Calculations belong to the APP, MPS, RCCP, MRP, CRP and PS capabilities; MPP only holds the approved, versioned result.

**Consequences**
1. An approved MPP is immutable. A change creates a new version (see AD-10).
2. APP runs are the only process that produces MPP versions. MPS is derived from an approved MPP version.
3. No new module, DbContext or service named "MPP" is created for calculation. Storage of approved versions may use an entity inside the planning context, if the owner approves the data model later.
4. This decision supersedes the earlier owner wording "approved, versioned output of the comprehensive production plan" (2026-11-13). The two must not both be used.

**Where recorded:** `03-business-glossary-and-decisions.md` (glossary, status OWNER-CONFIRMED); `APP-MPS-Assessment.md`.

---

## Stage 0 proposals and their later disposition

The entries below preserve the Stage 0 design rationale. Their 2026-11-14 "Decision needed" wording is historical. The owner confirmed the Stage 1 direction on 2026-11-16 in `audits/stage-00/Owner-Decisions-2026-11-16.md`; remaining plant-specific values and release evidence are still open.

### AD-01 Planning engine boundary and run model
- **Context:** no planning engine; Hangfire runs only licensing, letters, OCR, tenders, uploads (Audit-02 A1).
- **Proposal:** calculation services in `PEMS.Application`, same pattern as `NetDemandCalculator`. Planning runs execute as Hangfire jobs with an idempotency key (scope and input version). No new scheduler.
- **Decision needed:** OD-16.

### AD-02 Plan approval and release through WorkflowPlatform
- **Context:** `ProductionOrderService.ReleaseAsync` changes status directly; creator may approve (Audit-02 A2).
- **Proposal:** plan approval, BOM or recipe release and production release are workflow stages with maker-checker.
- **Decision needed:** OD-15.

### AD-03 Data and audit in one transaction
- **Context:** `PEMSDbContext` writes audit in a second save (Audit-02 A3).
- **Proposal:** one database transaction for data and audit rows, using the execution-strategy pattern of `NumberingService`.
- **Decision needed:** OD-17.

### AD-04 Row versions on production and stock tables
- **Context:** no RowVersion on order, stock, transaction (Audit-02 A4).
- **Proposal:** additive migration adding row versions; never edit an applied migration.
- **Decision needed:** OD-18.

### AD-05 External keys for ERP and MES
- **Context:** no external key (Audit-02 A5).
- **Proposal:** nullable source system and external key, unique per source; idempotent import.
- **Decision needed:** OD-19.

### AD-06 Attribution rule for shared inventory transactions
- **Context:** a transaction can carry both a work order and a batch (Audit-02 A7).
- **Proposal:** maintenance use sets only the work order; production use sets only the batch. Enforced in service and by a check constraint.
- **Decision needed:** OD-20.

### AD-07 UOM required on production lines
- **Context:** UOM nullable; conversion unused (Audit-02 A9, Audit-01 F17).
- **Proposal:** UOM required on new lines; backfill with a documented default per material.
- **Decision needed:** OD-21 and OD-11.

### AD-08 Time basis
- **Context:** production code uses local time (Audit-03 X20).
- **Proposal:** store UTC; display in the user's time zone.
- **Decision needed:** OD-05.

### AD-09 Process first; discrete later; separate routing contract
- **Context:** owner decided process first (2026-11-13). `RoutingRule` is a document routing table and must not be reused for operations (RoutingRule-Assessment.md).
- **Proposal:** Stage 1 models process structures (formula, yield, co-products, batch) and a process routing contract. Discrete (BOM, operations, serial) follows later.
- **Decision needed:** OD-01 (Hybrid scope) and OD-03.

### AD-10 Plan versions and reversal of posted stock
- **Context:** no plan version; cancel does not reverse stock (Audit-03 X14, X15).
- **Proposal:** each approved plan is an immutable version (MPP as ADR-001 defines it); changes create a version. Cancelling a released run posts a reversing transaction, not a delete.
- **Decision needed:** OD-04, OD-10.

### AD-11 Service-layer permission checks
- **Context:** controllers check permissions; services do not (Audit-03 X17).
- **Proposal:** same permission checks in planning services.
- **Decision needed:** none beyond AD-02.

---

## Explicitly not proposed
- No separate planning database or DbContext.
- No separate authorization, audit or inventory ledger.
- No WorkOrder to ProductionOrder link.
- No direct PLC, SCADA or DCS write path (Audit-02 A6).
- No automatic release of purchase or production orders by a planning run.
- No MPP calculation engine (ADR-001).
