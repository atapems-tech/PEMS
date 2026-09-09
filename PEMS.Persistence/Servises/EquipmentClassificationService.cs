using Microsoft.EntityFrameworkCore;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// طبق «PEMS Phase 3 — Equipment Classification Completion» بند ۴-۱۳.
///
/// همه‌ی قاعده‌ها با پروفایل مستقیم داده‌ی واقعی زنده ساخته شدند (نه فرضی) - از جمله کشف مهم
/// این فاز: EquipmentType='DEVICE'/'F&amp;G DEVICE' (که فاز ۲ آن‌ها را عمومی/غیرقابل‌طبقه‌بندی
/// می‌دانست) در واقع فقط ۱۶۳/۱۵ مقدار Name متمایز دارند - یعنی خودِ فیلد Name سیگنال طبقه‌بندی
/// تقریباً کاملی است (مثلاً Name="THERMOWELL" با ۱۳۱۸ رکورد واقعی).
///
/// سه لایه‌ی قطعیت (طبق بند ۶):
///   EXACT  - فقط EquipmentType به‌تنهایی به یک کلاس واحد اشاره دارد (میراث فاز ۲) - اعمال خودکار
///   HIGH   - ترکیب (EquipmentType + Name) دقیقاً یک کلاس مشخص را تعیین می‌کند - اعمال خودکار
///   MEDIUM - ترکیب (EquipmentType + Name) یک کلاس محتمل ولی نه صددرصد قطعی نشان می‌دهد - فقط
///            Suggestion (هرگز روی Equipment نوشته نمی‌شود)
/// مقادیر عمومی/مبهم بدون کلاس قابل‌دفاع (DEVICE به‌تنهایی، CABLE، PANEL، package، ...) عمداً
/// بدون هیچ قاعده‌ای می‌مانند - طبق بند ۳۱ هرگز صرفاً برای افزایش Coverage یک کلاس جعل نمی‌شود.
/// </summary>
public class EquipmentClassificationService : IEquipmentClassificationService
{
    private readonly PEMSDbContext _context;

    // ==================== EXACT: فقط EquipmentType (میراث فاز ۲) ====================
    private static readonly (string EquipmentTypeExact, string EquipmentClassCode, string Reason)[] ExactTypeRules =
    {
        ("pump", "PUMP", "مقدار متن دقیقاً برابر نام عمومی کلاس Pump است"),
        ("pummp", "PUMP", "نگارش اشتباه شناخته‌شده‌ی 'pump' در داده‌ی موجود"),
        ("compressor", "COMPRESSOR", "مقدار متن دقیقاً برابر نام کلاس Compressor است"),
        ("blower", "BLOWER", "مقدار متن دقیقاً برابر نام کلاس Blower است"),
        ("filter", "FILTER", "مقدار متن دقیقاً برابر نام کلاس Filter است"),
        ("reactor", "REACTOR", "مقدار متن دقیقاً برابر نام کلاس Reactor است"),
        ("valve", "VALVE", "مقدار متن دقیقاً برابر نام کلاس Valve است"),
        ("fan", "FAN", "مقدار متن دقیقاً برابر نام کلاس Fan است"),
        ("motor", "MOTOR", "مقدار متن دقیقاً برابر نام کلاس Electric Motor است"),
        ("vessel", "PRESSURE_VESSEL", "مترادف صنعتی استاندارد برای Pressure Vessel"),
        ("drum", "PRESSURE_VESSEL", "در صنعت نفت/گاز، Drum یک مخزن تحت فشار فرآیندی است"),
        ("exchanger", "HEAT_EXCHANGER", "مترادف صنعتی استاندارد برای Heat Exchanger"),
        ("air cooler", "HEAT_EXCHANGER", "Air Cooler یک مبدل حرارتی هوایی (Fin-Fan) است"),
        ("tank", "STORAGE_TANK", "مترادف صنعتی استاندارد برای Storage Tank"),
        ("tower", "COLUMN", "Tower مترادف صنعتی استاندارد برای Column (برج تقطیر) است"),
        ("mixer", "MIXER_AGITATOR", "مقدار متن دقیقاً برابر نام کلاس Mixer/Agitator است"),
        ("agitator", "MIXER_AGITATOR", "مقدار متن دقیقاً برابر نام کلاس Mixer/Agitator است"),
        ("conveyor", "CONVEYOR", "مقدار متن دقیقاً برابر نام کلاس Conveyor است"),
    };

