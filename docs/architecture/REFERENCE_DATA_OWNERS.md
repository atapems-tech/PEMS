# Reference data owners: the 61 seeder classes (SCRUM-802)

Proposal for the owner to confirm (SCRUM-516, rule in guideline 04 section 7). **Nothing in the Confirmation column is confirmed yet**: the owner sets each row to `confirmed` or writes the changed owner. A test reads this table: every class in `PEMS.Persistence/Seed` must be a row with one of the three owners.

Owners:

- **platform**: the product ships and updates the rows (an unedited row is updated, an edited row is kept and reported, a row we stop shipping is retired).
- **application**: the customer owns the data; the product seeds it once on a clean install, adds what is missing, and never updates or removes what exists.
- **extensible**: the product ships a baseline (platform rows, updated while unedited) and the customer adds rows of their own (never touched). In a reference-data definition it is `owner: platform`; the customer rows are simply rows without a state row.

| Seeder | Seeds | Proposed owner | Why | Status | Confirmation |
|---|---|---|---|---|---|
| `AccessControlSeeder` | Role permissions, SoD policies, permission grants (RBAC baseline) | application | Grants are adjusted in the permission matrix; seed once, additive, never remove a customer change | not moved | pending |
| `ConditionPriorityMappingSeeder` | Condition to priority mapping (CBM) | application | The own policy of the customer; the shipped values are a starting point | not moved | pending |
| `ContractWorkflowSeeder` | Contract finalization workflow, 2 stages | platform | Shipped workflow document (SCRUM-517) | slice: two stages with their own roles; needs its document and a parity test | pending |
| `DepartmentSeeder` | Departments | application | The organization belongs to the customer | not moved | pending |
| `DisciplineSeeder` | Engineering disciplines | extensible | Standard codes ship, customers add their own | not moved | pending |
| `EngineeringDocumentWorkflowBackfillService` | Backfill of workflow instances (not a list) | platform | One-time data repair, stays code | not a list | pending |
| `EngineeringDocumentWorkflowSeeder` | Engineering document workflow, 8 stages, versions 1 and 2 | platform | Shipped workflow document (SCRUM-517) | slice: two versions and a backfill service of running instances; the version history must be kept as it is, so it needs its own design | pending |
| `EquipmentClassSeeder` | Equipment classes | extensible | Standard classes ship, customers add classes | not moved | pending |
| `ExecutionGateSeeder` | Permit execution requirements per permit type | application | Safety policy the customer tunes; common industry defaults only | not moved | pending |
| `FailureCauseMechanismSeeder` | Failure cause to mechanism links | extensible | Links of the ISO 14224 taxonomy; customers add links | not moved | pending |
| `FailureCauseSeeder` | Failure causes | extensible | ISO 14224 baseline plus customer codes | not moved | pending |
| `FailureConsequenceSeeder` | Failure consequences | extensible | ISO 14224 baseline plus customer codes | not moved | pending |
| `FailureEffectSeeder` | Failure effects | extensible | ISO 14224 baseline plus customer codes | not moved | pending |
| `FailureMechanismSeeder` | Failure mechanisms | extensible | ISO 14224 baseline plus customer codes | moved: reference-data/core/failure-mechanism.json (803) | pending |
| `FailureModeSeeder` | Failure modes per equipment class, mode to cause links | extensible | ISO 14224 baseline plus customer codes | not moved | pending |
| `GasTestSeeder` | Gas parameter limits, gas test validity rule | application | Safety limits the customer sets; common industry defaults only | not moved | pending |
| `HelpContentSeeder` | Help articles and categories | platform | Product documentation, updated with every release | not moved | pending |
| `HelpKnowledgeSeeder` | Help knowledge layer (concepts, standard references) | platform | Product documentation | not moved | pending |
| `HelpVisualSeeder` | Help visuals | platform | Product documentation | not moved | pending |
| `IrnDispositionWorkflowSeeder` | Workflow: irn-disposition | platform | Shipped workflow document (SCRUM-517); a customised workflow keeps its version and shows the upstream diff | moved: Workflow/Shipped/irn-disposition.workflow.json (804) | pending |
| `IrnHoldResolutionWorkflowSeeder` | Workflow: irn-hold-resolution | platform | Shipped workflow document (SCRUM-517); a customised workflow keeps its version and shows the upstream diff | moved: Workflow/Shipped/irn-hold-resolution.workflow.json (804) | pending |
| `LetterTemplateSeeder` | Internal letter templates | extensible | Templates ship; customers copy and edit them (their edit must survive) | not moved | pending |
| `MOCWorkflowSeeder` | Management of change workflow, 4 stages | platform | Shipped workflow document (SCRUM-517) | slice: four stages, needs its document and a parity test | pending |
| `MaintenanceActivitySeeder` | Maintenance activities | extensible | Standards-based baseline plus customer activities | not moved | pending |
| `MaintenanceGroupSeeder` | Maintenance groups | application | Organizational grouping of the customer | not moved | pending |
| `MaintenanceRoleSeeder` | Maintenance roles per discipline | application | The maintenance organization of the customer | not moved | pending |
| `MaintenanceStrategyTypeSeeder` | Maintenance strategy types | extensible | Standards-based baseline plus customer types | not moved | pending |
| `MaintenanceStrategyWorkflowSeeder` | Workflow: maintenance-strategy | platform | Shipped workflow document (SCRUM-517); a customised workflow keeps its version and shows the upstream diff | moved: Workflow/Shipped/maintenance-strategy.workflow.json (804) | pending |
| `MaintenanceTypeSeeder` | Maintenance types | extensible | Standards-based baseline plus customer types | moved: reference-data/core/maintenance-type.json (803) | pending |
| `MaterialCategorySeeder` | Material categories | extensible | Baseline plus customer categories | not moved | pending |
| `MaterialUOMSeeder` | Units of measure | extensible | Baseline plus customer units | moved: reference-data/core/material-uom.json (803) | pending |
| `NcrApprovalWorkflowSeeder` | Workflow: ncr-critical-approval | platform | Shipped workflow document (SCRUM-517); a customised workflow keeps its version and shows the upstream diff | moved: Workflow/Shipped/ncr-critical-approval.workflow.json (804) | pending |
| `NumberingBackfillSeeder` | Numbering sequences from existing work order numbers | application | Counter state of the customer, not a list; one-time backfill stays code | stays code | pending |
| `NumberingSchemeSeeder` | Numbering scheme per procurement document type | application | The customer edits patterns; seed once when missing | not moved | pending |
| `OperationsOrganizationSeeder` | Operations departments | application | The organization belongs to the customer | not moved | pending |
| `PMTemplateSeeder` | PM templates | application | Small example set; the customer owns its templates | not moved | pending |
| `PartCatalogueSeeder` | Part catalogue commodity groups, sections, workflow steps (Excel) | extensible | Reference classification plus customer additions; Excel import | not moved | pending |
| `PartCatalogueWorkflowSeeder` | Part catalogue request workflow with entry condition and responsibilities | platform | Shipped workflow document (SCRUM-517) | slice: reads the part catalogue workflow steps and actor responsibilities (not only stages), needs the responsibility part of the document first | pending |
| `PermitApprovalWorkflowSeeder` | Permit to work approval chain, 3 stages | platform | Shipped workflow document (SCRUM-517) | slice: three stages, needs its document and a parity test | pending |
| `PreProcurementWorkflowSeeder` | Workflow: pre-procurement | platform | Shipped workflow document (SCRUM-517); a customised workflow keeps its version and shows the upstream diff | moved: Workflow/Shipped/pre-procurement.workflow.json (804) | pending |
| `ProcurementReadViewSeeder` | Role module actions and role permissions for procurement read pages | application | Grants adjusted in the matrix; additive | not moved | pending |
| `PurchaseOrderApprovalWorkflowSeeder` | Workflow: purchase-order-approval | platform | Shipped workflow document (SCRUM-517); a customised workflow keeps its version and shows the upstream diff | moved: Workflow/Shipped/purchase-order-approval.workflow.json (804) | pending |
| `QCDisciplineSeeder` | QC disciplines | extensible | Industry baseline plus customer disciplines | moved: reference-data/core/qc-discipline.json (803) | pending |
| `QualificationCertificationSeeder` | Qualifications and certifications | extensible | Industry baseline plus customer entries | not moved | pending |
| `RoleCatalogSeeder` | Roles, role permissions, navigation visibility | extensible | Product roles ship; customers add roles and edit grants | not moved | pending |
| `SimopsSeeder` | SIMOPS interaction rules per permit type pair | application | Safety policy the customer sets | not moved | pending |
| `SkillSeeder` | Skills | application | Example list of a blueprint; the customer owns its skills | not moved | pending |
| `StandardReferenceSeeder` | Standard references (ISO, API, ...) | platform | Public standards the product ships and updates | not moved | pending |
| `SystemDefinitionSeeder` | Definitions of the product (not a list) | platform | Shipped definitions with their own owner rule (SCRUM-448) | not a list | pending |
| `TechnicalQueryWorkflowSeeder` | Technical query workflow, 2 stages | platform | Shipped workflow document (SCRUM-517) | slice: two stages, needs its document and a parity test | pending |
| `VdrWorkflowSeeder` | VDR submission workflow, 3 stages | platform | Shipped workflow document (SCRUM-517) | slice: three stages, needs its document and a parity test | pending |
| `WarehouseIntakeWorkflowSeeder` | Workflow: warehouse-intake | platform | Shipped workflow document (SCRUM-517); a customised workflow keeps its version and shows the upstream diff | moved: Workflow/Shipped/warehouse-intake.workflow.json (804) | pending |
| `WarehouseSeeder` | Warehouses from MRS prefixes | application | The own sites of the customer | not moved | pending |
| `WorkOrderGovernanceSeeder` | Work order permissions and role grants | application | Grants adjusted in the matrix; additive | not moved | pending |
| `WorkOrderLifecycleWorkflowSeeder` | Work order lifecycle: definition, version and binding | platform | Shipped workflow document (SCRUM-517) | slice: also writes a workflow binding; the binding is not part of the shipped document yet | pending |
| `WorkOrderLifecycleWorkflowV2Seeder` | Work order lifecycle version 2 and permission grants | platform | Shipped workflow document (SCRUM-517) | slice: second version of a bound workflow plus grants; the upgrade path must be designed with the binding | pending |
| `WorkOrderReopenPermissionSeeder` | Work order reopen permission grants | application | Additive grants; a manual deactivation stays | not moved | pending |
| `WorkOrderReviewWorkflowSeeder` | Work order review workflow, 1 stage | platform | Shipped workflow document (SCRUM-517) | slice: one stage, next family to convert (the same shape as the converted ones) | pending |
| `WorkRequestWorkflowSeeder` | Work request workflow, definition and binding | platform | Shipped workflow document (SCRUM-517) | slice: also writes permissions and a binding, not part of the shipped document yet | pending |
| `WorkflowBindingBackfillSeeder` | Explicit binding of every module workflow (not a list) | platform | Shipped workflow document (SCRUM-517) | stays code: a backfill over the bindings, not workflow content | pending |
| `WorkflowManagementSeeder` | Workflow Management role grants | application | Additive grants | not moved | pending |

Count: 61 seeder classes (application 18, extensible 18, platform 25).
