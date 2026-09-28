# PEMS Packaging — Configurability Architecture Decisions (M-1 … M-5)

**Document ID:** PEMS-OPS-PKG-CONFIG-ADR · **Version:** 1.0 · **Date:** 2026-09-25
**Kind:** Architecture Decision Record. **Not a business approval.**
**Implementation status:** Packaging remains **C — IMPLEMENTATION BLOCKED** (business approvals 0 of 16;
activation checklist 0 of 15). This document activates nothing.

Parents: `PEMS_OPERATIONS_PACKAGING_CONFIGURABILITY_GUIDE.md` (requirements and B8 open items) ·
`docs/audits/PEMS_OPERATIONS_PACKAGING_CONFIGURABILITY_ARCHITECTURE_AUDIT.md` (findings C-1…C-8).

---

## 0. How to read this document

### 0.1 Three different kinds of decision — never conflated

| Kind | Example | Who decides | Recorded where | Counted by the gate? |
|---|---|---|---|---|
| **Business decision** | "Management selected D1-A Filled Product Unit" | Named business owners and approvers | `PEMS_OPERATIONS_PHASE_2G_BUSINESS_SIGNOFF_BASELINE.md` (register rows D1–E-5) | **Yes** (only this) |
| **Architecture decision** | "PEMS represents D1 as a configurable Packaging policy" | Program owner / architecture owner | **This document** | No |
| **Technical implementation decision** | "The policy value is stored as a text column with a value converter" | Implementation review in Phase 3A | Pre-implementation technical design: `PEMS_OPERATIONS_PACKAGING_POLICY_INFRASTRUCTURE_TECHNICAL_DESIGN.md` (written 2026-09-25) | No |

An architecture decision constrains how business decisions are *represented*. It never chooses a
business option, never approves a decision row, and never opens the gate.

### 0.2 Status of the decisions in this record

**`ACCEPTED`** here means: adopted as the Packaging architecture baseline on the program owner's
instruction of 2026-09-25 (the M-decision baseline task). The named architecture-owner signature
line in §4 is intentionally blank; nobody's approval has been invented. Vocabulary for this record:
`PROPOSED · ACCEPTED · SUPERSEDED`. Each decision states its **residual open items** — the parts it
deliberately does not decide.

### 0.3 Classification of everything M-1…M-5 touch

| Category | Content |
|---|---|
| **A. Business decisions** | D1–D7, E-4, E-5, D12 (and D8–D11, E-2, E-3). All **OPEN**, none touched here. Also: *which* modes the business wants must fit what is implemented (M-4 consequence, §5.4) |
| **B. Architecture decisions** | M-1 … M-5 — **ACCEPTED** by this record |
| **C. Technical recommendations** | Scope precedence (TI-4), value-type form (TI-2), table shapes (TI-1), approval-stage role configuration (TI-3). Non-binding until the pre-implementation design record (§2) |
| **D. Implementation scope** | Only what is Implemented + Tested + Authorized + Production Ready may be activated (M-4). Today nothing in Packaging is implemented |
| **E. Future capability** | Platform-wide multi-tenancy; policy adoption by Maintenance/Procurement; every mode not implemented in release 1 |
| **F. Unresolved** | The business approvals; TI-1…TI-4; whether any permission-only policy category is ever approved (M-5) |

---

## M-1 — Organization / Tenancy Model

### Decision (ACCEPTED, architecture)
For the Packaging program: **one company = one independent PEMS deployment with its own database.**
Packaging introduces **no** tenant, organization or company entity, and does not use `TenantId`.

This is *multi-company support through independent deployments*. It is **not multi-tenancy**
and must not be described as such in any document, UI text or report.

### Evidence
- No `Tenant` / `Organization` / `Company` entity exists (repository and `sys.tables`).
- `BaseEntity.TenantId` is `int?`, documented "prepared for future multi-company, currently unused";
  no query filter, no tenant resolution, no tenant-scoped uniqueness or numbering.
