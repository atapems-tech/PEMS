# PEMS Packaging — Configurability Architecture Audit

Date: 2026-09-25 · Scope: **design-level audit** of the Configurability Guide against the current
repository and live database. **Nothing is implemented**, so implementation-level checks (tests,
migrations, runtime behavior) cannot be performed and are marked "not applicable yet". No code,
schema, migration, role, permission, workflow or data change was made.

**Result: DESIGN ACCEPTABLE FOR OWNER/ARCHITECT REVIEW; IMPLEMENTATION STILL BLOCKED (0 of 16
business approvals; activation checklist 0 of 15).** At the time of this audit five architecture items
(M-1…M-5) were open; they have since been closed as architecture decisions (see the update below and §8).

Guide: `docs/specifications/PEMS_OPERATIONS_PACKAGING_CONFIGURABILITY_GUIDE.md`.
**Update 2026-09-25:** M-1…M-5 have since been closed as architecture decisions; see §8 (addendum) and
`docs/specifications/PEMS_OPERATIONS_PACKAGING_CONFIGURABILITY_ARCHITECTURE_DECISIONS.md`. Where this audit says "open", §8 governs.

## 1. Method
Read the guide requirements; searched the repository for tenant/organization/site concepts, enums,
feature flags, versioned/effective-dated configuration, policy-as-data tables, role-name literals in
Production code, and the permission seam; read live `RolePermissions`/role data (previous phase);
re-ran the gate command.

## 2. Checklist (guide §32)

| # | Item | Result | Evidence / note |
|---|---|---|---|
| 1 | No company-specific hard-coding | **PASS (design)** | Guide A1 forbids branching on company/site/type; no Packaging code exists to violate it |
| 2 | No role-name hard-coding | **PASS (design, existing code verified)** | Production code has **0** role-name literals; authorization is `HasPermissionAsync(roles, module, action)`. Guide A6 (D7/E-5) keeps this |
| 3 | No duplicated authorization framework | **PASS (design)** | E-4/E-5 use existing `RolePermissions` data and the existing seam; no second engine |
| 4 | No duplicated workflow framework | **PASS (design)** | Production keeps explicit transition dictionaries; policy approval may use `IWorkflowRuntime` (M-5); nothing new |
| 5 | No duplicated organization hierarchy | **PASS with limit** | Scope reuses `FunctionalLocation` / `ProductionWorkCenter`. **No organization/tenant entity exists** and none is invented (B2, M-1) |
| 6 | Policy versioning | **DESIGNED, NOT IMPLEMENTED** | Pattern mirrors `WorkflowVersion` (VersionNumber, Status, EffectiveDate, ClonedFromVersionId) |
| 7 | Effective dating | **DESIGNED, NOT IMPLEMENTED** | Resolution by scope + effective date; overlap rejected at activation |
| 8 | Runtime resolution | **DESIGNED, NOT IMPLEMENTED** | Single resolver; version in cache identity; no ad hoc reads |
| 9 | Historical reproducibility | **DESIGNED, NOT IMPLEMENTED** | Additive `PackagingPolicyVersionId` on dependent records; history never reinterpreted |
| 10 | Validation (structural / business / technical) | **DESIGNED, NOT IMPLEMENTED** | Activation → BLOCKED if a dependency is missing |
| 11 | Auditability | **PASS (design)** | Existing `AuditLog` interceptor covers every `BaseEntity`; no need for a second audit table |
| 12 | Security | **PASS (design)** | Policy activation permission-gated; active version immutable; SoD between policy author and approver required in 3D |
| 13 | Inventory safety | **PASS (design)** | `InventoryTransaction` / `MaterialStock` / `IMaterialStockService` untouched; invariants non-configurable (guide A2) |
| 14 | Quality-gate preservation | **PASS** | Phase 2C gate in `ProductionRunService.PostOutputToInventoryAsync` unchanged (no file modified) |
| 15 | Migration safety | **PASS (design)** | Additive-only; no fabricated default policy; a placeholder can never activate behavior (A13) |

## 3. Findings

