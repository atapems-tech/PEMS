# PEMS Phase 3 — Packaging Activation Checklist

**Rule:** Phase 3A may begin only when **every mandatory gate below is checked**. A gate is checked
only on the evidence named beside it. A gate is never checked on a technical recommendation, a
verbal statement, or a filled-in sign-off sheet that has not been mirrored into the gate file.

**Current state (2026-09-25): 0 of 15 mandatory gates satisfied (the 13 requested, the D5 scope acknowledgement, and the configurability gate) → Packaging remains
C — IMPLEMENTATION BLOCKED.**

Gate file (the only place approvals are read): `PEMS_OPERATIONS_PHASE_2G_BUSINESS_SIGNOFF_BASELINE.md`.
Check command — must print a line for each business gate listed below:

```
grep -E "^\| (D[1-9]|D1[0-2]|E-[1-5]) .*\| APPROVED \|" docs/specifications/PEMS_OPERATIONS_PHASE_2G_BUSINESS_SIGNOFF_BASELINE.md
```

## A. Business gates (mandatory)

| ✔ | Gate | Evidence required | Verified by | Status |
|---|---|---|---|---|
| [ ] | D1 approved | `APPROVED` row: option, approver, date, evidence | gate command | OPEN |
| [ ] | D2 approved (incl. packaging-materials answer) | same | gate command | OPEN |
| [ ] | D3 approved | same | gate command | OPEN |
| [ ] | D4 approved (model + tare source) | same | gate command | OPEN |
| [ ] | D5 scope exclusion acknowledged by Packaging, Laboratory/Quality and Operations owners | acknowledgement lines filled and row mirrored | gate command | OPEN |
| [ ] | D6 approved | same as D1 | gate command | OPEN |
| [ ] | Quality authority approved (D7, incl. the Hold/ReleaseHold answer) | same | gate command | OPEN |
| [ ] | E-4 approved (permission module) | same | gate command | OPEN |
| [ ] | E-5 approved (role mapping / roles to create) | same | gate command | OPEN |

## B. Technical gates (mandatory)

| ✔ | Gate | Evidence required | Status |
|---|---|---|---|
| [ ] | Architecture Freeze activated | freeze document status line changed from "PREPARED — NOT ACTIVE" to ACTIVE with date, and §2 "value" column filled from the approved options | Not activated |
| [ ] | Migration strategy validated | additive-only plan reviewed against the current model snapshot and the head migration (`20260923141435_…`); FKs `Restrict`; no destructive operation; `Up()` read before any apply | Not started (nothing approved to migrate) |
| [ ] | Inventory boundary validated | design confirms: only Released packages post; posting idempotent; source bulk deducted if it was posted; no second ledger; failed posting mutates nothing | Design prepared; validation needs D1, D6 |
| [ ] | Barcode strategy validated | `"Package"` added as a target type with existing five types still passing; identity per package; replace/reprint reuse | Design prepared; needs D1 |
| [ ] | Security / SoD validated | permission set mapped to approved roles (E-5); SoD rows defined; quality release independent of packaging execution; no self-release | Not started; needs D7, E-4, E-5 |
| [ ] | Configurability architecture validated | Configurability Guide part of the freeze ✔; M-1..M-5 architecture decisions recorded in `PEMS_OPERATIONS_PACKAGING_CONFIGURABILITY_ARCHITECTURE_DECISIONS.md` ✔; TI-1..TI-4 settled in `PEMS_OPERATIONS_PACKAGING_POLICY_INFRASTRUCTURE_TECHNICAL_DESIGN.md` ✔; that design audited (`docs/audits/PEMS_OPERATIONS_PACKAGING_CONFIGURABILITY_ARCHITECTURE_AUDIT.md` §9) ✔; **architecture-owner name and date on the ADR §4 and design-record §21 sign-off lines** ✘ | **TECHNICAL EVIDENCE COMPLETE SUBJECT TO PR-1..PR-16 (safety review `PEMS_OPERATIONS_PACKAGING_FINAL_ARCHITECTURE_SAFETY_REVIEW.md`) - ARCHITECTURE OWNER APPROVAL REQUIRED.** Not checked. *(Clarification 2026-09-25: the checklist previously named no owner signature for this gate; because the M/TI decisions were proposed by a design exercise and both records carry an unsigned architecture-owner line, that signature is now recorded as required.)* |

## C. Standing preconditions (already true today — re-verify at activation)

| ✔ | Precondition | Verified how | Status today |
|---|---|---|---|
| [x] | Phase 2C quality gate intact | code read of `ProductionRunService.PostOutputToInventoryAsync` + existing tests | ✔ 2026-09-25 |
| [x] | No Packaging table or class exists yet | `sys.tables` query; repo search | ✔ 2026-09-25 |
| [x] | Baseline build and regression known | build 0 errors; suite 1950 passed / 1 pre-existing flake | ✔ |

## D. Later-phase gates (do not block 3A)

| Phase | Extra gate | Status |
|---|---|---|
| 3C (inventory posting) | D12 / E-1 financial treatment approved | OPEN |
| 3C | packaging-materials handling answered in D2 | OPEN |
| 3D | E-4/E-5 executed (roles/grants created by an authorized administrator, not by the program) - **requires the grant-administration capability PR-5, which does not exist today** (safety review SR-12) | OPEN |
| 3D | break-glass extension for `PackagingPolicy` (PR-4), SoD rows (PR-6), integrity check (PR-15) | OPEN |
| 3E | targets for any metric shown with a target | none approved |

## E. On activation

1. Fill the gate rows; run the check command; every business gate must print.
2. Update freeze §2 and status; log it in freeze §14.
3. Begin 3A: additive migration, entities only for what was approved (freeze §3), tests before phase exit.
