# 08-addendum Domain model addendum — Stage 1 supplementary entities (2026-11-18)

**Status:** APPROVED DESIGN ADDENDUM  
**Governing Decisions:** OD-24 (ProcessFormulaOperation, EBR), OD-25 (ISO 22400 Calendar), OD-26 (ATP & Time Fences), OD-27 (ProductFamily), OD-28 (Shared Calendar & Turnaround Events), OD-29 (LotSizingPolicy)  
**Parent Document:** `08-domain-model-specification.md`

---

## 1. موجودیت‌های تکمیلی ISA-88 (عملیات فرآیندی و لاگ الکترونیکی سوابق دسته)

### 1.1 موجودیت `ProcessFormulaOperation`
توالی مراحل استاندارد فرمولاسیون فرایند بر اساس بند ۴ استاندارد ISA-88.

```csharp
namespace PEMS.Domain.Entities;

public class ProcessFormulaOperation : BaseEntity
{
    public int ProcessFormulaOperationId { get; set; }
    
    public int ProcessFormulaId { get; set; }
    public ProcessFormula? ProcessFormula { get; set; }
    
    public int OperationSequence { get; set; } // e.g. 10, 20, 30...
    public string OperationName { get; set; } = string.Empty; // Charge, Heat, React, Cool, Drain, Package
    
    public decimal? MaxDurationMinutes { get; set; }
    public string? CriticalParametersJson { get; set; } // {"TempRangeC":[80,95], "PressureBarMax":3.5, "AgitationRpm":120}
    public string? OperatingInstructions { get; set; }
}
```

### 1.2 موجودیت `ProductionRunOperationLog` (Electronic Batch Record - EBR)
سوابق زمان‌بندی‌شده اجرای مراحل فرآیند برای هر دسته تولید طبق الزامات ردیابی ISA-88 و FDA 21 CFR Part 11.

```csharp
namespace PEMS.Domain.Entities;

public class ProductionRunOperationLog : BaseEntity
{
    public int ProductionRunOperationLogId { get; set; }
    
    public int ProductionRunId { get; set; }
    public ProductionRun? ProductionRun { get; set; }
    
    public int ProcessFormulaOperationId { get; set; }
    public ProcessFormulaOperation? ProcessFormulaOperation { get; set; }
    
    public DateTime ActualStartUtc { get; set; }
    public DateTime? ActualEndUtc { get; set; }
    
    public string Status { get; set; } = "Running"; // Running, Completed, Skipped, Aborted
    
    public int? OperatorEmployeeId { get; set; }
    public Employee? OperatorEmployee { get; set; }
    
    public string? ActualParametersJson { get; set; } // {"FinalTempC": 88.5, "PeakPressureBar": 3.1}
    public string? DeviationNotes { get; set; }
}
```

---

## 2. موجودیت‌های مدل زمانی تقویم کارخانه (ISO 22400-2)

### 2.1 موجودیت `ProductionCalendar`
تعریف‌کننده افق زمانی و ظرفیت اشتغال پایه‌ای برای مراکز کاری یا کل سایت.

```csharp
namespace PEMS.Domain.Entities;

public class ProductionCalendar : BaseEntity
{
    public int ProductionCalendarId { get; set; }
    public string CalendarCode { get; set; } = string.Empty; // unique, e.g. "CAL-MAIN-2027"
    public string Title { get; set; } = string.Empty;
    
    public int? ProductionWorkCenterId { get; set; }
    public ProductionWorkCenter? ProductionWorkCenter { get; set; }
    
    public DateTime ValidFromUtc { get; set; }
    public DateTime ValidToUtc { get; set; }
    public bool IsActive { get; set; } = true;
    
    public ICollection<CalendarShift> Shifts { get; set; } = new List<CalendarShift>();
    public ICollection<CalendarEvent> Events { get; set; } = new List<CalendarEvent>();
}
```

### 2.2 موجودیت `CalendarShift`
الگوی تکرارشونده شیفت‌های کاری برای محاسبه دقیق Planned Operation Time.

```csharp
namespace PEMS.Domain.Entities;

public class CalendarShift : BaseEntity
{
    public int CalendarShiftId { get; set; }
    
    public int ProductionCalendarId { get; set; }
    public ProductionCalendar? ProductionCalendar { get; set; }
    
    public string ShiftName { get; set; } = string.Empty; // Morning, Evening, Night
    public string WorkDaysJson { get; set; } = "[]"; // JSON array of DayOfWeek: [1,2,3,4,5]
    
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public decimal PlannedBusyHours { get; set; } // Duration minus scheduled statutory breaks
}
```

### 2.3 موجودیت `CalendarEvent`
مدیریت توقفات و رویدادهای تقویمی طبق رده‌بندی استاندارد ISO 22400.

