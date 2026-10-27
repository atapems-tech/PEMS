# Architecture decision records (ADR)

One short file per significant decision, so that the reason survives the people who took it. Created under plan row #2 (SCRUM-14).

- Use [TEMPLATE.md](TEMPLATE.md) for a new ADR; copy it to `NNNN-short-name.md` with the next free number and add a row below.
- Record only decisions that are already taken or approved by the owner. Facts that are not documented are written as "not recorded", never guessed.
- A decision is changed by a **new** ADR that supersedes the old one (set the old status to `Superseded by ADR-NNNN`); small dated updates go in the ADR's *Amendments* section.
- Instructions rule 4 (no new static module without an ADR) and guideline chapter 01 section 1 point here.
- English only, no secrets.

| ADR | Decision | Status | Date | Decided by | Plan rows | Explained in |
|---|---|---|---|---|---|---|
| [0001](0001-hybrid-storage-for-user-built-entities.md) | Hybrid storage: real tables for user-built entities, JSON column for custom fields on existing entities | Accepted | 2026-10-18 | Project owner | #4 | [04-platform-direction](../../docs/guidelines/04-platform-direction.md) |
| [0002](0002-razor-with-separate-pems-ui-library.md) | Keep Razor; reusable component library as the separate Razor Class Library `PEMS.UI` (component technology still gated by spike #319) | Accepted | 2026-10-18 (refined 2026-10-23) | Project owner | #5 | [COMPONENT_LIBRARY_PLAN](../../docs/design/COMPONENT_LIBRARY_PLAN.md) |
| [0003](0003-code-first-database.md) | Fully code-first database; migrator installs and updates | Accepted | 2026-10-22 | Project owner | #309 (Instructions rules 6, 23) | [CODE_FIRST_DATABASE](../../docs/deployment/CODE_FIRST_DATABASE.md) |
| [0004](0004-environment-and-secrets-files.md) | Settings in `env/<name>.env`, secrets only in `env/<name>.secrets.env` or environment variables | Accepted | 2026-10-23 | Project owner | #315 (Instructions rule 24) | [env/README](../../env/README.md) |
| [0005](0005-licensing-threat-model-and-vm-policy.md) | Casual-copy defense plus contract/EULA, no hardware token; VMs and Docker allowed with extra protection | Accepted | 2026-10-18 | Project owner | #96, #97 | [04-platform-direction](../../docs/guidelines/04-platform-direction.md) |
| [0006](0006-demo-and-staging-branch-policy.md) | Long-lived `demo` branch, product changes only on `main`; staging branch added and `dev` retired 2026-10-25 | Accepted (amended 2026-10-25) | 2026-10-18 | Project owner | #300 (Instructions rule 18) | [DEPLOYMENT_README](../../docs/deployment/DEPLOYMENT_README.md) |
| [0007](0007-component-technology-static-razor-components.md) | Component technology: static Razor components with a Tag Helper facade (spike result, fallback per class) | Proposed (Member A to approve) | 2026-10-27 | Not yet decided | #319 (SCRUM-23), unblocks #320, #321 | [COMPONENT_LIBRARY_PLAN](../../docs/design/COMPONENT_LIBRARY_PLAN.md) |

The "approved by Member A" step of SCRUM-14 is recorded by the pull request review of this log.
