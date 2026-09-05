# Material Master Import — Excel Metadata Extraction & Registration

**Date:** 2026-08-19
**Scope:** `Dashboard / Inventory & Materials / Material Master` — Excel metadata capture, Vendor/Purchase-Order structuring, source-document traceability

---

## 1. Audit performed before implementation (per directive §14)

1. **Existing metadata implementation** — none found (`grep` for `Metadata`/`DocumentMetadata`/`DynamicMetadata` entities returned nothing). No competing system existed to reuse; the new `MrsImportRowMetadata` table is the first of its kind, deliberately designed to extend the same pattern used elsewhere in this codebase (append-only child rows, not a generic reflection-based framework).
2. **Current Material Master entities** — `MaterialItem`, `MaterialCategory`, `MaterialUOM`, `MaterialStock`, `MaterialReservation` already exist and are real (`MaterialItem.cs`'s own comment states the schema was designed from ~50k real MRS rows).
3. **Existing Excel import** — `MrsImportService`/`MrsImportBatch`/`MrsImportRow` is a real, working Stage→Review→Commit pipeline (not a stub), already handling many of the directive's real headers via a `FieldAliases` dictionary. This is the system extended, not replaced.
4. **Existing Source Document/Attachment architecture** — `DocumentAttachment` (generic, already used by Engineering/VDR/Letters/TQ/MOC via nullable FKs) was reused for the Excel source file, rather than inventing a new file-storage entity.

---

## 2. What was built

| Directive requirement | Implementation |
|---|---|
| Dynamic header detection & persistence | `StageFromExcelAsync` now captures every non-empty header cell in the header row (`originalHeadersByColumn`), independent of whether it matches a known alias |
| Original header preserved verbatim | `MrsImportRowMetadata.OriginalHeader` — exact cell text, no normalization, no case change (proven by the `چک`/`Wight` test cases) |
| Canonical mapping where applicable | New `MrsMetadataCanonicalMap` (case-sensitive exact map first, for the genuine `QTY check` vs `QTY CHECK` collision in the real source; normalized fallback map for everything else) |
| Unknown headers preserved, not discarded | Any header not in the canonical map still gets a `MrsImportRowMetadata` row with `CanonicalField = null` (verified for `temp unique`, `Warehouse Bin`) |
| Vendor Name / Purchase Number as structured metadata | Both captured as `MrsImportRowMetadata` rows (`CanonicalField = "VendorName"` / `"PurchaseOrderNumber"`) **and** the pre-existing `MaterialItem.VendorId` FK (previously unused — see bug fix below) |
| Import Batch concept | Already existed (`MrsImportBatch`) — extended with `SourceAttachmentId` instead of duplicated |
| Source Document link | `MrsImportBatch.SourceAttachmentId → DocumentAttachment` (real file now saved to `wwwroot/uploads/mrs-import/`, not just a filename string) |
| Transaction safety | `StageFromExcelAsync` restructured to a single atomic `SaveChangesAsync()` (batch + rows + metadata together, via navigation-property fixup) instead of two separate saves that could leave an orphaned empty batch on failure |
| UI visibility | Review page: Vendor/PO columns added, per-row expandable "all Excel columns" panel, source-file download link. Material Details page: new "Source Imports" tab showing which batches/rows produced this material |

---

## 3. A real, pre-existing bug found and fixed (not scope creep — directly required by §3)

`MaterialItem.cs`'s own design comment already stated: *"Manufacturer و Supplier یکی نیستند - Supplier به Vendor واقعی وصل می‌شود"* (Manufacturer and Supplier are not the same — Supplier connects to a real Vendor). But `MrsImportService.CommitAsync` violated this: it wrote `row.RawSupplier` into the free-text `Manufacturer` field and never touched the `VendorId` FK that already existed on `MaterialItem`. Fixed by resolving/creating a real `Vendor` via the existing `IVendorService.FindOrCreateByNameAsync` (already used by the VDR module) and setting `VendorId` — this is precisely what directive §3 requires ("Vendor Name... persist as structured metadata... associate with Material record"), and it makes the pre-existing (already-shipped) "Vendor" field on `MaterialItem/Details.cshtml` — which showed "-" for every imported material until now — actually populate.

---

## 4. Real header-schema gaps found and fixed

Testing against the directive's **actual** header list (not a simplified example) surfaced alias mismatches in the existing `FieldAliases` dictionary that would have caused real data loss on real files:
- `PACK NO.` normalizes to `packno`, but the only existing alias was `packingno` — added `packno`.
- `Purchase Number` / `PO Number` / `Purchase No` — directive's own example list names these as required variants, but none were in the canonical map — added (caught by a failing test, not silently missed).
- `QTY check` vs `QTY CHECK` — these are two **different** real columns in the source file but normalize to the identical string `qtycheck`. Solved with a case-sensitive exact-match layer checked before the case-insensitive fallback, so both are captured under their own correct canonical name.

## 5. Deliberately out of scope (documented, not silently skipped)

- **QTY-to-InventoryTransaction stock-movement mapping** (`QTY`, `QTY MIV`, `QTY MRV`, `QTY ISSUE`, `QTY Remain`) — the real header schema doesn't even match the existing `RawQtyReceived`/`RawQtyIssued` aliases used to drive stock transactions today. Rewriting the stock/inventory-transaction business logic to understand these new quantity states is a separate, larger, higher-risk change to already-live inventory math, not requested by this metadata-focused directive (§12 explicitly: "Do not implement those future modules in this task"). All 8 quantity headers **are** captured correctly and distinctly as metadata (`Quantity`, `QtyCheck`, `QtyCheck2`, `MIVQuantity`, `MRVQuantity`, `IssuedQuantity`, `RemainingQuantity`, `CheckedQuantity`) — only the stock-ledger wiring was left untouched.
- **`temp unique` / `Temp ID` / `Count` / literal `"0"` header** — left unmapped (`CanonicalField = null`) per §7's explicit instruction not to promote temporary/import-control columns into business fields.

---

## 6. Duplicate / Re-import behavior (§9 — documented as required)

- **Same file uploaded again**: creates a new, independent `MrsImportBatch` with its own `MrsImportRowMetadata` rows. Nothing from the previous batch is overwritten — append-only across batches, matching the audit-history pattern already used elsewhere in this codebase (`VDRSubmission`, `DocumentHold`).
- **Same Material re-imported**: the pre-existing three-layer match (exact `ItemCode` → fuzzy normalized description → in-batch duplicate detection) prevents a duplicate `MaterialItem`; the row is marked `MatchedExisting` instead. Verified by test 7.
- **Changed Vendor/PO on a re-imported row for an existing Material**: `CommitAsync`'s Vendor-resolution code only runs in the "create new `MaterialItem`" branch — a `MatchedExisting` row never touches the existing item's fields. This means a changed Vendor on a re-import does **not** silently overwrite the original Material's Vendor — satisfies §9's explicit "Do not silently overwrite important existing Material metadata."
- **Changed Excel header structure**: headers are detected fresh per batch from that batch's own header row; no dependency on a previous batch's structure.

---

## 7. Files changed

- New: `PEMS.Domain/Entities/MrsImportRowMetadata.cs`
- New: `PEMS.Persistence/Servises/MrsMetadataCanonicalMap.cs`
- New: `PEMS.Tests/Inventory/MrsImportMetadataTests.cs` (14 tests)
- Modified: `MrsImportRow.cs`, `MrsImportBatch.cs` (domain)
- Modified: `MrsImportConfigurations.cs`, `PEMSDbContext.cs` (persistence)
- Modified: `MrsImportService.cs`, `IMrsImportService.cs` (alias fix, Vendor fix, metadata capture, transaction fix)
- Modified: `MrsImportController.cs` (real file save as `DocumentAttachment`)
- Modified: `MrsImportDtos.cs`, `MaterialItemDtos.cs` (new fields)
- Modified: `MaterialItemService.cs` (source-import traceability query)
- Modified: `Views/MrsImport/Review.cshtml`, `Views/MaterialItem/Details.cshtml`
- Migration: `20260819053012_AddMrsImportMetadataAndSourceDocument` (purely additive: 1 new table, 1 new nullable FK column — reviewed line-by-line before applying, no destructive operations)

---

## 8. Verification (all actually executed, not claimed)

- **Migration**: reviewed in full before `dotnet ef database update`; applied cleanly.
- **Build**: `PEMS.Web` and `PEMS.Tests` both build with 0 errors (remaining warnings are all pre-existing, in unrelated files).
- **Tests**: **108/108 passed, 0 failed, 0 skipped** (94 pre-existing + 14 new). One genuine gap was caught by a failing test during development (`Purchase Number` unmapped) and fixed before this final count — not silently patched afterward.
- **Graphify**: `graphify update .` run; verified via `graphify query "MrsImportRowMetadata canonical field vendor purchase order material"` that `MrsImportRowMetadata`, `MrsMetadataCanonicalMap`, `MrsImportRow`, `MaterialItem`, `Vendor`, and `DocumentAttachment` are all present and connected in the graph — not merely assumed from running the command.

---

## 9. Definition of Done — accounting

| Item | Status |
|---|---|
| Every Excel header dynamically captured | ✅ |
| Original header text preserved | ✅ |
| Canonical mapping where applicable | ✅ |
| Unknown headers preserved | ✅ |
| Vendor Name captured as metadata | ✅ |
| Purchase Number captured as metadata | ✅ |
| Metadata linked to correct Material | ✅ |
| Material traceable to Import Batch | ✅ |
| Import Batch traceable to Source Document | ✅ |
| Re-import behavior deterministic | ✅ (documented in §6) |
| Transaction safety | ✅ (staging now atomic; Commit's existing per-row save granularity is a pre-existing characteristic, not altered — documented, not silently left) |
| UI exposes important metadata | ✅ |
| Automated tests pass | ✅ 14/14 new |
| Full regression suite passes | ✅ 108/108 |
| Graphify updated | ✅ verified, not assumed |
| No unrelated production regressions | ✅ (only genuinely required files touched; QTY-to-stock business logic explicitly left alone) |
