using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Persistence.Context;

namespace PEMS.Web.Controllers;

/// <summary>
/// SCRUM-830: the register itself (list, filters, saved views, both Excel exports) is the definition page document-register over IDocumentPagesOperations.
/// What stays is the redirect that opens a document on the details page of its own type (engineering, VDR, contract, maintenance); the target page checks the permissions.
/// </summary>
public class DocumentRegisterController(PEMSDbContext context) : Controller
{
    /// <summary>SCRUM-538: opens a document's own details page by its type (engineering, VDR, contract, maintenance); the target page checks the permissions.</summary>
    [HttpGet]
    public async Task<IActionResult> Open(int id)
    {
        var target = PEMS.Web.Helpers.DocumentLinks.Target(await context.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.DocumentId == id));
        return target is { } t ? Redirect($"/{t.Controller}/Details/{t.Id}") : NotFound();
    }
}
