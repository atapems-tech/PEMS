# 04 - Platform direction (what we are building toward)

Source: plan pages (`plan/sheets`: Overview, Roadmap, Instructions, Lessons) and owner decisions. Read before touching metadata, forms, workflow, licensing, deployment, demo or versioning. The plan is authoritative; this is the working summary.

## 1. Product

PEMS: enterprise engineering and maintenance platform for oil, gas and petrochemical projects (EDMS/VDR, CMMS/EAM, permit to work, inventory/procurement, production, reliability, workflows, local AI). .NET 10, ASP.NET Core MVC/Razor, EF Core 10, SQL Server, Hangfire, SignalR, QuestPDF, Fido2, Serilog, Ollama/Tesseract. Persian-first (RTL, Jalali).

Direction (owner, 2026-10-15/18): turn static MVC modules into a **dynamic BPMS** - users create forms, modules and workflows without code or deploy - on a native .NET engine. Priority: **commercialisation + dynamicisation**, milestones M0..M6 in the Roadmap.

## 2. Decisions to build on (do not re-litigate; change only with the owner)

The reasoning behind each decision is in the ADR log: [plan/adr](../../plan/adr/README.md) (storage 0001, UI 0002, database 0003, settings 0004, licensing 0005, demo/staging branches 0006).

| Topic | Decision |
|---|---|
| Name | PEMS (closed) |
| Storage | hybrid: real tables for user-built entities, JSON column for custom fields on existing entities |
| UI | Razor + reusable component library (`PEMS.UI`), designers as embedded JS components; component tech recommendation B (Razor components, static render) with Tag Helper facade, gated by spike #319 |
| Database | fully code-first; migrator does install and update; Admin > Database Update page; `/health/database` for pipelines |
| Settings | env files per environment, secrets apart (chapter 01) |
| Licensing | on-prem, bound to machine + install time; request file -> vendor -> signed licence; lifetime or term (a time period); perpetual licence + annual maintenance; **no user limit**; casual-copy defence + EULA; VMs/Docker allowed with extra protection; never phones home; licence active in all environments, free surfaces only from an allowlist |
| Editions/versions | BPMS core has its own version; every module separately versioned and tracked per customer; builder limits come from the **Supportability Matrix**, not editions. Open: Edition matrix (L_EDITION), "times" licence (L_TIMES) |
| Customers | no per-customer forks; tagged releases only; customer registry (CV_REG), artifact library, per-release branch policy, release map file (CV_MAP) |
| Licensing design | Technical design (fingerprint, seal, `.pemsreq`/`.pemslic`, verification points, vendor tool, sequences, threat mapping): [LICENSING_TECHNICAL_DESIGN.md](../design/LICENSING_TECHNICAL_DESIGN.md) (draft until Member A approves); first implementation is in the repo behind `Licensing__Enforce=false` (section 6) |
| Workflow engine | one engine (`PEMS.WorkflowPlatform`, `IWorkflowRuntime`); every module flow is a versioned definition with an explicit binding, editable in Admin > Workflow Management. Module code may rely only on what its **module contract** declares (`PemsWorkflowContracts`: actions, stages read by name, dynamic-participant hand-overs, BusinessStatus); the validator refuses a version that breaks it. A new module flow ships its seeder, binding and contract together (SCRUM-103, [WORKFLOW_PLATFORM_MODULE_CONTRACTS.md](../workflow/WORKFLOW_PLATFORM_MODULE_CONTRACTS.md)); user-built BPMS processes have no contract |
| Data vs code (owner, 2026-11-06, SCRUM-514/515) | Three boxes: **Settings** (env files), **Definitions** (versioned records in the definition store, shipped as System definitions; includes reference data and workflow content), **Registry** (the vocabulary the code owns: field types, validator types, actions, permissions, components). Code says *how*, data says *what*. Fields of a form are **platform fields** (ours, read-only in the designer, changed only by product updates) or **application fields** (the customer's, editable, never touched by updates); customer data always wins and an update that would break it stops before changing anything and reports why (SCRUM-515, refining the upgrade rule of SCRUM-448; precedent: the sidebar's code-owned vs admin-owned fields). Audit of what is still hard-coded: [HARD_CODED_DATA_AUDIT.md](../architecture/HARD_CODED_DATA_AUDIT.md). "Business fields" and "system fields" are not used. |
| UI-only operations | licensing, install, update, version reporting completable in the UI; CLI only as optional extra [R20] |
| Hosting | landing website (main domain, repo `pems-landing`) and demo (subdomain, long-lived `demo` branch of this repo) are totally separate deployables sharing a host; both deployed by GitHub Actions with a manual-artifact fallback [R18] |
| Plan prose | the plan must not mention Titan in prose; Titan-derived items are credited only in reference columns |
| Product language | Persian; plan/code/ADRs English |