```csharp
namespace PEMS.Domain.Entities;

public class CalendarEvent : BaseEntity
{
    public int CalendarEventId { get; set; }
    
    public int ProductionCalendarId { get; set; }
    public ProductionCalendar? ProductionCalendar { get; set; }
    
    public string EventType { get; set; } = "PlannedMaintenance"; 
    // PlannedMaintenance (reduces PBT / PDOT)
    // Turnaround (reduces PBT / PDOT)
    // Setup (inside PBT / reduces APT)
    // Cleaning (inside PBT / reduces APT)
    // UnplannedBreakdown (inside PBT / reduces APT)
    // Holiday (reduces POT)
    
    public string Title { get; set; } = string.Empty;
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    
    public int? ProductionWorkCenterId { get; set; }
    public ProductionWorkCenter? ProductionWorkCenter { get; set; }
    
    public int? MaintenanceWorkOrderId { get; set; } // Integration point with CMMS (WorkOrder)
    public DateTime? ScopeFreezeDate { get; set; } // Scope Freeze Date for Turnaround events
    
    public string? Description { get; set; }
}
```

---

## 3. موجودیت خانواده محصول (Product Family - ISA-95 Level 4)

### 3.1 موجودیت `ProductFamily` و `ProductFamilyMember`
برای گروه‌بندی اقلام در برنامه‌ریزی کلان تولید (APP).

```csharp
namespace PEMS.Domain.Entities;

public class ProductFamily : BaseEntity
{
    public int ProductFamilyId { get; set; }
    public string FamilyCode { get; set; } = string.Empty; // e.g. "FAM-POLYMER-HDPE"
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    
    public ICollection<ProductFamilyMember> Members { get; set; } = new List<ProductFamilyMember>();
}

public class ProductFamilyMember : BaseEntity
{
    public int ProductFamilyMemberId { get; set; }
    
    public int ProductFamilyId { get; set; }
    public ProductFamily? ProductFamily { get; set; }
    
    public int MaterialItemId { get; set; }
    public MaterialItem? MaterialItem { get; set; }
    
    public decimal HistoricalMixPercent { get; set; } // Disaggregation percentage to MPS (Sum=100)
}
```

---

## 4. خط‌مشی تعیین اندازه انباشته (Lot-Sizing Policy - APICS ASCM)

### 4.1 موجودیت `LotSizingPolicy`
کنترل رفتار محاسباتی موتور MRP برای هر قلم کالا.

```csharp
namespace PEMS.Domain.Entities;

public class LotSizingPolicy : BaseEntity
{
    public int LotSizingPolicyId { get; set; }
    
    public int MaterialItemId { get; set; } // Unique 1-to-1 mapping
    public MaterialItem? MaterialItem { get; set; }
    
    public string Method { get; set; } = "LotForLot"; // LotForLot, FixedOrderQuantity, EconomicOrderQuantity
    
    public decimal? FixedOrderQuantity { get; set; } // Required if Method = FixedOrderQuantity
    public decimal? MinimumOrderQuantity { get; set; } // Floor for all batch releases
    public decimal? MaximumOrderQuantity { get; set; } // Safety ceiling per run
    
    public decimal? OrderingCostPerSetup { get; set; } // Setup/PO Cost (S in EOQ)
    public decimal? AnnualHoldingCostPercent { get; set; } // Carrying Cost % (H in EOQ)
    
    public int? LeadTimeDays { get; set; }
}
```

---

## 5. پیام‌های هشدار و استثنای MRP (APICS ASCM)

### 5.1 موجودیت `MrpExceptionMessage`
ارائه بازخورد تعاملی به برنامه‌ریز بدون اعمال مستقیم تغییرات بدون تایید.

```csharp
namespace PEMS.Domain.Entities;

public class MrpExceptionMessage : BaseEntity
{
    public int MrpExceptionMessageId { get; set; }
    
    public int PlanVersionId { get; set; }
    public PlanVersion? PlanVersion { get; set; }
    
    public int MaterialItemId { get; set; }
    public MaterialItem? MaterialItem { get; set; }
    
    public string ExceptionType { get; set; } = "Expedite"; // Expedite, Defer, Cancel, CapacityOverload
    public string MessageText { get; set; } = string.Empty;
    
    public DateTime SuggestedActionDate { get; set; }
    public string Status { get; set; } = "Open"; // Open, Acknowledged, Resolved, Dismissed
    
    public int? ResolvedByEmployeeId { get; set; }
    public Employee? ResolvedByEmployee { get; set; }
    public DateTime? ResolvedDate { get; set; }
}
```

---

## 6. گسترش موجودیت‌های برنامه‌ریزی قبلی (PlanVersion و PlanItem)

### افزوده‌ها به `PlanVersion`:
* `public int DemandTimeFenceDays { get; set; } = 14;` (افق تثبیت‌شده و منجمد)
* `public int PlanningTimeFenceDays { get; set; } = 45;` (افق بازنگری سرپرست)

### افزوده‌ها به `PlanItem`:
* `public int? ProductFamilyId { get; set; }` (برای ردیف‌های برنامه‌ریزی کلان APP)
* `public ProductFamily? ProductFamily { get; set; }`
* `public decimal AvailableToPromise { get; set; }` (مقدار آزاد برای تعهد به سفارشات جدید)
* `public decimal ProjectedAvailableBalance { get; set; }` (موجودی پیش‌بینی‌شده پایان دوره)
