namespace PEMS.Web.Helpers;

/// <summary>
/// نگاشت (Module, BusinessObjectType) موتور گردش‌کار به صفحه‌ی جزئیات رکورد. فقط نگاشت‌هایی که مقصدشان در کد بررسی شده آمده است؛
/// برای هر ترکیب ناشناخته null برمی‌گردد (UI بدون لینک نمایش می‌دهد، لینک حدسی ساخته نمی‌شود). مجوز دسترسی به صفحه‌ی مقصد همچنان
/// با خود همان کنترلر اعمال می‌شود؛ این کلاس مجوزی نمی‌دهد.
/// </summary>
public static class WorkflowTaskLinks
{
    public static string? TryGetPath(string? module, string? businessObjectType, int id)
    {
        if (id <= 0 || string.IsNullOrWhiteSpace(businessObjectType)) return null;

        return businessObjectType switch
        {
            "Permit" => $"/Permit/Details/{id}",
            "MOC" => $"/MOC/Details/{id}",
            "TechnicalQuery" => $"/TechnicalQuery/Details/{id}",
            "VDRItem" => $"/VDR/Details/{id}",
            "Document" => $"/EngineeringDocuments/Details/{id}",
            "PartCatalogueRequest" => $"/Warehouse/PartCatalogueRequest/Details/{id}",
            "NonConformanceReport" => $"/QC/NcrDetails/{id}",
            "Contract" => $"/Contract/Details/{id}",
            "IrnReceiptDisposition" => $"/Warehouse/IrnReceipt/Details/{id}",
            "WorkOrder" or "WorkOrderLifecycle" or "WorkOrderMultiDisciplineReview" => $"/WorkOrder/Details/{id}",
            "WorkRequest" => $"/WorkRequest/Details/{id}",
            "WorkOrderMaterialIntake" => $"/Warehouse/WarehouseIntake/Details/{id}",   // SCRUM-472: id = work order
            _ => null
        };
    }
}
