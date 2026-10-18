# Deferred: Part Catalogue traceability (NOT approved, NOT applied, NOT compiled)

These files are an archive of the first WP-C implementation that added four columns to `PartCatalogueRequests`
(`MaterialItemId`, `SourceWorkOrderId`, `SourceStoreRequisitionId`, `SourceMaterialRequisitionId`) with the migration
`20261009225034_PartCatalogueTraceability`.

* They are stored as `.txt` so the compiler and EF tooling ignore them.
* The migration was removed from the project; no database ever received it.
* To resume, a schema change must first be explicitly approved. Prefer a separate additive side table so existing queries do not depend on it.
* See `docs/procurement/PHASE1_REMEDIATION_AND_FOLLOWUP.md` §8.
