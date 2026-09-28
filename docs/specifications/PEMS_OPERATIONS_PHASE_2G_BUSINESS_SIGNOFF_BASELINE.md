# PEMS Operations Phase 2G — Business Sign-off Baseline

**This is the only file the Packaging program reads approvals from.**
A decision counts as approved only when its row below has `Status = APPROVED`, a named
`Approver`, a `Decision Date` (YYYY-MM-DD), a chosen option, and an `Evidence` reference (a signed
document, minutes, or a ticket number). A technical recommendation, a chat message, or a comment
in code is not evidence.

## How the program re-evaluates the gate

Run from the repository root; the gate opens for a decision only when this prints an `APPROVED`
line for it:

```
grep -E "^\| (D[1-9]|D1[0-2]|E-[1-5]) .*\| APPROVED \|" docs/specifications/PEMS_OPERATIONS_PHASE_2G_BUSINESS_SIGNOFF_BASELINE.md
```

Current result (2026-09-25): **no lines** — 0 of 16 decisions approved (plus the final GATE row).

Status vocabulary: `OPEN` · `OWNER REVIEW` · `APPROVED` · `REJECTED` · `DEFERRED` · `BLOCKED`.
Full analysis of each decision: `PEMS_OPERATIONS_PHASE_2G_EXECUTIVE_DECISION_AND_ROLE_MAPPING_WORKBOOK.md`.

## Decision Register

Columns: ID | Decision | Status | Business Owner | Approver | Decision Date | Chosen option | Rationale | Conditions | Evidence | Technical Recommendation (NOT BUSINESS APPROVAL) | Implementation Impact

| ID | Decision | Status | Business Owner | Approver | Decision Date | Chosen option | Rationale | Conditions | Evidence | Technical Recommendation | Implementation Impact |
|---|---|---|---|---|---|---|---|---|---|---|---|
| D1 | Package definition | OPEN | Packaging Manager | Complex Manager, Laboratory Manager | — | — | — | — | none on file | Option A: filled container | Blocks the `Package` entity and everything that depends on it |
| D2 | Mixed-product package | OPEN | Packaging Manager | Engineering Manager, Complex Manager | — | — | — | — | none on file | Option A: one `MaterialItem` per package | Blocks `Package` shape |
| D3 | Multi-batch package | OPEN | Process Engineering Lead | Packaging Manager, Laboratory Manager | — | — | — | — | none on file | Design for multiple batches; enforce single if business says no blending | Blocks package→batch linkage |
| D4 | Gross / Tare / Net | OPEN | Finance Manager | Laboratory Manager, Complex Manager | — | — | — | — | none on file | Option A: Gross + Tare stored, Net derived | Blocks quantity columns (quantity-model blocker) |
| D5 | Split / Merge / Repack | OPEN | Laboratory Manager | Packaging Manager, Operations Manager | — | — | — | — | none on file | Not supported in first release | Excluded from implementation scope (technical); question still open |
| D6 | Bulk vs packaged identity | OPEN | Finance Manager | Packaging Manager, Complex Manager | — | — | — | — | none on file | Store output item independently | Blocks inventory posting semantics and reports |
| D7 | Quality release authority for packages | OPEN | Laboratory Manager | Engineering Manager | — | — | — | — | none on file | Reuse `Production.Approve` | Blocks package quality gate wiring |
| D8 | Packaging order / planning tier | OPEN | Operations Manager | Packaging Manager, Complex Manager | — | — | — | — | none on file | No order tier | Non-blocking (default usable) |
| D9 | Rework | OPEN | Packaging Manager | Laboratory Manager, Finance Manager | — | — | — | — | none on file | Not supported | Excluded |
| D10 | Offline / mobile | OPEN | Operations Manager | Complex Manager | — | — | — | — | none on file | Online only | Excluded |
| D11 | Loss / variance tolerance | OPEN | Process Engineering Lead | Finance Manager, Operations Manager | — | — | — | — | none on file | No tolerance until defined | Blocks reliable variance reporting only |
| D12 | Financial / accounting treatment (≡ E-1) | OPEN | Finance Manager | Complex Manager | — | — | — | — | none on file | none — not a technical question | Blocks cost KPIs and any posting with financial meaning |
| E-2 | Commissioning vs production boundary | OPEN | Commissioning Manager | Packaging Manager | — | — | — | — | none on file | No marker until decided | Non-blocking for build |
| E-3 | HSE stop-work authority / legal | OPEN | HSE Manager | Complex Manager | — | — | — | — | none on file | `SITE / LEGAL CONFIRMATION REQUIRED` | Non-blocking for build |
| E-4 | Packaging permission module | OPEN | Complex Manager | Laboratory Manager, Engineering Manager | — | — | — | — | none on file | Reuse `"Production"` | Blocks permission wiring |
| E-5 | Role mapping / new roles | OPEN | Complex Manager | Engineering Manager, Finance Manager | — | — | — | — | none on file | Analysis complete; approval needed before any role or grant change | Blocks role/grant changes |
| GATE | Final approval to proceed to Phase 3 | OPEN | Complex Manager | — | — | — | — | — | none on file | — | Phase 3 cannot start |

