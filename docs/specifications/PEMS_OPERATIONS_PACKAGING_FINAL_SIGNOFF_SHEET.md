# Packaging Business Sign-Off

Reference material for every decision: `PEMS_OPERATIONS_PACKAGING_FINAL_BUSINESS_DECISION_PACK.md`.
All fields below are intentionally blank. Nothing has been approved.

**After signing:** copy the result into the matching row of
`PEMS_OPERATIONS_PHASE_2G_BUSINESS_SIGNOFF_BASELINE.md` (Status = APPROVED, Decision Date, Chosen
option, Evidence). That file is the only one the program's gate reads. A filled sheet that is not
mirrored there does not open the gate.

**Read before signing (configurability).** Each decision below becomes the value of the **initial
Packaging Policy (v1)** for the deployment and scope you name here — it is not a permanent product
rule (`PEMS_OPERATIONS_PACKAGING_CONFIGURABILITY_GUIDE.md`; architecture decisions:
`PEMS_OPERATIONS_PACKAGING_CONFIGURABILITY_ARCHITECTURE_DECISIONS.md`).
Scope this sheet applies to (company deployment / plant / line): ______________________

**Delivery consequence.** Choosing an option does not make it operational. Only options whose
capability is Implemented, Tested, Authorized and Production-Ready can be activated; today none is.
A signed option that is not yet built is recorded, and its policy waits (BLOCKED) until it is built.
The M-1…M-5 architecture decisions are **not** signed on this sheet and are not business approvals.

## D1
Decision: Package definition (options A saleable unit / B package unit of one product / C logistics unit / D layered)
Selected Option:
Business Owner: Packaging Manager
Approver: Complex Manager, Laboratory Manager
Decision Date:
Rationale:
Conditions:
Evidence:
Signature/Approval Reference:

## D2
Decision: Mixed-product package (options A one item / B several items / C one product + packaging components / D other); also state whether packaging materials are recorded per package
Selected Option:
Business Owner: Packaging Manager
Approver: Engineering Manager, Complex Manager
Decision Date:
Rationale:
Conditions:
Evidence:
Signature/Approval Reference:

## D3
Decision: Multi-batch package (options A one batch / B several contributions / C primary + secondary genealogy)
Selected Option:
Business Owner: Process Engineering Lead
Approver: Packaging Manager, Laboratory Manager
Decision Date:
Rationale:
Conditions:
Evidence:
Signature/Approval Reference:

## D4
Decision: Gross / Tare / Net model (options A gross+tare measured / B gross+net measured / C all three stored with validation / D weighing events); also state tare source
Selected Option:
Business Owner: Finance Manager
Approver: Laboratory Manager, Complex Manager
Decision Date:
Rationale:
Conditions:
Evidence:
Signature/Approval Reference:

## D5
Decision: Acknowledge "Split / Merge / Repack is OUT OF INITIAL PHASE 3 SCOPE" (acknowledgement only, not approval to build)
Selected Option:
Business Owner: Laboratory Manager
Approver: Packaging Manager, Operations Manager
Decision Date:
Rationale:
Conditions:
Evidence:
Signature/Approval Reference:

Acknowledgements — Packaging Owner: ______ · Laboratory/Quality Owner: ______ · Operations Owner: ______

## D6
Decision: Bulk vs packaged product identity (options A same item / B separate items / C parent-child / D separate items + mapping table)
Selected Option:
Business Owner: Finance Manager
Approver: Packaging Manager, Complex Manager
Decision Date:
Rationale:
Conditions:
Evidence:
Signature/Approval Reference:

## D7
Decision: Quality release authority for packages (options A Laboratory/QA / B QAManager stands in / C separate Packaging Quality role / D other); also state whether the release authority needs Hold/ReleaseHold
Selected Option:
Business Owner: Laboratory Manager
Approver: Engineering Manager
Decision Date:
Rationale:
Conditions:
Evidence:
Signature/Approval Reference:

## E-4
Decision: Packaging permission module (options A reuse Production / B dedicated Packaging module / C hybrid)
Selected Option:
Business Owner: Complex Manager
Approver: Laboratory Manager, Engineering Manager
Decision Date:
Rationale:
Conditions:
Evidence:
Signature/Approval Reference:

## E-5
Decision: Role mapping — which PEMS roles represent the 14 organizational roles, and which new roles (if any) are created
Selected Option:
Business Owner: Complex Manager
Approver: Engineering Manager, Finance Manager
Decision Date:
Rationale:
Conditions:
Evidence:
Signature/Approval Reference:

---

## Option-letter crosswalk (read before signing)

`PEMS_OPERATIONS_PACKAGING_FINAL_BUSINESS_BASELINE.md` (PEMS-OPS-PKG-FINAL-BUSINESS-BASELINE v1.0) is
the authoritative business baseline. Its option letters (D1-A, D6-D …) are **not always the same** as
the letters used in the decision pack and above. **When the two differ, the baseline's wording
governs.** Write the option by its baseline ID and name, never by letter alone.

| Decision | Baseline option | Nearest decision-pack option | Note |
|---|---|---|---|
| D1 | D1-A Filled Product Unit | B — package unit of one product | same meaning |
| D1 | D1-B Generic Packaging Unit | none (closest to "container only") | new in the baseline |
| D1 | D1-C Logistics / Handling Unit | C | same |
| D1 | D1-D Layered (product package + handling + logistics) | D | baseline adds a logistics layer |
| D2 | D2-A / D2-B / D2-C | A / B / C | same |
| D3 | D3-A / D3-B / D3-C | A / B / C | same |
| D4 | D4-A / D4-B / D4-C / D4-D | A / B / C / D | same |
| D6 | D6-A Same MaterialItem / D6-B Separate / D6-C Parent-child | A / B / C | same |
| D6 | D6-D Transformation / state model | none (pack D was "separate items + mapping table") | different option |
| D7 | D7-A / D7-B / D7-C | A / B / C | same |
| D7 | D7-D Hybrid (QAManager + dedicated packaging permissions) | pack D was "other" | narrowed in the baseline |
| E-4 | E4-A / E4-B / E4-C | A / B / C | same |

Technical recommendations in the baseline differ from the decision pack in two places, both still
non-binding: **D3 → D3-A** (the pack said B if blending happens, else A) and **D6 → no
recommendation** (the pack suggested a schema-neutral design). The baseline's statements are the
current technical position.

