# PEMS Operations Phase 2E — Packaging Executive Decision Pack

**Purpose:** six decisions, for the people who need to make them. Nothing here has been built.
No software terms are used except where the underlying business concept genuinely requires one.
This document does not make any decision on management's behalf — it lays out the question,
why it matters, the real options, and what each one means for day-to-day operation, then asks
for a choice.

**Who should read this:** Complex/Site Manager, Packaging Manager, Laboratory Manager, Finance
Manager, Engineering Manager, Process Engineering Lead — the named owners and approvers below.

**What happens after this is decided:** these six answers become the fixed foundation the
Packaging module is built on in the next phase. Changing any of them after building starts
means redoing the work, not adjusting it — that is why they are being asked now, before a
single line of code is written.

---

## Decision 1 — What exactly is a "Package"?

**The question:** When someone in PEMS says "Package," do we mean the physical box or bag
itself, the specific quantity of product inside it, or both together as one thing?

**Why it matters:** Every other decision in this pack depends on this one. If we track only the
container, we cannot trace which production run is inside it. If we track only the quantity, we
lose the ability to manage physical containers — barcodes, damage, reuse.

**Options:**
| Option | What it means day-to-day |
|---|---|
| **A — Both together (recommended)** | "This specific box contains this specific quantity of this specific product, from this specific production run." One record, one identity. |
| B — Container and quantity tracked completely separately | More flexible in theory, but doubles the bookkeeping for every package and makes it harder to answer "what's actually in box #4471" with confidence. |

**Consequences of Option A:** matches how a Production run is already tracked today in PEMS
(both "this much material" and "this specific event") — nothing new to learn operationally.

**Consequences of Option B:** would require staff to look in two places to answer one question,
and increases the chance the two records drift apart over time.

**Technical recommendation (not a business decision):** Option A.

**Business decision required:** Confirm Option A, or state that containers and quantities must
be tracked completely separately in this operation.

**Decision owner:** Packaging Manager
**Approval required from:** Complex Manager, Laboratory Manager

---

## Decision 2 — Can one package ever hold more than one product?

**The question:** Does packaging ever combine two different products into one container — an
assortment or mixed box — or is every package always a single product?

**Why it matters:** Getting this wrong means redesigning the system later. A mixed-product
package needs to be handled completely differently from a single-product one — for quality
release, for stock counting, for pricing.

**Options:**
| Option | What it means day-to-day |
|---|---|
| **A — Always single product (recommended)** | Simple, fully traceable, standard practice for bulk/industrial packaging. |
| B — Mixed-product packages allowed | Would need to be treated as its own new catalog item (a "kit"), not as an exception bolted onto single-product packaging. |

**Business decision required:** Confirm every package is always one product, or tell us if
mixed packages are a real, current need.

**Decision owner:** Packaging Manager
**Approval required from:** Engineering Manager, Complex Manager

---

## Decision 3 — Can a package be made by blending more than one production batch?

**The question:** Does packaging ever combine material from two or more different production
runs into the same package — for example, topping up a partially-filled batch with material
from a second one?

**Why it matters:** If this happens, every package must record *every* source batch it came
from, not just one. If a quality problem is later found in one batch, we need to be able to find
every package that contains any of it — this is the core of a product recall capability.

**Options:**
| Option | What it means day-to-day |
|---|---|
| **A — Design for it now (recommended)** | Low cost to support even if it's rare, because the traceability risk of *not* supporting it is high. |
| B — Assume it never happens | Simpler design, but if blending does happen and isn't recorded, a future recall could miss affected product. |

**Business decision required:** Confirm whether this happens in this operation today, even
occasionally.

**Decision owner:** Process Engineering Lead
**Approval required from:** Packaging Manager, Laboratory Manager

---

## Decision 4 — How do we weigh a package, and what number goes on the label? (Highest priority)

**The question:** When a package is weighed, there are normally three numbers: the full weight
including the container (**Gross**), the empty container's own weight (**Tare**), and the
actual product weight (**Net** = Gross − Tare). Do we store Gross and Tare and always calculate
Net fresh when needed, or do we also permanently lock in the Net number at the moment of
packaging, separate from the Gross/Tare it came from?

**Why it matters — this is the single most consequential decision in the whole document.** It
decides the shape of every packaging record before anything is built. It also has a compliance
angle: if a Net weight is ever printed on a customer-facing label or certificate, does that
number need to stay fixed forever — even if someone later discovers the original Gross or Tare
reading was mis-keyed?

