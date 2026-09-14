namespace PEMS.Web.Models;

// طبق «PEMS Phase 2C-3 — Safe Extraction of Shared Stage Diagram»: سه پیاده‌سازی مستقل
// (_WorkflowStagesDiagram برای EDMS، _VdrWorkflowStagesDiagram برای VDR) عملاً یک نمودار
// گرافیکی یکسان (دایره‌ها + رابط‌های بین‌شان، با وضعیت Done/Current/Pending/Rejected) را با
// CSS تقریباً بایت‌به‌بایت یکسان رندر می‌کردند. تصمیم این‌که کدام مرحله Done/Current/
// Pending/Rejected است، همیشه در همان View مصرف‌کننده (بر اساس Status/CurrentStageName
// خودش) محاسبه می‌شد - این مدل عمداً همان تقسیم مسئولیت را حفظ می‌کند: Partial فقط رندر
// می‌کند، هرگز State را خودش محاسبه نمی‌کند.
public enum StageItemState
{
    Pending,
    Current,
    Done,
    Rejected
}

public class StageDiagramItem
{
    public string Label { get; set; } = "";
    public string IconCssClass { get; set; } = "";

    // طبق ممیزی فاز ۲C-۲: _WorkOrderStagesDiagram اصلاً فیلد نقش ندارد - این‌جا Nullable
    // است تا آن مصرف‌کننده (در فاز‌های بعدی، در صورت تأیید) بدون رندر یک خط خالی از همین
    // مدل استفاده کند.
    public string? RoleLabel { get; set; }

    public StageItemState State { get; set; }
}

public class StageDiagramViewModel
{
    public List<StageDiagramItem> Stages { get; set; } = new();
}
