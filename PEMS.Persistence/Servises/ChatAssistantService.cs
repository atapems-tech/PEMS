using Microsoft.EntityFrameworkCore;
using PEMS.AI.Interfaces;
using PEMS.AI.Models;
using PEMS.Application.DTOs;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Services;

/// <summary>
/// دستیار گفتگوی مهندسی، طبق بخش ۱۱ سند AI Redesign: نباید فقط روی آمار کلی کار کند، باید
/// بتواند به سوالات مبتنی بر یک تجهیز/مدرک/Vendor مشخص هم پاسخ دهد (مثل "مدارک مرتبط با پمپ
/// P-101 را نشان بده"). قبل از پاسخ، اول تلاش می‌کند از AI Relationship Engine یک زمینه‌ی
/// متمرکز (Focused Context) بسازد؛ اگر موجودیت خاصی در سوال پیدا نشد، به همان عکس‌فوری کلی
/// آمار سیستم برمی‌گردد. این یک نسخه‌ی "RAG سبک، مبتنی بر داده‌ی ساختاریافته" است، نه Embedding
/// برداری کامل (که در فاز بعدی به AI Architecture اضافه می‌شود).
/// </summary>
public class ChatAssistantService : IChatAssistantService
{
    private readonly PEMSDbContext _context;
    private readonly IOllamaService _ollamaService;
    private readonly IRelationshipService _relationshipService;
    private readonly IKnowledgeBaseService _knowledgeBaseService;

    public ChatAssistantService(PEMSDbContext context, IOllamaService ollamaService,
        IRelationshipService relationshipService, IKnowledgeBaseService knowledgeBaseService)
    {
        _context = context;
        _ollamaService = ollamaService;
        _relationshipService = relationshipService;
        _knowledgeBaseService = knowledgeBaseService;
    }

