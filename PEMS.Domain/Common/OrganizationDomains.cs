namespace PEMS.Domain.Common;

/// <summary>
/// طبق Phase 3A Governance: چهار دامنه‌ی سازمانی جدا. «Mechanical» در Engineering و «Mechanical» در Maintenance دو هویت متفاوت‌اند؛
/// Discipline تنها برچسب تخصص است و هویت کامل = (OrganizationDomain, Discipline). دامنه از سلسله‌مراتب Department
/// (نزدیک‌ترین Department خودش یا والدش که OrganizationDomain دارد) به Employee می‌رسد - بدون جدول سازمان موازی.
/// </summary>
public static class OrganizationDomains
{
    public const string Engineering = "Engineering";
    public const string Maintenance = "Maintenance";
    public const string Operations = "Operations";
    public const string Warehouse = "Warehouse";

    public static readonly string[] All = { Engineering, Maintenance, Operations, Warehouse };

    public static bool IsValid(string? value) => value != null && All.Contains(value, StringComparer.Ordinal);
}