## Architecture decisions (NOT business approvals; not read by the gate)

Recorded in `PEMS_OPERATIONS_PACKAGING_CONFIGURABILITY_ARCHITECTURE_DECISIONS.md`. They govern how an approved business decision is *represented*
(configurable, versioned, effective-dated policy); they never choose an option and never change a row
above. The gate command's pattern matches only `D1…D12` / `E-1…E-5` rows, so nothing here can open it.

| ID | Architecture decision | Status | Residual |
|---|---|---|---|
| M-1 | One company = one independent deployment/database; no tenant model in Packaging | CLOSED — ACCEPTED | none for Packaging |
| M-2 | Reusable PEMS Policy Infrastructure (lifecycle/governance) + domain-owned policy schema | CLOSED — ACCEPTED | TI-1 settled (design) |
| M-3 | Typed policy values, stable text codes, central validation, no default fallback | CLOSED — ACCEPTED | TI-2 settled (design) |
| M-4 | Only Implemented+Tested+Authorized+Production-Ready capabilities may be Active | CLOSED — ACCEPTED (principle) | per-mode release-1 scope |
| M-5 | Governed policy approval via `IWorkflowRuntime`; risk-based tiers (Tier 2 empty); `AuditLog` always | CLOSED — ACCEPTED | TI-3 settled (design) |

**Consequence for the business rows above:** each approval is recorded as the initial (v1) policy value
for a named deployment and scope. Approving an option that is not yet an Implemented capability is
valid, but the resulting policy stays BLOCKED until the capability is Implemented, Tested, Authorized and
Production-Ready (ADR M-4).

TI-1…TI-4 (infrastructure shape, value representation, approval-role mapping, scope precedence) are settled as *design* in `PEMS_OPERATIONS_PACKAGING_POLICY_INFRASTRUCTURE_TECHNICAL_DESIGN.md`; still architecture decisions, not business approvals; architecture-owner name pending.

## Named-person fields (to be filled by the business)

| Role | Person | Date |
|---|---|---|
| Complex Manager | | |
| Packaging Manager | | |
| Laboratory Manager | | |
| Finance Manager | | |
| Process Engineering Lead | | |
| Engineering Manager | | |
| Operations Manager | | |
| HSE Manager | | |
| Commissioning Manager | | |

Person names are needed because the governance role names are organizational, not PEMS roles
(workbook §12).

## Change Log

- 2026-09-25 (M-decisions): architecture decisions M-1…M-5 recorded in `PEMS_OPERATIONS_PACKAGING_CONFIGURABILITY_ARCHITECTURE_DECISIONS.md` and summarized above. **No business row changed; no status changed; still 0 of 16 approved.** The gate command output is unchanged.
- 2026-09-25 (TI-1…TI-4): technical design written (`PEMS_OPERATIONS_PACKAGING_POLICY_INFRASTRUCTURE_TECHNICAL_DESIGN.md`). **No business row changed; still 0 of 16.**

- 2026-09-25 (decision workshop): D5 approver changed from "Packaging Manager, Finance Manager" to
  "Packaging Manager, Operations Manager" to match the workshop's acknowledgement owners (Packaging,
  Laboratory/Quality, Operations). Finance Manager is consulted. No status changed; all rows remain
  OPEN. Human-facing form: `PEMS_OPERATIONS_PACKAGING_FINAL_SIGNOFF_SHEET.md` — transcribe signed
  results into the rows above; this file remains the only one the gate reads.
- 2026-09-25 (final business baseline received): `PEMS_OPERATIONS_PACKAGING_FINAL_BUSINESS_BASELINE.md`
  (v1.0, status BUSINESS SIGN-OFF REQUIRED) was added to the repository. All of its decision fields
  are blank and every status is OPEN, so it records **no approval**. Its option letters differ from
  the sign-off sheet in places; see the crosswalk in `…FINAL_SIGNOFF_SHEET.md`. When it is signed,
  reference it in the `Evidence` column here; this file remains the only one the gate command reads.