    public async Task<List<ChatMessageDto>> GetHistoryAsync(int userId)
    {
        return await _context.ChatMessages
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.CreatedDate)
            .Select(x => new ChatMessageDto { Role = x.Role, Content = x.Content, CreatedDate = x.CreatedDate })
            .ToListAsync();
    }

    public async Task ClearHistoryAsync(int userId)
    {
        var messages = await _context.ChatMessages.Where(x => x.UserId == userId).ToListAsync();
        _context.ChatMessages.RemoveRange(messages);
        await _context.SaveChangesAsync();
    }

    public async Task<string> AskAsync(int userId, string question)
    {
        await _context.ChatMessages.AddAsync(new ChatMessage
        {
            UserId = userId,
            Role = "User",
            Content = question,
            CreatedDate = DateTime.Now
        });
        await _context.SaveChangesAsync();

        string answer;

        try
        {
            // تلاش برای یافتن موجودیت مشخص (تجهیز/مدرک/Vendor) در سوال، از طریق Relationship Engine
            var focused = await _relationshipService.ExploreByFreeTextAsync(question);

            string context;

            if (focused != null)
            {
                context = BuildFocusedContext(focused);
            }
            else
            {
                // هیچ موجودیت مشخصی پیدا نشد: Semantic Search واقعی روی Knowledge Base (طبق AI
                // Architecture) - برای سوالات باز مثل "کدام مدارک درباره‌ی فشار طراحی صحبت می‌کنند؟"
                var semanticResults = await _knowledgeBaseService.SearchAsync(question, topK: 3);

                context = semanticResults.Any()
                    ? "Semantically relevant records:\n" + string.Join("\n", semanticResults
                        .Select(r => $"- [{r.SourceType} #{r.SourceId}, relevance {r.SimilarityScore:P0}] {r.Content}"))
                    : await BuildGeneralSnapshotAsync();
            }

            var prompt = $"""
You are the PEMS Engineering Assistant for a petrochemical engineering management system.
Answer the user's question using ONLY the data below. If the answer is not in the data, say
clearly that you don't have that information yet in the system rather than guessing. Be concise
(max 6 sentences) and professional. Answer in the same language as the question.

DATA:
{context}

QUESTION:
{question}
""";

            answer = await _ollamaService.GenerateAsync(prompt, new OllamaGenerateOptions { Temperature = 0.3, MaxResponseTokens = 300 });
        }
        catch (Exception ex)
        {
            // طبق گزارش واقعی «هوش مصنوعی جوابی نمی‌دهد»: قبلاً فقط تماس نهایی با Ollama را
            // Try/Catch می‌کرد - اگر خطا در مراحل قبلی (Relationship Engine یا Knowledge Base)
            // رخ می‌داد، کل درخواست با خطای خام سرور (۵۰۰) شکست می‌خورد و کاربر هیچ پیامی
            // نمی‌دید. الان کل مسیر محافظت می‌شود.
            answer = $"متاسفانه در حال حاضر امکان ارتباط با موتور هوش مصنوعی محلی (Ollama) وجود ندارد یا خطایی رخ داد. جزئیات فنی: {ex.Message}";
        }

        if (string.IsNullOrWhiteSpace(answer))
            answer = "پاسخی از موتور هوش مصنوعی دریافت نشد. لطفاً دوباره تلاش کنید.";

        await _context.ChatMessages.AddAsync(new ChatMessage
        {
            UserId = userId,
            Role = "Assistant",
            Content = answer,
            CreatedDate = DateTime.Now
        });
        await _context.SaveChangesAsync();

        return answer;
    }

    private static string BuildFocusedContext(RelationshipExplorationDto r)
    {
        var lines = new List<string>
        {
            $"Focused on: {r.RootType} \"{r.RootLabel}\" (id {r.RootId})",
            "Engineering Documents: " + (r.Documents.Any() ? string.Join(" | ", r.Documents.Select(x => $"{x.Label} [{x.SubLabel}]")) : "none"),
            "Vendor Documents (VDR): " + (r.VendorDocuments.Any() ? string.Join(" | ", r.VendorDocuments.Select(x => $"{x.Label} [{x.SubLabel}]")) : "none"),
            "Related Equipment: " + (r.Equipment.Any() ? string.Join(" | ", r.Equipment.Select(x => $"{x.Label} ({x.SubLabel})")) : "none"),
            "Vendors: " + (r.Vendors.Any() ? string.Join(" | ", r.Vendors.Select(x => $"{x.Label} (risk: {x.SubLabel})")) : "none"),
            "Contracts: " + (r.Contracts.Any() ? string.Join(" | ", r.Contracts.Select(x => $"{x.Label} [{x.SubLabel}]")) : "none"),
            "Purchase Orders: " + (r.PurchaseOrders.Any() ? string.Join(" | ", r.PurchaseOrders.Select(x => $"{x.Label} [{x.SubLabel}]")) : "none"),
            "Material Requisitions: " + (r.MaterialRequisitions.Any() ? string.Join(" | ", r.MaterialRequisitions.Select(x => $"{x.Label} [{x.SubLabel}]")) : "none"),
            "Quotations (MRQ): " + (r.Quotations.Any() ? string.Join(" | ", r.Quotations.Select(x => $"{x.Label} [{x.SubLabel}]")) : "none"),
            "Maintenance / Work Orders: " + (r.WorkOrders.Any() ? string.Join(" | ", r.WorkOrders.Select(x => $"{x.Label} [{x.SubLabel}]")) : "none"),
        };

        return string.Join("\n", lines);
    }

    private async Task<string> BuildGeneralSnapshotAsync()
    {
        var docsByStatus = await _context.EngineeringDocuments
            .GroupBy(x => x.Status)
            .Select(g => g.Key + ": " + g.Count())
            .ToListAsync();

        var activeProjects = await _context.Projects
            .Where(x => x.IsActive)
            .Select(x => x.ProjectCode + " - " + x.ProjectName + " (" + x.Status + ")")
            .Take(10)
            .ToListAsync();

        var openMRs = await _context.MaterialRequisitions
            .Where(x => x.Status != "Closed" && x.Status != "Cancelled")
            .CountAsync();

        var openWorkOrders = await _context.WorkOrders
            .Where(x => x.IsActive && x.Status != "Closed" && x.Status != "Completed")
            .CountAsync();

        var recentDocs = await _context.EngineeringDocuments
            .OrderByDescending(x => x.CreatedDate)
            .Take(5)
            .Select(x => x.DocumentNo + " - " + x.Title + " (" + x.Status + ", Rev " + x.RevisionNo + ")")
            .ToListAsync();

        var pendingIntake = await _context.DocumentAttachments
            .Where(x => x.IntakeStage != null && x.IntakeStage != "Routed" && x.IntakeStage != "Rejected")
            .CountAsync();

        var lines = new List<string>
        {
            "Documents by status: " + string.Join(", ", docsByStatus),
            "Active projects: " + (activeProjects.Any() ? string.Join(" | ", activeProjects) : "none"),
            "Open Material Requisitions (MR): " + openMRs,
            "Open Work Orders (CMMS): " + openWorkOrders,
            "Files pending in AI Intake Queue: " + pendingIntake,
            "Most recent documents: " + (recentDocs.Any() ? string.Join(" | ", recentDocs) : "none")
        };

        return string.Join("\n", lines);
    }
}
