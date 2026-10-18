# PEMS Packaging — Final Architecture Safety Review

**Document ID:** PEMS-OPS-PKG-ARCH-SAFETY-REVIEW · **Date:** 2026-09-25 · **Version:** 1.0
**Scope:** design review of the Packaging Policy architecture (M-1…M-5, TI-1…TI-4) against **source and live-database evidence**, before Business Sign-off and before Phase 3A.
**Nature:** documentation and design review only. No code, schema, migration, seed, role, permission, workflow definition or test was changed; database access was read-only `SELECT`. Nothing was committed.

**Result: NOT SAFE TO IMPLEMENT AS DOCUMENTED — SAFE TO PROCEED TO OWNER REVIEW AFTER THE 16 REQUIRED CHANGES (PR-1…PR-16) ARE ACCEPTED INTO THE 3A DESIGN FREEZE.**
Packaging remains **C — IMPLEMENTATION BLOCKED** · business approvals **0 / 16** · activation gates **0 / 15**.

Classification legend: **A** accepted architecture · **B** documentation clarification · **C** required implementation change · **D** business decision required · **E** architecture-owner approval required.

---

# Executive Summary

The core architecture (two generic lifecycle tables + domain-owned typed values; permission-based approval; nearest-scope resolution; version id on history; no defaults) **holds**. The review, however, found that the *technical design record contains statements that the source contradicts*, and eight material gaps:

| # | Headline | Severity |
|---|---|---|
| 1 | The runtime **auto-approves** a stage whose participant list is empty — and a stage that names *only* `ResponsibleEmployeeId` counts as empty (the validator accepts it as a participant; the runtime ignores it). A policy approval workflow must be guarded at the Packaging boundary | **HIGH** |
| 2 | **A user with no Employee record bypasses the platform's initiator check** (`ForbidInitiator` is skipped when the actor id is null). Live: user `admin` (SystemAdmin) has no Employee | **HIGH** |
| 3 | `HasPermissionAsync` — the call the design named — is **roles-only: no Scope, no SoD, no expiry, no audit row**. SoD works only through the user-aware `AuthorizeAsync` with a `ResourceId` | **HIGH** (design wording wrong) |
| 4 | **There is no runtime way to grant or revoke a permission for a new action** (`Submit`, `Activate`, `Retire`): the Permission Matrix edits only ten legacy flags; `RoleModuleAction` has no write API; grant sync is startup-only and additive-only. The design's "an administrator grants it through the existing administration" is **not currently true** | **HIGH** (blocks E-5 execution) |
| 5 | SystemAdmin currently bypasses every non-break-glass permission (even undefined ones), can publish/bind workflows (explicit grants exist), and the break-glass list is **hard-coded in two code sites** | **HIGH** |
| 6 | The D12 dimension sits inside the policy that gates Phase 3A, but D12 gates only Phase 3C — a contradiction inside the design | MEDIUM |
| 7 | Business approval lives only in a markdown gate file; the runtime cannot see it, so a policy author could enter a technical recommendation and no control would notice | MEDIUM |
| 8 | Concurrent activations can create overlapping windows (unique index does not prevent it); inactive scope targets and child-retirement fall-through are unspecified | MEDIUM |

Two earlier claims are **corrected in the other direction** (the repo is better than documented): PEMS *does* have a `SaveChanges`-level immutability guard (`WorkflowVersionImmutabilityGuard`), and Package-level workflow SoD (`ForbidInitiator`, `RequiredPermission`) is available as runtime-enforced transition data.

---

# Workflow Approval Safety

## W.1 Exactly what `TryAutoSkipEmptyStageAsync` does (source-verified)
Called from `StartInstanceAsync` (first stage) and `ExecuteActionAsync` (each next stage).

| Question | Answer |
|---|---|
| When is a stage "empty"? | If a caller supplied an **explicit participant list**: when no element is non-blank. Otherwise: when `stage.ResponsibleRole` is null/whitespace |
| What is "participant resolution"? | `CreateInstanceStages` creates a pending `WorkflowInstanceStage` per role (Parallel) or one with the comma list (Sequential). **`ResponsibleEmployeeId` and `EscalationRole` are never consulted.** The `Permission:X` and `Discipline:X` prefixes are just strings in `ResponsibleRole` |
| Can empty become Approve? | **Yes.** It selects the first transition with `ActionType == "Approve"` from that stage, writes history `ActionType = "AutoSkip"`, moves on — or, if none leads onward, **sets the instance `Completed`** |
| Intentional? | Yes, for dynamic-participant workflows (the code comment: otherwise the instance hangs forever). It is correct for those modules |
| Two different "nobody" cases | (a) *Empty* participant string → **auto-approved**. (b) *Non-empty but nobody can act* (e.g. `Permission:X` with no holder) → **the instance stalls** (fail-closed but deadlocked). The technical design's S-1 wording ("unroutable stage would be auto-skipped") was true only for (a) — **B** |
| Employee-only stage | A stage with only `ResponsibleEmployeeId` set has no instance-stage row and is **auto-skipped**, although `WorkflowVersionValidator` treats that field as a valid participant. A latent inconsistency in the platform (outside this program) |
| Can Packaging Policy approval use this behaviour? | **No** — it would let a policy become approved with no human |

