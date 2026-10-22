# legacy-trash

Retired material kept for reference only. Nothing here is built, packaged or executed.

## manual-sql/

Hand-run SQL that used to create or change the schema. Retired on 2026-10-22 when the database became fully
code-first (`DatabaseUpdateService`, migrations applied automatically on first run and on update, plus the
Admin > Database Update page). The same path layout as the original location is kept below `manual-sql/`.

| Moved script | Replaced by |
|---|---|
| `PEMS.Persistence/Scripts/EnableFullTextSearch.sql` | migration `FullTextSearch` |
| `docs/procurement/MaterialStockNoLocationUniqueness.migration.sql` | EF migrations of the procurement phases |
| `docs/procurement/P4_P5_delta_after_20261008075704.sql` | EF migrations of the procurement phases |
| `docs/procurement/ReplenishmentRecommendations.migration.sql` | EF migrations of the procurement phases |
| `docs/procurement/P4_P5_PREFLIGHT_DIAGNOSTICS.sql` | read-only diagnostics for the old manual flow |
| `docs/task manager/TaskManagerCore_migration.sql` | migration `TaskManagerCore` |

Do not run these against a database managed by the migrator: the migration history already covers them.
Delete this folder once the team agrees it is no longer needed.