| ID | Finding | Severity | Status |
|---|---|---|---|
| C-1 | **No tenant/organization abstraction.** `BaseEntity.TenantId` is nullable and unused; no query filter, no resolver. The guide's Organization level is unavailable. Company A/B on one database would need platform-wide tenancy | HIGH (scope) | Documented; owner decision **M-1**. Separate deployments satisfy the guide today |
| C-2 | No general policy framework exists. Precedents (`ContractApprovalAuthorityRule`, `SoDPolicy`, `RoutingRules`, `GasParameterLimit`, `AIConfidenceSettings`) are unversioned, not effective-dated. Only `WorkflowVersion` is versioned — workflow-specific | MEDIUM | Decision **M-2**: Packaging-specific store mirroring `WorkflowVersion` vs a general engine. Not built |
| C-3 | PEMS has **no enums on entities**; values are strings + static whitelists. The guide's "enumerated values" needs a convention choice | LOW | Decision **M-3**; recommendation: validated strings + whitelists |
| C-4 | **"Configurable" ≠ "implemented".** Storing every mode is cheap; implementing them is not (layered containers, mixed products, multi-batch, ParentChild identity). D6 ParentChild needs a new relation on `MaterialItem`, a table shared by procurement, maintenance and production | HIGH (expectation) | Mode-support matrix in guide B5 and decision **M-4**; unsupported modes are rejected at activation |
| C-5 | **Company Independence Test does not pass by configuration alone in release 1**, by design: Company B's set needs capabilities that will be marked "not implemented" | MEDIUM (expectation) | Stated honestly in guide B6; not hidden |
| C-6 | Policy approval mechanism undecided: `IWorkflowRuntime` (used by 12 services) vs permission-only | LOW | Decision **M-5** |
| C-7 | The sign-off baseline documents were written before the guide and speak of "the" chosen option. Under the guide each approval is v1 of a policy for one scope | LOW | Fixed by the statement added to the Final Business Baseline and guide B7 |
| C-8 | Existing `HasPermissionAsync` role-mapping data can be changed at runtime by an administrator, so E-5 mapping evidence has a shelf life | INFORMATIONAL | Noted; activation re-verifies |

No CRITICAL findings.

## 4. Company Independence Test (design result)

| | Company A | Company B |
|---|---|---|
| Set | FilledProductUnit · SingleProduct · SingleBatchOnly · GrossPlusTare · SeparateMaterialIdentity · IndependentQuality | Layered · PrimaryProductWithControlledComponents · MultipleBatches · GrossTareNet · ParentChildIdentity · Hybrid |
| Same codebase, configuration only | **Supported when 3A–3C are built** | **Supported only after the layered, component, multi-batch and ParentChild capabilities are built and tested.** Until then the policy is rejected as `BLOCKED` |
| Needs source change to differ | No | No (once capabilities exist) |

The test verifies the *principle* (differences live in policy data, not code) rather than promising
all modes on day one.

## 5. Not Verified (cannot be, yet)
Runtime resolver behavior, cache invalidation, effective-date edge cases, migration behavior,
performance, UI explainability, reports that separate policy versions, and test coverage. These are
Phase 3 acceptance criteria (guide A16), not present facts.

## 6. Implementation-Surface Validation
Recorded by the final validation step of this phase (git-based): no `.cs`, `.cshtml`, `.csproj`,
`.resx`, `.css`, `.js` file and no migration changed after the Phase 2F baseline; no database write;
gate command still returns 0 approvals.

## 7. What Is Required Next
1. Owners answer M-1…M-5 (guide B8) alongside the D1–D7, E-4, E-5 approvals.
2. Owners decide whether sign-offs are recorded as v1 of the initial policy for a named scope.
3. Activation checklist reaches 15 of 15, then Phase 3A begins with the policy foundation before any
   package entity.

## 8. Addendum 2026-09-25 — status of findings after the M-decision baseline

Architecture decisions are recorded in `docs/specifications/PEMS_OPERATIONS_PACKAGING_CONFIGURABILITY_ARCHITECTURE_DECISIONS.md`. The findings above are
otherwise unchanged ("residual TI-x" entries in the table below are settled by §9); the audit's own "open" wording in §7 item 1 is superseded by this table.

