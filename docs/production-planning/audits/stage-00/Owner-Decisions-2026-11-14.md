# Owner decisions on the Major-Decision-Register (PPC Stage 0, 2026-11-14)

> **Historical record:** this was the scope and authorisation boundary on 2026-11-14. It was superseded by the explicit Stage 1 authorisation recorded in `Owner-Decisions-2026-11-16.md`. The open conditions in this document remain relevant only where the later decision did not settle them.

**Scope of these decisions:** business and architecture principles only. They did **not at that date** authorise Stage 1, source code, database changes, EF migrations, configuration, test code, Git commits, pushes, pull requests or Jira updates.

**Status values:**
- **APPROVED (principle):** the owner approved the stated principle. No open OD item is named for it.
- **MODIFIED (open conditions):** the owner approved with conditions. The conditions are listed and remain open.
- **DEPENDENT (OPEN):** the owner's direction depends on OD items that are not yet decided. The row stays OPEN until those items are resolved.

No corrective-action row was finally closed on 2026-11-14. The owner later dispositioned all 26 Major findings for Stage 1; implementation closure still requires verified evidence.

## 1. Row-by-row decisions

| ID | Owner decision | Status | Owner direction and conditions | Unresolved OD or dependency |
|---|---|---|---|---|
| F1 | MODIFY | DEPENDENT (OPEN) | Versioned Process Formula; separate ProcessRoute / OperationRouting concepts; do not reuse document RoutingRule | OD-02, OD-03 |
| F10 | MODIFY | MODIFIED (open conditions) | Output allowed only into an open, actively producing batch. Output rejected for AwaitingQC, Released, Rejected and Quarantined batches. Exceptional reopening only through a controlled workflow | Condition: a written definition of "open, actively producing" state |
| F11 | APPROVE | APPROVED (principle) | QC-controlled inputs must come from Released batches. Non-QC-controlled materials follow their approved material policy | Condition for later work: the material policy text must exist |
| F12 | APPROVE | APPROVED (principle) | Quarantine and recall after release, with traceability and reversal support | Implementation needs separate authorisation |
| X12 | MODIFY | DEPENDENT (OPEN) | Replace free-text yield and loss classes with controlled, configurable master data. Final taxonomy needs manufacturing-domain validation | OD-02; manufacturing-domain validation |
| F2 | MODIFY | DEPENDENT (OPEN) | MRP initially produces recommendations only; no automatic purchase or production order. Reuse NetDemandCalculator only if its semantics support time-phased MRP | OD-06 |
| F3 | MODIFY | DEPENDENT (OPEN) | Start with bucket-level RCCP. Defer detailed CRP until reconciled | OD-05, OD-07, row 0227 |
| F4a | MODIFY | DEPENDENT (OPEN) | Confirm APP to MPP to MPS architecture; planning horizon and bucket configuration | OD-04 |
| F4b | APPROVE | APPROVED (principle) | MPP versions immutable. Corrections create new versions. An approved or released plan is never overwritten | None named |
| A1 | MODIFY | DEPENDENT (OPEN) | Reuse existing Hangfire; no new scheduler; idempotent planning requests; controlled concurrency | OD-16 |
| F5 | MODIFY | MODIFIED (open conditions) | Separate master-plan approval and release from production-order execution release, with different validation gates | Condition: write both gate definitions. Depends on outcomes of F2 and F3 |
| A2 | MODIFY | DEPENDENT (OPEN) | Use WorkflowPlatform for maker-checker. The creator cannot approve their own release | OD-15 |
| F7 | MODIFY | DEPENDENT (OPEN) | Tolerances configured by material, unit, production mode and process or site. No universal numeric tolerance is invented | OD-10 (owner supplies values) |
| F8 | APPROVE | APPROVED (principle) | Hold and cancel propagate to relevant active runs. Completed operations and posted transactions are preserved | None named |
| F9 | MODIFY | DEPENDENT (OPEN) | Cumulative planned quantity plus approved tolerance. Several concurrent runs only with explicit quantity and resource allocations and concurrency protection | OD-10 |
| X15 | APPROVE | APPROVED (principle) | Compensating or reversing inventory transactions. Posted transactions are never deleted. Reversals reference the original and are idempotent | None named |
| F6 | MODIFY | DEPENDENT (OPEN) | Production calendar with shifts, holidays, planned shutdowns and maintenance downtime. Basic production scheduling is in PPC scope. Advanced APS optimisation is deferred | OD-05, OD-07 |
| F13 | MODIFY | DEPENDENT (OPEN) | The inventory ledger is the source of truth. Batch balances are derived, or provided as a reconciled read projection, never as an independent authoritative balance | OD-09 |
| F14 | MODIFY | DEPENDENT (OPEN) | Multi-level batch or lot genealogy for both production types. Serial genealogy configurable for applicable discrete products | OD-09 (serial scope) |
| F15 | APPROVE | APPROVED (principle) | Reconciliation per material. Unrelated material balances are never aggregated | None named |
| F16 | APPROVE | APPROVED (principle) | Mixed-unit aggregation rejected. Conversion only with an explicit, valid, dimensionally compatible factor | None named (detail of conversion policy is under X2) |
| X2 | MODIFY | DEPENDENT (OPEN) | UOM required on all new material and transaction lines. Legacy backfill only from verified source information. Missing units are never assumed | OD-11 (conversion policy detail); OD-21 direction given |
| A3 | APPROVE | APPROVED (principle) | Business changes and their audit records in one database transaction. Transactional outbox for cross-system delivery where needed | Outbox design and implementation need separate authorisation |
| A4 | MODIFY | MODIFIED (open conditions) | Optimistic concurrency or versioning on mutable aggregate roots. No row versions added to immutable append-only ledger records | Condition: list of mutable aggregate roots to be written. OD-18 direction given |
| A5 | MODIFY | DEPENDENT (OPEN) | External identifiers only for integration-relevant entities. Uniqueness by SourceSystem plus ExternalId; internal primary keys kept | OD-19 (integration scope and timing) |
| X21 | APPROVE | APPROVED (principle) | Independent test strategy, including calculation oracles | Writing and running new tests needs separate implementation authorisation |

