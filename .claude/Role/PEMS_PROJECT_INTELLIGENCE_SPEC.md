from pathlib import Path

content = r"""# PEMS Project Intelligence & Visual Progress System

**Repository:** `saberahmadi48-collab/PEMS-cmms-edit-empoleeys`  
**Branch:** `master`  
**Purpose:** Evidence-based architecture audit, project progress assessment, Graphify integration, visual project intelligence, and implementation roadmap.

---

## 1. Mission

Build an internal **PEMS Project Intelligence** capability that answers:

> **Where is PEMS really today, what is complete, what is partial, what is missing, what is blocked, and what must be done next?**

This must be an evidence-based system.

Do **not** use manually hard-coded progress percentages as the source of truth.

The assessment must derive evidence from:

- Source code
- Controllers
- Services
- Repositories
- Entities
- DTOs
- Interfaces
- DbContext
- EF Core configurations
- Migrations
- Database schema
- Views / Razor
- JavaScript / CSS
- Authorization
- Configuration
- Documentation
- SQL
- Tests
- Git state/history
- Graphify Knowledge Graph

---

# 2. Non-Negotiable Rules

Before implementation:

1. Audit the existing repository.
2. Do not rewrite existing architecture unnecessarily.
3. Do not delete existing business logic.
4. Do not create migrations unless a database change is proven necessary.
5. Do not refactor unrelated code.
6. Do not introduce CQRS, microservices, multi-tenancy, event buses, or other unnecessary architecture.
7. Do not declare a feature complete without evidence.
8. Distinguish `Implemented` from `Verified`.
9. If evidence is insufficient, report `Unknown`; never guess.
10. Preserve the existing PEMS Design System and application architecture.
11. Existing Build success is a health indicator, not project completion.
12. `dotnet build` must remain healthy after changes.

---

# 3. Graphify

Graphify already exists in the repository under:

```text
.claude/skills/graphify/