    // ==================== HIGH: ترکیب (EquipmentType, Name) - طبق پروفایل فاز ۳ ====================
    private static readonly (string EquipmentTypeExact, string NameExact, string EquipmentClassCode, string Reason)[] HighTypeNameRules =
    {
        ("DEVICE", "THERMOWELL", "THERMOWELL", "نام دقیقاً 'THERMOWELL' است"),
        ("DEVICE", "PRESSURE GAUGE", "PRESSURE_GAUGE", "نام دقیقاً 'PRESSURE GAUGE' است"),
        ("DEVICE", "TEMP. GAUGE", "TEMPERATURE_GAUGE", "نام دقیقاً 'TEMP. GAUGE' است"),
        ("DEVICE", "RTD ELEMENT DUPLEX", "RTD_ELEMENT", "نام دقیقاً 'RTD ELEMENT DUPLEX' است"),
        ("DEVICE", "SOLENOID VALVE", "ON_OFF_VALVE", "شیر سولنوییدی از نظر عملکردی یک On/Off Valve است"),
        ("DEVICE", "VALVE CLOSED POSITION SWITCH", "LIMIT_SWITCH", "سوئیچ فیدبک وضعیت شیر - یک Limit Switch است"),
        ("DEVICE", "VALVE OPEN POSITION SWITCH", "LIMIT_SWITCH", "سوئیچ فیدبک وضعیت شیر - یک Limit Switch است"),
        ("DEVICE", "ACTUATING ON/OFF VALVE", "ON_OFF_VALVE", "نام دقیقاً یک On/Off Valve را توصیف می‌کند"),
        ("DEVICE", "TEMP. TRANSMITTER", "TEMPERATURE_TRANSMITTER", "نام دقیقاً یک Temperature Transmitter را توصیف می‌کند"),
        ("DEVICE", "TEMP. TRANSMITTER FieldBusFF", "TEMPERATURE_TRANSMITTER", "نوع پروتکل (FieldBus FF) نوع تجهیز را تغییر نمی‌دهد"),
        ("DEVICE", "PRESSURE TRANSMITTER", "PRESSURE_TRANSMITTER", "نام دقیقاً یک Pressure Transmitter را توصیف می‌کند"),
        ("DEVICE", "PRESSURE TRANSMITTER FieldBusFF", "PRESSURE_TRANSMITTER", "نوع پروتکل (FieldBus FF) نوع تجهیز را تغییر نمی‌دهد"),
        ("DEVICE", "FLOW RESTRICTION ORIFICE", "FLOW_ELEMENT", "نام دقیقاً یک Flow Element (اوریفیس) را توصیف می‌کند"),
        ("DEVICE", "FLOW ELEMENT - ORIFICE", "FLOW_ELEMENT", "نام دقیقاً یک Flow Element (اوریفیس) را توصیف می‌کند"),
        ("DEVICE", "GLOBE CONTROL VALVE", "CONTROL_VALVE_EQ", "شیر کنترل گلوب - یک Control Valve است"),
        ("DEVICE", "PRESSURE SAFETY VALVE", "PRESSURE_SAFETY_VALVE", "نام دقیقاً یک PSV را توصیف می‌کند"),
        ("DEVICE", "ROTAMETER", "ROTAMETER", "نام دقیقاً 'ROTAMETER' است"),
        ("DEVICE", "CAPACITIVE TYPE LEVEL SWITCH", "LEVEL_SWITCH", "سوئیچ سطح خازنی - یک Level Switch است"),
        ("DEVICE", "HAND SWITCH", "HAND_SWITCH", "نام دقیقاً 'HAND SWITCH' است"),
        ("DEVICE", "LEVEL SWITCH LOW", "LEVEL_SWITCH", "نام دقیقاً یک Level Switch را توصیف می‌کند"),
        ("DEVICE", "LEVEL SWITCH HIGH", "LEVEL_SWITCH", "نام دقیقاً یک Level Switch را توصیف می‌کند"),
        ("DEVICE", "LEVEL GAUGE", "LEVEL_GAUGE", "نام دقیقاً 'LEVEL GAUGE' است"),
        ("DEVICE", "PRESSURE REGULATING VALVE", "CONTROL_VALVE_EQ", "شیر تنظیم فشار - یک Control Valve است"),
        ("DEVICE", "DIFF PRESSURE XMTR", "PRESSURE_TRANSMITTER", "ترانسمیتر فشار افتراقی یک نوع Pressure Transmitter است"),
        ("DEVICE", "DIFF PRESSURE XMTR FieldBusFF", "PRESSURE_TRANSMITTER", "ترانسمیتر فشار افتراقی یک نوع Pressure Transmitter است"),
        ("DEVICE", "PRESSURE SWITCH HIGH", "PRESSURE_SWITCH", "نام دقیقاً یک Pressure Switch را توصیف می‌کند"),
        ("DEVICE", "MASS FLOW TRANSMITTER", "FLOW_TRANSMITTER", "ترانسمیتر دبی جرمی یک نوع Flow Transmitter است"),
        ("DEVICE", "RUPTURE DISC", "PRESSURE_SAFETY_VALVE", "دیسک پارگی یک وسیله‌ی تخلیه‌ی اضطراری فشار مشابه PSV است"),
        ("DEVICE", "DYNISCO PRESSURE TRANSMITTER", "PRESSURE_TRANSMITTER", "نام دقیقاً یک Pressure Transmitter را توصیف می‌کند (برند Dynisco)"),
        ("DEVICE", "ELECTRONIC LEVEL INDICATOR FieldBusFF", "LEVEL_TRANSMITTER", "نشانگر سطح الکترونیکی یک نوع Level Transmitter است"),
        ("DEVICE", "PRESSURE DIFFERENTIAL TRANSMITTER", "PRESSURE_TRANSMITTER", "ترانسمیتر فشار افتراقی یک نوع Pressure Transmitter است"),
        ("DEVICE", "FLOW TRANSMITTER", "FLOW_TRANSMITTER", "نام دقیقاً 'FLOW TRANSMITTER' است"),
        ("DEVICE", "TEMPERATURE SAFETY VALVE", "PRESSURE_SAFETY_VALVE", "شیر اطمینان حرارتی از خانواده‌ی شیرهای تخلیه‌ی اضطراری است"),
        ("DEVICE", "FLOW SWITCH LOW", "FLOW_SWITCH", "نام دقیقاً یک Flow Switch را توصیف می‌کند"),
        ("DEVICE", "ELECTRONIC LEVEL INDICATOR", "LEVEL_TRANSMITTER", "نشانگر سطح الکترونیکی یک نوع Level Transmitter است"),

        ("F&G DEVICE", "SMOKE DETECTOR", "FIRE_DETECTOR", "نام دقیقاً یک آشکارساز دود را توصیف می‌کند"),
        ("F&G DEVICE", "HYDROCARBON GAS DETECTOR", "GAS_DETECTOR", "نام دقیقاً یک آشکارساز گاز را توصیف می‌کند"),
        ("F&G DEVICE", "MANUAL CALL POINT", "MANUAL_CALL_POINT", "نام دقیقاً 'MANUAL CALL POINT' است"),
        ("F&G DEVICE", "TOXIC GAS DETECTOR", "GAS_DETECTOR", "نام دقیقاً یک آشکارساز گاز را توصیف می‌کند"),
        ("F&G DEVICE", "FLASHING LIGHT (FIRE)", "ALARM_NOTIFICATION_DEVICE", "دستگاه اعلام هشدار بصری آتش"),
        ("F&G DEVICE", "HOOTER (FIRE)", "ALARM_NOTIFICATION_DEVICE", "دستگاه اعلام هشدار صوتی آتش"),
        ("F&G DEVICE", "HOOTER (GAS)", "ALARM_NOTIFICATION_DEVICE", "دستگاه اعلام هشدار صوتی گاز"),
        ("F&G DEVICE", "FLASHING LIGHT (TOXIC GAS)", "ALARM_NOTIFICATION_DEVICE", "دستگاه اعلام هشدار بصری گاز سمی"),
        ("F&G DEVICE", "ANALYSER ELEMENT", "ANALYZER_EQ", "المان آنالایزر - یک Analyzer است"),
        ("F&G DEVICE", "LINEAR HEAT DETECTOR", "FIRE_DETECTOR", "آشکارساز حرارتی خطی یک نوع آشکارساز آتش است"),
        ("F&G DEVICE", "HAND SWITCH", "HAND_SWITCH", "نام دقیقاً 'HAND SWITCH' است"),
        ("F&G DEVICE", "SOLENOID VALVE", "ON_OFF_VALVE", "شیر سولنوییدی از نظر عملکردی یک On/Off Valve است"),
    };

