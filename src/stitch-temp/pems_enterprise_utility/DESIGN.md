---
name: PEMS Enterprise Utility
colors:
  surface: '#f8f9ff'
  surface-dim: '#c8dcf5'
  surface-bright: '#f8f9ff'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#eef4ff'
  surface-container: '#e4efff'
  surface-container-high: '#daeaff'
  surface-container-highest: '#d0e4fe'
  on-surface: '#081d2f'
  on-surface-variant: '#44474c'
  inverse-surface: '#1f3245'
  inverse-on-surface: '#e9f1ff'
  outline: '#75777d'
  outline-variant: '#c5c6cd'
  surface-tint: '#535f75'
  primary: '#000819'
  on-primary: '#ffffff'
  primary-container: '#142033'
  on-primary-container: '#7c889f'
  inverse-primary: '#bbc7e0'
  secondary: '#895200'
  on-secondary: '#ffffff'
  secondary-container: '#fdaa47'
  on-secondary-container: '#6e4100'
  tertiary: '#000815'
  on-tertiary: '#ffffff'
  tertiary-container: '#00213d'
  on-tertiary-container: '#498bcd'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#d7e3fd'
  primary-fixed-dim: '#bbc7e0'
  on-primary-fixed: '#101c2f'
  on-primary-fixed-variant: '#3b475c'
  secondary-fixed: '#ffdcbc'
  secondary-fixed-dim: '#ffb86a'
  on-secondary-fixed: '#2c1700'
  on-secondary-fixed-variant: '#683d00'
  tertiary-fixed: '#d1e4ff'
  tertiary-fixed-dim: '#9ecaff'
  on-tertiary-fixed: '#001d36'
  on-tertiary-fixed-variant: '#00497c'
  background: '#f8f9ff'
  on-background: '#081d2f'
  surface-variant: '#d0e4fe'
typography:
  headline-lg:
    fontFamily: Inter
    fontSize: 24px
    fontWeight: '700'
    lineHeight: 32px
    letterSpacing: -0.01em
  headline-md:
    fontFamily: Inter
    fontSize: 18px
    fontWeight: '700'
    lineHeight: 24px
    letterSpacing: '0'
  kpi-stat:
    fontFamily: Inter
    fontSize: 22px
    fontWeight: '700'
    lineHeight: 24px
    letterSpacing: -0.02em
  body-base:
    fontFamily: Inter
    fontSize: 13.5px
    fontWeight: '400'
    lineHeight: 20px
    letterSpacing: '0'
  body-medium:
    fontFamily: Inter
    fontSize: 13.5px
    fontWeight: '600'
    lineHeight: 20px
    letterSpacing: '0'
  table-cell:
    fontFamily: Inter
    fontSize: 13px
    fontWeight: '400'
    lineHeight: 18px
    letterSpacing: '0'
  table-header:
    fontFamily: Inter
    fontSize: 11.5px
    fontWeight: '700'
    lineHeight: 16px
    letterSpacing: 0.04em
  label-caps:
    fontFamily: Inter
    fontSize: 11px
    fontWeight: '700'
    lineHeight: 14px
    letterSpacing: 0.06em
  badge-label:
    fontFamily: Inter
    fontSize: 11px
    fontWeight: '600'
    lineHeight: 14px
    letterSpacing: 0.02em
  headline-lg-mobile:
    fontFamily: Inter
    fontSize: 20px
    fontWeight: '700'
    lineHeight: 28px
rounded:
  sm: 0.125rem
  DEFAULT: 0.25rem
  md: 0.375rem
  lg: 0.5rem
  xl: 0.75rem
  full: 9999px
spacing:
  unit: 4px
  xs: 4px
  sm: 8px
  md: 16px
  lg: 24px
  xl: 32px
  gutter: 16px
  sidebar-width: 260px
  topbar-height: 60px
---

## Brand & Style

The design system for this CMMS platform is engineered for mission-critical reliability, analytical precision, and high operational throughput. It adopts a **Corporate / Modern** aesthetic refined for industrial environments, emphasizing high information density without sacrificing clarity. 

The visual atmosphere is grounded in authoritative deep navies and crisp, low-fatigue slate backgrounds. It utilizes a strict hierarchy to manage complex data structures—ranging from asset tagging to multi-discipline engineering workflows. The style avoids superfluous decoration, opting instead for functional elevation, precise borders, and a logical flow that supports both daylight control room operations and low-glare night shifts.

- **Tone**: Authoritative, stable, efficient, and technical.
- **Visual Strategy**: High-density layouts, clear functional color-coding for statuses, and robust interactive focal points using industrial amber.

## Colors

This design system uses a palette calibrated for long-term readability and rapid situational awareness. 

