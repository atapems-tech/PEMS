# 04 - Platform direction (what we are building toward)

Source: plan workbook (Overview, Roadmap, Instructions, Lessons) and owner decisions. Read before touching metadata, forms, workflow, licensing, deployment, demo or versioning. The workbook is authoritative; this is the working summary.

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
| UI-only operations | licensing, install, update, version reporting completable in the UI; CLI only as optional extra [R20] |
| Hosting | landing website (main domain, repo `pems-landing`) and demo (subdomain, long-lived `demo` branch of this repo) are totally separate deployables sharing a host; both deployed by GitHub Actions with a manual-artifact fallback [R18] |
| Plan prose | the plan must not mention Titan in prose; Titan-derived items are credited only in reference columns |
| Product language | Persian; plan/code/ADRs English |

## 3. Supportability Matrix [R21]

The plan sheet *Supportability Matrix* lists what is supported (capability or limit, component, supported value, version, verifying backlog row). **Every finished task adds or changes its rows** and records that in the task Report. Builder limits are read from it, never invented in code.

## 4. Claims

Sales material, website and demo claim only Done capabilities with acceptance tests [R13]. Demo content is created by the team and controlled by pushes to the `demo` branch.

## 5. Where the roadmap lives

`plan/PEMS_Backlog_Roadmap.xlsx` (Backlog, Roadmap, Test Plan, Licensing Design, Capability Checklist, Lessons, Old to New Map, Supportability Matrix, Sprint sheets) and its text mirror `plan/backlog.csv`. Areas (codes): FND, DYN, CMP, FRM, MOD, WFL, MIG, LIC, BRD, DEM, WEB, INS, SEC, INT, QA, LOC, RPT, AI, EDM, EAM, BIZ, OPS, DONE, DEV, CVM, SUP, HST.

## 6. Licensing in the code (SCRUM-26, 2026-10-26)

- One engine: `PEMS.Licensing` (`LicenseManager`). Product wiring in `PEMS.Web/Licensing`; customer page `Admin/License`; vendor tool `tools/PemsVendorTool` (never referenced by `PEMS.Web`; a test enforces it). Design and map: [LICENSING_TECHNICAL_DESIGN.md](../design/LICENSING_TECHNICAL_DESIGN.md) section 14.
- **`Licensing__Enforce=false` in every `env/*.env` until the owner switches an environment on.** With false nothing is checked, blocked or written, so merging `main` never changes a developer machine. Do not add a second switch, a dev shortcut or a bypass symbol; to try the flow locally set `Licensing__Enforce=true` in your own environment variables, not in the committed file.
- Allowlist of always-free surfaces: `PEMS.Web/Licensing/LicenceFreeSurfaces.cs`, the only place; a new always-free route needs the owner's agreement.
- New licensing behaviour goes through `LicenseManager`; no check outside it. Licence/request/seal formats change only with a version bump and test vectors (design 5.4).
- The signing private key and the reset key never exist in the repository or on CI; tests generate throw-away keys in temp folders. The product contains no signing code.
