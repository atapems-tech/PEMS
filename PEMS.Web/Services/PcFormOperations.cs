using System.Globalization;
using Microsoft.Extensions.Localization;
using PEMS.Application.Common;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Web.Areas.Warehouse.Models;
using PEMS.Web.Resources;

namespace PEMS.Web.Services;

/// <summary>SCRUM-826: the part catalogue request form as operations (see <see cref="IPcFormOperations"/>): the actor is the employee and roles of the user, every rule is the service's, messages are the old ones.</summary>
public sealed class PcFormOperations(IPartCatalogueService service, IPartCatalogueLookupService lookups, IRoleModuleActionService actions, ICurrentUserContext user, IStringLocalizer<SharedResource> l) : IPcFormOperations
{
    private static int? Int(string? s) => int.TryParse(s, out var v) ? v : null;
    private static decimal? Dec(string? s) => decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    private static DateTime? Date(string? s) => DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    private static byte[]? Version(string? s) { try { return string.IsNullOrEmpty(s) ? null : Convert.FromBase64String(s); } catch (FormatException) { return null; } }

    private PcActor? Actor => user.EmployeeId is int emp ? new PcActor(emp, user.UserId, user.Roles) : null;

    private async Task<(PcActor? Actor, PcFormAnswer? Refusal)> CheckAsync()
    {
        var actor = Actor;
        if (actor == null) return (null, new(false, l["Pc_Err_NoEmployee"].Value, 0));
        foreach (var a in new[] { "Create" }) if (await actions.HasActionAsync(user.Roles, "PartCatalogue", a)) return (actor, null);
        throw new UnauthorizedAccessException();
    }

    private PcFormAnswer Answer(PcResult r, int id = 0) => r.Success ? new(true, null, id) : new(false, string.Join(" | ", PcMessages.FormatAll(l, r.Errors)), 0);

    public async Task<List<PcFormRow>> OneAsync(int id)
    {
        var (actor, _) = await CheckAsync();
        if (actor == null) return [];
        var d = await service.GetDetailsAsync(id, actor);
        if (d == null || !d.Actions.CanEdit) return [];
        var f = d.Form;
        var attributes = string.Join("\n", f.Attributes.Select(a => a.Name + " = " + a.Value));
        return [new PcFormRow(d.Id, d.RowVersion is { Length: > 0 } ? Convert.ToBase64String(d.RowVersion) : "", d.RequestType, d.Status, d.RequestNumber, d.RequesterName, attributes, f.Urgent.ToString().ToLowerInvariant(), f.SectionId?.ToString(System.Globalization.CultureInfo.InvariantCulture), f.IsNewEquipment.ToString().ToLowerInvariant(), f.IsRd.ToString().ToLowerInvariant(), f.ExistingPartNumber, f.ChangesMatchInventory?.ToString(System.Globalization.CultureInfo.InvariantCulture).ToLowerInvariant(), f.TechnicalClassId?.ToString(System.Globalization.CultureInfo.InvariantCulture), f.AssetType, f.CommodityGroup1Id?.ToString(System.Globalization.CultureInfo.InvariantCulture), f.CommodityGroup2Id?.ToString(System.Globalization.CultureInfo.InvariantCulture), f.ProductFamilyId?.ToString(System.Globalization.CultureInfo.InvariantCulture), f.UnitOfMeasureId?.ToString(System.Globalization.CultureInfo.InvariantCulture), f.ManufacturerId?.ToString(System.Globalization.CultureInfo.InvariantCulture), f.IsNewManufacturer.ToString().ToLowerInvariant(), f.NewManufacturerName, f.ManufacturerPartNumber, f.SupplierId?.ToString(System.Globalization.CultureInfo.InvariantCulture), f.SupplierPartNumber, f.PartPrice?.ToString(System.Globalization.CultureInfo.InvariantCulture), f.CurrencyId?.ToString(System.Globalization.CultureInfo.InvariantCulture), f.TypeDesignationId?.ToString(System.Globalization.CultureInfo.InvariantCulture), f.SparePartSerialNo, f.EquipmentSerialNo, f.ManufacturerDrawingNo, f.DocumentNo, f.PlanningMethodId?.ToString(System.Globalization.CultureInfo.InvariantCulture), f.MinimumStock?.ToString(System.Globalization.CultureInfo.InvariantCulture), f.MaxConsumptionRate?.ToString(System.Globalization.CultureInfo.InvariantCulture), f.FirstPurchaseQuantity?.ToString(System.Globalization.CultureInfo.InvariantCulture), f.PrimaryIssuedRequired.ToString().ToLowerInvariant(), f.IssuedQty?.ToString(System.Globalization.CultureInfo.InvariantCulture), f.StockGradeId?.ToString(System.Globalization.CultureInfo.InvariantCulture), f.WantedReceiptDate?.ToString("yyyy-MM-dd"), f.StorageVolume)];
    }

