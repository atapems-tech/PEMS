# Workflow Platform — Phase 4 Admin Audit (Workflow Administration & Visual Designer)

Status: **AUDIT ONLY — no code changed.** Evidence source: repository (`PEMS.WorkflowPlatform`, `PEMS.Persistence`, `PEMS.Web`) and a read-only query of `PEMSDB`.
Note: the Phase 4 charter text received ends mid-§21 (Separation of Duties for Workflow Administration). §1–§20 are audited here; the remainder should be confirmed before implementation.

Doc-name note: the charter lists `WORKFLOW_PLATFORM_PHASE1_HARDENING.md`; the repository file is `WORKFLOW_PLATFORM_PHASE1.md`.

---

## A. What the Workflow Platform can already do
* **Definitions** (`WorkflowDefinition`: Name, Category, Module, Description, IsActive, CreatedDate/By) → **Versions** (`WorkflowVersion`: VersionNumber, Status Draft→Validated→Published→Retired, Validated/Published/Retired dates, `ValidatedById`, `PublishedById`, `ClonedFromVersionId`, `ChangeSummary`, EffectiveDate) → **Stages** (`WorkflowStage`: Name, `NameFa`, `BusinessStatus`, StageOrder, StageType Sequential/Parallel, ResponsibleRole, ResponsibleEmployeeId, ApprovalStrategy, MinimumApprovalCount, IsMandatory/Skippable/Delegatable/Escalatable, TimeoutHours, EscalationRole, NotificationKey, Color, Icon, Description) and **Transitions** (`WorkflowTransition`: From/To stage, ActionType, `Label`/`LabelFa`, RequiredPermission, ForbidInitiator, ConditionExpression, Priority; `FromStageId = null` = start edge, `ToStageId = null` = terminal edge).
* **Bindings** (`WorkflowBinding`: Module + EntityType + Context → Definition + pinned version, IsActive, EffectiveFrom/To).
* **Immutability**: published/retired versions guarded in the service and in `PEMSDbContext.SaveChanges` (`WorkflowVersionImmutabilityGuard`, `WorkflowImmutableVersionException`).
* **Runtime**: version-pinned instances, action registry (module actions by name), condition evaluator, module authorization hook, read model (`GetRuntimeViewAsync`, `GetAvailableActionsAsync`), workflow history, concurrency protection (unique running instance per entity).
* **Configuration audit**: `WorkflowConfigAudits` with `Action`, actor, timestamp, `PreviousState`, `CurrentState`, `Details`; 14 action codes (DefinitionCreated/Cloned/Archived, VersionCreated/Cloned, DraftEdited, Validated, ValidationFailed, Published, Retired, Activated, Deactivated, BindingCreated, BindingChanged). PEMSDB currently holds 63 audit rows.

## B. Administrative services that already exist (all registered in DI in `Program.cs`)
* `IWorkflowDefinitionService`: `CreateDefinition`, `CreateDraftVersion`, `CloneVersion`, `CloneDefinition`, `AddStage`, `UpdateStage`, `RemoveStage`, `AddTransition`, `RemoveTransition`, `ValidateVersion`, `PublishVersion`, `RetireVersion`, `ArchiveDefinition`, `GetAudit`.
* `IWorkflowVersionValidator.ValidateAsync` — 38 issue codes with severity Error/Warning (e.g. `NO_STAGES`, `START_MISSING/MULTIPLE`, `UNREACHABLE_STAGE`, `DEAD_END`, `NO_TERMINAL`, `DUPLICATE_STAGE_NAME/ORDER`, `ROLE_UNKNOWN`, `PERMISSION_UNKNOWN`, `ACTION_NOT_REGISTERED`, `CONDITION_INVALID`, `SOD_ON_START`, `BINDING_PIN_MISMATCH` …).
* `IWorkflowBindingService`: `Create`, `Change`, `Deactivate`, `Activate`, `Resolve`.
* `IWorkflowReadModelService`: runtime view/actions (instance-oriented, not definition-oriented).
* `IWorkflowActionRegistry`, `IWorkflowConditionEvaluator`, `IWorkflowReferenceCatalog` (role / employee / permission existence) — the sources for dropdowns in a designer.

Rules already enforced by the service: one open Draft/Validated version per definition; editing a Validated version silently returns it to Draft; Publish requires Validated **and re-validates**; Retire refuses if an active binding pins the version; Archive refuses if an active binding exists; archived definitions cannot be cloned/edited.

