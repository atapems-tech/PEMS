# PEMS Operations Phase 2D — Packaging Business Decision Register

**Purpose of this document**: to get answers from business owners, not engineers. It avoids
software terms wherever possible. Each item lists the business question, why it matters,
the realistic options, what each option means in day-to-day operation, which direction the
technical team leans toward, and what is needed from you. Nothing described here has been
built yet — this is a decision-gathering document before any Packaging feature is coded.

---

## How to read this document

Each decision has a status:
- **DECISION REQUIRED** — operations/quality/commercial leadership must choose before building starts.
- **RECOMMENDED, PLEASE CONFIRM** — the technical team has a suggested answer; please confirm or override it.
- **DEFERRED** — not needed for the first version of Packaging; can be decided later without cost.

---

## D1. What exactly is a "Package"? — DECISION REQUIRED

**The question**: When we say "Package" in the system, do we mean the physical box/bag/drum
itself, or the specific batch of product placed inside it, or both together as one thing?

**Why it matters**: Every other decision in this document depends on this answer. If we track
only the container, we lose the ability to trace which production batch is inside it. If we
track only the product quantity, we lose the ability to manage physical containers (barcodes,
damage, reuse).

**Recommended answer**: Both together, as a single record — "this specific box contains this
specific quantity of this specific product, made from this specific production batch." This
mirrors how a Production Batch already works today: it is both "this much material" and "this
specific production event."

**What we need from you**: Confirm this is correct, or tell us if containers and product
quantities need to be tracked completely separately in your operation.

---

## D2. Can one package ever contain more than one product? — DECISION REQUIRED

**The question**: Does packaging ever combine two different products into a single container
(e.g., an assortment or mixed box), or is every package always a single product?

**Why it matters**: If mixed-product packages are a real scenario, the system needs a
different design than if every package is always single-product. Getting this wrong now means
redesigning later.

**Options**:
| Option | What it means operationally |
|---|---|
| Always single product (recommended) | Simple, traceable, matches standard practice for bulk/industrial packaging |
| Mixed-product packages allowed | Would need to be modeled as its own new product ("kit"), not as an exception |

**What we need from you**: Confirm every package is always one product, or tell us if mixed
packages are a real, current operational need.

---

## D3. Can a package be made from more than one production batch (blending)? — DECISION REQUIRED

**The question**: Does packaging ever blend material from two or more different production
runs into the same package (e.g., topping up a partial batch with material from a second
batch)?

**Why it matters**: If this happens, the system must record every source batch, not just one,
so that if a quality issue is later found in one batch, every package that contains it can be
traced and recalled.

**Recommended answer**: Design for it now (low cost to support), even if rare, because the
traceability/recall risk of not supporting it is high.

**What we need from you**: Confirm whether this happens in your operation today, even
occasionally.

---

## D4. How do we weigh a package — and what number goes on the label? — DECISION REQUIRED (highest priority)

**The question**: When a package is weighed, we typically capture three numbers: the full
weight including the container (**Gross**), the weight of the empty container alone
(**Tare**), and the actual product weight (**Net** = Gross − Tare). The question is: do we
store the Gross and Tare and always calculate Net fresh, or do we also permanently record the
Net number at the moment of packaging, separate from the Gross/Tare it came from?

**Why it matters**: This is the single most consequential decision in this whole document —
it decides the shape of the packaging records before anything is built. It also affects
compliance: if a Net weight is ever printed on a customer-facing label or certificate, do we
need that number to stay fixed forever, even if a data-entry mistake in the original Gross or
Tare is discovered and corrected later?

**Options**:
| Option | Operational consequence |
|---|---|
| **A. Store Gross + Tare, always calculate Net on the fly** | Net weight can never disagree with its source numbers. If you correct a Tare mistake, Net updates automatically everywhere. Best if Net is only used internally for reporting/inventory. |
| **B. Store Gross + Tare + a frozen Net number** | Net stays exactly what was printed on the label/certificate, even if Gross or Tare is corrected later for a different reason. Best if Net appears on a customer document that must never silently change. |

**Recommended answer**: Option A, unless there is a regulatory/customer-certificate reason
Net must be a permanently frozen number — in which case Option B.

**Also decide**: Where does the "Tare" (empty container weight) come from — a standard weight
looked up from the container type, an actual measurement taken every time, or a standard
weight that can be overridden if someone actually measures it? (Recommended: the last option —
a standard default that can be overridden.)

**What we need from you**: Choose A or B, and confirm the Tare source rule.

---

## D5. What happens when we split one package into two, or combine two into one? — DECISION REQUIRED

**The question**: Sometimes a large package is divided into smaller ones, or several small
ones are combined into one. When this happens: (a) does the new package need to go through
quality release again, or does it automatically inherit its "OK to ship" status from the
original? (b) what do we call this kind of internal movement in the inventory system, since it
isn't quite a normal stock receipt or issue?

**Why it matters**: If a released package is split and the pieces are treated as
automatically released without any check, a real damage or contamination risk during the
physical split could go unnoticed. If every split requires full re-inspection, that may be
operationally too slow for routine repacking.

**Recommended answer**: Require re-confirmation (not necessarily a full re-inspection) before
a split/combined package is marked ready-to-ship, since the physical handling itself is a risk
point. Fast-track this confirmation if your quality process allows it.

