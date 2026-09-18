# Phase 4 — Article Count Reconciliation (Increment 3A.1)

## The discrepancy

The Increment 3A final report stated "Total knowledge articles: **28**" against a live database total of **80** (53 baseline + reported 28 = 81, but live = 80). This document identifies and corrects the exact source of the error.

## Root cause (confirmed against live data and seeder source)

The "28" figure was computed as `Concept-type article count (16) + DisciplineGuide (4) + StandardReference (8)`. That arithmetic is internally correct for *ArticleType totals currently in the database*, but those 16 Concept-type rows are **not all new to Increment 3A**.

One of them — `import-batches-overview` — was created in **Increment 2** (the shared Help-key article for the six staged-import controllers) and was deliberately typed `ArticleType = "Concept"` at that time, because it documents a reusable cross-cutting pattern rather than one specific page. It has always been part of the 53-article baseline count reported at the start of Increment 3A.

`HelpKnowledgeSeeder` (the actual Increment 3A code) creates exactly **15** Concept articles, not 16 — verified by the seeder's own idempotency test (`SeedAsync_ExactCounts_Concepts15_Disciplines4_Standards8`) and by the live query below.

## Corrected arithmetic

```
Baseline (end of Increment 2):        53 articles
New in Increment 3A:
  Concepts (HelpKnowledgeSeeder):     15
  Discipline Guides:                   4
  Standard References:                 8
  ------------------------------------------
  Subtotal new:                       27
------------------------------------------------
Total (live database, verified):      80   ✓ matches exactly
```

No articles were deleted, merged, reclassified, or double-counted in the database itself — the error was entirely in how the Increment 3A summary report *described* the count, not in the seeded data. The live database has always been correct at 80.

## Machine-generated inventory

Full per-article inventory (all 80 rows) generated directly from `PEMSDB` via `SqlClient`/`Export-Csv` (not manually transcribed) is at:

```
docs/help/phase4_article_inventory.csv
```

Columns: `ArticleId, Slug, ArticleType, Category, IsPublished, HasEnglishTranslation, HasPersianTranslation, CreatedOrSeededBy, IsBaselineArticle, IsNewKnowledgeArticle`.

Verified totals from that file: **53** rows with `IsBaselineArticle=Y`, **27** rows with `IsNewKnowledgeArticle=Y`, **80** total — every row has exactly one of the two flags set to `Y` (mutually exclusive, confirmed by construction).

## Corrected statement for all future Phase 4 reporting

- Increment 3A added **27** new Help articles (15 Concepts + 4 Discipline Guides + 8 Standard References), not 28.
- The database's `Concept` ArticleType total (16) legitimately spans two increments (15 from 3A, 1 from Increment 2) and should never again be quoted alone as "new articles this increment."
