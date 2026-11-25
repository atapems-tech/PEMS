# Reference data owners: the 61 seeder classes (SCRUM-802)

Proposal for the owner to confirm (SCRUM-516, rule in guideline 04 section 7). **Confirmed by the owner in chat on 2026-11-25: all 61 rows as proposed** (a later change of a row is written in the Confirmation column). A test reads this table: every class in `PEMS.Persistence/Seed` must be a row with one of the three owners.

Owners:

- **platform**: the product ships and updates the rows (an unedited row is updated, an edited row is kept and reported, a row we stop shipping is retired).
- **application**: the customer owns the data; the product seeds it once on a clean install, adds what is missing, and never updates or removes what exists.
- **extensible**: the product ships a baseline (platform rows, updated while unedited) and the customer adds rows of their own (never touched). In a reference-data definition it is `owner: platform`; the customer rows are simply rows without a state row.

| Seeder | Seeds | Proposed owner | Why | Status | Confirmation |
|---|---|---|---|---|---|
| `AccessControlSeeder` | Role permissions, SoD policies, permission grants (RBAC baseline) | application | Grants are adjusted in the permission matrix; seed once, additive, never remove a customer change | not moved | confirmed 2026-11-25 |
| `ConditionPriorityMappingSeeder` | Condition to priority mapping (CBM) | application | The own policy of the customer; the shipped values are a starting point | not moved | confirmed 2026-11-25 |
| `ContractWorkflowSeeder` | Contract finalization workflow, 2 stages | platform | Shipped workflow document (SCRUM-517) | slice: two stages with their own roles; needs its document and a parity test | confirmed 2026-11-25 |
| `DepartmentSeeder` | Departments | application | The organization belongs to the customer | not moved | confirmed 2026-11-25 |
| `DisciplineSeeder` | Engineering disciplines | extensible | Standard codes ship, customers add their own | not moved | confirmed 2026-11-25 |
| `EngineeringDocumentWorkflowBackfillService` | Backfill of workflow instances (not a list) | platform | One-time data repair, stays code | not a list | confirmed 2026-11-25 |
| `EngineeringDocumentWorkflowSeeder` | Engineering document workflow, 8 stages, versions 1 and 2 | platform | Shipped workflow document (SCRUM-517) | slice: two versions and a backfill service of running instances; the version history must be kept as it is, so it needs its own design | confirmed 2026-11-25 |
| `EquipmentClassSeeder` | Equipment classes | extensible | Standard classes ship, customers add classes | not moved | confirmed 2026-11-25 |
| `ExecutionGateSeeder` | Permit execution requirements per permit type | application | Safety policy the customer tunes; common industry defaults only | not moved | confirmed 2026-11-25 |
| `FailureCauseMechanismSeeder` | Failure cause to mechanism links | extensible | Links of the ISO 14224 taxonomy; customers add links | not moved | confirmed 2026-11-25 |
| `FailureCauseSeeder` | Failure causes | extensible | ISO 14224 baseline plus customer codes | not moved | confirmed 2026-11-25 |
| `FailureConsequenceSeeder` | Failure consequences | extensible | ISO 14224 baseline plus customer codes | not moved | confirmed 2026-11-25 |
| `FailureEffectSeeder` | Failure effects | extensible | ISO 14224 baseline plus customer codes | not moved | confirmed 2026-11-25 |
| `FailureMechanismSeeder` | Failure mechanisms | extensible | ISO 14224 baseline plus customer codes | moved: reference-data/core/failure-mechanism.json (803) | confirmed 2026-11-25 |
| `FailureModeSeeder` | Failure modes per equipment class, mode to cause links | extensible | ISO 14224 baseline plus customer codes | not moved | confirmed 2026-11-25 |
| `GasTestSeeder` | Gas parameter limits, gas test validity rule | application | Safety limits the customer sets; common industry defaults only | not moved | confirmed 2026-11-25 |
| `HelpContentSeeder` | Help articles and categories | platform | Product documentation, updated with every release | not moved | confirmed 2026-11-25 |
| `HelpKnowledgeSeeder` | Help knowledge layer (concepts, standard references) | platform | Product documentation | not moved | confirmed 2026-11-25 |
| `HelpVisualSeeder` | Help visuals | platform | Product documentation | not moved | confirmed 2026-11-25 |
| `IrnDispositionWorkflowSeeder` | Workflow: irn-disposition | platform | Shipped workflow document (SCRUM-517); a customised workflow keeps its version and shows the upstream diff | moved: Workflow/Shipped/irn-disposition.workflow.json (804) | confirmed 2026-11-25 |
| `IrnHoldResolutionWorkflowSeeder` | Workflow: irn-hold-resolution | platform | Shipped workflow document (SCRUM-517); a customised workflow keeps its version and shows the upstream diff | moved: Workflow/Shipped/irn-hold-resolution.workflow.json (804) | confirmed 2026-11-25 |
| `LetterTemplateSeeder` | Internal letter templates | extensible | Templates ship; customers copy and edit them (their edit must survive) | not moved | confirmed 2026-11-25 |
| `MOCWorkflowSeeder` | Management of change workflow, 4 stages | platform | Shipped workflow document (SCRUM-517) | slice: four stages, needs its document and a parity test | confirmed 2026-11-25 |
| `MaintenanceActivitySeeder` | Maintenance activities | extensible | Standards-based baseline plus customer activities | not moved | confirmed 2026-11-25 |
| `MaintenanceGroupSeeder` | Maintenance groups | application | Organizational grouping of the customer | not moved | confirmed 2026-11-25 |
| `MaintenanceRoleSeeder` | Maintenance roles per discipline | application | The maintenance organization of the customer | not moved | confirmed 2026-11-25 |
| `MaintenanceStrategyTypeSeeder` | Maintenance strategy types | extensible | Standards-based baseline plus customer types | not moved | confirmed 2026-11-25 |
| `MaintenanceStrategyWorkflowSeeder` | Workflow: maintenance-strategy | platform | Shipped workflow document (SCRUM-517); a customised workflow keeps its version and shows the upstream diff | moved: Workflow/Shipped/maintenance-strategy.workflow.json (804) | confirmed 2026-11-25 |
| `MaintenanceTypeSeeder` | Maintenance types | extensible | Standards-based baseline plus customer types | moved: reference-data/core/maintenance-type.json (803) | confirmed 2026-11-25 |
| `MaterialCategorySeeder` | Material categories | extensible | Baseline plus customer categories | not moved | confirmed 2026-11-25 |
| `MaterialUOMSeeder` | Units of measure | extensible | Baseline plus customer units | moved: reference-data/core/material-uom.json (803) | confirmed 2026-11-25 |
| `NcrApprovalWorkflowSeeder` | Workflow: ncr-critical-approval | platform | Shipped workflow document (SCRUM-517); a customised workflow keeps its version and shows the upstream diff | moved: Workflow/Shipped/ncr-critical-approval.workflow.json (804) | confirmed 2026-11-25 |
| `NumberingBackfillSeeder` | Numbering sequences from existing work order numbers | application | Counter state of the customer, not a list; one-time backfill stays code | stays code | confirmed 2026-11-25 |
| `NumberingSchemeSeeder` | Numbering scheme per procurement document type | application | The customer edits patterns; seed once when missing | not moved | confirmed 2026-11-25 |
| `OperationsOrganizationSeeder` | Operations departments | application | The organization belongs to the customer | not moved | confirmed 2026-11-25 |
| `PMTemplateSeeder` | PM templates | application | Small example set; the customer owns its templates | not moved | confirmed 2026-11-25 |
| `PartCatalogueSeeder` | Part catalogue commodity groups, sections, workflow steps (Excel) | extensible | Reference classification plus customer additions; Excel import | not moved | confirmed 2026-11-25 |
| `PartCatalogueWorkflowSeeder` | Part catalogue request workflow with entry condition and responsibilities | platform | Shipped workflow document (SCRUM-517) | slice: reads the part catalogue workflow steps and actor responsibilities (not only stages), needs the responsibility part of the document first | confirmed 2026-11-25 |
| `PermitApprovalWorkflowSeeder` | Permit to work approval chain, 3 stages | platform | Shipped workflow document (SCRUM-517) | slice: three stages, needs its document and a parity test | confirmed 2026-11-25 |
| `PreProcurementWorkflowSeeder` | Workflow: pre-procurement | platform | Shipped workflow document (SCRUM-517); a customised workflow keeps its version and shows the upstream diff | moved: Workflow/Shipped/pre-procurement.workflow.json (804) | confirmed 2026-11-25 |
| `ProcurementReadViewSeeder` | Role module actions and role permissions for procurement read pages | application | Grants adjusted in the matrix; additive | not moved | confirmed 2026-11-25 |
| `PurchaseOrderApprovalWorkflowSeeder` | Workflow: purchase-order-approval | platform | Shipped workflow document (SCRUM-517); a customised workflow keeps its version and shows the upstream diff | moved: Workflow/Shipped/purchase-order-approval.workflow.json (804) | confirmed 2026-11-25 |
| `QCDisciplineSeeder` | QC disciplines | extensible | Industry baseline plus customer disciplines | moved: reference-data/core/qc-discipline.json (803) | confirmed 2026-11-25 |
| `QualificationCertificationSeeder` | Qualifications and certifications | extensible | Industry baseline plus customer entries | not moved | confirmed 2026-11-25 |
| `RoleCatalogSeeder` | Roles, role permissions, navigation visibility | extensible | Product roles ship; customers add roles and edit grants | not moved | confirmed 2026-11-25 |
| `SimopsSeeder` | SIMOPS interaction rules per permit type pair | application | Safety policy the customer sets | not moved | confirmed 2026-11-25 |
| `SkillSeeder` | Skills | application | Example list of a blueprint; the customer owns its skills | not moved | confirmed 2026-11-25 |
| `StandardReferenceSeeder` | Standard references (ISO, API, ...) | platform | Public standards the product ships and updates | not moved | confirmed 2026-11-25 |
| `SystemDefinitionSeeder` | Definitions of the product (not a list) | platform | Shipped definitions with their own owner rule (SCRUM-448) | not a list | confirmed 2026-11-25 |
| `TechnicalQueryWorkflowSeeder` | Technical query workflow, 2 stages | platform | Shipped workflow document (SCRUM-517) | slice: two stages, needs its document and a parity test | confirmed 2026-11-25 |
| `VdrWorkflowSeeder` | VDR submission workflow, 3 stages | platform | Shipped workflow document (SCRUM-517) | slice: three stages, needs its document and a parity test | confirmed 2026-11-25 |
| `WarehouseIntakeWorkflowSeeder` | Workflow: warehouse-intake | platform | Shipped workflow document (SCRUM-517); a customised workflow keeps its version and shows the upstream diff | moved: Workflow/Shipped/warehouse-intake.workflow.json (804) | confirmed 2026-11-25 |
| `WarehouseSeeder` | Warehouses from MRS prefixes | application | The own sites of the customer | not moved | confirmed 2026-11-25 |
| `WorkOrderGovernanceSeeder` | Work order permissions and role grants | application | Grants adjusted in the matrix; additive | not moved | confirmed 2026-11-25 |
| `WorkOrderLifecycleWorkflowSeeder` | Work order lifecycle: definition, version and binding | platform | Shipped workflow document (SCRUM-517) | slice: also writes a workflow binding; the binding is not part of the shipped document yet | confirmed 2026-11-25 |
| `WorkOrderLifecycleWorkflowV2Seeder` | Work order lifecycle version 2 and permission grants | platform | Shipped workflow document (SCRUM-517) | slice: second version of a bound workflow plus grants; the upgrade path must be designed with the binding | confirmed 2026-11-25 |
| `WorkOrderReopenPermissionSeeder` | Work order reopen permission grants | application | Additive grants; a manual deactivation stays | not moved | confirmed 2026-11-25 |
| `WorkOrderReviewWorkflowSeeder` | Work order review workflow, 1 stage | platform | Shipped workflow document (SCRUM-517) | slice: one stage, next family to convert (the same shape as the converted ones) | confirmed 2026-11-25 |
| `WorkRequestWorkflowSeeder` | Work request workflow, definition and binding | platform | Shipped workflow document (SCRUM-517) | slice: also writes permissions and a binding, not part of the shipped document yet | confirmed 2026-11-25 |
| `WorkflowBindingBackfillSeeder` | Explicit binding of every module workflow (not a list) | platform | Shipped workflow document (SCRUM-517) | stays code: a backfill over the bindings, not workflow content | confirmed 2026-11-25 |
| `WorkflowManagementSeeder` | Workflow Management role grants | application | Additive grants | not moved | confirmed 2026-11-25 |

Count: 61 seeder classes (application 18, extensible 18, platform 25).