**What we need from you**: Confirm the re-release rule, and tell us the inventory-record wording
your team would recognize for this kind of internal transfer (e.g., "repack," "requalify," "internal
transformation").

---

## D6. Does packaging ever create a different product than what went in? — DECISION REQUIRED

**The question**: When bulk material is packaged, is the packaged item still considered "the
same product," or does packaging sometimes create what is, commercially, a different product
(e.g., "Bulk Chemical X" packaged into 25kg bags becomes, in your catalog, "Chemical X 25kg
Bag" — a distinct item with its own code)?

**Why it matters**: This determines whether packaging inventory should track under the same
product code as the bulk material, or a new one — this affects how stock reports and sales
orders will look.

**What we need from you**: For your main product lines, does the packaged form get its own
product code today (e.g., in your ERP or price list)? If yes, packaging should be modeled as
producing that distinct code.

---

## D7. Who is allowed to release a package as ready-to-ship? — RECOMMENDED, PLEASE CONFIRM

**The question**: Should the same people who are authorized to release a bulk production batch
also be authorized to release a finished package, or does package release need a separate,
distinct authorization (e.g., a packaging-line quality inspector distinct from a production
quality inspector)?

**Recommended answer**: Reuse the same authorization used for production batch release, unless
your quality organization has a genuinely separate packaging QC role today.

**What we need from you**: Confirm reuse is fine, or name the distinct role/approval chain if
one already exists in your operation.

---

## D8. Do we need a formal "Packaging Order" (a plan of what should be packaged), or is it enough to record what was actually packaged? — RECOMMENDED, PLEASE CONFIRM

**The question**: Production has both a "plan" (Production Order — what should happen) and a
"record of what happened" (Production Run). Does Packaging need the same two-tier structure,
or is packaging typically decided and executed on the spot without a separate formal plan?

**Recommended answer**: Start with only "what was actually packaged" (no formal plan tier),
since packaging is often shorter-cycle and more reactive than bulk production planning. Add a
formal plan tier later if scheduling/forecasting needs emerge.

**What we need from you**: Confirm this is acceptable for a first version, or tell us if
packaging is planned days in advance in your operation and needs its own schedule/plan record
from day one.

---

## D9. Do we need to group packages onto pallets or other handling units, and if so, do we need to support pallets-within-pallets? — DEFERRED unless needed immediately

**The question**: Should the system track which packages are grouped onto which pallet/cage/
container for movement and shipment? And do handling units ever nest (e.g., cartons stacked on
a pallet, both tracked)?

**Recommended answer**: Build simple pallet/handling-unit tracking (one level, no nesting)
only if warehouse/shipment operations need it in the first release; otherwise defer.

**What we need from you**: Tell us if pallet-level tracking is needed for the first release, and
whether nesting (units within units) is a real requirement or a "nice to have."

---

## D10. Repacking — does an already-packaged, already-labeled product ever get opened and repackaged? — DEFERRED unless needed immediately

**The question**: After a package has been created and labeled, does it ever get opened again
and its contents moved into a different package (as opposed to a same-time split described in
D5)?

**Recommended answer**: Treat as a future capability, not needed for the first release, unless
you can confirm this happens routinely today.

**What we need from you**: Confirm whether this is common enough to require day-one support.

---

## D11. How much natural loss/variance is acceptable during packaging, and should the system flag anything outside that range? — DECISION REQUIRED (can be answered later, but flag now)

**The question**: When bulk material is packaged, the packaged weight plus recorded losses
should roughly equal what went in — but real-world weighing, spillage, and moisture can cause
small differences. Does your operation have (or want) a defined "acceptable" range for this
difference, outside of which it should be flagged for review?

**Recommended answer**: Until a number is defined, the system will show the raw difference
honestly, never silently force the numbers to balance to zero. This is a currently-open gap,
not a defect — no assumption should be made about what's "acceptable."

**What we need from you**: If you already have a tolerance figure used elsewhere (e.g. a
moisture-loss allowance), share it now; otherwise this can be answered later without blocking
early design.

---

## D12. Rounding — DEFERRED, low priority

**The question**: When quantities are rounded for display or calculation, what rounding rule
should be used (nearest whole unit, nearest gram, always round down, etc.)?

**Recommended answer**: Not needed until real packaging quantities and units are finalized;
can be answered alongside D4.

---

## Summary table

| ID | Topic | Status | Blocks Phase 3 start? |
|---|---|---|---|
| D1 | What a Package is | DECISION REQUIRED | Yes |
| D2 | Mixed-product packages | DECISION REQUIRED | Yes |
| D3 | Blended-batch packages | DECISION REQUIRED | Yes |
| D4 | Gross/Tare/Net model | DECISION REQUIRED | Yes |
| D5 | Split/merge quality & inventory rules | DECISION REQUIRED | Yes |
| D6 | Packaged product identity | DECISION REQUIRED | Yes |
| D7 | Package release authority | RECOMMENDED, confirm | No (default usable) |
| D8 | Packaging Order tier | RECOMMENDED, confirm | No (default usable) |
| D9 | Pallets / handling units | DEFERRED | No |
| D10 | Repacking | DEFERRED | No |
| D11 | Loss/variance tolerance | DECISION REQUIRED (can follow later) | Partially — reporting only |
| D12 | Rounding | DEFERRED | No |

**Six items (D1-D6) are true blockers** — Phase 3 coding should not begin until these are
answered, because they each change the basic shape of the records being built. D7/D8 have safe
defaults and can proceed with the recommended answer if no one objects. D9/D10/D12 can be
decided anytime without cost. D11 does not block starting the build but should be resolved
before packaging reports are relied upon for real variance decisions.
