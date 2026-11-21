# APP and MPS assessment (PPC Stage 0, read-only, 2026-11-14)

## Method
Word-level search (case-insensitive) for the terms APP, MPP, MPS, MRP, RCCP, CRP, APS, "aggregate plan", "master plan", "master production", "capacity plan", "production plan" across `*.cs`, `*.cshtml`, `*.md`, `*.resx`, `*.js` in the repository, excluding worktrees, legacy folders, migrations, build output, the graph folder, `.claude`, and this package. Then the same terms across `plan/` and `docs/`.

## Result
- **Code, views, resources and existing documents: 0 matches** for APP, MPS, MPP, RCCP, CRP, APS and the planning phrases above. (Count: 0.)
- **Matches found only in this package's own files** (documents and plan rows 0561 to 0564 created in this batch). These are not evidence of existing capability.
- **Conclusion:** APP and MPS are **absent** in the repository. Finding F4 is **confirmed for the absence of APP and MPS**.

## What exists that is nearest to APP and MPS (and why it is not them)

| Nearest item | Where | What it is | Why it is not APP or MPS |
|---|---|---|---|
| `ProductionOrder` (PlannedQuantity, PlannedStart, PlannedFinish) | `PEMS.Domain/Entities/ProductionOrder.cs` | One order for one material and quantity, with dates | No product family, no time bucket, no horizon, no version, no approval of an aggregate plan. This is an order, not a plan. |
| `ProductionTarget` (per date, material, work centre, shift) | `PEMS.Domain/Entities/ProductionTarget.cs` (service `ProductionTargetService.cs`) | A daily target per material and work centre | Target, not a plan; no aggregation across products; no approval; the material is not checked against the order (Audit-01 F19). |
| `NetDemandCalculator` | `PEMS.Application/Inventory/NetDemandCalculator.cs` | Procurement net requirement on stock | Material side only; demand is CMMS work orders only; no production demand (Audit-03 X5). |
| `OperationsReportService` | `PEMS.Persistence/Servises/OperationsReportService.cs` | Actuals against target | Reporting, not planning. Mixes units (Audit-01 F16). |

## Assessment by term (owner definitions, latest wording)

| Term | Owner definition (2026-11-14, confirmed as architecture decision) | PEMS support | Gap | Status |
|---|---|---|---|---|
| APP | Process of macro planning of production at product-family level (owner, earlier wording: "فرآیند برنامه‌ریزی کلان تولید در سطح خانواده محصولات") | None | No family grouping, no aggregate horizon, no aggregate process | Absent |
| MPP | Master Production Plan: the approved, versioned plan that is the output of the APP process; a document or entity, not an engine (owner, 2026-11-14) | None | No versioned approved plan entity | Absent. Definition confirmed (ADR-001). |
| MPS | Detailed production plan of the products (owner, 2026-11-14) | `ProductionOrder` is the nearest; one order only | No time-phased product schedule, no buckets, no netting | Absent |

## Finding F4 status after this assessment
- **Absence of APP and MPS:** confirmed by search (0 matches) and by the nearest items above.
- **Absence of plan versioning:** confirmed (no version field on `ProductionOrder`; no update method in `ProductionOrderService`; Audit-03 X14).
- **Severity and gate effect:** set in `Critical-Major-Findings-Register.md`. Severity is separate from gate effect.

## Limits
- The search covers the repository only. An external planning spreadsheet or another system is not in scope.
- The owner's earlier, different wording of MPP is recorded in the glossary; the confirmed definition is the one in ADR-001.