## W.2 Required invariant and where it is enforced
> A Packaging Policy approval MUST NOT become approved because no participant could be resolved.
> No approvable participant ⇒ **submission refused (or version `Blocked`)** — never auto-approved.

**It can be enforced at the Packaging policy boundary without changing global WorkflowPlatform behaviour.** Because `IWorkflowRuntime` is the only entry point and the *policy service* alone starts instances and decides `Approved`, the boundary is sufficient (classification **A**, with the guards below **C**). Required guards:

1. **Start only through the policy service**, with `firstStageParticipantRoles = null` and `nextStageParticipantRoles = null` always (explicit lists override `ResponsibleRole` and could smuggle in blank/other participants).
2. **Pre-submit inspection of the version the binding resolves** (published workflow versions are immutable — see H.5 — and instances are pinned to a version): every stage has a non-blank `ResponsibleRole` made **only** of `Permission:PackagingPolicy.Approve`-type participants; no stage relies on `ResponsibleEmployeeId`; the approving transition carries `ForbidInitiator = true` and `RequiredPermission`; exactly one start transition; the permission exists **and at least one active user holds it** (via `RolePermissionGrant`, see P.3). Otherwise Submit is refused: `TECH_APPROVAL_ROUTING_MISSING`.
3. **Approval evidence** (below, S-2′) — a *completed* instance is never evidence by itself.
4. If evidence fails after completion (e.g. an `AutoSkip` appears anywhere): the version goes `PendingApproval → Blocked` with a reason code and an integrity alarm; it may be retired but **never** advanced to `Approved`. (Amends the TD lifecycle: `Blocked` may now follow `PendingApproval`.)

**Minimum platform-level change:** *none required*. **Optional hardening for the platform owners** (classification **E**, outside this program): make `STAGE_NO_PARTICIPANT` an error for modules that are not dynamic-participant, and count `ResponsibleEmployeeId` in the emptiness test.

## S-2′ Approval evidence (replaces TD S-2)
A version may move `PendingApproval → Approved` only if the immutable `WorkflowHistory` of its single instance shows **all** of:
- an `Approve` action (not `AutoSkip`, not `Start`) on a stage whose `ResponsibleRole` contains the policy approve permission participant;
- performed by an actor with a **non-null employee id**, different from `instance.InitiatedById` **and** whose application user is different from the submitting user (see SD.2);
- that actor held the permission at that moment per the **user-aware** authorization decision (`AuthorizeAsync`, which writes an `AuthorizationAuditLog` row) — recorded, not assumed from the caller-supplied role list;
- no `AutoSkip` entry anywhere in the instance history.
`ApprovedOn/ById` are read from that history entry, never from the caller.

---

# Self-Approval / SoD

## SD.1 Platform mechanisms (source-verified)
| Mechanism | Behaviour | Limit |
|---|---|---|
| `WorkflowTransition.ForbidInitiator` | Runtime refuses when `actorEmployeeId == instance.InitiatedById` | **Only if `actorEmployeeId.HasValue`.** A null actor is not blocked |
| `WorkflowTransition.RequiredPermission` | Runtime calls the auth hook (fail-closed if no hook); hook = `AuthorizeForRolesAsync(context.ActorRoles, …)` | **Roles-only, caller-supplied role list; no SoD; no scope** |
| `SoDPolicy` rows (10 live) | Evaluated **only** in the user-aware `AuthorizeAsync` when a `ResourceId` is given: denies if the *same user* has a logged `Allow` on the paired permission for the *same resource* | Not evaluated by `HasPermissionAsync`/`AuthorizeForRolesAsync`; depends on `AuthorizationAuditLog` rows; the SystemAdmin bypass returns before SoD unless the resource is break-glass |
| Role matching in the runtime | `RoleMatches(assignedToRole, actorRoles)` on a **caller-supplied list** | The runtime is **not** an authorization boundary for `Permission:` participants — the calling module adds the virtual role after its own check (`WorkRequestService.ExpandRolesAsync`) |

## SD.2 Findings
- **SR-05 (C, HIGH) Null-employee bypass.** Live database: 3 users, 1 with no `EmployeeId` — user `admin`, a **SystemAdmin**. `AspNetUsers.EmployeeId` is not unique-constrained (no duplicates today). With a null actor, `ForbidInitiator` is skipped and history `UserId` is null: such a user could both start and approve. **Requirement:** the policy service refuses Submit and Approve when the actor's employee id is null; it records **both** application-user id and employee id on submit and approval and compares **both** (the two identity domains are not enforced 1:1).
- **SR-06 (C) Caller-supplied roles.** The policy service derives roles from the authenticated user server-side (never from a request), runs the user-aware authorization first, and only then adds the virtual `Permission:` role. The approving transition also carries `RequiredPermission` + `ForbidInitiator` as *runtime-enforced data* (defense in depth). Because that is workflow data an administrator can publish differently, the Submit-time inspection (W.2 item 2) verifies it.
- **SR-07 (B+C) `HasPermissionAsync` is the wrong call for lifecycle actions.** The TD's Security section named it. Lifecycle actions (`Edit`, `Submit`, `Approve`, `Activate`, `Retire`) must use **`IAccessControlService.AuthorizeAsync(AccessControlRequest{ UserId, ResourceName="PackagingPolicy", ActionName, ResourceId=<PolicyVersionId> })`**, which is the only path that evaluates scope, expiry and SoD and writes an audit row. `HasPermissionAsync` remains acceptable for read-only *visibility* checks.
- **SR-08 (C) SoD rows are data created by code seeders** (`AccessControlSeeder.SeedPhase5GovernanceAsync` at startup), not administrator UI. Required rows: `PackagingPolicy.Edit ↔ Approve`, `Submit ↔ Approve` on the same record, plus (recommended) `Approve ↔ Activate` so the approver is not also the activator (**E**: confirm the pairs, OQ-6). A test proves each denial.
- **Prohibitions (hard rules):** *Edit an Active policy* — impossible (H.5 guard). *Approve own submission* — blocked by S-2′, `ForbidInitiator`, SoD, and the null-identity rule.