**Options:**
| Option | Day-to-day consequence |
|---|---|
| **A — Store Gross + Tare, always calculate Net (recommended unless B applies)** | Net can never disagree with the numbers it came from. Fix a Tare mistake, and Net updates automatically everywhere. Best if Net is only used internally. |
| B — Store Gross + Tare + a frozen Net number | Net stays exactly what was printed on a label or certificate, even if Gross or Tare is corrected later for an unrelated reason. Best if Net appears on a document that must never silently change. |

**Also needs an answer:** where does "Tare" — the empty container's weight — come from? A
standard weight looked up from the container type, an actual measurement taken every time, or a
standard weight that can be overridden if someone actually measures it? (Recommended: the last
option.)

**Business decision required:** Choose A or B, and confirm the Tare source rule.

**Decision owner:** Finance Manager
**Approval required from:** Laboratory Manager (if any certificate is affected), Complex Manager

---

## Decision 5 — What happens when one package is split into two, or two are combined into one?

**The question:** When a large package is divided into smaller ones, or several small ones are
combined into one: (a) does the new package need to go through quality release again, or does
it automatically inherit "OK to ship" status from the original? (b) what should this kind of
internal movement be called in the stock system, since it isn't quite a normal receipt or issue?

**Why it matters:** If a released package is split and the pieces are automatically treated as
still released with no check, a real damage or contamination risk introduced during the
physical split could go unnoticed. If every split requires a full re-inspection, that may be
too slow for routine repacking to be practical.

**Options:**
| Option | Day-to-day consequence |
|---|---|
| **A — Require re-confirmation before shipping (recommended)** | Not necessarily a full re-inspection, but *some* deliberate check — because the physical handling itself is a risk point. Can be fast-tracked if the quality process allows it. |
| B — Automatically inherit the original release status | Faster, but a real risk if the physical split/merge itself can introduce contamination or damage. |

**Business decision required:** Confirm the re-release rule, and the wording your team already
uses for this kind of internal transfer (e.g., "repack," "requalify," "internal
transformation") — that wording becomes the label used everywhere in the system.

**Decision owner:** Laboratory Manager
**Approval required from:** Packaging Manager, Finance Manager

---

## Decision 6 — Does packaging ever turn bulk material into a different, distinct product?

**The question:** When bulk material is packaged, is the packaged item still considered "the
same product" as the bulk — or does packaging sometimes create what is, commercially, a
different product? For example: does "Bulk Chemical X" packaged into 25kg bags become "Chemical
X 25kg Bag" — a distinct item with its own product code in your price list or ERP?

**Why it matters:** This decides whether packaged stock should be tracked under the same
product code as the bulk material or a new one — which directly changes how stock reports and
sales orders will look.

**Business decision required:** For your main product lines, does the packaged form already get
its own product code today (in your ERP or price list)? If yes, packaging should be modeled as
producing that distinct code.

**Decision owner:** Finance Manager
**Approval required from:** Packaging Manager, Complex Manager

---

## What happens next

Once all six decisions above are confirmed, PEMS's engineering team will use them as the fixed
input to build the Packaging module (Phase 3). Two further items were also flagged by this
review but are **not** hard blockers — they have safe, ready-to-use default answers unless
someone objects:

- **Who is allowed to release a package as ready-to-ship?** Default: the same people already
  authorized to release a bulk production batch, unless a genuinely separate packaging quality
  role already exists in your operation. *(Confirm with Laboratory Manager if this needs to
  change.)*
- **Do we need a formal "Packaging Order" (a plan of what should be packaged), or is it enough
  to record what was actually packaged?** Default: record only what actually happened for the
  first version; add a formal plan later if scheduling needs emerge. *(Confirm with Operations
  Manager if packaging is planned days in advance and needs its own schedule from day one.)*

A small number of additional financial and safety questions (which packaging events should
affect cost accounting; who is authorized to stop work on safety grounds; site-specific legal
requirements) were also identified during this review and are tracked separately in the full
governance baseline document — they do not block the start of Phase 3 building but should be
answered before those specific features go live.

## Sign-off

| Decision | Decision owner | Signature / date |
|---|---|---|
| D1 — What is a Package | Packaging Manager | |
| D2 — Mixed-product packages | Packaging Manager | |
| D3 — Blended-batch packages | Process Engineering Lead | |
| D4 — Gross/Tare/Net model | Finance Manager | |
| D5 — Split/merge rules | Laboratory Manager | |
| D6 — Packaged product identity | Finance Manager | |
| **Final approval to proceed to Phase 3** | **Complex Manager** | |
