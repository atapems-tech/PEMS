# Test inventory for planning-related code (PPC Stage 0, read-only, 2026-11-14)

**Method:** counted test methods (`[Fact]` and `[Theory]`) per file with the search tool in `PEMS.Tests`. **No test was run** in this pass. Database type is taken from the file: "verified" means the file or its helper was read; "by name" means the class name indicates the type and it was not opened.

## 1. Production orders, runs, batches, targets (Cmms)

| File | Tests | Database | Covers |
|---|---|---|---|
| `PEMS.Tests/Cmms/ProductionOrderServiceTests.cs` | 9 | InMemory (per Audit-03 X21) | Order create, plan, release, transitions |
| `PEMS.Tests/Cmms/ProductionRunServiceTests.cs` | 15 | **InMemory, verified** (`PemsDbContextFactory.CreateInMemory`, line 24) | Run create, start, pause, resume, hold, complete, cancel; input and output recording |
| `PEMS.Tests/Cmms/ProductionInventoryQualityGateTests.cs` | 8 | InMemory (per Audit-03) | Batch QC gate and posting to inventory |
| `PEMS.Tests/Cmms/InventoryBatchTraceabilityTests.cs` | 9 | InMemory (per Audit-03) | Batch-linked ledger rows, batch balances, transfers keep batch identity, quality status from production batch |
| `PEMS.Tests/Cmms/ProductionTargetServiceTests.cs` | 7 | InMemory (per Audit-03) | Target create, validation |
| `PEMS.Tests/Cmms/ProductionDowntimeServiceTests.cs` | 7 | InMemory (per Audit-03) | Downtime events |
| `PEMS.Tests/Cmms/OperationsReportServiceTests.cs` | 7 | InMemory (per Audit-03) | Operations summary (actual against target) |

Subtotal: **62 tests** in production and batch areas.

## 2. Net demand, posting, replenishment (Inventory)

| File | Tests | Database | Covers |
|---|---|---|---|
| `PEMS.Tests/Inventory/NetDemandCalculatorTests.cs` | 23 | Pure (no database; per Audit-03) | Net demand arithmetic |
| `PEMS.Tests/Inventory/NetDemandCollectorTests.cs` | 23 | Per Audit-03 | Demand collection from CMMS work-order material |
| `PEMS.Tests/Inventory/NetDemandServiceTests.cs` | 9 | Per Audit-03 | Net demand run |
| `PEMS.Tests/Inventory/NetDemandIncomingSupplyTests.cs` | 4 | Per Audit-03 | Incoming supply handling |
| `PEMS.Tests/Inventory/NetDemandSqlTests.cs` | 2 | SQL Server (by name) | Net demand on SQL Server |
| `PEMS.Tests/Inventory/ProductionPostingIdempotencyTests.cs` | 2 | **SQL Server, verified** (`UseSqlServer`, `PemsDbContextFactory.ConnectionString`) | Posting idempotency and the SQL Server lock |
| `PEMS.Tests/Inventory/Replenishment*Tests.cs` (4 files) | 43 | Mixed; `ReplenishmentSqlTests` 7 by name SQL | Replenishment rules, recommendations, isolation |

Subtotal: **106 tests** in net-demand, posting and replenishment areas.

## 3. Adjacent (not planning)
- `PEMS.Tests/Cmms/ExecutiveKpiTests.cs`: 18 tests, KPI dashboard. Not planning.

## 4. What is NOT tested (gaps)

| Planning capability | Test exists? |
|---|---|
| BOM or formula explosion | **No** |
| Routing or operation sequence | **No** |
| Production MRP (demand from production) | **No** (only procurement net demand) |
| RCCP | **No** |
| CRP and capacity load | **No** |
| MPS (time-phased product schedule) | **No** |
| APP and MPP (aggregate plan, versioning, approval) | **No** |
| Scheduling and sequencing (PS, APS) | **No** |
| Shift and production calendar | **No** |
| UOM conversion (ConversionFactor) | **No** |
| Lot or serial genealogy beyond one level | **No** (one-level batch balance only) |
| Plan versioning and historical reproducibility | **No** |
| Reversal of posted production stock | **No** |

## 5. Observations for the findings
- Production service tests run against an in-memory context. Concurrency and the SQL Server posting lock are tested only in `ProductionPostingIdempotencyTests` (2 tests, SQL Server). Whether that suite runs in CI was not verified.
- Batch traceability (`InventoryBatchTraceabilityTests`, 9 tests) exists at the inventory ledger level. This is evidence that finding F13 needs a refined statement: batch identity is kept on ledger rows and batch balances are derived from them; stock records have no batch dimension.
- The test gate tool (`tools/test-gates/gates.py`) was not run in this pass.

## 6. Totals
- Production and batch tests: 62. Net demand, posting and replenishment: 106. Total planning-adjacent: **168**. Tests for the planning capabilities of the target design (MRP, RCCP, CRP, MPS, APP, scheduling, BOM, routing, calendar): **0**.
