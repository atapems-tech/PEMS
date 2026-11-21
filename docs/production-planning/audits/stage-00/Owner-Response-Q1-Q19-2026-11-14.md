# Owner response to OD questions Q1 to Q19 (PPC Stage 0, 2026-11-14)

> **Historical record:** this response records the position on 2026-11-14. Its lack of implementation authorisation was superseded by `Owner-Decisions-2026-11-16.md`, which authorised Stage 1. Plant-specific parameters and acceptance conditions remain open.

**Recorded as:** owner direction ("Accept with Modifications"). It was **not at that date** implementation authorisation. No OD item was closed at that time.

**Authorisation boundary at 2026-11-14:** no source code, database, EF migration, configuration, test, Git commit, push, PR or Jira change. No Stage 1.

## 1. Owner decisions, Q1 to Q19

| Q | OD | Owner decision | Owner direction recorded | Remaining open (not closed) |
|---|---|---|---|---|
| Q1 | OD-02 | MODIFY | Target architecture supports process AND discrete manufacturing from the start. No process-only domain model that blocks discrete. Phase 1: process formula and process manufacturing. Phase 2: discrete BOM and discrete manufacturing. Later: hybrid execution | Formula yield and co-product data (plant); phase boundaries to be confirmed in the roadmap |
| Q2 | OD-03 | ACCEPT | ProcessRoute for process; OperationRouting for discrete. RoutingRule never used as a production routing | None for naming; UI wording to confirm |
| Q3 | OD-12 | ACCEPT | Method associated with Product and Manufacturing Site/Plant; versioning and effective dates. Site and line counts not hard-coded | Number of sites and lines (PLANT DATA) |
| Q4 | OD-13 | MODIFY | Separate Data Owner, Creator, Reviewer, Approver. Creator cannot approve own change. Functional responsibilities for Engineering, Quality, PPC, Production, mapped to real positions | Actual organisational positions and permissions (PLANT DATA). No invented positions |
| Q5 | OD-14 | MODIFY | V1 demand: approved production demand requests and approved demand forecasts. Customer order integration deferred until system of record is identified. No ERP sales-order integration claimed | Exact business meaning of "MR" must be clarified before implementation (OPEN). System of record for customer orders (PLANT DATA) |
| Q6 | OD-04 | MODIFY | Configurable defaults: APP 12-month rolling, monthly buckets. MPS 13-week rolling, weekly buckets, 2-week initial freeze. Configurable by plant and planning policy. Freeze exceptions by controlled approval; not hard-coded | Plant and planning-policy confirmation of defaults (PLANT DATA) |
| Q7 | OD-05 | MODIFY | Common basis: hours per planning bucket; effective capacity = approved work-center efficiency. Process constraints: kg/h, t/h, batch capacity, equipment volume, maximum batch size. Discrete: pieces/h, machine hours, labour hours, setup hours. No 100% efficiency assumed; missing capacity data reported as incomplete or unvalidated | Units and efficiency factors per work center (PLANT DATA) |
| Q8 | OD-07 | ACCEPT with scope clarification | V1: infinite capacity loading, overload warnings, priority-based sequencing, due-date visibility, basic scheduling. V1 must not claim the schedule is physically executable under finite capacity. Later: finite scheduling, advanced sequencing, APS | Sequencing and changeover data (PLANT DATA) |
| Q9 | OD-06 | MODIFY | Time-phased MRP engine. Reuse demand collector only after verifying its semantics. Lot-for-lot default; respect MOQ and order multiples; material-specific lot-sizing. Lead times only from approved material, supplier and site records. Missing values reported, never invented. Procurement workflow not modified | Verification of collector semantics (OPEN, engineering task). Lead-time and lot-sizing records (PLANT DATA) |
| Q10 | OD-08 | MODIFY | Blocked = QC failure or material not acceptable. Quarantined = previously released batch on hold for later risk or investigation. Only authorised Quality releases a quarantined batch, through WorkflowPlatform, with reason, evidence, authorisation and audit trail. Requester cannot approve own release | Who holds the Quality authority (PLANT DATA). Quarantined is a new state (see section 3) |
| Q11 | OD-09 | MODIFY | Lot-first traceability. Batch genealogy for process; lot genealogy for discrete; serial configurable per product. Ledger remains source of truth. Multi-level forward and backward genealogy. No invented retention periods or recall deadlines | Legal retention, recall deadlines, customer and regulatory requirements (PLANT DATA, to be captured before final configuration) |
| Q12 | OD-10 | ACCEPT | Default overrun tolerance ZERO. Exceptions configured explicitly by material and manufacturing context. Tolerance changes require authorisation and audit logging. No silent user-level override | Any non-zero tolerance values (PLANT DATA; none assumed) |
| Q13 | OD-11 | MODIFY | Material-specific, dimensionally valid factors. Reject lines with missing or invalid factor. Density only from approved engineering specifications, supplier CoA for lot data, or verified lab measurements; record source, validity, unit, conditions. No generic or unverified density | Approved density sources (PLANT DATA) |
| Q14 | OD-15 | MODIFY | Use existing WorkflowPlatform. Creator and approver separate. Functional responsibilities: Engineering (formula, BOM, routing ownership); PPC (planning preparation and review); Plant Management (plan approval); Quality (QC release and quarantine disposition); Warehouse (material issue and inventory transaction control). No new workflow engine; no fictional accounts | Map to actual organisation and approval hierarchy (PLANT DATA) |
| Q15 | OD-16 | MODIFY | One Hangfire job per planning run. Each run has unique Run ID, idempotency key, input snapshot or version, calculation algorithm version, controlled concurrency, retry-safe execution, auditable status transitions. V1 on demand or manual. Recurring frequency from actual plant cycles. No new scheduler | Recurring frequency and expected data volume (PLANT DATA) |
| Q16 | OD-17 to OD-21 | ACCEPT (OD-17); ACCEPT (OD-18); DEFER (OD-19); ACCEPT (OD-20); ACCEPT (OD-21) | OD-17: one transaction for business changes and audit. OD-18: optimistic concurrency on mutable aggregate roots only; list derived from repository and domain analysis. OD-19: external ID schema deferred until ERP/MES identified. OD-20: exclusive attribution default; any split via explicit allocation records reconciling to source quantity; no duplicate attribution. OD-21: mandatory UOM on new material and transaction lines; legacy backfilled only from verified information | OD-18 aggregate-root list (OPEN, to derive). OD-19 target systems (PLANT DATA). OD-20 legitimate split scenarios (PLANT DATA). OD-21 legacy verification (OPEN) |
| Q17 | OD-22 | ACCEPT | Effective-date ranges: EffectiveFrom inclusive, EffectiveTo exclusive. On order release, snapshot the exact Formula/BOM and Routing versions. Later engineering changes must not silently alter released orders. Exceptions by controlled engineering change or deviation approval | Change-control practice confirmation (PLANT DATA) |
| Q18 | OD-23 | ACCEPT | V1: no certification or formal conformance claim. Alignment statements only after official edition verification, requirement mapping, evidence, scope assessment and gap review. Wording: "designed with reference to" | Standards verification remains open (eight NOT VERIFIED) |
| Q19 | OD-01 and OD-08 | MODIFY | Process first. Discrete is a mandatory subsequent delivery phase, not an optional deferred feature. Shared architecture supports both from initial design. Hybrid follows after actual hybrid requirements are identified | Existing hybrid product list (PLANT DATA). OD-01 Hybrid scope remains OPEN |

