from pathlib import Path

content = """# PEMS — Permit Authentication, Authority & Competency Rules

**Document ID:** PEMS-SEC-PERMIT-AUTH-001  
**Status:** Mandatory Architecture Rule  
**Scope:** Permit to Work (PTW), SIMOPS, LOTO, Isolation and other safety-critical approval workflows  
**Priority:** Architecture / Security Gate  
**Version:** 1.0

---

## 1. Purpose

This document defines the mandatory authentication, authorization, competency, and approval rules for safety-critical Permit workflows in PEMS.

The objective is to ensure that:

1. The person approving a Permit is genuinely authenticated.
2. The person is actually authorized to approve that specific Permit.
3. The person's competency, certification, training, and operational scope are valid.
4. Permit Man cannot assign or accept an unauthorized approver.
5. Approval records are tamper-evident and auditable.
6. Biometric data is never stored or processed by PEMS.

---

# 2. Rule SEC-AUTH-001 — Strong Authentication for Permit Approval

All sensitive and formal Permit operations MUST use **Step-up Authentication** based on **WebAuthn / Passkeys**.

This applies at minimum to:

- Permit Approval
- Permit Rejection
- Permit Close
- SIMOPS Approval
- Isolation Approval
- LOTO Approval
- Other safety-critical Permit actions designated by the workflow authority model

### Supported authentication

The user's device may authenticate through:

- Fingerprint
- Face ID / Face Unlock
- Device PIN / Passcode
- Other platform-supported WebAuthn authenticator mechanisms

PEMS MUST NOT store or process raw biometric information.

### Mandatory principle

Biometric verification MUST be performed by the device operating system / authenticator.

PEMS receives and validates only the cryptographic WebAuthn / Passkey assertion.

---

# 3. Rule SEC-AUTH-002 — No Biometric Data Storage

PEMS MUST NOT store:

- Fingerprint images
- Fingerprint templates
- Face images for authentication
- Face templates
- Raw biometric measurements
- Any equivalent biometric secret

PEMS MAY store only the minimum information required for authentication and audit, such as:

- Credential ID
- Public key
- Authenticator metadata
- Authentication result
- Authentication timestamp
- Credential reference

---

# 4. Rule PERMIT-AUTH-002 — Permit Authority & Competency

A successful login or biometric authentication DOES NOT automatically grant Permit approval authority.

A user MUST NOT be allowed to Approve, Reject, Sign, Close, or otherwise formally authorize a Permit unless the user has valid authority for that specific action.

Eligibility MUST be evaluated by the PEMS Authorization & Competency Engine.

---

# 5. Approver Eligibility Model

Before an approver can be assigned or allowed to approve a Permit, PEMS MUST validate the following:

| Control | Mandatory |
|---|---|
| User Identity | YES |
| Active Account | YES |
| Assigned Role | YES |
| Permit Role Authority | YES |
| Required Competency | YES |
| Required Training | YES |
| Certification / License | YES |
| Certification Validity | YES |
| Permit Type Authorization | YES |
| Plant / Area / Unit Scope | YES |
| Shift / Duty Assignment where applicable | YES |
| Authority Level | YES |
| Effective Date | YES |
| Expiry Date | YES |

If any mandatory condition fails, approval MUST be blocked.

---

# 6. Permit Man Rule

**Permit Man is responsible for managing the Permit workflow but MUST NOT have unrestricted authority to select any user as an approver.**

When Permit Man attempts to add an approver:

```text
Permit Man
    |
    v
Select User
    |
    v
Authorization & Competency Engine
    |
    +-- Role valid?
    +-- Competency valid?
    +-- Training valid?
    +-- Certification valid?
    +-- Certification not expired?
    +-- Permit type authorized?
    +-- Area / Unit authorized?
    +-- Authority level sufficient?
    +-- Effective date valid?
    |
    +---- NO ----> BLOCK
    |
    +---- YES ---> Eligible Approver
                      |
                      v
                 Step-up Authentication
                      |
                      v
                    APPROVAL
The UI SHOULD preferably show only eligible approvers by default.

If a selected person is not eligible, the system MUST display the reason and prevent assignment.

7. No Unauthorized Permit Signing

The following MUST be prevented by the backend, regardless of UI restrictions:

Assigning an unauthorized approver
Approving through a direct API call
Approving by manipulating request parameters
Bypassing the UI
Changing the approver after approval without re-validation
Reusing an old authentication assertion
Approving after competency/certification expiry
Approving outside the user's authorized operational scope

Authorization MUST therefore be enforced server-side.

Frontend controls are NOT considered a security boundary.

8. Approval Chain Integrity

Every Permit MUST maintain an explicit Approval Chain.

Example:

Permit
 |
 +-- Permit Man
 |
 +-- Area / Operations Authority
 |
 +-- Maintenance Authority
 |
 +-- HSE Authority
 |
 +-- Isolation / LOTO Authority
 |
 +-- Other Required Competent Person

The exact approval chain MUST be configurable by Permit Type, Area, Risk Level, and workflow rules.

The system MUST NOT allow a user to approve a stage for which they do not have authority.

9. Authentication + Authorization Separation

PEMS MUST maintain a strict separation between:

Authentication

"Who is this person?"

Implemented using:

WebAuthn
Passkeys
Device biometrics
Device PIN / Passcode
Authorization

"Is this person authorized to approve this specific Permit?"

Determined using:

Role
Competency
Training
Certification
Authority
Permit Type
Area / Unit Scope
Shift / Duty
Effective / Expiry dates

Authentication alone MUST never be treated as authorization.

10. Approval Audit Record

Every formal Permit approval MUST create an immutable / tamper-evident audit record.

Minimum required fields:

UserId
PermitId
Action
ApprovalStage
Timestamp
AuthenticationMethod
CredentialReference
AuthorizationDecision
Role
CompetencyReference
CertificationReference
AreaScope
Device / Session Reference
AuditCorrelationId

The record MUST be linked to the exact Permit revision/version being approved.

11. Permit Revision Protection

If a material Permit field changes after an approval, PEMS MUST determine whether the previous approval remains valid.

For material changes, the system SHOULD invalidate affected approvals and require re-approval.

Examples include:

Work scope
Hazard classification
Isolation requirements
Energy source
Work location
Permit type
Risk level
Critical safety controls
Validity period
Responsible authority

Approval MUST be bound to a specific Permit revision/version.

12. Break-Glass / Emergency Override

Emergency override MAY exist only where explicitly approved by the PEMS governance model.

Break-glass MUST:

Require explicit elevated authorization.
Require a mandatory reason.
Record the identity of the overriding user.
Record timestamp.
Record affected Permit.
Record the original authorization failure.
Create a high-priority audit event.
Never silently bypass the authorization engine.
Be reviewable after the emergency operation.

A normal Admin role MUST NOT automatically constitute Break-Glass authority.

13. Shared Authorization & Competency Engine

Authorization logic MUST NOT be implemented independently inside each workflow.

PEMS SHOULD provide a shared:

Authorization & Competency Engine

                    PEMS
                     |
        +------------+------------+
        |                         |
 Identity & Authentication   Authorization &
        Service              Competency Engine
        |                         |
 WebAuthn / Passkey          Roles
 Biometrics                  Competencies
 Device Auth                 Training
                             Certifications
                             Authority
                             Operational Scope
                             Validity
        |                         |
        +------------+------------+
                     |
              Approval Engine
                     |
       +-------------+-------------+
       |             |             |
     Permit        LOTO         SIMOPS
       |             |             |
      WO        Isolation      Handover

This shared infrastructure MUST be reusable by other safety-critical workflows.

14. Mobile Approval

PEMS MUST support secure Permit approval from mobile devices.

The mobile experience SHOULD allow:

User receives/opens assigned Permit.
User reviews Permit and required controls.
User selects Approve / Reject.
PEMS performs Step-up Authentication.
PEMS validates Authorization & Competency.
PEMS creates cryptographic/tamper-evident approval record.
PEMS records the audit event.
PEMS updates the workflow state.

The workflow MUST remain secure whether the user accesses PEMS through:

Mobile Web
PWA
Native Mobile Application
15. Electronic Signature Rule

A stored image of a handwritten signature MUST NOT be treated as sufficient proof of Permit approval.

If a formal electronic signature is required, it MUST be linked to:

Verified user identity
Specific Permit
Specific Permit revision
Specific approval action
Timestamp
Authorization decision
Cryptographic evidence where applicable

A signature image MAY be used for visual presentation only and MUST NOT be the security mechanism.

16. Backend Enforcement

All authorization rules MUST be enforced server-side.

The system MUST assume that:

The client application, browser, mobile application, API request, and UI can be manipulated.

Therefore:

UI filtering is not sufficient.
Role checks in frontend code are not sufficient.
Hidden buttons are not security controls.
API endpoints MUST independently enforce authorization.
Domain/application services MUST validate Permit authority before state transitions.
Database records MUST preserve approval integrity.
17. Minimum Acceptance Criteria

Implementation is NOT complete until all of the following are verified:

 WebAuthn / Passkey support
 Fingerprint authentication support where device/platform supports it
 Face authentication support where device/platform supports it
 Device PIN / Passcode fallback
 No biometric data stored in PEMS
 Step-up authentication for sensitive Permit actions
 Authorization & Competency Engine implemented
 Permit Man cannot assign unauthorized approvers
 Backend blocks unauthorized approval
 Role validation
 Competency validation
 Training validation
 Certification validation
 Certification expiry validation
 Permit Type authorization validation
 Plant / Area / Unit scope validation
 Authority level validation
 Effective / expiry date validation
 Approval Chain enforcement
 Permit revision binding
 Replay protection
 Tamper-evident audit trail
 Approval linked to exact Permit revision
 Break-Glass mechanism, if enabled, is separately authorized and audited
 Mobile Web / PWA support
 Android validation
 iOS validation
 Direct API bypass testing
 Unauthorized-user penetration testing
 Expired-certification testing
 Wrong-area authorization testing
18. Architecture Gate

This requirement is an Architecture Gate.

Claude / Development Team MUST NOT implement Permit Approval as a simple:

if (user.Role == "PermitMan")

or equivalent role-only authorization mechanism.

The final implementation MUST distinguish:

Authentication
        +
Authorization
        +
Competency
        +
Certification
        +
Operational Scope
        +
Permit Authority
        +
Step-up Authentication
        +
Audit Integrity

A Permit approval is valid only when all mandatory controls pass.

19. Final Governance Principle

No identity + no authority + no competency = no Permit approval.

And:

Being logged in is not sufficient. Being Permit Man is not sufficient. Having a valid biometric authentication is not sufficient. The approver must be an authenticated, currently authorized, competent person with valid authority for the specific Permit and operational scope.

End of Rule Set