| Finding / item | Was | Now |
|---|---|---|
| C-1 / M-1 tenancy | open (owner decision) | **CLOSED — ACCEPTED:** separate deployment per company; no tenant model in Packaging |
| C-2 / M-2 policy store | open | **CLOSED — ACCEPTED:** reusable policy infrastructure, domain-owned schema (residual TI-1) |
| C-3 / M-3 value convention | open | **CLOSED — ACCEPTED:** typed values + stable text codes (residual TI-2). Correction of detail: eight non-persisted enums exist in DTO/UI/workflow code; no entity enum and no `HasConversion` |
| C-4, C-5 / M-4 scope | open | **CLOSED — ACCEPTED (principle):** capability matrix in the ADR; expectation limits unchanged |
| C-6 / M-5 approval | open | **CLOSED — ACCEPTED:** `IWorkflowRuntime`, risk-based tiers, Tier 2 empty (residual TI-3) |
| New finding C-9 | — | Existing workflow seeders embed role names (`SystemRoles.QAManager`) in stage definitions; Packaging policy approval must take roles from the E-5 mapping, not a seeder constant (MEDIUM, documented; TI-3) |
| New finding C-10 | — | The M-2 decision creates a shared platform component with one initial consumer; over/under-fitting risk mitigated by a minimal responsibility list (LOW, documented) |

Business approvals: **0 of 16** (unchanged). Activation checklist: **0 of 15** (gate 15 not satisfied: the
pre-implementation design record settling TI-1…TI-4 is outstanding — *superseded by §9: the record is now written*). Classification unchanged:
**C — IMPLEMENTATION BLOCKED.**

## 9. Design audit 2026-09-25 — `PEMS_OPERATIONS_PACKAGING_POLICY_INFRASTRUCTURE_TECHNICAL_DESIGN.md` (TI-1…TI-4)

Scope: design-level audit of the technical design against the §2 checklist and the repository. Nothing is implemented,
so runtime/test items remain "designed, not implemented". No code, schema, role, permission, workflow, seed or data
was changed; database access was read-only `SELECT`.

### 9.1 Checklist re-run against the design

| # | Item | Result | Evidence |
|---|---|---|---|
| 1 | No company-specific hard-coding | **PASS (design)** | No branch on company/site/type in any contract; differences are policy data |
| 2 | No role-name hard-coding | **PASS (design)** | TI-3: approval stage names `Permission:PackagingPolicy.Approve`; roles are grant data. Existing pattern in production use (40 live stages) |
| 3 | No duplicated authorization framework | **PASS** | Reuses `HasPermissionAsync`/`IAccessControlService`; option D (a fourth mapping table) rejected |
| 4 | No duplicated workflow framework | **PASS** | `IWorkflowRuntime` + `WorkflowBinding`; no second approval engine |
| 5 | No duplicated organization hierarchy | **PASS** | Scope kinds reference `FunctionalLocation`/`ProductionWorkCenter` by FK; no Packaging hierarchy; organization/plant/line rejected as non-existent |
| 6 | Versioning | **DESIGNED** | `PolicyVersion` (pattern `WorkflowVersion`); unique (definition, version number) |
| 7 | Effective dating | **DESIGNED** | window + R1–R10; no backdating |
| 8 | Runtime resolution | **DESIGNED** | deterministic pure resolver; ambiguity is an error (pattern `WorkflowBindingService`) |
| 9 | Historical reproducibility | **DESIGNED** | version id on the root aggregate; re-resolution only at creation/preview |
| 10 | Validation | **DESIGNED** | structural / business / technical, domain-owned rules, recorded summary |
| 11 | Auditability | **PASS (design)** | `AuditLog` covers all new tables automatically; reason columns added because the log has no reason field |
| 12 | Security | **PASS with conditions** | S-1…S-5; break-glass extension is a 3D code item |
| 13 | Inventory safety | **PASS** | no inventory/`MaterialStock`/`IMaterialStockService` touch; invariants independent of the bound version |
| 14 | Quality-gate preservation | **PASS** | no file modified; Phase 2C gate untouched |
| 15 | Migration safety | **PASS (design)** | additive, three tables, no seed of policy rows, no default policy |

### 9.2 New findings

