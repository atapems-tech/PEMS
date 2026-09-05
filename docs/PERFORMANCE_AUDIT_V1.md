# PEMS Performance Audit V1

**Scope:** PEMS-cmms-edit-empoleeys / master
**Status:** Static architecture audit completed; runtime load measurements pending.

## 1. Current runtime topology

- ASP.NET Core MVC on .NET 10.
- EF Core + SQL Server (`PEMSConnection`).
- Hangfire + Hangfire.SqlServer using the same SQL Server connection/database.
- Ollama accessed through a typed `HttpClient`; default timeout is 15 minutes.
- Redis is optional; when `ConnectionStrings:Redis` is empty, `MemoryCacheService` is used.
- SignalR is enabled.
- Serilog writes console + rolling file logs.
- A global authorization filter is applied to MVC controllers.
- A request middleware resolves `PEMSDbContext` and current-user services for every request.
- Workflow services use the same scoped `PEMSDbContext` instance for the request.

## 2. High-risk performance findings

### P0 — AI/Ollama request isolation
`OllamaService` is registered with `HttpClient` and a 15-minute timeout. AI work must remain outside synchronous web-request execution. Verify every upload/intelligence path uses Hangfire/background processing and never waits on Ollama for a normal page request.

**Test:** concurrent AI jobs while continuously exercising Login, Dashboard, Search, Engineering Documents and CMMS.

**Failure condition:** AI load materially increases normal web P95/P99 latency or exhausts request/thread resources.

### P0 — Hangfire and application workload share SQL Server
Hangfire SQL storage and `PEMSDbContext` both use `PEMSConnection`. This creates a contention path between job polling/state updates and business queries.

**Test:** AI/document jobs at 5/10/20/50 concurrent queued jobs while running normal user traffic. Monitor SQL CPU, waits, blocking, deadlocks, log growth and query latency.

### P0 — SaveChanges has an additional database write for audit
`PEMSDbContext.SaveChanges/SaveChangesAsync` performs the business save, then creates `AuditLog` rows and performs a second `SaveChanges` when audit entries exist. Audit preparation also enumerates `ChangeTracker` and modified properties.

**Risk:** write-heavy CMMS, workflow and document operations pay an additional persistence round trip and ChangeTracker traversal.

**Test:** WorkOrder/document update throughput with and without audit-heavy traffic; measure P95, SQL CPU and logical reads.

### P1 — Large DbContext surface
`PEMSDbContext` exposes a very large number of DbSets spanning Engineering, VDR, Procurement, CMMS, AI, Workflow, QC/QA and correspondence. This is not itself a runtime defect, but it increases model complexity and makes query-shape discipline critical.

**Test:** identify top slow queries and queries with large Include graphs; inspect generated SQL and execution plans.

### P1 — Global search fan-out
`Program.cs` registers many `ISearchableEntityProvider` implementations (Equipment, EngineeringDocument, VDR, Letter, Vendor, Project, Employee, Department, Discipline, MR, WorkOrder, Contract, revisions, TQ, MOC, SparePart, tags, attachments, notifications, etc.).

**Risk:** a single global-search request may fan out into many database queries. This is a prime candidate for load-test amplification.

**Test:** 10/25/50/100 concurrent search requests with short, common and empty search terms. Capture SQL command count per request and P95/P99.

### P1 — Default cache is process-local
When Redis is not configured, `MemoryCacheService` is selected. This is acceptable for a single instance but does not provide shared cache state across multiple application instances.

**Test:** single-node baseline first; if scaling horizontally, repeat with Redis and compare hit ratio/latency and consistency.

### P1 — File/document processing
The project contains document extraction/OCR/AI ingestion services and upload folders. Large PDF/Office/image inputs can create CPU, memory and I/O pressure.

**Test:** 1 MB, 10 MB, 50 MB and 100 MB representative files; concurrent uploads 1/5/10/20. Monitor working set, GC, disk I/O, request duration and queue depth.

