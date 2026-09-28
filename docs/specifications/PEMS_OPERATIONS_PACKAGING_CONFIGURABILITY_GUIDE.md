# PEMS Packaging — Configurability & Multi-Organization Policy Guide

**Extends:** `PEMS_OPERATIONS_PACKAGING_FINAL_BUSINESS_BASELINE.md`
**Part of:** the Packaging Architecture Freeze (`PEMS_OPERATIONS_PHASE_3_PACKAGING_ARCHITECTURE_FREEZE.md` §15)
**Architecture decisions:** `PEMS_OPERATIONS_PACKAGING_CONFIGURABILITY_ARCHITECTURE_DECISIONS.md` (M-1..M-5 closed).
**Safety review:** `docs/audits/PEMS_OPERATIONS_PACKAGING_FINAL_ARCHITECTURE_SAFETY_REVIEW.md` (16 required changes PR-1..PR-16; amends the technical design).
**Technical design:** `PEMS_OPERATIONS_PACKAGING_POLICY_INFRASTRUCTURE_TECHNICAL_DESIGN.md` (TI-1..TI-4 settled; logical data model, resolver, lifecycle, failure modes).
**Status:** normative design guide. Nothing here is implemented; Phase 3A remains blocked by the
activation checklist.

> **Principle.** PEMS is implemented as one reusable product, many organizations, configurable
> business policies, immutable historical truth, and invariant system safety. A business-policy
> change normally needs *configuration + approval + effective date + audit*, not a source-code
> change, recompile and redeploy.

Packaging business decisions D1–D7, E-4, E-5 and D12 are **configurable policies, not permanent
company-specific source-code rules.**

## Document map

```
FINAL BUSINESS BASELINE  (decision options, sign-off)
        └── CONFIGURABILITY GUIDE  (this document: policy model, versioning, effective dating,
                │                    scope, validation, authorization, audit)
                ▼
        ARCHITECTURE FREEZE  (§15 makes this guide part of the freeze)
                ▼
        Phase 3A → 3I
```
No Packaging documentation index exists in `docs/`; none was created.

---

# PART A — Normative Requirements

## A1. Core rule
- Do not turn current technical recommendations into permanent rules.
- Do not branch on company, plant, customer or packaging type (`if (company == …)`).
- Domain services consume a **resolved policy**; they never know which organization chose it:
  `Organization → Site → Packaging Policy → Validated Policy → Domain Services → Execution / Quality / Inventory / Reporting`.

## A2. Configuration vs domain safety invariants

| Configurable business policy (may vary by organization/site) | Non-configurable invariant (enforced in code, never switchable) |
|---|---|
| package definition model · mixed-product policy · multi-batch policy · weighing model · bulk vs packaged identity · quality release authority · permission model · packaging-material treatment · financial treatment · allowed packaging operations · approved tolerances · required workflow stages · effective dates · site-specific rules | inventory ledger integrity · idempotent posting · concurrency protection · referential integrity · audit-trail integrity · authorization enforcement · **no inventory posting unless quality is Released** · no negative stock where existing policy forbids it · no impossible quantities · no invalid genealogy · no duplicate posting · no SoD bypass · no deletion of immutable history · no barcode identity corruption · no historical rewriting · no unauthorized policy activation |

A configuration must never disable an invariant.

## A3. Scope
Support `Organization → Plant/Site → (Production Area) → (Packaging Line)`. Inspect and reuse the
existing organizational model; do **not** invent a parallel tenancy or hierarchy. Choose the lowest
existing level that can safely own the configuration and document it (Part B, B2).

## A4. Policy model (conceptual target)
`PackagingPolicy` → identity, scope, version, status, EffectiveFrom/To, and one value each for:
D1 PackageDefinitionMode · D2 MixedProductMode · D3 MultiBatchMode · D4 WeighingMode · D5
SplitMergeRepackPolicy · D6 ProductIdentityMode · D7 QualityReleaseMode · E4 PermissionModel · E5
RoleMappingReference · D12 FinancialTreatmentMode · further validated parameters. Reuse an existing
configuration/policy framework if one fits; do not create a Packaging-only duplicate.

## A5. Decision options become enumerated policy values (not free strings)

