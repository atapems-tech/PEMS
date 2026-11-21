# PEMS Production Planning & Control (PPC): Stage 0 executive summary (Updated 2026-11-16)

**Status:** Stage 0 documentation is complete. **The gate HAS PASSED (2026-11-16).** All 26 Major findings received final owner disposition from Hamid Maleki (`Owner-Decisions-2026-11-16.md`). **Stage 1 (Domain Model, Architecture Design & Additive Implementation) is AUTHORISED.**

## What exists in PEMS
- Production execution: orders, runs, batches with a QC release gate, inputs, outputs and losses that post to inventory (output only after release), targets, downtime and a dashboard.
- Reusable: inventory ledger, CMMS equipment, the workflow platform, Hangfire, AuditLog, Excel export, KPI catalogue.
- Stack: .NET 10, EF Core 10.0.10, SQL Server.

## What is missing for planning (all Major; none Critical)
- **F1 (Major, gate blocking):** no BOM, formula or manufacturing routing. Owner classified as Major.
- **F2 (Major, gate blocking):** no production MRP and no lead times.
- **F3 (Major, gate blocking):** no capacity model, RCCP or CRP.
- **F4a (Major, gate blocking):** no APP and MPS layer, confirmed absent by search (0 matches).
- **F4b (Major, MPP design blocking):** no plan versioning.
- **Other Major rows (21):** release is a status change only; no calendar or scheduling; no quantity closure; hold and cancel do not reach runs; over-production allowed; QC gates missing on inputs and outputs; no quarantine or recall; no discrete traceability; reconciliation and KPI sums mix materials and units; no reversal of posted stock; no planning run model; audit and data in two steps; no row versions; no external keys; no planning tests; standards editions unverified.
- `RoutingRule` is a document routing table, not a manufacturing routing.

Full list: `audits/stage-00/Critical-Major-Findings-Register.md` (26 Major rows, 0 Critical). Individual review: `audits/stage-00/Major-Decision-Register.md`.

## Settled decisions
- **ADR-001 (owner-confirmed):** APP is the aggregate planning process; MPP (Master Production Plan) is the approved, version-controlled plan that is the output of APP; MPS (Master Production Schedule) is the detailed product schedule derived from an approved MPP version. MPP does not create a computation engine or a parallel module. See `06-architecture-decision-proposal.md`.
- **Process is the first production type** (owner, 2026-11-13).

## Standards
- ISO 9001:2026 (Edition 6, published 16 September 2026) and ISO 19011:2026 (Edition 4, published 2026-05) were read from official ISO pages.
- Eight other references are NOT VERIFIED from official sources.
- Publication of a standard does **not** show that PEMS complies. No conformity or certification is claimed.

## Three reviews
- Pass 1: 23 findings. The F1 rating was Critical; the owner reclassified it to Major, gate blocking.
- Pass 2: no Critical; Major for the same gaps.
- Pass 3: 19 confirmed items after recount (the earlier figure of 17 was wrong). Role-based internal review, **not an independent third-party audit**.

## Open
- Owner direction recorded for the 26 Major findings: 9 approved as principle, 3 modified, 14 dependent on open OD items (see Owner-Decisions-2026-11-14.md).
- Owner decisions OD-01 to OD-23 (see `03-business-glossary-and-decisions.md`).
- RCCP, MRP, CRP, PS and MRP II definitions: owner terms, definitions proposed. APS and S&OP scope undecided.
- Plan row 0250 (Done, not verified) and row 0227 (capacity overlap) need owner decisions. Rows 0092 and 0095 stay outside PPC unless a dependency is proven. Row 0033 (Gantt) has no Gantt component in the repository.

## Documents
- Requirements traceability: `04-requirements-traceability-matrix.md` (updated with REQ-30 to REQ-46)
- Architecture decisions: `06-architecture-decision-proposal.md`
- Domain model specification: `08-domain-model-specification.md` and `08-domain-model-addendum.md`
- Supplementary standards & decisions: `10-supplementary-decisions-and-requirements.md` (OD-24 to OD-29)
- Revised wave map: `11-revised-wave-map.md`
- Standards gap index: `12-standards-gap-index.md` (ISA-88, ISO 22400, ISA-95, APICS ASCM, GS1 EPCIS)
- Integration & B2MML architecture: `13-integration-and-b2mml-architecture.md`
- Standards: `05-standards-register-and-mapping.md`
- Petrochemical & continuous process study: `18-petrochemical-process-domain-study.md`
- Non-functional requirements & reliability: `19-non-functional-requirements-and-governance.md`
- Risks (final register): `15-risk-and-corrective-action-register.md`
- Test strategy: `14-test-strategy.md`
- Roadmap: `16-implementation-roadmap.md` (Stage 1 is authorised; each implementation wave still needs its own tested delivery.)
- Release readiness: `17-release-readiness-assessment.md`
- Gate result and file list: `audits/stage-00/Stage-00-Gate-Assessment.md`

## Next
Stage 1 is authorised by the owner decision of 2026-11-16. The next work is to implement the approved additive model in controlled waves, beginning with the data-integrity and test prerequisites. Open plant data, standards verification and release gates remain open; they do not reverse the Stage 1 authorisation.
