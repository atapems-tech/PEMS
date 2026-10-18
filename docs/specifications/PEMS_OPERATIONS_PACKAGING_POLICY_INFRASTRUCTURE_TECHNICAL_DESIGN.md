# PEMS Packaging — Policy Infrastructure Technical Design (TI-1 … TI-4)

**Document ID:** PEMS-OPS-PKG-POLICY-INFRA-TD · **Version:** 1.0 · **Date:** 2026-09-25
**Kind:** pre-implementation architecture design record. **Documentation only. Not a business approval.**
**Implementation status:** Packaging remains **C — IMPLEMENTATION BLOCKED** · business approvals **0 / 16** ·
activation gates **0 / 15**. Nothing described here exists, and nothing here creates an entity, migration,
permission, role, workflow definition, seed or test.

Parents: `PEMS_OPERATIONS_PACKAGING_CONFIGURABILITY_ARCHITECTURE_DECISIONS.md` (M-1…M-5; this record settles
its residual items TI-1…TI-4) · `PEMS_OPERATIONS_PACKAGING_CONFIGURABILITY_GUIDE.md` ·
`docs/audits/PEMS_OPERATIONS_PACKAGING_CONFIGURABILITY_ARCHITECTURE_AUDIT.md` (design audit in its §9).

> **Safety review 2026-09-25 (`docs/audits/PEMS_OPERATIONS_PACKAGING_FINAL_ARCHITECTURE_SAFETY_REVIEW.md`):** this record was reviewed against source and live-database evidence. Statements marked *[Amended]* below are corrected or superseded by the section "Safety Review Amendments" (before the sign-off block); 16 required changes PR-1..PR-16 are recorded there. The record is therefore *technical evidence complete subject to PR-1..PR-16*, not a final design.

Vocabulary: **business decision** (D1–E-5, D12 — only management, only in the sign-off baseline) ·
**architecture decision** (how a business decision is represented) · **technical implementation decision**
(how it is stored/coded). This record contains architecture decisions and *proposals for* implementation
decisions. Names below are **logical** and may change in the Phase 3A design freeze without reopening the
decisions. Status wording: `ACCEPTED (design)` = adopted for this design record on the program owner's
instruction of 2026-09-25; the architecture-owner name/date line (§21) is blank because no approval is invented.

---

# Executive Summary

| ID | Question | Decision (design) |
|---|---|---|
| **TI-1** | Shape of the policy infrastructure | **Two** generic structures — `PolicyDefinition` (identity + scope) and `PolicyVersion` (lifecycle) — plus a **domain-owned** typed values table (`PackagingPolicyValues`). No separate Scope, Approval or AuditReference tables: scope is an immutable property of the definition, approval state lives in the existing workflow instance, audit is the existing `AuditLog`. Boundary is a **namespace boundary** in existing layers, enforced by an architecture test — not a new project |
| **TI-2** | Value representation | A **registry-backed value type persisted as a stable text code** — *not* a C# enum and *not* a raw string. A value type cannot be mapped by EF without an explicit converter, so ordinal persistence becomes structurally impossible; unknown historical codes load as an explicit *Unrecognized* value that validation rejects, instead of crashing or defaulting |
| **TI-3** | Approval-role mapping | Reuse the **existing permission-participant pattern**: a workflow approval stage names a *permission* (`Permission:PackagingPolicy.Approve`), never a role. Which role holds that permission is ordinary grant data set by an authorized administrator after E-5 business approval. Domain code contains no role names. Rejected: policy-level mapping, site-level mapping, a new responsibility-to-role table |
| **TI-4** | Scope precedence | Three scope kinds that exist in PEMS today: **Deployment → FunctionalLocation subtree → ProductionWorkCenter**, nearest wins, whole-policy replacement (no field merging), deterministic, ambiguity is an error. Organization, plant/site and packaging-line levels are **not** scope kinds (no such entities/data exist) |

Two design-critical discoveries made while inspecting the repository (details in *Current PEMS Findings*):

1. **A workflow stage with no participant is auto-approved by the runtime** (`TryAutoSkipEmptyStageAsync`), and the workflow validator only *warns* about it. Policy approval therefore must never treat "workflow completed" as proof of approval; it requires a recorded human `Approve` action by an actor other than the submitter, and a pre-submit routing check.
2. **The organizational data is thin**: `FunctionalLocation` has 32 rows, 28 of them roots, maximum depth 2, `Plant`/`Unit` never populated; `ProductionWorkCenter` has **0 rows**. The scope hierarchy is real but shallow; the deployment level carries most policies for now.

Gate 15 evidence status: **TECHNICAL EVIDENCE COMPLETE — ARCHITECTURE OWNER APPROVAL REQUIRED** (§ Phase 3A Implications). Gate 15 is **not** checked.

---

# Current PEMS Findings

Inspected 2026-09-25 (source and read-only database queries; no writes). Where earlier documents said otherwise, the repository wins and the correction is noted.

## F.1 Candidate inventory (reuse assessment)