    public async Task<PcFormAnswer> SaveAsync(int id, string? rowVersion, string requestType, string? attributes, bool submitAfterSave, bool urgent, string? sectionId, bool isNewEquipment, bool isRd, string? existingPartNumber, string? changesMatchInventory, string? technicalClassId, string? assetType, string? commodityGroup1Id, string? commodityGroup2Id, string? productFamilyId, string? unitOfMeasureId, string? manufacturerId, bool isNewManufacturer, string? newManufacturerName, string? manufacturerPartNumber, string? supplierId, string? supplierPartNumber, string? partPrice, string? currencyId, string? typeDesignationId, string? sparePartSerialNo, string? equipmentSerialNo, string? manufacturerDrawingNo, string? documentNo, string? planningMethodId, string? minimumStock, string? maxConsumptionRate, string? firstPurchaseQuantity, bool primaryIssuedRequired, string? issuedQty, string? stockGradeId, string? wantedReceiptDate, string? storageVolume)
    {
        var (actor, refusal) = await CheckAsync();
        if (actor == null) return refusal!;
        var form = new PartCatalogueFormDto { Id = id, RowVersion = Version(rowVersion), RequestType = requestType, Urgent = urgent, SectionId = Int(sectionId), IsNewEquipment = isNewEquipment, IsRd = isRd, ExistingPartNumber = existingPartNumber, ChangesMatchInventory = changesMatchInventory == null || changesMatchInventory == "" ? null : changesMatchInventory == "true", TechnicalClassId = Int(technicalClassId), AssetType = assetType, CommodityGroup1Id = Int(commodityGroup1Id), CommodityGroup2Id = Int(commodityGroup2Id), ProductFamilyId = Int(productFamilyId), UnitOfMeasureId = Int(unitOfMeasureId), ManufacturerId = Int(manufacturerId), IsNewManufacturer = isNewManufacturer, NewManufacturerName = newManufacturerName, ManufacturerPartNumber = manufacturerPartNumber, SupplierId = Int(supplierId), SupplierPartNumber = supplierPartNumber, PartPrice = Dec(partPrice), CurrencyId = Int(currencyId), TypeDesignationId = Int(typeDesignationId), SparePartSerialNo = sparePartSerialNo, EquipmentSerialNo = equipmentSerialNo, ManufacturerDrawingNo = manufacturerDrawingNo, DocumentNo = documentNo, PlanningMethodId = Int(planningMethodId), MinimumStock = Dec(minimumStock), MaxConsumptionRate = Dec(maxConsumptionRate), FirstPurchaseQuantity = Dec(firstPurchaseQuantity), PrimaryIssuedRequired = primaryIssuedRequired, IssuedQty = Dec(issuedQty), StockGradeId = Int(stockGradeId), WantedReceiptDate = Date(wantedReceiptDate), StorageVolume = storageVolume };
        foreach (var line in (attributes ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var p = line.Split('=', 2, StringSplitOptions.TrimEntries);
            form.Attributes.Add(new PartCatalogueAttributeDto { Name = p[0], Value = p.Length > 1 ? p[1] : null });
        }
        var saved = await service.SaveAsync(form, actor);
        if (!saved.Success) return Answer(saved.Result);
        if (!submitAfterSave) return new(true, null, id);
        var fresh = await service.GetDetailsAsync(id, actor);
        var submit = await service.SubmitAsync(id, fresh?.RowVersion, actor);
        return Answer(submit, id);
    }

    public async Task<PcFormAnswer> GenerateNumberAsync(int id, string? rowVersion) { var (a, r) = await CheckAsync(); return a == null ? r! : Answer(await service.GenerateNumberAsync(id, Version(rowVersion), a), id); }
    public async Task<PcFormAnswer> LoadPartAsync(int id, string? existingPartNumber) { var (a, r) = await CheckAsync(); return a == null ? r! : Answer(await service.LoadExistingPartAsync(id, existingPartNumber ?? "", a), id); }
    public async Task<PcFormAnswer> AddNoteAsync(int id, string text) { var (a, r) = await CheckAsync(); return a == null ? r! : Answer(await service.AddNoteAsync(id, text, a), id); }
    public async Task<PcFormAnswer> AddEquipmentAsync(int id, string? equipmentId, string? equipmentText, string? installedQuantity) { var (a, r) = await CheckAsync(); return a == null ? r! : Answer(await service.AddEquipmentAsync(id, Int(equipmentId), equipmentText, Dec(installedQuantity) ?? 0, a), id); }
    public async Task<PcFormAnswer> RemoveEquipmentAsync(int id, int lineId) { var (a, r) = await CheckAsync(); return a == null ? r! : Answer(await service.RemoveEquipmentAsync(id, lineId, a), id); }
    public async Task<PcFormAnswer> RemoveAttachmentAsync(int id, int attachmentId) { var (a, r) = await CheckAsync(); return a == null ? r! : Answer(await service.RemoveAttachmentAsync(id, attachmentId, a), id); }

    public async Task<PcFormAnswer> UploadAttachmentAsync(int id, OperationFile? file, string? kind)
    {
        var (a, r) = await CheckAsync();
        if (a == null) return r!;
        if (file == null || file.Bytes.Length == 0) return new(false, string.Join(" | ", PcMessages.FormatAll(l, new[] { new PcMessage("File", "Pc_Err_Required") })), 0);
        await using var stream = new MemoryStream(file.Bytes);
        return Answer(await service.UploadAttachmentAsync(id, file.FileName, file.ContentType, kind ?? "Document", stream, file.Bytes.Length, a), id);
    }

    private static List<PcFormChoice> Pick(IEnumerable<PcLookupItem> items) => items.Select(x => new PcFormChoice(x.Id.ToString(), x.Text)).ToList();
    public async Task<List<PcFormChoice>> SectionsAsync() => Pick(await lookups.GetSectionsAsync());
    public async Task<List<PcFormChoice>> Group1Async(string? sectionId = null) => Pick(await lookups.GetCommodityGroup1Async(Int(sectionId)));
    public async Task<List<PcFormChoice>> Group2Async(string? group1Id = null) => Pick(await lookups.GetCommodityGroup2Async(Int(group1Id)));
    public async Task<List<PcFormChoice>> FamiliesAsync() => Pick(await lookups.GetProductFamiliesAsync());
    public async Task<List<PcFormChoice>> UnitsAsync() => Pick(await lookups.GetUnitsAsync());
    public async Task<List<PcFormChoice>> CurrenciesAsync() => Pick(await lookups.GetCurrenciesAsync());
    public async Task<List<PcFormChoice>> StockGradesAsync() => Pick(await lookups.GetStockGradesAsync());
    public async Task<List<PcFormChoice>> PlanningMethodsAsync() => Pick(await lookups.GetPlanningMethodsAsync());
    public async Task<List<PcFormChoice>> TypeDesignationsAsync() => Pick(await lookups.GetTypeDesignationsAsync());
    public async Task<List<PcFormChoice>> TechnicalClassesAsync() => Pick(await lookups.SearchTechnicalClassesAsync(null, 500));
    public async Task<List<PcFormChoice>> ManufacturersAsync() => Pick(await lookups.SearchManufacturersAsync(null, 500));
    public async Task<List<PcFormChoice>> SuppliersAsync() => Pick(await lookups.SearchSuppliersAsync(null, 500));
}