**Count:** 9 APPROVED (principle); 3 MODIFIED with open conditions (F5, F10, A4); 14 DEPENDENT (OPEN) (F1, X12, F2, F3, F4a, A1, A2, F7, F9, F6, F13, F14, X2, A5). Total 26. No row is finally closed.

## 2. Additional owner directions (policy level, not implemented)

### 2.1 Process manufacturing loss taxonomy (configurable reason codes)
Startup or purge loss; changeover loss; normal process loss; sampling loss; handling or spillage loss; quality rejection; moisture or volatile loss where applicable.

### 2.2 Discrete manufacturing loss taxonomy (configurable reason codes)
Setup scrap; operation scrap; assembly damage; quality rejection; inspection or test failure.

Rework, recovered materials, recycled materials, co-products and by-products must stay distinguishable from ordinary losses. The final taxonomy needs manufacturing-domain validation (open).

### 2.3 Release gate policy
- **Gate 1, master plan (MPP or MPS):** approval and release are separate from execution. A planning run may show and publish shortages and capacity constraints, with explicit warnings and documented approval.
- **Gate 2, execution (production order or run):** hard constraints enforced: invalid production structure; unavailable mandatory material; unreleased QC-controlled input; unavailable required resource.
- Any approved exception is authorised and auditable.
- Gate definitions still need to be written (open condition, see F5).

### 2.4 Batch and genealogy policy
- Process manufacturing: batch-based genealogy.
- Discrete manufacturing: lot-based genealogy.
- Serial tracking: configurable by product and business requirement.
- Genealogy: multi-level, forward and backward traceability.

### 2.5 Scheduling policy
Basic production scheduling is inside PPC scope. Advanced optimisation, autonomous scheduling and advanced APS are deferred to a later approved phase.

## 3. Existing plan row decisions (no plan row edited in Stage 0)

| Row | Owner decision | Direction |
|---|---|---|
| 0227 | MODIFY | Reconcile with the PPC capacity model |
| 0250 | DEFER | Acceptance deferred until required acceptance tests have been executed. Not Verified until then |
| 0033 | DEFER | Final dependency decision deferred until existing Gantt capabilities and PS scope are reviewed |
| 0092 | DEFER | Outside PPC scope unless an actual dependency is demonstrated |
| 0095 | DEFER | Outside PPC scope unless an actual dependency is demonstrated |

## 4. Unresolved OD dependencies for owner review

| OD | Question | Rows waiting on it | State |
|---|---|---|---|
| OD-02 | Product structure: formula or BOM, yield, co-products | F1, X12 | OPEN |
| OD-03 | Manufacturing routing name and definition | F1 | OPEN |
| OD-04 | Planning horizon and time buckets (APP and MPS) | F4a | OPEN |
| OD-05 | Capacity unit, calendar, shifts, time basis | F3, F6 | OPEN |
| OD-06 | Production demand source and MRP netting | F2 | OPEN |
| OD-07 | Finite or infinite capacity; PS scope | F3, F6 | OPEN |
| OD-09 | Lot or serial scope; genealogy depth for serials | F13, F14 | OPEN |
| OD-10 | Tolerance values by material, unit, mode and site; concurrent run rule | F7, F9 | OPEN |
| OD-11 | Detailed unit conversion policy | X2 (and F16 detail) | OPEN |
| OD-15 | WorkflowPlatform maker-checker for release | A2 | OPEN |
| OD-16 | Planning run model and Hangfire boundary | A1 | OPEN |
| OD-19 | External keys: integration scope and timing | A5 | OPEN |

Decisions taken in direction but with open scope (not finally closed):
- OD-17 (transactional audit): direction given through A3 (APPROVED). Implementation scope open.
- OD-18 (row versions): direction given through A4 (MODIFIED). List of aggregate roots open.
- OD-21 (UOM required): direction given through X2 (MODIFIED). Backfill rules open.
- OD-01 (process first): decided earlier. Hybrid scope still open.

OD items not tied to a Major row in this matrix and still open: OD-08 (quality gate detail for other batch states), OD-12 (method ownership), OD-13 (BOM and recipe data owner), OD-14 (sales order scope), OD-20 (attribution rule for shared transactions), OD-22 (effectivity and routing versioning), OD-23 (compliance scope).

## 5. Open conditions with no OD number (must be written before a row can close)
1. Written definition of the "open, actively producing" batch state (F10).
2. Material policy text for non-QC-controlled materials (F11).
3. Manufacturing-domain validation of the loss taxonomy (X12).
4. Gate 1 and Gate 2 definitions (F5).
5. List of mutable aggregate roots for optimistic concurrency (A4).
6. Transactional outbox design for cross-system delivery (A3).
7. Reconciliation of row 0227 with the PPC capacity model (F3, plan row decision).

## 6. Authorisation boundary
This record does not authorise: Stage 1; architecture implementation; source code; database changes; EF migrations; configuration; test code; Git commits, pushes or pull requests; Jira updates. The current task remains documentation and decision-register reconciliation only.

## 6. Owner response to Q1 to Q19 (2026-11-14)
Recorded in Owner-Response-Q1-Q19-2026-11-14.md. Direction recorded; no OD closed; plant data still required.
