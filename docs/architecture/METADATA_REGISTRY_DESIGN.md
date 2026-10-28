# Metadata registry and definition model: design

Status: **Proposed - 2026-10-28** (plan rows #338 and #430; [ADR-0010](../../plan/adr/0010-metadata-registry-definition-envelope.md), [ADR-0011](../../plan/adr/0011-ui-definition-model.md)). Owner review pending.
Scope: how PEMS stores everything that is *defined* rather than coded (data entities and fields, and the user interface: pages, forms, views, menus, themes, data sources, actions), how it is versioned and published, how shipped definitions coexist with customer-made ones, and how consumers cache it.

## 1. Principles

1. **Everything defined is a definition.** One envelope, one lifecycle, one REST surface ([ADR-0009](../../plan/adr/0009-rest-api-standard.md), `/api/v1/definitions`), one place to audit, export and roll back.
2. **Reuse what already works.** The workflow platform already has `WorkflowDefinition` + `WorkflowVersion` (Draft → Validated → Published → Retired, published versions immutable, `RowVersion` concurrency, clone, binding). The dynamic sidebar already syncs a code-owned catalog into a table without overwriting admin-owned fields. The registry generalises both; it does not replace them in this step.
3. **Texts are `TranslatableText`** (ADR-0008). Stored as JSON maps of language slug to text.
4. **Schema changes only through migrations** (rule 6). Definitions are *data*; the tables that hold them are created once by a migration.
5. **Differences between installations are handled by migrations.** Owner decision 2026-10-28. System definitions arrive the same way on every installation (migration / seed); only customer-created definitions differ, and they move between environments as signed packages (#23).

## 2. Definition kinds

| Kind | What it defines | Row | Notes |
|---|---|---|---|
| `app`, `module` | grouping, menu root, entitlement key | #10 | module key matches the licence entitlement (#113) |
| `entity` | a data entity: fields, keys, relations, storage mode | #10, #11, #13 | storage per ADR-0001: real table for user-built entities, JSON column for custom fields on static ones |
| `option-set` | managed value lists with translations | #21 | |
| `page` | layout of regions holding component instances | #438 | ADR-0011 |
| `form`, `view` | data entry and list definitions (`FormDefinition`, `ViewDescriptor` shapes) | #44, #57, #387 | already JSON-shaped in `PEMS.UI.Abstractions` |
| `dashboard`, `report` | KPI/chart layouts and report definitions | #58, #59 | |
| `menu` | navigation tree | #20 | generalises `NavigationMenuItem` |
| `theme` | tokens and brand | #30 | |
| `data-source`, `action` | declarative data binding and behaviour | #437 | |
| `workflow-ref` | pointer to a `WorkflowDefinition` | #67 | workflow tables stay as they are |

New kinds are registered in code with: key, JSON Schema of the payload, the permission needed to edit/publish, and the references it may hold. An unregistered kind cannot be stored.

## 3. The envelope

Tables (new migration, never edited once applied): `Definitions`, `DefinitionVersions`, `DefinitionReferences`, `DefinitionStamps`. The compile-checked sketch is [sketches/DefinitionRegistrySketch.cs](sketches/DefinitionRegistrySketch.cs).

- `Definition`: `Kind`, `Key` (unique per `AppKey` + `Kind`), `AppKey`, `Origin` (`System` | `Customer`), `TitleJson`, `PublishedVersionId`, `ShippedVersionId`, `IsCustomized`, `RowVersion`.
- `DefinitionVersion`: `VersionNumber`, `Status`, `PayloadJson`, `PayloadSchemaVersion`, `ContentHash` (SHA-256 of the canonical payload), `ChangeSummary`, validated/published/retired audit columns, `ClonedFromVersionId`, `RowVersion`.
- `DefinitionReference`: edges (`renders`, `reads`, `calls`, `menu-target`) written at validation; they power the impact preview of the schema planner (#13) and "what uses this component".
- `DefinitionStamp`: one counter per kind for cache invalidation.

### Lifecycle

`Draft` (editable) → `Validated` (schema + references + permissions checked) → `Published` (immutable, the one consumers read) → `Retired` (immutable, kept for history and for instances pinned to it). **Rollback = publish an older version again as a new version number** (history is never rewritten). A published version is never edited; changing it means a new draft cloned from it. Enforced in the service and in `SaveChanges` exactly as for `WorkflowVersion`.

In-flight records (workflow instances, form drafts) stay pinned to the version they started with.

## 4. Static entities and shipped definitions

- **Static entities** (the existing 70+ code-first entities) appear in the registry as read-only `entity` definitions with `Origin = System`, generated from the EF model at startup. Their structure cannot be edited; customers may add fields only through an `entity-extension` definition stored in the JSON extension column (#14).
- **Shipped definitions** (system pages, forms, menus) are delivered by migration or seed as `Origin = System` published versions and recorded in `ShippedVersionId`. The product upgrade rule:
  - not customised: the new shipped version becomes the published one;
  - customised (`IsCustomized`): the customer's published version stays; the new shipped version is stored as a draft with a "upstream changed" flag and a diff, and an administrator merges or ignores it;
  - **reset to shipped** = publish the shipped payload again as a new version.
- This is the same pattern the dynamic sidebar uses (code-owned fields synced, admin-owned fields kept), applied to whole definitions.

## 5. Cache and invalidation contract (row #28)

- Consumers read the **published** version only, through one `IDefinitionStore` that caches `(kind, key) → payload + ContentHash` in memory.
- Publish, retire and rollback increment `DefinitionStamp(kind)` in the same transaction. The instance that published evicts its cache entries immediately; other instances compare the stamp (one cheap query, at most once per second) and evict what changed. Worst-case staleness across instances: about 2 seconds.
- HTTP: `ETag` = `ContentHash`; `Cache-Control: private, no-cache` with `If-None-Match` → 304 (ADR-0009). Static assets that are generated from definitions (for example the component manifest) are named by their hash and may be cached as immutable.
- Responses over 1 KB are gzip compressed.

## 6. Limits and security

- Payload ≤ 1 MB per version; ≤ 500 component instances per page; nesting depth ≤ 12; unknown properties rejected by the schema. (Configurable in `env/*.env`, rule 24.)
- Edit and publish are separate permissions per kind (`Definitions.<Kind>.Edit`, `.Publish`), granted through the access matrix (#60, #178); system definitions can be marked protected and then need a second approver (#440).
- Every change is in the audit trail (#17) with a diff; the definition store REST API (#434) is the only write path.
- A definition can reference only definitions the *publisher* may read (checked at validation), so a page cannot smuggle data the author cannot see; the runtime still applies row-level security to the data itself.

## 7. Relation to existing tables

| Existing | In this model |
|---|---|
| `WorkflowDefinition`/`WorkflowVersion`/`WorkflowBinding` | stay; a `workflow-ref` definition points to them. Their lifecycle is the template for the envelope. |
| `NavigationMenuItem` | becomes the first `menu` definition (#20) behind its feature flag; its catalog-sync rule is the shipped-definition rule above. |
| `PMTemplate`, `LetterTemplate`, `ShiftDefinition` | domain data, not definitions; unchanged. |

## 8. Open questions (with owner)

| # | Question | Owner |
|---|---|---|
| 1 | JSON Schema library and licence for payload validation and the component manifest (#433) | assistant proposes, owner approves (rule: library first, licence recorded) |
| 2 | Is a second approver needed for every system-definition change or only protected ones? (#440) | owner |
| 3 | Maximum payload size and instance counts above are starting values; measure with the page runtime (#438) | assistant |
| 4 | When both an upstream shipped change and a customer change touch the same property, is a manual merge enough or is a 3-way merge tool needed? | owner |
| 5 | Should `entity` definitions drive DDL directly (#13/#14) or only through reviewed migrations? Proposal: planner generates a migration script that is reviewed and applied, never silent DDL | owner |
