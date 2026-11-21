# RoutingRule assessment (PPC Stage 0, read-only, 2026-11-14)

## Question
Does `RoutingRule` provide a manufacturing routing (operation sequence, work centres, setup and run time) that PPC could build on?

## Answer
**No.** `RoutingRule` is a document-routing table for the AI intake and cartable. It maps a document category to a PEMS business module. It has no production concept. Assessment status: **assessed, not a manufacturing routing.** Manufacturing routing is **absent**.

## Evidence
- **Entity** `PEMS.Domain/Entities/RoutingRule.cs` (class `RoutingRule`). Fields: `RoutingRuleId`, `DocumentCategory` (for example "Engineering Drawing"), `TargetModule` (MDR, VDR, MR, MRQ, Contract, CMMS, Unrouted), `AllowAutoRouting`, `Description`, `IsActive`, `CreatedDate`. Its own summary says: "جدول Routing داده‌محور ... هر دسته‌بندی سند AI به کدام ماژول کسب‌وکار هدایت شود." No operation, sequence, work centre, setup, run time or quantity.
- **Service** `PEMS.Persistence/Servises/RoutingRuleService.cs`: `GetAllAsync`, `SetRuleAsync`, `GetTargetModuleAsync`, `AllowsAutoRoutingAsync`, `GetConfidenceSettingsAsync`, `UpdateConfidenceSettingsAsync`, `SeedDefaultsAsync`. Confidence thresholds are for AI document classification.
- **Seed defaults** (`RoutingRuleService.cs` lines 23–39): Engineering Drawing → MDR; Vendor Drawing, Vendor Datasheet, Vendor Manual → VDR; Material Requisition → MR; Material Requisition Query → MRQ; Contract → Contract; Correspondence, Meeting Minutes, Other → Unrouted.
- **Consumers (non-test):** `PEMS.Web/Areas/Admin/Controllers/RoutingRulesController.cs`; `PEMS.Web/Controllers/LetterCartableController.cs`; `PEMS.Web/Program.cs`; `PEMS.Persistence/Servises/IncomingCorrespondenceService.cs`; `IntakeService.cs`; `LetterService.cs`; `LetterRoutingRuleService.cs` (separate letter routing); `PEMS.Application/Interfaces/IRoutingRuleService.cs`; `PEMS.Application/Common/CartableRules.cs`.
- **Tests:** `PEMS.Tests/Cartable/CartableRulesTests.cs`, `CartableSettingsServicesTests.cs`, `CartableWebTests.cs`; `PEMS.Tests/Web/AdminSettingsListPagesTests.cs`, `RelatedListComponentTests.cs`. None concern production routing.
- **Other "routing" types in the repo, none manufacturing:** `LetterRoutingRule.cs` (letters), `StorageRoute` (storage), `PartCatalogueRoutingPlanner` (part catalogue), workflow routing in `PEMS.WorkflowPlatform`.

## Consequences for PPC
1. `RoutingRule` cannot be extended into a manufacturing routing without changing its meaning. It should **not** be reused for operations. A separate routing contract is needed (see document 06, AD-09 and the BOM/routing decision OD-03).
2. Finding **F1** (no BOM, recipe or routing) stays **confirmed for routing** on this evidence. Previous status "routing not verified" is now resolved.
3. Naming risk: the term "Routing" is used for four different concepts. The glossary must say which is meant.

## Status
| Item | Status |
|---|---|
| RoutingRule purpose | Assessed: document routing |
| Manufacturing routing | Absent (confirmed) |
| Reuse for PPC routing | Not recommended |
| Owner decision | OD-03 (routing), with naming decision for the four uses of "routing" |
