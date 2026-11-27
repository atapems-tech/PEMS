using PEMS.Application.Common;

namespace PEMS.Application.Interfaces;

public sealed record PcFormAnswer(bool Success, string? Error, int NewId);
public sealed record PcFormRow(int Id, string RowVersion, string RequestType, string Status, string? RequestNumber, string? Requester, string Attributes, string? Urgent, string? SectionId, string? IsNewEquipment, string? IsRd, string? ExistingPartNumber, string? ChangesMatchInventory, string? TechnicalClassId, string? AssetType, string? CommodityGroup1Id, string? CommodityGroup2Id, string? ProductFamilyId, string? UnitOfMeasureId, string? ManufacturerId, string? IsNewManufacturer, string? NewManufacturerName, string? ManufacturerPartNumber, string? SupplierId, string? SupplierPartNumber, string? PartPrice, string? CurrencyId, string? TypeDesignationId, string? SparePartSerialNo, string? EquipmentSerialNo, string? ManufacturerDrawingNo, string? DocumentNo, string? PlanningMethodId, string? MinimumStock, string? MaxConsumptionRate, string? FirstPurchaseQuantity, string? PrimaryIssuedRequired, string? IssuedQty, string? StockGradeId, string? WantedReceiptDate, string? StorageVolume);
public sealed record PcFormChoice(string Value, string Label);

/// <summary>SCRUM-826: the part catalogue request form (PartCatalogueRequestController Edit and its side commands) as operations over IPartCatalogueService with the same actor, rules and messages; numbers are text on the form and parsed here.</summary>
public interface IPcFormOperations
{
    [PemsOperation("PcForm", "One")]
    Task<List<PcFormRow>> OneAsync(int id);

    [PemsOperation("PcForm", "Save")]
    Task<PcFormAnswer> SaveAsync(int id, string? rowVersion, string requestType, string? attributes, bool submitAfterSave, bool urgent, string? sectionId, bool isNewEquipment, bool isRd, string? existingPartNumber, string? changesMatchInventory, string? technicalClassId, string? assetType, string? commodityGroup1Id, string? commodityGroup2Id, string? productFamilyId, string? unitOfMeasureId, string? manufacturerId, bool isNewManufacturer, string? newManufacturerName, string? manufacturerPartNumber, string? supplierId, string? supplierPartNumber, string? partPrice, string? currencyId, string? typeDesignationId, string? sparePartSerialNo, string? equipmentSerialNo, string? manufacturerDrawingNo, string? documentNo, string? planningMethodId, string? minimumStock, string? maxConsumptionRate, string? firstPurchaseQuantity, bool primaryIssuedRequired, string? issuedQty, string? stockGradeId, string? wantedReceiptDate, string? storageVolume);

    [PemsOperation("PcForm", "GenerateNumber")]
    Task<PcFormAnswer> GenerateNumberAsync(int id, string? rowVersion);

    [PemsOperation("PcForm", "LoadPart")]
    Task<PcFormAnswer> LoadPartAsync(int id, string? existingPartNumber);

    [PemsOperation("PcForm", "AddNote")]
    Task<PcFormAnswer> AddNoteAsync(int id, string text);

    [PemsOperation("PcForm", "AddEquipment")]
    Task<PcFormAnswer> AddEquipmentAsync(int id, string? equipmentId, string? equipmentText, string? installedQuantity);

    [PemsOperation("PcForm", "RemoveEquipment")]
    Task<PcFormAnswer> RemoveEquipmentAsync(int id, int lineId);

    [PemsOperation("PcForm", "UploadAttachment")]
    Task<PcFormAnswer> UploadAttachmentAsync(int id, OperationFile? file, string? kind);

    [PemsOperation("PcForm", "RemoveAttachment")]
    Task<PcFormAnswer> RemoveAttachmentAsync(int id, int attachmentId);

    [PemsOperation("PcForm", "Sections")] Task<List<PcFormChoice>> SectionsAsync();
    [PemsOperation("PcForm", "Group1")] Task<List<PcFormChoice>> Group1Async(string? sectionId = null);
    [PemsOperation("PcForm", "Group2")] Task<List<PcFormChoice>> Group2Async(string? group1Id = null);
    [PemsOperation("PcForm", "Families")] Task<List<PcFormChoice>> FamiliesAsync();
    [PemsOperation("PcForm", "Units")] Task<List<PcFormChoice>> UnitsAsync();
    [PemsOperation("PcForm", "Currencies")] Task<List<PcFormChoice>> CurrenciesAsync();
    [PemsOperation("PcForm", "StockGrades")] Task<List<PcFormChoice>> StockGradesAsync();
    [PemsOperation("PcForm", "PlanningMethods")] Task<List<PcFormChoice>> PlanningMethodsAsync();
    [PemsOperation("PcForm", "TypeDesignations")] Task<List<PcFormChoice>> TypeDesignationsAsync();
    [PemsOperation("PcForm", "TechnicalClasses")] Task<List<PcFormChoice>> TechnicalClassesAsync();
    [PemsOperation("PcForm", "Manufacturers")] Task<List<PcFormChoice>> ManufacturersAsync();
    [PemsOperation("PcForm", "Suppliers")] Task<List<PcFormChoice>> SuppliersAsync();
}
