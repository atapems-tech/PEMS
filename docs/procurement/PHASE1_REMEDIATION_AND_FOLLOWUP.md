# Procurement Lifecycle — Phase 1 remediation, policy decisions and bounded follow-up

Scope: Work Package A (ProcurementPackage closeout), B (PO status + SystemAdmin bypass), C (Part Catalogue Phase 1).
Nothing here was committed or pushed. The two earlier local commits (`47c2fd2`, `6bee674`) are untouched.

## 1. Work Package A — ProcurementPackage closeout

**Root cause.** `ProcurementPackageController.AdvanceStage` (permission: `ProcurementPackage.Edit`) called
`IProcurementPackageService.AdvanceStageAsync(packageId, "Closeout")`, and the Details view rendered a *Closeout* stage
button that posted to it. `AdvanceStageAsync` only checked that the stage name was in the list, then set
`Status="Closed"` + `ClosedDate`. That skipped the ITP inspection gate, the `Approve` permission and the SoD check that
the dedicated `Closeout` action enforces.

**Fix.**
- `AdvanceStageAsync` rejects `Closeout`, unknown stages, and any change on a `Closed`/`Cancelled` package
  (`InvalidOperationException`). The six working stages keep their previous behaviour (forward and backward moves, status `InProgress`).
- `CloseoutAsync` is now the *only* writer of `Stage=Closeout` / `Status=Closed` / `ClosedDate`; it no longer calls the
  public method, refuses already-closed / cancelled packages (no `ClosedDate` rewrite), and keeps the ITP gate.
- Controller `AdvanceStage` surfaces the rejection as `TempData["Error"]`; controller `Closeout` additionally runs the
  SoD-aware `AuthorizeAsync(ProcurementPackage.Approve, packageId)` (same as `AddApproval`), which also writes the audit row.
- The view no longer renders a Closeout stage button; the existing dedicated "Closeout Package" form is the only path.

**Behaviour change to be aware of.** A user who *created or edited* a package can no longer close it themselves
(`ProcurementPackage.Create ↔ Approve` SoD policy, seeded earlier, now also applies to Closeout).

**Policy gap (documented, not invented).** No rule requires a recorded `ProcurementApproval` before closeout, nor
defines what a "failed" inspection is beyond ITP `Status != "Closed"`. Enforcing either is a business decision.

## 2. Work Package B — PO status and SystemAdmin bypass

**PO status.**
- Manual `PurchaseOrderService.CreateAsync` now creates `Draft`.
- Import paths are unchanged and still create `Issued`: `VDRImportService` (direct entity) and `FindOrCreateByNumberAsync`;
  the entity default also stays `Issued`. No migration; no existing PO is touched (63 `Issued` POs on the dev DB remain as is).
- New validated transition `IssueAsync` (Draft → Issued only; inactive / non-Draft / unknown id are rejected) behind
  `PurchaseOrderController.Issue`: requires `PurchaseOrder.Approve` **and** a SoD-aware `AuthorizeAsync`; manual create writes the
  `PurchaseOrder.Create` audit row; seeded SoD policy `PurchaseOrder.Create ↔ PurchaseOrder.Approve`.
- **Policy decision (fail-closed).** Verified in the dev and clone DBs: *no role holds `PurchaseOrder.Approve`* (ContractManager has
  only Create/Edit). Therefore **issuing a new manual PO is currently impossible for everyone** until the organisation decides who may
  issue POs and grants it (Permission Matrix). No role, threshold or approver was invented or seeded.

**SystemAdmin bypass.** The global bypass is kept for everything except a closed, verified list
(`AccessControlService.RequiresExplicitGrant`): the existing break-glass cases (`ExecutionGate.EmergencyOverride`, all
`WorkflowManagement`) plus `Approve` on `ProcurementPackage`, `Tender`, `PurchaseOrder`, `Contracts`. Both decision paths
(role-only `DecideAsync`, user-aware `DecideForUserAsync`) use the same predicate. For those actions SystemAdmin needs an explicit
`RolePermissionGrant` (the documented emergency path — the grant is a visible, auditable admin action), and on the user-aware path
the grant is then subject to Scope and SoD and every decision is written to `AuthorizationAuditLogs`. **No SystemAdmin grant was seeded.**
Verified zero such grants exist today, so SystemAdmin loses silent approve rights on those four resources.

Also removed: the two `SystemAdmin` substitutions in `ContractWorkflowService` (discipline review by any role; approval-authority
rule `RequiredApproverRole`). The workflow runtime already has no SystemAdmin bypass (it throws `UnauthorizedAccessException`).

