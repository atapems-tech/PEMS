# 05 Standards verification register (PPC Stage 0, final for this pass)

**Verification date:** 2026-11-14.
**Principle (owner):** publication of a standard is not proof that PEMS complies with it. Every state below describes what was checked, not PEMS conformity or certification.

**Verification levels:**
- **OFFICIAL:** read from the publisher's own page (iso.org) in this pass.
- **SECONDARY:** quoted by a third party or search summary; not read from the publisher.
- **NOT VERIFIED:** the publisher page could not be read; no status claimed.

**Assessment states:** ALIGNED (design consistent with a concept; not a certification), PARTIALLY CONFORMANT, NONCONFORMANT, NOT ASSESSED, NOT APPLICABLE (with reason), NOT VERIFIED.

## 1. Register

| # | Reference | Identifier and edition | Official source | Verification level | Lifecycle status (2026-11-14) | Applicability to PEMS PPC | PEMS evidence | Gap | State |
|---|---|---|---|---|---|---|---|---|---|
| S1 | ISA-95 / IEC 62264 | ANSI/ISA-95.00.01-2025 (IEC 62264-1 Mod) | isa.org product page (identifier confirmed) | Identifier OFFICIAL; status SECONDARY | Published (ISA page identifier; status from listing) | Object model: enterprise, site, area, production order | ProductionOrder, ProductionRun, ProductionBatch | No written object mapping; parts 2–5 not checked | ALIGNED (informal object mapping only; no conformance claim) |
| S2 | IEC 62264 parts 2–5 | — | — | NOT VERIFIED | NOT VERIFIED | Activity and information models | — | Full mapping | NOT ASSESSED |
| S3 | ISA-88 / IEC 61512 | Claimed IEC 61512-1:2026 Ed. 2 (secondary); ISA-88.00.01-2010 is the ISA edition | IEC and ISA pages not readable in this pass | NOT VERIFIED | NOT VERIFIED | Only if recipe-driven batch control is in scope | ProductionBatch; no recipe or phase model | Edition unconfirmed | NOT ASSESSED |
| S4 | ISO 22400-1 | 2014 edition (revision work item reported, secondary) | iso.org/standard/56847.html not readable | NOT VERIFIED | Revision status NOT VERIFIED | KPI definitions for production dashboards | ProductionDashboardDto; ExecutiveKpiService | No KPI definition mapping | NOT ASSESSED |
| S5 | ISO 22400-2 | 2014 with Amendment 1:2017 (secondary) | iso.org/standard/54497.html not readable | NOT VERIFIED | NOT VERIFIED | As S4 | As S4 | As S4 | NOT ASSESSED |
| S6 | **ISO 9001** | **ISO 9001:2026, Edition 6, Quality management systems — Requirements** | **https://www.iso.org/standard/9001** (read this pass) | **OFFICIAL** | **Published (Edition 6, 2026). ISO text: "Published on 16 September" (2026).** Replaces ISO 9001:2015 (ISO record shows 2015 and its amendment as withdrawn; confirmed by secondary sources only) | Quality management system of the organisation; not a product requirement for PPC software | No QMS evidence in scope of this review | Software does not certify an organisation; relevant to process controls only | NOT ASSESSED (out of product scope). Not claimed as met. |
| S7 | **ISO 19011** | **ISO 19011:2026, Edition 4, Guidelines for auditing management systems** | **https://www.iso.org/standard/19011** (read this pass) | **OFFICIAL** | **Published (Edition 4, 2026-05; stage 60.60). ISO lifecycle shows ISO 19011:2018 as Withdrawn and ISO 19011:2026 as Published.** | Guidance for internal and remote audits used for the Stage 0 method | No audit-programme evidence in scope | Guidance only; no certification requirement | NOT ASSESSED (guidance, not a product standard) |
| S8 | ISO/IEC 19987 (EPCIS) | ISO/IEC 19987:2024 (Edition 3) per ISO listing (secondary); EPCIS 2.0.1 not verified | iso.org/standard/85557.html not readable; ref.gs1.org not readable | NOT VERIFIED (secondary only) | 19987:2024 listed (secondary); EPCIS 2.0.1 NOT VERIFIED | Event-based traceability; only if external sharing is needed | No EPCIS event model in repo | No event exchange designed | NOT ASSESSED |
| S9 | ISA/IEC 62443-2-1 | 2024 Ed. 2.0 (secondary) | isa.org not read at source | NOT VERIFIED | NOT VERIFIED | Asset-owner security programme; applies if OT is connected | AuthorizationAuditLog, RoleAssignment, controller permission checks | No OT security programme evidence | NOT ASSESSED |
| S10 | ISA/IEC 62443-3-3 | 2013 Ed. 1.0 (secondary) | — | NOT VERIFIED | NOT VERIFIED | Only when an industrial control system is connected | No OT connection (no OPC UA, Modbus, S7, MQTT package) | — | NOT APPLICABLE while no OT link exists |
| S11 | ISA/IEC 62443-2-4, TS 62443-1-5 | 2023 Ed. 2.0; TS 1-5:2023 (secondary) | — | NOT VERIFIED | NOT VERIFIED | Service-provider and profile guidance | None | — | NOT APPLICABLE (no IACS service-provider role) |
| S12 | ASCM CPIM | Professional certification and body of knowledge | ascm.org (not a standard) | Not a normative standard | Not applicable as a standard | Reference vocabulary for S&OP | — | — | NOT APPLICABLE as a standard |

## 2. Corrections made in this pass
- **S6 ISO 9001:** earlier entries (Audit-02 and the previous version of this file) said "FDIS closed 2026-07-09; publication date conflicting". Superseded by the ISO page: **Published, Edition 6, published 16 September 2026.**
- **S7 ISO 19011:** earlier entries said the 2018 edition was current or that its replacement was not identified. Superseded by the ISO page: **ISO 19011:2026 (Edition 4) is published; ISO 19011:2018 is withdrawn.**
- The governing document's reference dates (ISO 9001:2026 and ISO 19011:2026) are now **confirmed from the official ISO pages**.

## 3. What publication does and does not show
- ISO 9001:2026 being published does **not** show that PEMS meets it. PEMS would need an organisational QMS audit, which is outside the product scope.
- ISO 19011:2026 being published does **not** make PEMS audit-compliant. It is method guidance for auditors.
- No row in this register is a conformity claim. Where the state is ALIGNED or PARTIALLY CONFORMANT, that describes design consistency only.

## 4. Still not verified from official sources
S2, S3, S4, S5, S8, S9, S10, S11. For these the owner needs an official or licensed copy before any statement beyond NOT VERIFIED.

## 5. Final status counts (2026-11-14)
- OFFICIAL (read from publisher): S6 ISO 9001:2026, S7 ISO 19011:2026.
- SECONDARY identifier or status only: S1 ISA-95 / IEC 62264-1.
- NOT VERIFIED: S2, S3, S4, S5, S8, S9, S10, S11.
- ALIGNED (design consistency only, not a conformance claim): S1.
- NOT APPLICABLE or NOT ASSESSED: all others.
- No row claims PEMS conformance or certification. The owner requirement "no standards compliance without evidence" is met by this state.
