# Combined test run: batches 1 and 4 (owner decision 2026-11-19; batch 2 is in the same run)

Written, merged and compiled; **not run**. One combined run covers the three batches (components and screen engines, data/action/security engines, Admin-area conversions). Run it once, when the machine is quiet, then fix and cite it on every issue's QA line 1.

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

## What batch 4 added (Admin conversions, the pages to open in the browser)

Every page below is a definition page now; its old address (`/Admin/...`) is a `route` definition that must land on the new one. Sign in as `SystemAdmin` unless a line says otherwise. The Jira comment of each issue has the full line list.

| Issue | Old address | New page | Check |
|---|---|---|---|
| 644 | /Admin/Roles | /p/roles | list, create a custom role, catalog roles cannot be deleted, custom role can; sidebar link active |
| 643 | /Admin/PurchaseOrderAuthority | /p/po-authority | new level; an overlapping range shows the service message in the form; edit loads the values; approver list has only approver roles; Active switch replaces the old toggle |
| 649 | /Admin/StorageRouting | /p/storage-routing | create (unknown provider refused by the service), edit, remove (soft delete) |
| 645 | /Admin/RoutingRules | /p/routing-rules, /p/routing-confidence | edit target module; thresholds edit loads 0.85 and 0.5 when empty; caches are refreshed (service) |
| 632 | /Admin/ContractApprovalAuthority | /p/contract-approval-authority | service validation message on overlap; toggle |
| 629, 630, 646, 647 | audit log, authorization audit, SoD policies, security events | /p/audit-log, /p/authorization-audit, /p/sod-policies, /p/security-events | list, filters, details; SoD policy create and toggle; a non-admin cannot open them |

Also: `DynamicSidebarTests` mirror test (legacy sidebar and catalog menu must list the same `/p/...` links), the Web test family (`dotnet test PEMS.Tests --filter "FullyQualifiedName~Web"`), `MigrationClassificationTests`, `PoAuthorityDefinitionsTests`, `OperationTests`.

## Still open in batch 4 (not in this run)

Not converted yet, each waits on a generic input (issues in `plan/sheets/sprint-batches.md`): 631, 633, 634, 635, 636, 637, 638, 639, 640, 641, 642, 648, 650, 651. Their controllers still serve their pages, so this run is valid without them.

## Known, not regressions

- `AuditTemporaryKeySqlTests` and other SQL-instance families fail without SQL.
- The `matrix` snapshot does not exist until step 1; the manifest test fails until it is recorded.