**Operational impact.** The ContractManager / EngineeringManager approver paths are unchanged. Any SystemAdmin routine that
approved packages, tenders or contracts without holding those roles will now be refused until an explicit grant exists.

**Not changed (listed gaps).** `TenderController` approvals still use role-only `HasPermissionAsync` (no SoD / audit) and
`TenderService.SelectWinnerAsync` has no SoD; `MR`/`IRN` have no SoD policy; `ProcurementApproval.ApproverName` is a string.

## 3. Work Package C — Part Catalogue Phase 1

| Item | Decision |
|---|---|
| Search existing parts | `IPartCatalogueDuplicateService.SearchExistingPartsAsync`, JSON `PartCatalogueDedup/ExistingParts`, panel on the request Edit page. Searches approved (Completed) requests + Material Master; ≥ 2 chars; capped at 50. |
| MPN lookup / duplicates | `FindDuplicateCandidatesAsync` (MPN, old MPN, supplier PN; normalised = letters/digits, upper-case). Placeholders (`NO_PART_NR`, `N/A`, `TBD`…) and < 3 chars never match. Kinds: SameMpnSameManufacturer / OtherManufacturer / ManufacturerUnknown / SameSupplierPartNumber. Pending requests of other users are only *counted*, never disclosed. |
| Alias table | **Not created.** Existing identifier columns cover today's need; an alias table is only justified when one part must carry several active MPNs (supersession / cross-reference) — not modelled today. If introduced later it needs a normalised-key unique index scoped to (manufacturer, normalised MPN) and concurrency handled via `RowVersion`. |
| No auto-merge | `IsAmbiguous` when ≥ 2 distinct parts share MPN + manufacturer; `AutoMergeAllowed` is always false; the service is read-only. |
| Request → `MaterialItem` link | **Not delivered (scope correction, see §8).** The first implementation stored it in a new nullable `PartCatalogueRequests.MaterialItemId`. That schema change was never approved/applied and it broke every query that loads the full request entity, so it was removed. The pre-existing read-time rule (`MaterialItem.MaterialCode == AssignedPartNumber`, used by `ResolveExistingPartAsync`; `MaterialCode` is unique but editable) is unchanged and is what the duplicate search uses to avoid counting a request and its material twice. |
| Demand origin | **Not delivered (blocked on a schema decision, see §8).** No existing field or relation can hold a typed origin (the only free-form structures are request attributes/notes — exactly the fragile polymorphic reference WP-C #7 forbids). |
| Migration | **None.** `20261009225034_PartCatalogueTraceability` was removed with the EF tooling (`migrations remove`); the model snapshot is identical to `HEAD`. The archived originals are under `docs/procurement/deferred/PartCatalogueTraceability/` (inert `.txt` files). |
| Preserved | Assigned part numbers, routes, imports/exports, history, `IWorkflowRuntime` + actor resolution (untouched), no seeded roles/employees/limits/approvers. |

Policy-dependent items deliberately **not** enforced: blocking submission when an exact duplicate exists, requiring a catalogue
part before purchase ("catalogue-before-purchase"), and whether demand origin is mandatory.

## 4. Deferred (not implemented) and bounded follow-up

| Deferred item | Dependency / what must be decided first | Bounded next step |
|---|---|---|
| Net-demand / replenishment engine | Reliable stock + on-order + reservation quantities; units of measure; planning method semantics | Read-only net-demand report per `MaterialItem` using existing inventory tables |
| `double` → `decimal` migration | Inventory the affected columns (balances, MR/IRN quantities); rounding policy | One entity group per migration with a data-compare script |
| PO line / receipt line / incoming-stock integration | `PurchaseOrder` has no lines; approved PO workflow (who issues) | Add `PurchaseOrderLine` (additive) after the Issue authority decision |
| AP / invoice / payment / ledger | Finance ownership and chart-of-accounts decisions | Out of scope until finance requirements exist |
| Organisational / financial authority rules | `ContractApprovalAuthorityRule` has 0 rows by design; PO issue authority undefined | Business sign-off, then seed via admin UI, not code |
| Tender approval SoD / audit | Decision on whether Tender needs the same SoD as Contracts | Mirror `ProcurementPackageController.AddApproval` pattern |

## 5. Test evidence

See the completion report delivered with this change for run counts, environment and failures.
New test files: `ProcurementPackageCloseoutSafetyTests`, `ProcurementApprovalBreakGlassTests`, `VDRImportPurchaseOrderStatusTests`,
`PartCatalogueDuplicateDetectionTests`, `PartCatalogueSchemaContractTests`; extended: `ContractWorkflowServiceTests`, `PartCatalogueServiceTests`,
`PurchaseOrderControllerAuthorizationTests` (constructor only).

---

## 6. Security hardening and acceptance gate (follow-up to the verification report)

### 6.1 Fail-closed protected actions (Tasks A and B)

**Root cause.** `ProcurementPackageController.Closeout` and `AddApproval` called `AuthorizeAsync` only when a user id existed and acted
only on `DeniedBySoD`; any other user-aware denial (`DeniedByScope`, `DeniedByRole` when the cookie role claim was stale, workflow/ownership
reasons) was ignored, and a missing user id skipped the check entirely. `PurchaseOrderController.Issue` was already fail-closed but had its
own copy of the logic.

**Change.** One shared guard, `PEMS.Web/Security/ProtectedActionGuard.EvaluateAsync`, now used by `Closeout`, `AddApproval` and `Issue`:

1. role check (`HasPermissionAsync`, unchanged behaviour) — failure is a denial;
2. a valid authenticated user id is required — otherwise denial;
3. user-aware `AuthorizeAsync` (Scope + SoD + audit row);
4. only `Allowed == true && Reason == Allow` proceeds. Everything else — scope/role/workflow/ownership denial, `null` result, an
   inconsistent result, or an exception while evaluating — is a denial (`Forbid`). The single exception to `Forbid` is a SoD denial, which keeps the
   existing friendly redirect with `TempData["Error"]` (still a denial, no mutation), exactly like the previous `Issue`.

No service logic, ITP gate, state transition or SoD policy was changed. The package SoD message now says "created" instead of
"created/edited", because only `Create` is paired with `Approve` (seeded policy).

**Denial auditing.** `IAccessControlService` gained an additive default-interface method `RecordDenialAsync` (implemented by
`AccessControlService`) that writes one `AuthorizationAuditLog` row (decision, reason text, user id or null, resource, IP) — the same table
`AuthorizeAsync` writes. The guard uses it for role-only denials and missing identity, which previously left no trace. Audit failure is logged
and never turns a denial into an allow. Exactly one row per denied attempt (user-aware denials are written by `AuthorizeAsync` itself).

**Still not audited (documented, not changed).** Role-only denials in all other controllers (~16, including every `Tender` action, `ContractController`, the
`Edit`-level actions of the three modules) — the guard is available to adopt them.

### 6.2 `FindOrCreateByNumberAsync` (Task C)

Call graph (source search + Graphify): declaration `IPurchaseOrderService`, implementation `PurchaseOrderService`, **no production caller**; the only reference is a test.
It is in the initial import with the comment "used for Import". Decision: import/reconciliation-only. Behaviour is unchanged (creates `Issued`, idempotent by
trimmed, case-insensitive number); the interface now documents that it must never be used for operational creation, and `PurchaseOrderCreationPathTests`
fail if any production code starts calling it or constructing a `PurchaseOrder` outside `PurchaseOrderService` and `VDRImportService`. Manual creation stays `Draft`;
no data or migration touched.

### 6.3 `PurchaseOrder.Approve` ownership (Task D) — recommendation only, nothing granted

**Existing facts (dev DB `PEMSDB`, read-only).**
- No role holds `PurchaseOrder.Approve`; no SystemAdmin Approve grants exist. There is no role named for procurement/PO approval among the 21 roles. `ContractManager` has PO Create/Edit.
- `Positions` has no role/permission mapping column. Authority is given to *users* through roles (`AspNetUserRoles`, scoped `RoleAssignments`) and the Permission Matrix.
  `WorkflowActorResponsibilities` map positions to workflow responsibilities, and all current ones are Part Catalogue steps; there is no PO workflow.
- Procurement-like positions: `PC-PRC-MGR` *Procurement Manager* (primary holder employee 63 "FPA TestC", **no user account**) and `PC-FIN-DIR` *Finance Director*
  (secondary assignment to the administrator account). Both were created on 2026-10-09 together with other `PC-*` positions assigned to test-named employees,
  so they look like fixture data from earlier provisioning, not an approved organisation chart.
- The supplied spec (the proposed-approver table) lists "PO Approver / Procurement Manager; high value: Finance Manager" for PO approval and marks it **"not approved —
  amount limits unknown"**, and says position names must not be assumed to be system roles.

**Can the current model represent it without schema?** Yes for *who*: grant the `PurchaseOrder` module's **Approve** action to one role (an existing role the
organisation designates, or a new role created in the Roles administration page and assigned to named users). No for *how much*: there is no PO value-limit/authority table
(contracts have `ContractApprovalAuthorityRule`; POs have nothing), so any grant today means unlimited issue authority.

