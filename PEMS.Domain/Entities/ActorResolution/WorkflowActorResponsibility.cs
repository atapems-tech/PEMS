using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// «مسئولیت» کسب‌وکاری یک مرحله‌ی گردش‌کار (مثلاً MaintenanceSupervisor, CommodityGroupReview). این Identity Role نیست:
/// Role = مجوز؛ Position = جایگاه سازمانی؛ Scope = محدوده‌ی سازمانی؛ Commodity Group = طبقه‌بندی کالا. هر مرحله‌ی گردش‌کار با
/// <c>WorkflowStage.ResponsibilityKey</c> به یکی از این ردیف‌ها اشاره می‌کند و Enterprise Actor Resolver افراد واجد شرایط
/// را (هنگام ورود به مرحله) از روی همین پیکربندی و داده‌ی سازمانی حل می‌کند. هیچ UserId ثابتی در تعریف گردش‌کار نیست.
/// </summary>
public class WorkflowActorResponsibility : BaseEntity
{
    public int WorkflowActorResponsibilityId { get; set; }

    /// <summary>کلید یکتا؛ برابر WorkflowStage.ResponsibilityKey.</summary>
    public string ResponsibilityKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
    public string? DisplayNameFa { get; set; }
    public string? Description { get; set; }

    /// <summary>
    /// منبع کاندیدها: AssignmentPool (فهرست صریح Legacy کاتالوگ قطعه)، PositionScope (Position + نقش اختیاری)،
    /// RequesterManager (زنجیره‌ی مدیر درخواست‌دهنده؛ سطح در ManagerLevel)، CommodityPosition (نگاشت Commodity Group به Position/کارمند).
    /// </summary>
    public string Kind { get; set; } = ActorResponsibilityKinds.PositionScope;

    /// <summary>مجوز لازم برای بازیگر (ماژول.عمل) - مثلاً PartCatalogue.Review. خالی = بررسی مجوز انجام نمی‌شود.</summary>
    public string? RequiredModule { get; set; }
    public string? RequiredAction { get; set; }

    /// <summary>نقش Identity اختیاری که کارمند (از طریق حساب کاربری) باید داشته باشد؛ AND با Position.</summary>
    public string? RoleName { get; set; }

    /// <summary>محدوده‌ی سازمانی: None | Department | Discipline | MaintenanceGroup | Team. ScopeValue فهرست Idها با کاما.</summary>
    public string ScopeType { get; set; } = ActorResponsibilityScopes.None;
    public string? ScopeValue { get; set; }

    /// <summary>فقط برای RequesterManager: ۱ = مدیر مستقیم، ۲ = مدیر مافوق.</summary>
    public int? ManagerLevel { get; set; }

    /// <summary>Queue = همه‌ی واجدین می‌توانند اقدام کنند. SingleActor = یک نفر به‌صورت قطعی انتخاب می‌شود.</summary>
    public string SelectionStrategy { get; set; } = ActorSelectionStrategies.Queue;

    public bool IsActive { get; set; } = true;

    public ICollection<WorkflowActorResponsibilityPosition> Positions { get; set; } = new List<WorkflowActorResponsibilityPosition>();
}

/// <summary>Position های مجاز یک مسئولیت (چند سمت می‌توانند یک مسئولیت را داشته باشند). Priority کوچک‌تر = اولویت بالاتر در SingleActor.</summary>
public class WorkflowActorResponsibilityPosition
{
    public int WorkflowActorResponsibilityPositionId { get; set; }
    public int WorkflowActorResponsibilityId { get; set; }
    public WorkflowActorResponsibility? Responsibility { get; set; }
    public int PositionId { get; set; }
    public Position? Position { get; set; }
    public int Priority { get; set; }
}

/// <summary>
/// نگاشت Commodity Group به مسئول بازبینی (ترجیحاً Position، با امکان Override کارمند مشخص). فقط برای مسئولیت‌های Kind=CommodityPosition.
/// ValidFrom/ValidTo و IsActive قابل‌ممیزی‌اند؛ Override کارمند هرگز مجوز/SoD/Scope را دور نمی‌زند (همان فیلترهای Resolver اعمال می‌شود).
/// </summary>
public class CommodityGroupResponsible : BaseEntity
{
    public int CommodityGroupResponsibleId { get; set; }

    public string ResponsibilityKey { get; set; } = string.Empty;

    public int CommodityGroupId { get; set; }
    public PartCatalogueCommodityGroup? CommodityGroup { get; set; }

    public int? PositionId { get; set; }
    public Position? Position { get; set; }

    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;
}

public static class ActorResponsibilityKinds
{
    public const string AssignmentPool = "AssignmentPool";
    public const string PositionScope = "PositionScope";
    public const string RequesterManager = "RequesterManager";
    public const string CommodityPosition = "CommodityPosition";

    public static readonly string[] All = { AssignmentPool, PositionScope, RequesterManager, CommodityPosition };
}

public static class ActorResponsibilityScopes
{
    public const string None = "None";
    public const string Department = "Department";
    public const string Discipline = "Discipline";
    public const string MaintenanceGroup = "MaintenanceGroup";
    public const string Team = "Team";

    // Warehouse عمداً نیست: در PEMS ارتباطی بین Employee و Warehouse مدل نشده (ممیزی)؛ بدون داده‌ی واقعی Scope جعلی ساخته نمی‌شود.
    public static readonly string[] All = { None, Department, Discipline, MaintenanceGroup, Team };
}

public static class ActorSelectionStrategies
{
    public const string Queue = "Queue";
    public const string SingleActor = "SingleActor";
}