- The only organizational anchors are `FunctionalLocation` (self-referencing tree with free-text
  Plant/Area/Unit) and `ProductionWorkCenter` (required `FunctionalLocationId`).
- No approval of platform-wide multi-tenancy exists in any repository document.

### Rationale
Multi-tenancy is a property of the whole platform (identity, roles, numbering, files, caches, jobs,
reports, AI). A Packaging-only tenant layer would be a parallel architecture that covers a fraction of
the data and gives a false isolation guarantee. Independent deployments already satisfy the Company
Independence Test in principle: same code, different configuration, different data.

### What it means for Company A / Company B
- Each has its own database, users, roles and role mappings (E-5), policies, policy versions,
  audit trail, numbering, barcode identities.
- Both run the **same codebase version line**; differences live only in policy data (Guide A14).
- No cross-company query, report or consolidation. Consolidated reporting would be a separate integration.
- Release management is per deployment: schema/migration parity per deployment is an operational duty.
- Within one deployment, the Packaging policy scope is **deployment-wide → plant/site
  (`FunctionalLocation` subtree) → line (`ProductionWorkCenter`)** (TI-4, settled in `PEMS_OPERATIONS_PACKAGING_POLICY_INFRASTRUCTURE_TECHNICAL_DESIGN.md`: three scope kinds that exist in PEMS, nearest wins, deterministic rules R1-R10).
  "Deployment-wide" means an *approved* policy covering the whole deployment; it is not a developer
  default (see Guide A13).

### Alternatives
| Option | Verdict |
|---|---|
| B. Several companies in one database now | Rejected for Packaging: needs platform work far outside scope; would create a parallel tenancy |
| A′. Separate deployment, but store a company code on policies | Rejected: speculative field; contradicts "no fields without a use" |

### Consequences
Simpler security and data boundary today; no shared-database isolation risk. Costs: deployments must
be kept in step; no shared reporting; a company wanting several legal entities in one database is out
of scope.

### Future path (not started, not approved)
Multi-tenancy would be a **platform-wide initiative** with its own business case, not a Packaging
feature. It would require at least: a Tenant/Organization entity and per-request tenant resolution;
EF global query filters on every `BaseEntity`; tenant-aware unique indexes and numbering sequences;
per-tenant roles/permissions/SoD; file-store partitioning; tenant-keyed caches and background jobs;
AI/OCR data separation; backfill of existing rows to a default tenant; a security review. The Packaging
policy infrastructure (M-2) keeps its *scope kind* a controlled list so a tenant scope could be added
later additively; **nothing is built for it now.**

### Residual open items
None for Packaging. If any company later requires a shared database, that is a new decision for the
platform owner and does not reopen M-1's scope for Packaging.

---

## M-2 — Policy Framework

### Decision (ACCEPTED, architecture)
Build a **reusable PEMS Policy Infrastructure** that provides *governance and lifecycle only*;
**each domain owns its policy schema and its interpretation.** Packaging is the first consumer.

```
PEMS Policy Infrastructure  (domain-neutral: lifecycle + governance)
    ├── Policy identity
    ├── Scope (controlled kinds)
    ├── Version
    ├── Effective dates
    ├── Status (Draft → PendingApproval → Approved → Active → Superseded → Retired; BLOCKED by validation)
    ├── Approval (bound to the existing IWorkflowRuntime — M-5)
    ├── Validation hook (calls the owning domain's validator)
    └── Audit (existing AuditLog)
            │
            ├── Packaging policy   (owned by the Packaging domain: D1–D7, E-4, E-5, D12 values, validators, resolver)
            ├── Future Maintenance policy   (not scheduled)
            ├── Future Procurement policy   (not scheduled)
            └── Future domain policies
```

