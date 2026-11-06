# Hard-coded data audit: what should be data, not code

Issue: SCRUM-514 (plan row 514). Date: 2026-11-06. Author: Claude, for Hamid. Status: first audit, for the owner to read and rank.

## 1. What this is and how it was made

The owner asked to separate the data from the code: form lists, form fields and steps, validations, sidebar items, list columns, page elements, workflows, lookups. The definition store already exists (ADR-0010, ADR-0011, SCRUM-434; envelope with versions, Origin System/Customer, ShippedVersionId, IsCustomized). This audit measures how much is still outside it.

**Method and limits.** Counts come from searching the repository (file counts, attribute counts, seeder classes). They show size and direction, not exact figures: a count of `[MaxLength]` says nothing about how many rules are business rules. Nothing was run; no database was read. Where a statement is a judgement it says so.

## 2. The three boxes

| Box | Meaning | Edited by | Lives in |
|---|---|---|---|
| **Settings** | How the application behaves on one machine or customer | the installer / operator | `env/*.env`, secrets in `*.secrets.env` (ADR-0004) |
| **Definitions** | What users see and do: forms, menus, lists, pages, workflows, reference lists | designers (customer, our team) | the definition store; the shipped set is delivered with the product as System definitions |
| **Registry** | The vocabulary the code owns and promises: field types, validator types, actions, permissions, components, layouts | developers only | code; readable by the designers |

Rule of thumb: data says *what*, code says *how*. Code defines the vocabulary; definitions compose with it.

## 3. Inventory