---

# Break-Glass

## BG.1 Source facts
- `AccessControlService` has **two** near-identical code sites (`DecideAsync` and `DecideForUserAsync`) each hard-coding `isBreakGlassAction = (ExecutionGate.EmergencyOverride) || resource == "WorkflowManagement"`. Outside that set, **any user holding the `SystemAdmin` role is allowed for every permission** — including permissions that do not exist yet (bypass is evaluated *before* the permission lookup).
- Live: `SystemAdmin` holds explicit grants `WorkflowManagement.{View,Edit,Validate,Publish,Bind,Retire}`. The Permission Matrix is `[Authorize(Roles = SystemAdmin)]`.

## BG.2 What a SystemAdmin could do today (once `PackagingPolicy` exists), and with the required changes
| Action | Today's behaviour (if unchanged) | With required changes |
|---|---|---|
| Submit a policy | **Allowed** (bypass) | Requires an explicit `PackagingPolicy.Submit` grant (PR-4); needs a non-null employee id |
| Approve own policy | **Allowed** (bypass; SoD skipped) | Explicit grant needed; **and** S-2′ + `ForbidInitiator` + SoD deny the submitter |
| Bypass the approval workflow | Cannot act on a `Permission:` stage without the virtual role, which the service adds only after a real authorization — but can **publish a new approval workflow version** or **rebind** (`WorkflowManagement` grants exist) | Submit-time inspection (W.2 item 2) rejects any bound version that is not the required shape |
| Directly grant self `PackagingPolicy.Approve` | Not possible via UI today (no grant UI, item P.2); possible by direct DB/seeder | The new grant-administration path (PR-5) must **forbid granting `PackagingPolicy.*` to the acting user** and every grant is `AuditLog`-visible |
| Activate an unapproved policy | Only via the lifecycle service; direct EF/DB writes not blocked by anything today | `PolicyVersionImmutabilityGuard` (H.5) rejects illegal transitions; the service requires evidence S-2′ |

## BG.3 Required (implementation requirement, PR-4)
Add the `PackagingPolicy` lifecycle actions (`Submit`, `Approve`, `Activate`, `Retire`) to the break-glass rule **through a single shared predicate** used by both sites (removing the duplicated literal), so that **SystemAdmin never auto-satisfies Packaging Policy actions**. `Edit` (draft) and `View` may stay bypass-eligible.

## BG.4 Residual risk (classification **E** — architecture owner must accept or reject)
A person with SystemAdmin **and** database access, or the future grant-administration capability, can always subvert an in-application control. The design can only guarantee: no single-step or accidental self-approval; deliberate circumvention needs several distinct, individually audited acts by identifiable users; and a **detective control** (PR-15: an integrity check that every version ≥ `Approved` has valid human approval evidence, and that reports changes to `PackagingPolicy.*` grants). It does **not** claim tamper-proofing against database-level administrators.

---

# Permission Architecture

## P.1 Can `Permission:PackagingPolicy.Approve` be represented without a second authorization system? — **Yes (A).**
A row in `Permissions` (`ResourceName="PackagingPolicy"`, `ActionName="Approve"`) plus `RolePermissionGrant` rows (keyed by `RoleId`) is the enforcement source read by `AccessControlService`. A missing permission is denied (fail-closed) — except for SystemAdmin bypass (BG.1). The workflow validator already validates `Permission:` participants (`PERMISSION_UNKNOWN`); 40 live stages use the pattern.

## P.2 Source-verified problems with *how grants get created* (SR-12, C + E, HIGH)
| Mechanism | Table | Role reference | Who can change it at runtime | Reaches enforcement how |
|---|---|---|---|---|
| Legacy `RolePermission` (10 fixed flags) | `RolePermissions` | **role-name string** | Permission Matrix UI (SystemAdmin) via `SetPermissionsAsync` | Copied to `RolePermissionGrants` by `AccessControlSeeder.SeedFromLegacyAsync` **at application startup only**; the copy is **additive only** (`GrantIfTrue` returns when the flag is false) — **no revoke path exists** in code |
| `RoleModuleAction` (arbitrary actions) | `RoleModuleActions` | **role-name string** | **Nobody at runtime** — `IRoleModuleActionService` has only `HasActionAsync` and `SeedDefaultsAsync` (code seeds, e.g. Permit) | Same startup copy |
| `Permission` + `RolePermissionGrant` (enforcement) | `Permissions`, `RolePermissionGrants` | **`RoleId`** (stable) | **No UI or API** — only seeders (`AccessControlSeeder`, `WorkflowManagementSeeder`, `WorkOrderGovernanceSeeder`, `WorkRequestWorkflowSeeder`) add rows | Read directly |
| Workflow stage participants | `WorkflowStage.ResponsibleRole` / `EscalationRole` | **role-name strings** (comma list) | Workflow Management (explicit grant) | Runtime matching on supplied role names |