## C. What can be safely exposed to an admin UI
All of §B may be called from thin controllers. Nothing needs to bypass the services. `ValidateVersion` and `PublishVersion` are the only validation/publish paths (UI must not duplicate validator logic).

## D. Gaps (what is missing)
| Capability | Status |
|---|---|
| Definition list / details pages | **Missing** (no admin UI at all; the only workflow UI is `DocumentWorkflowController`, an EDMS module page, not an administrator tool). |
| Definition Persian name, last-modified date | **Gap in model**: `WorkflowDefinition` has no `NameFa`/`ModifiedDate` (list requirement "Persian Name / Last modified"). Last modified/published can be *derived* from versions + audit without schema change; a Persian display name would need one nullable column (additive) or a resource-key mapping. |
| Definition edit (name/description) | **Missing** service method (only create/clone/archive). |
| Version management (clone, validate, publish, retire, view) | Service exists; UI missing. |
| Discard/abandon an open Draft | **Missing service method**. `EnsureNoOpenDraft` tells the user to "publish or release" the draft but no release/discard operation exists. Needed so a stale draft does not block cloning. |
| Stage edit | `UpdateStage` exists (takes full `WorkflowStageInput`; `Color/Icon` are not in `WorkflowStageInput`, so they are not editable through the service). |
| Transition edit | **Missing**: only Add/Remove. Editing = remove + add (loses transition id; acceptable inside a draft, but a real `UpdateTransition` with audit is cleaner). |
| Participants | **No `WorkflowParticipant` entity exists.** Design-time "participants" are `ResponsibleRole` / `ResponsibleEmployeeId` on the stage and `RequiredPermission` (incl. `Permission:Resource.Action` pseudo-roles) on the transition. Runtime participants (`WorkflowInstanceStage`) are generated. The designer must expose exactly these fields and must not invent a participant table. |
| Reorder stages | `StageOrder` is an integer edited through `UpdateStage`; no bulk reorder call (do as N updates in one request or add a service method). |
| Graph visualization | **Missing**. No graph library in `wwwroot/lib` (bootstrap, icons, jquery, chartjs, quill, signalr). Layout data (x/y) does not exist in the model; a deterministic auto-layout (layered, computed server- or client-side from stages/transitions) needs no schema change. |
| Validation display | Result type carries `Code`, `Severity`, message and location; UI missing. |
| Binding administration | Service exists; UI missing. Rebinding is high-risk: needs explicit confirmation, before/after summary and audit (audit exists). |
| Usage / impact | No service. Computable from `WorkflowInstances` (Status, `WorkflowVersionId`, Module) — reliable: counts by version and status (PEMSDB example: EngineeringDocument 213 completed / 123 rejected / 3 running; VendorDocument 1 completed / 2 running; WorkOrder 1 running). |
| Audit viewer | `GetAuditAsync` exists (filter by definition/version); UI missing. `PreviousState`/`CurrentState` are short strings (e.g. `v2 Draft`), not full before/after snapshots. |
| Rollback | No rollback primitive. The safe equivalent is: clone an older published version → validate → publish → rebind (keeps history). No mutation of published versions. |
| Draft-editor concurrency | `WorkflowVersion` has no `RowVersion`; two admins editing one draft can overwrite each other. Needs a decision (optimistic token vs. single-editor lock) — **possible additive column**. |
| Designer vs. publisher separation | `WorkflowVersion` stores `ValidatedById` and `PublishedById` but **no creator/last-editor id**; the editor identity exists only in `WorkflowConfigAudits` (`DraftEdited` rows). A "publisher must differ from last editor" rule is implementable from audit without schema change but should be a deliberate decision (§21 of the charter is truncated). |

## E. Database changes actually necessary
Minimum: **none are strictly required** for a first usable release (list, versions, clone/validate/publish/retire, bindings, usage, audit, read-only + draft-editing graph with auto-layout).
Candidate additive changes, each only if the corresponding feature is confirmed:
1. `WorkflowDefinition.NameFa` (nullable) — Persian name in list/detail (charter §5).
2. `WorkflowVersion.RowVersion` — draft-editor concurrency.
3. (Optional, later) stored canvas positions — **not** recommended for phase 4; auto-layout is sufficient and avoids drift.
No new tables. Any migration must be additive; PEMSDB must not be reset.

