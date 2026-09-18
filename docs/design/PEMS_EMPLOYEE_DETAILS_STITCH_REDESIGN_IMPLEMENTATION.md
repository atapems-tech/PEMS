# Employee Details — Stitch UI Redesign: Implementation Notes

## What changed

`PEMS.Web/Views/Employee/Details.cshtml` was redesigned into a bento-grid layout inspired by
the Stitch reference (`src/stitch-temp/employee_profile_marcus_thorne_dual_language_en_fa`):
a profile header card (avatar/initials, name, Active/Inactive status badge, position ·
department · discipline, personnel code, Edit Profile action) followed by an Employment
Details card, a compact Current Team card, a full-width Contact Information row (3 tiles), and
the pre-existing Labor Rate / Team History / Skills / Qualifications / Certifications sections
restyled into the same card language. All labels switch English/Persian with the existing
culture-cookie mechanism (`CultureInfo.CurrentUICulture`), matching `_Layout.cshtml`'s own
`isRtl` pattern — no new localization system was introduced. Dates remain Gregorian/ISO
(`yyyy-MM-dd`) regardless of culture, per the project's explicit `Program.cs` decision to keep
the `fa` culture's calendar Gregorian.

## Existing components reused

Bootstrap 5 grid/badges/modals, the `.content-card` class (`site.css`), `.btn-pems-solid`,
Bootstrap Icons, the existing `EmployeeController.Edit` POST action, the existing Skills/
Qualifications/Certifications/Labor Rate add-remove modals and forms (untouched), the existing
`_Layout.cshtml` RTL/culture switching, and `EmployeeController.Details`'s existing
`Employee`+`ViewBag` contract (extended, not replaced).

## New components introduced

- An "Edit Profile" modal on the Details page itself (`#editProfileModal`), posting to the
  same `Edit` action the Index grid's per-row modal already uses, with a `returnToDetails`
  flag so saving from here returns to Details instead of Index (the Index modal's behavior is
  unchanged — it still returns to Index).
- A "Current Team" bento tile, deriving the employee's manager from the current active
  `TeamMember` → `Team.SupervisorEmployee` (no new Manager field — reuses the existing
  Employee/Team relationship, Single Source of Truth preserved).

## Backend changes

Four new **nullable, additive** columns on `Employee` (`PEMS.Domain/Entities/Employee.cs`):
`HireDate` (DateTime?), `Location` (string?, 200), `Shift` (string?, 100), `HomeAddress`
(string?, 500). Reason: the Stitch reference showed these as real employment/contact facts;
PEMS had no backing field for any of them, and the user explicitly scoped this redesign to
include them (excluding the sensitive Safety & Health section — blood type, allergies,
emergency contact — and the fictitious "Download Resume" feature, neither of which has any
backing data or an agreed permission model). `EmployeeController.Create`/`Edit` accept the four
new fields as optional parameters; `EmployeeListItemDto` and the Index grid's Create/Edit
modals were updated in lockstep so editing from the grid can never silently null out a value
set via the new Details-page modal (both surfaces now submit the complete field set).

`EmployeeController.Details` now also `.Include(x => x.VendorRef)` and
`.Include(x => x.Team!).ThenInclude(t => t.SupervisorEmployee)` on `TeamHistory`, and calls the
existing `LoadDropdownsAsync()` (previously Index-only) to supply the Edit Profile modal's
Department/Discipline/Vendor dropdowns.

## Database changes

Migration `20260916222752_AddEmployeeProfileFields` — four `AddColumn` statements only
(`HireDate`, `HomeAddress`, `Location`, `Shift`), all nullable, no data transformation, no
column/table removed. Applied to the local `PEMSDB` database.

## Localization

- English/LTR: default culture; all page labels render in English.
- Persian/RTL: switching culture (existing topbar switcher) re-renders the same page with
  Persian labels and `dir="rtl"` (handled by `_Layout.cshtml`, unchanged) — verified by reading
  the rendered markup logic; a full authenticated visual RTL check should still be done in a
  browser (see Known Limitations).

## Permissions

Unchanged: the page remains gated by the existing controller-level
`[Authorize(Roles = SystemAdmin, EngineeringManager)]`. No new field is more sensitive than
what these two roles already see on this page (Location/Shift/HireDate/HomeAddress are
ordinary HR facts, not medical/emergency data — which is exactly why those were excluded from
this phase pending a separate permission-model decision).

## Tests executed

- `dotnet build PEMS.slnx --configuration Debug` — 0 errors (pre-existing warnings only,
  unrelated to this change).
- `dotnet test` filtered to `EmployeeProfileTests` (new: 4 tests) — all passed. Covers: the
  four new fields persisting/reloading correctly, defaulting to `null` for employees created
  before this change, and the Manager-derivation query (present and absent cases).
- Full solution regression suite (`dotnet test PEMS.slnx`) — **1405 passed, 0 failed**, 11m32s.

## Known limitations

No headless-browser/screenshot tool was available in this environment, so full authenticated
visual comparison against the Stitch mockup (spacing, RTL layout, responsive breakpoints) was
not performed by the agent. The dev server was left running at `http://localhost:5088` for the
repository owner to verify directly in a browser (`/Employee/Details/1`, and via the language
switcher for RTL). HTTP-level checks (unauthenticated request correctly redirects to login,
server starts without error) were performed as a partial substitute.