## 2. Unresolved dependencies after this response

### 2.1 Plant-specific data (owner list, kept open)
1. Number of manufacturing sites and production lines.
2. Actual customer-order system of record.
3. Plant Engineering and PPC organisational positions.
4. Work-center capacity units and efficiency factors.
5. Material lead times and lot-sizing parameters.
6. Legal traceability requirements and recall obligations.
7. Approved density sources.
8. Actual approval hierarchy.
9. Planning run frequency and expected data volume.
10. Target ERP and MES integration systems.
11. Mutable aggregate root list (derived from repository and domain analysis; not yet written).
12. Legitimate multiple-attribution scenarios.
13. Existing hybrid product list.

### 2.2 Open conditions the owner did not close
- Meaning of "MR" in V1 demand (Q5).
- Verification of demand-collector semantics before reuse (Q9).
- Validity of OD-21 backfill from verified sources (Q16).
- Phase boundaries and the OD-01 hybrid scope (Q1, Q19).

### 2.3 Effect on the open rows (Major-Decision-Register)
Rows are reclassified only by owner closure. Status now:
- **Direction recorded, still DEPENDENT on plant data or conditions:** F1, X12, F2, F3, F4a, F6, F7, F9, F13, F14, X2, A1, A2, A5.
- **Direction recorded, conditions still open:** F5, F10, A4.
- **Approved as principle earlier (unchanged):** F4b, F8, F11, F12, F15, F16, X15, A3, X21.

No row is finally closed.

## 3. Consequences recorded for design (direction, not implementation)
- Target architecture: process and discrete from the start (Q1). No process-only domain model.
- Naming: ProcessRoute and OperationRouting. RoutingRule is document routing only (Q2).
- New state: Quarantined, distinct from Blocked, with WorkflowPlatform release by Quality only (Q10). This resolves the earlier discrepancy in the Owner Decision Package (Q10 question).
- Demand scope v1: approved production demand requests and approved forecasts only (Q5).
- Capacity: hours per bucket with approved efficiency; no 100% default (Q7).
- Planning horizon defaults are configuration, not code constants (Q6).
- Tolerance default zero, with authorised and audited exceptions (Q12).
- Released orders keep a snapshot of the formula, BOM and routing versions (Q17).
- Standards language: "designed with reference to" only (Q18).

## 4. What has not been done
- No OD item is closed.
- No plant value has been invented. Items in section 2.1 remain PLANT DATA REQUIRED.
- No source code, database, migration, configuration, test, Git, PR or Jira change.
- Stage 1 has not started.