### Architecture boundary (mandatory)
| The infrastructure MAY | The infrastructure MUST NOT |
|---|---|
| Own identity, scope, version numbering, effective dates, status transitions, approval binding, audit, "resolve the version effective at date X for scope Y" | Contain domain rules, mode meanings, or interpretation of any policy value |
| Call a registered domain validator and record its verdict | Store arbitrary business logic, expressions, scripts or key/value bags |
| Refuse to activate a version its domain validator marks `BLOCKED` | Be a generic "BusinessRule" table; **no such table is created** |
| Expose a typed resolver contract that domains implement | Let a domain read another domain's policy values |

The Packaging domain defines its own policy schema (typed columns for D1–D7, E-4, E-5, D12 values),
its own validators (structural, business, technical — Guide A9), and its own capability-readiness
check (M-4). Domain services receive an already-resolved, version-stamped Packaging policy object.

### Rationale
- Every requirement in the Guide (version, effective date, status, approval, audit, validation,
  reproducibility) is domain-independent; building it once avoids a Packaging-only clone that other
  domains would copy or contradict.
- The existing repository has **no** general policy mechanism (findings C-2): policy-as-data tables
  (`ContractApprovalAuthorityRule`, `SoDPolicy`, `RoutingRule`, `GasParameterLimit`,
  `AIConfidenceSetting`) are unversioned and not effective-dated; `WorkflowVersion` is the only
  versioned, effective-dated precedent and is workflow-specific. Its lifecycle fields
  (`VersionNumber`, `Status`, `EffectiveDate`, `PublishedDate`, `RetiredDate`,
  `ClonedFromVersionId`, `ChangeSummary`, `RowVersion`) are the pattern to follow.
- Domain ownership keeps the module boundaries the codebase already uses (business logic in
  Application/Persistence services, never in the framework).

### Alternatives
| Option | Verdict |
|---|---|
| A. Packaging-only policy tables | Rejected as the baseline: duplicates lifecycle/approval/audit logic that other domains will need; still allowed as a fallback if the infrastructure is judged too large (would need a new ADR) |
| One generic `BusinessRule` table | **Rejected outright** — moves domain logic into data, defeats validation and typing |
| Reuse `WorkflowVersion` tables for policies | Rejected: couples policy to workflow definitions and their stages; different concern |

### Consequences
- Phase 3A gains a first step, **3A-0 Policy infrastructure foundation**, before any package entity
  (freeze §12). It is still blocked by the same gate; it may not start earlier.
- One consumer (Packaging) exists initially. **Risk:** an infrastructure designed around one consumer
  can be over- or under-fitted. Mitigation: keep it minimal (only the responsibilities in the table
  above); no other domain is migrated; no infrastructure feature without a Packaging need.
- New shared tables affect the platform model → migration strategy review (checklist gate) covers them.

### Residual open items
**TI-1** - *settled (design) in `PEMS_OPERATIONS_PACKAGING_POLICY_INFRASTRUCTURE_TECHNICAL_DESIGN.md`:* two infrastructure tables (`PolicyDefinition`, `PolicyVersion`) + domain-owned `PackagingPolicyValues`; namespace boundary with an architecture test. Architecture-owner name pending.

---

## M-3 — Policy Value Representation

### Decision (ACCEPTED, architecture)
Policy values are **strongly typed in the application/domain layer, persisted as controlled,
self-describing, stable codes, and validated centrally.** Arbitrary strings, spelling variants,
undocumented modes and silent developer-default fallback are **prohibited**.

Binding constraints (any implementation must satisfy all):
1. **One registry per decision** lists the permitted modes with a stable code, a status
   (`Implemented` / `Designed` / `Deprecated` / `Retired`) and its business meaning. No mode exists
   outside the registry; no code path names a mode by literal outside it.
2. **Persist the stable code as text, not an ordinal.** A renumbered enum would silently reinterpret
   history; a text code is readable in audit rows and reports years later.
3. **Codes are immutable.** A code is never renamed and never reused for a different meaning. A mode
   is retired by status, never deleted, so any historical policy version stays interpretable.
