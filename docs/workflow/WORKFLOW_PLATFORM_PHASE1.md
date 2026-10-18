# PEMS Workflow Platform — Phase 1 Hardening

Scope: hardening of the **existing** `PEMS.WorkflowPlatform` (no second engine). No business module was migrated; no module UI, no visual designer.
Follows `WORKFLOW_PLATFORM_AUDIT.md`.

## 1. Lifecycle

`Draft → Validated → Published → Retired`

| State | Editable | Usable for new instances | Notes |
|---|---|---|---|
| Draft | yes | no | created by `CreateDraftVersionAsync` / `CloneVersionAsync` |
| Validated | yes (edit returns it to Draft) | no | validator reported no *errors* (warnings allowed) |
| Published | **no** | yes | immutable |
| Retired | **no** | no (existing instances keep running) | only reachable from Published; blocked while an active binding pins it |

"**Active**" is not a version state. A version is *active* for a key when an **active binding** selects it (pinned) or when it is the latest Published version of a bound definition (unpinned). Definitions can be archived (`IsActive = false`) only when no active binding references them.

Only one open (Draft/Validated) version per definition exists at a time.

## 2. Versioning and immutability

Enforced twice:
1. **Services** (`WorkflowDefinitionService`): every stage/transition mutation requires an editable version (`WorkflowImmutableVersionException` otherwise).
2. **`PEMSDbContext.SaveChanges/SaveChangesAsync`** → `WorkflowVersionImmutabilityGuard`: any Added/Modified/Deleted `WorkflowStage`/`WorkflowTransition` whose version was Published/Retired *before this save* is rejected; a Published/Retired version row may change only `Published → Retired` (+`RetiredDate`) and can never be deleted or moved backwards.
   Inserting a brand-new version directly as `Published` (how the existing seeders work) is still allowed. The guard does not cover raw SQL.

**Clone** (`CloneVersionAsync`, `CloneDefinitionAsync`) copies stages, transitions (with remapped ids), conditions, approval strategy/minimum count, roles, responsible employee, SLA (`TimeoutHours`), escalation role/flags, notification key, required permission and SoD flag. It never copies instances, instance stages or history. Running instances stay pinned to their original `WorkflowVersionId`. Inserting a stage in a new version (e.g. "Reliability Review") only affects instances started after that version is published.

## 3. Publish rules

A version can be published only if it is `Validated` **and** the validator returns no errors again at publish time. Validator (`IWorkflowVersionValidator`) checks:

- **Structural**: has stages; exactly one start transition with a target; no broken/cross-version references; no duplicate `(from, action, priority)`; a terminal transition exists; every stage reachable; no dead-end stage; duplicate/empty names; (warnings) duplicate order, self loops, stage without Approve transition.
- **Security**: roles, responsible employee, escalation role and required permission exist (via `IWorkflowReferenceCatalog`; `Discipline:{id}` pseudo-roles are dynamic and skipped); permission format `Resource.Action`. If no catalog is registered a `CATALOG_UNAVAILABLE` **warning** is produced — it is never a silent pass.
- **Runtime**: transition actions registered for the definition's module and of *Outcome* kind; conditions parse in the closed grammar; stage type/strategy valid; `MinimumCount` has a valid count; SLA positive; (warnings) `MandatoryRolesOnly` is treated like `AllApprovalsRequired`, escalatable stage without role/SLA.
- **Binding/definition**: module/category present; (warnings) inactive definition, legacy ambiguity with another active definition of the same module+category; (error) binding pinned to another definition's version.

## 4. Binding

`WorkflowBinding(Module, EntityType, Context, DefinitionId, PinnedVersionId?, IsActive, EffectiveFrom?, EffectiveTo?)`.
`Context` corresponds to `WorkflowDefinition.Category` (the `workflowCategory` argument of `StartInstanceAsync`); `EntityType` corresponds to `businessObjectType`.

Resolution (`IWorkflowBindingService.ResolveAsync`):
- 0 matching active bindings → `null` → **legacy path** (`Module` + `Category`).
- 1 → definition + pinned version (must be Published) or latest Published.
- >1 → `WorkflowBindingAmbiguousException`. Never "first active".
- Overlapping active bindings for the same key are rejected on create/change/activate.

Legacy path change: if more than one active definition matches `Module`+`Category`, `StartInstanceAsync` now throws `WorkflowBindingAmbiguousException` instead of silently taking the first (previously nondeterministic). Existing seeded data has exactly one per key.

No binding backfill was performed: existing modules keep running on the legacy path.

## 5. Runtime behaviour

`IWorkflowRuntime` signature is unchanged; `new WorkflowRuntimeService(ctx)` still works (all new dependencies are optional constructor parameters; DI fills them).

- Start via binding also: stores `WorkflowBindingId`; refuses a second **Running** instance for the same `(Module, EntityType, Id)`; refuses a version with more than one start transition. (These stricter rules apply **only** to binding-started instances, so unmigrated modules behave as before.)
- Explicit entity association: every new instance stores `EntityKey = "Module|BusinessObjectType|BusinessObjectId"`; `IWorkflowReadModelService.FindInstancesForEntityAsync` returns all instances for the entity (newest first). Old instances have `EntityKey = null` (not rewritten).
- Transition selection is deterministic: outgoing transitions for the outcome ordered by `Priority`, first whose condition holds (no condition = holds). An invalid condition fails closed.
- Instances started via a binding accept only registered actions.

