# Workflow Platform — Phase 4: Workflow Administration & Visual Designer

**Classification: ACCEPTED WITH DOCUMENTED LIMITATIONS** (see §21–22)

Charter decisions applied as approved (NameFa, RowVersion, SystemAdmin + WorkflowManagement.* permissions, four-eyes publishing, conservative editable set). Preceded by `WORKFLOW_PLATFORM_PHASE4_ADMIN_AUDIT.md`.

---

## 1. Audit
See the separate audit document. Key confirmations from the repository: definition/version/stage/transition/binding/validator/audit services existed; no admin UI, no definition-metadata edit, no discard, no transition update, no participant table, no graph component, no version concurrency token. Audit correction found during implementation: `PublishedById` (not `PublisherId`) is the publisher field.

## 2. Architecture
```
Administration → Workflow Management (Areas/Admin, SystemAdmin)
   → WorkflowManagementController (thin)
      → IWorkflowAdministrationService (facade: authorization, edit policy, four-eyes, result mapping)
         → IWorkflowDefinitionService / IWorkflowBindingService / IWorkflowVersionValidator   (existing platform)
            → WorkflowDefinitions / Versions / Stages / Transitions / Bindings / ConfigAudits  (existing tables)
```
No second engine, definition model, instance model, history or audit table was created. The facade contains no validation/publish rules; it only authorizes, applies the edit policy and forwards. DTOs/interface: `PEMS.Application` (`WorkflowAdministrationDtos.cs`, `IWorkflowAdministrationService.cs`); implementation: `PEMS.Persistence/Servises/WorkflowAdministrationService.cs`; layout: `PEMS.Persistence/Workflow/WorkflowGraphLayout.cs`; English validator texts: `WorkflowValidationText.cs`.

## 3. Existing capabilities reused
Clone, add/update/remove stage, add/remove transition, validate, publish (re-validates), retire (refused while a binding pins the version), archive, immutability guard (service + `SaveChanges`), one-open-draft rule, binding create/change/deactivate, configuration audit.

## 4. New capabilities
Platform (shared, additive, required by the approved administration scope): `UpdateTransitionAsync`, `UpdateDefinitionAsync` (Name, NameFa, Description), `DiscardDraftAsync`, optional `expectedRowVersion` on every draft-editing call, `Discarded` version status, audit codes `DefinitionUpdated` / `DraftDiscarded`. Web/Application: definition list and details, version management, visual designer, validation panel, binding review/confirmation, usage, audit, Help article, EN/FA resources, sidebar entry.

## 5. Data model
Two additive nullable columns (migration `20260920193708_WorkflowAdminDefinitionNameFaVersionRowVersion`): `WorkflowDefinitions.NameFa nvarchar(200)` and `WorkflowVersions.RowVersion rowversion`. No new tables. `Discarded` is a status string (no schema change).

## 6. Security
* Every controller action is `[Authorize(Roles = SystemAdmin)]` (unchanged Admin-area convention) **and** the facade re-checks server-side: SystemAdmin role AND `WorkflowManagement.View/Edit/Validate/Publish/Bind/Retire`.
* `AccessControlService` normally lets SystemAdmin bypass all permissions. For the resource `WorkflowManagement` it now requires an explicit grant (same pattern as the existing Break-Glass exception), otherwise "SystemAdmin AND permission" would not be enforceable. `WorkflowManagementSeeder` creates the six permissions and grants them to SystemAdmin, so behaviour is unchanged until an administrator narrows it.
* Verified: non-SystemAdmin (even a forged POST) is redirected to Access Denied; a real SystemAdmin whose `Publish` grant was deactivated cannot publish (UI hidden and forged POST refused); published/discarded/legacy forged edits change nothing.

## 7. Definition management
List (name / Persian name with explicit fallback, module, category, published version, open draft, binding state, editable/view-only with reason, last modified, last published, created by), details, metadata edit (Name, NameFa, Description – shared by all versions and labelled so), archive. No generic delete.

## 8. Version management
Clone (from Published/Retired), edit, validate, publish, retire, discard, view. Statuses shown: Draft, Validated, Published, Retired, Discarded (definition Archived). One open draft per definition is preserved and surfaced. Discard (Draft or Validated): refused if any instance or binding references the version; the rows are kept (status `Discarded`), audited, confirmed in the UI.

