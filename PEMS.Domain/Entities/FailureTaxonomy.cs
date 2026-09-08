using PEMS.Domain.Common;

namespace PEMS.Domain.Entities;

/// <summary>
/// طبق «PEMS Phase 2 — Equipment Classification + ISO 14224 Failure Taxonomy» بند ۱۱-۱۷: تاکسونومی
/// مرجع خرابی طبق مفاهیم ISO 14224 (Failure Mode/Cause/Mechanism/Effect/Consequence) - جدا از
/// FailureRecord.FailureMode/FailureCause (رشته‌ی آزاد امروز، دست‌نخورده برای Backward
/// Compatibility) و WorkOrder.FailureModeCode/FailureCauseCode (همچنین دست‌نخورده). این پنج
/// Entity فقط زیرساخت مرجع/AI-Ready هستند؛ رکوردهای تاریخی همچنان معتبرند.
/// طبق بند ۱۲: رابطه‌ی Mode↔Cause و Cause↔Mechanism واقعاً چند-به-چند است (یک علت می‌تواند
/// چند حالت خرابی ایجاد کند و برعکس) - Join Table های پایین‌تر همین را مدل می‌کنند؛ Mode→Effect
/// و Mode→Consequence برای سادگی (طبق بند ۴۴: پرهیز از Over-engineering) تک-مقداری نگه داشته
/// شدند - قابل گسترش به چند-به-چند در آینده اگر داده‌ی واقعی نیاز آن را ثابت کند.
/// </summary>
public class FailureMode : BaseEntity
{
    public int FailureModeId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? NameFa { get; set; }

    public string? Description { get; set; }

    // طبق بند ۱۳: این حالت خرابی معمولاً برای کدام Equipment Class معنادار است (اختیاری -
    // برخی حالت‌ها عمومی هستند و به همه‌ی کلاس‌ها اعمال می‌شوند)
    public int? EquipmentClassId { get; set; }
    public EquipmentClass? EquipmentClass { get; set; }

    public int? FailureEffectId { get; set; }
    public FailureEffect? FailureEffect { get; set; }

    public int? FailureConsequenceId { get; set; }
    public FailureConsequence? FailureConsequence { get; set; }

    public bool IsSystemDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<FailureModeCause> Causes { get; set; } = new List<FailureModeCause>();
}

public class FailureCause : BaseEntity
{
    public int FailureCauseId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? NameFa { get; set; }

    public string? Description { get; set; }

    public bool IsSystemDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<FailureModeCause> Modes { get; set; } = new List<FailureModeCause>();
    public ICollection<FailureCauseMechanism> Mechanisms { get; set; } = new List<FailureCauseMechanism>();
}

public class FailureMechanism : BaseEntity
{
    public int FailureMechanismId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? NameFa { get; set; }

    public string? Description { get; set; }

    public bool IsSystemDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<FailureCauseMechanism> Causes { get; set; } = new List<FailureCauseMechanism>();
}

public class FailureEffect : BaseEntity
{
    public int FailureEffectId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? NameFa { get; set; }

    public string? Description { get; set; }

    public bool IsSystemDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}

public class FailureConsequence : BaseEntity
{
    public int FailureConsequenceId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? NameFa { get; set; }

    public string? Description { get; set; }

    // SAFETY, ENVIRONMENT, PRODUCTION, QUALITY, COST, NONE
    public string Category { get; set; } = string.Empty;

    public bool IsSystemDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}

/// <summary>طبق بند ۱۲: رابطه‌ی چند-به-چند Failure Mode ↔ Failure Cause.</summary>
public class FailureModeCause
{
    public int FailureModeId { get; set; }
    public FailureMode? FailureMode { get; set; }

    public int FailureCauseId { get; set; }
    public FailureCause? FailureCause { get; set; }
}

/// <summary>طبق بند ۱۲: رابطه‌ی چند-به-چند Failure Cause ↔ Failure Mechanism.</summary>
public class FailureCauseMechanism
{
    public int FailureCauseId { get; set; }
    public FailureCause? FailureCause { get; set; }

    public int FailureMechanismId { get; set; }
    public FailureMechanism? FailureMechanism { get; set; }
}