4. **Central validation**: one validator resolves persisted text → typed value. An unknown, retired-for-new-use
   or empty value makes the policy **invalid (`BLOCKED`)**; it never falls back to a default.
5. **Not-configured is a state**, never an implicit mode (Guide A13).
6. **No blanket enum introduction**: existing entities are not converted. The rule applies to new
   policy structures only.

### Repository evidence (checked 2026-09-25)
- No entity property in the solution is an enum; there is no `HasConversion` anywhere in
  `PEMS.Persistence` (outside migrations). Persisted values are strings validated by static
  whitelists or transition dictionaries (e.g. production quality transitions).
- Eight enums exist, all in DTO/UI/workflow-contract code and none persisted (`PeriodType`,
  `AccessDecisionReason`, `SetupStepStatus`, `ResourceHeaderTone`, `StageItemState`,
  `ImportCatalog.LaunchMode`, `WorkflowValidationSeverity`, `WorkflowActionKind`).
- `PEMS.Domain` has no `Enums` folder although the project charter mentions one.
- Consequence: a persisted typed value is a **new convention** for PEMS. It must be introduced
  narrowly (Packaging policy only) and reviewed once, not spread.

### Rationale
Typed values give compile-time safety and one validation point; text codes give migration safety
(no ordinal drift, additive modes need no schema change) and human-readable history. Rejecting
free strings closes the "spelling variant / undocumented mode" hole that pure whitelists still leave
open when each caller writes its own literal.

### Persistence implications
Text column per decision (bounded length) plus the domain validator; no lookup table needed for
release 1 because the registry is code-owned and versioned with the application. If registry values
must ever be edited by administrators at runtime, that is a different design and needs its own ADR.
Adding a mode = additive code + registry entry; retiring = status change; neither requires a data rewrite.

### Alternatives
| Option | Verdict |
|---|---|
| A. Validated strings + whitelists only (current PEMS habit) | Rejected as insufficient alone: no typing, easy spelling drift across call sites. Whitelist logic is retained *inside* the registry |
| C# enum persisted as integer | Rejected: ordinal drift, unreadable history |
| Lookup table per decision | Deferred: adds runtime-editable modes, which contradicts M-4 (a mode is available only when implemented) |

### Consequences
A small amount of new infrastructure (registry + converter + validator) and one new convention to
document. Historical interpretability and migration safety in return.