## 3. Supportability Matrix [R21]

The plan page *Supportability Matrix* (`plan/sheets/supportability-matrix.md`) lists what is supported (capability or limit, component, supported value, version, verifying backlog row). **Every finished task adds or changes its rows** and records that in the task Report. Builder limits are read from it, never invented in code.

## 4. Claims

Sales material, website and demo claim only Done capabilities with acceptance tests [R13]. Demo content is created by the team and controlled by pushes to the `demo` branch.

## 5. Where the roadmap lives

`plan/rows/NNNN.md` (the Backlog, one file per row) and `plan/sheets/*.md` (Roadmap, Test Plan, Licensing Design, Capability Checklist, Lessons, Old to New Map, Supportability Matrix, Sprint pages; counts: `plan_sync.py summary`). Areas (codes): FND, DYN, CMP, FRM, MOD, WFL, MIG, LIC, BRD, DEM, WEB, INS, SEC, INT, QA, LOC, RPT, AI, EDM, EAM, BIZ, OPS, DONE, DEV, CVM, SUP, HST.

## 6. Licensing in the code (SCRUM-26, 2026-10-26)

- One engine: `PEMS.Licensing` (`LicenseManager`). Product wiring in `PEMS.Web/Licensing`; customer page `Admin/License`; vendor tool `tools/PemsVendorTool` (never referenced by `PEMS.Web`; a test enforces it). Design and map: [LICENSING_TECHNICAL_DESIGN.md](../design/LICENSING_TECHNICAL_DESIGN.md) section 14.
- **`Licensing__Enforce=false` in every `env/*.env` until the owner switches an environment on.** With false nothing is checked, blocked or written, so merging `main` never changes a developer machine. Do not add a second switch, a dev shortcut or a bypass symbol; to try the flow locally set `Licensing__Enforce=true` in your own environment variables, not in the committed file.
- Allowlist of always-free surfaces: `PEMS.Web/Licensing/LicenceFreeSurfaces.cs`, the only place; a new always-free route needs the owner's agreement.
- New licensing behaviour goes through `LicenseManager`; no check outside it. Licence/request/seal formats change only with a version bump and test vectors (design 5.4).
- The signing private key and the reset key never exist in the repository or on CI; tests generate throw-away keys in temp folders. The product contains no signing code.

## 7. Definition-only model (owner decision 2026-11-19, sprint 2 goal)

**The goal:** adding, removing or editing any form, page, menu, action, data source or permission needs **no code change, no build and no deploy**: only database rows, applied by the migrator (install, update, feed, refeed packages; SCRUM-581 to 585, designers SCRUM-603 to 610).