- **Primary (Navy)**: Used for structural branding, deep navigation elements, and primary headings to establish authority.
- **Secondary (Industrial Amber)**: Reserved for high-priority actions, active states, and focus indicators. Its warm contrast against the navy foundation ensures "call-to-action" elements are immediately visible.
- **Tertiary (Engineering Blue)**: Applied to technical links, formal transmittals, and engineering-specific metadata to distinguish them from operational actions.
- **Functional Palette**: 
    - **Success (Emerald)**: For approved documents and healthy assets.
    - **Warning (Amber)**: For pending queue items and in-progress work.
    - **Danger (Crimson)**: For critical failures and emergency work orders.
- **Neutral Scale**: A refined range of slates (`#5c6f85` and `#1b2733`) manages secondary text and UI borders to minimize cognitive load in data-heavy views.

## Typography

Typography is optimized for technical, numerical, and alphanumeric data. By utilizing **Inter** across all levels, the system ensures maximum legibility for complex equipment tags (e.g., `P-101A`) and high-density data tables.

Key typographic rules:
- **Numerical Isolation**: In RTL contexts, technical codes and metric counts must maintain LTR directionality to prevent punctuation inversion.
- **Case Usage**: Uppercase is used strictly for table headers and sidebar section labels to provide structural contrast without increasing font size.
- **Data Density**: Body and table sizes are slightly reduced (13px - 13.5px) to maximize the amount of information visible on standard enterprise displays.

## Layout & Spacing

The layout utilizes a **Fixed Sidebar + Fluid Content** model. This provides a consistent navigation anchor (the sidebar) while allowing the workspace to expand for wide data tables.

- **Grid System**: A standard 12-column fluid grid is used within the content workspace.
- **Spacing Rhythm**: A 4px micro-increment grid ensures consistent optical alignment. Components like input fields and small buttons use 8px (sm) or 12px (between sm and md) internal padding.
- **Breakpoints**: 
    - **Desktop (≥992px)**: Persistent 260px sidebar. 24px-28px page margins.
    - **Mobile/Tablet (<992px)**: Sidebar transitions to an off-canvas drawer. Page margins reduce to 16px. KPI cards reflow from 4-across to a 2x2 or 1x1 stack.
- **Vertical Density**: Table rows use compact vertical padding (8px) to reduce scrolling in long equipment registries.

## Elevation & Depth

Visual hierarchy is conveyed through **Tonal Layers** and **Low-Contrast Outlines**. Because the UI is data-heavy, shadows are used sparingly to prevent visual "muddiness."

- **Surface Levels**:
    - **Level 0 (Background)**: `#f3f5f8` (Canvas).
    - **Level 1 (Card/Surface)**: `#ffffff` (White) with a 1px `#edf0f4` border.
    - **Level 2 (Modals/Popovers)**: White surface with an ambient, extra-diffused shadow (`0 4px 20px rgba(20,32,51,0.08)`).
- **Interactions**: On hover, interactive cards should transition their border color to the Industrial Amber color or apply a very subtle background tint (`rgba(31, 78, 120, 0.04)`).
- **Sticky Headers**: Use a slightly darker neutral (`#f5f7fa`) for sticky table headers to distinguish the fixed structural elements from the scrolling data rows.

## Shapes

The shape language is **Soft**, striking a balance between the rigidity of industrial software and the approachability of modern SaaS.

- **Standard Radius (8px)**: Used for buttons, input fields, and standard content cards.
- **Small Radius (4px - 6px)**: Used for nested elements like chips, status badges, and compact table-row buttons.
- **Large Radius (12px)**: Used for high-level KPI cards and large modal containers to soften the layout.
- **Interactive States**: Focus rings should follow the 8px radius with a 2px-3px offset using the Industrial Amber color at 15% opacity.

## Components

### Buttons
- **Primary**: Solid gradient (`#d98c2b` to `#b8721e`) with white text. High-contrast for main actions.
- **Secondary**: 1px border (`#dde3ea`) with `#5c6f85` text.
- **Quick Action**: Compact square (32px or 36px) with an 8px radius for row-level edits and deletes.

### KPI Cards
Each KPI card should feature a `46x46px` icon tile with 10px rounded corners. The tile background should use 135-degree gradients corresponding to the status (e.g., Blue for total assets, Red for critical failures).

### Data Tables
- **Headers**: Sticky, 11.5px Bold, Uppercase, `#f5f7fa` background.
- **Rows**: 13px font, vertical-align middle, subtle hover tint.
- **Registry Monospace**: Use a monospaced font for Tag Numbers and Document IDs to ensure character alignment and readability of technical codes.

### Form Inputs
Standardize on an 8px radius and a 1px `#dde3ea` border. On focus, the border transitions to Industrial Amber with a soft amber glow (focus ring). Use "Compact" variants (6px-10px padding) for multi-column filter bars.

### Status Badges
Pill-shaped (999px radius) using low-saturation background tints and high-saturation text for readability. Examples:
- **Active**: Light Green bg / Dark Green text.
- **Draft**: Light Slate bg / Dark Slate text.
- **Critical**: Light Red bg / Dark Red text.