## 9. Stage management
Draft only: add, edit, remove (removal deletes the stage's transitions; the server validator then rejects an invalid graph at validate/publish time). Exposed real fields: Name, NameFa, BusinessStatus, StageOrder, StageType, ResponsibleRole, ResponsibleEmployeeId, ApprovalStrategy, MinimumApprovalCount, TimeoutHours, IsMandatory, Description. `UpdateStage` overwrites every field, so the facade loads the existing stage and preserves the fields the UI does not edit (IsSkippable, IsDelegatable, IsEscalatable, EscalationRole, NotificationKey) — regression-tested. Stages have no Label/LabelFa fields in the platform, so none were invented.

## 10. Transition management
Add, update (new atomic platform operation), remove: from/to (empty = start/end), action, Label, LabelFa, RequiredPermission, ConditionExpression, Priority, ForbidInitiator. Source/target must belong to the same version (server-checked, also against ids from another version).

## 11. Visual designer
Inline SVG generated from the real version; deterministic layered auto-layout (no positions stored): longest-path layers, branches side by side, return edges routed as dashed lanes, start/terminal markers, labels (Persian labels in FA), selection highlight, tooltips, keyboard-focusable links, horizontal scroll on narrow screens. Stage/transition tables below the graph provide an accessible alternative. The SVG is a selection surface; edits are ordinary form posts to application services.

## 12. Validation
`WorkflowVersionValidator` only. Issues shown with severity, code, message, affected stage/transition links. English UI uses a code→English text table (`WorkflowValidationText`); Persian UI shows the validator's Persian message. Publish re-validates on the server regardless of earlier client state.

## 13. Publishing
Sequence: authorize (Publish) → edit policy → four-eyes check → token check → platform `PublishVersionAsync` (Validated only, re-validates) → immutable Published. Failure leaves the version Validated (verified). Four-eyes: the last editor (latest `VersionCreated`/`VersionCloned`/`DraftEdited` audit row of that version) cannot publish; a clone counts as an edit; if the audit has no actor (system seed) publishing is allowed.

## 14. Binding
Explicit change only, for definitions in the editable set; the target must be a Published version of the same definition; an impact page shows current version, target version and running instances per version; an explicit confirmation is required; audited (`BindingChanged`). Legacy/unbound definitions cannot be bound from the UI. Deactivate is available with confirmation.

## 15. Version pinning
Verified in the browser and in tests: after publishing and binding v2, a new Work Order showed and used the extra stage, while an existing Work Order stayed on v1 and kept its path.

## 16. Concurrency
`WorkflowVersions.RowVersion` is the token. Every draft-editing operation sets the token as the original value and touches the version row (so stage/transition edits also change it); a stale save raises `DbUpdateConcurrencyException`, converted to a localized "changed by another administrator, reload" result — no raw exception, no overwrite. Verified on real SQL Server (stale add/remove/validate/discard rejected; two simultaneous saves from one token: exactly one winner in 20 of 20 runs) and in two real browser sessions (EN and FA).

## 17. Audit
Existing `WorkflowConfigAudits`. Recorded: VersionCloned, DraftEdited (all stage/transition edits), Validated, ValidationFailed, Published, Retired, BindingChanged, DefinitionUpdated, DraftDiscarded, DefinitionArchived. Shown on the definition page and (per version) in the designer. The audit stores short state strings, not full before/after snapshots; no snapshot is claimed.

## 18. Browser QA
Real Chrome on an isolated database (`PEMS_QA_P4`, created from migrations; seeders ran on the fresh DB; dropped afterwards). 46/48 scripted checks passed first run; the 2 failures were harness artifacts (sidebar collapsed on Home; console code page mangling Persian in a SQL comparison) and were re-verified passing (A1, D7). Coverage: navigation, list (9 definitions, 3 editable / 6 view-only, 6 × "No active binding", never "error"), legacy view-only + forged clone, non-SystemAdmin denial + forged POST, details/versions/binding/usage/audit, graph = real nodes/edges, stage and transition selection and editing, add stage, update transition, add transitions (forward and terminal), invalid draft blocked with codes, valid draft, four-eyes, missing-permission enforcement, publish by second administrator, published/forged edits refused, binding impact + confirmation + refusal without confirmation + rebind, pinning in the real Work Order UI, rollback by rebinding, retire, discard, audit. Responsive/RTL: 6 pages × 1440/820/390 × EN/LTR and FA/RTL, no horizontal page overflow, graph canvas scrolls inside its container; localization: no Persian UI labels in EN pages and no untranslated English UI labels in FA pages; FA functional pass (clone, add stage, Persian validation/conflict/discard messages). Fixes made after QA: SVG colours now follow the app theme (they followed the OS dark scheme and made the selected node unreadable), SVG no longer shrinks on mobile, English validator texts.

## 19. Dynamic workflow proof
Through the designer only (no controller/service/view change): cloned Work Order Lifecycle v1 → v2; added stage "Reliability Review"; re-targeted Planning → Reliability Review; added Reliability Review → Preparation and a terminal Reject; an intentionally invalid intermediate state was rejected by the validator; validated; the last editor was refused as publisher; a second administrator published; binding changed to v2 with confirmation; a new Work Order showed the stage in the real Work Order UI and routed through it; the existing Work Order stayed on v1. Reverted by rebinding to v1 and retiring v2 (the Work Order that started on v2 keeps using it); new Work Orders use v1 again.

## 20. Help
Bilingual article `workflow-management` (administration category) covering definition, version, draft, stage, transition, permission, responsible role/employee, validation, publish, retire, binding, pinning, rollback-by-new-version/rebinding, security, concurrency, scope. It states that there is no participant table. Help count test updated 57 → 58.

## 21. Limitations
* Editing is form-based with full-page reloads; there is no drag-and-drop or free-form canvas editing.
* Layout is vertical layered; very large workflows produce a tall graph.
* Only Work Request, Work Order Lifecycle and Work Order Multi-Discipline Review are editable (owner decision); all other definitions are view-only, and their bindings are not administrable.
* `DescriptionFa` was not added (no existing pattern); definition-metadata edits carry no concurrency token (versions do).
* Platform exception messages outside the mapped cases (e.g. some validator-derived text embedded in publish errors) may appear in Persian in the English UI; the mapped set covers the common cases.
* Discarded drafts are kept as rows with status `Discarded` (no physical delete).
* The build/test runs for this phase used a separate output directory because the user's own PEMS.Web (port 5089) was running and locks the default `bin` folder; results are equivalent.

## 22. Deferred capabilities
Workflow version diff (deferred), full before/after audit snapshots, stored canvas layout, definition creation from scratch and definition clone from the UI, editing legacy definitions, participant model redesign (none exists by design).

## 23. Database changes
Migration `WorkflowAdminDefinitionNameFaVersionRowVersion` (2 nullable additive columns) applied to PEMSDB after inspection (9 definitions, 11 versions) and a copy-only backup (`C:\Users\Public\PEMSDB_pre_phase4.bak`, not modified). Only that migration was applied (previous head `20260920133743_WorkOrderGovernanceOrigin`). No reset, no deletion of history or instances. `WorkflowManagementSeeder` (permissions + SystemAdmin grants) runs at application start.

## 24. Tests
`WorkflowAdministrationTests` (14, in-memory): per-permission denial with a real SystemAdmin, non-SystemAdmin denial, editable vs view-only/unbound (no error state), metadata + NameFa + audit, clone → edit → validate → publish with four-eyes, editor rotation, immutability (published/retired, platform and facade), invalid workflow cannot publish, one open draft + discard (rows kept) + discard refusal with instance, stage/transition add/update/remove with preserved fields and cross-version rejection, graph generation/determinism/no overlap/branches/back edges, rebinding with confirmation and pinned instances/rollback. `WorkflowAdministrationConcurrencyTests` (3, real SQL Server, retrying strategy): stale save, stale add/remove/validate/discard, 20 simultaneous-save races. A recursion defect in the new save helper was caught by the first test run before any UI use.

## 25. Final closure audit (verified 2026-09-21)

| Gate | Result | Evidence |
|---|---|---|
| Build | PASS | `dotnet build PEMS.slnx --configuration Debug`: 0 errors, 22 warnings (none in Phase 4 files) |
| Full tests | PASS | 1762 passed / 0 failed / 0 skipped (baseline 1745; +17 = 14 `WorkflowAdministrationTests` + 3 `WorkflowAdministrationConcurrencyTests`; the Help count assertion changed 57 → 58 without a new test) |
| PEMSDB integrity | PASS | Backup `PEMSDB_pre_phase4.bak` unmodified (last write 2026-09-20 23:07); 9 definitions, 3 bindings, 39 stages, 93 transitions; Work Orders 1–3 and running review instance 74 intact; nothing deleted or reset |
| Migration | PASS | `20260920193708_WorkflowAdminDefinitionNameFaVersionRowVersion` applied; `WorkflowDefinitions.NameFa` and `WorkflowVersions.RowVersion` present; 2 AddColumn only (no CreateTable); it is the only migration applied by this phase |
| Administration UI | PASS | Browser QA on isolated DB (§18); definitions, versions, bindings, usage, audit |
| Visual designer | PASS | Graph nodes/edges equal persisted stages/transitions; theme-following colours and mobile scaling fixed after QA and re-verified at 1440/820/390 |
| Security | PASS | SystemAdmin AND `WorkflowManagement.*`; explicit-grant rule in `AccessControlService`; six permissions + six SystemAdmin grants seeded; forged POSTs verified (non-SystemAdmin, missing Publish grant, legacy clone, published edit) |
| Four-eyes | PASS | Last editor = latest `VersionCreated/VersionCloned/DraftEdited` audit row of the version; server refuses the same user, allows another (tests + browser) |
| Concurrency | PASS | Real SQL: stale save/add/remove/validate/discard rejected, 20/20 simultaneous-save races with one winner; two browser sessions EN and FA |
| Binding/Versioning | PASS | Impact page, explicit confirmation, unconfirmed change refused, existing instances stay pinned, rollback by rebinding to v1 |
| Dynamic proof | PASS | Reliability Review added via the designer only; new Work Order used v2, existing stayed on v1; reverted by rebinding + retiring v2; no Work Order controller/service/view change |
| Browser QA | PASS | 46/48 first run (2 harness artifacts re-verified passing), 48/48 responsive+localization, 8/8 FA functional |
| EN/LTR | PASS | 1440/820/390, no page overflow, no Persian UI labels |
| FA/RTL | PASS | 1440/820/390, no page overflow, no untranslated English UI labels |
| Help | PASS | `workflow-management` (EN+FA) in the administration category; 58-article test green |
| Audit | PASS | `WorkflowConfigAudits` reused; VersionCloned, DraftEdited, Validated, ValidationFailed, Published, Retired, BindingChanged, DefinitionUpdated, DraftDiscarded recorded |
| Architecture integrity | PASS | Only three platform entity files changed (2 columns + constants); no new tables/DbSets/engine/history/audit; controller has no EF/DbContext use |

Notes recorded during closure:
* An earlier full run used an output folder outside the repository and reported 3 failures (1759/3/0 of 1762): the source-guard tests walk up from the test output folder to find the repo's views and could not. They are environmental, not product failures; the rerun with output inside the repository passed 1762/0/0.
* PEMSDB grew during closure for reasons outside this phase: (a) the existing test suite writes EngineeringDocument test workflow instances into the live PEMSDB on every full run (pre-existing test behaviour; nothing deleted); (b) Work Order WO-2026-0004 (with lifecycle and review instances) was created on 2026-09-20 23:19 while Phase 4 QA used a separate database, consistent with the user's live instance; (c) at 00:25 on 2026-09-21 "admin" cloned Work Order Lifecycle v1 → v2 and discarded it in PEMSDB (a Discarded row and two audit rows) after the user's PEMS.Web restarted at 00:20 with the Phase 4 build — the live Workflow Management feature working; binding and published versions unchanged.

FINAL CLASSIFICATION: **ACCEPTED WITH DOCUMENTED LIMITATIONS**. BLOCKERS: none. NEXT PHASE: not started.
