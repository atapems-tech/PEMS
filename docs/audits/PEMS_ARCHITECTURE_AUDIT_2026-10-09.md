# PEMS — Architecture, Security & Production-Readiness Audit (2026-10-09)

Scope: evidence-based audit on branch `feat/cmms-passport-global-page-header` (HEAD `13ee6b6` + uncommitted work). Static source inspection + Graphify + automated tests + live UI smoke on an isolated database. **No penetration test, load test, soak test, backup/restore drill or UAT was performed.**

## 1. Baseline

* Branch `feat/cmms-passport-global-page-header`, HEAD `13ee6b6`; uncommitted Employee/Discipline/Position/authorization work plus this audit's fixes. `PEMS.Web/Properties/launchSettings.json` carries the user's own local edit and must not be committed with this work.
* Graphify built for HEAD (35,659 nodes / 73,293 edges); refreshed after the Employee/Position work; must be refreshed once more after the audit fixes (see §10).
* 207 EF migration files; the two newest (`OrganizationPositionHierarchy`, `OrganizationAssignmentEmployeeIndex`) are Phase-1 org migrations, already applied earlier to dev. **This audit adds no migration.**
* `PEMSDB` (dev) is **being actively edited by the user through the running application** (new Petrochemical positions `PC-CEO`, `PC-OPS-DIR`, `PC-FIN-DIR`…, employees `00000/01000/02000`, matching users and a RoleAssignment, created 10:32–12:14 on 2026-10-09). The earlier "unchanged" baseline (73/62/249/10/13/20) is therefore superseded: 73 departments / 63 employees / 249 disciplines / 19–20 positions / 14 users / 21 role assignments at the time of check. These rows were created through the UI, not by tests (all tests and smoke runs use `PEMSDB_*` isolated databases; the test factory refuses `PEMSDB`). They must be preserved.

## 2. Evidence matrix (hypotheses from existing audits → current code)