| Decision | Modes |
|---|---|
| D1 | FilledProductUnit · GenericPackagingUnit · LogisticsHandlingUnit · Layered |
| D2 | SingleProduct · MixedProduct · PrimaryProductWithControlledComponents |
| D3 | SingleBatchOnly · MultipleBatches · PrimaryBatchWithSecondaryGenealogy |
| D4 | GrossPlusTare · GrossPlusNet · GrossTareNet · EventBasedWeighing |
| D5 | Split / Merge / Repack each Disabled or Enabled |
| D6 | SameMaterialIdentity · SeparateMaterialIdentity · ParentChildIdentity · TransformationIdentity |
| D7 | IndependentQuality · QAManager · DedicatedPackagingQuality · Hybrid |
| E-4 | ReuseProductionPermissions · DedicatedPackagingPermissions · Hybrid |
| D12 | NoFinancialTransformation · PackagingMaterialConsumption · ProductTransformation · CostTransformation · FullCostingIntegration |

Names must follow existing PEMS conventions; equivalent existing concepts win over these names.

## A6. Per-decision rules
- **D1–D3:** validate package creation against the *active* policy; a later policy change never
  rewrites history (Package P001 created under v3 stays valid under v4).
- **D4:** policy chooses which measurements are required; always validate arithmetic, preserve
  measured values, source, UOM, timestamp, operator/device, audit; never invent precision or
  tolerance (configurable only where the business defines them).
- **D5:** operations may be `Disabled` initially; enabling by configuration must not make an
  operation available unless its implementation, permissions, workflow, genealogy, inventory and audit
  controls are complete — *configuration availability ≠ implementation availability*.
- **D6:** do not assume bulk = one item or packaged = separate item; use an abstraction so the
  model can change without rewriting execution logic.
- **D7:** resolve authority through a policy abstraction (e.g. `PackagingQualityReleasePolicy`), not
  `if (user.Role == "QAManager")`. Only the configured authority may perform the quality transition;
  Produced → AwaitingQC → Released | Blocked | Rejected is unchanged.
- **E-4:** use the existing permission infrastructure; no second authorization engine; no role
  checks in controllers; controllers/UI call the existing authorization abstraction.
- **E-5:** map *business responsibility → PEMS role → permission set*; the domain never cares which
  concrete role name was chosen.
- **D12:** separate from operational execution; expose a boundary (e.g. `PackagingFinancialPolicy`);
  no invented accounting rules; remains a Phase 3C activation gate.

## A7. Versioning, effective dating, lifecycle
Policies are versioned and effective-dated. States (aligned with existing PEMS conventions):
`Draft · PendingApproval · Approved · Active · Superseded · Retired`. A future policy may be created
before its effective date. Resolution key: **organization + site (+ line) + business context +
effective date**. Every package (and other packaging record that depends on policy) keeps the policy
version under which it was created; history is never reinterpreted with the current policy.

## A8. Governance and audit of policy change
Requires permission; records creator, approver, approval date, effective date, previous version,
changed values; writes an audit entry; forbids direct modification of an active policy. Reuse the
existing audit, approval, workflow and permission mechanisms.

## A9. Activation validation
A populated policy is not an active policy. Before `Active`:
- **Structural:** all decisions chosen; valid values; valid dates; no overlapping active policies; valid scope.
- **Business:** D1/D2/D3 combination supported; D4 compatible with quantity/UOM model; D6 compatible
  with Material/Inventory; D7 compatible with actual role mapping; E-4 compatible with authorization
  config; D12 complete before inventory/financial activation.
- **Technical:** required domain implementation, permissions, workflow, inventory, barcode and audit
  integration exist. If any dependency is missing the status is `BLOCKED`, not `Active`.

## A10. Capability readiness
A policy may be Configured → Validated → Approved → Active, but a *capability* must also be
Implemented → Tested → Authorized → Operationally Ready. Configuration must not activate
unfinished functionality (e.g. `Split = Enabled` is rejected while Split is not production-ready).

## A11. Runtime resolution and caching
Application services call a policy resolver once (`ResolveAsync(organization, site, effectiveDate)`)
and pass the resolved policy down: Controller → Application Service → Policy Resolver → Domain
Service; no scattered configuration queries. A cache is allowed only with deterministic
invalidation and the policy **version** in its identity; processing of historical records uses their
recorded version; no distributed cache unless PEMS already requires one.

## A12. UI, explainability, reporting
- **UI:** policy administration is separate from daily execution (Packaging → Operations · Packages
  · Quality · Traceability · Administration → Packaging Policies). Show current policy, version,
  scope, effective date, status, approver, and each decision; use business terms, not raw enum names.
