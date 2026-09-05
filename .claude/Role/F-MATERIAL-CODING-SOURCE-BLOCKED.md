# F — Material Coding (Appendix 4): SOURCE-BLOCKED

**Status:** OPEN / SOURCE-BLOCKED
**Date:** 2026-08-18

## What this covers

`.claude/Role/VENDOR DOCUMENTATION MANAGEMENT PROCEDURE.md`, Appendix 4 ("Material Coding") defines a
4-digit hierarchical numeric code system (e.g. `0900` = Pumps, `0600` = Heat Exchangers) used to
identify equipment/material types, with further `+NN` 2-digit attribute sub-codes for piping/
instrument/electrical items, and is explicitly meant to be embedded as a segment in official
document numbers (per §4.3.1, the `MC` segment of `J-85-XXX-ME-DSH-0611-0001-001-0`).

## Why it is blocked

The source document's own text states, verbatim, at the start of Appendix 4:

> "In the original Word document, this entire appendix (contents index and detailed code
> breakdown) is embedded as picture objects (WMF/EMF images), not as native text or tables. The
> content below was reconstructed via OCR ... OCR is not 100% reliable on this kind of dense
> tabular image — please treat this appendix as a best-effort transcription and verify against the
> original document for critical use."

This is not a formatting inconvenience — it is the document's own explicit warning that the
numeric codes in this section cannot be trusted as authoritative without verification against the
original image-based source (which this session does not have access to). Numeric transcription
errors in a 4-digit hierarchical code are exactly the kind of error that is easy to make and hard
to catch (e.g. a single OCR digit substitution silently redirects a document into the wrong
material category).

## What was NOT done (and why)

- The ~120+ material codes and their `+NN` attribute sub-codes were **not** seeded into the
  database as a lookup table (unlike Appendix 1/2/3, which are native text/tables in the source
  document and were seeded as `Discipline`, `DocumentTypeCode`, and `VendorDocumentTypologyCode`
  respectively during this session).
- No enum, constant list, or "official code" validation was built against Appendix 4 values.
- No auto-generation logic resolves a Material Code from Equipment/Material type — this must stay
  a human decision until the source is verified.

## What IS safe today

- `VDRItem.MaterialCode` (added earlier in this session, for gap B/CONTRACTOR-numbering support)
  is a **plain free-text field**, manually entered by the user, with no claim of being validated
  against an authoritative code table. This is the correct, honest state — it does not pretend to
  be Appendix-4-verified.
- `Equipment.EquipmentType` / `MaterialItem.MaterialCode` (pre-existing entities, confirmed in the
  earlier gap analysis) are likewise free text, not tied to any Appendix 4 mapping.
- No part of the numbering services (`DocumentNumberingService`, `VdrDocumentNumberingService`)
  validates or fabricates a Material Code — they accept whatever the user enters into
  `VDRItem.MaterialCode` and pass it through unchanged into the composed CONTRACTOR-format number.

## Confirmed: no fabricated/uncertain seed data exists

A repository-wide check for any existing Material Coding lookup table, enum, or seed data tied to
Appendix 4 numeric codes found none. There is nothing to mark `SourceBlocked` on an existing
record, because nothing was ever fabricated from this section in the first place — the blocking
happened at design time (during the B/gap-analysis work) rather than after-the-fact remediation.

## What is required to close F

1. The original `.docx` (or a PDF export with the embedded WMF/EMF images intact, or a clean
   re-scan) of the "VENDOR DOCUMENTATION MANAGEMENT PROCEDURE" so the Appendix 4 tables can be
   read directly rather than through this session's OCR pass.
2. Once available, re-run the same seeding pattern already used for Appendix 1/2/3: a
   `MaterialCode` lookup entity (`Code`, `Description`, parent/attribute relationship for the
   `+NN` sub-codes), seeded via `HasData`, with a `MaterialCodeMatcher` mirroring
   `DisciplineMatcher`/`DocumentTypeMatcher`/`VendorDocumentTypologyMatcher`.
3. Once seeded, `VDRItem.MaterialCode` can be upgraded from free text to a validated
   lookup-backed field (matching how `Discipline`/`DocumentTypeCode`/`VendorDocumentTypologyCode`
   are validated today), and `VdrDocumentNumberingService` can reject unrecognized codes instead
   of accepting anything.

Until then, F remains explicitly open. Treating the current OCR transcription as authoritative and
seeding it anyway was considered and rejected, per this directive's explicit instruction not to
fabricate or auto-correct uncertain engineering master data.