| # | Finding | Source | Current code evidence | Test evidence | Classification | Decision |
|---|---------|--------|----------------------|---------------|----------------|----------|
| 1 | Work-order release gate accepts a single Verified isolation point | new | `WorkOrderReleaseGateService` used `AnyAsync(Status=="Verified")`; `ExecutionGateService` requires all active, non-cancelled points | Phase6 tests covered only 0 or 1 point | **Confirmed defect (P1)** | **Fixed** + 2 tests |
| 2 | Legacy `AdvanceStatusAsync` completes Corrective/Emergency WOs without ISO 14224 failure mode | CLAUDE.md rule | Governed path rejects; legacy path only synced FailureRecord *if* a code was supplied | none | **Confirmed defect (P1)** | **Fixed** + 2 tests |
| 3 | Legacy completion has no concurrency handling (`RowVersion` conflict → unhandled exception) | new | `SaveChangesAsync` without `DbUpdateConcurrencyException` catch | none | Confirmed defect (P2) | **Fixed** (conflict message, same as governed path) |
| 4 | Position replacement not atomic | this session | `SetPrimaryPositionAsync` ended old assignment then assigned new one in separate saves | InMemory only | Confirmed defect (P1) | **Fixed** (transaction inside `CreateExecutionStrategy`), SQL-backed test added; live smoke caught and fixed a retrying-strategy bug |
| 5 | AI/OCR jobs unbounded & non-idempotent | PERFORMANCE_AUDIT_V1 | 3 `BackgroundJob.Enqueue` sites; no `[AutomaticRetry]`, queue, `DisableConcurrentExecution` or semaphore; default Hangfire workers (≤20) share SQL and Ollama (15-min timeout); `ProcessAttachmentAsync` re-runs regardless of `AIStatus` | none | Confirmed architectural risk (P1) | **Documented only** — needs deployment decision (worker count / queue) |
| 6 | MRS 5,000-row commit exceeds 60 s | test | Test is **InMemory**, so SQL is not the cause. Chunking (100 rows) and per-chunk `ChangeTracker.Clear()` already exist. Observed 89 s alone, 588 s under load | `MrsImportBatchCapacityTests` fails | Confirmed performance defect; **root cause not isolated** (P1) | **Open**; threshold untouched |
| 7 | Audit log written in a second `SaveChanges` | PERFORMANCE_AUDIT_V1 | `PEMSDbContext.SaveChangesAsync` saves business data, then saves `AuditLog` rows; atomic only when the caller holds a transaction | none | Confirmed architectural risk (P2): a failure/crash between saves loses the audit row | Documented |
| 8 | `ApplyCreatedDate` overwrites `CreatedDate` on every Added entity (reflection per entity per save) | new | `PEMSDbContext.ApplyCreatedDate` | none | Confirmed (P2/P3): import fidelity + per-save reflection cost | Documented |
| 9 | Release gate evaluated only at release and only when the JobPlan flags Permit/LOTO; Permit→Active enforced by `ExecutionGate` at controller level only | new | `WorkOrderReleaseGateService` early-returns without JobPlan/flags; `PermitService.AdvanceStatusAsync("Active")` has no service-level gate (controller blocks it); no revalidation if a permit expires/suspends while the WO is InProgress | partial | Partially confirmed design gap (P2) | **Business decision required** — not changed |
| 10 | `TenantId` not enforced | prompt | `TenantId` on `BaseEntity` but zero references in Persistence/Web query code; no `HasQueryFilter` | none | Confirmed (P3 if single-tenant) | Documented; do not claim multi-tenant isolation |
| 11 | Redis mandatory for scale-out | PERFORMANCE_AUDIT_V1 | Only 2 `ICacheService` consumers (`RoutingRuleService`, `LetterController`); memory cache fallback is explicit | none | Partially confirmed — overstated (P3) | Do not mandate Redis |
| 12 | SignalR `JoinUserGroup(userId)` | historical | Hub is `[Authorize]`, group derived from authenticated identity only | — | **Historical, resolved** | None |
| 13 | `[AllowAnonymous]` on whole AccountController | historical | Now per-action; global `AuthorizeFilter`; only Login/Culture/health-style endpoints anonymous | `FileSecurityHttpTests` etc. | **Historical, resolved** | None |
| 14 | Hangfire dashboard exposure | PERFORMANCE_AUDIT_V1 | `HangfireDashboardAuthFilter` = authenticated SystemAdmin only | — | Not a defect | None |
| 15 | Secrets in repo | prompt | `appsettings*.json` use integrated security, empty passwords/Redis | — | Not reproducible | None |
| 16 | WorkOrder legacy path bypasses governance | prior audits | Legacy path refused for governed WOs and for WOs created after the governance cutoff | `WorkOrderGovernance*` tests | Historical, resolved | None |

## 3. Verified architecture map (condensed)

* Layers as documented (Domain → Application → Persistence → Web; AI, Infrastructure, WorkflowPlatform, Shared). Business logic sits in Persistence services; controllers do permission checks and delegate.
* WorkOrder lifecycle: centralized `WorkflowRuntime` governs new WOs (transition permission + organization + discipline + SoD hook); legacy `LegalTransitions` map remains for pre-governance records only. Legacy map: Open→Assigned|Cancelled|Rejected; Assigned→InProgress|OnHold|Cancelled; InProgress→Completed|OnHold|Cancelled; OnHold→InProgress|Cancelled; Completed→Closed.
* Permit lifecycle: Draft→Submitted→UnderReview→Approved→Issued→Active⇄Suspended→Closed, with Cancelled/Rejected/Revoked; Active only via `ExecutionGate` (isolations all Verified, valid gas test, no unresolved SIMOPS, not expired); sensitive actions (Close, LOTO verify) go through `PermitAuthorityService` incl. WebAuthn.
* Sources of truth: position = `EmployeeAssignment` (legacy `Employee.Position` is display-only, mirrored on SystemAdmin assignment); roles = Identity roles ∪ active `RoleAssignment`, kept in sync by `IUserRoleAssignmentSynchronizer`; permissions = `RolePermissionGrant` (legacy `RolePermission` matrix synced), navigation = `MenuAccess` + `NavigationVisibilityOverride` + permission filter; Position/Department/Discipline never grant permissions.

## 4. Prioritised backlog