- **Everything is generic and reusable.** One host per kind (`/p` page, `/f` form, `/a` action, `/d` data, file result) plus one route table in the database (SCRUM-768). A module differs from another only by **inputs**: slug, fields, components, authorizations, workflow, data sources. A new per-entity controller, view, handler or inline script is a defect (the ratchet gate SCRUM-611 fails it).
- **Who owns a field or a row** (owner decision 2026-11-06, SCRUM-515, 448): a form field is **platform** (ours: delivered and changed only by product updates, read-only for the customer, `owner: "platform"`) or **application** (the customer's, the default: editable by them, never touched by an update). The customer's data and application fields always win: an update merges the new platform fields into a customised form, keeps every application field as it is, and **stops before changing anything** with a report (form, field, reason) when it would collide with or retype an application field. A customised definition keeps its version; the new shipped version is kept beside it and `GET /api/v1/definitions/{id}/upstream` shows the difference; reset to shipped republishes the shipped payload. The words are "platform" and "application" ("business" and "system" are not used). Code: `FormOwnership`, `DefinitionService.ShipAsync`.
- **The ratchet gate** (SCRUM-611): `DefinitionRatchetTests` compares every Razor view and the action count of every controller with `plan/sheets/definition-ratchet.json` and fails with the file or controller that is new; a host or static controller (`plan/sheets/classification.json`) is exempt. A new screen is a page or form definition, a new read or write is a data source or action definition. If code really is the right answer, run the test once with `PEMS_WRITE_RATCHET=1` and say why in the pull request: the baseline diff is what the reviewer reads. A conversion only lowers the numbers (rewrite the baseline the same way after deleting the controller and its views).
- **Measure before you cache** (SCRUM-577): every page and form request logs one line with the trace id, the time of each stage (resolve, authorize, hydrate, evaluate, render) and the counts (definitions, hydrators, option lists, options, rows); a stage over its budget (`Definitions__Budgets__*Ms` in `env/*.env`) logs a warning with the same trace id. The caches of SCRUM-574 to 576 are only added where these numbers show the need.
- **One action, several writes, a confirmation** (SCRUM-593, 594, 595): an action that writes several tables is ONE action definition with the handler `action.steps` (`steps`: handler key, `parameters`, `as`, `values` with sources `field:name`, `literal:text`, `step:as.key`). The steps run in order inside ONE database transaction; the first refusal or exception rolls everything back and later steps do not run. A handler that returns values (`ActionOutcome.Returned`, the entity create action returns the row it wrote) feeds later steps and the `{as.key}` placeholders of the definition's `redirect`. A handler opens its scope with `ActionScope.Open` (never `scopes.CreateScope()`), so it joins the transaction. An action that needs a security key declares `stepUp: { resource, action }`: the host answers `StepUpRequired` with the challenge (HTTP 428 for a fetch), verifies the answer, audits a denial, and only then runs the operation; a definition that declares it never runs without a verifier. The service no longer checks the WebAuthn answer itself once its action declares `stepUp` (checking twice fails: an answer is single-use).
- **Domain logic stays in services**, exposed as `[PemsOperation(Model, Action, Permission?)]` and run by the one handler `operation.invoke` (settings: args sources, `guard`, `sectionGuard`, options objects). A controller that talks to `PEMSDbContext` directly needs its service extracted first (91 of 140 controllers today).
- **Inventory before backlog (learning 2026-11-19).** Before creating issues for a migration, run `python tools/definition-audit/audit.py --write`; the table `plan/sheets/definition-inventory.md` is the checklist, every controller is one issue ("Convert XController to definitions") or marked host/static (SCRUM-628). A backlog written from memory missed 150+ tasks. The remaining gap classes the audit found and the engine issues that close them: file results (SCRUM-615), inline scripts (616), shared layout/partials (617), TempData messages (618), specialised components (619-627), routes (768).
- **No slice left behind.** An issue is converted completely (all actions, views, scripts, roles, rules, feed) or split into separate issues before it starts; "remaining: ..." in a Jira comment is a defect of planning, not a follow-up. Each conversion lists its browser QA lines.
- **Change manifest.** Every change declares migration, feed data, definitions or none (SCRUM-585); definitions ship as migrator feed, not as code.
