using Microsoft.AspNetCore.Identity;
using PEMS.Domain.Entities;
using PEMS.Persistence.Identity;

namespace PEMS.Web.Definitions;

/// <summary>
/// The entities definitions may use (the waves of the migration): one registration per entity with the permission and the roles that guard it today (the same as its controller), and the property names a
/// grid or an option list may read and a create or edit may write. Registering an entity is all the code a master-data page, form and action need; the rest is definition files
/// (<c>tools/definitions/make_entity_pages.py</c>). Entities with business rules express them as <c>deny</c> and <c>unique</c> rules in the action definition, never as a handler.
/// </summary>
internal static class EntityCatalogDefaults
{
    private static readonly string[] AdminRoles = [SystemRoles.SystemAdmin];
    private static readonly string[] OrganizationRoles = [SystemRoles.SystemAdmin, SystemRoles.EngineeringManager];

    public static EntityCatalog Build() => new EntityCatalog()
        // Administration (W1)
        .Add<Department>("DepartmentId", e => e.Roles(OrganizationRoles, OrganizationRoles)
            .Readable("Code", "Name", "Description", "IsActive", "CreatedDate").Writable("Code", "Name", "Description", "IsActive", "CreatedDate"))
        .Add<ApplicationRole>("Id", e => e.Roles(AdminRoles, AdminRoles)
            .Readable("Name", "DisplayName", "Description", "Domain", "Category", "IsSystemRole", "SortOrder").Writable("Name", "DisplayName", "Description")
            .BeforeCreate(r => { r.NormalizedName = r.Name?.ToUpperInvariant(); r.ConcurrencyStamp = Guid.NewGuid().ToString(); }))
        .Add<SecurityEvent>("SecurityEventId", e => e.Roles(AdminRoles, null)
            .Readable("EventType", "Method", "Result", "FailureReason", "ActorUserName", "TargetUserName", "IpAddress", "OccurredAtUtc"))
        .Add<AuditLog>("AuditLogId", e => e.Roles(AdminRoles, null)
            .Readable("EntityName", "EntityKeyValue", "Action", "ChangedFieldsSummary", "UserId", "UserName", "Timestamp", "IsAIAssisted"))
        .Add<AuthorizationAuditLog>("AuthorizationAuditLogId", e => e.Roles(AdminRoles, null)
            .Readable("UserId", "ResourceName", "ActionName", "ResourceId", "ScopeType", "ScopeValue", "Decision", "Reason", "Timestamp", "IpAddress"))
        .Add<DefinitionGrant>("DefinitionGrantId", e => e.Roles(AdminRoles, AdminRoles)   // SCRUM-591: an administrator opens or closes a definition for a role without a deploy
            .Readable("Kind", "DefinitionKey", "RoleName", "Effect", "Reason", "CreatedBy", "CreatedDate").Writable("Kind", "DefinitionKey", "RoleName", "Effect", "Reason"))
        .Add<SoDPolicy>("SoDPolicyId", e => e.Roles(AdminRoles, AdminRoles)
            .Readable("PermissionAId", "PermissionBId", "Description", "IsActive", "CreatedDate").Writable("PermissionAId", "PermissionBId", "Description", "IsActive", "CreatedDate"))
        .Add<ContractApprovalAuthorityRule>("ContractApprovalAuthorityRuleId", e => e.Roles(AdminRoles, AdminRoles)
            .Readable("MinValue", "MaxValue", "CriticalityClass", "RequiredApproverRole", "Description", "Priority", "IsActive")
            .Writable("MinValue", "MaxValue", "CriticalityClass", "RequiredApproverRole", "Description", "Priority", "IsActive"))
        .Add<RoutingRule>("RoutingRuleId", e => e.Roles(AdminRoles, null).Readable("DocumentCategory", "TargetModule", "AllowAutoRouting", "Description", "IsActive"))
        .Add<AIConfidenceSettings>("AIConfidenceSettingsId", e => e.Roles(AdminRoles, null).Readable("HighThreshold", "MediumThreshold", "ModifiedDate"))
        .Add<StorageRoute>("StorageRouteId", e => e.Roles(AdminRoles, null).Readable("Module", "DocumentType", "StorageProviderId", "RelativeRootPath", "IsEnabled", "IsDefault", "Priority"))
        .Add<StorageProvider>("StorageProviderId", e => e.Roles(AdminRoles, null).Readable("Name", "ProviderType"))
        // read-only registrations used for related counts and labels
        .Add<ApplicationUser>("Id", e => e.Roles(AdminRoles, null).Readable("UserName"))
        .Add<Permission>("PermissionId", e => e.Roles(AdminRoles, null).Readable("Name", "IsActive"))
        .Add<Employee>("EmployeeId", e => e.Roles(OrganizationRoles, null).Readable("DepartmentId", "IsActive"))
        .Add<IdentityUserRole<int>>("RoleId", e => e.Roles(AdminRoles, null).Readable("UserId", "RoleId"))
        .Add<RolePermissionGrant>("RolePermissionGrantId", e => e.Roles(AdminRoles, null).Readable("RoleId", "IsActive"))
        // Inventory master data (W4): InventoryMasterData permissions
        .Add<MaterialUOM>("MaterialUOMId", e => e.Create("InventoryMasterData.Create").Edit("InventoryMasterData.Edit")
            .Readable("Code", "Name", "Description", "ConversionFactor", "IsActive", "CreatedDate").Writable("Code", "Name", "Description", "ConversionFactor", "IsActive", "CreatedDate"))
        .Add<MaterialCategory>("MaterialCategoryId", e => e.Create("InventoryMasterData.Create").Edit("InventoryMasterData.Edit")
            .Readable("Code", "Name", "Description", "ParentCategoryId", "IsActive", "CreatedDate").Writable("Code", "Name", "Description", "ParentCategoryId", "IsActive", "CreatedDate"))
        .Add<StorageLocation>("StorageLocationId", e => e.Create("InventoryMasterData.Create").Edit("InventoryMasterData.Edit")
            .Readable("Code", "Name", "WarehouseId", "ParentLocationId", "IsActive", "CreatedDate").Writable("Code", "Name", "WarehouseId", "ParentLocationId", "IsActive", "CreatedDate"))
        // Maintenance master data (W2): MaintenanceMasterData permissions
        .Add<Discipline>("DisciplineId", e => e.Readable("Code", "Name", "IsActive"))
        .Add<MaintenanceGroup>("MaintenanceGroupId", e => e.Read("MaintenanceMasterData.View").Create("MaintenanceMasterData.Create").Edit("MaintenanceMasterData.Edit")
            .Readable("Code", "Name", "DisciplineId", "ParentMaintenanceGroupId", "Description", "IsSystemDefault", "IsActive", "CreatedDate").Writable("Code", "Name", "DisciplineId", "ParentMaintenanceGroupId", "Description", "IsActive", "CreatedDate"))
        .Add<Skill>("SkillId", e => e.Read("MaintenanceMasterData.View").Create("MaintenanceMasterData.Create").Edit("MaintenanceMasterData.Edit")
            .Readable("Name", "DisciplineId", "Description", "IsActive", "CreatedDate").Writable("Name", "DisciplineId", "Description", "IsActive", "CreatedDate"))
        .Add<Qualification>("QualificationId", e => e.Read("MaintenanceMasterData.View").Create("MaintenanceMasterData.Create").Edit("MaintenanceMasterData.Edit")
            .Readable("Name", "DisciplineId", "Description", "IsActive", "CreatedDate").Writable("Name", "DisciplineId", "Description", "IsActive", "CreatedDate"))
        // Spare parts (W2): the controller has no permission check beyond signing in
        .Add<SparePart>("SparePartId", e => e.Readable("PartNumber", "PartName", "Manufacturer", "Model", "Unit", "Description", "QuantityOnHand", "MinStockLevel", "ReorderPoint", "StorageLocation", "IsActive", "CreatedDate")
            .Writable("PartNumber", "PartName", "Manufacturer", "Model", "Unit", "Description", "QuantityOnHand", "MinStockLevel", "ReorderPoint", "StorageLocation", "IsActive", "CreatedDate"))
        .Add<Warehouse>("WarehouseId", e => e.Create("InventoryMasterData.Create").Edit("InventoryMasterData.Edit")
            .Readable("Code", "Name", "Description", "IsActive", "CreatedDate").Writable("Code", "Name", "Description", "IsActive", "CreatedDate"));
}