| Priority | Finding | Impact | Proposed fix | Test plan |
|---|---|---|---|---|
| P1 (done) | Release gate partial isolation | LOTO considered satisfied with unverified points | Align with ExecutionGate (all active non-cancelled Verified) | `Phase6ExecutionTests` (4 gate tests) |
| P1 (done) | Legacy completion w/o ISO 14224 | Missing failure data for MTBF analysis | Reject at service | 2 lifecycle tests |
| P1 (done) | Non-atomic primary-position replacement | Employee left without primary on failure/race | Transaction in execution strategy | `PositionAssignmentSqlTests` (real SQL, retry strategy) |
| P1 (open) | AI/OCR job isolation | Ollama/SQL contention, duplicate processing | Dedicated Hangfire queue + capped workers, `[AutomaticRetry(Attempts=2)]`, per-attachment `DisableConcurrentExecution`, skip when `AIStatus` Completed/Processing unless explicit reprocess | Concurrent-enqueue test + load test (5/10/20/50 jobs) while measuring web P95 |
| P1 (open) | MRS import capacity | 5,000-row import beyond SLA; 89 s alone vs 588 s loaded | Profile first (dotTrace/`dotnet-counters`): suspect audit preparation (`ChangeTracker.Entries()` + reflection per entity per save) and per-chunk lookups; compare InMemory vs SQL; then fix the dominant cost | Keep 60 s threshold; add per-phase timing output |
| P2 | Audit write atomicity | Missing audit rows on crash | In `SaveChangesAsync`, open a transaction when none exists (inside execution strategy) so business+audit commit together | SQL fault-injection test |
| P2 | Release-gate scope/revalidation | WO without flagged JobPlan skips permit/gas checks; no revalidation during execution | **Needs HSE/operations decision**, then gate on linked-permit requirements regardless of JobPlan flags and add periodic revalidation | Scenario tests per decision |
| P2 | Service-level Permit→Active guard | Defense in depth only | Reject `Active` in `PermitService.AdvanceStatusAsync` unless called by ExecutionGate | Unit test |
| P2 | Test gaps | 22 controllers without a test reference; few SQL-backed tests | Prioritise authorization negative tests for Permit/WorkOrder/MRS; SQL tests for unique/filtered-index invariants | — |
| P3 | `TenantId` unused, `CreatedDate` override, Redis | Misleading / minor | Document or remove; make `ApplyCreatedDate` respect supplied values for imports; Redis only if multi-instance is chosen | — |

## 5. Production-readiness status

| Area | Status |
|---|---|
| Server-side authorization on WorkOrder/Permit transitions | Implemented; automated-test verified (not penetration-tested) |
| Position / Discipline / Employee management | Implemented; automated-test verified; live-UI verified on isolated DB (EN/FA) |
| Isolation-gate fixes (this audit) | Implemented; automated-test verified (210 focused tests pass) |
| Load / stress / soak (`PERFORMANCE_AUDIT_V1` matrix) | **Not run** |
| AI/OCR queue isolation | **Not implemented** |
| MRS import capacity | **Failing test, not resolved** |
| Backup/restore, monitoring/alerting, runbook rehearsal, UAT | **Not verified** (runbook exists: `docs/deployment/PEMS_UAT_DEPLOYMENT_RUNBOOK.md`) |
| Multi-tenant isolation | **Not provided** (TenantId unenforced) |

Conclusion: **not production-ready**; suitable for controlled UAT after the open P1 items are decided/fixed and a load test on the real topology is executed.

## 6. Decisions required from the business

1. HSE/operations: should a Work Order with a linked permit that requires a gas test/isolation be blocked at release even when its JobPlan does not flag Permit/LOTO? Should permit expiry/suspension during execution halt or flag the Work Order?
2. Deployment: number of Hangfire workers / dedicated AI server; single-instance vs multi-instance (decides Redis).
3. Executive Office department (see `ORGANIZATION_IMPORT_EXECUTIVE_OFFICE_FINDING.md`). Note: a department named "Executive Office" (code `EO`, created 2026-10-07) now exists in the dev database; the finding's "does not exist" statement is outdated for dev data, and the workbook import preview must be re-run against the current department list.