Live evidence: 625 active grants; **0** legacy flags set but ungranted (the startup copy works); **1** legacy flag false with an active grant (`MNT.DisciplineHead` → `WorkOrder.Hold`) — consistent with either a seeder-created grant or a non-propagated revoke; not investigated further. 8 grants have no legacy source (6 × `WorkflowManagement` for SystemAdmin, 2 × `Document.RecordClientResponse`).

**Consequences for Packaging Policy:**
1. **The five-flag Permission Matrix cannot express `Submit`/`Activate`/`Retire`,** and is not a place to implement policy authority.
2. **"An authorized administrator grants the permission through the existing administration" (TD TI-3 item 2/6) is not currently possible** for new actions, and *revocation* is not reliably effective for any grant. This makes E-5 (the business role mapping) **unexecutable** until a grant-administration capability exists. **PR-5 (C):** a minimal, generic, audited **grant / revoke on `RolePermissionGrants`** (immediate effect; forbids self-grant for `PackagingPolicy.*`; no new RBAC model). **E:** the architecture owner decides whether it is delivered as a platform prerequisite or inside the Packaging program. This does **not** redesign RBAC.
3. **Role-name hazards.** Legacy rows and workflow stage strings bind to role *names*: delete role X and create another named X, and both silently attach to the new role. Enforcement grants (by `RoleId`) do not. Therefore the minimum safe boundary is: **Packaging Policy uses only `Permission`+`RolePermissionGrant` via `AuthorizeAsync`, and workflow participants only as `Permission:` strings** — no role-name string anywhere in the Packaging design (the TD's "role names are grant data" statement is refined accordingly — **B**).
4. Correction of premise: there are **two** tables that reference roles by string among the permission tables (`RolePermissions`, `RoleModuleActions`), plus the workflow stage columns; the enforcement table uses `RoleId`.

## P.3 Effective-holder check
Activation and Submit validation must read **`RolePermissionGrants` + active `RoleAssignment`/`AspNetUserRoles`** (what enforcement reads), never the legacy tables, when asking "does at least one active user hold `PackagingPolicy.Approve`?".

---

# Policy Storage Evolution

## SE.1 Options
| Criterion | A. Fixed typed columns (one row per version) | B. Versioned typed value records (row per dimension) | C. JSON | D. EAV | E. Typed **section** tables / separate policy types |
|---|---|---|---|---|---|
| Type safety | DB column + registry converter | value column only | none in DB | none | per column |
| Validation | domain validator per known column | per-dimension rules in code | code only | code only | per section |
| Reporting | plain SQL group-by on a mode code | pivot | JSON functions | pivot | plain SQL |
| Migration safety | additive column | none needed (data) but schema-less | none needed | none needed | additive table |
| Historical interpretation | needs a schema version marker (below) | code must remember dimension semantics | serializer/version drift | dimension semantics live in code | per section |
| Domain ownership | strong | weak | weak | weak | strong |
| Performance | one row | n rows/version | parse cost | joins | 1..k rows |
| Maintainability | clear, some column growth | dispersed | opaque | opaque | clear |

## SE.2 Decision (classification **A + C**)
**A — fixed strongly typed columns — with a `ValuesSchemaVersion` and controlled-evolution rules; E as the *growth path*, applied by ownership; B, C, D rejected** (B is EAV with a registry: it loses per-column typing and referential clarity; C and D move validation out of the database and out of reach of reporting).

**Evolution rules (binding):**
- **ER-1 New dimension** = new **nullable** column + registry entry + `ValuesSchemaVersion` bump. Historical rows keep NULL and stay interpretable **under their own schema version**; the validator knows which dimensions each schema version requires (NULL in a dimension required by that version = `STRUCT_VALUE_MISSING`).
- **ER-2 New option** = a registry entry only; no schema change.
- **ER-3** A dimension/column/code is **never removed, renamed or repurposed**; retire by registry status.
- **ER-4** Every evolution is an additive migration plus a registry-manifest update plus tests.
- **ER-5 Split criterion.** Move a dimension group to its own section table — or to a **separate policy type** — when its **owner/approver differs, its change cadence differs, or its activation gate differs**; not because of column count.
- **ER-6 Site-specific configuration** is expressed only by **scope override** (whole-policy replacement). Consequence to accept (**A**): a child override restates all dimensions and can drift from later parent changes; mitigated by the retirement/change impact preview and a "clone from parent" authoring path.

## SE.3 D12 coupling defect (SR-15, C + E + D)
The TD placed D12 inside the values that must pass activation validation, yet D12 gates only Phase **3C** and has a different owner (Finance) and cadence. Either the Packaging policy cannot activate until D12 is decided (contradicting the freeze) or D12 would need a "not defined" placeholder mode (forbidden by TI-2). **Required decision (E, informed by D):** record D12 as a **separate policy type** (`PackagingFinancial`) with its own definition/version/approver — capabilities that need it (inventory posting) stay unavailable until it is in force — **or** allow D12 as an explicitly deferrable NULL dimension with capability-level requirement. Recommendation: the separate policy type (ER-5). If adopted, the record that posts inventory (the output record) carries the financial policy version id too.

## SE.4 Business evidence gap (SR-16, C + D)
Business approval exists only as rows in a **documentation gate file**; the running system cannot see it. A policy author could type the *technical recommendation* into a version and nothing in the system would flag it. **Required (PR-11):** each version carries a mandatory `BusinessApprovalReference` (sign-off record id / document reference) that the human policy approver must verify against the sign-off baseline; the field is **not machine-verifiable** and the review says so. No policy row or default value is ever seeded; the registry has no `IsDefault`. **D:** the business must name who verifies the reference (the holder of `PackagingPolicy.Approve`, tied to E-5).

---

# Policy Value Representation

TI-2 **revalidated (A)**: registry-backed stable text codes; explicit converter; `Unrecognized` explicit state; no ordinal; no silent fallback.

| Question | Decision |
|---|---|
| Registry kind | **Code-based**, with an **append-only committed manifest** (code, decision, status, first-used schema version, meaning). A **guard test fails if any released code disappears or changes meaning** (PR-12, C). *DB-based rejected* (a mode's availability equals code being implemented — M-4). *Hybrid rejected* (the only thing that would live in a DB, labels, is already localized in resx) |
| Localization | Persisted code is language-neutral; labels come from EN/FA resx keyed by code; a label may change with no effect on history. Retired codes keep their labels |
| Unknown code | Loads as `Unrecognized(code)`; the policy is invalid; reports display the raw code; nothing defaults |
| Historical meaning | Code + manifest (in source control) + the version's `ValuesSchemaVersion`; a deployed database alone does not carry meanings (accepted limitation, **B**) |
| Ordinals | Structurally impossible (value type needs an explicit converter) |

---

# Historical Policy Integrity

**Rule (A):** a historical record is never reinterpreted with today's policy; the version id recorded at creation is the sole reference.

## Minimum reference boundary (SR-18, C)
From the freeze entity set (`PackagingRun`, `PackagingInput`, `Package`, `PackagingOutput`, `PackagingLoss`, `PackagingEvent`):

| Entity | Carries `PackagingPolicyVersionId`? | Why |
|---|---|---|
| **`PackagingRun`** | **Yes (root)** | Process record created under one policy |
| **`Package`** | **Yes (own root)** | Independent, long-lived lifecycle (quality release, inventory posting, genealogy, barcode); linked to a run only through `PackagingOutput`; D1/D4/D6/D7 must be interpretable without a join. **Invariant:** a package created within a run has the run's version |
| `PackagingInput`, `PackagingOutput`, `PackagingLoss`, `PackagingEvent` | **No — inherit from the run** (mandatory run FK) | Avoids drift |
| Financial policy (if D12 is a separate type) | Recorded on `PackagingOutput` **at posting time** | `InventoryTransaction` is a shared ledger and must not be altered |

## Guarantees and their limits
- Superseding or retiring a policy changes no historical row (version rows are never edited after `Draft`).
- Completed transactions are unaffected by retirement; *in-flight* behaviour after supersession/retirement is **business decision OQ-3 (D)**.
- A future policy changes no historical report; reports group by version id.
- Audit can identify the exact version (FK + `AuditLog`).
- **Limit (B):** *which roles were entitled to act* at the time is **not** versioned — grants are live data. It is reconstructable only from `AuditLog` (grant history) and `AuthorizationAuditLog` (decisions), which is why every lifecycle authorization must use the user-aware path that writes an audit row.
- **Immutability enforcement — correction of TD F-T6 (B):** PEMS **does** enforce immutability at `SaveChanges`: `WorkflowVersionImmutabilityGuard.EnforceAsync` is called from `PEMSDbContext.SaveChangesAsync`, rejects edits to Published/Retired workflow versions and their stages/transitions even when made directly, and allows inserts of new versions. **PR-7 (C):** a `PolicyVersionImmutabilityGuard` in the same place — content immutable after `Draft`; only legal status transitions; inserts only as `Draft`; deletes only of `Draft`. (Same limitation as the precedent: it cannot verify the *workflow evidence*; the service does, and PR-15's integrity check detects violations.)

---

# Scope Resolution

Deployment → `FunctionalLocation` chain → `ProductionWorkCenter`; nearest applicable scope wins; whole-policy replacement. **Revalidated (A)** with these clarifications and defects:

| Item | Result |
|---|---|
| Deterministic | Pure function of (type, scope context, date, stored versions); ties are an **error**, as in `WorkflowBindingService` |
| No overlap at one scope | Unique index gives one *definition* per scope; **overlapping windows between versions are not prevented by an index** — activation must check under a **concurrency token on `PolicyDefinition`** (PR-8, C) or two concurrent activations both pass |
| No field merging | Confirmed: child replaces parent whole |
| Future effective dates | Not candidates until reached; no backdating |
| Retired / expired | Not candidates. **Child retired ⇒ resolution falls to the parent** — a real change of governing policy: PR-16 requires an *impact preview and recorded acknowledgement* in the approval; **D:** should a retired scoped policy revert to the parent or stop that scope? (default in the design: revert, with acknowledgement) |
| Missing policy | `NoPolicyInForce` → Packaging unavailable |
| Invalid / blocked | Never candidates |
| Inactive scope target | Not specified before — **PR-8 (C):** activation requires an active target; a definition whose target later becomes inactive is skipped and the trace says why |
| `FunctionalLocation` is not a `BaseEntity` | No row version; re-parenting changes *future* resolution and is visible only in `AuditLog`. Resolver walks the chain **bounded and cycle-guarded** (B/C) |
| **Operational status** | **`ProductionWorkCenter` has 0 live rows → WorkCenter scope is designed but not operational; no WorkCenter data is created.** `FunctionalLocation` (32 rows, 28 roots, depth ≤ 2, `Plant`/`Unit` empty) is operational but flat; the **Deployment** scope carries the policy in practice (B) |

---

# Capability Activation Safety

## Hard safety invariant HI-1
> **A capability is operational only if all of: its registry status is `ProductionReady`; a validated, approved policy version in force selects it; and the required authorization grants exist. A policy value can never, by itself, make a capability operational.**

`Configured → Validated → Approved → Implemented → Tested → Authorized → Active` is enforced as:

| Step | Enforced by | Runtime-checkable? |
|---|---|---|
| Configured | policy values present | yes |
| Validated | activation validation | yes |
| Approved | workflow evidence S-2′ | yes |
| Implemented / Tested / Production-ready | a **code-declared registry status** flipped only in the release change that ships the implementation and its tests; a CI test asserts every `ProductionReady` capability has its listed tests (*the runtime cannot itself know that tests ran — stated honestly*) | yes (declared) |
| Authorized | required permissions exist and are held (P.3) | yes |
| Active | resolver + readiness | yes |

`Split = Enabled` is therefore rejected at activation (`BIZ_CAPABILITY_NOT_READY`) **and** independently refused at the Split operation's entry point by a **capability gate** (`RequireCapability(...)`) — so even a corrupted or hand-edited policy row cannot activate it (PR-14, C). Existing invariants are unaffected: the Phase 2C gate in `ProductionRunService.PostOutputToInventoryAsync` (refuses non-`Released` batches — re-read 2026-09-25, line ~470), `IMaterialStockService`, `BarcodeIdentity`, `BarcodeService.SupportedTargetTypes`.

**No default fallback (A):** no policy ⇒ *Packaging unavailable / configuration required*; no `IsDefault`, no seeded policy, no code path reads the Business Baseline's technical recommendations; a test asserts "no policy ⇒ unavailable".
**D1–D12 boundary (A):** the infrastructure never chooses a business option; it stores, validates, approves and activates what the business selected. Recommendations remain non-binding text in documents.

---

# Audit

| Requirement | Result |
|---|---|
| Reuse `AuditLog` | **A.** Every `Added/Modified/Deleted` entity is audited by `PrepareAuditEntries`/`FinalizeAuditEntries` (entity name, key, action, `prop: old -> new`, user, time); no exclusions except `AuditLog` itself; **no code path deletes or edits audit rows** (searched) |
| Lifecycle events (Created, Submitted, Approved, Rejected, Blocked, Activated, Superseded, Retired) | Derivable as `Status: X -> Y` updates plus the version's own stamps (`…On/ById`) and the immutable `WorkflowHistory` |
| Must identify policy, version, scope, actor, time, action, previous/new state, reason | Policy/version/scope: FK + definition; actor: **both user id and employee id** (PR-2); time: stamps; previous/new state: `AuditLog` summary; **reason**: `AuditLog` has none → **mandatory `Reason` for Reject, Block, Retire and `ChangeSummary` for every version** (PR-15, C) |
| Caveat 1 (B) | `SaveChangesAsync` writes the business change first and the audit rows in a **second** save; a failure between them can lose the audit row. The version's stamps (same transaction as the change) are therefore authoritative and `AuditLog` corroborating |
| Caveat 2 (B) | `AuditLog.UserId` is `CurrentUserId` and can be null for background work; policy lifecycle is user-driven only |
| Caveat 3 (B) | `AuditLog` is high-volume (~205k rows when its index was designed) and is editable by database administrators (BG.4) |
| No parallel audit table | Confirmed. (`WorkflowConfigAudit` is workflow-specific and not reused) |
| Authorization audit | User-aware `AuthorizeAsync` writes `AuthorizationAuditLog`; role-only checks do not (SR-07) |

---

# Architecture Duplication

| Concern | Result |
|---|---|
| Second RBAC | **No.** Uses `Permission`/`RolePermissionGrant`/`AuthorizeAsync`/`SoDPolicy`. PR-5 *completes* the existing enforcement table's administration; it adds no model (**A**, with **E** on ownership) |
| Second workflow engine | **No.** `IWorkflowRuntime` + `WorkflowBinding`; no Packaging-specific approval logic beyond evidence verification |
| Second audit engine | **No.** `AuditLog` |
| Second organization hierarchy | **No.** `FunctionalLocation`, `ProductionWorkCenter` referenced by FK |
| Second configuration framework | **No general one exists**; the Policy Infrastructure is the first *policy lifecycle* component (**A**) |
| Second effective-date framework | Three implementations of the same *pattern* would then exist (`WorkflowVersion.EffectiveDate`, `WorkflowBinding.EffectiveFrom/To`, `PolicyVersion`). Accepted as pattern duplication, **not** code sharing; extraction into a shared component is *not* recommended now (**E** may revisit when a second domain adopts policy) |
| Reusability for future domains | Yes: lifecycle, scope, resolver, approval binding, audit are domain-neutral; a domain supplies registries, values, validators, permission module (SE.2 ER-5 governs growth). The infrastructure has exactly one consumer until another domain adopts it — **B** |

---

# Findings Classification

| ID | Finding | Class | Severity |
|---|---|---|---|
| SR-01 | Auto-skip semantics exact; employee-only stage auto-skipped; correct for dynamic modules | A | — |
| SR-02 | Boundary can enforce approval safety; **no WorkflowPlatform change required**; optional hardening | A (+E optional) | — |
| SR-03 | Non-empty-but-unheld stage stalls (fail-closed); TD S-1 wording imprecise | B | LOW |
| SR-04 | Completion ≠ evidence; S-2′ | C | HIGH |
| SR-05 | Null-employee bypass of `ForbidInitiator`; live SystemAdmin without Employee | C | **HIGH** |
| SR-06 | Roles are caller-supplied; runtime not an authorization boundary for `Permission:` | C | HIGH |
| SR-07 | `HasPermissionAsync` has no SoD/scope/audit; TD wording wrong | B + C | HIGH |
| SR-08 | SoD rows are seeder data; pairs to confirm | C + E | MEDIUM |
| SR-09 | SystemAdmin: bypass, workflow publish/bind, self-grant, direct activation | C + E | HIGH |
| SR-10 | Break-glass hard-coded in two sites | C | MEDIUM |
| SR-11 | `Permission:PackagingPolicy.Approve` representable without a second authorization system | A | — |
| SR-12 | No runtime grant/revoke for new actions; startup-only additive sync; E-5 unexecutable | C + E | **HIGH** |
| SR-13 | Role-name strings in legacy tables/workflow stages; name-reuse hazard; premise corrected | A + B | MEDIUM |
| SR-14 | Fixed typed columns + `ValuesSchemaVersion` + ER-1…ER-6; JSON/EAV/typed-records rejected | A + C | — |
| SR-15 | D12 coupling contradicts its Phase 3C gate; separate policy type recommended | C + D + E | MEDIUM |
| SR-16 | Business approval not visible to runtime; mandatory `BusinessApprovalReference`, no defaults | C + D | MEDIUM |
| SR-17 | Code-based registry + append-only manifest guard; resx labels | A + C | LOW |
| SR-18 | Version id on `PackagingRun` and `Package`; children inherit; financial ref on output | C | MEDIUM |
| SR-19 | `SaveChanges`-level immutability guard exists as precedent; adopt (correct TD F-T6) | B + C | MEDIUM |
| SR-20 | Audit: reuse; second-save caveat; mandatory reasons; both identities | A + B + C | MEDIUM |
| SR-21 | Overlap prevention needs a definition-level concurrency token | C | MEDIUM |
| SR-22 | Inactive scope target rule | C | LOW |
| SR-23 | Child-retirement fall-through to parent: impact preview + business choice | C + D | MEDIUM |
| SR-24 | WorkCenter scope not operational (0 rows); Deployment carries policy | B | — |
| SR-25 | `FunctionalLocation` not `BaseEntity`; bounded, cycle-guarded resolver | B + C | LOW |
| SR-26 | HI-1 capability invariant; registry `ProductionReady` + entry-point gate; tests cannot be self-attested at runtime | C | HIGH |
| SR-27 | No default fallback; no seeded policy; D1–D12 boundary | A | — |
| SR-28 | Residual risk: SystemAdmin + database access | E | — |
| SR-29 | Pattern duplication of effective-dating accepted; no second RBAC/workflow/audit/hierarchy | A + E | LOW |

No finding was hidden or downgraded. **HIGH findings: SR-04, SR-05, SR-06, SR-07, SR-09, SR-12, SR-26** — all have a documented required change.

---

# Required Pre-Implementation Changes

These must be **accepted into the Phase 3A design freeze** and implemented in the phase named. None is implemented here.

| ID | Change | Phase | Fixes |
|---|---|---|---|
| PR-1 | Policy service is the only starter of policy workflows; no explicit participant lists; Submit-time inspection of the bound workflow version (W.2); `PendingApproval → Blocked` on bad evidence | 3A-0 | SR-01…04 |
| PR-2 | Refuse Submit/Approve when employee id is null; store and compare **both** user id and employee id | 3A-0 | SR-05 |
| PR-3 | All lifecycle authorization via user-aware `AuthorizeAsync` with `ResourceId`; roles derived server-side; approving transition carries `RequiredPermission` + `ForbidInitiator` (data) and is verified at Submit | 3A-0 | SR-06, SR-07 |
| PR-4 | Break-glass: one shared predicate for both `AccessControlService` sites; `PackagingPolicy.{Submit,Approve,Activate,Retire}` need explicit grants even for SystemAdmin | 3D (code change) | SR-09, SR-10 |
| PR-5 | Minimal generic audited grant/revoke on `RolePermissionGrants` (immediate; no self-grant of `PackagingPolicy.*`); scope of delivery decided by the architecture owner | prerequisite to E-5 execution / 3D | SR-12 |
| PR-6 | SoD policy rows (Edit↔Approve, Submit↔Approve, recommended Approve↔Activate) via the existing seeder pattern + denial tests | 3D | SR-08 |
| PR-7 | `PolicyVersionImmutabilityGuard` at `SaveChanges` (mirrors `WorkflowVersionImmutabilityGuard`) | 3A-0 | SR-19 |
| PR-8 | Concurrency token on `PolicyDefinition` for activation; active-target rule; bounded cycle-guarded chain walk | 3A-0 | SR-21, SR-22, SR-25 |
| PR-9 | `ValuesSchemaVersion` + ER-1…ER-6 recorded as binding evolution rules | 3A-0 | SR-14 |
| PR-10 | Record the D12 decision: separate `PackagingFinancial` policy type (recommended) or deferrable dimension | before 3A-0 design freeze | SR-15 |
| PR-11 | Mandatory `BusinessApprovalReference` per version; no seeded policy rows; no `IsDefault`; test "no policy ⇒ unavailable" | 3A-0 | SR-16, SR-27 |
| PR-12 | Registry append-only manifest + guard test; `Unrecognized` handling | 3A-0 | SR-17 |
| PR-13 | `PackagingPolicyVersionId` on `PackagingRun` **and** `Package`; children inherit; financial version on output at posting | 3A | SR-18 |
| PR-14 | Registry `ProductionReady` + `RequireCapability` gate at every operation entry point; CI test linking capability → tests | 3A/3B | SR-26 |
| PR-15 | Mandatory reasons and stamps in the same transaction; integrity check (detective) for approval evidence and `PackagingPolicy.*` grant changes; document the audit second-save caveat | 3A-0 / 3D | SR-20, BG.4 |
| PR-16 | Retirement/supersession **impact preview** and recorded acknowledgement | 3A-0 | SR-23 |

---

# Business Decisions Still Required (D)

- **D1, D2, D3, D4, D5 (acknowledgement), D6, D7, E-4, E-5, D12** — all still **OPEN, 0 / 16** (unchanged; nothing here approves or recommends an option).
- **OQ-3** — what an in-flight run/package may do after its bound policy version is superseded or retired.
- **SR-23** — should a retired scoped policy **revert to the parent** or **stop that scope** (default in the design: revert, with acknowledgement).
- **OQ-1 / OQ-10** — is a packaging line a `ProductionWorkCenter`; does the business want variation below deployment level at all.
- **SR-15** — Finance's ownership and cadence for D12 (informs the separate-policy-type decision).
- **SR-16** — who verifies the `BusinessApprovalReference` (the holder of `PackagingPolicy.Approve`, i.e. tied to E-5).

# Architecture Owner Decisions Still Required (E)

1. Name/date on the sign-off lines (ADR §4; TD §21); **Gate 15 stays unchecked without them**.
2. Accept PR-1…PR-16 into the 3A design freeze.
3. **PR-5**: grant-administration capability — platform prerequisite or inside Packaging; and acceptance that E-5 cannot be executed without it.
4. **PR-10 / SR-15**: D12 as a separate policy type or a deferrable dimension.
5. **BG.4**: accept or reject the residual SystemAdmin/database-administrator risk and the detective control.
6. SoD pairs (OQ-6); the `PendingApproval → Blocked` and `Rejected` states (OQ-8); optional WorkflowPlatform hardening (W.2).
7. Confirm that lifecycle authorization must use `AuthorizeAsync` (SR-07) and that the permission mechanism is `Permission`+`RolePermissionGrant` (OQ-9 resolved by P.2 in this direction).

---

# Final Readiness Statement

- The Packaging Policy architecture is **sound in structure** but the technical design record was **not safe as written**: three of its statements were contradicted by source (HasPermissionAsync provides SoD; an administrator can grant new actions; no database-level immutability precedent), and it lacked guards for the workflow auto-approval and null-identity paths. The amendments are recorded in the TD (§ "Safety Review Amendments") and the required changes above.
- **Gate 15:** **TECHNICAL EVIDENCE COMPLETE — SUBJECT TO PR-1…PR-16 — ARCHITECTURE OWNER APPROVAL REQUIRED.** Not checked.
- **Business approvals: 0 / 16. Activation gates: 0 / 15.**
- **Final classification: C — IMPLEMENTATION BLOCKED.** Nothing in this review activates Phase 3A, and nothing was implemented.