- **Explainability:** for any package, show policy, version, scope, effective date, active decisions,
  approver (example: "Package PKG-000123 · Policy v4 · Plant A · effective 2028-01-01 · D1 Filled
  Product Unit · D2 Single Product · D3 Single Batch · D4 Gross + Tare · D7 Independent Quality").
- **Reporting:** distinguish current policy, historical policy and policy version; never compare
  history as if created under the current policy; KPI/PI/RI/KRI that depend on policy assumptions
  must name the version. The 56-metric baseline stays authoritative; no targets are invented.

## A13. Migration and defaults
No destructive migration; no single global policy assumed; no fabricated defaults to make a
migration pass. A seed such as "System Default / Not Activated" is allowed only if technically
required and it cannot activate any Packaging behavior. **"No policy configured" must not mean "use
the developer recommendation"**; it must mean *Packaging capability unavailable / configuration
required*, unless an approved PEMS default explicitly says otherwise.

## A14. Company Independence Test
The same deployed codebase must support Company A (FilledProductUnit / SingleProduct /
SingleBatch / GrossPlusTare / SeparateMaterialIdentity / IndependentQuality) and Company B (Layered /
PrimaryProductWithControlledComponents / MultipleBatches / GrossTareNet / ParentChildIdentity /
Hybrid) with configuration only. Needing a source change for those differences means the design is
not configurable enough.

## A15. Mandatory pre-code review
Before Phase 3A: read the baseline, the sign-off baseline and the activation checklist; inspect the
repository and the database schema; identify reusable configuration/policy mechanisms; write a short
architecture finding (existing mechanism → Packaging integration point → reason for reuse); only then
implement. Do not convert recommendations into approvals; do not activate a policy without approval;
do not change unrelated modules, the Production quality gate, `InventoryTransaction` /
`MaterialStock` / `IMaterialStockService`, `BarcodeIdentity`, `WorkflowPlatform` or `AuditLog`.

## A16. Acceptance criteria
D1–D4, D6, D7, E-4 and D12 configurable; D5 controlled; E-5 supports organization-specific mapping;
policies versioned and effective-dated; history keeps its policy context; activation permission-
controlled; changes audited; invalid combinations rejected; unsupported capabilities cannot be
activated by configuration; no company-specific rule hard-coded; no duplicate configuration,
authorization or workflow framework; quality gates, `BarcodeIdentity`, `AuditLog` and the 56 metrics
unchanged; build, relevant tests and full regression pass; migration non-destructive; final
architecture audit passes.

---

# PART B — Repository Findings and How the Guide Maps to PEMS

Findings from inspecting the code and the live database on 2026-09-25 (guide §30 / §33 step 4–6).

## B1. Reusable mechanism inventory

| Need (guide) | Existing mechanism (verified) | Reuse for Packaging | Verdict |
|---|---|---|---|
| Organization / tenant | `BaseEntity.TenantId` (nullable `int?`), documented as "prepared for future multi-company, currently unused, stays null". **No Tenant/Organization/Company entity, no query filter, no usage outside `BaseEntity` and `Department`** | Do **not** use for scoping; do not invent a tenancy layer | **No tenant abstraction exists** — see B2 |
| Site / plant / area | `FunctionalLocation` (self-referencing tree via `ParentId`, with free-text `Plant`/`Area`/`Unit`); `EmployeeAreaScope` (free-text Plant/Area/Unit per employee); `Warehouse`; `Project` | Scope anchor | Site is a tree node or text, not an entity |
| Packaging line | `ProductionWorkCenter` (`Code`, `Name`, required `FunctionalLocationId`) | Line-level scope | Natural fit; already the production "line" |
| Settings / config UI | Admin `SettingsController`, `DashboardSettingsController`, `QuickSetupController` (setup/UI settings) | Not policy stores | No general policy framework |
| Business rule as data | `ContractApprovalAuthorityRule` (admin CRUD, `SystemAdmin` only, `Priority`, `IsActive`, no effective dating), `SoDPolicy`, `RoutingRules`, `GasParameterLimit`, `AIConfidenceSettings` | Pattern: admin-editable policy table | Precedent exists; none is versioned/effective-dated |
| Versioning + effective dating + lifecycle | `WorkflowVersion` (`VersionNumber`, `Status` Draft…, `EffectiveDate`, `PublishedDate`, `ValidatedDate/By`, `RetiredDate`, `ClonedFromVersionId`, `ChangeSummary`, `RowVersion`) and `WorkflowInstance` bound to a version | **Pattern to mirror** for `PackagingPolicyVersion` | The one versioned, effective-dated precedent — workflow-specific |
| Config-change audit | `WorkflowConfigAudit` (action, previous/current state); generic `AuditLog` interceptor for every `BaseEntity` | Use the generic interceptor; do not reuse the workflow-specific table | `AuditLog` suffices |
| Approval of a change | `IWorkflowRuntime` (12 services use it) | Policy approval **can** be a workflow instance if an approval chain is required | Reuse if chain approved; else permission-gated |
| Authorization | `HasPermissionAsync(roles, module, action)` (20 uses in `ProductionRunController`); **0 role-name literals** in Production controllers/services | The seam for E-4/E-5 | Already role-name-free |
| Role mapping | `RolePermissions` data (admin-editable, live) | E-5 = data rows; **no second role-mapping table** | Already configurable |
| Feature flags | none (no framework) | — | Do not add one |
| Enum convention | **No enum is persisted**; string columns + static whitelist arrays in services (`LossCategories`, `CauseCategories`, transition dictionaries); no `HasConversion` | New narrow convention for policy values only (typed value + stable text code) | M-3, see B4 |

## B2. Scope and tenancy — M-1 CLOSED (architecture decision; see `PEMS_OPERATIONS_PACKAGING_CONFIGURABILITY_ARCHITECTURE_DECISIONS.md`)
PEMS has **no organization or site entity**. **Decision M-1:** one company = one independent PEMS
deployment with its own database. This is *multi-company support through independent deployments*,
not multi-tenancy; Packaging introduces no tenant model and does not use `TenantId`. The guide's
`Tenant / Organization` level is therefore *not available* until the platform itself gains tenancy,
which would be a platform-wide initiative, not a Packaging feature.

Within one deployment, the lowest existing anchors are `FunctionalLocation` (tree) and
`ProductionWorkCenter` (line). **Technical recommendation TI-4 (non-binding):** scope precedence,
nearest wins: **deployment-wide → `FunctionalLocation` subtree (site/plant) → `ProductionWorkCenter`
(line)**, resolved by effective date. "Deployment-wide" means an *approved* policy covering the whole
deployment — not a developer default (A13).

## B3. Where the policy lives — M-2 CLOSED (architecture decision)
**Decision M-2:** a reusable **PEMS Policy Infrastructure** provides lifecycle and governance only
(identity, scope, version, effective dates, status, approval binding, validation hook, audit).
**Each domain owns its policy schema and interpretation**; the Packaging domain owns the D1–D7, E-4,
E-5 and D12 values, validators and resolver contract. No generic "BusinessRule" table, and no domain
logic in the infrastructure. The lifecycle pattern to follow is `WorkflowVersion` (`VersionNumber`,
`Status`, `EffectiveDate`, `RetiredDate`, `ClonedFromVersionId`, …). Packaging records that depend on
policy carry an additive **`PackagingPolicyVersionId`** so history keeps its context (technical
recommendation; table shapes are open item TI-1). Build order: step **3A-0** of the freeze work plan.

## B4. Value representation — M-3 CLOSED (architecture decision)
Repository facts (re-checked 2026-09-25): no entity property is an enum, `PEMS.Persistence` has no
`HasConversion`, and the eight enums that exist (DTO/UI/workflow contracts) are never persisted;
persisted values are strings guarded by static whitelists. **Decision M-3:** policy values are
strongly typed in the application/domain layer, persisted as **stable text codes** (not ordinals),
defined in a per-decision registry, and validated centrally; arbitrary strings, spelling variants,
undocumented modes, code reuse and silent developer-default fallback are prohibited; retired modes
remain interpretable. The exact type form is open item TI-2.

> **Safety-review note (2026-09-25).** Guide A2's "authorization enforcement" and A6's "E-5 role mapping" assume permission grants can be administered at runtime; the safety review found no runtime grant/revoke path for new actions (PR-5). A9's activation validation must also treat an *approval workflow that would auto-approve* as a `BLOCKED` dependency (PR-1), and A16 gains: no activation without a mandatory `BusinessApprovalReference` (PR-11) and no capability operational unless registry-`ProductionReady` (HI-1, PR-14).

## B5. Configurable ≠ everything implemented in the first release
A policy allowing every mode implies storage general enough for all of them. PEMS's existing habit
already points the right way (one row per source batch, one row per weighing event), so the storage
can be *general in shape* while each mode is *implemented and enabled only when ready* (guide A10).

| Decision | Mode | Extra structure it needs | Existing change needed? |
|---|---|---|---|
| D1 | FilledProductUnit | none beyond `Package` | no |
| D1 | GenericPackagingUnit / Layered / LogisticsHandlingUnit | container/handling entity (+ hierarchy) | no (new tables) |
| D2 | SingleProduct | none | no |
| D2 | MixedProduct / ControlledComponents | package content lines; component consumption | no (new tables); components need inventory issue |
| D3 | SingleBatchOnly | one contribution row | no |
| D3 | MultipleBatches / Primary+Secondary | contribution rows with quantity | no |
| D4 | GrossPlusTare / GrossPlusNet / GrossTareNet / EventBased | weighing-event rows; per-mode required set | no |
| D6 | Same / Separate identity | independent output item | no |
| D6 | **ParentChildIdentity** | relation between `MaterialItem` rows | **Yes — `MaterialItem` has no parent field; changes a table used by procurement, maintenance and production** |
| D6 | TransformationIdentity | transformation record + inventory semantics | new tables; D12 dependent |
| D7 | all | permission resolution only | no (data) |
| E-4 | Dedicated / Hybrid | module registry entry, role seed, SoD rows | no |
| D12 | modes beyond None | accounting design | needs D12 approval |

Consequence for the first release: the framework (policy, versioning, resolver, validation,
readiness) is built once; the modes are enabled per the **capability matrix** kept in `PEMS_OPERATIONS_PACKAGING_CONFIGURABILITY_ARCHITECTURE_DECISIONS.md`
(M-4). A mode not Implemented + Tested + Authorized + Production-Ready cannot be activated (guide A9/A10).

## B6. Company Independence Test — result by design
Company A's set is the simplest and is supportable first. Company B's set requires a layered
container model, component consumption, multi-batch contributions, GrossTareNet and a `MaterialItem`
parent relation. The design supports it **when those capabilities are built**; it does **not** pass
"configuration only" in the first release, by design, because those modes will be marked *not
implemented*. This is the honest reading of guide §20.

## B7. Meaning of the sign-off under this guide
Each "Selected Option" in the Final Business Baseline is the value of the **initial Packaging Policy
(v1) for the approving organization and scope**. It is not a permanent product rule. Another
organization or a later effective date signs its own policy version. Approval of D5 remains an
acknowledgement that split/merge/repack are policy `Disabled` and not implemented.

## B8. Architecture items M-1…M-5 — status

Decided in `PEMS_OPERATIONS_PACKAGING_CONFIGURABILITY_ARCHITECTURE_DECISIONS.md` (architecture decisions, **not business approvals**; they do not change any D/E row in the sign-off baseline).

| ID | Item | Status | Decision | Residual |
|---|---|---|---|---|
| M-1 | Deployment model | **CLOSED — ACCEPTED** | Separate deployment/database per company; no tenant model in Packaging | none for Packaging; multi-tenancy is a future platform initiative |
| M-2 | Policy framework | **CLOSED — ACCEPTED** | Reusable PEMS Policy Infrastructure (lifecycle/governance) + domain-owned schema | TI-1 table shapes/names |
| M-3 | Value representation | **CLOSED — ACCEPTED** | Typed values, stable text codes, central validation, no fallback | TI-2 exact type form and registry location |
| M-4 | First-release scope | **CLOSED — ACCEPTED (principle)** | Only Implemented+Tested+Authorized+Production-Ready capabilities may be Active; matrix in the ADR | per-mode release-1 scope chosen with the business approvals |
| M-5 | Policy approval | **CLOSED — ACCEPTED** | `IWorkflowRuntime` for governed changes; risk-based tiers (Tier 2 empty); `AuditLog` always | TI-3 approval-role configuration; approvers depend on E-5 (business) |

Technical items TI-1..TI-4 are **settled (design)** in `PEMS_OPERATIONS_PACKAGING_POLICY_INFRASTRUCTURE_TECHNICAL_DESIGN.md` (documentation only; the evidence for activation gate 15, whose status is *technical evidence complete - architecture owner approval required*). Corrections made by that design: the scope levels that actually exist are Deployment, `FunctionalLocation` (shallow: 32 rows, depth <= 2, `Plant`/`Unit` unpopulated) and `ProductionWorkCenter` (0 rows); a value type persisted as a text code replaces the enum/whitelist choice; approval stages name a permission, not a role.