    // ==================== MEDIUM: محتمل ولی نه صددرصد قطعی - فقط Suggestion ====================
    private static readonly (string EquipmentTypeExact, string NameExact, string EquipmentClassCode, string Reason)[] MediumTypeNameRules =
    {
        ("DEVICE", "CUMULATIVE ALARM", "FIRE_ALARM_PANEL", "احتمالاً بخشی از سامانه‌ی پنل هشدار است؛ بدون بررسی نقشه نمی‌توان قطعی گفت"),
        ("F&G DEVICE", "SWITCH HIGH", "GAS_DETECTOR", "در بافت F&G احتمالاً سوئیچ هشدار سطح‌بالای یک آشکارساز است؛ کلاس دقیق نامطمئن"),
        ("F&G DEVICE", "SWITCH LOW", "GAS_DETECTOR", "در بافت F&G احتمالاً سوئیچ هشدار سطح‌پایین یک آشکارساز است؛ کلاس دقیق نامطمئن"),
    };

    public EquipmentClassificationService(PEMSDbContext context)
    {
        _context = context;
    }

    // ============================================================================================
    // Backfill
    // ============================================================================================

    public async Task<BackfillResultDto> BackfillAsync()
    {
        var classesByCode = await _context.EquipmentClasses
            .ToDictionaryAsync(x => x.Code, x => x, StringComparer.OrdinalIgnoreCase);

        // طبق بند ۳۰/۱۲: فقط رکوردهایی که هنوز EquipmentClassId=Null دارند لمس می‌شوند - هرگز یک
        // انتساب موجود (دستی/OEM/Import/تاییدشده) بازنویسی نمی‌شود.
        var candidates = await _context.Equipments
            .Where(x => x.IsActive && x.EquipmentClassId == null)
            .Select(x => new { x.EquipmentId, x.EquipmentType, x.Name })
            .ToListAsync();

        var result = new BackfillResultDto();
        if (candidates.Count == 0) return result;

        // برای MEDIUM: از قبل چه Suggestion هایی (با هر Status ای) ساخته شده - طبق بند ۱۳
        // (Idempotency) هرگز یک تصمیم قبلی انسانی (Rejected) را با ساختن Suggestion تکراری دور نمی‌زند.
        var existingSuggestionKeys = (await _context.EquipmentClassificationSuggestions
                .Select(x => new { x.EquipmentId, x.SuggestedEquipmentClassId })
                .ToListAsync())
            .Select(x => (x.EquipmentId, x.SuggestedEquipmentClassId))
            .ToHashSet();

        var equipmentToUpdate = new List<(int EquipmentId, int ClassId, string Confidence)>();
        var newSuggestions = new List<EquipmentClassificationSuggestion>();

        foreach (var eq in candidates)
        {
            var type = (eq.EquipmentType ?? string.Empty).Trim();
            var name = (eq.Name ?? string.Empty).Trim();

            var exactRule = ExactTypeRules.FirstOrDefault(r => string.Equals(r.EquipmentTypeExact, type, StringComparison.OrdinalIgnoreCase));
            if (exactRule != default && classesByCode.TryGetValue(exactRule.EquipmentClassCode, out var exactClass))
            {
                equipmentToUpdate.Add((eq.EquipmentId, exactClass.EquipmentClassId, "EXACT"));
                continue;
            }

            var highRule = HighTypeNameRules.FirstOrDefault(r =>
                string.Equals(r.EquipmentTypeExact, type, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(r.NameExact, name, StringComparison.OrdinalIgnoreCase));
            if (highRule != default && classesByCode.TryGetValue(highRule.EquipmentClassCode, out var highClass))
            {
                equipmentToUpdate.Add((eq.EquipmentId, highClass.EquipmentClassId, "HIGH"));
                continue;
            }

            var mediumRule = MediumTypeNameRules.FirstOrDefault(r =>
                string.Equals(r.EquipmentTypeExact, type, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(r.NameExact, name, StringComparison.OrdinalIgnoreCase));
            if (mediumRule != default && classesByCode.TryGetValue(mediumRule.EquipmentClassCode, out var mediumClass))
            {
                if (existingSuggestionKeys.Contains((eq.EquipmentId, mediumClass.EquipmentClassId)))
                    continue; // قبلاً پیشنهاد شده (هر Status ای) - تکراری نمی‌سازیم

                newSuggestions.Add(new EquipmentClassificationSuggestion
                {
                    EquipmentId = eq.EquipmentId,
                    SuggestedEquipmentClassId = mediumClass.EquipmentClassId,
                    Confidence = "MEDIUM",
                    Source = "RULE",
                    Reason = mediumRule.Reason,
                    Status = "PENDING",
                    CreatedDate = DateTime.Now
                });
            }
        }

        if (equipmentToUpdate.Count > 0)
        {
            var idsToUpdate = equipmentToUpdate.Select(x => x.EquipmentId).ToHashSet();
            var entities = await _context.Equipments.Where(x => idsToUpdate.Contains(x.EquipmentId)).ToListAsync();
            var byId = entities.ToDictionary(x => x.EquipmentId);

            foreach (var (equipmentId, classId, confidence) in equipmentToUpdate)
            {
                var entity = byId[equipmentId];
                entity.EquipmentClassId = classId;
                entity.ClassificationConfidence = confidence;
                entity.ClassificationSource = "RULE";
            }

            result.AutoClassifiedCount = equipmentToUpdate.Count;
        }

        if (newSuggestions.Count > 0)
        {
            _context.EquipmentClassificationSuggestions.AddRange(newSuggestions);
            result.SuggestionsCreatedCount = newSuggestions.Count;
        }

        if (result.AutoClassifiedCount > 0 || result.SuggestionsCreatedCount > 0)
            await _context.SaveChangesAsync();

        return result;
    }

    // ============================================================================================
    // Dashboard (SQL-Aggregated - طبق بند ۲۵ هرگز کل جدول Equipment را به حافظه بارگذاری نمی‌کند)
    // ============================================================================================

    public async Task<ClassificationDashboardDto> GetDashboardAsync()
    {
        var dto = new ClassificationDashboardDto();

        dto.TotalActiveEquipment = await _context.Equipments.CountAsync(x => x.IsActive);
        dto.ClassifiedCount = await _context.Equipments.CountAsync(x => x.IsActive && x.EquipmentClassId != null);
        dto.UnclassifiedCount = dto.TotalActiveEquipment - dto.ClassifiedCount;
        dto.CoveragePercent = dto.TotalActiveEquipment == 0 ? 0 : Math.Round(dto.ClassifiedCount * 100.0 / dto.TotalActiveEquipment, 1);
        dto.PendingReviewCount = await _context.EquipmentClassificationSuggestions.CountAsync(x => x.Status == "PENDING");

        dto.ByConfidence = await _context.Equipments
            .Where(x => x.IsActive && x.EquipmentClassId != null)
            .GroupBy(x => x.ClassificationConfidence)
            .Select(g => new CountByKeyDto { Key = g.Key ?? "UNKNOWN", Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        dto.BySource = await _context.Equipments
            .Where(x => x.IsActive && x.EquipmentClassId != null)
            .GroupBy(x => x.ClassificationSource)
            .Select(g => new CountByKeyDto { Key = g.Key ?? "UNKNOWN", Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        dto.ByEquipmentClass = await _context.Equipments
            .Where(x => x.IsActive && x.EquipmentClassId != null)
            .GroupBy(x => x.EquipmentClassRef!.Name)
            .Select(g => new CountByKeyDto { Key = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(20)
            .ToListAsync();

        dto.PendingSuggestionsByConfidence = await _context.EquipmentClassificationSuggestions
            .Where(x => x.Status == "PENDING")
            .GroupBy(x => x.Confidence)
            .Select(g => new CountByKeyDto { Key = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        dto.UnclassifiedByDiscipline = await _context.Equipments
            .Where(x => x.IsActive && x.EquipmentClassId == null)
            .GroupBy(x => x.Discipline!.Name)
            .Select(g => new CountByKeyDto { Key = g.Key ?? "UNKNOWN", Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(20)
            .ToListAsync();

        dto.TopUnclassifiedEquipmentTypes = await _context.Equipments
            .Where(x => x.IsActive && x.EquipmentClassId == null)
            .GroupBy(x => x.EquipmentType)
            .Select(g => new CountByKeyDto { Key = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(50)
            .ToListAsync();

        dto.TopUnclassifiedEquipmentNames = await _context.Equipments
            .Where(x => x.IsActive && x.EquipmentClassId == null)
            .GroupBy(x => x.Name)
            .Select(g => new CountByKeyDto { Key = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(50)
            .ToListAsync();

        // طبق بند ۳۰: الگوی Tag (مثلاً پیشوند دوم در "80-PSH-601305" → "PSH") قابل استخراج در
        // SQL نیست (بدون STRING_SPLIT با ترتیب تضمین‌شده) - فقط ستون TagNumber (رشته‌ی باریک، نه
        // کل Entity) واکشی و الگو در حافظه استخراج می‌شود؛ این یک استثنای محدود و مستند به بند ۲۵
        // است (نه بارگذاری کامل جدول Equipment، فقط یک ستون برای گزارش تجمیعی).
        var unclassifiedTags = await _context.Equipments
            .Where(x => x.IsActive && x.EquipmentClassId == null)
            .Select(x => x.TagNumber)
            .ToListAsync();

        dto.TopUnclassifiedTagPatterns = unclassifiedTags
            .Select(ExtractTagPattern)
            .Where(p => !string.IsNullOrEmpty(p))
            .GroupBy(p => p)
            .Select(g => new CountByKeyDto { Key = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(50)
            .ToList();

        return dto;
    }

    /// <summary>
    /// طبق قرارداد رایج تگ‌گذاری ISA-5.1 در این پروژه (مثلاً "80-PSH-601305" → ناحیه-نوع-شماره):
    /// بخش دوم (جدا شده با "-") معمولاً کد نوع ابزار است. اگر تگ این الگو را نداشته باشد، خودِ
    /// تگ کامل به‌عنوان یک الگوی «بدون‌قاعده» برگردانده می‌شود.
    /// </summary>
    private static string ExtractTagPattern(string tagNumber)
    {
        if (string.IsNullOrWhiteSpace(tagNumber)) return string.Empty;

        var parts = tagNumber.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && parts[1].Any(char.IsLetter))
            return parts[1].ToUpperInvariant();

        return "(بدون الگوی مشخص)";
    }

    // ============================================================================================
    // Review Queue
    // ============================================================================================

    public async Task<ReviewQueuePageDto> GetReviewQueueAsync(int page, int pageSize, string statusFilter = "PENDING")
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 50 : pageSize;

        var query = _context.EquipmentClassificationSuggestions
            .AsNoTracking()
            .Where(x => x.Status == statusFilter);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(x => x.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ReviewQueueItemDto
            {
                SuggestionId = x.EquipmentClassificationSuggestionId,
                EquipmentId = x.EquipmentId,
                TagNumber = x.Equipment!.TagNumber,
                EquipmentName = x.Equipment.Name,
                EquipmentType = x.Equipment.EquipmentType,
                DisciplineName = x.Equipment.Discipline != null ? x.Equipment.Discipline.Name : null,
                SuggestedEquipmentClassId = x.SuggestedEquipmentClassId,
                SuggestedEquipmentClassName = x.SuggestedEquipmentClass!.Name,
                Confidence = x.Confidence,
                Source = x.Source,
                Reason = x.Reason,
                Status = x.Status,
                ReviewedBy = x.ReviewedBy,
                ReviewedAt = x.ReviewedAt,
                ReviewComment = x.ReviewComment,
                CreatedDate = x.CreatedDate
            })
            .ToListAsync();

        return new ReviewQueuePageDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    // ============================================================================================
    // طبق بند ۱۶: صفحه‌ی Equipment Details فقط پیشنهاد PENDING خودِ همان یک تجهیز را نیاز دارد -
    // نه کل صف
    // ============================================================================================

    public async Task<ReviewQueueItemDto?> GetPendingSuggestionForEquipmentAsync(int equipmentId)
    {
        return await _context.EquipmentClassificationSuggestions
            .AsNoTracking()
            .Where(x => x.EquipmentId == equipmentId && x.Status == "PENDING")
            .Select(x => new ReviewQueueItemDto
            {
                SuggestionId = x.EquipmentClassificationSuggestionId,
                EquipmentId = x.EquipmentId,
                SuggestedEquipmentClassId = x.SuggestedEquipmentClassId,
                SuggestedEquipmentClassName = x.SuggestedEquipmentClass!.Name,
                Confidence = x.Confidence,
                Source = x.Source,
                Reason = x.Reason,
                Status = x.Status,
                CreatedDate = x.CreatedDate
            })
            .FirstOrDefaultAsync();
    }

    // ============================================================================================
    // Accept / Reject
    // ============================================================================================

    public async Task<bool> AcceptSuggestionAsync(int suggestionId, string reviewedBy, string? comment)
    {
        var suggestion = await _context.EquipmentClassificationSuggestions
            .FirstOrDefaultAsync(x => x.EquipmentClassificationSuggestionId == suggestionId && x.Status == "PENDING");
        if (suggestion == null) return false;

        var equipment = await _context.Equipments.FirstOrDefaultAsync(x => x.EquipmentId == suggestion.EquipmentId);
        if (equipment == null) return false;

        // طبق بند ۳۰: حتی در تایید، اگر بین ساخته‌شدن Suggestion و الان یک منبع دیگر (مثلاً
        // Manual) قبلاً طبقه‌بندی کرده باشد، هرگز بازنویسی نمی‌شود.
        if (equipment.EquipmentClassId == null)
        {
            equipment.EquipmentClassId = suggestion.SuggestedEquipmentClassId;
            equipment.ClassificationConfidence = suggestion.Confidence;
            equipment.ClassificationSource = suggestion.Source == "AI" ? "AI" : "MANUAL";
        }

        suggestion.Status = "ACCEPTED";
        suggestion.ReviewedBy = reviewedBy;
        suggestion.ReviewedAt = DateTime.Now;
        suggestion.ReviewComment = comment;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RejectSuggestionAsync(int suggestionId, string reviewedBy, string? comment)
    {
        var suggestion = await _context.EquipmentClassificationSuggestions
            .FirstOrDefaultAsync(x => x.EquipmentClassificationSuggestionId == suggestionId && x.Status == "PENDING");
        if (suggestion == null) return false;

        suggestion.Status = "REJECTED";
        suggestion.ReviewedBy = reviewedBy;
        suggestion.ReviewedAt = DateTime.Now;
        suggestion.ReviewComment = comment;

        await _context.SaveChangesAsync();
        return true;
    }
}