### Residual open items
**TI-2** - *settled (design) in `PEMS_OPERATIONS_PACKAGING_POLICY_INFRASTRUCTURE_TECHNICAL_DESIGN.md`:* a registry-backed value type persisted as a stable text code (not a C# enum: an enum defaults to integer persistence in a codebase with no `HasConversion`). Only the struct-vs-class form and the registry's exact home remain for the 3A design freeze.

---

## M-4 — First-Release Capability Scope

### Decision (ACCEPTED, architecture)
**Configuration availability never implies capability availability.** Only capabilities that are
**Implemented, Tested, Authorized and Production-Ready** — and for which a validated, approved policy
exists — may be **Active**. Every other option stays `Designed` / `Future` / `Blocked`, and a policy
that names one is rejected at activation, however valid its value looks.

This decision is **not** "make every documented option configurable in release 1."

### Readiness ladder (per capability)
`Designed → Implemented → Tested → Authorized → Production Ready → Configurable → (with an approved, validated policy) Active`

- *Authorized*: permissions and SoD for the capability exist and are approved (E-4/E-5).
- *Configurable*: the policy infrastructure and the domain registry accept it (post 3A-0).
- *Active* requires: all of the above **and** the corresponding business decision APPROVED **and** the policy version validated.

### Capability matrix — actual state on 2026-09-25 (repository + database evidence)

Legend: ✔ present today · ◐ partial (the note says what) · ✘ absent · — not applicable.

| Cap. | Designed | Implemented | Tested | Authorized | Production Ready | Configurable | Business approval | Evidence / note |
|---|---|---|---|---|---|---|---|---|
| **D1** Package definition | ✔ (options in baseline; entity set in freeze §3) | ✘ | ✘ | ✘ | ✘ | ✘ | OPEN | No Packaging class or table exists |
| **D2** Mixed product | ✔ | ✘ | ✘ | ✘ | ✘ | ✘ | OPEN | Needs content/component structure; no BOM entity exists |
| **D3** Multi-batch | ✔ | ✘ | ✘ | ✘ | ✘ | ✘ | OPEN | Batch genealogy exists only at `ProductionBatch` level |
| **D4** Gross/Tare/Net | ✔ | ✘ | ✘ | ✘ | ✘ | ✘ | OPEN | Quantity scale `decimal(18,3)` exists; no weighing-event structure |
| **D5** Split/Merge/Repack | ✔ (as *Disabled/Future*) | ✘ | ✘ | ✘ | ✘ | ✘ | OPEN (acknowledgement) | Out of initial scope; the only valid policy value in release 1 is Disabled |
| **D6** Product identity | ✔ | ✘ | ✘ | ✘ | ✘ | ✘ | OPEN | `MaterialItem` has no parent/child relation; `MaterialStock` has no packaged/bulk dimension |
| **D7** Quality release authority | ✔ | ◐ batch-level only | ◐ batch-level only | ✘ for packages | ✘ for packages | ✘ | OPEN | The Phase 2C gate (`ProductionRunService.PostOutputToInventoryAsync` refuses non-`Released`) is implemented and tested **for `ProductionBatch`**; nothing exists for packages. `QAManager` lacks Hold/ReleaseHold (business question in D7) |
| **E-4** Permission module | ✔ | ◐ seam only (`HasPermissionAsync`, 0 role-name literals in Production) | ◐ | ✘ | ✘ | ✘ | OPEN | No Packaging module registered; no grants |
| **E-5** Role mapping | ✔ (analysis of 20 live roles) | ◐ role/permission data infrastructure exists | — | ✘ | ✘ | ✘ | OPEN | Mapping not approved; 5 roles likely missing; nothing created |
| **D12** Financial treatment | ◐ boundary only | ✘ | ✘ | ✘ | ✘ | ✘ | OPEN | Treatment itself undefined — a business input; no accounting rule invented |
| *Policy infrastructure (M-2)* | ✔ (this record) | ✘ | ✘ | ✘ | ✘ | — | — | Step 3A-0 |

Result: **no Packaging capability is Active or activatable today.** Zero rows are Production Ready.

### Mode-level readiness (release-1 classification)
| Decision | Modes | Class today |
|---|---|---|
| D1 | FilledProductUnit · GenericPackagingUnit · LogisticsHandlingUnit · Layered | all **Designed** |
| D2 | SingleProduct · MixedProduct · PrimaryProductWithControlledComponents | all **Designed** |
| D3 | SingleBatchOnly · MultipleBatches · PrimaryBatchWithSecondaryGenealogy | all **Designed** |
| D4 | GrossPlusTare · GrossPlusNet · GrossTareNet · EventBasedWeighing | all **Designed** |
| D5 | Split / Merge / Repack | **Future** (must be Disabled) |
| D6 | Same · Separate · ParentChild · Transformation | all **Designed**; ParentChild and Transformation additionally need shared-table change / D12 |
| D7 | IndependentQuality · QAManager · DedicatedPackagingQuality · Hybrid | all **Designed** |
| E-4 | ReuseProduction · Dedicated · Hybrid | all **Designed** |
| D12 | NoFinancialTransformation … FullCostingIntegration | **Blocked** (business input) |

Which of these become **Implemented** in release 1 is a scoping choice made together with the
business approvals (see consequence below); this record does not pre-select any mode, and no mode is
promoted to Implemented by being listed here.

### Consequences
- A business approval of an option that is not Implemented does **not** make that option operational.
  It is recorded as approved, the corresponding policy validates as `BLOCKED`, and the business must
  either accept phased delivery or choose an implemented option. **This is a real scheduling consequence
  the approvers must be told before signing** (added to the sign-off sheet note).
- The Company Independence Test result stands as reported in the audit (§4): principle supported;
  Company B's set is not available until its capabilities are built.
- The matrix is a living table: a row changes only on repository evidence (code, tests, grants), never on intent.

### Residual open items
Per-mode "Implemented in release 1" choices (business/architecture scoping with the D-approvals).

---

## M-5 — Policy Approval

### Decision (ACCEPTED, architecture)
Governed changes to Packaging policy use the **existing PEMS workflow infrastructure
(`IWorkflowRuntime`)**. **No new approval engine is created.** A risk-based rule applies, and every
change — governed or not — is recorded by the existing `AuditLog`.

### Risk-based rule
| Tier | Change | Route |
|---|---|---|
| **1 — Governed approval** | Any change to the business-policy values (D1–D7, E-4, E-5, D12 modes), scope, effective dates, activation, supersession, retirement of a policy version | `IWorkflowRuntime` instance on the policy version (binding key `Module="Policy"`, `EntityType="PolicyVersion"`, `Context=<policy type>` - settled in the technical design); Draft → PendingApproval → Approved → Active; author ≠ approver (SoD) |
| **2 — Permission-controlled only** | Low-risk operational configuration | Permission check via the existing `HasPermissionAsync`; **permitted only for categories the architecture has explicitly approved by name.** **No category is approved by this record — Tier 2 is empty.** Adding one requires an amendment to this ADR |
| **All tiers** | Every change | `AuditLog` (existing interceptor); no second audit table |

The current lone precedent for permission-only governance, `ContractApprovalAuthorityRule`
(SystemAdmin-only, unversioned, no effective dating), is a precedent for *rule data*, **not** an
approved PEMS governance model for versioned business policy, so it does not justify Tier 2.

### Repository evidence
- `IWorkflowRuntime` is the documented single entry point ("No module is allowed to implement its own
  approval engine"); ~20 files across services and controllers use it (Contract, IRN, Permit, Isolation,
  VDR, MOC, Work Order review, Work Request, …). Production execution states use explicit transition
  dictionaries by design and are unaffected.
- `StartInstanceAsync(module, businessObjectType, businessObjectId, workflowCategory, projectId,
  initiatedByEmployeeId, …)` scopes by project, **not** by site/line: policy *scope* therefore lives in
  the policy infrastructure, not in the workflow.
- Workflow definitions are data (`WorkflowDefinition` → `WorkflowVersion` → stages/transitions) created
  by seeders and administrable through the Workflow Management area.
- **Caution:** existing seeders write role names into stage definitions
  (`SystemRoles.SystemAdmin, SystemRoles.QAManager` in `IrnDispositionWorkflowSeeder`). Copying that
  pattern for Packaging policy approval would hard-code organization-specific role names. Approval
  stages for policy must be configured from the approved E-5 mapping (data), not fixed in a seeder.

### Alternatives
| Option | Verdict |
|---|---|
| B. Permission-only for everything | Rejected: no approver/author separation, no pending state, weak governance for business policy |
| Packaging-specific approval engine | **Forbidden** |
| Workflow for every change incl. trivial ones | Avoided by the risk-based rule; Tier 2 exists as a mechanism but is empty |

### Consequences
- A policy-approval workflow definition must exist before any policy can reach Approved; it is part of 3A-0.
- Policy approver identity depends on E-5 (business): until roles are mapped, no approval chain can be defined.
- SoD: policy author ≠ approver must be enforced, adding to the SoD review in checklist gate "Security/SoD".
- The workflow platform itself is not modified (freeze rule).

### Residual open items
**TI-3** - *settled (design) in `PEMS_OPERATIONS_PACKAGING_POLICY_INFRASTRUCTURE_TECHNICAL_DESIGN.md`:* workflow approval stages name a **permission** (`Permission:PackagingPolicy.Approve`), never a role; the role mapping is grant data applied after E-5 approval *[amended by the Safety Review: no runtime grant/revoke mechanism exists today - PR-5 is a prerequisite]*; safeguards S-1..S-5 (notably: the runtime auto-approves a stage with no participant, so a recorded human `Approve` by a non-author is required as evidence). Business: who the policy approvers are (E-5) is unchanged and still open.

---

## 1. Summary of Status

| ID | Architecture decision | Status | Residual |
|---|---|---|---|
| M-1 | Separate deployment/database per company; no tenant model in Packaging | **CLOSED — ACCEPTED** | none for Packaging; multi-tenancy = future platform initiative |
| M-2 | Reusable PEMS Policy Infrastructure (lifecycle/governance) + domain-owned policy schema | **CLOSED — ACCEPTED** | TI-1 settled (design) in the technical design |
| M-3 | Typed values, stable text codes, central validation, no fallback | **CLOSED — ACCEPTED** | TI-2 settled (design); struct-vs-class form left to the 3A freeze |
| M-4 | Capability matrix; only Implemented+Tested+Authorized+Production-Ready may be Active | **CLOSED — ACCEPTED** (principle) | per-mode release-1 scope chosen with business approvals; matrix stays live |
| M-5 | `IWorkflowRuntime` for governed policy changes; risk-based tiers; `AuditLog` for all | **CLOSED — ACCEPTED** | TI-3 settled (design); Tier 2 empty; approvers depend on E-5 (business) |

**Technical items TI-1..TI-4 - status update 2026-09-25:** all four are **settled (design), ACCEPTED on the program owner's instruction, architecture-owner name pending**, in `PEMS_OPERATIONS_PACKAGING_POLICY_INFRASTRUCTURE_TECHNICAL_DESIGN.md`. They remain *architecture decisions and implementation proposals*, not business approvals; physical names/shapes stay logical until the Phase 3A design freeze.

## 2. Pre-implementation design record and Phase 3A consequence

**Design record (documentation only) - WRITTEN 2026-09-25:** `PEMS_OPERATIONS_PACKAGING_POLICY_INFRASTRUCTURE_TECHNICAL_DESIGN.md`, audited in the configurability audit §9. It is the technical evidence for activation gate 15. **Gate 15 status: TECHNICAL EVIDENCE COMPLETE - ARCHITECTURE OWNER APPROVAL REQUIRED** (this record's §4 sign-off line and the design record's §21 line are blank). It is **not** implementation and does not activate anything.

Nothing here activates Phase 3A. The freeze work plan gains **3A-0 Policy infrastructure foundation**
(identity/scope/version/effective dates/status/approval binding/validation hook/audit, plus the
Packaging registry and resolver contract). 3A, including 3A-0, starts only when **all 15 checklist gates**
are satisfied. Today: 0 of 15; business approvals 0 of 16; Packaging **C — IMPLEMENTATION BLOCKED**.

## 3. Inviolable rules carried over (unchanged by this record)
The Phase 2C production quality gate; `InventoryTransaction` / `MaterialStock` / `IMaterialStockService`
and `BarcodeIdentity` reused, not duplicated; permission-based (not role-name) authorization; no change
to `QAManager` Hold/ReleaseHold; `PEMS.Web/filestore/` untouched; no invented KPI targets or accounting
rules; no Packaging entity, migration, controller, permission, role or workflow before the gate opens.

## 4. Sign-off (architecture record)

| Role | Name | Date | Signature/Reference |
|---|---|---|---|
| Architecture owner | | | |
| Program owner | | | *(instruction of 2026-09-25 recorded; no name on file)* |

Blank by design; this record is `ACCEPTED` on the program owner's instruction and awaits
named confirmation. The signature does not affect the business gate.