**Exact action once an owner is approved.** Administration → Permission Matrix → select the approved role → module **PurchaseOrder** → tick **Approve** → save
(`PermissionMatrixController.SetRole`; it posts that role's *entire* permission set, so review the whole row before saving). The grant syncs to `RolePermissionGrant`.
Do not give the same person Create and Approve for the same PO: the seeded SoD policy will block self-issue of a PO they created.

**Open business decisions (why nothing was granted).** (1) Which role/position owns PO issuance (named role and named people). (2) Whether value limits apply and who
approves above them (needs a rule table or an approved "no limit" decision). (3) Whether the SystemAdmin emergency grant is wanted at all, and who may create it.
Until then PO issuance is intentionally unavailable; manual POs remain `Draft`.

## 7. Residual risks (not fixed here)

| # | Risk | Notes |
|---|---|---|
| R1 | Tender has no SoD or audit: `Approve` actions (`ApproveNegotiation`, `SelectWinner`, `CreateContract`) are role-only; `Tender.Edit` actions remain under the SystemAdmin bypass; one user can create, evaluate, select and create the contract. | Mirror the package pattern with `ProtectedActionGuard` once Tender SoD pairs are approved. |
| R2 | `SelectWinnerAsync` accepts a bid with *any* passed technical and commercial evaluation even when failed evaluations of the same type coexist; later evaluations do not supersede earlier ones. | Needs a business rule (latest wins vs. all must pass). |
| R3 | `EvaluatorId` is nullable; evaluations can be recorded without an employee. | `GetCurrentEmployeeIdAsync` may return null; decide whether to require an employee. |
| R4 | SoD relies on a prior `Create` audit row for the same record; imported/historical packages and POs have none, so SoD cannot apply to them. | No backfill; decide whether historical records need an explicit exemption note. |
| R5 | Demand-origin authorization is module-level `View`, not record-level. | WorkOrder/StoreRequisition/MR have no record-level visibility model today. |
| R6 | Concurrent `Issue` requests: `IssueAsync` re-reads the PO, so a sequential duplicate gets a friendly error; a true race fails on the `RowVersion` token with `DbUpdateConcurrencyException`. No controller or service catches it; only the global `UseExceptionHandler("/Home/Error")` applies, so the loser sees a generic error page. No double issue. | Verified by search; fix = catch it in `IssueAsync` and rethrow `InvalidOperationException`. |
| R7 | Role-only denials outside the three protected actions are not audited (see 6.1). | |
| R8 | `AddCommunication`/`AddApproval` on a `Cancelled` package moves it to `InProgress`; nothing sets `Cancelled` today. | Latent. |

## 8. Scope correction — Part Catalogue schema change withdrawn

WP-C #6 (link to `MaterialItem`) and #7 (demand origin) were first implemented with four new nullable columns on `PartCatalogueRequests`
(`MaterialItemId`, `SourceWorkOrderId`, `SourceStoreRequisitionId`, `SourceMaterialRequisitionId`) plus a migration. The migration was deliberately never applied to
`PEMSDB`, but the columns were mapped on the core entity, so every full-entity query (details, edit, save, approve…) against `PEMSDB` failed with
`Invalid column name`, while projection-based queries (dashboard list/summary) kept working. The user's requirement is to keep existing Part Catalogue
behaviour working without an unapproved schema change, so:

* removed: the four properties, their EF mapping, the migration (via `dotnet ef migrations remove`), the snapshot delta, the link/origin services, controller actions,
  view cards, resource keys, auto-link in the approval path and the related tests (archived, not deleted, under `docs/procurement/deferred/`);
* kept (schema-free): normalised part-number search, MPN duplicate-candidate detection, the JSON endpoints and the Edit-page search panel;
* WP-C #6/#7 are therefore **not delivered**. If they are wanted later, the safer design is a separate additive side table (one row per request) so the core entity and
  its queries never depend on the migration, delivered only after the schema change is explicitly approved and with an "old schema + new code" compatibility test.
* guard: `PartCatalogueSchemaContractTests` fails if the request model maps these columns again or if the details query would reference them.
