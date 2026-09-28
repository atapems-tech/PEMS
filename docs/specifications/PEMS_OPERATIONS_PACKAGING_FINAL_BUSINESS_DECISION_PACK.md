# PEMS Packaging — Final Business Decision Pack

**For:** Complex Manager and the named decision owners. **Status of Packaging: C — IMPLEMENTATION
BLOCKED.** Nothing in this pack is approved. Every decision is `OPEN`.

**How to read it.** Each decision has three parts, always in this order and always visibly
separated:

> **OPTIONS** — what the business can choose, and what each choice means.
> **TECHNICAL RECOMMENDATION — NOT BUSINESS APPROVAL** — engineering's suggestion, kept apart.
> **BUSINESS DECISION** — a blank block only the named owner can fill.

Nothing is built, granted or changed by this pack. Record answers in
`PEMS_OPERATIONS_PACKAGING_FINAL_SIGNOFF_SHEET.md` and mirror them into the machine-checked record
`PEMS_OPERATIONS_PHASE_2G_BUSINESS_SIGNOFF_BASELINE.md` (see the sign-off sheet, "After signing").

---

## 1. One-Page Summary

| ID | Decision | Options | Technical recommendation (NOT approval) | Business owner | Approver | Status |
|---|---|---|---|---|---|---|
| D1 | What is a "Package"? | A saleable unit · B package unit of one product · C logistics unit · D layered (product package + handling unit) | B (leaves room for D later) | Packaging Manager | Complex Mgr, Laboratory Mgr | OPEN |
| D2 | Can a package hold more than one product? | A one item · B several items · C one product + packaging components · D other | A for the *content*; decide packaging *materials* separately (see D2) | Packaging Manager | Engineering Mgr, Complex Mgr | OPEN |
| D3 | Can a package come from several batches? | A one batch · B several contributions · C primary + secondary genealogy | B if blending happens, otherwise A; not C | Process Engineering Lead | Packaging Mgr, Laboratory Mgr | OPEN |
| D4 | Gross / Tare / Net | A gross+tare measured, net derived · B gross+net measured, tare derived · C all three stored + validation · D weighing events, latest is authoritative | A, recorded as weighing events (D's storage habit) | Finance Manager | Laboratory Mgr, Complex Mgr | OPEN |
| D5 | Split / merge / repack | Acknowledge "out of initial Phase 3 scope" | Exclude from first release | Laboratory Manager | Packaging Mgr, Operations Mgr | OPEN (acknowledgement needed) |
| D6 | Bulk vs packaged product identity | A same item · B separate items · C parent/child · D separate items + mapping table | Schema-neutral design; B needs the fewest changes to existing structures | Finance Manager | Packaging Mgr, Complex Mgr | OPEN |
| D7 | Who releases a package? | A Laboratory/QA · B existing `QAManager` stands in · C separate Packaging Quality role · D other | B, if the business confirms `QAManager` is the laboratory authority | Laboratory Manager | Engineering Mgr | OPEN |
| E-4 | Packaging permission module | A reuse Production · B dedicated Packaging module · C hybrid | C | Complex Manager | Laboratory Mgr, Engineering Mgr | OPEN |
| E-5 | Which PEMS roles represent the 14 organizational roles? | see §10 | see §10 | Complex Manager | Engineering Mgr, Finance Mgr | OPEN |

Not in this workshop but still open and needed later: **D12 / E-1 financial treatment** (needed
before packaging inventory is posted, not before Phase 3A), D8–D11, E-2, E-3 — see §11.

## 2. Current State (verified against code and the live database)

- No Packaging code, table or migration exists. Latest migration: `20260923141435_…AddContractApprovalRulePriority`.
- Existing production quality lifecycle: Produced → AwaitingQC → Released | Blocked | Rejected
  (Blocked → AwaitingQC). Inventory posting of a production batch is refused unless it is `Released`
  (enforced in `ProductionRunService`, not only in the screen). This is unchanged.
- The product master is `MaterialItem`: one unit of measure (`UomId`), flags `IsBatchControlled` and
  `IsSerialized`; **no parent/child field; no bill-of-materials entity exists**. Stock is kept per
  material / warehouse / location — there is **no "packaged vs bulk" dimension**. `MaterialUOM`
  has a conversion factor that nothing uses.
- Material is referenced by procurement (`MrsImportRow`, `IrnReceiptLine`, `StoreRequisitionLine`),
  maintenance (`JobPlanRequiredMaterial`, `MaintenanceSpareUsage`, `WorkOrderMaterial`,
  `MaterialReservation`) and production (`ProductionOrder/Batch/Input/Output/Target`).
- Approvals recorded: **0 of 16.** Sign-off table in the earlier pack is blank.

## 3. Definitive Live Role Baseline (20 roles)

Queried read-only from `AspNetRoles`, `AspNetUserRoles`, `RolePermissions` on 2026-09-25. The earlier
statement "12 roles" was wrong; it read only the code constants. 8 of the 20 are CMMS work-order
governance roles created by `WorkOrderGovernanceSeeder`. **Production** grants exist for exactly four
roles; "—" means the role has no row for that module.

| Role | Users | Existing source | Prod View | Prod Create | Prod Edit | Prod Approve | Quality authority | Inventory authority | Workflow authority | Notes |
|---|---|---|---|---|---|---|---|---|---|---|
| ContractManager | 1 | code constant | — | — | — | — | none | none | none | Contracts only |
| DisciplineEngineer | 2 | code constant | — | — | — | — | none | none | WorkOrder row, no approve | |
| DocumentController | 1 | code constant | — | — | — | — | none | none | none | |
| **EngineeringManager** | 2 | code constant | ✔ | ✔ | ✔ | ✔ | Release/Reject (Approve) **and** Block/Unblock (Hold/ReleaseHold) | MasterData + MaterialItem C/E, Barcode Approve | none | Holds Create **and** Approve; SoD pair blocks the same user doing both on one record; also Delete |
| EngineeringSupervisor | 2 | code constant | — | — | — | — | none | MasterData + MaterialItem C/E | none | No Production row |
| **Finance** | 1 | code constant | — | — | — | — | none | none | none | Contracts/Tender edit only |
| **HSE** | 1 | code constant | — | — | — | — | none | none | none | Contracts edit only; no stop-work concept exists |
| Legal | 1 | code constant | — | — | — | — | none | none | none | |
| maintenance planning | 1 | work-order seeder | — | — | — | — | none | none | none | |
| **MaintenanceEngineer** | 1 | code constant | ✔ | ✔ | ✔ | — | Block/Unblock only (Hold/ReleaseHold), cannot release | MasterData + MaterialItem C/E, Barcode Approve; can post production output (needs Production.Create) | none | |
| MNT.DisciplineHead | 1 | work-order seeder | — | — | — | — | none | none | WorkOrder create/edit | |
| MNT.Expert | 0 | work-order seeder | — | — | — | — | none | none | WorkOrder edit | No user |
| **MNT.Manager** | 1 | work-order seeder | — | — | — | — | none | none | **WorkOrder Approve** (the only role with it) | Named in `WorkOrderWorkflow` |
| MNT.PlanningExpert | 1 | work-order seeder | — | — | — | — | none | none | WorkOrder create | |
| MNT.PlanningHead | 1 | work-order seeder | — | — | — | — | none | none | WorkOrder create | |
| MNT.SeniorExpert | 1 | work-order seeder | — | — | — | — | none | none | WorkOrder edit | |
| **OPS.IssueAuthority** | 1 | work-order seeder | — | — | — | — | none | none | WorkOrder view only | Not an operations line-management role |
| **QAManager** | 1 | code constant | ✔ | — | — | ✔ | **Release/Reject** of batches; **IRN (incoming receipt) Approve**; **no Hold/ReleaseHold** | IRN Approve only | none | Only one user holds it |
| SystemAdmin | 2 | code constant | bypass | bypass | bypass | bypass | bypass | bypass | bypass (workflow management still needs an explicit grant) | General allow-all with break-glass exceptions |
| Viewer | 2 | code constant | ✔ | — | — | — | none | none | none | Read-only |

No live role name contains Warehouse, Packaging, Laboratory or Operations-manager wording. There are
24 role assignments in total.

---

## 4. D1 — Package Definition

**Question.** In PEMS, what is a Package?

| | **A. Saleable product unit** | **B. Package unit of one product** | **C. Logistics / handling unit** | **D. Layered: product package + handling unit** |
|---|---|---|---|---|
| Definition | A filled container that is the commercial item sold | A filled container holding a defined quantity of one product from identified batch(es) | A pallet/carton/cage grouping items for movement | B as the inner layer, C as a separate outer layer |
| Business meaning | "What the customer buys" | "What was physically packed" (need not equal a sales item) | "What the warehouse moves" | Both questions answered separately |
| Data model | One row per unit; product = a sellable `MaterialItem` (ties to D6) | One row per unit: one `MaterialItem`, quantity, UOM, batch link(s), identity | Container row holding other things; hierarchy | Two related entities |
| Traceability | Unit → batch | Unit → batch(es) → run → order | Container → contents → batch; extra hop | Full chain, two levels |
| Inventory effect | Stock unit is the sellable item | Stock unit is the material quantity | Stock unit ambiguous (contents vs container) | Contents drive stock; container is location/movement |
| Barcode effect | Unit barcode | Unit barcode | Container barcode | Both, two target types |
| Quality effect | Status per saleable unit | Status per unit | Status of contents, not container | Per unit; container inherits view |
| Reporting effect | Sales-oriented counts | Production/packaging counts and yield | Movement counts | Both |
| Operational effect | Matches order picking | Matches the packing line | Matches shipping | Most complete |
| Implementation complexity | Medium | Medium | High (hierarchy) | Highest |
| Migration impact | New tables; depends on D6 | New tables | New tables + hierarchy | New tables, built in two steps |
| Risks | Couples definition to D6; can't record a packed unit that has no sales item | Does not answer pallet questions | Loses per-unit quality/identity | More to build and test |

**TECHNICAL RECOMMENDATION — NOT BUSINESS APPROVAL:** B. It is the smallest complete unit
(identity + quantity + traceability) and does not close the door on D: a handling-unit layer can be
added later without altering B. *Change-control note:* the earlier phases' "filled container"
recommendation corresponds to this option B; the options have been re-cut, the intent is unchanged.

**BUSINESS DECISION**
Selected Option: __________________
Approver: __________________
Date: __________________
Evidence: __________________
Conditions: __________________

---

## 5. D2 — Mixed-Product Package

**Question.** May one package contain more than one `MaterialItem`?

| | **A. Exactly one item** | **B. Several items** | **C. One product + packaging components** | **D. Other approved model** |
|---|---|---|---|---|
| Meaning | Content is one product | Content is an assortment | Content is one product; the bag/drum/label consumed are recorded as components | to be stated by owner |
| Inventory | One ledger line per package; matches `MaterialStock` grain | Several lines per package | One product line + issue lines for components from stock | — |
| Genealogy | Simple | Per-item genealogy needed | Product genealogy + component lots | — |
| Barcode | One identity | One identity, many contents | One identity | — |
| Quality | One specification governs | Which specification releases the whole unit? | Product spec governs; components may need their own acceptance | — |
| Mass balance | Well defined | Ill-defined per item | Product balance stays clean; components balanced separately | — |
| Reporting | Clean | Mixed units | Clean + cost of consumables | — |
| MaterialItem identity | Unchanged | Requires a "kit" concept or multi-line content | Unchanged; components are ordinary stock items | — |
| Risk | An assortment box needs its own item | Breaks the stock grain | Adds an inventory-issue step per package | — |

**Important sub-question (new, surfaced here):** are **packaging materials** (bags, drums, labels)
consumed from stock and recorded per package? That is not "mixed product"; it is consumption of
materials, with inventory and cost effect (also feeds D12). Answer it inside this decision.

**TECHNICAL RECOMMENDATION — NOT BUSINESS APPROVAL:** A for the content. Whether to record packaging
materials (C) is the business's call.

**BUSINESS DECISION**
Selected Option: __________________
Packaging materials recorded per package? Yes / No: __________________
Approver: __________________
Date: __________________
Evidence: __________________
Conditions: __________________

---

## 6. D3 — Multi-Batch Package

**Question.** May one package contain material from more than one production batch?

| | **A. Exactly one batch** | **B. Several batch contributions** | **C. One primary batch + controlled secondary genealogy** |
|---|---|---|---|
| Genealogy | One link | One link per contribution, with quantity | Primary link + lighter secondary links |
| Recall / traceability | Simple, exact | Exact for every contributor | Ambiguous: is a secondary contributor in scope of a recall? |
| Quality inheritance | Follows the batch | Package releasable only if **all** contributors are Released (a stricter rule that itself needs approval) | Unclear which batch's status governs |
| Inventory | One deduction | One deduction per contributor | Deduction rule for secondary undefined |
| Mass balance | Simple | Sum of contributions | Undefined for secondary |
| Barcode | One lot reference | Lot reference must indicate several | One primary lot, secondary hidden |
| Reporting | Simple | Contribution reports | Two report styles |
| Audit | Simple | Contribution rows auditable | Split responsibility |
| Split/merge implications | Simplest | Merge of packages multiplies contributors | Hardest to reason about |

**TECHNICAL RECOMMENDATION — NOT BUSINESS APPROVAL:** B if blending ever happens (the existing input
pattern is one row per source batch, so cost is low); otherwise A enforced as a rule. Advise against
C: its quality and recall semantics are undefined.

**BUSINESS DECISION**
Selected Option: __________________
Approver: __________________
Date: __________________
Evidence: __________________
Conditions: __________________

---

## 7. D4 — Gross / Tare / Net

**Question.** Which weights are recorded and which are derived?

Already fixed by PEMS convention (technical, not for approval): quantity scale is `decimal(18,3)`;
corrections are new rows, never overwrites. **Numeric precision beyond that, tolerances, and
rounding are not invented here — they need approval.**

| | **A. Gross + tare measured, net derived** | **B. Gross + net measured, tare derived** | **C. All three stored + validation** | **D. Weighing events; latest is authoritative** |
|---|---|---|---|---|
| Precision / UOM | Per scale; one UOM per record | Same | Same | Same |
| Rounding | Net inherits gross/tare rounding | Tare inherits | Three values must agree within a rule (needs approval) | Per event |
| Re-weigh | New event/row | New event/row | New rows + re-validation | Native |
| Correction | Fix gross or tare; net follows | Fix gross or net; tare follows | Must fix all consistently | Add a corrected event |
| Audit | Net cannot drift from sources | Tare cannot drift | Drift possible without the rule | Full history |
| Operator entry | Weigh full, weigh empty (or default) | Weigh full and product | Three entries | Per weighing |
| Laboratory verification | Verifies net indirectly | Verifies net directly | Can verify each | Verifies the authoritative event |
| Inventory quantity | Derived net | Measured net | Stored net | Latest net |
| Reporting | One derivation rule | One derivation rule | Must trust validation | Must select "current" |

Also to decide: where tare comes from (container default / measured each time / default with
override), whether the weighing device and operator are captured, and rounding.

**TECHNICAL RECOMMENDATION — NOT BUSINESS APPROVAL:** A, stored as append-only weighing events (D's
storage habit, which the existing convention already requires). If a printed net weight must never
change after certification, add a frozen net — that is a business/compliance question.

**BUSINESS DECISION**
Selected Option: __________________
Tare source: __________________
Approver: __________________
Date: __________________
Evidence: __________________
Conditions: __________________

---

## 8. D5 — Split / Merge / Repack (acknowledgement)

**Statement to acknowledge:** *"Split / Merge / Repack is OUT OF INITIAL PHASE 3 SCOPE."*

- **Why excluded:** none of the three behaviors is defined by the business, and building an undefined
  behavior would mean guessing.
- **What it would need later:** who may do it; whether the new package needs quality re-confirmation
  or inherits status; what the stock ledger calls the movement (the existing types Receipt / Issue /
  Return / Transfer / Adjustment do not fit a same-material transformation); what happens to the old
  package's identity.
- **Quality:** handling itself can introduce damage or contamination, so inheriting "Released"
  automatically is a business risk decision.
- **Inventory:** net-zero transformation needs its own transaction meaning.
- **Genealogy:** needs a many-to-many parent/child record and a "superseded" state.
- **Barcode:** children get new identities; the existing "replace" means *same thing, new label*
  and does not fit.

This is an **acknowledgement, not approval to build**.

| Acknowledgement | Name | Date | Signature/Reference |
|---|---|---|---|
| Packaging Owner | | | |
| Laboratory/Quality Owner | | | |
| Operations Owner | | | |

---

## 9. D6 — Bulk vs Packaged Product Identity

**Question.** Is the packaged product the same `MaterialItem` as the bulk product?

| | **A. Same item; package is a state** | **B. Separate items** | **C. Parent/child identity** | **D. Separate items + mapping table** |
|---|---|---|---|---|
| Inventory | Bulk and packaged stock of the same material/location are **not distinguishable** in `MaterialStock` (no packaging dimension); only the ledger reference tells them apart | Separate stock rows naturally | Separate rows + relation | Separate rows + relation |
| MaterialItem | Unchanged | Business creates packaged items in the existing master | **Needs a new relation** — `MaterialItem` has no parent field | Existing master unchanged; new small mapping table |
| BOM | No BOM entity exists | same | same | Mapping table is a minimal "bulk → packaged" definition, not a BOM |
| Production | Unchanged | Bulk stays the production item | same | same |
| Packaging | Output = same item | Output = packaged item | Output = child | Output = mapped item |
| Barcode | Package identity only | Package + item identities | same | same |
| Quality | Package status | Package status; item spec may differ | same | same |
| Reporting | Needs ledger queries to separate | Direct | Direct + roll-up | Direct + roll-up |
| Costing | One cost per item; packaging cost has no home | Packaged item can carry its own cost | Roll-up cost | Mapped |
| Procurement | Unchanged | Packaged SKUs are purchasable/sellable separately | same | same |
| Maintenance/material usage | Unchanged | Unchanged (usually consume bulk) | same | same |
| Genealogy | Batch → package | Batch (bulk item) → package (packaged item) | same | same |
| Risk | Cannot report packaged stock without ledger tricks | More items to maintain | Change to a widely used table | New concept to maintain |

The table shape of the packaging records does not depend on this answer if the package output stores
its own item; the answer changes posting, master data, costing and reports. No second product master
is created under any option.

**TECHNICAL RECOMMENDATION — NOT BUSINESS APPROVAL:** keep the design schema-neutral (unchanged from
earlier phases). If a technical preference is requested: B requires no change to `MaterialItem` or
`MaterialStock`; A cannot separate packaged from bulk stock; C changes a table used by procurement,
maintenance and production.

**BUSINESS DECISION**
Selected Option: __________________
Approver: __________________
Date: __________________
Evidence: __________________
Conditions: __________________

---

## 10. D7 — Quality Release Authority

**Facts (live):** `QAManager` (1 user) holds Production View + Approve and IRN Approve, so it
releases/rejects batches and approves incoming receipts. `EngineeringManager` (2 users) also holds
Approve and can block/unblock, but also holds Create (a SoD pair prevents create+release on the same
record). **`QAManager` has no Hold/ReleaseHold**, so it cannot block or unblock a batch; only
`EngineeringManager` and `MaintenanceEngineer` can.

| | **A. Laboratory/QA owns release** | **B. `QAManager` stands in for Laboratory** | **C. Separate Packaging Quality role** | **D. Other** |
|---|---|---|---|---|
| Meaning | Authority is the laboratory function, role to be named | Existing role = laboratory authority | New role dedicated to packaging release | to be stated |
| Change needed | Depends on role | None for release; Hold decision separate | New role + grants + SoD | — |
| Independence from packaging | Strong | Strong (no Create right) | Strong | — |
| Risk | Role undefined today | One user only; cannot block | More roles to maintain | — |

**Existing finding for the owner to decide (nothing is changed):** the governance principle says the
laboratory decides Released *or* Blocked *or* Rejected, yet the role that releases and rejects cannot
block. Options: leave as is (blocking stays with engineering/maintenance roles), or grant
Hold/ReleaseHold to the release authority. **This pack does not grant them.**

**TECHNICAL RECOMMENDATION — NOT BUSINESS APPROVAL:** B, provided the business confirms `QAManager`
is the laboratory authority; decide the Hold/ReleaseHold question explicitly.

**BUSINESS DECISION**
Selected Option: __________________
Hold/ReleaseHold for the release authority? Yes / No: __________________
Approver: __________________
Date: __________________
Evidence: __________________
Conditions: __________________

---

## 11. E-4 — Packaging Permission Module

| | **A. Reuse the existing Production module** | **B. Dedicated Packaging module** | **C. Hybrid** |
|---|---|---|---|
| Meaning | Packaging actions use Production grants | Own module, own grants | Release/quality stays on Production; packaging execution has its own module |
| Least privilege | Anyone holding Production.Create (today EngineeringManager, MaintenanceEngineer) can create packaging records; operators cannot be separated from production | Best | Good |
| SoD | Existing Create↔Approve pairs apply automatically | New pairs needed | Existing pair for release; new pair for execution |
| One quality authority | Yes | Risks a second one | Yes |
| New surface | None | Module entry, role seed, SoD rows | Module entry, role seed, SoD row |
| Risk | Over-broad grants | Duplicated quality logic | Two places to review |

**TECHNICAL RECOMMENDATION — NOT BUSINESS APPROVAL:** C — one quality authority (Production.Approve),
packaging execution separable for operators. Depends on E-5.

**BUSINESS DECISION**
Selected Option: __________________
Approver: __________________
Date: __________________
Evidence: __________________
Conditions: __________________

---

## 12. E-5 — Role Mapping (14 organizational roles → 20 live roles)

Nothing is created or changed. "New role" is a least-privilege *assessment*.

| # | Organizational role | Proposed PEMS role | Required permissions | Existing | Missing | SoD concern | New role? | Confidence | Approval |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Complex Manager | Viewer | oversight view | Production View | none (approval authority is organizational) | none | No | Medium | Required |
| 2 | Maintenance Manager | MNT.Manager (+ MaintenanceEngineer) | work-order flow; equipment holds | WorkOrder Approve; Production C/E/Hold/RH (MaintenanceEngineer) | none for packaging maintenance | WorkOrder create↔close | No | High | Required |
| 3 | Finance Manager | Finance | View Production/inventory; cost approval later | Contracts/Tender edit | Production and inventory View | must not hold packaging Create/Approve | No (grant) | Medium | Required |
| 4 | Operations Manager | none (OPS.IssueAuthority is a work-order view role) | Production Create/Edit/Hold | only on engineering/maintenance roles | line-management role | Create↔Approve | **Yes** | Medium | Required |
| 5 | Commissioning Manager | none | depends on E-2 | none | undefined | undefined | **Not determined** | Low | Required |
| 6 | Operations Shift Supervisor | none | Create/Edit/Hold on runs | on MaintenanceEngineer only | dedicated role | Create↔Approve | **Yes** | Medium | Required |
| 7 | Operator | none | record input/output/loss only | MaintenanceEngineer would over-grant Barcode Approve and master-data edit | least-privilege role | no Approve; no stock adjustment | **Yes** | High | Required |
| 8 | Engineering Manager | EngineeringManager | technical approval | full Production | none | pair blocks same-record create+approve | No | High | Required |
| 9 | Process Engineering Lead | EngineeringSupervisor | investigate; view/edit | master-data C/E | Production View/Edit | none | No (grant) | Medium | Required |
| 10 | Packaging Manager | none | packaging ownership | none | no role | must not hold release | **Yes** | Medium | Required |
| 11 | Laboratory Manager | QAManager | release/reject (+block if decided) | Production View+Approve; IRN Approve | Hold/ReleaseHold (business decides) | separated from Create | No | Medium (depends on D7) | Required |
| 12 | HSE Manager | HSE | view; stop-work | none on Production | View; no stop-work permission concept | n/a | No (View) | Medium | Required |
| 13 | Security Manager | none (SystemAdmin does bypass) | audit view; investigation | not verified | undefined | must not hold Approve | **Not determined** | Low | Required |
| 14 | Warehouse Manager | none | receive/store/move stock | master-data C/E on engineering roles; QAManager IRN approve | no warehouse role | must not hold quality release | **Yes** | Medium | Required |

**Tally:** new role likely — 5 (rows 4, 6, 7, 10, 14); not required — 7 (1, 2, 3, 8, 9, 11, 12);
not determined — 2 (5, 13).

**TECHNICAL RECOMMENDATION — NOT BUSINESS APPROVAL:** approve the mapping for the seven "not
required" rows, decide the five "new role" rows, and resolve rows 5 and 13 after E-2 and an audit-view
review.

**BUSINESS DECISION**
Selected mapping / roles to create: __________________
Approver: __________________
Date: __________________
Evidence: __________________
Conditions: __________________

---

## 13. Decisions That Do Not Block Phase 3A (defaults exist)

| ID | Decision | Default if nobody objects (TECHNICAL, not approval) |
|---|---|---|
| D8 | Packaging order tier | none; can be added later |
| D9 | Rework | not supported |
| D10 | Offline/mobile | online only |
| D11 | Loss/variance tolerance | show raw difference, no number invented |
| E-2 | Commissioning marker | none until decided |
| E-3 | HSE stop-work/legal | `SITE / LEGAL CONFIRMATION REQUIRED` |
| **D12 / E-1** | **Financial treatment** | **No default — required before packaging stock is posted (Phase 3C), not before Phase 3A** |

## 14. What Management Must Decide, and When Phase 3A Can Start

**Decide now:** D1, D2, D3, D4, D6, D7, E-4, E-5; **acknowledge:** D5.
**Phase 3A may start only when** all of those carry an approved, dated, evidenced entry in the
sign-off baseline **and** the architecture freeze is activated (activation checklist). D12 is needed
before 3C.