| # | Area | Where it is today | Size (measured) | Box | Target | Covered by | Verdict |
|---|---|---|---|---|---|---|---|
| 1 | Sidebar and menu | `NavigationMenuDefaults.cs` (code catalog, 119 entries) synced into the table `NavigationMenuItems`; Admin edits order, parent, icon, titles, visibility | 119 entries, 209 lines | Definitions | kind `menu`, shipped as System definitions | #451 (menu kind), #448 (shipped delivery) | Mostly done, own table. Move onto the definition store later |
| 2 | Forms (screens and their fields) | hand-written Razor: 240 view files contain a `<form>`; 365 `.cshtml` in total | 240 files | Definitions | kind `form` (exists, stored forms are used only by the definitions API today, not by modules) | #438, #439, #49, #458 | Biggest gap; migrate module by module |
| 3 | Form steps and field layout | inside those views | same | Definitions | kind `form` (steps) and `layout` | #451 (layout), form designer issues | Same gap as 2 |
| 4 | List pages: columns, filters | 202 views use `pems-list-*` tag helpers; columns are written in the markup | 202 files | Definitions | kind `view` | #451 (view), #57 | Large gap; value high |
| 5 | Validations | DataAnnotations: 31 `[Required]`, 50 `[MaxLength]`, 2 `[EmailAddress]` (26 files); 47 `ModelState.AddModelError`; 7 thrown validation-style exceptions in Application/Infrastructure | about 140 spots | Registry (validator types) plus Definitions (which rule on which field) | form field constraints in the `form` kind; named code validators for complex rules | #355, #356, #365, #380 | Small today, grows with every module; do with the form kind |
| 6 | Workflows | engine and tables exist (`PEMS.WorkflowPlatform`, versioned definitions, Admin > Workflow Management); the **content** of about 20 workflows is created by seeder classes in C# | 20 `*Workflow*Seeder` classes | Definitions | shipped workflow definitions | #448 (delivery), workflow platform issues | Engine done; shipped-content delivery and the "customised workflow survives an update" rule are open |
| 7 | Reference lists and lookups | 61 seeder classes, 8,163 lines in `PEMS.Persistence/Seed` (departments, disciplines, failure codes, UOM, categories, strategy types, numbering schemes, letter templates, help content, roles) | 61 classes | Definitions (reference data) | a `lookup` kind or shipped rows with the same "customer data wins" rule | **none** | Gap: no rule says what an update may do to a lookup the customer edited |
| 8 | Page elements and layouts | 111 components in `PEMS.UI`; manifests exist (#433) | 111 components | Registry | stays in code; pages built from them are Definitions | #433 done, #438, #439 | Registry side is done; page host open |
| 9 | Permissions | catalog in code (`PEMS.Web/Security`, `RoleCatalogSeeder`, `AccessControlSeeder`); roles and grants are data | 12 constants plus seeders | Registry (permission names) plus Definitions (roles, grants) | permission names stay code; per-kind definition permissions | #456, #440 | Mostly right |
| 10 | Command palette actions | `CommandPaletteActions.cs` | 36 lines | Registry | code, or derived from the menu definitions | none | Small; follow the menu |
| 11 | Texts | `SharedResource.resx` 3,690 keys (fa 3,980); no Persian literal found in `.cshtml` | 3,690 keys | Definitions (texts of stored definitions are `TranslatableText`, ADR-0008) | unchanged | #386, #387 | Fine |
| 12 | Settings and paths | 83 non-comment lines in `env/local.env`; hard-coded: file store `filestore` and `wwwroot` fallbacks in `Program.cs`, break-glass folder | few | Settings | env files | #396 | Fine; one open issue |
| 13 | Numbering, letter and PM templates | `NumberingScheme`, `LetterTemplate`, `PMTemplate` entities, seeded in code | seeders | Definitions (reference data) | same as 7 | none | Same gap as 7 |

## 4. What already works and should be copied

* **The sidebar already has the platform/application split.** The code catalog owns route, visibility rule, kind and active-state hints; the admin owns order, parent, icon, titles, hidden flag and badge; the sync adds missing items and updates only code-owned fields, never admin-owned ones (`docs/architecture/DYNAMIC_SIDEBAR.md`). That is exactly the rule of SCRUM-515 (platform fields ours, application fields the customer's). SCRUM-515 should generalise this pattern, not invent a new one.
* **The definition store** already has Origin, ShippedVersionId, IsCustomized, versions with Draft/Validated/Published/Retired, and a cache contract (METADATA_REGISTRY_DESIGN sections 3 and 5).
* **Workflows** are already data with an engine, versions and an admin screen.
* **Texts** are already data (resx) and component inputs are `TranslatableText`.

## 5. Ranking (judgement, for the owner to change)

Ordered by value for the "dynamic BPMS" goal divided by effort.

1. **Rule first, before any mass move** (#515 and #448): without the "customer data wins, a conflicting update stops with a report" rule, every other move is unsafe. Small effort, unlocks the rest.
2. **Reference data and workflow content (rows 6, 7, 13)**: 61 seeders and about 20 workflow seeders. Highest risk today, because an update can overwrite what the customer changed. Needs the same rule and a shipped-data format. Medium effort.
3. **Menu onto the definition store (row 1)**: smallest, already almost there; a good first proof of #448 and #451.
4. **Forms and steps (rows 2, 3, 5)**: the biggest gain and the biggest effort (240 files). Do one pilot form end to end (fields, steps, validation), then module by module.
5. **List pages (row 4)**: 202 files, large but mechanical once the `view` kind exists.
6. Leave alone: Registry items (rows 8, 9, 10), texts (11), settings (12).

## 6. Proposed new issues (not created; the owner decides)

1. **Reference-data update rule** (rows 7, 13): what an update may do to a lookup, template or numbering scheme the customer changed; shipped rows marked as platform-owned, customer rows untouched, conflicts stop the update. Depends on #515 and #448.
2. **Pilot: one real form as a stored definition end to end** (rows 2, 3, 5): pick one small module form, render it from the stored `form` definition, keep today's behaviour, prove validation and steps. Depends on #438 and #515.
3. **Shipped workflow content** (row 6): move the workflow seeders to shipped workflow definitions with versions, so a customised workflow survives an update. Depends on #448.
4. **Menu migration to the definition store** (row 1): fold into #451 and #448 instead of a new issue unless the owner prefers a separate one.

Modified instead of created (rule 28): #448 (basic rule; menu defaults are shipped definitions; refined by #515), #440 (policy table), plan rows 0440, 0448.

## 7. Questions for the owner

1. Reference data (rows 7, 13): is a lookup the customer edited always theirs, or do some lookups belong to us (for example failure codes from a standard)? This decides the platform/application split for lookups.
2. Which module should be the pilot form (row 2)?
3. Should the sidebar table move into the definition store, or stay a separate table with the same rules?