### P2 — Logging under load
Serilog writes a rolling file and console at Information level. High-volume request/error logging can become an I/O bottleneck during stress testing.

**Test:** compare normal logging and a controlled high-volume workload; monitor log-file throughput and application latency.

### P2 — Per-request DbContext resolution in middleware
The audit middleware resolves `PEMSDbContext` for every request and populates current-user metadata. Because the context is scoped this should not create a second context in the same request, but it makes DbContext part of every authenticated request's dependency path.

**Test:** authenticated lightweight endpoint at 100/200/300 concurrent users.

## 3. Mandatory test matrix

| Test | Concurrency | Duration | Main objective |
|---|---:|---:|---|
| Baseline | 1, 5, 10 | 5 min/level | establish P50/P95/P99 |
| Load | 25, 50, 100 | 10 min/level | find normal capacity |
| Stress | 150, 200, 300, 500 | until degraded | find breaking point |
| Spike | 10 -> 200 | 5 min | recovery behavior |
| Soak | 50-100 | 4 hours | leaks/slow degradation |
| Search | 10-100 | 10 min/level | provider fan-out |
| Upload | 1-20 | 10 min/level | file/OCR pressure |
| AI | 5-50 jobs | until queue drains | Ollama/SQL isolation |
| CMMS | 25-200 users | 10 min/level | WorkOrder/Asset throughput |

## 4. Primary user journeys

1. Login/logout.
2. Dashboard load.
3. Global search.
4. Engineering Document list/filter/detail.
5. Document upload.
6. VDR list/detail/search.
7. CMMS Asset list/detail.
8. CMMS WorkOrder create/update/status transition.
9. Notification retrieval.
10. AI document ingestion and processing.

## 5. Required metrics

### Application
- Requests/sec
- Concurrent requests/users
- P50/P95/P99 latency
- HTTP 4xx/5xx rate
- Timeout rate
- CPU
- Working set / RSS
- GC allocation and pauses
- ThreadPool queue/thread counts

### SQL Server
- CPU
- Memory
- Batch requests/sec
- Connection count
- Wait stats
- Blocking
- Deadlocks
- Query duration
- Logical reads/writes
- TempDB pressure
- Transaction log growth

### Hangfire
- Enqueued jobs
- Processing jobs
- Failed jobs
- Retry count
- Queue latency
- Job duration
- Worker utilization

### Ollama
- Active requests
- Queue/wait time
- Generation time
- CPU/GPU utilization
- Memory/VRAM
- Timeout/error rate

## 6. Initial hypotheses to validate

1. Global Search is likely to be one of the first DB amplification points.
2. Audit logging can become a measurable write-path cost under CMMS/workflow/document write load.
3. Shared SQL Server between Hangfire and application traffic can create contention during AI/document bursts.
4. Synchronous AI/OCR/file processing is the highest-risk path if any request still waits for it.
5. Local MemoryCache is not a scalability problem for one node but becomes a consistency/scalability limitation for multiple nodes.

These are hypotheses, not measured failures.

## 7. Pass/fail gates

Initial engineering gates:

- P95 < 2 s for normal interactive pages under target load.
- P99 < 5 s for normal interactive pages under target load.
- HTTP 5xx < 1% during accepted load.
- No deadlocks during accepted load.
- No sustained connection-pool exhaustion.
- AI processing must not make normal interactive traffic unavailable.
- After a spike, the application must return to within 20% of pre-spike latency without restart.
- Four-hour soak must show no unbounded growth in memory, connections or job queue.

Final thresholds must be approved against the real PEMS SLA and deployment hardware.

## 8. Next implementation step

Create an isolated `performance-tests/` k6 suite. It must not modify production application behavior. The suite should provide separate scenarios for baseline, load, stress, spike, search, upload, CMMS and AI. Runtime monitoring should be captured separately for ASP.NET, SQL Server, Hangfire and Ollama.

Do not optimize based on this document alone. Run the measurements first, record evidence, then change code/database configuration and repeat the identical test matrix.
