using Microsoft.AspNetCore.Mvc;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;

namespace PEMS.Web.Controllers;

/// <summary>
/// Semantic Search واقعی روی Knowledge Base (Embedding محلی + شباهت کسینوسی) - طبق AI
/// Architecture (Volume VIII - Bible)، برای رصد و تست مستقیم بدون نیاز به Chat Assistant.
/// </summary>
public class KnowledgeBaseController : Controller
{
    private readonly IKnowledgeBaseService _service;

    public KnowledgeBaseController(IKnowledgeBaseService service)
    {
        _service = service;
    }

    public async Task<IActionResult> Index(string? q)
    {
        ViewData["Title"] = "Knowledge Base Semantic Search";
        ViewData["Section"] = "AI Engineering Intelligence";

        ViewBag.Query = q;
        ViewBag.EntryCount = await _service.GetEntryCountAsync();

        List<KnowledgeBaseSearchResultDto> results = new();

        if (!string.IsNullOrWhiteSpace(q))
            results = await _service.SearchAsync(q, topK: 10);

        return View(results);
    }
}
