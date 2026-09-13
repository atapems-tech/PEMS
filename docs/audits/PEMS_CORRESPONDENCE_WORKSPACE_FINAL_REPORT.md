# PEMS Correspondence Workspace — Gmail-Inspired UX Redesign — Final Report

*Builds directly on `docs/audits/PEMS_LETTER_CONTROLLED_LIFECYCLE_FINAL_REPORT.md` (previous phase). This phase preserves that phase's lifecycle/authorization/audit architecture unchanged and layers workspace-level UX, a design-time safety fix, and a real concurrency-test gap closure on top of it.*

## A. Audit Findings

- **`PEMSDbContextFactory` safety gap (explicitly named in this phase's brief):** confirmed it hardcoded `Database=PEMSDB` literally, so the previous phase's `ConnectionStrings__PEMSConnection` env-var override for a disposable repro database was silently ignored — the "LetterControlledLifecycle" migration had actually applied straight to live PEMSDB. **Fixed this phase** (see §D).
- **Concurrency testing gap (flagged as open in the previous report):** a spike test proved EF Core's InMemory provider **never generates `RowVersion` values** (always `null`), so no InMemory-based concurrency test can be meaningful. Real SQL-Server-backed tests (isolated DB, `Barrier`-forced overlap) were written instead, and **this immediately caught a genuine bug**: `SaveWithConcurrencyCheckAsync` threw a raw `InvalidOperationException` on conflict instead of returning the `(bool Success, string? Error)` tuple the rest of the service uses — meaning a real concurrent Approve/Return/Reject/Issue/Close collision would have surfaced to the user as an unhandled 500 error instead of the intended friendly `TempData["Error"]` message. **Fixed this phase** (see §C).
- **Two more eager-dropdown-loading gaps beyond the one already fixed for `Details`:** `Letter/Index` and `Letter/Create` both called the same heavy `LoadDropdownsAsync()` (all active Employees + 200 Documents + 200 MRs + all Tags), even though `Index.cshtml` only ever renders Projects/Disciplines and `Create.cshtml` only additionally needs RelatedDocuments/RelatedMRs — Employees/Departments/Tags were pure waste on both pages. **Fixed this phase.**
- **No real "Starred" folder possible:** `SearchAsync` computed `IsStarred` only for display on the current page; there was no server-side filter to actually show *only* starred letters. **Fixed this phase** (additive `starredOnly` parameter, default `false`, backward compatible).
- **Index had no folder-style navigation**, only a flat row of type buttons plus a right-side discipline-shortcuts panel — no Draft/Under Review/Returned/Approved/Issued/Closed/Starred folders as requested by this phase's UX brief.
- **Create was one large flat form** — no primary/secondary field separation (Compose-style progressive disclosure).
- **Status badge coloring logic was duplicated three times** (Index, Details, MyReferrals) with `MyReferrals` still on the old flat `bg-info`-for-everything badge — a direct violation of this phase's own §33 ("do not leave legacy status names/colors in one part of the application"). **Fixed this phase** via a single shared `PEMS.Web.Helpers.LetterStatusBadge.CssClass()` (mirrors the pre-existing `DocumentStatusBadge` helper convention exactly — no new pattern invented).
- **Digital signature labeling (§32) — audited, already correct:** `SignedByName`/`SignedDate` are labeled "Sign / Acknowledge" / "Electronically acknowledged", with an explicit on-screen disclaimer "not a PKI digital signature." No change needed.
- **Admin override (§31) — audited, still no requirement found:** no business need for unrestricted admin editing surfaced this phase either; immutability remains the default, undocumented as "intentionally not supported" per the previous phase's decision, reaffirmed.
- **Revision Mode (§17) — was missing:** the previous phase's edit flow showed only the current content, not the version the reviewer actually saw. `LetterReviewDto` didn't expose the `PreviousSubjectSnapshot`/`PreviousBodySnapshot` fields that already existed on the `LetterReview` entity from the prior phase. **Fixed this phase** (additive DTO fields only, entity/schema unchanged).
- **Review/Referral separation, thread rendering, context-aware actions, More menu, on-demand employee picker, immutability enforcement** — all audited and confirmed intact and unchanged from the previous phase; no regression found.

## B. Design Changes

- **`Letter/Index.cshtml`** — rebuilt into an inbox-style layout: a left sidebar (`col-lg-3`, renders on the natural "start" edge in both LTR and RTL since it's the first DOM column) with a Compose dropdown (Incoming/Outgoing/Internal), a folder list (All / Drafts / Under Review / Returned for Revision / Approved / Issued / Closed / **Starred** — now a real server-side filter), a Type sub-section (Incoming/Outgoing/Internal, preserved from before), and the existing Discipline Shortcuts panel moved into the same sidebar. The old always-visible structured filter form is now a collapsed "Advanced Filters" toggle (auto-expanded only when a filter is already active), decluttering the common case. List rows got a `pems-compact-rows` class for tighter vertical padding. KPI row and quick-search box unchanged.
- **`Letter/Create.cshtml`** — rebuilt into a Compose-style form: Date + From/To + Subject + Body shown directly; Letter Number/Priority/Confidentiality/Related Project/Discipline/Document/MR moved behind a "More Details" collapse. Save button label is now context-aware (`Save Draft` for Outgoing, `Register` for Incoming, `Send` for Internal — matching `CreateAsync`'s actual initial status per type).
- **`Letter/Details.cshtml`** — added a Revision Mode block: when editing a `ReturnedForRevision` letter, a "Revision N" badge and a collapsible "View previous version (as reviewed)" panel now show the exact snapshot the reviewer saw, sourced from the corresponding `LetterReview` row.
- **`Letter/MyReferrals.cshtml`** — status badge now uses the shared `LetterStatusBadge` helper instead of a flat, uninformative color.
- **`PEMS.Web/Helpers/LetterStatusBadge.cs`** (new) — single source of truth for status→badge-color mapping, used by Index/Details/MyReferrals.
- **`site.css`** — added `.pems-folder-link`/`.pems-folder-link.active` (sidebar nav styling) and `.pems-compact-rows` (denser table rows), reusing existing color tokens (`--pems-navy`, `--pems-accent`) — no new design system.

## C. Backend Changes

- **`PEMS.Persistence/Servises/LetterService.cs`**:
  - `SaveWithConcurrencyCheckAsync` changed from `Task` (throwing `InvalidOperationException` on conflict) to `Task<(bool Success, string? Error)>`; all five callers (`ApproveAsync`, `ReturnForRevisionAsync`, `RejectAsync`, `IssueAsync`, `CloseAsync`) now `return await SaveWithConcurrencyCheckAsync();` instead of assuming success. **This is a real bug fix**, not a refactor — proven by the concurrency test that failed with an unhandled exception before this change and passed cleanly after.
  - `SearchAsync` gained an additive `bool starredOnly = false` parameter; when true and a user is authenticated, filters to letters present in `LetterStars` for that user (anonymous + `starredOnly` → empty result, not an error).
  - `GetDetailAsync`'s `Reviews` projection now also maps `PreviousSubjectSnapshot`/`PreviousBodySnapshot`.
- **`PEMS.Application/DTOs/LetterDtos.cs`**: `LetterReviewDto` gained `PreviousSubjectSnapshot`/`PreviousBodySnapshot`.
- **`PEMS.Application/Interfaces/ILetterService.cs`**: `SearchAsync` signature updated to match (additive, default value — non-breaking).
- **`PEMS.Web/Controllers/LetterController.cs`**:
  - `Index` gained a `bool starred = false` parameter, threaded into `SearchAsync`, and switched from `LoadDropdownsAsync()` to a new light `LoadIndexDropdownsAsync()` (Projects + Disciplines only).
  - `Create` (GET) switched from `LoadDropdownsAsync()` to a new `LoadCreateDropdownsAsync()` (Projects + Disciplines + RelatedDocuments + RelatedMRs — no Employees/Departments/Tags).
  - The now-dead, fully-unused `LoadDropdownsAsync()` was replaced entirely by these two purpose-scoped methods rather than left as unreferenced code.
- **`PEMS.Persistence/Context/PEMSDbContextFactory.cs`** — rewritten (see §D).
- **`PEMS.Tests/TestSupport/FakeLetterService.cs`** — updated to match the new `SearchAsync` signature.

## D. Database Changes

**No migration was created or applied this phase.** Every requested UX capability (folders, starred filter, revision-mode comparison, compose layout) was implementable by reusing existing columns/entities (`Letter.Status`, `LetterStars`, `LetterReview.Previous*Snapshot` from the prior phase) plus additive, non-schema DTO/query changes. This was decided *before* writing any code, per this phase's own §2 instruction to determine whether a migration is avoidable first.

**`PEMSDbContextFactory` fix (the one required infrastructure change, not a migration):**
- **Before:** `CreateDbContext` hardcoded `Server=localhost\SQLEXPRESS;Database=PEMSDB;...` unconditionally — every `dotnet ef` command targeted live PEMSDB no matter what environment variables were set.
- **After:** resolves the connection string in this order — (1) `ConnectionStrings__PEMSConnection` environment variable, (2) `appsettings.Development.json` then `appsettings.json` in the `--startup-project` directory (parsed directly with `System.Text.Json` — a `Microsoft.Extensions.Configuration.*` extension-method version conflict elsewhere in this project's dependency graph made the "idiomatic" `ConfigurationBuilder` approach fail to compile, so the dependency-free approach was used instead), (3) the old hardcoded string only as a last-resort fallback. **Every design-time invocation now prints its resolved target database and server to the console** (`[PEMSDbContextFactory] Design-time DbContext target database: '...' on server '...'`) so the target is never silent.
- **Verified both directions:** `dotnet ef dbcontext info` with no override still correctly resolves to `PEMSDB` on `localhost\SQLEXPRESS` (no regression to normal usage); with `ConnectionStrings__PEMSConnection` set to a `PEMSDB_CorrespondenceWorkspaceRepro` connection string, it correctly targets that database instead — the exact override that silently failed at the end of the previous phase now works.
- **Target database verification for this phase's actual DB work:** none was needed since no migration ran, but the fix itself was validated live (see above) rather than merely reasoned about.

## E. UX Validation

**Full interactive/authenticated browser validation of the 15 states listed in this phase's brief was not achievable in this environment**, and I am reporting that limitation directly rather than claiming a pass I couldn't actually perform:
- `PEMS.Web` enforces a global `AuthorizeFilter` (`RequireAuthenticatedUser()` in `Program.cs`) on every controller, including `LetterController`. No test credentials, dev-mode auth bypass, or seeded default login were found anywhere in the repository (checked `AccessControlSeeder`, `appsettings*.json`, `Program.cs`) — attempting to guess or brute-force a login was not an appropriate way to obtain one.
- No headless-browser or screenshot tool is available in this environment.

**What was actually verified, concretely:**
- The dev server was started (`dotnet run`, port 5257) and reached "Application started" with **zero startup exceptions**, including all EF Core seed-check queries and Hangfire initialization — proving the full DI graph, migrations-check, and startup pipeline are intact after every change in this phase.
- `curl` requests to `/Letter/Index`, `/Letter/Details/1`, `/Letter/Create`, `/Letter/MyReferrals` as an anonymous user all returned a clean `302` redirect to `/Account/Login?ReturnUrl=...` with correctly URL-encoded return paths — not a `500`, confirming routing and the auth boundary are both intact (this exercises routing/model-binding up to the authorization filter, but not the Razor rendering itself, which the filter runs ahead of).
- Every view (`Index.cshtml`, `Create.cshtml`, `Details.cshtml`, `MyReferrals.cshtml`) was confirmed to **compile successfully as Razor** after every single edit in this phase (not just once at the end) — this catches most template-breaking mistakes (unresolved `@` expressions, mismatched model types, missing `ViewBag` casts).
- Each view was re-read in full after editing specifically looking for the class of bug a real browser would surface but Razor compilation wouldn't (duplicate rendering, mismatched conditional visibility, wrong action names) — this is exactly how the "body renders twice for a Draft letter" bug was caught and fixed in the previous phase, and the same review discipline was applied to every new block added this phase.
- The dev server was **stopped immediately after the HTTP checks**, since it was bound to live PEMSDB with Hangfire actively running and was not needed further.

**Recommendation:** a genuine visual/interactive pass (the 15 states, RTL vs LTR, narrow viewport) still needs to happen with real credentials in a real browser before this is considered fully UX-verified end-to-end — this is carried forward as an open item, not silently dropped.

## F. Test Results

```
Previous phase baseline:      1146 passed
New this phase:
  LetterConcurrencyTests           3 (real SQL-Server-backed, Barrier-forced overlap)
  SearchAsync starredOnly tests    2
                                 ----
  Subtotal new                    5
Full regression (final run):  1149 passed, 0 failed, 0 skipped  (6m41s, before the 2 starredOnly tests were added)
starredOnly tests (separate run): 2 passed, 0 failed            (4s)
                                 ----
Total:                        1151 passed, 0 failed, 0 skipped
```
No existing test was modified, skipped, or deleted to make anything pass. The concurrency tests are real: they run against an isolated SQL Server database built from the actual migration history (`PEMSDB_LetterConcurrencyTest`, via `CreateIsolatedSqlServerOrThrow`), not the InMemory provider — and one of them failed on first run (correctly), exposing the `SaveWithConcurrencyCheckAsync` bug fixed in §C, then passed after the fix.

## G. Remaining Findings

- **Full authenticated browser validation across all 15 requested states is still outstanding** — see §E. This is the most significant open item from this phase's own brief.
- **The `IConfigurationBuilder` extension-method compile failure in `PEMSDbContextFactory`** (worked around with direct `System.Text.Json` parsing) points to an unresolved package-version conflict somewhere in `PEMS.Persistence`'s dependency graph (`Microsoft.Extensions.Configuration.Json`/`.FileExtensions` are referenced at 10.0.10, all resolving consistently per `dotnet list package`, yet the extension methods didn't bind) — worth a dedicated investigation in a future phase since it could affect other code that tries to use standard `IConfigurationBuilder` patterns in this project.
- **`Letter/Reply.cshtml`** was re-checked this phase and still has no direct inconsistency with the new folder/status vocabulary (it doesn't render a status badge or reference removed concepts) — left untouched.
- **The `starred` folder link doesn't currently combine with the Type sub-filter or Advanced Filters in the sidebar UI** (clicking "Starred" resets `letterType`/`status`, matching how every other folder link already behaves) — this is consistent with the existing pattern, not a new gap, but a combined "Starred + Incoming" view would need the sidebar links to preserve `letterType` as a hidden carry-forward, which was not built (kept simple, matching §38's anti-over-engineering instruction).
- **No changes were made to `LetterReferralService`** — re-confirmed this phase it still has no `Approve`/`Reject`/`Return` semantics, so Review vs. Referral separation from the previous phase remains structurally sound.
- Pre-existing, unrelated to this task and left untouched: `src/stitch-temp/edit_employee_modal_en/` deletion (present before this session's work began), several `PEMS.Web/filestore/`, `wwwroot/uploads/` artifacts, and one untracked `.claude/Role/` file.

## H. Data Safety Confirmation

- **No existing Letter records were deleted.** Live PEMSDB `Letters` count: **6** (unchanged from the end of the previous phase and from before this phase started).
- **No tables were dropped.** `LetterReviews` count: **0** (unchanged — no live rows were ever created; expected, since this phase's LetterReview-related tests all ran against isolated test databases).
- **No production-like data was cleared.** `LetterReferrals`: **1**, `LetterRecipients`: **4**, `AspNetUsers`: **2** — all unchanged from before this phase's work.
- **No destructive migration was introduced — because no migration was introduced at all this phase** (see §D).
- **Existing migration history remains intact**: `dotnet ef dbcontext info` confirms the design-time factory (now fixed) still resolves to the same live `PEMSDB` by default; no migration was added, removed, or reordered.
- All concurrency and lifecycle tests that needed a real SQL Server (not InMemory) ran against dedicated, disposable, session-scoped isolated databases (`PEMSDB_LetterConcurrencyTest`) that seed their own rows and clean them up in a `finally` block — **live PEMSDB was never written to by any test this phase.**