| Existing mechanism | Location | Purpose | Reusable? |
|---|---|---|---|
| **AuditLog interceptor** | `PEMSDbContext.PrepareAuditEntries/FinalizeAuditEntries`; entity `AuditLog` (`EntityName`, `EntityKeyValue`, `Action`, `ChangedFieldsSummary`, `UserId`, `UserName`, `Timestamp`) | Records **every** Added/Modified/Deleted entity (all entity types, not only `BaseEntity`) with `prop: old -> new` text | **Reuse as is.** Limits: flat text; no reason field (so the version carries its own `ChangeSummary` / retirement reason) |
| **BaseEntity** | `PEMS.Domain/Common/BaseEntity.cs` | `CreatedBy`, `ModifiedBy`, `RowVersion` (`[Timestamp]`), `TenantId` (unused) | **Reuse** for new entities. `FunctionalLocation` is *not* a `BaseEntity`; `ProductionWorkCenter` is |
| **WorkflowDefinition / WorkflowVersion** | `PEMS.WorkflowPlatform/Entities` | Versioned, effective-dated lifecycle (`VersionNumber`, `Status`, `EffectiveDate`, `PublishedDate`, `ValidatedDate/ById`, `RetiredDate`, `ClonedFromVersionId`, `ChangeSummary`, `RowVersion`); status strings from static const class `WorkflowVersionStatus` | **Reuse the pattern, not the tables** (workflow-specific; coupling policy to workflow stages would be wrong) |
| **WorkflowBinding + WorkflowBindingService** | same | Effective-dated (`EffectiveFrom/To`, `IsActive`) mapping of (Module, EntityType, Context) → definition/version; overlapping active bindings rejected **at write time and at resolve time** (`WorkflowBindingAmbiguousException`); "never the first match" | **Reuse the pattern for policy resolution** and **reuse the binding itself** to find the approval workflow |
| **IWorkflowRuntime** | `PEMS.WorkflowPlatform/Interfaces` | Single approval entry point ("No module is allowed to implement its own approval engine"); ~20 files use it; history is immutable (`WorkflowHistory`: `ActionType`, `UserId`, `Role`, `ActionDate`, …) | **Reuse** for policy approval |
| **`Permission:` participants** | `WorkRequestService.ExpandRolesAsync`, `WorkOrderWorkflow.ParticipantPrefix`, `WorkflowVersionValidator` (`PERMISSION_UNKNOWN`) | A stage's `ResponsibleRole` may be `Permission:Resource.Action`; the calling module adds it to the actor's roles when the actor's real roles hold the permission. **40 live workflow stages already use it** | **Reuse — this is the TI-3 mechanism** |
| **Empty-participant auto-skip** | `WorkflowRuntimeService.TryAutoSkipEmptyStageAsync` | A stage with empty `ResponsibleRole` is skipped as an implicit approve (history `AutoSkip`) and can complete the instance; validator issues only `STAGE_NO_PARTICIPANT` **warning** | **Hazard** — see Security and Failure Modes |
| **IAccessControlService / PermissionService** | `AccessControlService`, `PermissionService.HasPermissionAsync` | `HasPermissionAsync(roles, module, action)` → `AuthorizeForRolesAsync` → `Permission` + `RolePermissionGrant`; ABAC scope check via `RoleAssignment.ScopeType/ScopeValue` (only `"Global"` is used today); SoD via `SoDPolicy` rows on the same resource; SystemAdmin bypass with a **break-glass list** (`ExecutionGate.EmergencyOverride`, `WorkflowManagement`) | **Reuse.** Three parallel permission tables feed it (`RolePermission` legacy flags, `RoleModuleAction`, `Permission`+`RolePermissionGrant`); live counts: 220 permissions, 100 role-module-actions, 10 SoD policies. Role references in `RolePermission`/`RoleModuleAction` are **role-name strings** (a role rename breaks them — existing platform limitation) |
| **FunctionalLocation** | `PEMS.Domain/Entities/FunctionalLocation.cs` | Self-referencing tree (`ParentId`) + free-text `Plant`/`Area`/`Unit`. **Live: 32 rows, 28 roots, max depth 2; `Plant` and `Unit` never populated; `Area` on 28** | **Reuse as a scope anchor** |
| **ProductionWorkCenter** | `PEMS.Domain/Entities/ProductionWorkCenter.cs` (`BaseEntity`, required `FunctionalLocationId`) | Production "line"; `ProductionOrder.ProductionWorkCenterId` is required, `ProductionRun.ProductionWorkCenterId` optional. **Live: 0 rows** | **Reuse as the lowest scope anchor** |
| Department / EmployeeAreaScope / Warehouse / Project | Domain entities | Department: 264 rows, none with a parent (flat), carries `OrganizationDomain`; EmployeeAreaScope: free-text Plant/Area/Unit; Warehouse: 9; Project: 1435 | **Not scope anchors** — flat, free-text, or a different concept |
| Policy-as-data tables | `ContractApprovalAuthorityRule`, `SoDPolicy`, `RoutingRule`, `GasParameterLimit`, `GasTestValidityRule`, `SimopsInteractionRule`, `AIConfidenceSettings` | Admin-editable rule data | **Pattern only.** None of the seven has a version or effective-date column (checked) |
| Settings / options / configuration | `appsettings.json` (Ollama, OCR, Ftp, Authentication, Logging); `IOptions<>` in 2 files; admin `SettingsController` etc. | Deployment/infrastructure configuration | **Not reusable**: not business policy, not versioned, not audited, not effective-dated |
| Feature flags | none (no framework, no `FeatureManagement`) | — | Do not add one (M-4's capability readiness is the mechanism) |
| Organization / tenant | none; `TenantId` unused | — | Nothing to reuse (M-1) |
| Enums | none persisted; 8 non-persisted enums in DTO/UI/workflow code; no `HasConversion` in `PEMS.Persistence` | — | Convention gap → TI-2 |
| Localization | EN/FA `.resx` + inline `T(fa, en)` in services | Display text | **Reuse** for mode display names (key = stable code) |
| Time | `DateTime.Now` used throughout (local time, not UTC) | — | Clock convention is an **open question** (OQ-2) |

## F.2 Corrections to earlier statements
- Earlier findings (audit C-3) said "no enums on entities"; the accurate statement is *no persisted enum anywhere and no `HasConversion`* (8 enums exist in non-persisted code). Consequence unchanged.
- The Guide/ADR scope recommendation "FunctionalLocation subtree (site/plant)" over-stated the data: there is no site/plant node in the data; see TI-4.

---

# TI-1 Policy Infrastructure

## Decision (ACCEPTED, design)
A reusable, **domain-neutral** Policy Infrastructure that owns *lifecycle and governance only*, implemented as **two tables and a small set of service contracts**, inside the existing layers (`PEMS.Domain` entities, `PEMS.Persistence` configurations/services, `PEMS.Application` contracts) under a `Policy` namespace/folder. The domain-owned values table is separate and belongs to the consuming domain.

## What it owns / does not own
| Owns (infrastructure) | Does **not** own (domain) |
|---|---|
| policy identity, policy type code, scope, version number, status, effective-from/to, approval integration, validation *boundary* (calls the domain's validator), activation/supersession/retirement, audit integration | policy fields, mode meanings, validators' rules, capability readiness, business rules, invariants |

Explicit non-goals (each **rejected**): generic BusinessRule/expression engine · arbitrary JSON or key-value store of business logic · a second workflow engine · a second authorization engine · a Packaging-specific copy of any of these.

## Why two tables, not five
| Candidate structure | Verdict | Reason |
|---|---|---|
| `PolicyDefinition` | **Keep** | Identity + type + scope: the stable thing versions belong to |
| `PolicyVersion` | **Keep** | The lifecycle unit; carries status, dates and stamps |
| `PolicyScope` | **Fold into Definition** | Scope is 1:1 with a definition and never changes. A separate table adds a join and an immutability rule for nothing |
| `PolicyApproval` | **Not needed** | Approval state and history already live in `WorkflowInstance` + immutable `WorkflowHistory`, found by the existing explicit link (`EntityKey`/`FindInstancesForEntityAsync(module, entityType, id)` pattern used by `WorkRequestService`). `PolicyVersion` stores only the *result stamps* (`ApprovedOn/ById`) |
| `PolicyAuditReference` | **Not needed** | `AuditLog` already records every insert/update/delete of both tables by (`EntityName`, `EntityKeyValue`) |
| `PackagingPolicyValues` | **Domain-owned, keep** | Typed columns for the Packaging decisions; 1:1 with a version |

## Boundary enforcement
- Namespace `…Policy.*` must not reference any Packaging type. An **architecture test** (reflection/namespace scan) enforces it (planned for 3A-0).
- A separate project (like `PEMS.WorkflowPlatform`) is **not** proposed for release 1: one consumer does not justify a project boundary. Revisit when a second domain adopts it.
- The infrastructure calls the domain through one registered contract per policy type (validator + values loader); the domain never calls infrastructure internals other than the resolver and lifecycle service.

## Logical contracts (names indicative)
```
IPolicyLifecycleService   Create draft / clone · Submit · RecordApproval · Activate · Retire · Reject
IPolicyResolver           Resolve(PolicyType, ScopeContext, EffectiveDate) → PolicyResolution
                          GetByVersion(PolicyVersionId) → PolicyVersion (for historical access)
IPolicyDomainRegistration one per policy type: PolicyType code · validator · values loader · permission module name
IPolicyDomainValidator    Validate(versionId, stage) → issues[{code, severity, stage, message-key}]
```

---

# TI-2 Policy Value Representation

## Evaluated options
| Criterion | A. C# enum stored as text (`HasConversion<string>`) | B. Raw validated strings + central whitelist | C. **Registry-backed value type, stored as text code** |
|---|---|---|---|
| Readability in DB/audit/reports | good (member name) | good | good (stable code) |
| Ordinal persistence possible? | **Yes, by default.** With no `HasConversion` anywhere in PEMS, a new enum property maps to **int** unless someone remembers the converter | n/a | **No.** EF cannot map a custom value type without an explicit converter; forgetting it fails at model build |
| Arbitrary strings / spelling variants | not possible in code; `Enum.Parse` also accepts numeric text ("1") and case variants | possible at every call site that writes a literal | not possible: values can only be minted by the registry |
| Silent fallback | `default(enum)` = 0 is a valid-looking member; unknown DB text **throws on entity load**, taking the whole row (and any report reading it) down | caller-defined | **Unrecognized** is an explicit value; validators reject it; reporting shows it; nothing defaults |
| Retiring/renaming a mode | member rename silently changes the persisted meaning; removing a member breaks old rows | whitelist edit | code is immutable; a mode is retired by registry *status*, the row stays interpretable |
| Historical interpretability | weak (rename/removal hazards) | weak (no per-code metadata) | **strong** (code → registry entry with meaning, status, since-version) |
| Localization | resx key from member name | resx key from string | resx key from stable code |
| Reporting / API | name | string | code (+ optional display) |
| DB portability | text column | text column | text column |
| Fit with PEMS conventions | new (no persisted enums) | current habit (`LossCategories`, `WorkflowVersionStatus` const strings) | extends the habit: const codes + a registry, plus typing |
| Migration safety when adding a mode | additive code | additive code | additive registry entry; **no schema change** |

## Decision (ACCEPTED, design)
**Option C.** Policy values are a **`PolicyMode`-style value type** minted only by a per-decision registry, persisted as a **bounded text column holding the stable code**, converted by an **explicit EF value converter**, validated centrally.

Binding rules (carried from ADR M-3, now concrete):
1. **One registry per decision** (D1, D2, D3, D4, D5-split/merge/repack, D6, D7, E-4, D12). Each entry: stable code, status (`Designed` · `Implemented` · `Deprecated` · `Retired`), business meaning, first-used version, resx key.
2. **No mode literal outside the registry.** Domain code compares against registry constants, never strings, never numbers.
3. **Codes are immutable and never reused.** Retire by status.
4. **Parse = registry lookup.** Unknown/empty text → `Unrecognized(code)` (never throws, never defaults); a policy containing one is invalid and cannot pass activation.
5. **"Not configured" is a state of the policy (no version in force), never a mode value.**
6. **No developer defaults** anywhere in the registry (no `IsDefault`).
7. **No CHECK constraint listing codes** in the database (would need a migration per mode). Integrity is enforced by the validator plus a **policy-integrity test** that compares distinct persisted codes to the registry.
8. Only the **policy** structures use this; existing entities are not converted.

## Residual technical detail (3A design freeze, not decided here)
Whether the value type is a `readonly record struct` or a sealed class, and whether the registry lives in `PEMS.Domain` or `PEMS.Application` (recommendation: the *types* in `PEMS.Domain`, the Packaging registry contents alongside the Packaging policy code).

---

# TI-3 Approval Role Mapping

## Requirement
`Business responsibility → configured policy approval responsibility → PEMS role mapping → permission/workflow authorization`, with no `if role == "QAManager"` in code and no duplicate RBAC.

## Options evaluated
| Option | Assessment | Verdict |
|---|---|---|
| A. Mapping stored **in the policy** | Puts authorization data inside a versioned business policy: the policy that defines *who approves policies* approves itself; a role rename would need a new policy version; duplicates RBAC | **Rejected** |
| B. Organization/site-level configuration | A new mechanism. The organization is the deployment (M-1). Site-limited authority already exists as `RoleAssignment.ScopeType/ScopeValue` (unused today) | **Rejected** (use existing ABAC scope if ever needed) |
| **C. Existing workflow role mapping via permission participants** | Already in production use (`Permission:` participants in 40 live stages; `WorkRequestService.ExpandRolesAsync`); validator already checks the permission exists | **ACCEPTED** |
| D. New reusable responsibility-to-role mapping table | Would be a **fourth** role/permission mechanism beside `RolePermission`, `RoleModuleAction` and `Permission`/`RolePermissionGrant`. In PEMS the *permission is* the responsibility | **Rejected** |

## Decision (ACCEPTED, design)
1. **The responsibility is a permission.** Policy administration defines a small action set on the domain's policy module — for Packaging the resource `PackagingPolicy` with actions `View`, `Edit` (draft), `Submit`, `Approve`, `Activate`, `Retire` (proposal; the legacy `RolePermission` flags cannot express these, so the `RoleModuleAction` mechanism — designed for exactly this in CMMS-10a — is recommended; final mechanism confirmed in 3A).
2. **The role mapping is ordinary grant data.** *[Amended - Safety Review P.2: today there is no runtime way to grant or revoke a permission for a new action; see PR-5.]* "Packaging Policy Approval" → `PackagingPolicy.Approve` → granted to whichever role(s) the organization maps: `QAManager` at Company A, `PackagingQualityManager` at Company B, each in its own deployment (M-1). No source change between them.
3. **The approval workflow references the permission, never a role**: the policy-approval stage's participant is `Permission:PackagingPolicy.Approve`. The workflow definition is *data* administered through the existing Workflow Management (which already always requires an explicit grant, even for SystemAdmin).
4. **Authorization resolution.** The lifecycle service checks, **through the user-aware `IAccessControlService.AuthorizeAsync` with the policy version as `ResourceId`**, every lifecycle action *[amended - SR-07: the original text named `HasPermissionAsync`, which is roles-only: no SoD, scope, expiry or audit row]*, and (as `WorkRequestService` does) expands the actor's roles with `Permission:PackagingPolicy.Approve` before calling `IWorkflowRuntime.ExecuteActionAsync`.
5. **Workflow binding.** Key `(Module = "Policy", EntityType = "PolicyVersion", Context = <PolicyType>)` — generic for all domains; one definition per policy type, resolved by the existing `WorkflowBindingService` (overlap ⇒ error).
6. **How E-5 enters the system.** *[Amended - depends on PR-5: the grant-administration capability does not exist today.]* E-5 is a *business* approval recorded in the sign-off baseline. After it, an **authorized administrator** (not the program, per the activation checklist) grants the `PackagingPolicy.*` permissions to the approved roles through the existing permission administration. Nothing in this design invents the E-5 mapping.
7. **How changes are audited.** Grant/mapping rows (`RoleModuleAction`, `RolePermissionGrant`, `RolePermission`) and workflow definition rows are `AuditLog`-audited automatically; policy activation records in its validation summary that the routing check passed at that moment.

## Mandatory safeguards (derived from repository findings)
- *[Amended - Safety Review W.1/W.2: two distinct "nobody" cases (empty participant string is auto-approved; non-empty but unheld stalls); an employee-only stage is also auto-skipped.]* **S-1 Approval routing check before Submit** (error, not warning): binding resolvable; every mandatory approval stage has a non-empty participant; the permission exists; **at least one active user holds a role with that permission** (via active `RoleAssignment`). Otherwise Submit is refused with `APPROVAL_ROUTING_MISSING` — because an unroutable stage would be **auto-skipped** by the runtime.
- *[Superseded by S-2' in the Safety Review: non-null employee id, both identities compared, user-aware authorization recorded, no AutoSkip anywhere.]* **S-2 Approval evidence is a human action, not a completed workflow.** `PendingApproval → Approved` requires history containing an `Approve` action by a `UserId` (not `AutoSkip`) that is **different from the submitter/author**. A completed instance with an `AutoSkip` on an approval stage is rejected as approval evidence.
- *[Amended - PR-4: the break-glass rule is hard-coded at two sites and must become one shared predicate; SystemAdmin otherwise bypasses every non-break-glass permission.]* **S-3 Break-glass.** Add `PackagingPolicy` (Approve/Activate/Retire) to the existing SystemAdmin no-bypass list (the list currently names `ExecutionGate.EmergencyOverride` and `WorkflowManagement`) so an administrator cannot approve their own policy by role bypass. *This is a code change to `AccessControlService` — a 3D item, not done here.*
- **S-4 SoD.** Proposed `SoDPolicy` rows (data): `PackagingPolicy.Edit ↔ Approve` and `PackagingPolicy.Submit ↔ Approve` on the same record, using the existing same-resource SoD check. Confirmation is open question OQ-6.
- **S-5 Role-name fragility.** `RoleModuleAction.RoleName` is a string; a renamed role silently loses the mapping. Activation validation checks each granted role still exists, and the routing check (S-1) runs again at every Submit.

## Not done / not invented
No E-5 role mapping is proposed or assumed; roles named here (`QAManager`, `PackagingQualityManager`) illustrate the mechanism only.

---

# TI-4 Scope Precedence

## What PEMS actually has (evidence)
| Candidate level | Exists? | Evidence | Scope kind? |
|---|---|---|---|
| Deployment | yes (one company = one deployment, M-1) | — | **Yes** |
| Organization / company | no entity | `TenantId` unused | **No** (equals deployment) |
| Plant / site | no entity; `FunctionalLocation.Plant` never populated (0 distinct values) | live query | **No** |
| FunctionalLocation | yes; 32 rows, 28 roots, depth ≤ 2, `Area` populated; `ParentId` chain | live query | **Yes** (area/unit-level policy variation) |
| ProductionWorkCenter | yes; required FL; 0 rows today | live query | **Yes** (lowest anchor) |
| Packaging line | **no entity** | none | **No** — a packaging line would be a work center (OQ-1); no new hierarchy is created |
| Department / Warehouse / Project | exist but are flat or a different concept | live counts | **No** |

The earlier "Deployment → FunctionalLocation → WorkCenter" order is **confirmed**, but not automatically: the data show FunctionalLocation is a shallow area list, so in practice most policies will sit at Deployment, with FL/WC overrides only where the business asks for them.

## Scope context (input of a resolution)
`ScopeContext { ProductionWorkCenterId?, FunctionalLocationId? }`. Given a work center, its `FunctionalLocationId` and that node's ancestors form the chain. Given only a location, its ancestors form the chain. Given neither, only the Deployment level applies. (Assumption that Packaging records carry a work center like `ProductionRun` does: OQ-1.)

## Precedence and rules
Most specific wins; **whole-policy replacement, never field-level merging** with a parent.

Chain, most → least specific: `ProductionWorkCenter` → `FunctionalLocation` (nearest ancestor first, then upward, cycle-guarded and bounded) → `Deployment`.

| # | Rule |
|---|---|
| R1 | A **candidate** is a `PolicyVersion` with status `Active` whose window contains the effective date: `EffectiveFrom ≤ d` and (`EffectiveTo` null or `d < EffectiveTo`). |
| R2 | Walk the chain from most to least specific; the **first level that has a candidate wins**. |
| R3 | At one scope there is **one definition per policy type** (unique index). Activation **rejects** any version whose window overlaps another Active version of the same definition, except that activating a successor closes the predecessor's window (`EffectiveTo = successor.EffectiveFrom`) in the same transaction. |
| R4 | If more than one candidate is nevertheless found at one level (data corruption), the resolver throws a **`PolicyAmbiguous`** error naming the versions. Never first-match, never last-write-wins — the same discipline as `WorkflowBindingAmbiguousException`. |
| R5 | **Parent exists, child has none** → the parent's candidate applies (nearest ancestor with a candidate). |
| R6 | **Child has a definition but no candidate at `d`** (only drafts, blocked, retired, expired, or not yet started) → that level contributes nothing and the walk continues upward. To keep this from being a *silent* change, the resolution trace states it, and Tier-1 approval of a retirement must show a **resolution impact preview** (which scopes will resolve to a different policy afterwards). |
| R7 | **Future policy** (`EffectiveFrom > d`) is not a candidate; the trace lists it as "scheduled". |
| R8 | **No candidate at any level** → outcome `NoPolicyInForce` ⇒ Packaging capability unavailable (see Failure Modes). There is no default. |
| R9 | **Retired, Superseded, Rejected, Blocked, Draft, PendingApproval, Approved-not-activated** versions are never candidates. |
| R10 | **No backdating:** activation requires `EffectiveFrom` ≥ the activation time (date granularity per OQ-2). Consequently the answer for any past date never changes after the fact. |

The resolver is a pure function of `(policy type, scope context, effective date, stored versions)` — deterministic, side-effect-free.

## Overlap and "effective dates overlap" cases
| Situation | Behaviour |
|---|---|
| Two versions of one definition overlap | Rejected at activation (R3); if found at resolve time → `PolicyAmbiguous` (R4) |
| Two definitions at *different* scopes overlap in time | Legitimate: nearest scope wins (R2) |
| Two definitions at the *same* scope | Impossible: unique index (R3) |
| Successor with a later `EffectiveFrom` than predecessor's open end | Predecessor's `EffectiveTo` is set to the successor's `EffectiveFrom`; there is no gap and no overlap |
| Retire without successor | Optional `EffectiveTo`; afterwards the level contributes nothing (R6, R8) |

---

# Logical Data Model

Names are logical; **nothing is created.** All entities derive from `BaseEntity` (`CreatedBy`, `ModifiedBy`, `RowVersion`); `TenantId` stays unused.

## `PolicyDefinition` (infrastructure)
| Field | Note |
|---|---|
| `PolicyDefinitionId` (PK, int) | surrogate, follows PEMS habit |
| `PolicyType` (text code) | from a registry of policy types (`Packaging` first) |
| `ScopeKind` (text code) | `Deployment` \| `FunctionalLocation` \| `ProductionWorkCenter` (registry; adding a kind is additive) |
| `FunctionalLocationId?`, `ProductionWorkCenterId?` (FKs, `Restrict`) | real foreign keys rather than a polymorphic id; exactly the one matching `ScopeKind` is set, both null for Deployment |
| `Name` / `NameFa` (optional) | display |
| `IsActive` | soft archive, PEMS style; **no hard delete** |
| **Natural identity / unique index** | (`PolicyType`, `ScopeKind`, `FunctionalLocationId`, `ProductionWorkCenterId`) — SQL Server treats NULLs as equal in a unique index, giving exactly one Deployment definition per type |
| Immutability | `PolicyType`, `ScopeKind` and the scope target never change after creation |

## `PolicyVersion` (infrastructure)
| Field | Note |
|---|---|
| `PolicyVersionId` (PK) | |
| `PolicyDefinitionId` (FK, `Restrict`) | 1 definition → many versions |
| `VersionNumber` | int, assigned `max+1` per definition, **unique with the definition** (pattern: `WorkflowVersion`); never reused |
| `Status` | text code: `Draft` · `PendingApproval` · `Approved` · `Active` · `Superseded` · `Retired` · `Blocked` · `Rejected` (*`Rejected` is a proposed addition to the requested list — OQ-8*) |
| `EffectiveFrom`, `EffectiveTo?` | window (see TI-4); `EffectiveFrom` required from `Approved` on |
| `ChangeSummary` | mandatory business reason for the version (the `AuditLog` has no reason field) |
| `ClonedFromVersionId?` | provenance (pattern: `WorkflowVersion`) |
| Lifecycle stamps | `SubmittedOn/ById`, `ApprovedOn/ById` (the human approver read from workflow history, S-2), `ValidatedOn/ById` + `ValidationSummary`, `ActivatedOn/ById`, `RetiredOn/ById` + `RetirementReason` |
| Approval link | **no column**: the workflow instance is found by (`Module="Policy"`, `EntityType="PolicyVersion"`, `EntityId = PolicyVersionId`); at most one instance per version (resubmission = new version), otherwise an inconsistency error as `WorkRequestService.CurrentInstanceAsync` does |
| `RowVersion` | optimistic concurrency (from `BaseEntity`) |
| Constraints | one open (`Draft`/`PendingApproval`) version per definition at a time |

## `PackagingPolicyValues` (domain-owned; not infrastructure)
Primary key = `PolicyVersionId` (shared-key 1:1). One typed text-code column per Packaging decision dimension that the sign-off baseline defines: D1 package-definition mode, D2 mixed-product mode, D3 multi-batch mode, D4 weighing mode, D5 split / merge / repack (each `Disabled`/`Enabled`; only `Disabled` valid in release 1), D6 product-identity mode, D7 quality-release mode, E-4 permission model, D12 financial-treatment mode *[amended - SR-15/PR-10: D12 gates Phase 3C and has a different owner; a separate `PackagingFinancial` policy type is recommended, decision pending]*. **E-5 is not a column** (TI-3: role mapping is grant data). Created with the version; immutable with it. The exact column set is fixed only after the business approvals define which dimensions have material meaning (no speculative columns).

## Transactional references (added in later phases)
*[Amended - SR-18/PR-13: both `PackagingRun` and `Package` carry it; children inherit; a separate financial policy version is recorded on the output at posting.]* Root Packaging aggregates carry `PackagingPolicyVersionId` (FK to `PolicyVersion`, `Restrict`); the domain validates that the referenced definition's type is `Packaging`. Child records read the root's version.

## Deliberately absent
`PolicyScope`, `PolicyApproval`, `PolicyAuditReference` tables · JSON/blob value columns · per-field inheritance · a "default policy" row · `TenantId` usage.

---

# Policy Resolution Model

## Contract (logical)
```
Resolve(PolicyType, ScopeContext, EffectiveDate) → PolicyResolution
  Outcome           Resolved | NoPolicyInForce | Ambiguous
  DefinitionId, PolicyVersionId, VersionNumber
  ScopeKind + scope target actually used
  EffectiveFrom, EffectiveTo
  Status, ApprovalState (approver, approved-on, activated-on)
  Trace[]           ordered, human-readable, code-tagged explanation
GetByVersion(PolicyVersionId) → the immutable version (historical access; no scope/date logic)
```
The **domain** wraps this: `ResolvedPackagingPolicy = PolicyResolution + typed PackagingPolicyValues + capability Readiness`. The infrastructure returns no values; it does not know what they mean (TI-1 boundary).

## Explanation example
```
Resolved Packaging Policy v4   (Definition: Packaging @ ProductionWorkCenter WC-01)
Effective date: 2028-03-10
  [1] Scope ProductionWorkCenter WC-01 : Active v4, window 2028-01-01 → open      ✔ candidate → WINS
  [2] Scope FunctionalLocation 80A04    : Active v2, window 2027-06-01 → open      overridden by [1]
  [3] Scope Deployment                  : Active v7, window 2027-01-01 → open      overridden by [1]
Reason: WC-01 has an in-force policy, so it overrides the FunctionalLocation and deployment-wide policies.
Approved by <user> on 2027-12-15; activated 2027-12-20.
```
Example of a fall-through: `[1] WC-01: definition exists, no version in force at 2028-03-10 (v3 retired 2028-02-01) → continues upward` plus the scheduled/expired listing (R6, R7).

## Caching (design constraints)
Version *content* is immutable after submission, so content may be cached by `PolicyVersionId`. *Which* version is in force changes with time and lifecycle events, so scope-chain results may be cached only with deterministic invalidation on activation/retirement and a bounded lifetime; cache identity includes the version id. Multi-instance web deployment is not verified in the repository; staleness tolerance is open question OQ-4. No distributed cache is introduced.

---

# Historical Policy Resolution

**Mandatory principle:** a historical record is never reinterpreted with today's policy.

| Strategy | Assessment |
|---|---|
| **A. Store `PackagingPolicyVersionId` on the transactional root** | Exact; survives any later supersession, retirement or clock issue; one indexed FK; lets reports group by policy version; correct even if resolution rules evolve. Cost: an additional FK per root aggregate |
| **B. Re-resolve by (scope, historical date)** | No extra column, but depends on scope data that can change (a work center re-parented to another `FunctionalLocation`), on clock/timezone conventions (OQ-2) and on the resolver's rules staying identical forever. Not safe for immutable business/quality/inventory records |

## Decision (ACCEPTED, design)
- **A is the rule** for immutable business transactions: the root aggregate (e.g. packaging run; standalone package if any) records the version **at creation**, resolved once via B's algorithm at that instant; child records inherit the root's version.
- **B is used only** at the moment of creation, and for previews/what-if (e.g. "which policy would apply to WC-01 next month").
- A record bound to a version keeps that interpretation forever; policy changes create a **new version**, never an in-place edit (Lifecycle).
- Safety invariants (ledger integrity, Released-only posting, idempotency) do **not** depend on the bound version.
- **Open:** what an in-flight aggregate may do after its version is superseded or retired (OQ-3). Proposal, not decision: new starts use the in-force version; in-flight aggregates finish under their bound version unless the business/quality owner rules otherwise.

---

# Policy Lifecycle

```
Draft ──Submit──▶ PendingApproval ──human Approve (S-2)──▶ Approved ──Activate (validation OK)──▶ Active
  │                    │                                      │                                     │  ▲ successor activated
  │ delete (Draft only)│ reject/return                        │ validation fails                    │  └─▶ Superseded (terminal)
  ▼                    ▼                                      ▼                                     └────▶ Retired (terminal)
(removed; audited)   Rejected (terminal)                    Blocked ──fix dependency, re-validate──▶ Active
                                                              └───abandon──▶ Retired
```
| State | Meaning | Content editable? | Who / permission |
|---|---|---|---|
| Draft | Being authored (new or cloned) | **Yes** | `Edit` |
| PendingApproval | Submitted; workflow running | **No** | `Submit` |
| Approved | Human approval recorded; not yet in force | No | workflow `Approve` |
| Active | Activated; in force within its window | **No** | `Activate` |
| Superseded | Replaced by a later Active version | No | automatic with successor activation |
| Retired | Ended by decision or abandoned | No | `Retire` (Tier-1 governed) |
| Blocked | Failed activation validation (dependency/capability missing); **also from `PendingApproval` when approval evidence is invalid (Safety Review W.2)** | No | automatic; correct the dependency and re-activate, or retire |
| Rejected | Approval refused | No | workflow `Reject` |

**Immutability tiers.** (i) *Content* — values, scope, version number, `EffectiveFrom`, `ChangeSummary` — is frozen from `PendingApproval` onward. (ii) *Lifecycle fields* — `Status`, `EffectiveTo`, stamps — change only through the transitions above, each audited. **An Active version's values are never edited; a business change is a new version** (clone → Draft → the whole path). Only `Draft` may be deleted; every other version is permanent.

**Blocked after activation.** A dependency lost *after* activation (a role mapping removed, a workflow deactivated) does not mutate stored status. The resolver reports a **runtime readiness** of `Blocked` with reasons, and the capability is unavailable until repaired — no batch job flips statuses.

**Reused conventions.** Status strings as a static const class (`WorkflowVersionStatus` idiom, but registry-typed per TI-2); clone-to-draft (`ClonedFromVersionId`); validate-then-publish (`ValidatedDate`); retirement date. Enforcement: service-level guard on every write, `RowVersion` concurrency, unit tests per transition, `AuditLog`. **Correction (Safety Review SR-19):** PEMS *does* enforce immutability at `SaveChanges` - `WorkflowVersionImmutabilityGuard` is called from `PEMSDbContext.SaveChangesAsync`. A `PolicyVersionImmutabilityGuard` in the same place is required (PR-7). Remaining limit: a guard cannot verify workflow evidence (the service does; PR-15 detects violations), and it does not stop database-level administrators.

---

# Validation

Three stages, always in this order, all recorded in `ValidationSummary` at activation:

| Stage | Owner | Checks (examples) |
|---|---|---|
| **Structural** | infrastructure + domain | scope target exists and is active; dates valid; no backdating (R10); no overlap (R3); one definition per scope; every value present and a registered code (no `Unrecognized`); version content present |
| **Business** | **domain** | permitted combinations of D1/D2/D3/D4/D6/D7/E-4; D5 all `Disabled` in release 1; D12 compatible with any inventory-posting mode; D7 authority resolvable to an existing permission |
| **Technical** | **domain** (+ infra routing check) | each selected mode is registry-status `Implemented` and passes capability readiness (Implemented+Tested+Authorized+Production-Ready, ADR M-4); required permissions exist and are granted to at least one existing role (E-5 executed); approval workflow binding present with non-empty participants (S-1) |

Result: any **Error** ⇒ `Blocked` with issue codes (e.g. `STRUCT_VALUE_UNRECOGNIZED`, `BIZ_COMBINATION_UNSUPPORTED`, `TECH_CAPABILITY_NOT_READY`, `TECH_ROLE_MAPPING_MISSING`, `TECH_WORKFLOW_MISSING`, `TECH_APPROVAL_ROUTING_MISSING`); warnings never activate anything. Precedent for issue shape: `WorkflowVersionValidator` (code, severity, message) — the *shape* is reused; the infrastructure does not depend on WorkflowPlatform types. The validator contract is per policy type; the infrastructure only orchestrates and records.

---

# Security

| Topic | Design |
|---|---|
| Permissions | Resource `PackagingPolicy`; actions `View`, `Edit`, `Submit`, `Approve`, `Activate`, `Retire` (TI-3). Lifecycle actions are checked through the **user-aware `AuthorizeAsync`** (SR-07; `HasPermissionAsync` is roles-only and only suitable for visibility checks); no role literal anywhere |
| Author ≠ approver | S-2 (recorded human `Approve` by a user ≠ submitter) **and** proposed `SoDPolicy` rows (S-4) |
| Administrator bypass | S-3: add `PackagingPolicy` approve/activate/retire to the break-glass no-bypass list (3D code item) |
| ABAC scope | Existing `RoleAssignment.ScopeType/ScopeValue` could confine a role to an area; only `Global` is in use and no policy-specific scoping is required now |
| Resolver | Internal service call; no user permission needed to *resolve*; `View` needed to *see* policies in a UI |
| Workflow admin | Changing the approval workflow already needs an explicit grant even for SystemAdmin (`WorkflowManagement`); a tampered approval definition is therefore also a governed act |
| Data sensitivity | Policy values are configuration, not personal or secret |
| Empty-participant hazard | Guarded by S-1/S-2 (see TI-3) |

---

# Audit

- `AuditLog` records every insert/update/delete of `PolicyDefinition`, `PolicyVersion`, `PackagingPolicyValues` and of the grant/workflow data automatically — **no new audit table**.
- Approval evidence: immutable `WorkflowHistory` (action, `UserId`, role, timestamp).
- Business reason: `ChangeSummary` and `RetirementReason` on the version (the audit log has no reason field).
- Which policy governed a transaction: the `PackagingPolicyVersionId` on the root aggregate.
- Denied/allowed authorization decisions: existing `AuthorizationAuditLog`.
- Gaps recorded: `ChangedFieldsSummary` is flat text (adequate); there is no row-level "before/after" JSON; AuditLog is already the busiest table (~205k rows at last measurement), which is a volume consideration, not a blocker.

---

# Failure Modes

**Rule:** absence, invalidity or unreadiness of policy **never** falls back to a developer recommendation. Operational Packaging behaviour requires a valid in-force policy; there is **no approved PEMS default policy**, so none exists.

| Situation | Detected | Result |
|---|---|---|
| No policy exists / none in force | resolver (R8) | `NoPolicyInForce` → Packaging capability **unavailable — configuration required**; no record can be created |
| Policy invalid | activation validation | version cannot become Active (`Blocked`); an unrecognized stored code is `Unrecognized` and blocks |
| Policy expired | resolver (R1/R6) | not a candidate; walk continues upward; if nothing remains → unavailable |
| Policy not approved (Draft/Pending/Approved-not-active/Rejected) | resolver (R9) | not a candidate |
| Policy `Blocked` | activation | not a candidate; reasons listed |
| Unsupported capability | activation (technical) + runtime readiness | `Blocked` (`TECH_CAPABILITY_NOT_READY`); operation refused with a clear message; the mode is not operational merely because a value exists |
| Required role mapping missing | activation + Submit routing check | `Blocked` / Submit refused (`TECH_ROLE_MAPPING_MISSING`, `TECH_APPROVAL_ROUTING_MISSING`) |
| Required workflow missing | Submit pre-check | Submit refused (`TECH_WORKFLOW_MISSING`) — never a raw runtime exception, never a silent skip |
| Required implementation missing | activation (registry status ≠ `Implemented`) | `Blocked` |
| Ambiguous data (two candidates / two workflow instances) | resolver / lifecycle | hard error naming the records; nothing is chosen |
| Dependency lost after activation | runtime readiness | capability unavailable until repaired; stored status unchanged |
| Approval stage unroutable | S-1 | Submit refused (prevents runtime auto-skip approval) |

---

# Reuse Boundaries

| Concern | Reused (must not be duplicated) | New (infrastructure) | Domain-owned (Packaging) |
|---|---|---|---|
| Audit | `AuditLog` | — | — |
| Approval | `IWorkflowRuntime`, `WorkflowBinding`, workflow definitions, `Permission:` participants | binding key `("Policy","PolicyVersion",<type>)` | approval workflow definition content (data) |
| Authorization | `HasPermissionAsync`/`IAccessControlService`, permission tables, `SoDPolicy`, `RoleAssignment` scope | — | the `PackagingPolicy` action set |
| Roles | role data; E-5 grants | — | none (no role names) |
| Hierarchy | `FunctionalLocation`, `ProductionWorkCenter` | scope kinds referring to them | which scope a Packaging record carries |
| Versioning pattern | `WorkflowVersion` shape | `PolicyVersion` | — |
| Resolution pattern | `WorkflowBindingService` (overlap rejection, ambiguity error) | `IPolicyResolver` | — |
| Concurrency/audit columns | `BaseEntity` | — | — |
| Localization | resx + `T(fa,en)` | — | mode display keys |
| **Infrastructure owns** | | identity · type · scope · version · status · dates · approval binding · validation boundary · activation/supersession · audit hook | |
| **Domain owns** | | | policy fields · semantics · validators · business rules · invariants · capability readiness |
| **Not reused** | `WorkflowVersion` tables, `ContractApprovalAuthorityRule` and other rule tables (unversioned), `appsettings`/`IOptions`, `TenantId` | | |

---

# Future Multi-Tenancy Impact

Not built, not planned (ADR M-1). Effect if the platform later adopts tenancy: add a `Tenant` scope kind at the top of the chain (additive registry entry); make the `PolicyDefinition` unique index and the resolver tenant-aware; make the workflow binding and permission checks tenant-aware (platform work); backfill existing rows to a default tenant. **Unaffected:** `PolicyVersion` content, historical `PackagingPolicyVersionId` references, registries, validators, lifecycle. The two-table design and text-coded scope kinds were chosen so this stays additive.

---

# Packaging Integration Boundary

| Packaging supplies | Packaging consumes | Packaging must never |
|---|---|---|
| policy-type registration (`Packaging`), mode registries, `PackagingPolicyValues`, validators (structural/business/technical), capability-readiness registry, values loader, `ResolvedPackagingPolicy` provider, permission module `PackagingPolicy`, D7 authority mapping (mode → **required permission**, never a role) | `IPolicyResolver`, `IPolicyLifecycleService`, workflow runtime/binding, `HasPermissionAsync`, `AuditLog` | branch on company/site/role name · read policy other than through the provider · write policy tables directly · bypass the lifecycle · treat a configured value as an operational capability |

Services resolve the policy **once at aggregate creation**, persist `PackagingPolicyVersionId`, and thereafter read the version by id. Inventory, barcode, quality-gate and audit invariants (freeze §5–§8) are not policy-dependent.

---

# Phase 3A Implications

Step **3A-0** (blocked with the rest of Phase 3, ADR M-2) becomes:
1. Additive migration for `PolicyDefinition`, `PolicyVersion`, `PackagingPolicyValues` only (FKs `Restrict`; unique indexes as above); no seed of any policy row.
2. Registries and value type (TI-2), lifecycle service, resolver, validator orchestration, capability-readiness registry.
3. `PackagingPolicy` permission module registration **without granting any role** (grants are E-5, an administrator's act); policy-approval workflow *not seeded* (its stages are administrator data).
4. Tests before phase exit: every lifecycle transition and forbidden transition; immutability guards; resolver matrix (R1–R10: parent/child, overlap, future, retired fall-through, no policy, ambiguous data); `Unrecognized` handling; policy-integrity check; S-1/S-2 (auto-skip cannot approve; author ≠ approver); architecture test (no Packaging reference from `Policy` namespace); InMemory tests must seed parent rows (known EF InMemory required-FK quirk); regression of the Production quality gate and existing barcode types.
5. Not in 3A-0: any Packaging aggregate, UI beyond what a later phase specifies, code change to `AccessControlService` (S-3, phase 3D).

## Gate 15 evidence status
Checklist gate 15 (as written) asks for: the guide part of the freeze ✔ · M-1…M-5 recorded ✔ · TI-1…TI-4 settled in a pre-implementation design record ✔ (this document) · that design audited against the configurability audit ✔ (audit §9). **The checklist itself names no owner signature for gate 15**, but both the ADR (§4) and this record carry an unsigned architecture-owner line, and TI-1…TI-4 are decisions proposed by this design exercise. Therefore:

> **TECHNICAL EVIDENCE COMPLETE**
> **ARCHITECTURE OWNER APPROVAL REQUIRED**

Gate 15 is **not checked**. The checklist now records this explicitly.

---

# Open Questions

| ID | Question | Kind | Blocks |
|---|---|---|---|
| OQ-1 | Is a packaging line a `ProductionWorkCenter`, and does every Packaging record carry one (as `ProductionRun` optionally does)? | business + architecture (with D1) | scope context of Packaging aggregates |
| OQ-2 | Clock convention: PEMS uses local `DateTime.Now`; is `EffectiveFrom` a date at local midnight, and how are DST changes handled? | architecture | resolver and R10 details |
| OQ-3 | What may an in-flight aggregate do after its bound version is superseded/retired (proposal in Historical Resolution)? | business (quality/operations owner) | in-flight rules |
| OQ-4 | Acceptable staleness of a cached scope resolution; is the web tier multi-instance? | architecture / operations | caching design |
| OQ-5 | Policy administration UI: part of 3A-0 or a later phase? | program | UI scope |
| OQ-6 | Confirm the proposed SoD pairs (Edit↔Approve, Submit↔Approve) | architecture owner + security | S-4 |
| OQ-7 | Name of the architecture owner who signs §21 | management | Gate 15 |
| OQ-8 | Accept the extra `Rejected` state beyond the requested list? | architecture owner | lifecycle table |
| OQ-9 | Confirm `RoleModuleAction` (vs legacy `RolePermission` flags) as the permission mechanism for the `PackagingPolicy` actions | 3A design freeze | TI-3 implementation |
| OQ-11 | When a scoped (child) policy is retired with no successor, should the scope revert to the parent policy or stop? (design default: revert, with recorded impact acknowledgement - SR-23) | business | PR-16 |
| OQ-12 | Who verifies the mandatory `BusinessApprovalReference` on each policy version (the holder of `PackagingPolicy.Approve`, tied to E-5)? | business | PR-11 |
| OQ-13 | Is the grant-administration capability (PR-5) a platform prerequisite or delivered inside the Packaging program? | architecture owner | E-5 execution |
| OQ-10 | Does the business want policy variation below deployment level (FunctionalLocation/work center) at all? Default until told: deployment-wide policy only, lower scopes supported by the resolver but unused | business | nothing (non-blocking) |

**Outside this program, observed only:** 10 published workflow stages in the live database have an empty `ResponsibleRole` (e.g. MOC `RiskAssessment` and `SeniorApproval`, TechnicalQuery stages). Many are dynamic-participant stages by design, but because the runtime auto-skips an unassigned stage, they merit a separate review by the workflow owners. **Nothing was changed or analyzed further.**

---

# Architecture Decision Summary

| ID | Decision | Kind | Status |
|---|---|---|---|
| TI-1 | Two infrastructure tables (`PolicyDefinition`, `PolicyVersion`) + domain-owned `PackagingPolicyValues`; namespace boundary with architecture test; no scope/approval/audit tables; no rule/expression engine | Architecture | **ACCEPTED (design)** — architecture-owner name pending |
| TI-2 | Registry-backed value type persisted as stable text code with explicit converter; `Unrecognized` explicit value; no enum, no ordinals, no fallback, no CHECK list | Architecture | **ACCEPTED (design)** — pending |
| TI-3 | Permission-participant pattern; responsibility = permission `PackagingPolicy.*`; roles are grant data; safeguards S-1…S-5; no new RBAC/mapping table | Architecture | **ACCEPTED (design)** — pending |
| TI-4 | Scope kinds Deployment → FunctionalLocation → ProductionWorkCenter; nearest wins; whole-policy replacement; deterministic rules R1–R10; ambiguity is an error | Architecture | **ACCEPTED (design)** — pending |
| Historical | Version id on root aggregate (A), re-resolution only at creation/preview (B) | Architecture | **ACCEPTED (design)** — pending |
| Lifecycle | Draft→Pending→Approved→Active→Superseded/Retired; Blocked; Rejected (proposed); content immutable from submission | Architecture | **ACCEPTED (design)** — pending |
| Business D1–D7, E-4, E-5, D12 | unchanged | **Business** | **OPEN — 0 / 16 approved** |
| Gate 15 | technical evidence complete | Gate | **NOT CHECKED — architecture owner approval required** |

Nothing in this record activates Phase 3A. Classification: **C — IMPLEMENTATION BLOCKED.**

---

# Safety Review Amendments (2026-09-25)

Source: `docs/audits/PEMS_OPERATIONS_PACKAGING_FINAL_ARCHITECTURE_SAFETY_REVIEW.md`. Each amendment supersedes any conflicting statement above.

| ID | Amendment | Replaces / adds | PR |
|---|---|---|---|
| A-1 | **Two "nobody" cases.** An empty participant string (or an all-blank explicit list) is auto-approved by the runtime; a non-empty stage nobody can act on stalls. A stage naming only `ResponsibleEmployeeId` counts as empty and is auto-skipped. The Packaging boundary - not the platform - guards this: the policy service alone starts policy workflows, never passes explicit participant lists, inspects the bound workflow version before Submit, and treats a completed instance as insufficient evidence | S-1, S-2 | PR-1 |
| A-2 | **Approval evidence S-2'.** Human `Approve` on a stage whose participant is the approve permission; actor employee id non-null and different from the initiator; application user different from the submitting user; user-aware authorization recorded; no `AutoSkip` anywhere; `ApprovedOn/ById` read from history. Failure: `PendingApproval -> Blocked` | S-2, lifecycle | PR-1, PR-2 |
| A-3 | **Identity.** Submit and Approve are refused for a user with no Employee record (the runtime's `ForbidInitiator` is skipped for a null actor; live user `admin` is a SystemAdmin with no Employee). Record and compare both user id and employee id (the two are not enforced 1:1) | `PolicyVersion` stamps | PR-2 |
| A-4 | **Authorization call.** Lifecycle actions use user-aware `AuthorizeAsync(ResourceId = PolicyVersionId)`; roles are derived server-side; the approving transition carries `RequiredPermission` and `ForbidInitiator` as data and is verified at Submit | Security, TI-3 item 4 | PR-3 |
| A-5 | **Break-glass.** One shared predicate; `PackagingPolicy.{Submit,Approve,Activate,Retire}` need explicit grants even for SystemAdmin; `Edit`/`View` may bypass | S-3 | PR-4 |
| A-6 | **Grant administration prerequisite.** Grants for new actions cannot be created or revoked at runtime today (Permission Matrix = 10 legacy flags; `RoleModuleAction` has no write API; the legacy-to-grant copy is startup-only and additive-only). A minimal audited grant/revoke on `RolePermissionGrants` (no self-grant of `PackagingPolicy.*`) is required before E-5 can be executed | TI-3 items 2, 6 | PR-5 |
| A-7 | **SoD rows** are seeder data: `Edit<->Approve`, `Submit<->Approve`, recommended `Approve<->Activate`; SoD is evaluated only on the user-aware path | S-4 | PR-6 |
| A-8 | **Immutability guard** at `SaveChanges` (precedent exists) | Lifecycle limit statement | PR-7 |
| A-9 | **Resolver hardening.** Concurrency token on `PolicyDefinition` for activation (a unique index does not prevent overlapping windows); active scope target required; bounded, cycle-guarded chain walk | TI-4 rules R3, R6 | PR-8 |
| A-10 | **Values evolution.** `ValuesSchemaVersion`; rules ER-1..ER-6; JSON, EAV and typed-value-records rejected; scope override is the only site-specific mechanism | `PackagingPolicyValues` | PR-9 |
| A-11 | **D12** is not part of the values that gate Phase 3A: separate `PackagingFinancial` policy type (recommended) or a deferrable dimension - decision pending | `PackagingPolicyValues` | PR-10 |
| A-12 | **Business evidence.** Mandatory `BusinessApprovalReference` per version (human-verified, not machine-verifiable); no seeded policy; no `IsDefault` | Logical model | PR-11 |
| A-13 | **Registry** is code-based with an append-only manifest and a guard test; labels in resx | TI-2 | PR-12 |
| A-14 | **History references:** `PackagingRun` and `Package` carry the version id; child records inherit | Transactional references | PR-13 |
| A-15 | **Capability gate HI-1:** registry `ProductionReady` + `RequireCapability` at every operation entry point; CI ties capability to tests | Validation, Failure Modes | PR-14 |
| A-16 | **Audit:** mandatory reasons and stamps in the same transaction; audit is written in a second save (stamps are authoritative); detective integrity check | Audit | PR-15 |
| A-17 | **Retirement impact:** preview and recorded acknowledgement for child-retirement fall-through | TI-4 rule R6 | PR-16 |

# 21. Sign-off (architecture record)

| Role | Name | Date | Reference |
|---|---|---|---|
| Architecture owner | | | |
| Program owner | | | *(instruction of 2026-09-25 recorded; no name on file)* |

Blank by design. This signature does not affect the business gate (sign-off baseline).
