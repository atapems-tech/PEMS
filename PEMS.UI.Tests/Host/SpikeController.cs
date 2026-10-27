using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PEMS.UI.Abstractions;

namespace PEMS.UI.Tests.Host;

public sealed class SpikeModel
{
    [Required, StringLength(50)] public string? Name { get; set; }
    [Range(1, 100)] public int Quantity { get; set; }
    public DateTime? Due { get; set; }
    public bool Active { get; set; }
    public IFormFile? Attachment { get; set; }
}

[AutoValidateAntiforgeryToken]
public sealed class SpikeController(IAntiforgery antiforgery) : Controller
{
    private const int Rows = 1000;

    // Option A: View Component
    [HttpGet("/a/table")] public IActionResult TableA() => View("TableA");

    // Option B: static Razor component rendered from an MVC view
    [HttpGet("/b/table")] public IActionResult TableB() => View("TableB");
    [HttpGet("/b/table-percell")] public IActionResult TableBPerCell() => View("TableBPerCell");
    [HttpGet("/b/descriptor-table")] public IActionResult DescriptorTable() => View("DescriptorTable");
    [HttpGet("/b/antiforgery-probe")] public IActionResult AntiforgeryProbe() => View("AntiforgeryProbe");

    // Form through the Tag Helper facade: model binding + data-annotation validation + antiforgery by the MVC form
    [HttpGet("/b/form")] public IActionResult Form() => View("Form", new SpikeModel());

    [HttpPost("/b/form")]
    public IActionResult FormPost(SpikeModel model)
    {
        if (!ModelState.IsValid) return View("Form", model);
        return Content($"saved:{model.Name}:{model.Quantity}:{model.Due:yyyy-MM-dd}:{model.Active}:{model.Attachment?.Length}");
    }

    // Form rendered entirely from a FormDefinition; the antiforgery token is passed in as a parameter
    [HttpGet("/b/descriptor-form")]
    public IActionResult DescriptorForm()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        ViewData["Token"] = tokens.RequestToken;
        ViewData["TokenName"] = tokens.FormFieldName;
        return View("DescriptorForm");
    }

    [HttpPost("/b/descriptor-form")]
    public IActionResult DescriptorFormPost([FromForm] SpikeModel model, [FromForm] string? Name)
        => Content($"saved:{model.Name}:{model.Attachment?.Length}");

    public static int RowCount => Rows;
}
