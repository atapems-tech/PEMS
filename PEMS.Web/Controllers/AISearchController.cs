using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.AI.Search.Interfaces;
using PEMS.Application.AI.Search.Models;
using PEMS.Persistence.Context;

namespace PEMS.Web.Controllers;

public class AISearchController : Controller
{
    private readonly IDocumentSearchAIService _searchService;
    private readonly PEMSDbContext _context;

    public AISearchController(IDocumentSearchAIService searchService, PEMSDbContext context)
    {
        _searchService = searchService;
        _context = context;
    }

    public async Task<IActionResult> Index(string? q, string? discipline, string? documentType, string? status)
    {
        ViewData["Title"] = "AI Search";
        ViewData["Section"] = "AI Engineering Intelligence";

        ViewBag.Disciplines = await _context.Disciplines.Select(x => x.Name).ToListAsync();
        ViewBag.DocumentTypes = await _context.EngineeringDocuments.Select(x => x.DocumentType).Distinct().ToListAsync();
        ViewBag.Statuses = await _context.EngineeringDocuments.Select(x => x.Status).Distinct().ToListAsync();

        ViewBag.Query = q;
        ViewBag.SelectedDiscipline = discipline;
        ViewBag.SelectedDocumentType = documentType;
        ViewBag.SelectedStatus = status;

        if (string.IsNullOrWhiteSpace(q) && string.IsNullOrWhiteSpace(discipline) &&
            string.IsNullOrWhiteSpace(documentType) && string.IsNullOrWhiteSpace(status))
        {
            return View(new List<DocumentSearchResult>());
        }

        var results = await _searchService.SearchAsync(new DocumentSearchRequest
        {
            Direction = q ?? string.Empty,
            Discipline = discipline,
            DocumentType = documentType,
            Status = status
        });

        return View(results);
    }
}
