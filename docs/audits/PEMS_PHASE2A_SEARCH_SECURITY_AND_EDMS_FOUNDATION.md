# PEMS Phase 2A — Global Search Security, EDMS Command Bar, Pending My Action Foundation

*Continues from `docs/audits/PEMS_PORTAL_UX_AZURE_INSPIRED_AUDIT.md` (Phase 1 shell-consistency work already complete and tested — not repeated here). Starting state re-confirmed at the top of this phase: branch `main`, HEAD `aa5a768`, Phase 1's uncommitted shell changes present, build green, 1151/1151 tests green.*

## A. Search Security Findings

Reconfirmed against HEAD by reading all 19 provider files directly (not relying on the prior audit's summary). Two classes of gap, evidence-based, not assumed:

1. **Row-level scoping bypass** — `LetterSearchProvider` and `EngineeringDocumentSearchProvider` (plus their dependents `DocumentRevisionSearchProvider` and `AttachmentSearchProvider`) performed **zero** authorization filtering, while their entities' primary browsing pages (`LetterController.Index` → `LetterService.SearchAsync`, `EngineeringDocumentsController.Index`/`Details` → `ScopeToOwnDisciplineIfRestrictedAsync`) both enforce real, existing row-level rules (confidentiality+ownership+referral for Letters; discipline+`ViewAllDisciplines` permission for Engineering Documents). A user could search their way around a restriction the normal UI already enforces.
2. **Module-permission bypass** — `WorkOrderSearchProvider` and `MaterialRequisitionSearchProvider` returned results to any authenticated user, while their entities' primary controllers (`WorkOrderController.Index`, `MaterialRequisitionController.Index`) both check `HasPermissionAsync(roles, module, "View")` before showing anything. A role with zero WorkOrder/MaterialRequisition access (e.g. `QAManager` for WorkOrder, `Viewer` for MaterialRequisition — confirmed via the real seeded permission matrix, not assumed) could still find and read these records via search.

**Explicitly NOT changed, with reasoning:** `VdrItemSearchProvider`, `TechnicalQuerySearchProvider`, `MOCSearchProvider`, `VendorSearchProvider`, `SparePartSearchProvider`, `EquipmentSearchProvider`, `EquipmentTagSearchProvider`, `ContractSearchProvider` (view-only), `EmployeeSearchProvider`, `DepartmentSearchProvider`, `DisciplineSearchProvider`, `ProjectSearchProvider`. Verified by direct inspection that `VDRController`, `TechnicalQueryController`, `MOCController`, `VendorController`, `SparePartController`, `EquipmentController` have **zero** authorization checks of any kind today (no `[RequirePermission]`, no `[Authorize(Roles=...)]`, no inline `HasPermissionAsync`) — confirmed via grep, not assumed. Adding a permission gate to their search providers alone, while the entities' own primary pages remain fully open to any authenticated user, would make search *more* restrictive than direct navigation — inconsistent, and out of this phase's scope (fixing those six controllers' base authorization is a separate, larger initiative). `Employee`/`Department`/`Discipline`/`Project` are reference/master data with no restriction anywhere else in the app either (used broadly across dropdowns system-wide). `NotificationSearchProvider` was already correctly user-scoped and untouched.

## B. All 19 Providers (final disposition)

| Provider | Category (per brief §6) | Change |
|---|---|---|
| `LetterSearchProvider` | D — Permission + Resource | **Fixed**: replicates `LetterService`'s exact confidentiality/ownership/referral rule, applied to both the LIKE and semantic-search query paths |
| `EngineeringDocumentSearchProvider` | C — Resource (discipline) | **Fixed**: replicates `ScopeToOwnDisciplineIfRestrictedAsync`'s exact rule, applied to FTS/LIKE/fallback/semantic query paths |
| `DocumentRevisionSearchProvider` | C — Resource (via parent doc) | **Fixed**: same discipline rule via `x.Document.DisciplineId` |
| `AttachmentSearchProvider` | C+D — Resource (via whichever parent) | **Fixed**: applies discipline rule when linked to a document, confidentiality rule when linked to a letter, no restriction while unlinked (pre-link Intake state) |
| `WorkOrderSearchProvider` | A — Permission only | **Fixed**: added `HasPermissionAsync(roles, "WorkOrder", "View")` gate |
| `MaterialRequisitionSearchProvider` | A — Permission only | **Fixed**: added `HasPermissionAsync(roles, "MaterialRequisition", "View")` gate |
| `NotificationSearchProvider` | E — User-specific | Already correct; untouched |
| `VdrItemSearchProvider`, `TechnicalQuerySearchProvider`, `MOCSearchProvider`, `VendorSearchProvider`, `SparePartSearchProvider`, `EquipmentSearchProvider`, `EquipmentTagSearchProvider`, `ContractSearchProvider` | F — Global (matches their controllers' current, unrestricted behavior) | Untouched — see §A reasoning |
| `EmployeeSearchProvider`, `DepartmentSearchProvider`, `DisciplineSearchProvider`, `ProjectSearchProvider` | F — Reference/master data | Untouched |

## C. Authorization Model Used

Exclusively the **existing** mechanisms — no new authorization framework, per the brief's own explicit instruction:
- `IPermissionService.HasPermissionAsync(roles, module, action)` — the module-level permission matrix.
- `IAccessControlService.AuthorizeForRolesAsync(roles, resource, action)` — the finer-grained, data-driven permission model (used specifically for `EngineeringDocuments`/`ViewAllDisciplines`, which has no legacy `RolePermissions` equivalent).
- Both were already usable from `PEMS.Persistence` (search providers live there) via existing DI registrations — no architectural boundary was crossed.

## D. Project Scoping Model

**Finding, confirmed by direct inspection, not assumed:** PEMS has **no enforced project-membership/access-control mechanism anywhere in the codebase today.** No `ProjectMember`/`UserProject`/`ProjectAccess` entity exists. `IAccessControlService.AuthorizeAsync`'s own XML doc comment states plainly that Scope evaluation "همیشه عبور می‌کند" (always passes) in the current phase of that system's own rollout — it's an intentional stub, not a bug. `ProjectId` is a categorization/filter field on many entities, not a security boundary anywhere in the app.

Given this, the pre-existing, optional `projectId` parameter already present on most `ISearchableEntityProvider.SearchAsync` signatures is correctly classified as a **convenience filter** (used by picker widgets scoped to a specific project context in a form), not a security control — and was left as-is. Inventing a real project-membership restriction inside search alone, when nothing else in the application enforces one, would be a new authorization concept unilaterally added in the wrong layer — exactly what the brief's §5 warns against ("Do not blindly add ProjectId filters... where the application has project-level access, search must respect it" — the finding here is that the application does **not** have project-level access anywhere, so there is nothing for search to inconsistently under- or over-enforce).

## E. Changes Per Provider

See §B table. Two small shared helpers were extracted (not duplicated across providers) rather than repeating the same rule three or four times:
- `EngineeringDocumentDisciplineScope` (new, internal, `PEMS.Persistence.Services.SearchProviders`) — used by `EngineeringDocumentSearchProvider`, `DocumentRevisionSearchProvider`, `AttachmentSearchProvider`.
- `LetterConfidentialityScope` (new, internal, same namespace) — used by `LetterSearchProvider`, `AttachmentSearchProvider`.

`ICurrentUserContext` (existing, `PEMS.Application.Interfaces`, previously used only by `NotificationSearchProvider`) was extended with one new member, `Roles`, since permission checks need it and none of the fixed providers had access to the caller's roles before. Its single real implementation (`PEMS.Web.Security.CurrentUserContext`) was updated to expose it from the already-injected `ICurrentUserService.Roles`. No new abstraction was introduced — this is the brief's own preferred path ("If an existing abstraction can be extended safely, extend it").

## F. Command Bar Refactoring — STOP, not built (see §27 of the brief)

The Engineering Document Details command bar (`Details.cshtml`, ~110 lines) mixes four structurally different action kinds in the same block: plain `<form>` posts (Check/Reject/Approve/IdcRespond, each with different hidden fields), modal-trigger buttons (Submit/Revise/Issue/ClientResponse/Freeze/Hold/NewRevision), a plain link (Edit), and a form styled as a dropdown item (AnalyzeRelationships) — plus embedded conditional text that replaces the buttons entirely in one state (IDC Review, waiting-on-other-disciplines badge). No TagHelper-with-child-content pattern (the correct, idiomatic ASP.NET Core mechanism for a genuinely reusable "shell with caller-provided heterogeneous content") exists anywhere in this codebase today — confirmed by the Phase 1 audit's own explicit finding ("No dedicated TagHelper or Razor HtmlHelper extension exists").

Forcing all of this through a single scalar-typed ViewModel (label/icon/style/action) to make it "data-driven" would either lose fidelity (can't represent the IDC waiting-badge case, can't represent forms with 3 different hidden-field sets cleanly) or require building a small bespoke templating mechanism from scratch inside this phase — real behavior-change risk on PEMS's most complex, highest-traffic document-control page, which the brief's own §27 explicitly lists as a valid reason to stop ("Command bar extraction would change behavior").

**Recommendation for a dedicated future phase:** introduce a `PemsCommandBarTagHelper` (or an MVC View Component) supporting child content via `context.GetChildContentAsync()`/render fragments, as the *first* instance of this pattern in the codebase, prototyped on a simpler page first (e.g. Letters' already-clean command bar) before migrating Engineering Documents' more complex one. Not attempted here.

## G. Pending My Action Architecture

**Audited first, per §14 of the brief.** `HomeController.Index()` already has a real, working "My Work" section (`ViewBag.MyWork`, `MyWorkSummary` model) covering Permits (pending approval), Work Orders (assigned to me), and Letter Referrals (open, addressed to me) — each a live query against existing workflow/assignment fields, not a persisted task table. This is exactly the pattern the brief asks for (§17: "reuse existing workflow/task infrastructure... do not create a duplicate workflow engine").

**Gap found and closed this phase:** Engineering Documents had no equivalent — confirmed zero per-user "pending my action" query existed for EDMS anywhere in the codebase (VDR/TechnicalQuery/MOC remain gaps, not addressed this phase — see §K).

**What was built** (no schema change): `MyWorkSummary` gained `ShowDocumentReviews`/`PendingDocumentReviewsCount`. `HomeController.Index()`'s existing "My Work" try/catch block now additionally computes, via two targeted `CountAsync` queries (not a full-table load-then-filter):
- If the user holds `EngineeringDocuments`/`ViewAllDisciplines` (Manager/Supervisor/DocumentController — confirmed via the real `AccessControlService`, not assumed): count of documents in `Submitted`/`Checked` status system-wide (the pool anyone with reviewer/approver eligibility might act on).
- Otherwise (a plain Discipline Engineer, IDC participant): count of documents in `IDC Review` status where `DisciplineId` matches the user's own discipline (or is unassigned) — the exact same rule as `EngineeringDocumentDisciplineScope` used for search (§E), applied here as a live COUNT query rather than duplicated logic with different semantics.

This surfaces as a fourth tile in the existing "My Work" card (`Home/Index.cshtml`), styled identically to the three existing tiles (same `kpi-icon`/count/label/link pattern) — not a new UI pattern, not a redesign of the page, per §18's explicit instruction. Clicking it navigates to the Engineering Documents list, consistent with how the three existing tiles already behave (they link to a filtered list page too, not a single resource — this phase did not change that established interaction model).

**Query design (`GetPendingActionsForUser`-equivalent, §16):** implemented inline in `HomeController` rather than as a separate named service method, matching how the three pre-existing categories (Permit/WorkOrder/Referral) are already structured in the same method — extracting all four into a dedicated `IPendingActionsService` was considered but deferred (see §K) since it would mean also refactoring the three working, tested, pre-existing categories in the same pass, which is unnecessary risk for this phase's stated scope.

## H. Tests

New test files, all against real seeded permission/discipline data (the same `AccessControlParityTests` setup pattern — real `AccessControlService`/`PermissionService`/`AccessControlSeeder`, not hand-rolled fixtures):

```
PEMS.Tests/Search/SearchProviderSecurityTests.cs   16 tests
PEMS.Tests/Engineering/TransmittalRelatedDocumentQueryTests.cs   3 tests
```

Search security tests cover (mapping to the brief's own §8/§21 required scenarios):
- Authorized user finds a confidential/restricted resource; unauthorized user does not (Letter ×2, EngineeringDocument ×2).
- Owner/referred-to user can still find their own confidential letter even without the broader permission (2 tests) — proves the fix didn't over-restrict.
- Cross-discipline denial for Engineering Documents and Document Revisions (2 tests).
- Module-permission denial/allow for WorkOrder and MaterialRequisition (4 tests) — using the **real** seeded matrix (`QAManager` genuinely has no WorkOrder row; `Viewer` genuinely has no MaterialRequisition row — confirmed by reading the actual seed code, not assumed).
- Attachment inherits both parent rules correctly, and is *not* over-restricted while still unlinked during Intake (2 tests).
- No-regression check: an unauthorized user can still find ordinary Public letters (1 test) — proves the fix didn't break the common case.
- Notification stays user-scoped (1 test, regression guard on the one provider that was already correct).

One genuine, non-security bug was caught *by* writing these tests, not before: `SaveWithConcurrencyCheckAsync` (unrelated to this phase — actually, correction: this was the prior phase's fix, not this one; the bug caught *this* phase was purely a test-data issue — see §I) — no production bug was found in this phase's own new code; all failures during development were test-data setup issues (see Remaining Risks, §J, for the one real infrastructure quirk this surfaced).

## I. Performance Findings

- No N+1 queries introduced. Each fix adds at most one or two additional `CountAsync`/`Where`-clause conditions to queries that already existed, or one small lookup (`Employees.FirstOrDefaultAsync` for discipline resolution, `LetterReferrals` id list) computed once per search call, not per row.
- `GetForDocumentAsync` (new Transmittal query) is a single targeted query against `TransmittalItems` filtered by `DocumentId`, not a load-everything-then-filter — confirmed by design and by the passing "unrelated document returns empty" test.
- The new Home dashboard tile adds exactly one or two `CountAsync` calls (mutually exclusive branches, never both) inside the existing try/catch block — no change to the *number* of round trips for the three pre-existing categories.
- **Diagnostic finding, not a production issue:** while writing the WorkOrder/MaterialRequisition positive tests, `.Include(x => x.Equipment)`/`.Include(x => x.Discipline)` on EF Core's **InMemory** provider was found to silently exclude the parent row when the required (non-nullable `int`) FK pointed at a non-existent related row — purely a test-data-completeness artifact of the InMemory provider (real SQL Server's FK constraint would never allow such a row to exist in the first place). Documented in project memory so it doesn't cost debugging time again; not a code change, not a production risk.

## J. Files Changed

```
PEMS.Application/DTOs/TransmittalDto.cs                                   (+RelatedTransmittalDto)
PEMS.Application/Interfaces/ICurrentUserContext.cs                        (+Roles)
PEMS.Application/Interfaces/ITransmittalService.cs                        (+GetForDocumentAsync)
PEMS.Persistence/Servises/SearchProviders/AttachmentSearchProvider.cs     (security fix)
PEMS.Persistence/Servises/SearchProviders/DocumentRevisionSearchProvider.cs (security fix)
PEMS.Persistence/Servises/SearchProviders/EngineeringDocumentSearchProvider.cs (security fix)
PEMS.Persistence/Servises/SearchProviders/LetterSearchProvider.cs         (security fix)
PEMS.Persistence/Servises/SearchProviders/MaterialRequisitionSearchProvider.cs (security fix)
PEMS.Persistence/Servises/SearchProviders/WorkOrderSearchProvider.cs      (security fix)
PEMS.Persistence/Servises/SearchProviders/EngineeringDocumentDisciplineScope.cs (new, shared helper)
PEMS.Persistence/Servises/SearchProviders/LetterConfidentialityScope.cs   (new, shared helper)
PEMS.Persistence/Servises/TransmittalService.cs                          (+GetForDocumentAsync)
PEMS.Web/Controllers/EngineeringDocumentsController.cs                   (+ITransmittalService DI, Transmittal cross-link)
PEMS.Web/Controllers/HomeController.cs                                   (+IAccessControlService DI, Document Reviews tile)
PEMS.Web/Models/DashboardViewModel.cs                                     (+ShowDocumentReviews/PendingDocumentReviewsCount)
PEMS.Web/Security/CurrentUserContext.cs                                  (+Roles)
PEMS.Web/Views/EngineeringDocuments/Details.cshtml                       (+Related Transmittals section)
PEMS.Web/Views/Home/Index.cshtml                                         (+Document Reviews tile)
PEMS.Tests/Engineering/TransmittalRelatedDocumentQueryTests.cs           (new, 3 tests)
PEMS.Tests/Search/SearchProviderSecurityTests.cs                         (new, 16 tests)
PEMS.Tests/TestSupport/FakeCurrentUserContext.cs                         (new test double)
PEMS.Tests/TestSupport/FakeKnowledgeBaseService.cs                       (new test double)
```
Plus Phase 1's already-reported shell-consistency files (`ThemeController.cs`, `_Layout.cshtml`, `admin.js`, deletion of `cmms-rtl.css`) — unchanged since the prior report, not re-touched this phase.

## K. Database Changes

**None.** Zero new entities, zero migrations, zero schema modifications — confirmed by `git diff --stat` containing no `Migrations/` files and no new `DbSet` registrations. Every change in this phase is either a query addition against existing tables/columns or a new small DTO/interface method.

## L. Data Changes

**None.** No data deleted, reset, or modified. All new tests run against isolated InMemory databases; no test in this phase touches the live PEMSDB. G-28 (MDR bulk-import bypassing SoD/workflow) was not touched, per the brief's explicit instruction to leave it deferred.

---

## Test Results

```
Focused (this phase):  SearchProviderSecurityTests            16 passed
                        TransmittalRelatedDocumentQueryTests    3 passed
Full regression:       1170 passed, 0 failed, 0 skipped (7m54s)
                        = 1151 pre-existing (Phase 1 baseline) + 19 new — exact reconciliation
Build:                  PEMS.Web, PEMS.Persistence both succeed, 0 errors
                        (only pre-existing, unrelated nullable-reference warnings)
```

## Deferred Items (explicitly, per the brief's own §22 instruction)

- Help, UserPreference, Favorites, Recent Items — remain deferred pending this foundation, unchanged from the Phase 1 report's own STOP recommendation.
- Full EDMS command bar extraction into a reusable component — STOP, see §F.
- VDR/TechnicalQuery/MOC "pending my action" queries — not built this phase; only Engineering Documents was added, since it was this phase's named focus and the most clearly scoped gap. The same pattern (discipline/permission-eligibility count) could be replicated for the other three modules in a follow-up pass once each module's own eligibility rule is confirmed by reading its controller (not assumed).
- The base-authorization gap on `VDRController`/`TechnicalQueryController`/`MOCController`/`VendorController`/`SparePartController`/`EquipmentController` (zero permission checks today) is a real, pre-existing finding surfaced by this phase's search audit, but fixing six controllers' authorization is a distinct, larger initiative — out of scope here, flagged for a dedicated future phase.
