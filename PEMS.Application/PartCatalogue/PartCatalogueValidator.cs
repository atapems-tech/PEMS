using PEMS.Application.DTOs;

namespace PEMS.Application.PartCatalogue;

/// <summary>
/// اعتبارسنجی خالص درخواست. دو حالت: Save (نرم‌سازی و قواعد قالب؛ پیش‌نویس ناقص مجاز) و Submit (همه‌ی فیلدهای * الزامی).
/// پیام‌ها کلید Localization هستند (Pc_Err_* خطای مسدودکننده / Pc_Warn_* هشدار نرم).
/// </summary>
public static class PartCatalogueValidator
{
    public static PcResult Validate(PartCatalogueFormDto f, PartCatalogueValidationContext ctx, bool forSubmit)
    {
        var r = new PcResult();
        var isChange = string.Equals(f.RequestType, "Change", StringComparison.OrdinalIgnoreCase);

        void Err(string field, string key, string? arg = null) => r.Errors.Add(new PcMessage(field, key, arg));
        void Warn(string field, string key, string? arg = null) => r.Warnings.Add(new PcMessage(field, key, arg));
        void Req(bool ok, string field) { if (!ok) Err(field, "Pc_Err_Required"); }

        // ---------- قواعد قالب (در Save هم اعمال می‌شود) ----------
        foreach (var (field, value) in new (string, decimal?)[]
        {
            (nameof(f.MinimumStock), f.MinimumStock), (nameof(f.MaxConsumptionRate), f.MaxConsumptionRate),
            (nameof(f.FirstPurchaseQuantity), f.FirstPurchaseQuantity), (nameof(f.IssuedQty), f.IssuedQty),
            (nameof(f.PartPrice), f.PartPrice)
        })
            if (value.HasValue && value.Value < 0) Err(field, "Pc_Err_Negative");

        if (f.PartPrice.HasValue && f.PartPrice.Value > 0 && !f.CurrencyId.HasValue)
            Err(nameof(f.CurrencyId), "Pc_Err_PriceNeedsCurrency");

        // Low Value Asset: کالای مصرفی کم‌ارزش - تا آستانه‌ی قابل‌تنظیم (فقط وقتی ارز ریال است قابل‌مقایسه)
        if (f.AssetType == "LowValueAsset" && f.PartPrice.HasValue)
        {
            if (string.Equals(ctx.CurrencyCode, PartCatalogueRules.IrrCurrencyCode, StringComparison.OrdinalIgnoreCase))
            {
                if (f.PartPrice.Value > ctx.LowValueThresholdIrr)
                    Err(nameof(f.AssetType), "Pc_Err_LowValueThreshold", ctx.LowValueThresholdIrr.ToString("N0"));
            }
            else
                Warn(nameof(f.AssetType), "Pc_Warn_LowValueNotComparable");
        }

        // Manufacturer
        if (f.IsNewManufacturer)
        {
            var name = PartCatalogueRules.NormalizeNewManufacturerName(f.NewManufacturerName);
            if (name.Length == 0) Err(nameof(f.NewManufacturerName), "Pc_Err_NewManufacturerName");
            else if (!PartCatalogueRules.IsLatinManufacturerName(name)) Err(nameof(f.NewManufacturerName), "Pc_Err_NewManufacturerLatin");
            else
            {
                var (exact, similar) = PartCatalogueRules.FindManufacturerMatches(name, ctx.ExistingManufacturers);
                if (exact.Count > 0) Err(nameof(f.NewManufacturerName), "Pc_Err_ManufacturerExists", string.Join(", ", exact));
                else if (similar.Count > 0) Warn(nameof(f.NewManufacturerName), "Pc_Warn_ManufacturerSimilar", string.Join(", ", similar));
            }
        }
        else if (f.ManufacturerId.HasValue && ctx.ManufacturerIsWrong)
            Err(nameof(f.ManufacturerId), "Pc_Err_ManufacturerWrong");

        // Section ↔ Commodity Group (تنظیمات مدیر؛ null = بدون محدودیت)
        if (f.CommodityGroup1Id.HasValue && ctx.AllowedGroup1Ids != null && !ctx.AllowedGroup1Ids.Contains(f.CommodityGroup1Id.Value))
            Err(nameof(f.CommodityGroup1Id), "Pc_Err_Group1NotAllowed");
        if (f.CommodityGroup2Id.HasValue && ctx.AllowedGroup2Ids != null && !ctx.AllowedGroup2Ids.Contains(f.CommodityGroup2Id.Value))
            Err(nameof(f.CommodityGroup2Id), "Pc_Err_Group2NotAllowed");

        // FREE FORMAT TEXT
        var fft = f.Attributes.FirstOrDefault(a => string.Equals(a.Name, "FREE FORMAT TEXT", StringComparison.OrdinalIgnoreCase));
        if (fft != null)
        {
            var userText = fft.Value ?? string.Empty;
            var mfrNames = new[] { ctx.ManufacturerName, f.NewManufacturerName, ctx.SupplierName };
            // متن اضافه‌شده‌ی «VENDOR NAME/CODE» در Change عمداً مجاز است؛ فقط متن کاربر بررسی می‌شود
            var cut = userText.IndexOf("VENDOR NAME:", StringComparison.OrdinalIgnoreCase);
            var scanned = cut >= 0 ? userText[..cut] : userText;
            var cut2 = scanned.IndexOf("VENDOR CODE:", StringComparison.OrdinalIgnoreCase);
            if (cut2 >= 0) scanned = scanned[..cut2];
            if (PartCatalogueRules.MentionsAnyName(scanned, mfrNames))
                Warn("FreeFormatText", "Pc_Warn_FreeFormatVendor");
        }

        if (!forSubmit) return r;

        // ---------- الزامی‌های Submit ----------
        if (!ctx.HasRequestNumber) Err(nameof(f.Id), "Pc_Err_NumberRequired");

        Req(f.SectionId.HasValue, nameof(f.SectionId));
        Req(f.TechnicalClassId.HasValue, nameof(f.TechnicalClassId));
        Req(f.CommodityGroup1Id.HasValue, nameof(f.CommodityGroup1Id));
        Req(f.CommodityGroup2Id.HasValue, nameof(f.CommodityGroup2Id));
        Req(f.ProductFamilyId.HasValue, nameof(f.ProductFamilyId));
        Req(f.UnitOfMeasureId.HasValue, nameof(f.UnitOfMeasureId));
        Req(f.IsNewManufacturer || f.ManufacturerId.HasValue, nameof(f.ManufacturerId));
        // Is Critical*/Is Capital مشتق از Type Designation‌اند و پیش‌فرضشان No است؛ پس Type Designation اختیاری می‌ماند
        Req(f.MinimumStock.HasValue || PartCatalogueRules.IsPlanningMethodA(ctx.PlanningMethodCode), nameof(f.MinimumStock));
        Req(f.MaxConsumptionRate.HasValue || PartCatalogueRules.IsPlanningMethodA(ctx.PlanningMethodCode), nameof(f.MaxConsumptionRate));
        Req(f.StockGradeId.HasValue, nameof(f.StockGradeId));

        // FREE FORMAT TEXT اجباری است (مقدارِ حداقل یک Space)
        if (fft == null || string.IsNullOrEmpty(fft.Value)) Err("FreeFormatText", "Pc_Err_FreeFormatRequired");
        else if (PartCatalogueRules.ContainsLineBreak(fft.Value)) Err("FreeFormatText", "Pc_Err_FreeFormatSingleLine");

        // اگر Is New Equipment: تجهیز به‌صورت متن آزاد ثبت می‌شود (حداقل یک خط)
        if (f.IsNewEquipment && ctx.EquipmentLineCount == 0) Err("Equipment", "Pc_Err_NewEquipmentText");

        // شماره‌ی قطعه‌ی سازنده نیست + شرح به Pos/Item اشاره می‌کند ⇒ نقشه/مدرک پیوست الزامی
        var mfrPartMissing = !ctx.ManufacturerIsLocal && string.IsNullOrWhiteSpace(f.ManufacturerPartNumber);
        if (mfrPartMissing && PartCatalogueRules.ReferencesPosOrItem(fft?.Value) && ctx.DrawingOrDocumentAttachmentCount == 0)
            Err("Attachments", "Pc_Err_PosItemNeedsAttachment");

        // ---------- Change ----------
        if (isChange)
        {
            Req(!string.IsNullOrWhiteSpace(f.ExistingPartNumber), nameof(f.ExistingPartNumber));
            Req(f.ChangesMatchInventory.HasValue, nameof(f.ChangesMatchInventory));
            Req(!string.IsNullOrWhiteSpace(f.ManufacturerPartNumber) || ctx.ManufacturerIsLocal, nameof(f.ManufacturerPartNumber));

            if (!string.IsNullOrWhiteSpace(f.ExistingPartNumber))
            {
                if (!ctx.ExistingPartResolved) Err(nameof(f.ExistingPartNumber), "Pc_Err_ChangePartNotFound");
                // واحد اندازه‌گیری در Change قابل تغییر نیست: پارت جدید باید ثبت شود (مسئولیت با درخواست‌دهنده)
                else if (ctx.ExistingPartUnitOfMeasureId.HasValue && f.UnitOfMeasureId.HasValue
                         && ctx.ExistingPartUnitOfMeasureId.Value != f.UnitOfMeasureId.Value)
                    Err(nameof(f.UnitOfMeasureId), "Pc_Err_ChangeUomLocked");
            }

            if (f.ChangesMatchInventory == false && ctx.SupportAttachmentCount + ctx.DrawingOrDocumentAttachmentCount == 0)
                Err("Attachments", "Pc_Err_ChangeSupportDocs");
        }

        return r;
    }
}
