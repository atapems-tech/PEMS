using System.ComponentModel.DataAnnotations;

namespace PEMS.Domain.Entities;

/// <summary>
/// جدول Routing داده‌محور (طبق قانون "Workflow Engine باید Configurable باشد، نه Hardcode"):
/// هر دسته‌بندی سند AI به کدام ماژول کسب‌وکار هدایت شود. مدیر سیستم می‌تواند این جدول را
/// از UI تغییر دهد بدون نیاز به Deploy کد جدید.
/// </summary>
public class RoutingRule
{
    [Key]
    public int RoutingRuleId { get; set; }

    // Engineering Drawing, Vendor Datasheet, Material Requisition, ...
    public string DocumentCategory { get; set; } = string.Empty;

    // MDR, VDR, MR, MRQ, Contract, CMMS, Unrouted
    public string TargetModule { get; set; } = string.Empty;

    // آیا این دسته می‌تواند با اطمینان بالا خودکار Route شود یا همیشه نیاز به تایید انسانی دارد
    public bool AllowAutoRouting { get; set; } = true;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
