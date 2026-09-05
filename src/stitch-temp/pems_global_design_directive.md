# PEMS Enterprise Utility - Global Design Directive & Skill Definition

## 1. Core Identity & Visual Language
The PEMS (Predictive Enterprise Utility Management System) follows a high-density, professional engineering aesthetic designed for large-scale industrial operations.

- **Primary Motif:** Enterprise Utility Utility (Data-dense, high-fidelity).
- **Color Palette:** Professional deep navy (#142033) for primary brand elements, balanced with utility surfaces (Surface-dim #c8dcf5, Surface-low #eef4ff).
- **Typography:** Inter (Sans-serif). Headlines use Bold/Semi-bold for hierarchy, body text uses Medium/Regular for legibility in data grids.
- **Components:** High-density data tables, interactive Sankey diagrams, 3D BIM/CAD integrations, and multi-step configuration wizards.
- **Localization:** 1:1 Dual-language support (English LTR / Persian RTL) across all labels, data, and charts.

## 2. Technical Design Principles (Skills)
Any AI assistant or developer working on this project must adhere to these established skills:

### A. Dual-Language Engineering (EN/FA)
- **Constraint:** Never generate a screen in a single language unless explicitly requested for a test.
- **Logic:** Labels must be mirrored. RTL layouts for Persian must maintain logical alignment (icons flip, text justifies right).

### B. Industrial Data Density
- **Logic:** Maximize whitespace efficiency. Use compact padding and professional type scales to ensure maximum data visibility on 1920x1080 resolution.
- **Component Pattern:** Sidebar-driven navigation with a persistent top bar for global actions (Search, Notifications, Profile, Language Switch).

### C. Technical Process Mapping
- **Skill:** Translating complex engineering workflows (VDR/MDR, RCA Fishbone, LOTO checklists) into intuitive digital interfaces.
- **Constraint:** Maintain 1:1 parity with the technical schema found in the GitHub repository (saberahmadi48-collab/PEMS-cmms-edit-empoleeys).

### D. System Governance & Admin
- **Skill:** Centralized 'Quick Configuration' logic. All bulk imports (Excel/CSV) and system-wide settings are handled through the Admin Control Plane.

## 3. Persistent Design Tokens
Refer to `{{DATA:DESIGN_SYSTEM:DESIGN_SYSTEM_1}}` for specific hex codes, spacing units (Round_Four), and typography scales.

## 4. Current Progress State
- **Modules Completed:** Assets, Maintenance, Personnel, Safety (HSE), Engineering, Procurement (MRO), Utilities (Energy), Emergency Response, Analytics (BI/AI), System Admin, and Onboarding.
- **Screen Count:** 210+ screens across Desktop and Mobile (Tablet-optimized Field App).
