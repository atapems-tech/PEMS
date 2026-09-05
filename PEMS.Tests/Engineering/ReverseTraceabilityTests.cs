using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Entities;
using PEMS.Tests.TestSupport;

namespace PEMS.Tests.Engineering;

/// <summary>
/// طبق Phase E-04 (رفع P1 ممیزی Reverse Traceability): تایید این‌که Equipment/Contract/Vendor/
/// MaterialRequisition واقعاً می‌توانند مدارک مهندسی مرتبط خودشان را برشمارند - نه فقط این‌که
/// EngineeringDocument یک FK به آن‌ها دارد.
/// </summary>
public class ReverseTraceabilityTests
{
    [Fact]
    public async Task Equipment_EngineeringDocuments_ReturnsRelatedDocumentsOnly()
    {
        var ctx = PemsDbContextFactory.CreateInMemory(nameof(Equipment_EngineeringDocuments_ReturnsRelatedDocumentsOnly));

        var equipment = new Equipment { TagNumber = "P-101", Name = "Pump", EquipmentType = "Pump", ProjectId = 1, DisciplineId = 1 };
        var otherEquipment = new Equipment { TagNumber = "P-102", Name = "Pump 2", EquipmentType = "Pump", ProjectId = 1, DisciplineId = 1 };
        ctx.Equipments.AddRange(equipment, otherEquipment);
        await ctx.SaveChangesAsync();

        ctx.EngineeringDocuments.Add(new EngineeringDocument
        {
            DocumentNo = "PID-001", Title = "P&ID for P-101", DocumentType = "P&ID", ProjectId = 1,
            Status = "Draft", EquipmentId = equipment.EquipmentId
        });
        ctx.EngineeringDocuments.Add(new EngineeringDocument
        {
            DocumentNo = "DS-001", Title = "Datasheet for P-101", DocumentType = "Datasheet", ProjectId = 1,
            Status = "Draft", EquipmentId = equipment.EquipmentId
        });
        ctx.EngineeringDocuments.Add(new EngineeringDocument
        {
            DocumentNo = "PID-002", Title = "P&ID for P-102", DocumentType = "P&ID", ProjectId = 1,
            Status = "Draft", EquipmentId = otherEquipment.EquipmentId
        });
        await ctx.SaveChangesAsync();

        var reloaded = await ctx.Equipments
            .Include(e => e.EngineeringDocuments)
            .FirstAsync(e => e.EquipmentId == equipment.EquipmentId);

        Assert.Equal(2, reloaded.EngineeringDocuments.Count);
        Assert.Contains(reloaded.EngineeringDocuments, d => d.DocumentNo == "PID-001");
        Assert.Contains(reloaded.EngineeringDocuments, d => d.DocumentNo == "DS-001");
        Assert.DoesNotContain(reloaded.EngineeringDocuments, d => d.DocumentNo == "PID-002");
    }

    [Fact]
    public async Task Vendor_EngineeringDocuments_ReturnsRelatedDocuments()
    {
        var ctx = PemsDbContextFactory.CreateInMemory(nameof(Vendor_EngineeringDocuments_ReturnsRelatedDocuments));

        var vendor = new Vendor { Name = "Acme Pumps" };
        ctx.Vendors.Add(vendor);
        await ctx.SaveChangesAsync();

        ctx.EngineeringDocuments.Add(new EngineeringDocument
        {
            DocumentNo = "VD-001", Title = "Vendor Datasheet", DocumentType = "Datasheet", ProjectId = 1,
            Status = "Draft", VendorId = vendor.VendorId
        });
        await ctx.SaveChangesAsync();

        var reloaded = await ctx.Vendors.Include(v => v.EngineeringDocuments).FirstAsync(v => v.VendorId == vendor.VendorId);

        Assert.Single(reloaded.EngineeringDocuments);
        Assert.Equal("VD-001", reloaded.EngineeringDocuments.First().DocumentNo);
    }
}
