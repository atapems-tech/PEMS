using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Seed;

/// <summary>
/// ==============================================================================
/// طبق «PEMS Help Phase 4 — Increment 3A»: لایه‌ی دانش مهندسی - Concept، DisciplineGuide و
/// StandardReference. هر سه فقط HelpArticle با ArticleType متفاوت هستند (بدون Entity جدید،
/// طبق تصمیم مستندشده در HelpArticle.cs)؛ فقط StandardReference یک رکورد کمکی
/// HelpStandardReference هم دارد.
///
/// هر Concept/Discipline در این فایل مستقیماً بر اساس کدِ واقعاً خوانده‌شده در فازهای قبلی این
/// جلسه نوشته شده - نه حدس. هر Standard در برابر منبع رسمی (ISO/SAE/API/IEC) با WebSearch در
/// همین افزایش تایید شده - عنوان/سازمان/URL دقیقاً از آن نتایج گرفته شده.
///
/// ایدمپوتنت: مقالات بر اساس Code، رکوردهای Standard بر اساس Identifier - عیناً همان الگوی
/// HelpContentSeeder/HelpVisualSeeder.
/// ==============================================================================
/// </summary>
public static class HelpKnowledgeSeeder
{
    private sealed record Cat(string Code, string NameEn, string NameFa, string Icon, int Order);

    private sealed record Art(
        string Code, string CategoryCode, string ArticleType, int Order,
        string TitleEn, string SummaryEn, string ContentEn, string KeywordsEn,
        string TitleFa, string SummaryFa, string ContentFa, string KeywordsFa,
        string[]? RelatedCodes = null);

    private sealed record Std(
        string Code, int Order, string Identifier, string IssuingOrg, string Edition, string OfficialUrl,
        string RelatedModule, string Applicability, string Verification, string ImplStatus, string? EvidencePath,
        string TitleEn, string SummaryEn, string ContentEn, string KeywordsEn,
        string TitleFa, string SummaryFa, string ContentFa, string KeywordsFa,
        string[]? RelatedCodes = null);

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        var categories = new[]
        {
            new Cat("concepts", "Concepts & Glossary", "مفاهیم و واژه‌نامه", "bi-book", 6),
            new Cat("standards", "Standards Reference", "مرجع استانداردها", "bi-award", 7),
            new Cat("disciplines", "Discipline Guides", "راهنمای دیسیپلین‌ها", "bi-diagram-2", 8),
        };

        var categoryIds = new Dictionary<string, int>();
        foreach (var c in categories)
        {
            var existing = await db.HelpCategories.FirstOrDefaultAsync(x => x.Code == c.Code);
            if (existing == null)
            {
                existing = new HelpCategory { Code = c.Code, NameEn = c.NameEn, NameFa = c.NameFa, IconCssClass = c.Icon, DisplayOrder = c.Order, IsActive = true };
                db.HelpCategories.Add(existing);
                await db.SaveChangesAsync();
            }
            categoryIds[c.Code] = existing.HelpCategoryId;
        }

        var concepts = BuildConcepts();
        var disciplines = BuildDisciplines();
        var standards = BuildStandards();

        foreach (var a in concepts.Concat(disciplines))
        {
            if (await db.HelpArticles.AnyAsync(x => x.Code == a.Code)) continue;

            var article = new HelpArticle
            {
                Code = a.Code, HelpCategoryId = categoryIds[a.CategoryCode], ArticleType = a.ArticleType,
                DisplayOrder = a.Order, IsPublished = true, CreatedDate = DateTime.Now
            };
            article.Translations.Add(new HelpArticleTranslation { LanguageCode = "en", Title = a.TitleEn, Summary = a.SummaryEn, Content = a.ContentEn, SearchKeywords = a.KeywordsEn });
            article.Translations.Add(new HelpArticleTranslation { LanguageCode = "fa", Title = a.TitleFa, Summary = a.SummaryFa, Content = a.ContentFa, SearchKeywords = a.KeywordsFa });
            db.HelpArticles.Add(article);
        }

        await db.SaveChangesAsync();

        foreach (var s in standards)
        {
            if (await db.HelpStandardReferences.AnyAsync(x => x.Identifier == s.Identifier)) continue;
            if (await db.HelpArticles.AnyAsync(x => x.Code == s.Code)) continue;

            var article = new HelpArticle
            {
                Code = s.Code, HelpCategoryId = categoryIds["standards"], ArticleType = "StandardReference",
                DisplayOrder = s.Order, IsPublished = true, CreatedDate = DateTime.Now
            };
            article.Translations.Add(new HelpArticleTranslation { LanguageCode = "en", Title = s.TitleEn, Summary = s.SummaryEn, Content = s.ContentEn, SearchKeywords = s.KeywordsEn });
            article.Translations.Add(new HelpArticleTranslation { LanguageCode = "fa", Title = s.TitleFa, Summary = s.SummaryFa, Content = s.ContentFa, SearchKeywords = s.KeywordsFa });
            db.HelpArticles.Add(article);
            await db.SaveChangesAsync();

            db.HelpStandardReferences.Add(new HelpStandardReference
            {
                HelpArticleId = article.HelpArticleId,
                Identifier = s.Identifier,
                IssuingOrganization = s.IssuingOrg,
                EditionOrYear = s.Edition,
                OfficialReferenceUrl = s.OfficialUrl,
                RelatedModule = s.RelatedModule,
                ApplicabilityStatus = s.Applicability,
                VerificationStatus = s.Verification,
                PEMSImplementationStatus = s.ImplStatus,
                EvidencePath = s.EvidencePath,
                CreatedDate = DateTime.Now
            });
        }

        await db.SaveChangesAsync();

        // طبق «PEMS Help Phase 4 — Increment 3A.1» بخش ۶: در ممیزی این افزایش مشخص شد ۷ از ۸
        // استانداردِ seed شده در Increment 3A با VerificationStatus=PublisherMetadataVerified
        // ثبت شده بودند در حالی‌که OfficialReferenceUrl آن‌ها واقعاً از دامنه‌ی اصلیِ خودِ سازمان
        // صادرکننده (iso.org/sae.org/api.org/webstore.iec.ch) با WebSearch همان افزایش تایید
        // شده بود - یعنی طبق تعریف روشن‌شده‌ی این وضعیت (پایین در HelpStandardReference.cs)
        // باید OfficialSourceVerified می‌بودند. این Backfill یک‌بارِ ایدمپوتنت فقط همین ۷ رکورد
        // از‌قبل‌seed‌شده را اصلاح می‌کند - محتوای مقاله دست‌نخورده می‌ماند.
        var verificationBackfill = standards.ToDictionary(s => s.Identifier, s => s.Verification);
        var existingStandards = await db.HelpStandardReferences.ToListAsync();
        foreach (var existing in existingStandards)
        {
            if (verificationBackfill.TryGetValue(existing.Identifier, out var correctStatus) && existing.VerificationStatus != correctStatus)
                existing.VerificationStatus = correctStatus;
        }
        await db.SaveChangesAsync();

        await LinkRelatedAsync(db, concepts.Select(a => (a.Code, a.RelatedCodes))
            .Concat(disciplines.Select(a => (a.Code, a.RelatedCodes)))
            .Concat(standards.Select(s => (s.Code, s.RelatedCodes)))
            .ToList());

        // طبق «PEMS Help Phase 4 — Increment 3A.1» بخش ۷ (Reverse Cross-Link Closure): پیوندهای
        // بالا فقط یک‌طرفه‌اند (مقاله‌ی جدید → مقاله‌ی پایه) - چون HelpService.ToDetailDto فقط
        // article.RelatedArticles (خروجی) را می‌خواند، نه پیوندهای ورودی، مقاله‌ی پایه هرگز
        // مقاله‌ی جدیدِ مرتبط را در صفحه‌ی خودش نمی‌بیند مگر یک ردیف معکوسِ صریح هم ثبت شود.
        // این‌جا عمداً منطق کوئری HelpService تغییر نکرد (که رفتار نمایشِ هر مقاله‌ی دیگر در کل
        // سیستم را هم تحت تاثیر قرار می‌داد) - به‌جایش، فقط همین جفت‌های مشخص و شواهددارِ زیر
        // (پایه ← جدید) اضافه می‌شوند، نه یک اتصال کورکورانه‌ی هر مقاله به هر مفهوم/استاندارد.
        await LinkRelatedAsync(db, BuildReverseLinks());