## F. Permissions available to protect the area
* **Convention in the repository**: every `Areas/Admin` controller uses `[Authorize(Roles = SystemRoles.SystemAdmin)]`, and the Administration sidebar section is gated by `MenuAccess.Administration` (SystemAdmin only).
* **Fine-grained model**: `Permissions` (`Resource.Action`) + `RolePermissionGrants` via `IAccessControlService`/`HasPermissionAsync`, used by business modules and already consumed by the workflow platform for task permissions (`AccessControlWorkflowAuthorizationHook`). The permission catalogue is seeded by startup seeders (pattern: `GetOrCreatePermissionAsync` in `WorkOrderGovernanceSeeder`).
* **Recommendation**: keep the outer gate as the existing Admin-area convention (SystemAdmin), and add server-side action permissions `WorkflowManagement.View / Edit / Validate / Publish / Bind / Retire` checked in the controller/service layer, seeded and granted to SystemAdmin by default (so behaviour is unchanged until an administrator delegates). This matches both conventions and allows Designer/Publisher separation later. Whether to require permission-*only* (no role) access is a decision for the owner.

## G. Reusable UI components
* Admin area layout, `ViewData["Section"] = "Administration"`, sidebar sub-item pattern (`_SidebarMenu.cshtml` lines ~477-520), `PermissionMatrix`/`Roles`/`AuditLog` views as list/table patterns, global compact page header (previous phase), bilingual `SharedResource` resx (EN/FA) and RTL support, Help administration + `HelpContentSeeder` for a Help article.
* `_PemsStageDiagram` is a **linear, read-only** stage strip: reusable for a *summary* preview only; it cannot render branches/rejects/returns. The designer graph is a new component (inline SVG, no heavy library), built over the runtime read shapes.
* Bootstrap 5, bootstrap-icons, jQuery already present (no new frontend framework needed).

## H. What must NOT be duplicated
Validator logic; publish/immutability rules; version/status transitions; binding resolution; audit table; stage/transition/participant models; permission expansion; role/permission lookups (`IWorkflowReferenceCatalog`); the action registry; instance/history queries beyond read-only counts.

## Other findings relevant to implementation
* PEMSDB workflow inventory (9 definitions): EngineeringDocument (3 published versions, **no binding** — legacy direct lookup), VendorDocument/VDR, TechnicalQuery, MOC, NCR, Permit (1 published version each, no binding), WorkOrder multi-discipline review (bound), Work Request Approval (bound), Work Order Lifecycle (bound). The admin UI must handle "definition without binding" as a normal state and must not assume every definition is bound.
* Several legacy definitions carry Persian `Name`s while newer ones are English → the list must display `Name` as stored and add a Persian display only via `NameFa` (if approved) rather than translating in code.
* Definitions consumed by hard-coded module code (EDMS/VDR/TQ/MOC/NCR/Permit) may depend on exact stage names; editing them through a designer is riskier than for the platform-bound Work Request/Work Order flows. Publishing a new version of those modules is allowed by the platform, but the UI should warn when a definition has **no binding** (module code may look stages up by name) and rely on the validator. This is a risk to acknowledge, not to redesign.
* A publish that succeeds changes nothing for running instances (pinned); usage counts make this visible.

## Proposed implementation slices (after approval)
1. Admin services facade (read models for definition list/detail/usage/audit; `UpdateDefinition`, `DiscardDraft`, `UpdateTransition`, optional reorder) inside the existing platform/Application layers — no new engine.
2. `Areas/Admin` controllers (thin) + permissions + sidebar entry + resx (EN/FA).
3. Definition list/detail, version list + clone/validate/publish/retire, validation panel, binding page (with confirmation & impact), usage and audit views.
4. Read-only graph (auto-layout SVG) → then draft editing panels for stage/transition.
5. Tests (service, authorization, immutability, concurrency, no-hard-coding), real-browser QA on an isolated DB, Help article, final doc.

## Decisions needed before coding
1. Add `WorkflowDefinition.NameFa` and `WorkflowVersion.RowVersion` (additive migrations)? Recommended: yes to both.
2. Access model: SystemAdmin gate plus `WorkflowManagement.*` permissions (recommended) vs. permissions only.
3. Designer/Publisher separation (§21 truncated): enforce "publisher ≠ last editor" from `WorkflowConfigAudits`?
4. Scope of editable definitions: allow editing all definitions, or start with platform-bound ones (Work Request, Work Order Lifecycle, WO Review) and make legacy-module definitions view-only until their modules are migrated?
5. Please resend the tail of the charter (§21 onward: separation of duties, tests/QA requirements, documentation and stop rules).
