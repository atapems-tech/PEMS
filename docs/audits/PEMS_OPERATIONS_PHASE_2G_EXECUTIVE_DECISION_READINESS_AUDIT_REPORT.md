# PEMS Operations Phase 2G — Executive Decision Readiness Audit Report

**Question audited:** is each open Packaging decision ready for its owner to answer, and is the role
mapping accurate enough to act on?
**Result:** the decision material is **READY FOR OWNER REVIEW**. No decision is approved.
**Code changed:** none. **Database changed:** none (read-only queries only).

## 1. Method

1. Baseline: branch `feat/cmms-passport-global-page-header`, .NET SDK 10.0.400, working tree with 43
   modified tracked files and 45 untracked entries that belong to earlier, unrelated work.
2. Confirmed no source file changed since the last full test run (`find -newer`, empty result).
3. Searched the repository for approval evidence; inspected the sign-off table.
4. Audited the real RBAC: code constants, seed blocks, SoD seeder, **and the live database**
   (read-only `SELECT`s on `AspNetRoles`, `AspNetUserRoles`, `RolePermissions`).
5. Verified the behaviors each decision touches (quality gate, barcode target registry, numbering
   defaults, lifecycle style, help/import architecture).
6. Wrote the workbook, sign-off baseline and this report.

## 2. Baseline Record

| Item | Result |
|---|---|
| Current branch | `feat/cmms-passport-global-page-header` |
| Latest commit | `ea22450` (procurement) |
| Build (last run, no source changed since) | Succeeded, 0 errors, 10 warnings |
| Full test baseline (last run) | Passed 1950, Failed 1, Skipped 0, Total 1951 (32m 47s); the failure is the known `MrsImportBatchCapacityTests` timing flake |
| Packaging code in solution | None |
| Packaging tables in live DB | None (`sys.tables` query returned no rows) |
| Latest migration | `20260923141435_AddContractApprovalRulePriority` |
| Existing Phase 2C gate | Present in `ProductionRunService.PostOutputToInventoryAsync` |
| Approval evidence | None — sign-off table blank; no `APPROVED` Packaging row anywhere |

## 3. Readiness of Each Decision for Owner Review

| ID | Question stated? | Options with pros/cons? | Impact analysed (migration, reporting, quality, inventory, barcode, workflow, audit, KPI)? | Recommendation separated from approval? | Owner + approver named? | Ready for owner review |
|---|---|---|---|---|---|---|
| D1 | ✔ | ✔ (4) | ✔ | ✔ | ✔ | **Yes** |
| D2 | ✔ | ✔ (2) | ✔ | ✔ | ✔ | **Yes** |
| D3 | ✔ | ✔ (2) | ✔ | ✔ | ✔ | **Yes** |
| D4 | ✔ | ✔ (3) + sub-questions | ✔ | ✔ | ✔ | **Yes** |
| D5 | ✔ | ✔ (3) | ✔ | ✔ | ✔ | **Yes** |
| D6 | ✔ | ✔ (3) | ✔ | ✔ | ✔ | **Yes** |
| D7–D12 | ✔ | ✔ | summarized | ✔ | ✔ | **Yes** (short form) |
| E-2…E-5 | ✔ | ✔ | summarized | ✔ | ✔ | **Yes** |

No decision is approved, so nothing moved to `OWNER REVIEW` either: that state means an owner has
the document and has started a review, which cannot be claimed from repository evidence.

## 4. Findings

| ID | Finding | Severity | Class | Evidence | Status |
|---|---|---|---|---|---|
| G-1 | Earlier documents (Phase 2F §6, first workbook draft, earlier master audit) said PEMS has 12 roles. The live database has **20** (8 extra CMMS work-order governance roles incl. `MNT.Manager`, `OPS.IssueAuthority`) | MEDIUM (my documentation error, changes the role mapping for Maintenance Manager) | Documentation discrepancy | `AspNetRoles` query; `WorkOrderGovernanceSeeder` | **Corrected** in the 2G workbook, 2F baseline and the earlier master audit |
| G-2 | Live `Production` grants equal the seeded defaults (4 roles) — no runtime drift | INFORMATIONAL | — | `RolePermissions` query | Recorded |
| G-3 | `QAManager` (release/reject) lacks `Hold`/`ReleaseHold`, so it cannot block or unblock a batch; `BlockBatch` needs `Hold` | MEDIUM | Existing implementation observation (not a defect claim: grants may be deliberate) | `ProductionRunController` lines 277–290; `RolePermissions` | Documented, not changed (outside Packaging scope) |
| G-4 | `Finance` and `HSE` roles have no Production grant | LOW | Permission gap vs governance | `RolePermissions` | Documented |
| G-5 | Only one live user holds `QAManager` | LOW | Operational continuity | `AspNetUserRoles` | Documented |
| G-6 | Master prompt's decision numbering differs from Phase 2D/2E/2F (D5 absorbs repack; D9 rework and D10 offline newly numbered; D12 ≡ E-1; handling unit not listed) | LOW | Documentation discrepancy | prompt vs `2D` register | Crosswalk added (workbook §2); earlier IDs preserved |
| G-7 | Production does not use `IWorkflowRuntime` (explicit transition dictionaries) while the master prompt §37 says to use the central platform where workflow is required | INFORMATIONAL | Architecture precedent | no `IWorkflowRuntime` reference in Production services | Handled as a technical decision in the 2H activation record |
| G-8 | The 2D "17 deleted filestore files" claim is stale; current state is five untracked entries, zero deletions | LOW | Documentation discrepancy | `git status` | Carried forward, unchanged |

## 5. Role Mapping Verdict

Of 14 governance roles: 3 map cleanly to an existing role (Complex Manager→`Viewer`,
Engineering Manager→`EngineeringManager`, Maintenance Manager→`MNT.Manager`); 4 map with a permission
gap that can be closed by grants on existing roles (Finance Manager, Process Engineering Lead,
Laboratory Manager→`QAManager`, HSE Manager); 5 have no suitable role and a technical assessment says
a new role is likely required (Operations Manager, Shift Supervisor, Operator, Packaging Manager,
Warehouse Manager); 2 cannot be determined (Commissioning Manager — depends on E-2; Security Manager).
The technical assessment is not an approval. Nothing was created or changed.

## 6. Conclusion

Owners can now answer D1–D6 from one document each. The program cannot proceed to implementation
until at least D1, D2, D3, D4 and D6 (and the D5 scope acknowledgement) carry an `APPROVED` row in
the sign-off baseline.