        // طبق «PEMS Help Phase 4 - Increment 3C» بند ۹ (Outcome B، اصلاح هدفمند): همان الگوی
        // پچِ ایدمپوتنتِ HelpContentSeeder - فقط اگر متنِ دقیقِ نسخه‌ی قبل از اصلاح هنوز موجود
        // باشد جایگزین می‌شود؛ اجرای دوباره بی‌اثر است.
        await PatchMtbfMttrAvailabilityConceptCaveatAsync(db);
    }

    private static async Task PatchMtbfMttrAvailabilityConceptCaveatAsync(PEMSDbContext db)
    {
        var article = await db.HelpArticles
            .Include(x => x.Translations)
            .FirstOrDefaultAsync(x => x.Code == "concept-mtbf-mttr-availability");
        if (article == null) return;

        var en = article.Translations.FirstOrDefault(t => t.LanguageCode == "en");
        if (en != null)
        {
            const string oldEn = "Availability formula. All three are calculated live from real data - never manually entered.</p>";
            const string newEn = "Availability formula. All three are calculated live from real data - never manually entered. " +
                "<strong>Verified limitation:</strong> Availability is not mathematically bounded to 0-100% - PEMS does not " +
                "currently guard MTTR against an invalid negative Work Order duration.</p>";
            if (en.Content.Contains(oldEn))
                en.Content = en.Content.Replace(oldEn, newEn);
        }

        var fa = article.Translations.FirstOrDefault(t => t.LanguageCode == "fa");
        if (fa != null)
        {
            const string oldFa = "هر سه به‌صورت زنده از داده‌ی واقعی محاسبه می‌شوند - هرگز دستی وارد نمی‌شوند.</p>";
            const string newFa = "هر سه به‌صورت زنده از داده‌ی واقعی محاسبه می‌شوند - هرگز دستی وارد نمی‌شوند. " +
                "<strong>محدودیت تاییدشده:</strong> Availability از نظر ریاضی به بازه‌ی ۰-۱۰۰٪ محدود نیست - PEMS در حال " +
                "حاضر MTTR را در برابر مدت منفیِ نامعتبرِ دستور کار محافظت نمی‌کند.</p>";
            if (fa.Content.Contains(oldFa))
                fa.Content = fa.Content.Replace(oldFa, newFa);
        }

        await db.SaveChangesAsync();
    }

    private static List<(string Code, string[]? RelatedCodes)> BuildReverseLinks() => new()
    {
        // Maintenance Strategy ↔ RCM/ISO 14224/SAE JA1011 - طبق concept-maintenance-strategy:
        // «Can be generated automatically when an approved RCM Decision is Applied»
        ("maintenance-strategy-overview", new[] { "concept-rcm", "standard-iso-14224", "standard-sae-ja1011" }),

        // Work Order ↔ ISO 14224 (طبق CLAUDE.md: فیلدهای ISO14224 دستور کار) + Maintenance Strategy
        ("work-order-details", new[] { "standard-iso-14224", "concept-maintenance-strategy" }),

        // Functional Location vs Equipment ↔ همان مفهوم در واژه‌نامه
        ("functional-location-vs-equipment", new[] { "concept-functional-location" }),

        // Document Workflow/Revisions ↔ مفهوم بازنگری مدرک
        ("document-workflow", new[] { "concept-document-revision" }),
        ("document-revisions-overview", new[] { "concept-document-revision" }),

        // Permits ↔ مفاهیم Permit-to-Work/Isolation-LOTO
        ("permits-overview", new[] { "concept-permit-to-work", "concept-isolation-loto" }),

        // QC/NCR ↔ ISO 9001 (طبق کامنت کدِ خودِ NcrApprovalWorkflowSeeder که صریحاً بند ۸.۷ را ارجاع می‌دهد)
        ("qc-qa-overview", new[] { "standard-iso-9001" }),
        ("ncr-critical-approval-workflow", new[] { "standard-iso-9001" }),

        // RCM/FMEA ↔ ISO 14224 + SAE JA1011/JA1012 (منابع اصلیِ روش‌شناسی این ماژول)
        ("rcm-fmea-overview", new[] { "standard-iso-14224", "standard-sae-ja1011", "standard-sae-ja1012" }),

        // Criticality Assessment ↔ ISO 55001 (تفکر ریسک‌محورِ مدیریت دارایی)
        ("criticality-assessment-overview", new[] { "standard-iso-55001" }),

        // Instrument Passport ↔ IEC 61508/61511 (طبق فیلد SIL واقعاً ثبت‌شده روی رکورد ابزار دقیق)
        ("instrument-passport", new[] { "standard-iec-61508", "standard-iec-61511" }),

        // طبق «PEMS Help Phase 4 — Increment 3B»: reliability-overview در همین افزایش (توسط
        // HelpContentSeeder، پیش از این متد) ساخته می‌شود - پس این‌جا هم در دسترس است. جهت رفتِ
        // آن (reliability-overview → rcm-fmea-overview/criticality-assessment-overview/
        // maintenance-strategy-overview/concept-mtbf.../concept-bad-actor) از قبل در خودِ
        // HelpContentSeeder/BuildConcepts ثبت شده؛ این‌جا فقط جهتِ معکوسِ سه مقاله‌ی پایه به آن
        // اضافه می‌شود - همان الگوی «پایه ← جدید» بالا.
        ("rcm-fmea-overview", new[] { "reliability-overview" }),
        ("criticality-assessment-overview", new[] { "reliability-overview" }),
        ("maintenance-strategy-overview", new[] { "reliability-overview" }),
    };

    private static async Task LinkRelatedAsync(PEMSDbContext db, List<(string Code, string[]? RelatedCodes)> items)
    {
        var idByCode = await db.HelpArticles.ToDictionaryAsync(x => x.Code, x => x.HelpArticleId);

        foreach (var (code, relatedCodes) in items)
        {
            if (relatedCodes == null || relatedCodes.Length == 0) continue;
            if (!idByCode.TryGetValue(code, out var articleId)) continue;

            foreach (var relatedCode in relatedCodes)
            {
                if (!idByCode.TryGetValue(relatedCode, out var relatedId)) continue;
                if (relatedId == articleId) continue; // هرگز Self-Link نمی‌سازد
                var exists = await db.HelpRelatedArticles.AnyAsync(r => r.HelpArticleId == articleId && r.RelatedHelpArticleId == relatedId);
                if (exists) continue;
                db.HelpRelatedArticles.Add(new HelpRelatedArticle { HelpArticleId = articleId, RelatedHelpArticleId = relatedId });
            }
        }

        await db.SaveChangesAsync();
    }

    // ============================================================================
    // Concepts (15) - هر کدام مستقیماً از کدِ واقعاً بررسی‌شده در Increment های ۱/۲ این جلسه
    // ============================================================================
    private static List<Art> BuildConcepts() => new()
    {
        new Art("concept-idc", "concepts", "Concept", 0,
            "IDC (Inter-Discipline Check)",
            "An internal, advisory review step - no single discipline's result can reject a document by itself.",
            "<p><strong>Generic meaning:</strong> a review of an engineering document by other disciplines it affects, before formal approval.</p>" +
            "<p><strong>PEMS implementation (verified):</strong> the originating discipline nominates other disciplines to review a submitted document in parallel; each records No Comment / Approve with Comments / Revision Required independently. No individual result sets the document's own status to Rejected - only after every nominated discipline has responded does the document reach the formal Approval stage, where the real decision is made.</p>",
            "idc,inter-discipline check,discipline review",
            "IDC (بازبینی بین‌دیسیپلینی)",
            "یک مرحله‌ی بازبینی داخلی و مشورتی - نتیجه‌ی هیچ دیسیپلینی به‌تنهایی نمی‌تواند مدرک را رد کند.",
            "<p><strong>معنای عمومی:</strong> بازبینی یک مدرک مهندسی توسط سایر دیسیپلین‌های متاثر، پیش از تایید رسمی.</p>" +
            "<p><strong>پیاده‌سازی در PEMS (تاییدشده):</strong> دیسیپلین سازنده سایر دیسیپلین‌ها را برای بازبینی موازی مدرک ارسال‌شده معرفی می‌کند؛ هر کدام مستقل بدون نظر/تایید با نظر/نیازمند اصلاح را ثبت می‌کند. هیچ نتیجه‌ی منفردی وضعیت خودِ مدرک را Rejected نمی‌کند - فقط پس از پاسخ همه، مدرک به مرحله‌ی رسمی Approval می‌رسد.</p>",
            "idc,بازبینی بین دیسیپلینی",
            new[] { "document-workflow", "idc-inter-discipline-check" }),

        new Art("concept-mdr", "concepts", "Concept", 1,
            "MDR",
            "Commonly \"Master Document Register\" industry-wide - PEMS itself uses the term literally without defining an expansion in code.",
            "<p><strong>Generic meaning:</strong> in engineering document control, MDR commonly stands for \"Master Document Register\" (or \"Master Deliverables Register\") - the master list of all documents/deliverables a project must produce, with their planned and actual issue status.</p>" +
            "<p><strong>PEMS implementation (verified):</strong> a staged Excel importer (MDRImportController/MDRImportService) that loads an MDR spreadsheet, matches rows to disciplines, and commits them following the same staged batch pattern as every other PEMS importer (see \"How Staged Imports Work\"). The codebase never spells out the expansion itself - this page states the common industry meaning explicitly as external convention, not as a PEMS-internal definition.</p>",
            "mdr,master document register,document control",
            "MDR",
            "معمولاً «Master Document Register» طبق قرارداد صنعتی - خودِ PEMS این عبارت را عیناً استفاده می‌کند بدون تعریف بسط آن در کد.",
            "<p><strong>معنای عمومی:</strong> در کنترل مدارک مهندسی، MDR معمولاً به «Master Document Register» (یا «Master Deliverables Register») اشاره دارد - فهرست اصلی همه‌ی مدارک/تحویل‌دادنی‌هایی که یک پروژه باید تولید کند.</p>" +
            "<p><strong>پیاده‌سازی در PEMS (تاییدشده):</strong> یک Import‌کننده‌ی مرحله‌ای اکسل (MDRImportController/MDRImportService) که یک صفحه‌گسترده‌ی MDR را بارگذاری، ردیف‌ها را با دیسیپلین‌ها تطبیق، و طبق همان الگوی Batch مرحله‌ای بقیه‌ی Import‌کننده‌های PEMS Commit می‌کند. خودِ کد هرگز بسط را ننوشته - این صفحه معنای رایج صنعتی را صریحاً به‌عنوان قرارداد بیرونی بیان می‌کند، نه تعریف داخلی PEMS.</p>",
            "mdr,ثبت اصلی مدارک",
            new[] { "import-batches-overview" }),

        new Art("concept-vdr", "concepts", "Concept", 2,
            "VDR (Vendor Document Register)",
            "The register of documents a vendor/contractor must submit for review, with discipline and lead-engineer review stages.",
            "<p><strong>Generic meaning:</strong> the register of documents a vendor or contractor is contractually required to submit for the purchaser's review (datasheets, drawings, certificates, manuals).</p>" +
            "<p><strong>PEMS implementation (verified):</strong> a VDR item moves through discipline review then lead-engineer review categorization; it has its own live-generated workflow diagram (VendorDocument/Review). Terminal review-outcome codes (AP/NC/AC/CO/RE) are recorded literally - the codebase does not define their expansion, so PEMS Help never guesses it. A vendor's Risk Score is calculated entirely from its VDR submission history (overdue rate, resubmission rate, closure rate).</p>",
            "vdr,vendor document register,vendor review",
            "VDR (رجیستر مدارک وندور)",
            "رجیستر مدارکی که وندور/پیمانکار باید برای بازبینی ارسال کند، با مراحل بازبینی دیسیپلین و سرپرست.",
            "<p><strong>معنای عمومی:</strong> رجیستر مدارکی که یک وندور یا پیمانکار قراردادی موظف است برای بازبینی خریدار ارسال کند (دیتاشیت، نقشه، گواهینامه، دفترچه راهنما).</p>" +
            "<p><strong>پیاده‌سازی در PEMS (تاییدشده):</strong> یک آیتم VDR از بازبینی دیسیپلین سپس دسته‌بندیِ بازبینی سرپرست مهندسی عبور می‌کند؛ نمودار گردش‌کار زنده‌ی اختصاصی خودش را دارد (VendorDocument/Review). کدهای نتیجه‌ی پایانی بازبینی (AP/NC/AC/CO/RE) عیناً ثبت می‌شوند - کد بسط آن‌ها را تعریف نمی‌کند، پس راهنمای PEMS هرگز حدس نمی‌زند. امتیاز ریسک یک وندور کاملاً از تاریخچه‌ی ارسال VDR آن محاسبه می‌شود.</p>",
            "vdr,رجیستر مدارک وندور",
            new[] { "vdr-overview", "vendors-overview" }),

        new Art("concept-document-revision", "concepts", "Concept", 3,
            "Document Revision",
            "A tracked change to an engineering document, created only through the document's own workflow - never directly.",
            "<p><strong>Generic meaning:</strong> a distinct, numbered version of an engineering document issued after changes, superseding the previous version.</p>" +
            "<p><strong>PEMS implementation (verified):</strong> revisions are created exclusively via \"Create New Revision\" on the document's own Details page, which correctly numbers it, records who created it, and supersedes the prior revision. A previously-orphaned direct-create path was found and disabled - it bypassed this and let status/numbering be set arbitrarily. No revision comparison/diff feature currently exists.</p>",
            "document revision,revision history,baseline",
            "بازنگری مدرک",
            "یک تغییر ردگیری‌شده روی مدرک مهندسی، که فقط از طریق گردش‌کار خودِ مدرک ساخته می‌شود - نه مستقیم.",
            "<p><strong>معنای عمومی:</strong> یک نسخه‌ی متمایز و شماره‌گذاری‌شده‌ی مدرک مهندسی که پس از تغییرات صادر می‌شود و نسخه‌ی قبلی را جایگزین می‌کند.</p>" +
            "<p><strong>پیاده‌سازی در PEMS (تاییدشده):</strong> بازنگری‌ها منحصراً از طریق «Create New Revision» روی صفحه‌ی جزئیات خودِ مدرک ساخته می‌شوند، که به‌درستی شماره‌گذاری می‌کند، سازنده را ثبت می‌کند، و بازنگری قبلی را جایگزین می‌کند. یک مسیر مستقیمِ قبلاً یتیم پیدا و غیرفعال شد - چون این را دور می‌زد. در حال حاضر قابلیت مقایسه/Diff بازنگری وجود ندارد.</p>",
            "بازنگری مدرک,تاریخچه بازنگری",
            new[] { "document-revisions-overview", "document-workflow" }),

        new Art("concept-transmittal", "concepts", "Concept", 4,
            "Transmittal",
            "The formal cover record of what was sent, to whom, and when - with its own sent/response/acknowledge lifecycle.",
            "<p><strong>Generic meaning:</strong> a formal cover document/record accompanying a document package sent between parties, recording what was sent, to whom, and when, often requiring acknowledgement or a response by a due date.</p>" +
            "<p><strong>PEMS implementation (verified):</strong> a Transmittal is created, marked Sent (which sets a response-due date computed in working days), can be Cancelled and later Reissued, and separately records the recipient's Response and Acknowledgement.</p>",
            "transmittal,cover letter,document transmission",
            "ترنسمیتال",
            "رکورد رسمیِ این‌که چه چیزی، به چه کسی، و چه زمانی ارسال شده - با چرخه‌ی ارسال/پاسخ/تایید مخصوص خودش.",
            "<p><strong>معنای عمومی:</strong> یک مدرک/رکورد رسمیِ همراهِ یک بسته‌ی مدرک که بین طرفین ارسال می‌شود، و ثبت می‌کند چه چیزی، به چه کسی، و چه زمانی ارسال شده، اغلب نیازمند تایید یا پاسخ تا یک تاریخ سررسید.</p>" +
            "<p><strong>پیاده‌سازی در PEMS (تاییدشده):</strong> یک Transmittal ساخته می‌شود، Sent علامت می‌خورد (که تاریخ سررسید پاسخ را بر اساس روزهای کاری محاسبه می‌کند)، می‌تواند Cancelled و بعداً Reissue شود، و جداگانه پاسخ و تاییدِ گیرنده را ثبت می‌کند.</p>",
            "ترنسمیتال,ارسال مدرک",
            new[] { "transmittals-overview" }),

        new Art("concept-moc", "concepts", "Concept", 5,
            "MOC (Management of Change)",
            "A structured process for proposing, assessing and approving a change - with a risk-based extra approval gate.",
            "<p><strong>Generic meaning:</strong> a structured process (standard practice under Process Safety Management) for proposing, assessing, and approving any change - permanent or temporary - to equipment, process, or procedure, so consequences are evaluated before implementation.</p>" +
            "<p><strong>PEMS implementation (verified):</strong> Pending Risk Assessment → Pending Approval → (High risk only) Pending Senior Approval → Approved → Pending Closure → Closed, with Reject exits. Closure is blocked unless affected documents are marked updated and, if flagged, required training is marked complete.</p>",
            "moc,management of change,risk level",
            "MOC (مدیریت تغییر)",
            "فرآیندی ساختاریافته برای پیشنهاد، ارزیابی و تایید یک تغییر - با یک دروازه‌ی تایید اضافه‌ی مبتنی بر ریسک.",
            "<p><strong>معنای عمومی:</strong> فرآیندی ساختاریافته (رویه‌ی استاندارد تحت Process Safety Management) برای پیشنهاد، ارزیابی و تایید هر تغییر - دائم یا موقت - در تجهیز، فرآیند، یا رویه، تا پیامدها پیش از اجرا ارزیابی شوند.</p>" +
            "<p><strong>پیاده‌سازی در PEMS (تاییدشده):</strong> Pending Risk Assessment → Pending Approval → (فقط ریسک بالا) Pending Senior Approval → Approved → Pending Closure → Closed، با خروجی‌های Reject. بستن مسدود است مگر مدارک متاثر به‌روزرسانی‌شده علامت بخورند و، اگر لازم بود، آموزش تکمیل‌شده علامت بخورد.</p>",
            "moc,مدیریت تغییر",
            new[] { "moc-overview" }),

        new Art("concept-ncr", "concepts", "Concept", 6,
            "NCR (Non-Conformance Report)",
            "A record of a product/process that fails to meet requirements - Critical severity requires an extra independent approval.",
            "<p><strong>Generic meaning:</strong> a formal record raised when a product, material, or process is found not to meet specified requirements, tracked through disposition (how it will be handled) to closure - a core concept under ISO 9001 §8.7 (\"control of nonconforming outputs\").</p>" +
            "<p><strong>PEMS implementation (verified):</strong> Open → (disposition recorded) → Dispositioned directly for Minor/Major severity, or via a Critical-only independent QA Manager approval gate (Pending Approval) for Critical severity → Closed.</p>",
            "ncr,non-conformance,disposition,iso 9001",
            "NCR (گزارش عدم‌انطباق)",
            "رکورد یک محصول/فرآیندی که الزامات را برآورده نمی‌کند - شدت Critical نیازمند تایید مستقل اضافه است.",
            "<p><strong>معنای عمومی:</strong> رکوردی رسمی که وقتی یک محصول، ماده، یا فرآیند الزامات مشخص‌شده را برآورده نکند ثبت می‌شود، و تا بسته‌شدن از طریق Disposition (چگونگی رسیدگی) ردیابی می‌شود - مفهومی اصلی تحت ISO 9001 بند ۸.۷ («کنترل خروجی‌های نامنطبق»).</p>" +
            "<p><strong>پیاده‌سازی در PEMS (تاییدشده):</strong> Open → (Disposition ثبت می‌شود) → مستقیماً Dispositioned برای شدت Minor/Major، یا از طریق دروازه‌ی تایید مستقل مدیر QA (فقط برای Critical) → Closed.</p>",
            "ncr,عدم انطباق,disposition",
            new[] { "ncr-critical-approval-workflow", "qc-qa-overview" }),

        new Art("concept-technical-query", "concepts", "Concept", 7,
            "TQ / TSR / RFI",
            "A formal question raised during engineering execution, answered by a responsible discipline and confirmed by the raiser.",
            "<p><strong>Generic meaning:</strong> a formal mechanism for raising a question or requesting clarification/information during project execution; RFI (\"Request for Information\") is a widely-used industry term. The codebase defines \"TQ\" as \"Technical Query\"; it does not define an expansion for \"TSR\", so it is shown as-is.</p>" +
            "<p><strong>PEMS implementation (verified):</strong> Open → (responsible discipline responds) → Pending Confirmation → Closed (confirmed) or Reopened (rejected, looping back to the responding discipline).</p>",
            "tq,tsr,rfi,technical query,request for information",
            "TQ / TSR / RFI",
            "یک پرسش رسمی که در طول اجرای مهندسی مطرح می‌شود، توسط یک دیسیپلین مسئول پاسخ داده و توسط ثبت‌کننده تایید می‌شود.",
            "<p><strong>معنای عمومی:</strong> مکانیزمی رسمی برای طرح یک پرسش یا درخواست توضیح/اطلاعات در طول اجرای پروژه؛ RFI («درخواست اطلاعات») اصطلاحی رایج در صنعت است. کد پایه TQ را «Technical Query» تعریف می‌کند؛ بسطی برای TSR تعریف نشده، پس همین‌طور نمایش داده می‌شود.</p>" +
            "<p><strong>پیاده‌سازی در PEMS (تاییدشده):</strong> Open → (دیسیپلین مسئول پاسخ می‌دهد) → Pending Confirmation → Closed (تاییدشده) یا Reopened (رد شده، بازگشت به دیسیپلین پاسخ‌دهنده).</p>",
            "tq,tsr,rfi,پرسش فنی",
            new[] { "technical-query-overview" }),

        new Art("concept-fmea", "concepts", "Concept", 8,
            "FMEA (Failure Mode and Effects Analysis)",
            "A systematic method for identifying failure modes and prioritizing them by a Risk Priority Number.",
            "<p><strong>Generic meaning:</strong> a systematic engineering method for identifying potential failure modes of a system/equipment, their causes and effects, and prioritizing them for attention - widely used across reliability engineering, not tied to one specific standard.</p>" +
            "<p><strong>PEMS implementation (verified):</strong> built per equipment class (not per individual equipment); each failure mode gets a Risk Priority Number, calculated (never stored) as Severity × Occurrence × Detectability (each 1-10). Follows a Draft → Under Review → Approved lifecycle, superseding the prior approved analysis for that class. An approved FMEA is the required starting point for an RCM analysis.</p>",
            "fmea,fmeca,risk priority number,rpn,failure mode",
            "FMEA (تحلیل حالات خرابی و اثرات آن)",
            "روشی نظام‌مند برای شناسایی حالات خرابی و اولویت‌بندی آن‌ها با عدد اولویت ریسک.",
            "<p><strong>معنای عمومی:</strong> روشی مهندسیِ نظام‌مند برای شناسایی حالات خرابیِ بالقوه‌ی یک سیستم/تجهیز، علل و اثرات آن‌ها، و اولویت‌بندی برای توجه - به‌طور گسترده در مهندسی قابلیت‌اطمینان استفاده می‌شود، وابسته به یک استاندارد خاص نیست.</p>" +
            "<p><strong>پیاده‌سازی در PEMS (تاییدشده):</strong> به‌ازای هر کلاس تجهیز ساخته می‌شود (نه هر تجهیز مجزا)؛ هر حالت خرابی یک عدد اولویت ریسک می‌گیرد، محاسبه‌شده (هرگز ذخیره‌نشده) به‌صورت شدت × وقوع × قابلیت‌تشخیص (هرکدام ۱ تا ۱۰). از چرخه‌ی Draft → Under Review → Approved پیروی می‌کند. یک FMEA تاییدشده نقطه‌ی شروع الزامیِ یک تحلیل RCM است.</p>",
            "fmea,fmeca,عدد اولویت ریسک",
            new[] { "rcm-fmea-overview" }),

        new Art("concept-rcm", "concepts", "Concept", 9,
            "RCM (Reliability-Centered Maintenance)",
            "A structured process for deciding the right maintenance task for each failure mode, built from an approved FMEA.",
            "<p><strong>Generic meaning:</strong> a structured process for determining what maintenance must be done to ensure any physical asset continues to fulfil its intended functions, by systematically evaluating each significant failure mode. SAE JA1011 defines the evaluation criteria a process must meet to be called RCM.</p>" +
            "<p><strong>PEMS implementation (verified):</strong> an RCM analysis can only be started from an Approved FMEA for that equipment class, following the same Draft/Submit/Approve pattern at both the analysis level and per individual maintenance-task Decision. Applying an approved decision creates a real Maintenance Strategy for one specific equipment instance.</p>",
            "rcm,reliability centered maintenance,maintenance decision",
            "RCM (نگهداری متمرکز بر قابلیت‌اطمینان)",
            "فرآیندی ساختاریافته برای تعیین کار نگهداری درست برای هر حالت خرابی، ساخته‌شده از یک FMEA تاییدشده.",
            "<p><strong>معنای عمومی:</strong> فرآیندی ساختاریافته برای تعیین این‌که چه نگهداری‌ای باید انجام شود تا یک دارایی فیزیکی به انجام کارکردهای موردنظرش ادامه دهد، با ارزیابی نظام‌مند هر حالت خرابی مهم. SAE JA1011 معیارهای ارزیابیِ لازم برای نامیدن یک فرآیند «RCM» را تعریف می‌کند.</p>" +
            "<p><strong>پیاده‌سازی در PEMS (تاییدشده):</strong> یک تحلیل RCM فقط از یک FMEA Approved همان کلاس تجهیز قابل‌شروع است، با همان الگوی Draft/Submit/Approve هم در سطح تحلیل هم برای هر Decision. Apply کردن یک تصمیم تاییدشده یک استراتژی نگهداری واقعی برای یک تجهیز مشخص می‌سازد.</p>",
            "rcm,نگهداری متمرکز بر قابلیت اطمینان",
            new[] { "rcm-fmea-overview", "concept-fmea" }),

        new Art("concept-criticality", "concepts", "Concept", 10,
            "Equipment Criticality",
            "The worst-dimension-governs scoring rule PEMS uses to classify equipment as A/B/C - not an average.",
            "<p><strong>Generic meaning:</strong> a ranking of equipment by the consequence of its failure (commonly safety, production, environment, cost), used to prioritize maintenance strategy, spares, and inspection effort - a common asset-management concept, related to the risk-based thinking behind ISO 55000.</p>" +
            "<p><strong>PEMS implementation (verified):</strong> a Criticality Assessment scores Safety/Production/Environment 1-5; the class comes from the maximum score, not the average (score 4-5 → Class A, 3 → Class B, 1-2 → Class C) - PEMS's own project-defined methodology (PEMS-CRIT-1.0), not a claim of external standard compliance. Approving an assessment is what updates the equipment's real Criticality Class.</p>",
            "criticality,criticality class,risk ranking",
            "بحرانی‌بودن تجهیز",
            "قاعده‌ی «بدترین بُعد حاکم است» که PEMS برای طبقه‌بندی تجهیز به A/B/C استفاده می‌کند - نه میانگین.",
            "<p><strong>معنای عمومی:</strong> رتبه‌بندی تجهیزات بر اساس پیامد خرابی آن‌ها (معمولاً ایمنی، تولید، محیط‌زیست، هزینه)، برای اولویت‌بندی استراتژی نگهداری، قطعات یدکی، و تلاش بازرسی - مفهومی رایج در مدیریت دارایی، مرتبط با تفکر ریسک‌محور پشت ISO 55000.</p>" +
            "<p><strong>پیاده‌سازی در PEMS (تاییدشده):</strong> یک ارزیابی بحرانی‌بودن ایمنی/تولید/محیط‌زیست را از ۱ تا ۵ نمره می‌دهد؛ کلاس از بیشینه‌ی نمره می‌آید، نه میانگین (نمره ۴-۵ → کلاس A، ۳ → کلاس B، ۱-۲ → کلاس C) - روش‌شناسیِ پروژه‌ایِ خودِ PEMS (PEMS-CRIT-1.0)، نه ادعای انطباق با استانداردی بیرونی.</p>",
            "بحرانی بودن,کلاس بحرانی بودن",
            new[] { "criticality-assessment-overview" }),

        new Art("concept-permit-to-work", "concepts", "Concept", 11,
            "Permit to Work",
            "A formal, auditable authorization for hazardous work, moving through a real approval chain before work may start.",
            "<p><strong>Generic meaning:</strong> a formal, documented system that authorizes specific hazardous work to be carried out at a specific time, under specific control measures, by specific authorized people - a core element of process safety management.</p>" +
            "<p><strong>PEMS implementation (verified):</strong> Draft → Submitted → Under Review → Approved → Issued → Active → Closed, with Suspended/Cancelled/Rejected/Revoked exits, and a return-to-Draft rework path from Under Review. Nine verified types (Cold Work, Hot Work, Confined Space, Electrical, Excavation, Working at Height, Radiography, Line Breaking, Other). Starting actual work and its emergency-override path require WebAuthn step-up confirmation.</p>",
            "permit to work,ptw,hot work,confined space",
            "مجوز کار (Permit to Work)",
            "یک مجوز رسمی و قابل‌ممیزی برای کار خطرناک، که پیش از شروع کار از یک زنجیره‌ی تایید واقعی عبور می‌کند.",
            "<p><strong>معنای عمومی:</strong> سیستمی رسمی و مستندشده که کار خطرناک مشخصی را در زمان مشخص، تحت اقدامات کنترلی مشخص، توسط افراد مجاز مشخص، مجاز می‌کند - عنصری اصلی در مدیریت ایمنی فرآیند.</p>" +
            "<p><strong>پیاده‌سازی در PEMS (تاییدشده):</strong> Draft → Submitted → Under Review → Approved → Issued → Active → Closed، با خروجی‌های Suspended/Cancelled/Rejected/Revoked، و مسیر بازگشت به Draft برای اصلاح از Under Review. نُه نوع تاییدشده. شروع واقعیِ کار و مسیر بازگشایی اضطراری نیازمند تایید گام دومِ WebAuthn‌اند.</p>",
            "مجوز کار,permit to work",
            new[] { "permit-details" }),

        new Art("concept-isolation-loto", "concepts", "Concept", 12,
            "Isolation / LOTO",
            "The physical process of isolating equipment energy sources, with its own Verified step gated by a passkey confirmation.",
            "<p><strong>Generic meaning:</strong> Lock-Out/Tag-Out - the physical isolation of hazardous energy sources (electrical, mechanical, process) before work, with a lock and tag preventing accidental reactivation, and a verification step confirming isolation is genuinely in place.</p>" +
            "<p><strong>PEMS implementation (verified):</strong> an Isolation Point moves Requested → Isolated → Verified → Deisolated (or Requested → Cancelled), embedded within a Permit's Details page rather than having its own standalone page. Verifying an isolation requires a WebAuthn step-up confirmation, not just being logged in.</p>",
            "loto,lock out tag out,isolation point",
            "ایزوله‌سازی / LOTO",
            "فرآیند فیزیکیِ ایزوله‌کردن منابع انرژی تجهیز، با یک مرحله‌ی تاییدشده که با تایید پس‌کلید محافظت می‌شود.",
            "<p><strong>معنای عمومی:</strong> Lock-Out/Tag-Out - ایزوله‌سازی فیزیکیِ منابع انرژی خطرناک (برق، مکانیکی، فرآیندی) پیش از کار، با قفل و برچسبی که از فعال‌سازی تصادفی جلوگیری می‌کند، و یک مرحله‌ی تایید که واقعاً ایزوله‌بودن را تایید می‌کند.</p>" +
            "<p><strong>پیاده‌سازی در PEMS (تاییدشده):</strong> یک نقطه‌ی ایزوله‌سازی از Requested → Isolated → Verified → Deisolated عبور می‌کند (یا Requested → Cancelled)، تعبیه‌شده در صفحه‌ی جزئیات یک Permit، نه صفحه‌ی مستقل خودش. تایید یک ایزوله‌سازی نیازمند تایید گام دومِ WebAuthn است.</p>",
            "loto,ایزوله سازی",
            new[] { "permit-details" }),

        new Art("concept-functional-location", "concepts", "Concept", 13,
            "Functional Location",
            "A position/role in the plant's structure - stays fixed even when the physical equipment installed there is swapped.",
            "<p><strong>Generic meaning:</strong> a place or role within a plant's technical structure (an area, a system, an installation point), independent of which physical asset currently occupies it.</p>" +
            "<p><strong>PEMS implementation (verified):</strong> Equipment optionally links to a Functional Location via Equipment.FunctionalLocationId - the Functional Location itself never depends on which equipment is currently installed, so maintenance history/planning tied to \"this position\" survives equipment swaps.</p>",
            "functional location,fl,plant structure",
            "موقعیت فنی",
            "یک جایگاه/نقش در ساختار واحد - حتی وقتی تجهیز فیزیکیِ نصب‌شده در آن‌جا عوض شود، ثابت می‌ماند.",
            "<p><strong>معنای عمومی:</strong> یک جایگاه یا نقش در ساختار فنیِ واحد (یک منطقه، یک سیستم، یک نقطه‌ی نصب)، مستقل از این‌که کدام دارایی فیزیکی فعلاً آن‌جا را اشغال کرده.</p>" +
            "<p><strong>پیاده‌سازی در PEMS (تاییدشده):</strong> تجهیز به‌صورت اختیاری از طریق Equipment.FunctionalLocationId به یک موقعیت فنی وصل می‌شود - خودِ موقعیت فنی هرگز به تجهیز فعلاً نصب‌شده وابسته نیست.</p>",
            "موقعیت فنی",
            new[] { "functional-location-vs-equipment" }),

        new Art("concept-maintenance-strategy", "concepts", "Concept", 14,
            "Maintenance Strategy",
            "The link between an equipment and its recurring maintenance plan - can be authored manually or produced by an approved RCM decision.",
            "<p><strong>Generic meaning:</strong> the defined approach (preventive/predictive/corrective, and its triggers) for maintaining a specific piece of equipment, as opposed to relying on ad-hoc reactive work.</p>" +
            "<p><strong>PEMS implementation (verified):</strong> defined per equipment - strategy type, trigger type (e.g. calendar-based), review frequency, and optionally the failure mode/consequence category it addresses. Can be created directly by a user, or generated automatically when an approved RCM Decision is Applied to a specific equipment instance.</p>",
            "maintenance strategy,preventive maintenance,trigger",
            "استراتژی نگهداری",
            "پیوند میان یک تجهیز و برنامه‌ی نگهداری تکرارشونده‌اش - می‌تواند دستی نوشته شود یا از یک تصمیم تاییدشده‌ی RCM تولید شود.",
            "<p><strong>معنای عمومی:</strong> رویکرد تعریف‌شده (پیشگیرانه/پیش‌بینانه/اصلاحی، و محرک‌های آن) برای نگهداری یک تجهیز مشخص، در برابر تکیه بر کار واکنشیِ موردی.</p>" +
            "<p><strong>پیاده‌سازی در PEMS (تاییدشده):</strong> به‌ازای هر تجهیز تعریف می‌شود - نوع استراتژی، نوع محرک، دوره‌ی بازبینی، و به‌صورت اختیاری حالت خرابی/دسته‌ی پیامد. می‌تواند مستقیم توسط کاربر ساخته شود، یا وقتی یک Decision تاییدشده‌ی RCM روی یک تجهیز مشخص Apply می‌شود، خودکار تولید شود.</p>",
            "استراتژی نگهداری",
            new[] { "maintenance-strategy-overview", "rcm-fmea-overview" }),

        // ============================= Phase 4 Increment 3B: Reliability =============================
        new Art("concept-mtbf-mttr-availability", "concepts", "Concept", 15,
            "MTBF / MTTR / Availability",
            "The exact reliability-engineering formulas PEMS calculates, and why they require at least 2 real failure records to mean anything.",
            "<p><strong>Generic meaning:</strong> MTBF (Mean Time Between Failures), MTTR (Mean Time To Repair) and " +
            "Availability are standard reliability-engineering metrics used across industry to quantify how often " +
            "equipment fails and how quickly it is restored.</p>" +
            "<p><strong>PEMS implementation (verified, exact formulas):</strong> MTBF = (time span from first to last " +
            "recorded failure) ÷ (failure count − 1) - requires at least 2 Failure Records for an equipment, otherwise " +
            "no value is shown. MTTR = the average of actual Work Order repair durations (Start Date to Completion " +
            "Date) linked to those failures. Availability = MTBF ÷ (MTBF + MTTR) × 100, the standard Inherent " +
            "Availability formula. All three are calculated live from real data - never manually entered. " +
            "<strong>Verified limitation:</strong> Availability is not mathematically bounded to 0-100% - PEMS does not " +
            "currently guard MTTR against an invalid negative Work Order duration.</p>",
            "mtbf,mttr,availability,inherent availability,reliability metrics",
            "MTBF / MTTR / Availability",
            "فرمول‌های دقیقِ مهندسیِ قابلیت‌اطمینانی که PEMS محاسبه می‌کند، و چرا حداقل به ۲ رکورد واقعیِ خرابی نیاز دارند تا معنا داشته باشند.",
            "<p><strong>معنای عمومی:</strong> MTBF (میانگین زمان بین خرابی‌ها)، MTTR (میانگین زمان تعمیر) و Availability " +
            "معیارهای استانداردِ مهندسیِ قابلیت‌اطمینان‌اند که در سراسر صنعت برای کمّی‌کردن این‌که تجهیز چقدر خراب می‌شود " +
            "و چقدر سریع بازیابی می‌شود استفاده می‌شوند.</p>" +
            "<p><strong>پیاده‌سازی در PEMS (تاییدشده، فرمول‌های دقیق):</strong> MTBF = (بازه‌ی زمانی از اولین تا آخرین " +
            "خرابیِ ثبت‌شده) ÷ (تعداد خرابی − ۱) - نیازمند حداقل ۲ رکورد خرابی برای یک تجهیز است، وگرنه هیچ مقداری نشان " +
            "داده نمی‌شود. MTTR = میانگینِ مدت واقعیِ تعمیر دستورهای کارِ مرتبط (تاریخ شروع تا تکمیل). Availability = " +
            "MTBF ÷ (MTBF + MTTR) × ۱۰۰، فرمول استاندارد Inherent Availability. هر سه به‌صورت زنده از داده‌ی واقعی " +
            "محاسبه می‌شوند - هرگز دستی وارد نمی‌شوند. <strong>محدودیت تاییدشده:</strong> Availability از نظر ریاضی به " +
            "بازه‌ی ۰-۱۰۰٪ محدود نیست - PEMS در حال حاضر MTTR را در برابر مدت منفیِ نامعتبرِ دستور کار محافظت نمی‌کند.</p>",
            "mtbf,mttr,availability,معیارهای قابلیت اطمینان",
            new[] { "reliability-overview" }),

        new Art("concept-bad-actor", "concepts", "Concept", 16,
            "Bad Actor",
            "PEMS's verified ranking rule - failure count first, downtime second - plus a real 90-day repeat-failure detection window.",
            "<p><strong>Generic meaning:</strong> in reliability engineering, a \"bad actor\" is a piece of equipment " +
            "that fails disproportionately often or causes disproportionate downtime/cost compared to its peers - a " +
            "common prioritization concept, not tied to one specific standard.</p>" +
            "<p><strong>PEMS implementation (verified):</strong> the top-N worst equipment within a date range, ranked " +
            "by failure count first, then total downtime hours. Each Bad Actor also reports its most frequent failure " +
            "mode and cause, and a \"repeat failure\" count using a verified rule: the same failure mode recurring " +
            "within a 90-day window counts as a repeat.</p>",
            "bad actor,repeat failure,failure ranking",
            "Bad Actor (تجهیز پرخرابی)",
            "قاعده‌ی رتبه‌بندیِ تاییدشده‌ی PEMS - اول تعداد خرابی، دوم ساعات توقف - به‌علاوه‌ی یک پنجره‌ی تشخیص خرابیِ تکراریِ واقعیِ ۹۰روزه.",
            "<p><strong>معنای عمومی:</strong> در مهندسی قابلیت‌اطمینان، «Bad Actor» تجهیزی است که به‌نسبت همتایانش " +
            "به‌طور نامتناسبی زیاد خراب می‌شود یا توقف/هزینه‌ی نامتناسبی ایجاد می‌کند - یک مفهوم رایج اولویت‌بندی، وابسته " +
            "به یک استاندارد خاص نیست.</p>" +
            "<p><strong>پیاده‌سازی در PEMS (تاییدشده):</strong> بدترین N تجهیز در یک بازه‌ی زمانی، ابتدا بر اساس تعداد " +
            "خرابی، سپس مجموع ساعات توقف رتبه‌بندی می‌شوند. هر Bad Actor همچنین پرتکرارترین حالت و علت خرابی‌اش را " +
            "گزارش می‌کند، و یک شمارشِ «خرابی تکراری» با یک قاعده‌ی تاییدشده: تکرار همان حالت خرابی درون پنجره‌ی ۹۰روزه، " +
            "تکراری شمرده می‌شود.</p>",
            "bad actor,خرابی تکراری,رتبه بندی خرابی",
            new[] { "reliability-overview" }),
    };

    // ============================================================================
    // Discipline Guides (4) - فقط دیسیپلین‌هایی که عملکرد PEMS-واقعیِ متمایز و تاییدشده دارند.
    // مکانیک/برق/پایپینگ/فرآیند/مدنی/خوردگی/سیویل عمداً انتخاب نشدند - چون فراتر از برچسب عمومیِ
    // Discipline (نام/دیسیپلین مهندسی برای مسیریابی مدرک/کار)، عملکرد PEMS متمایزی برای آن‌ها
    // تایید نشد.
    // ============================================================================
    private static List<Art> BuildDisciplines() => new()
    {
        new Art("discipline-instrumentation-control", "disciplines", "DisciplineGuide", 0,
            "Instrumentation & Control",
            "Calibration, alarms/cause-and-effect, control loops and a captured SIL value - not full SIS lifecycle management.",
            "<p><strong>Typical concerns:</strong> field instrument specification, calibration, alarm/trip setpoints, cause-and-effect logic, control loop configuration, and safety instrumented function integrity (SIL).</p>" +
            "<p><strong>PEMS functionality (verified):</strong> the Instrument Passport page - append-only calibration history with cached next-due dates, alarms with Cause &amp; Effect entries (including Trip/Shutdown actions), one Control Loop per instrument, I/O Points, and a captured SIL field on the instrument record. A Cause &amp; Effect entry can directly raise a Work Request.</p>" +
            "<p><strong>Explicit limitation:</strong> PEMS captures a SIL value but does not implement SIL verification/calculation or full Safety Instrumented System lifecycle management per IEC 61508/61511 methodology - see the linked standard references.</p>",
            "instrumentation,control,sil,calibration,alarm",
            "ابزار دقیق و کنترل",
            "کالیبراسیون، آلارم/علت‌واثر، حلقه‌های کنترل و یک مقدار SIL ثبت‌شده - نه مدیریت کامل چرخه‌ی عمر SIS.",
            "<p><strong>دغدغه‌های معمول:</strong> مشخصه‌گذاری ابزار دقیق میدانی، کالیبراسیون، نقاط تنظیم آلارم/تریپ، منطق علت‌واثر، پیکربندی حلقه‌ی کنترل، و یکپارچگیِ کارکرد ایمنی ابزاری (SIL).</p>" +
            "<p><strong>عملکرد PEMS (تاییدشده):</strong> صفحه‌ی پاسپورت ابزار دقیق - تاریخچه‌ی کالیبراسیونِ فقط-افزایشی با تاریخ‌های سررسید کش‌شده، آلارم‌ها با رکوردهای علت‌واثر (شامل اقدامات Trip/Shutdown)، یک حلقه‌ی کنترل به‌ازای هر ابزار، نقاط I/O، و یک فیلد SIL ثبت‌شده روی رکورد ابزار.</p>" +
            "<p><strong>محدودیت صریح:</strong> PEMS یک مقدار SIL را ثبت می‌کند اما تایید/محاسبه‌ی SIL یا مدیریت کامل چرخه‌ی عمر سیستم ابزاردقیقِ ایمنی طبق روش‌شناسی IEC 61508/61511 را پیاده‌سازی نمی‌کند.</p>",
            "ابزار دقیق,کنترل,sil",
            new[] { "instrument-passport", "concept-isolation-loto" }),

        new Art("discipline-reliability-maintenance", "disciplines", "DisciplineGuide", 1,
            "Reliability & Maintenance",
            "FMEA/RCM/Criticality with a real approved-decision-to-strategy pipeline - a project-defined methodology, not a certified standard implementation.",
            "<p><strong>Typical concerns:</strong> failure mode identification, criticality ranking, maintenance strategy selection, equipment reliability history, and asset lifecycle data.</p>" +
            "<p><strong>PEMS functionality (verified):</strong> a full FMEA → RCM → applied-Decision → real Maintenance Strategy pipeline; a Criticality Assessment workflow with a documented worst-dimension-governs scoring rule (PEMS-CRIT-1.0); Maintenance Strategy/Plan generation of Work Orders; a Reliability module (RAM simulation, condition monitoring, AI-generated insights) covering equipment/class-level KPIs.</p>" +
            "<p><strong>Explicit limitation:</strong> the scoring methodologies (RPN, Criticality) are PEMS-defined conventions informed by common industry practice - not certified implementations of ISO 14224, SAE JA1011/JA1012 or ISO 55000. See the linked standard references for what those actually require.</p>",
            "reliability,maintenance,rcm,fmea,criticality",
            "قابلیت‌اطمینان و نگهداری",
            "FMEA/RCM/بحرانی‌بودن با یک خط‌لوله‌ی واقعیِ تصمیم‌تاییدشده-تا-استراتژی - روش‌شناسیِ پروژه‌ای، نه پیاده‌سازیِ تاییدشده‌ی یک استاندارد.",
            "<p><strong>دغدغه‌های معمول:</strong> شناسایی حالت خرابی، رتبه‌بندی بحرانی‌بودن، انتخاب استراتژی نگهداری، تاریخچه‌ی قابلیت‌اطمینان تجهیز، و داده‌ی چرخه‌ی عمر دارایی.</p>" +
            "<p><strong>عملکرد PEMS (تاییدشده):</strong> خط‌لوله‌ی کامل FMEA → RCM → تصمیمِ Apply‌شده → استراتژی نگهداری واقعی؛ گردش‌کار ارزیابی بحرانی‌بودن با قاعده‌ی مستندشده‌ی «بدترین بُعد حاکم است»؛ تولید دستور کار از استراتژی/برنامه‌ی نگهداری؛ ماژول قابلیت‌اطمینان (شبیه‌سازی RAM، پایش وضعیت، بینش‌های تولیدشده با AI).</p>" +
            "<p><strong>محدودیت صریح:</strong> روش‌شناسی‌های امتیازدهی (RPN، بحرانی‌بودن) قراردادهای تعریف‌شده‌ی PEMS متاثر از رویه‌ی رایج صنعت‌اند - نه پیاده‌سازیِ تاییدشده‌ی ISO 14224، SAE JA1011/JA1012 یا ISO 55000.</p>",
            "قابلیت اطمینان,نگهداری,rcm,fmea",
            new[] { "rcm-fmea-overview", "criticality-assessment-overview", "concept-maintenance-strategy" }),

        new Art("discipline-quality", "disciplines", "DisciplineGuide", 2,
            "Quality",
            "ITP and NCR, including a real ISO 9001 §8.7-referenced Critical-approval gate cited directly in the code.",
            "<p><strong>Typical concerns:</strong> inspection and test planning, hold points, non-conformance disposition and closure, and quality KPIs.</p>" +
            "<p><strong>PEMS functionality (verified):</strong> Inspection &amp; Test Plans (Draft → Active, line items Pending → Notified → Passed/Failed) and Non-Conformance Reports (Open → Dispositioned/Pending Approval → Closed). The Critical-severity NCR approval gate is directly cited in the codebase's own comments as implementing the spirit of ISO 9001 §8.7 (independent review before closing a critical nonconformance).</p>" +
            "<p><strong>Explicit limitation:</strong> this reflects an internal design decision informed by ISO 9001, not a certified quality management system implementation.</p>",
            "quality,itp,ncr,iso 9001",
            "کیفیت",
            "ITP و NCR، شامل یک دروازه‌ی تایید Critical که مستقیماً در کد به ISO 9001 بند ۸.۷ ارجاع داده شده.",
            "<p><strong>دغدغه‌های معمول:</strong> برنامه‌ریزی بازرسی و آزمون، نقاط توقف، Disposition و بستن عدم‌انطباق، و KPI های کیفیت.</p>" +
            "<p><strong>عملکرد PEMS (تاییدشده):</strong> برنامه‌های بازرسی و آزمون (Draft → Active) و گزارش‌های عدم‌انطباق (Open → Dispositioned/Pending Approval → Closed). دروازه‌ی تایید NCR بحرانی مستقیماً در کامنت‌های خودِ کد به روح ISO 9001 بند ۸.۷ ارجاع داده شده.</p>" +
            "<p><strong>محدودیت صریح:</strong> این یک تصمیم طراحیِ داخلیِ متاثر از ISO 9001 است، نه پیاده‌سازیِ تاییدشده‌ی یک سیستم مدیریت کیفیت.</p>",
            "کیفیت,itp,ncr",
            new[] { "qc-qa-overview", "ncr-critical-approval-workflow" }),

        new Art("discipline-safety-permit", "disciplines", "DisciplineGuide", 3,
            "Safety / Permit-to-Work",
            "A full Permit lifecycle with embedded LOTO and gas-testing, and cryptographic step-up confirmation at every safety-critical action.",
            "<p><strong>Typical concerns:</strong> hazardous work authorization, isolation of energy sources, atmospheric testing, and simultaneous-operations conflict management.</p>" +
            "<p><strong>PEMS functionality (verified):</strong> a full Permit-to-Work lifecycle (Draft through Closed) with an internal HSE/Maintenance/Operations approval chain; embedded Isolation Points (LOTO) and Gas Tests; SIMOPS conflict flagging/confirmation/resolution. Every safety-critical action (verifying an isolation, starting work under a permit, emergency override, confirming/resolving a SIMOPS conflict) requires a WebAuthn passkey step-up, not just being logged in.</p>",
            "safety,permit to work,loto,gas test,simops",
            "ایمنی / مجوز کار",
            "چرخه‌ی کامل مجوز کار با LOTO و تست گاز تعبیه‌شده، و تایید رمزنگاری‌شده‌ی گام دوم در هر اقدام ایمنی‌بحرانی.",
            "<p><strong>دغدغه‌های معمول:</strong> مجوزدهی کار خطرناک، ایزوله‌سازی منابع انرژی، تست جوی، و مدیریت تعارض عملیات هم‌زمان.</p>" +
            "<p><strong>عملکرد PEMS (تاییدشده):</strong> چرخه‌ی کامل مجوز کار (Draft تا Closed) با زنجیره‌ی تایید داخلی HSE/نگهداری/عملیات؛ نقاط ایزوله‌سازی (LOTO) و تست گاز تعبیه‌شده؛ اعلام/تایید/حل تعارض SIMOPS. هر اقدام ایمنی‌بحرانی نیازمند تایید گام دومِ پس‌کلید WebAuthn است، نه صرفاً واردشدن به سیستم.</p>",
            "ایمنی,مجوز کار,loto,تست گاز",
            new[] { "permit-details", "concept-isolation-loto" }),
    };

    // ============================================================================
    // Standards (8) - عنوان/سازمان/URL هرکدام با WebSearch در همین افزایش تایید شده (نه از حافظه)
    // ============================================================================
    private static List<Std> BuildStandards() => new()
    {
        new Std("standard-iso-14224", 0, "ISO 14224", "International Organization for Standardization (ISO)", "2016",
            "https://www.iso.org/standard/64076.html", "CMMS / Reliability & Maintenance",
            "Applicable", "OfficialSourceVerified", "ConceptuallyMapped",
            "PEMS.Domain/Entities/RcmFmea.cs (FailureMode/FailureCause/FailureMechanism/FailureEffect taxonomy); CLAUDE.md documents WorkOrder as \"aligned with ISO 14224\" (FailureModeCode/FailureCauseCode/RootCause)",
            "ISO 14224 — Reliability and Maintenance Data for Equipment",
            "Defines a standard methodology and taxonomy for collecting reliability and maintenance data across the equipment lifecycle in the petroleum, petrochemical and natural gas industries.",
            "<p><strong>What it is:</strong> ISO 14224 (\"Petroleum, petrochemical and natural gas industries — Collection and exchange of reliability and maintenance data for equipment\") defines a common data structure and terminology - equipment classes, failure modes, failure causes, and maintainable-item taxonomy - so reliability/maintenance data can be collected consistently and compared across organizations.</p>" +
            "<p><strong>Why it matters:</strong> without a common taxonomy, failure data collected by different teams or systems can't be meaningfully aggregated or benchmarked.</p>" +
            "<p><strong>PEMS relevance:</strong> PEMS's own project documentation (CLAUDE.md) explicitly states its WorkOrder ISO 14224 fields (FailureModeCode, FailureCauseCode, RootCause) are \"aligned with ISO 14224\" - this is a documented design intent, not a certified compliance claim.</p>" +
            "<p><strong>Current PEMS coverage:</strong> a failure-mode/cause/mechanism/effect taxonomy exists and is used by WorkOrder failure coding and FMEA analysis.</p>" +
            "<p><strong>Outside current PEMS scope:</strong> full ISO 14224 equipment-class taxonomy conformance and formal reliability-data-exchange format have not been independently verified against the published standard.</p>",
            "iso 14224,reliability data,maintenance data,failure taxonomy",
            "ISO 14224 — داده‌ی قابلیت‌اطمینان و نگهداری تجهیزات",
            "یک روش‌شناسی و تاکسونومیِ استاندارد برای جمع‌آوری داده‌ی قابلیت‌اطمینان و نگهداری در طول چرخه‌ی عمر تجهیزات در صنایع نفت، پتروشیمی و گاز طبیعی تعریف می‌کند.",
            "<p><strong>چیست:</strong> ISO 14224 («صنایع نفت، پتروشیمی و گاز طبیعی — جمع‌آوری و تبادل داده‌ی قابلیت‌اطمینان و نگهداری تجهیزات») یک ساختار داده و واژگان مشترک - کلاس‌های تجهیز، حالات خرابی، علل خرابی، و تاکسونومیِ آیتم‌های قابل‌نگهداری - تعریف می‌کند.</p>" +
            "<p><strong>چرا اهمیت دارد:</strong> بدون یک تاکسونومیِ مشترک، داده‌ی خرابیِ جمع‌آوری‌شده توسط تیم‌ها یا سیستم‌های مختلف قابل‌تجمیع یا مقایسه‌ی معنادار نیست.</p>" +
            "<p><strong>ارتباط با PEMS:</strong> مستندات خودِ پروژه‌ی PEMS (CLAUDE.md) صریحاً بیان می‌کند فیلدهای ISO 14224 دستور کار (FailureModeCode، FailureCauseCode، RootCause) «هم‌راستا با ISO 14224» هستند - این یک نیتِ طراحیِ مستندشده است، نه ادعای انطباق تاییدشده.</p>" +
            "<p><strong>پوشش فعلی PEMS:</strong> یک تاکسونومیِ حالت‌خرابی/علت/مکانیزم/اثر وجود دارد و توسط کدگذاری خرابیِ دستور کار و تحلیل FMEA استفاده می‌شود.</p>" +
            "<p><strong>خارج از محدوده‌ی فعلی PEMS:</strong> انطباق کاملِ تاکسونومیِ کلاس تجهیزِ ISO 14224 و فرمت رسمیِ تبادل داده‌ی قابلیت‌اطمینان مستقلاً در برابر متن منتشرشده‌ی استاندارد تایید نشده.</p>",
            "iso 14224,داده قابلیت اطمینان,تاکسونومی خرابی",
            new[] { "concept-criticality", "rcm-fmea-overview" }),

        new Std("standard-sae-ja1011", 1, "SAE JA1011", "SAE International", "2024 (most recent per search; earlier editions exist)",
            "https://www.sae.org/standards/ja1011_202411-evaluation-criteria-reliability-centered-maintenance-rcm-processes", "CMMS / Reliability & Maintenance",
            "Applicable", "OfficialSourceVerified", "ConceptuallyMapped",
            "PEMS.Persistence/Servises/RCMService.cs; PEMS.Domain/Entities/RcmFmea.cs (RCMAnalysis/RCMDecision)",
            "SAE JA1011 — Evaluation Criteria for Reliability-Centered Maintenance (RCM) Processes",
            "Defines the minimum criteria a process must satisfy before it can properly be called RCM.",
            "<p><strong>What it is:</strong> SAE JA1011 (\"Evaluation Criteria for Reliability-Centered Maintenance (RCM) Processes\") sets out the seven questions an RCM process must answer for each asset (functions, functional failures, failure modes, failure effects, failure consequences, proactive tasks, default actions) and the criteria a process must meet to be validly called RCM.</p>" +
            "<p><strong>Why it matters:</strong> \"RCM\" is used loosely in industry for many different processes; JA1011 is the reference point for what genuinely qualifies.</p>" +
            "<p><strong>PEMS relevance:</strong> PEMS's RCM module structure (Functional Requirement → Functional Failure → FMEA → RCM Decision) conceptually follows the same question sequence JA1011 describes.</p>" +
            "<p><strong>Current PEMS coverage:</strong> the functional-requirement/functional-failure/decision data model exists and is used to build RCM analyses and decisions.</p>" +
            "<p><strong>Outside current PEMS scope:</strong> PEMS has not been formally audited against JA1011's specific evaluation criteria - the structural similarity is a conceptual mapping, not a certification.</p>",
            "sae ja1011,rcm evaluation criteria,reliability centered maintenance",
            "SAE JA1011 — معیارهای ارزیابیِ فرآیندهای RCM",
            "حداقل معیارهایی که یک فرآیند باید برآورده کند تا بتوان آن را درست RCM نامید تعریف می‌کند.",
            "<p><strong>چیست:</strong> SAE JA1011 («معیارهای ارزیابی برای فرآیندهای نگهداری متمرکز بر قابلیت‌اطمینان (RCM)») هفت پرسشی که یک فرآیند RCM باید برای هر دارایی پاسخ دهد (کارکردها، خرابی‌های کارکردی، حالات خرابی، اثرات خرابی، پیامدهای خرابی، وظایف پیشگیرانه، اقدامات پیش‌فرض) را مشخص می‌کند.</p>" +
            "<p><strong>چرا اهمیت دارد:</strong> «RCM» در صنعت برای فرآیندهای متفاوت زیادی به‌طور سست استفاده می‌شود؛ JA1011 نقطه‌ی مرجعِ این‌که چه چیزی واقعاً واجد شرایط است.</p>" +
            "<p><strong>ارتباط با PEMS:</strong> ساختار ماژول RCM پی‌ام‌اس (نیاز کارکردی → خرابی کارکردی → FMEA → تصمیم RCM) مفهوماً همان توالی پرسشیِ JA1011 را دنبال می‌کند.</p>" +
            "<p><strong>پوشش فعلی PEMS:</strong> مدل داده‌ی نیاز-کارکردی/خرابی-کارکردی/تصمیم وجود دارد و برای ساخت تحلیل‌ها و تصمیمات RCM استفاده می‌شود.</p>" +
            "<p><strong>خارج از محدوده‌ی فعلی PEMS:</strong> PEMS رسماً در برابر معیارهای مشخصِ ارزیابیِ JA1011 ممیزی نشده - شباهت ساختاری یک نگاشت مفهومی است، نه گواهی.</p>",
            "sae ja1011,معیار ارزیابی rcm",
            new[] { "concept-rcm", "rcm-fmea-overview" }),

        new Std("standard-sae-ja1012", 2, "SAE JA1012", "SAE International", "2011",
            "https://www.sae.org/standards/ja1012-a-guide-reliability-centered-maintenance-rcm-standard/", "CMMS / Reliability & Maintenance",
            "ReferenceOnly", "OfficialSourceVerified", "NotVerified", null,
            "SAE JA1012 — A Guide to the Reliability-Centered Maintenance (RCM) Standard",
            "The companion explanatory guide to SAE JA1011 - amplifies and clarifies each evaluation criterion.",
            "<p><strong>What it is:</strong> SAE JA1012 (\"A Guide to the Reliability-Centered Maintenance (RCM) Standard\") is the companion document to JA1011: it explains and clarifies each of JA1011's criteria in plain language and addresses additional issues involved in applying RCM successfully.</p>" +
            "<p><strong>Why it matters:</strong> JA1011 alone is terse; JA1012 is the practical guide most practitioners actually use to apply it correctly.</p>" +
            "<p><strong>PEMS relevance:</strong> referenced here alongside JA1011 for completeness - it is explanatory guidance, not a separate set of requirements.</p>" +
            "<p><strong>Current PEMS coverage / outside scope:</strong> not independently assessed against PEMS's RCM implementation in this increment - listed as Reference Only.</p>",
            "sae ja1012,rcm guide",
            "SAE JA1012 — راهنمای استاندارد RCM",
            "راهنمای توضیحیِ همراهِ SAE JA1011 - هر معیار ارزیابی را بسط و روشن می‌کند.",
            "<p><strong>چیست:</strong> SAE JA1012 («راهنمایی برای استاندارد نگهداری متمرکز بر قابلیت‌اطمینان (RCM)») سند همراهِ JA1011 است: هر معیار JA1011 را به زبان ساده توضیح و روشن می‌کند.</p>" +
            "<p><strong>چرا اهمیت دارد:</strong> JA1011 به‌تنهایی موجز است؛ JA1012 راهنمای عملی‌ای است که اکثر متخصصان واقعاً برای اعمال درست آن استفاده می‌کنند.</p>" +
            "<p><strong>ارتباط با PEMS:</strong> این‌جا در کنار JA1011 برای کامل‌بودن ذکر شده - راهنمای توضیحی است، نه مجموعه‌ای جدا از الزامات.</p>" +
            "<p><strong>پوشش فعلی PEMS / خارج از محدوده:</strong> در این افزایش مستقلاً در برابر پیاده‌سازی RCM پی‌ام‌اس ارزیابی نشده - فقط مرجع ذکر شده.</p>",
            "sae ja1012,راهنمای rcm",
            new[] { "standard-sae-ja1011" }),

        new Std("standard-iso-55001", 3, "ISO 55001", "International Organization for Standardization (ISO)", "2014 (2024 edition also exists per ISO)",
            "https://www.iso.org/standard/55089.html", "CMMS / Reliability & Maintenance",
            "PotentiallyApplicable", "OfficialSourceVerified", "NotImplemented", null,
            "ISO 55001 — Asset Management, Management Systems — Requirements",
            "Specifies requirements for an organization-wide asset management system - broader than any single PEMS module.",
            "<p><strong>What it is:</strong> ISO 55001 (\"Asset management — Management systems — Requirements\", part of the ISO 55000 family alongside 55000 and 55002) specifies requirements for establishing, implementing, maintaining and improving an organization-wide asset management system.</p>" +
            "<p><strong>Why it matters:</strong> it frames asset management as a whole-organization system (policy, objectives, risk, resourcing), not just a maintenance tool.</p>" +
            "<p><strong>PEMS relevance:</strong> PEMS's Equipment/Criticality/Maintenance Strategy data model supports asset-management-style thinking (risk-based prioritization), but ISO 55001 requires an organization-wide management system, which is far broader than what any single application can implement.</p>" +
            "<p><strong>Current PEMS coverage:</strong> equipment register, criticality-based prioritization, and maintenance planning data exist.</p>" +
            "<p><strong>Outside current PEMS scope:</strong> the organizational asset management system itself (policy, strategic asset management plan, leadership commitment) is outside what any software system can provide - PEMS is not, and does not claim to be, an ISO 55001 management system.</p>",
            "iso 55001,iso 55000,asset management",
            "ISO 55001 — مدیریت دارایی، سیستم‌های مدیریت — الزامات",
            "الزامات یک سیستم مدیریت دارایی در سطح کل سازمان را مشخص می‌کند - فراتر از هر ماژول منفردِ PEMS.",
            "<p><strong>چیست:</strong> ISO 55001 («مدیریت دارایی — سیستم‌های مدیریت — الزامات»، بخشی از خانواده‌ی ISO 55000 در کنار 55000 و 55002) الزامات ایجاد، اجرا، نگهداری و بهبود یک سیستم مدیریت دارایی در سطح کل سازمان را مشخص می‌کند.</p>" +
            "<p><strong>چرا اهمیت دارد:</strong> مدیریت دارایی را به‌عنوان یک سیستم کل‌سازمانی (سیاست، اهداف، ریسک، تخصیص منابع) قاب می‌کند، نه صرفاً یک ابزار نگهداری.</p>" +
            "<p><strong>ارتباط با PEMS:</strong> مدل داده‌ی تجهیز/بحرانی‌بودن/استراتژی نگهداری PEMS از تفکر سبک-مدیریت-دارایی (اولویت‌بندی ریسک‌محور) پشتیبانی می‌کند، اما ISO 55001 یک سیستم مدیریتیِ کل‌سازمانی نیاز دارد که فراتر از چیزی است که یک نرم‌افزار به‌تنهایی می‌تواند فراهم کند.</p>" +
            "<p><strong>پوشش فعلی PEMS:</strong> رجیستر تجهیز، اولویت‌بندیِ بحرانی‌بودن‌محور، و داده‌ی برنامه‌ریزی نگهداری وجود دارد.</p>" +
            "<p><strong>خارج از محدوده‌ی فعلی PEMS:</strong> خودِ سیستم مدیریت داراییِ سازمانی خارج از چیزی است که هر نرم‌افزاری می‌تواند فراهم کند - PEMS یک سیستم مدیریتیِ ISO 55001 نیست و چنین ادعایی هم نمی‌کند.</p>",
            "iso 55001,iso 55000,مدیریت دارایی",
            new[] { "concept-criticality" }),

        new Std("standard-iso-9001", 4, "ISO 9001", "International Organization for Standardization (ISO)", "2015 (referenced in code); ISO 9001:2026 is now the current edition per ISO",
            "https://www.iso.org/standard/62085.html", "CMMS / Quality",
            "Applicable", "OfficialSourceVerified", "ConceptuallyMapped",
            "PEMS.Persistence/Seed/NcrApprovalWorkflowSeeder.cs (code comment explicitly cites ISO 9001 §8.7)",
            "ISO 9001 — Quality Management Systems — Requirements",
            "The globally-used quality management system standard - directly cited in PEMS's own NCR approval code.",
            "<p><strong>What it is:</strong> ISO 9001 (\"Quality management systems — Requirements\") is the world's most widely used quality management standard, specifying requirements an organization must meet to consistently provide products/services that satisfy customer and regulatory requirements. Clause 8.7 covers control of nonconforming outputs.</p>" +
            "<p><strong>Why it matters:</strong> it is the most common external quality-management reference point across engineering/manufacturing organizations.</p>" +
            "<p><strong>PEMS relevance:</strong> uniquely among the standards in this initial set, PEMS's own source code explicitly cites this standard by clause number - the NCR Critical-severity independent-approval gate's code comment states it exists \"per ISO 9001 §8.7\".</p>" +
            "<p><strong>Current PEMS coverage:</strong> the NCR disposition/Critical-approval workflow and ITP hold-point mechanism implement practices consistent with §8.7's intent.</p>" +
            "<p><strong>Outside current PEMS scope:</strong> this is a documented design influence on one workflow, not a certified quality management system covering the whole organization - PEMS does not claim ISO 9001 certification.</p>",
            "iso 9001,quality management,ncr,clause 8.7",
            "ISO 9001 — سیستم‌های مدیریت کیفیت — الزامات",
            "پرکاربردترین استاندارد مدیریت کیفیت در جهان - مستقیماً در کدِ تایید NCR پی‌ام‌اس ارجاع داده شده.",
            "<p><strong>چیست:</strong> ISO 9001 («سیستم‌های مدیریت کیفیت — الزامات») پرکاربردترین استاندارد مدیریت کیفیت در جهان است. بند ۸.۷ کنترل خروجی‌های نامنطبق را پوشش می‌دهد.</p>" +
            "<p><strong>چرا اهمیت دارد:</strong> رایج‌ترین نقطه‌ی مرجعِ مدیریت کیفیتِ بیرونی در سازمان‌های مهندسی/تولیدی است.</p>" +
            "<p><strong>ارتباط با PEMS:</strong> به‌طور منحصربه‌فرد در میان استانداردهای این مجموعه‌ی اولیه، خودِ کد منبع PEMS این استاندارد را با شماره‌ی بند صریحاً ارجاع می‌دهد - کامنت کدِ دروازه‌ی تایید مستقل NCR بحرانی می‌گوید «طبق ISO 9001 بند ۸.۷» وجود دارد.</p>" +
            "<p><strong>پوشش فعلی PEMS:</strong> گردش‌کار Disposition/تایید بحرانیِ NCR و مکانیزم نقطه‌ی توقف ITP رویه‌هایی هم‌راستا با نیت بند ۸.۷ پیاده‌سازی می‌کنند.</p>" +
            "<p><strong>خارج از محدوده‌ی فعلی PEMS:</strong> این یک تاثیر طراحیِ مستندشده روی یک گردش‌کار است، نه یک سیستم مدیریت کیفیتِ تاییدشده برای کل سازمان - PEMS ادعای گواهی ISO 9001 ندارد.</p>",
            "iso 9001,مدیریت کیفیت,ncr,بند ۸.۷",
            new[] { "concept-ncr", "discipline-quality" }),

        new Std("standard-api-510", 5, "API 510", "American Petroleum Institute (API)", "11th Edition, October 2022",
            "https://www.api.org/products-and-services/standards/important-standards-announcements/standard510", "CMMS / Inspection",
            "PotentiallyApplicable", "OfficialSourceVerified", "NotImplemented", null,
            "API 510 — Pressure Vessel Inspection Code",
            "The industry code for in-service pressure vessel inspection - PEMS's Inspection module is generic, not vessel-specific.",
            "<p><strong>What it is:</strong> API 510 (\"Pressure Vessel Inspection Code: In-Service Inspection, Rating, Repair, and Alteration\") covers inspection, rating, repair and alteration of in-service pressure vessels and their pressure-relieving devices.</p>" +
            "<p><strong>Why it matters:</strong> it is the standard inspection interval/methodology reference for pressure vessels across the oil &amp; gas and petrochemical industry.</p>" +
            "<p><strong>PEMS relevance:</strong> PEMS has a generic Inspection module (InspectionRecord/InspectionChecklist) that can raise Work Requests from failed inspections, but it does not encode API 510-specific concepts such as remaining-life calculation, corrosion-rate-based interval setting, or vessel-specific rating rules.</p>" +
            "<p><strong>Current PEMS coverage:</strong> generic inspection recording and follow-up work-request generation.</p>" +
            "<p><strong>Outside current PEMS scope:</strong> API 510-specific inspection interval calculation, remaining-life/corrosion-rate methodology, and vessel rating are not implemented.</p>",
            "api 510,pressure vessel inspection",
            "API 510 — آیین‌نامه‌ی بازرسی مخازن تحت فشار",
            "آیین‌نامه‌ی صنعتیِ بازرسی مخازن تحت فشار در حال سرویس - ماژول بازرسی PEMS عمومی است، نه مخصوص مخزن.",
            "<p><strong>چیست:</strong> API 510 («آیین‌نامه‌ی بازرسی مخازن تحت فشار: بازرسی در حال سرویس، رتبه‌بندی، تعمیر و اصلاح») بازرسی، رتبه‌بندی، تعمیر و اصلاح مخازن تحت فشار در حال سرویس و دستگاه‌های تخلیه‌ی فشار آن‌ها را پوشش می‌دهد.</p>" +
            "<p><strong>چرا اهمیت دارد:</strong> مرجع استاندارد فاصله‌ی بازرسی/روش‌شناسی برای مخازن تحت فشار در صنعت نفت و گاز و پتروشیمی است.</p>" +
            "<p><strong>ارتباط با PEMS:</strong> PEMS یک ماژول بازرسیِ عمومی دارد که می‌تواند از بازرسی ناموفق درخواست کار بسازد، اما مفاهیم مخصوصِ API 510 مثل محاسبه‌ی عمر باقی‌مانده یا فاصله‌ی بازرسیِ بر پایه‌ی نرخ خوردگی را کدگذاری نمی‌کند.</p>" +
            "<p><strong>پوشش فعلی PEMS:</strong> ثبت بازرسیِ عمومی و تولید درخواست کارِ پیگیری.</p>" +
            "<p><strong>خارج از محدوده‌ی فعلی PEMS:</strong> محاسبه‌ی فاصله‌ی بازرسیِ مخصوص API 510، روش‌شناسیِ عمر باقی‌مانده/نرخ خوردگی، و رتبه‌بندی مخزن پیاده‌سازی نشده‌اند.</p>",
            "api 510,بازرسی مخزن تحت فشار",
            new[] { "discipline-instrumentation-control" }),

        new Std("standard-iec-61508", 6, "IEC 61508", "International Electrotechnical Commission (IEC)", "Current per IEC (multi-part standard)",
            "https://webstore.iec.ch/en/publication/24241", "CMMS / Instrumentation & Control",
            "PotentiallyApplicable", "OfficialSourceVerified", "PartiallyRelated",
            "PEMS.Web/Controllers/InstrumentController.cs (instrument.SIL = form.SIL)",
            "IEC 61508 — Functional Safety of Electrical/Electronic/Programmable Electronic Safety-Related Systems",
            "The generic functional-safety framework behind SIL - PEMS captures a SIL value without implementing the verification methodology.",
            "<p><strong>What it is:</strong> IEC 61508 (\"Functional safety of electrical/electronic/programmable electronic safety-related systems\") is the foundational, generic framework for functional safety, defining Safety Integrity Levels (SIL 1-4) and the lifecycle activities required to achieve them.</p>" +
            "<p><strong>Why it matters:</strong> it underlies sector-specific standards like IEC 61511 (process industry) - SIL is a concept defined here.</p>" +
            "<p><strong>PEMS relevance:</strong> the Instrument record captures a SIL field, set directly from an admin/engineering form.</p>" +
            "<p><strong>Current PEMS coverage:</strong> a SIL value can be recorded and displayed per instrument.</p>" +
            "<p><strong>Outside current PEMS scope:</strong> PEMS does not calculate, verify, or manage the SIL determination process (e.g. LOPA, SIL verification calculations) or the broader safety lifecycle IEC 61508 requires - the field is a stored value, not a computed or verified one.</p>",
            "iec 61508,functional safety,sil",
            "IEC 61508 — ایمنی کارکردیِ سیستم‌های الکتریکی/الکترونیکی/الکترونیکِ قابل‌برنامه‌ریزی",
            "چارچوب عمومیِ ایمنی کارکردی پشتِ SIL - PEMS یک مقدار SIL را ثبت می‌کند بدون پیاده‌سازیِ روش‌شناسیِ تایید آن.",
            "<p><strong>چیست:</strong> IEC 61508 («ایمنی کارکردیِ سیستم‌های ایمنی‌محورِ الکتریکی/الکترونیکی/الکترونیکِ قابل‌برنامه‌ریزی») چارچوب پایه‌ای و عمومیِ ایمنی کارکردی است، سطوح یکپارچگیِ ایمنی (SIL ۱ تا ۴) را تعریف می‌کند.</p>" +
            "<p><strong>چرا اهمیت دارد:</strong> پایه‌ی استانداردهای بخشیِ مثل IEC 61511 (صنعت فرآیندی) است - SIL مفهومی است که این‌جا تعریف می‌شود.</p>" +
            "<p><strong>ارتباط با PEMS:</strong> رکورد ابزار دقیق یک فیلد SIL را ثبت می‌کند، مستقیماً از یک فرم ادمین/مهندسی.</p>" +
            "<p><strong>پوشش فعلی PEMS:</strong> یک مقدار SIL می‌تواند به‌ازای هر ابزار ثبت و نمایش داده شود.</p>" +
            "<p><strong>خارج از محدوده‌ی فعلی PEMS:</strong> PEMS فرآیند تعیین SIL (مثلاً LOPA، محاسبات تاییدِ SIL) یا چرخه‌ی عمر ایمنیِ گسترده‌تری که IEC 61508 نیاز دارد را محاسبه، تایید، یا مدیریت نمی‌کند - فیلد یک مقدار ذخیره‌شده است، نه محاسبه‌شده یا تاییدشده.</p>",
            "iec 61508,ایمنی کارکردی,sil",
            new[] { "discipline-instrumentation-control" }),

        new Std("standard-iec-61511", 7, "IEC 61511", "International Electrotechnical Commission (IEC)", "2016 (Part 1)",
            "https://webstore.iec.ch/en/publication/24241", "CMMS / Instrumentation & Control",
            "PotentiallyApplicable", "OfficialSourceVerified", "NotImplemented", null,
            "IEC 61511 — Functional Safety: Safety Instrumented Systems for the Process Industry Sector",
            "The process-industry-specific application of IEC 61508 - not implemented as a lifecycle-managed capability in PEMS.",
            "<p><strong>What it is:</strong> IEC 61511 (\"Functional safety — Safety instrumented systems for the process industry sector\") is the sector-specific standard applying IEC 61508's framework to Safety Instrumented Systems (SIS) in the process industry - covering the full SIS lifecycle from design through operation and maintenance.</p>" +
            "<p><strong>Why it matters:</strong> it is the standard process plants use to design, verify and maintain safety instrumented functions (e.g. emergency shutdown systems).</p>" +
            "<p><strong>PEMS relevance:</strong> PEMS's Instrument/Alarm/Cause &amp; Effect data model touches the same subject area (trip logic, safety-related alarms) but was not built as a dedicated SIS lifecycle management capability.</p>" +
            "<p><strong>Current PEMS coverage:</strong> alarms with Cause &amp; Effect (including Trip/Shutdown action types) and a captured SIL field on instruments.</p>" +
            "<p><strong>Outside current PEMS scope:</strong> proof-test scheduling/tracking against SIL targets, SIS lifecycle documentation, and IEC 61511-specific verification are not implemented.</p>",
            "iec 61511,sis,safety instrumented system,process industry",
            "IEC 61511 — ایمنی کارکردی: سیستم‌های ابزاردقیقِ ایمنی برای بخش صنعت فرآیندی",
            "کاربردِ مخصوصِ صنعت فرآیندیِ IEC 61508 - در PEMS به‌عنوان یک قابلیتِ مدیریت‌شده‌ی چرخه‌عمر پیاده‌سازی نشده.",
            "<p><strong>چیست:</strong> IEC 61511 («ایمنی کارکردی — سیستم‌های ابزاردقیقِ ایمنی برای بخش صنعت فرآیندی») استاندارد بخشیِ اعمال چارچوب IEC 61508 روی سیستم‌های ابزاردقیقِ ایمنی (SIS) در صنعت فرآیندی است.</p>" +
            "<p><strong>چرا اهمیت دارد:</strong> استانداردی است که کارخانه‌های فرآیندی برای طراحی، تایید و نگهداریِ کارکردهای ابزاردقیقِ ایمنی (مثلاً سیستم‌های توقف اضطراری) استفاده می‌کنند.</p>" +
            "<p><strong>ارتباط با PEMS:</strong> مدل داده‌ی ابزار دقیق/آلارم/علت‌واثرِ PEMS همان حوزه‌ی موضوعی (منطق Trip، آلارم‌های ایمنی‌محور) را لمس می‌کند اما به‌عنوان یک قابلیت اختصاصیِ مدیریت چرخه‌عمر SIS ساخته نشده.</p>" +
            "<p><strong>پوشش فعلی PEMS:</strong> آلارم‌ها با علت‌واثر (شامل انواع اقدام Trip/Shutdown) و یک فیلد SIL ثبت‌شده روی ابزارها.</p>" +
            "<p><strong>خارج از محدوده‌ی فعلی PEMS:</strong> زمان‌بندی/ردگیری تست اثبات در برابر اهداف SIL، مستندسازیِ چرخه‌عمر SIS، و تاییدِ مخصوصِ IEC 61511 پیاده‌سازی نشده‌اند.</p>",
            "iec 61511,sis,سیستم ابزاردقیق ایمنی",
            new[] { "standard-iec-61508", "discipline-instrumentation-control" }),
    };
}
