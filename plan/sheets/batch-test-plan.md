# Combined test run: batches 1, 2, 3, 4b and 5 (owner decision 2026-11-20; was 1, 2 and 4 on 2026-11-19)

Written and merged; **not run**. One combined run covers the five batches (components and screen engines, data/action/security engines, designers/governance/migrator, the Admin-area conversions still open, shell/organization/AI/dashboards conversions). **Trigger: batches 3, 4b and 5 are all In Review; then ask the owner; nothing heavy and no build of a test project before that, and none without the owner's answer.** Run it once, when the machine is quiet, then fix and cite it on every issue's QA line 1.

Gap batches 1b, 2b and 3b (sprint-batches.md) are done after the combined run is decided; they are separate issues, so they do not hold the run.

Batch 3 adds (to expect): the definition workbench (grid providers `definitions`, `definition-kinds`, `definition-usages`; stored pages `definition-workbench`, `definition-details`, `definition-kinds`; forms and actions `definition-*`; operations model `Definition`), designer and migrator pieces as they land; its test classes are listed in the batch-3 Jira comments.

## Order

1. **Record the new component snapshot** (batch 1 added `record-view`, `matrix`; changed `kpi-tile`, `field`). Only the files that changed are kept:
   `cd PEMS.UI.Tests && PEMS_GOLDEN_RECORD=1 dotnet test --filter "FullyQualifiedName~The_generated_manifest_matches"`, then `git checkout -- PEMS.UI.Tests/Manifests` for every file except `matrix.json`, `kpi-tile.json`, `record-view.json`, `field.json`, review the diff of those four, commit.
2. **UI suite** (golden manifests, literal-text gate, registry and guideline drift, ratchets): `cd PEMS.UI.Tests && dotnet test`.
3. **Definition and engine suite** (kinds, pipeline, hosts, operations, entity layer, routes, components, record binding, chart, matrix, masks, catalog): `dotnet test PEMS.Tests/PEMS.Tests.csproj --filter "FullyQualifiedName~Definitions"`.
4. **Guards the batch touched** (Persian keys, theme, accessibility): `python tools/test-gates/gates.py --slice --run`.
5. **SQL-instance families** (need the SQL instance; `AuditTemporaryKeySqlTests` fails without it and is not a regression): run where SQL is available.
6. **Browser checks**: the unchecked "Browser" lines of each issue (the QA checklists in Jira, batch comments of 2026-11-19).

## What batch 1 added to the suite (to expect in the run)

| Area | Tests |
|---|---|
| Component definitions | `ComponentDefinitionTests` (validation, expansion, each/if, templates, inline, shipped entity-list) |
| Details, chart, matrix, dashboard, views | `RecordViewTests` (record binding, map, chart, matrix, entity-dashboard, entity-views, request values, one theory for "may not read") |
| Routes | `RouteTableTests` |
| JSON actions | `ActionJsonValuesTests` |
| Catalog | `ComponentCatalogProviderTests` |
| Masks | `FieldMaskTests` |
| UI components | manifest snapshots, literal-text gate, registry drift (`record-view`, `matrix`, `kpi-tile.value`, `field.mask`) |

## Known, not regressions

- `AuditTemporaryKeySqlTests` and other SQL-instance families fail without SQL.
- The `matrix` snapshot does not exist until step 1; the manifest test fails until it is recorded.