## 6. Conditions (deliberately limited)

`Field op value` clauses joined by `AND` / `OR` (no parentheses). Operators `== != > >= < <=`, `IN (...)`. Values: `'text'`, numbers, `true/false`. Fields (closed list): `Priority, ProjectId, Module, BusinessObjectType, BusinessObjectId, StageName`. Missing fact → false (except `!=`). `>`/`<` numeric only. No functions, scripting, reflection or SQL. Anything else is a validation error.

## 7. Action registry

Core: `Approve, Reject, Return, Complete` (Outcome — may be a transition), `Assign, Notify, Escalate` (SideEffect — recognized names only, never a transition, never executed by the platform), `Start` reserved. Modules register additional **names** with `RegisterModuleAction(module, name, kind, description)` (alphanumeric, ≤50, cannot shadow core). The registry stores no delegates and executes nothing; there is no arbitrary C#/script/SQL/reflection path. Legacy action names of the existing seeded workflows (Issue, Assess, Close, Confirm, Reopen, Respond, AC, AP, CO, NC, RE) are registered in `Program.cs` so those definitions validate cleanly.

## 8. Authorization and separation of duties

Existing role check is unchanged and comes first. Additionally, on the selected transition:
- `ForbidInitiator` (SoD): the employee who initiated the instance cannot execute the action.
- `RequiredPermission` (`Resource.Action`): checked through `IWorkflowAuthorizationHook`. PEMS implementation `AccessControlWorkflowAuthorizationHook` delegates to the existing `IAccessControlService.AuthorizeForRolesAsync` using the roles the calling server-side service passes. **Fail-closed**: a transition with `RequiredPermission` and no hook denies.

Limits: the hook is role-based (`AuthorizeForRolesAsync`); the runtime API carries an employee id, not an Identity user id, so per-user scope rules and SoD-on-resource from `AuthorizeAsync` are not applied here. Transitions without these fields behave exactly as before.

## 9. Configuration audit

`WorkflowConfigAudits` (append-only, separate from `WorkflowHistory`): `DefinitionCreated, DefinitionCloned, DefinitionArchived, VersionCreated, VersionCloned, DraftEdited, Validated, ValidationFailed, Published, Retired, BindingCreated, BindingChanged, Activated, Deactivated` with actor id/name, timestamp, definition/version/binding ids, previous/current state and details. Written in the same `SaveChanges` as the change. Read with `IWorkflowDefinitionService.GetAuditAsync`. Runtime actions never write it, configuration never writes `WorkflowHistory`.

## 10. UI read model and available actions

`IWorkflowReadModelService.GetRuntimeViewAsync(instanceId, user)` returns workflow name, version, status, current stage, **ordered stages read from the instance's pinned version** with status (`Current/Completed/Skipped/Rejected/Upcoming`), participants (role/employee/status/decision/dates), approval progress, SLA view, available actions, history summary and warnings. `GetAvailableActionsAsync` computes actions server-side from state, the user's pending participation, transitions, conditions, SoD and permission; each action carries `IsAllowed`/`DeniedReason` so UIs can show a disabled button.

Limits: only actions that have an outgoing transition are listed (module-specific "Comment/Skip" style actions are not); stage status derives from history, so non-linear graphs show "Completed" for any visited stage.

## 11. SLA, escalation, notifications — what is *not* implemented

- SLA: due date (`assigned + TimeoutHours`) and overdue flag are **computed** on read. Nothing runs on a timer.
- Escalation: data only (`IsEscalatable`, `EscalationRole`); **no** escalation is executed (`EscalationExecuted` is always false).
- Notifications: `IWorkflowEventSink` (default no-op) receives `StageEntered / ActionExecuted / InstanceCompleted / InstanceRejected` with the stage's `NotificationKey`. Nothing is sent by the platform. A failing sink cannot break a workflow.

## 12. Database migration (`20260919230148_WorkflowPlatformPhase1Hardening`)

Additive only in `Up` (drops exist only in `Down`): new tables `WorkflowBindings`, `WorkflowConfigAudits`; new nullable/defaulted columns on `WorkflowVersions` (ValidatedDate/ById, RetiredDate, ClonedFromVersionId, ChangeSummary), `WorkflowStages` (ResponsibleEmployeeId, EscalationRole, NotificationKey), `WorkflowTransitions` (RequiredPermission, ForbidInitiator), `WorkflowInstances` (WorkflowBindingId, EntityKey); indexes on bindings, audits, `EntityKey`. No data is rewritten. Verified up → down → up on a disposable SQL Server database (dropped afterwards). The live `PEMSDB` was **not** updated by this work.

## 13. Migration strategy for future modules (not started)

1. Publish the module's definition through the lifecycle (not via a code seeder).
2. Create an explicit binding (pin the version for a controlled rollout).
3. Switch the module's *start* call to the same `StartInstanceAsync`; the binding makes it use the new rules.
4. Replace module-specific stage displays with the read model.
5. Move permission checks onto `RequiredPermission`/`ForbidInitiator` transitions.
Each step is per-module and reversible by deactivating the binding. **No module has been migrated.**
