using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Interfaces;
using PEMS.Web.Security;

namespace PEMS.Web.Controllers;

/// <summary>
/// Engineering Document Intelligence: طراحی UI و جریان کار مطابق همان الگوی موفق VDR -
/// Dashboard + صف بازبینی + صفحه‌ی Review با تایید/رد هر پیشنهاد AI + Comment Management.
/// Upload → AI Processing → Classification → Discipline Assignment → Engineering Review →
/// Comment → Revision → Approval → Knowledge Base
/// </summary>
public class EngineeringDocumentIntelligenceController : Controller
{
    private readonly IEngineeringDocumentIntelligenceService _service;
    private readonly ICurrentUserService _currentUser;

    public EngineeringDocumentIntelligenceController(
        IEngineeringDocumentIntelligenceService service,
        ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Document Intelligence";
        ViewData["Section"] = "AI Engineering Intelligence";

        var dashboard = await _service.GetDashboardAsync();
        var pending = await _service.GetPendingReviewAsync();

        ViewBag.Dashboard = dashboard;

        return View(pending);
    }

    public async Task<IActionResult> Review(int id)
    {
        ViewData["Title"] = "Document Intelligence Review";
        ViewData["Section"] = "AI Engineering Intelligence";

        var detail = await _service.GetDetailAsync(id);
        if (detail == null) return NotFound();

        return View(detail);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveClassification(int documentId, int classificationId,
        bool applyDiscipline, bool applyVendor, bool applyMR, bool applyRevision, bool applyIssuePurpose)
    {
        await _service.ApproveClassificationAsync(classificationId, applyDiscipline, applyVendor,
            applyMR, applyRevision, applyIssuePurpose, _currentUser.UserId);

        TempData["Message"] = "Classification approved and applied to the document";
        return RedirectToAction(nameof(Review), new { id = documentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectClassification(int documentId, int classificationId)
    {
        await _service.RejectClassificationAsync(classificationId, _currentUser.UserId);
        return RedirectToAction(nameof(Review), new { id = documentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveTagLink(int documentId, int tagLinkId)
    {
        await _service.ApproveTagLinkAsync(tagLinkId, _currentUser.UserId);
        return RedirectToAction(nameof(Review), new { id = documentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectTagLink(int documentId, int tagLinkId)
    {
        await _service.RejectTagLinkAsync(tagLinkId, _currentUser.UserId);
        return RedirectToAction(nameof(Review), new { id = documentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdvanceStage(int documentId, string newStage)
    {
        await _service.AdvanceStageAsync(documentId, newStage);
        TempData["Message"] = $"Document moved to stage: {newStage}";
        return RedirectToAction(nameof(Review), new { id = documentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(int documentId, string commentText)
    {
        await _service.AddCommentAsync(documentId, commentText, _currentUser.FullName);
        return RedirectToAction(nameof(Review), new { id = documentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCommentAction(int documentId, int commentId, string actionStatus)
    {
        await _service.SetCommentActionStatusAsync(commentId, actionStatus);
        return RedirectToAction(nameof(Review), new { id = documentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordCommentResponse(int documentId, int commentId, string responseText)
    {
        await _service.RecordCommentResponseAsync(commentId, responseText);
        return RedirectToAction(nameof(Review), new { id = documentId });
    }
}