| ID | Finding | Severity | Handling |
|---|---|---|---|
| F-T1 | `WorkflowRuntimeService.TryAutoSkipEmptyStageAsync` treats a stage with an empty participant list as an implicit **Approve**, and the validator reports `STAGE_NO_PARTICIPANT` only as a **warning**. A policy-approval stage that ends up with no participants would approve without a human | **HIGH (design hazard)** | Design guards S-1 (routing check before Submit, error) and S-2 (approval evidence must be a recorded human `Approve` by a user ≠ submitter; `AutoSkip` rejected) |
| F-T2 | Three parallel permission mechanisms (`RolePermission` flags, `RoleModuleAction`, `Permission`+`RolePermissionGrant`); the first two reference roles by **name string** | MEDIUM | Recommend `RoleModuleAction` for the `PackagingPolicy` action set (OQ-9); S-5 re-checks role existence at activation and at every Submit |
| F-T3 | The organizational data is thin: `FunctionalLocation` 32 rows / 28 roots / depth ≤ 2 / `Plant`,`Unit` empty; `ProductionWorkCenter` 0 rows; Department flat (264, no parents) | MEDIUM (expectation) | Scope kinds limited to what exists; deployment-wide level expected to carry most policies; OQ-1, OQ-10 |
| F-T4 | Clock convention: PEMS uses local `DateTime.Now`; effective-date semantics (date vs instant, DST) undefined | LOW | OQ-2; R10 (no backdating) protects past resolution regardless |
| F-T5 | The audit log has no reason field and is already a high-volume table (~205k rows when its index was designed) | LOW | `ChangeSummary` / `RetirementReason` on the version; volume is not a blocker |
| F-T6 | PEMS has no database triggers or state-machine framework, so "active versions are immutable" is enforced in the service layer and detected by audit, **not** by the database | LOW (limitation) | Stated in the design; unit tests per transition and forbidden edit |
| F-T7 | Live database: 10 published workflow stages have an empty `ResponsibleRole` (e.g. MOC `RiskAssessment`, `SeniorApproval`; TechnicalQuery stages); many are dynamic-participant by design | INFORMATIONAL — **outside this program** | Observed only, not analyzed or changed; recommend a separate review by the workflow owners because of F-T1 |

No CRITICAL findings. No business decision was touched.

### 9.3 Result
**Design acceptable for architecture-owner review.** Gate 15: **TECHNICAL EVIDENCE COMPLETE — ARCHITECTURE OWNER APPROVAL REQUIRED** (not checked).
Business approvals **0 / 16**; activation gates **0 / 15**; classification **C — IMPLEMENTATION BLOCKED**.

## 10. Safety review 2026-09-25 — supersedes the PASS wording in §2 and §9 where it conflicts

Full record: `PEMS_OPERATIONS_PACKAGING_FINAL_ARCHITECTURE_SAFETY_REVIEW.md`. The earlier design-level results in §2/§9 were based partly on documentation; the safety review re-tested them against source and live data.

| Earlier result | Safety-review result |
|---|---|
| §9 item 2 "no role-name hard-coding — PASS (design)" | Holds for Packaging domain code; **but** legacy permission rows and workflow stage participants bind to role *names*, and there is no runtime way to administer grants for new actions (SR-12, SR-13) |
| §9 item 12 "Security — PASS with conditions" | **Downgraded to CONDITIONAL — 7 HIGH findings** (SR-04, 05, 06, 07, 09, 12, 26), each with a required change |
| F-T1 auto-skip hazard | Refined: two "nobody" cases; employee-only stage also auto-skipped; enforceable at the Packaging boundary with no platform change (SR-01..03) |
| F-T6 "no database-level immutability" | **Corrected:** `WorkflowVersionImmutabilityGuard` exists at `SaveChanges`; adopt it (SR-19) |
| §9 item 9/10 reproducibility, validation | Reference boundary fixed (`PackagingRun` and `Package`); values evolution rules; D12 coupling defect (SR-15, SR-18) |

New findings SR-05 (null-employee bypass; live SystemAdmin `admin` has no Employee), SR-07 (`HasPermissionAsync` has no SoD/scope/audit), SR-09/10 (SystemAdmin bypass; break-glass hard-coded at two sites), SR-12 (no runtime grant/revoke; startup-only additive sync), SR-16 (business approval invisible to runtime), SR-21 (overlap race). **Required changes PR-1..PR-16.**

Status: business approvals 0 / 16 · activation gates 0 / 15 · Gate 15 **technical evidence complete subject to PR-1..PR-16 — architecture owner approval required** · classification **C — IMPLEMENTATION BLOCKED**.

