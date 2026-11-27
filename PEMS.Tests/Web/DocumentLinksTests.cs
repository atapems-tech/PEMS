using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using PEMS.Domain.Entities;
using PEMS.Tests.TestSupport;
using PEMS.Web.Controllers;
using PEMS.Web.Helpers;

namespace PEMS.Tests.Web;

/// <summary>
/// SCRUM-538: a related document (Letter, MOC, Technical Query) can be any document type; the link always went to the
/// engineering document page, so a VDR document gave 404. DocumentRegister/Open chooses the page by the type.
/// </summary>
public class DocumentLinksTests
{
    [Fact]
    public void EachDocumentType_OpensItsOwnDetailsPage()
    {
        Assert.Equal(("EngineeringDocuments", 4), DocumentLinks.Target(new EngineeringDocument { DocumentId = 4 }));
        Assert.Equal(("VDR", 6), DocumentLinks.Target(new VDRItem { DocumentId = 6 }));
        Assert.Equal(("Contract", 12), DocumentLinks.Target(new ContractDocument { DocumentId = 7, ContractId = 12 }));
        Assert.Equal(("WorkOrder", 30), DocumentLinks.Target(new MaintenanceDocument { DocumentId = 8, EquipmentId = 21, WorkOrderId = 30 }));
        Assert.Equal(("Equipment", 21), DocumentLinks.Target(new MaintenanceDocument { DocumentId = 9, EquipmentId = 21 }));
        Assert.Null(DocumentLinks.Target(null));
    }

    [Fact]
    public async Task Open_RedirectsByTheStoredType_AndIsNotFoundForAnUnknownId()
    {
        using var db = PemsDbContextFactory.CreateInMemory("doclinks_" + Guid.NewGuid().ToString("N"));
        db.Add(new VDRItem { DocumentId = 6, DocumentNo = "QPC-IN-DS-003" });
        db.Add(new EngineeringDocument { DocumentId = 1, DocumentNo = "PEMS-001-PR-DWG-0001" });
        await db.SaveChangesAsync();

        var shell = new PEMS.Web.Services.ShellOperations(null!, null!, db);

        Assert.Equal("/VDR/Details/6", (await shell.OpenDocumentAsync(6)).RedirectUrl);
        Assert.Equal("/EngineeringDocuments/Details/1", (await shell.OpenDocumentAsync(1)).RedirectUrl);
        Assert.False((await shell.OpenDocumentAsync(999)).Success);
    }

    private static string Read(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PEMS.Web", "PEMS.Web.csproj"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine([dir!.FullName, .. parts]));
    }
}
