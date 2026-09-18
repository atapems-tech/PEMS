using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Persistence.Seed;

/// <summary>
/// ==============================================================================
/// طبق «PEMS Bilingual Help & Knowledge System» بخش ۱۵: کتابخانه‌ی محتوای اولیه‌ی راهنما.
/// ایدمپوتنت به‌ازای هر مقاله (بر اساس Code) - اجرای دوباره نه رکورد تکراری می‌سازد و نه
/// ویرایش‌های دستی ادمین روی مقالات از‌قبل‌موجود را رونویسی می‌کند (فقط مقالاتی که Code شان
/// هنوز وجود ندارد اضافه می‌شوند).
///
/// هر مقاله دقیقاً بر اساس رفتار واقعیِ تاییدشده‌ی همین ریپو نوشته شده - نه حدس. جاهایی که
/// یک قاعده صرفاً توصیه/مفهوم مرجع است نه قانون اجراشده‌ی سیستم، صریحاً برچسب خورده.
/// ==============================================================================
/// </summary>
public static class HelpContentSeeder
{
    private sealed record Cat(string Code, string NameEn, string NameFa, string Icon, int Order);

    private sealed record Art(
        string Code, string CategoryCode, string ArticleType, string? HelpKey, int Order,
        string TitleEn, string SummaryEn, string ContentEn, string? KeywordsEn,
        string TitleFa, string SummaryFa, string ContentFa, string? KeywordsFa,
        string[]? RelatedCodes = null);

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PEMSDbContext>();

        var categories = new[]
        {
            new Cat("general", "General", "عمومی", "bi-info-circle", 0),
            new Cat("engineering", "Engineering", "مهندسی", "bi-file-earmark-text", 1),
            new Cat("cmms", "CMMS", "نگهداری و تعمیرات", "bi-tools", 2),
            new Cat("operations", "Operations & Production", "عملیات و تولید", "bi-diagram-3", 3),
            new Cat("administration", "Administration", "مدیریت سامانه", "bi-shield-lock", 4),
            new Cat("troubleshooting", "Troubleshooting", "رفع اشکال", "bi-life-preserver", 5),
        };

        var categoryIds = new Dictionary<string, int>();
        foreach (var c in categories)
        {
            var existing = await db.HelpCategories.FirstOrDefaultAsync(x => x.Code == c.Code);
            if (existing == null)
            {
                existing = new HelpCategory
                {
                    Code = c.Code, NameEn = c.NameEn, NameFa = c.NameFa,
                    IconCssClass = c.Icon, DisplayOrder = c.Order, IsActive = true
                };
                db.HelpCategories.Add(existing);
                await db.SaveChangesAsync();
            }
            categoryIds[c.Code] = existing.HelpCategoryId;
        }

        var articles = BuildArticles();

        foreach (var a in articles)
        {
            var exists = await db.HelpArticles.AnyAsync(x => x.Code == a.Code);
            if (exists) continue;

            var article = new HelpArticle
            {
                Code = a.Code,
                HelpCategoryId = categoryIds[a.CategoryCode],
                ArticleType = a.ArticleType,
                HelpKey = a.HelpKey,
                DisplayOrder = a.Order,
                IsPublished = true,
                CreatedDate = DateTime.Now
            };

            article.Translations.Add(new HelpArticleTranslation
            {
                LanguageCode = "en", Title = a.TitleEn, Summary = a.SummaryEn, Content = a.ContentEn, SearchKeywords = a.KeywordsEn
            });
            article.Translations.Add(new HelpArticleTranslation
            {
                LanguageCode = "fa", Title = a.TitleFa, Summary = a.SummaryFa, Content = a.ContentFa, SearchKeywords = a.KeywordsFa
            });

            db.HelpArticles.Add(article);
        }

        await db.SaveChangesAsync();

        // پیوندهای «مقاله‌ی مرتبط» - بعد از این‌که همه‌ی مقالات (چه تازه‌ساخته چه قبلاً موجود) شناسه دارند
        await LinkRelatedArticlesAsync(db, articles);
    }

    private static async Task LinkRelatedArticlesAsync(PEMSDbContext db, List<Art> articles)
    {
        var idByCode = await db.HelpArticles.ToDictionaryAsync(x => x.Code, x => x.HelpArticleId);

        foreach (var a in articles)
        {
            if (a.RelatedCodes == null || a.RelatedCodes.Length == 0) continue;
            if (!idByCode.TryGetValue(a.Code, out var articleId)) continue;

            foreach (var relatedCode in a.RelatedCodes)
            {
                if (!idByCode.TryGetValue(relatedCode, out var relatedId)) continue;

                var linkExists = await db.HelpRelatedArticles
                    .AnyAsync(r => r.HelpArticleId == articleId && r.RelatedHelpArticleId == relatedId);
                if (linkExists) continue;

                db.HelpRelatedArticles.Add(new HelpRelatedArticle { HelpArticleId = articleId, RelatedHelpArticleId = relatedId });
            }
        }

        await db.SaveChangesAsync();
    }

    private static List<Art> BuildArticles() => new()
    {
        // ============================= GENERAL =============================
        new Art("welcome-to-pems", "general", "Module", null, 0,
            "Welcome to PEMS",
            "What PEMS is and how this Help system is organized.",
            "<p>PEMS (Project Engineering Management System) is an integrated platform for engineering document control, CMMS " +
            "(maintenance) and enterprise operations for oil &amp; gas / petrochemical projects. It brings together engineering " +
            "document review and approval, equipment and maintenance management, production/operations tracking, and " +
            "administration (users, roles, permissions) in one system with a consistent audit trail.</p>" +
            "<p>This Help Center is organized by module (Engineering, CMMS, Operations, Administration) and by content type: " +
            "module overviews, page guides, field explanations, workflow guides and troubleshooting articles. Every article " +
            "exists in both English and Persian.</p>" +
            "<p><strong>Note:</strong> Help explains how PEMS works and what your role can do - it never grants access by " +
            "itself. What you can actually see and do is always enforced by the real permission system, not by this Help " +
            "content.</p>",
            "welcome,pems,introduction,help",
            "به PEMS خوش آمدید",
            "PEMS چیست و این سامانه‌ی راهنما چگونه سازمان‌دهی شده است.",
            "<p>PEMS (سامانه‌ی مدیریت مهندسی پروژه) یک بستر یکپارچه برای کنترل مدارک مهندسی، CMMS (نگهداری و تعمیرات) و " +
            "عملیات سازمانی پروژه‌های نفت و گاز/پتروشیمی است. این سامانه بازبینی و تایید مدارک مهندسی، مدیریت تجهیزات و " +
            "نگهداری‌وتعمیرات، پایش عملیات/تولید و مدیریت سامانه (کاربران، نقش‌ها، مجوزها) را در یک سیستم واحد با ردگیری کامل " +
            "رویدادها (Audit Trail) گرد هم می‌آورد.</p>" +
            "<p>این مرکز راهنما بر اساس ماژول (مهندسی، CMMS، عملیات، مدیریت سامانه) و نوع محتوا (معرفی ماژول، راهنمای صفحه، " +
            "توضیح فیلد، راهنمای گردش‌کار، رفع اشکال) سازمان‌دهی شده است. هر مقاله به هر دو زبان انگلیسی و فارسی موجود است.</p>" +
            "<p><strong>نکته:</strong> راهنما توضیح می‌دهد سامانه چگونه کار می‌کند و نقش شما چه اختیاراتی دارد - اما خودش هرگز " +
            "دسترسی اعطا نمی‌کند. آنچه واقعاً می‌بینید و می‌توانید انجام دهید همیشه توسط سیستم واقعی مجوزها تعیین می‌شود، نه " +
            "این محتوای راهنما.</p>",
            "خوش آمدید,پمز,معرفی,راهنما",
            new[] { "getting-started" }),

        new Art("getting-started", "general", "Module", null, 1,
            "Getting Started: Main Navigation",
            "How the sidebar, top bar, search and Help Center fit together.",
            "<p>The sidebar (left) groups pages into collapsible sections by module - Engineering, CMMS, Operations, " +
            "Administration and so on; which sections you see depends on your role. The top bar (always visible) has: a " +
            "global search box, a theme switcher, a language switcher (English/Persian), notifications, the Help icon, and " +
            "your user menu.</p>" +
            "<p>Switching language (topbar language icon) changes both the interface text and the page direction: English " +
            "renders left-to-right, Persian renders right-to-left. Dates and numeric values stay in the same format in both " +
            "languages by design, so records remain consistent regardless of which language you're viewing them in.</p>",
            "navigation,sidebar,topbar,getting started",
            "شروع کار: پیمایش اصلی",
            "منوی کناری، نوار بالا، جستجو و مرکز راهنما چگونه در کنار هم کار می‌کنند.",
            "<p>منوی کناری (سمت چپ/راست بسته به جهت) صفحات را در بخش‌های جمع‌شونده بر اساس ماژول گروه‌بندی می‌کند - مهندسی، " +
            "CMMS، عملیات، مدیریت سامانه و غیره؛ این‌که کدام بخش‌ها را می‌بینید به نقش شما بستگی دارد. نوار بالا (همیشه " +
            "قابل‌مشاهده) شامل این‌هاست: کادر جستجوی سراسری، تعویض پوسته، تعویض زبان (انگلیسی/فارسی)، اعلان‌ها، آیکون راهنما و " +
            "منوی کاربر شما.</p>" +
            "<p>تعویض زبان (آیکون زبان در نوار بالا) هم متن رابط کاربری و هم جهت صفحه را تغییر می‌دهد: انگلیسی از چپ‌به‌راست و " +
            "فارسی از راست‌به‌چپ نمایش داده می‌شود. تاریخ‌ها و مقادیر عددی عمداً در هر دو زبان به همان قالب باقی می‌مانند، تا " +
            "رکوردها صرف‌نظر از زبان نمایش، یکسان و قابل‌مقایسه بمانند.</p>",
            "پیمایش,منو,نوار بالا,شروع کار",
            new[] { "welcome-to-pems", "help-center-usage" }),

        new Art("help-center-usage", "general", "Module", null, 2,
            "Using the Help Center",
            "How to browse, search, and get contextual help while you work.",
            "<p>Open the full Help Center from the topbar Help icon. You can browse articles by module/category, or search by " +
            "keyword in either English or Persian - searching in one language can still surface an article written primarily " +
            "for the other language if the term matches.</p>" +
            "<p>On supported pages, a contextual Help button opens a short panel specific to that page - what it's for, key " +
            "fields, available actions and related articles - without leaving your current task. Where the AI assistant is " +
            "available, you can also ask a follow-up question about the open article; the answer is generated only from that " +
            "article's actual content, and the system will say so explicitly if something isn't documented yet rather than " +
            "guessing.</p>",
            "help center,search,contextual help,ask ai",
            "استفاده از مرکز راهنما",
            "چگونه مقالات را مرور/جستجو کنید و هنگام کار راهنمای زمینه‌ای بگیرید.",
            "<p>مرکز راهنمای کامل را از آیکون راهنما در نوار بالا باز کنید. می‌توانید مقالات را بر اساس ماژول/دسته مرور کنید، یا " +
            "با کلیدواژه به انگلیسی یا فارسی جستجو کنید - جستجو به یک زبان همچنان می‌تواند مقاله‌ای را که عمدتاً به زبان دیگر " +
            "نوشته شده نشان دهد، اگر اصطلاح جستجو با آن مطابقت داشته باشد.</p>" +
            "<p>در صفحات پشتیبانی‌شده، دکمه‌ی راهنمای زمینه‌ای یک پنل کوتاه مخصوص همان صفحه باز می‌کند - این صفحه برای چیست، " +
            "فیلدهای مهم، اقدامات موجود و مقالات مرتبط - بدون این‌که کار جاری‌تان را ترک کنید. اگر دستیار هوش مصنوعی فعال " +
            "باشد، می‌توانید درباره‌ی همان مقاله‌ی باز‌شده سوال بپرسید؛ پاسخ فقط از محتوای واقعی همان مقاله ساخته می‌شود و اگر " +
            "چیزی هنوز مستند نشده، سیستم صریحاً همین را می‌گوید، نه این‌که حدس بزند.</p>",
            "مرکز راهنما,جستجو,راهنمای زمینه‌ای,پرسش از هوش مصنوعی",
            new[] { "welcome-to-pems" }),

        // ============================= ENGINEERING =============================
        new Art("engineering-module-overview", "engineering", "Module", null, 0,
            "Engineering Module Overview",
            "What the Engineering module covers and how its pieces connect.",
            "<p>The Engineering module manages the lifecycle of engineering documents: creation, internal review " +
            "(Check → IDC → Approval → Issue), revisions, transmittals to external parties, and links to related records " +
            "(equipment, contracts, vendors). It exists so that every issued technical document has a traceable, " +
            "authorized history - who prepared it, who checked it, who approved it, and under what revision it was issued.</p>" +
            "<p>Key concepts: a <strong>Document</strong> is the persistent record (number, title, discipline, current " +
            "status); a <strong>Revision</strong> is a specific version of that document's content, with its own status and " +
            "its own client-review outcome; <strong>IDC</strong> (Inter-Discipline Check) is the internal, advisory review " +
            "by other engineering disciplines before formal approval.</p>",
            "engineering,module,overview,document lifecycle",
            "معرفی ماژول مهندسی",
            "ماژول مهندسی چه چیزهایی را پوشش می‌دهد و اجزای آن چگونه به هم وصل‌اند.",
            "<p>ماژول مهندسی چرخه‌ی عمر مدارک مهندسی را مدیریت می‌کند: ایجاد، بازبینی داخلی (Check → IDC → Approval → Issue)، " +
            "ویرایش‌ها، نامه‌ی ارسال مدارک (Transmittal) به طرف‌های بیرونی، و ارتباط با رکوردهای مرتبط (تجهیز، قرارداد، " +
            "فروشنده). این ماژول وجود دارد تا هر مدرک فنی صادرشده یک تاریخچه‌ی قابل‌ردیابی و مجاز داشته باشد - چه کسی تهیه " +
            "کرد، چه کسی بررسی کرد، چه کسی تایید کرد، و تحت کدام ویرایش صادر شد.</p>" +
            "<p>مفاهیم کلیدی: <strong>مدرک (Document)</strong> رکورد پایدار است (شماره، عنوان، دیسیپلین، وضعیت فعلی)؛ " +
            "<strong>ویرایش (Revision)</strong> نسخه‌ی مشخصی از محتوای همان مدرک است، با وضعیت و نتیجه‌ی بازبینی کارفرمای " +
            "مخصوص به خودش؛ <strong>IDC</strong> (بازبینی بین‌دیسیپلینی) بازبینی داخلی و مشورتیِ سایر دیسیپلین‌های مهندسی " +
            "پیش از تایید رسمی است.</p>",
            "مهندسی,ماژول,معرفی,چرخه عمر مدرک",
            new[] { "engineering-documents-overview", "document-workflow", "idc-inter-discipline-check" }),

        new Art("engineering-documents-overview", "engineering", "Page", "EngineeringDocuments.Index", 1,
            "Engineering Documents — Document Register",
            "The main register page: what you can filter, and what actions are available per row.",
            "<p>This page lists all engineering documents with filters (project, discipline, status, document type) and a " +
            "searchable grid. From here you can open a document's details, create a new document (if you have Create " +
            "permission), and see each document's current status at a glance.</p>" +
            "<p><strong>Common mistake:</strong> a document's badge shows its internal workflow status (e.g. \"Checked\"), " +
            "which is not the same as whether it has actually been delivered to a client - client delivery/response is " +
            "tracked separately, after the document reaches \"Issued\".</p>",
            "document register,engineering documents,index,list",
            "مدارک مهندسی — رجیستر مدارک",
            "صفحه‌ی رجیستر اصلی: چه فیلترهایی دارید و روی هر ردیف چه اقداماتی ممکن است.",
            "<p>این صفحه همه‌ی مدارک مهندسی را با فیلتر (پروژه، دیسیپلین، وضعیت، نوع مدرک) و یک جدول قابل‌جستجو فهرست می‌کند. " +
            "از این‌جا می‌توانید جزئیات یک مدرک را باز کنید، مدرک جدید بسازید (اگر مجوز ایجاد داشته باشید)، و وضعیت فعلی هر " +
            "مدرک را در یک نگاه ببینید.</p>" +
            "<p><strong>اشتباه رایج:</strong> نشان (Badge) روی یک مدرک وضعیت داخلی گردش‌کار آن را نشان می‌دهد (مثلاً " +
            "«Checked»)، که با این‌که آیا واقعاً به کارفرما تحویل شده یکی نیست - تحویل/پاسخ کارفرما جداگانه و بعد از این‌که " +
            "مدرک به وضعیت «Issued» رسید ردیابی می‌شود.</p>",
            "رجیستر مدارک,مدارک مهندسی,فهرست",
            new[] { "engineering-documents-details", "document-workflow" }),

        new Art("engineering-documents-details", "engineering", "Page", "EngineeringDocuments.Details", 2,
            "Engineering Document Details",
            "The document's own page: tabs, action buttons, and what each workflow stage's buttons actually do.",
            "<p>The Details page shows the document's identity card (number, current status, revision) at the top, with " +
            "tabs for Overview, Attachments, IDC, Revisions, Workflow History and Correspondence. The action buttons shown " +
            "at the top always match the document's <em>current</em> workflow stage - e.g. at \"Submitted\" you'll see " +
            "Check/Reject/Revise &amp; Resubmit; at \"IDC Review\" you'll see your own discipline's review buttons (if you " +
            "have a pending review); at \"Checked\" you'll see the formal Approve/Reject/Revise &amp; Resubmit actions.</p>" +
            "<p><strong>Important:</strong> the IDC review buttons (labelled \"My IDC Review\") record only your own " +
            "discipline's opinion - they do not by themselves approve or reject the whole document. The formal decision is " +
            "always made separately, after all referred disciplines have responded, by whoever has Approval-stage " +
            "authority.</p>",
            "document details,workflow buttons,idc review,tabs",
            "جزئیات مدرک مهندسی",
            "صفحه‌ی خود مدرک: تب‌ها، دکمه‌های اقدام، و این‌که دکمه‌های هر مرحله‌ی گردش‌کار واقعاً چه می‌کنند.",
            "<p>صفحه‌ی جزئیات، کارت هویت مدرک (شماره، وضعیت فعلی، ویرایش) را بالای صفحه نشان می‌دهد، با تب‌های Overview، " +
            "پیوست‌ها، IDC، ویرایش‌ها، تاریخچه‌ی گردش‌کار و مکاتبات. دکمه‌های اقدام بالای صفحه همیشه با مرحله‌ی " +
            "<em>فعلی</em> گردش‌کار مدرک منطبق‌اند - مثلاً در وضعیت «Submitted» دکمه‌های Check/Reject/Revise &amp; Resubmit " +
            "را می‌بینید؛ در «IDC Review» دکمه‌های بازبینی دیسیپلین خودتان را می‌بینید (اگر بازبینی معلق داشته باشید)؛ در " +
            "«Checked» اقدامات رسمی Approve/Reject/Revise &amp; Resubmit را می‌بینید.</p>" +
            "<p><strong>نکته‌ی مهم:</strong> دکمه‌های بازبینی IDC (با برچسب «My IDC Review») فقط نظر دیسیپلین خودتان را ثبت " +
            "می‌کنند - به‌تنهایی کل مدرک را تایید یا رد نمی‌کنند. تصمیم رسمی همیشه جداگانه، بعد از پاسخ همه‌ی دیسیپلین‌های " +
            "ارجاع‌شده، توسط کسی که اختیار مرحله‌ی Approval را دارد گرفته می‌شود.</p>",
            "جزئیات مدرک,دکمه‌های گردش‌کار,بازبینی idc,تب‌ها",
            new[] { "document-workflow", "idc-inter-discipline-check", "document-revision-concept" }),

        new Art("document-revision-concept", "engineering", "Field", "EngineeringDocuments.Details.Field.Revision", 3,
            "Document Revision",
            "What a revision is, how the revision number changes, and what happens to old revisions.",
            "<p>A document's <strong>Revision</strong> represents a specific version of its content. Creating a new " +
            "revision increases the revision number, sets the new revision's status to Draft, and marks the previous " +
            "revision as Superseded - the previous revision is preserved, not deleted, so its history (including any " +
            "client review it received) remains available.</p>" +
            "<p>Client review status is tracked per revision, not on the document as a whole - this matters if a document " +
            "goes through more than one revision cycle with the client, since each revision's own client response stays " +
            "correctly attached to it.</p>",
            "revision,document revision,supersede",
            "ویرایش مدرک",
            "ویرایش چیست، شماره‌ی ویرایش چگونه تغییر می‌کند، و بر سر ویرایش‌های قدیمی چه می‌آید.",
            "<p><strong>ویرایش (Revision)</strong> یک نسخه‌ی مشخص از محتوای مدرک را نشان می‌دهد. ساخت ویرایش جدید، شماره‌ی " +
            "ویرایش را افزایش می‌دهد، وضعیت ویرایش جدید را Draft می‌کند، و ویرایش قبلی را Superseded علامت می‌زند - ویرایش " +
            "قبلی حذف نمی‌شود، بلکه حفظ می‌شود تا تاریخچه‌اش (از جمله هر بازبینی کارفرمایی که دریافت کرده) در دسترس بماند.</p>" +
            "<p>وضعیت بازبینی کارفرما به‌ازای هر ویرایش (نه کل مدرک) ردیابی می‌شود - این نکته وقتی مهم می‌شود که یک مدرک بیش " +
            "از یک چرخه‌ی بازبینی با کارفرما داشته باشد، چون پاسخ کارفرما برای هر ویرایش درست به همان ویرایش متصل می‌ماند.</p>",
            "ویرایش,ریویژن,جایگزینی",
            new[] { "engineering-documents-details" }),

        new Art("document-workflow", "engineering", "Workflow", null, 4,
            "Engineering Document Workflow",
            "The verified stage-by-stage lifecycle: Draft → Submitted → IDC Review → Checked → Approved → Issued.",
            "<p>A document's status moves through these stages: <strong>Draft</strong> (being prepared) → " +
            "<strong>Submitted</strong> (sent for Check) → <strong>Checked</strong> (Check passed; if inter-discipline " +
            "review was requested, the document sits at <strong>IDC Review</strong> in between until all referred " +
            "disciplines respond) → <strong>Approved</strong> → <strong>Issued</strong>. A document can also be moved to " +
            "<strong>Rejected</strong> or <strong>Revise &amp; Resubmit</strong> at the Check or Approval stage - the " +
            "latter is not a final rejection, it explicitly allows resubmission after correction.</p>" +
            "<p><strong>Who can do what</strong> (verified against the system's actual role gates): the Check stage is " +
            "performed by Engineering Supervisor/Manager/System Admin roles; the formal Approval stage additionally " +
            "requires that the approver did not themselves create or edit that same document (a segregation-of-duties " +
            "rule); Issue is performed by Document Controller/Engineering Manager/System Admin roles.</p>" +
            "<p>IDC (Inter-Discipline Check) is a separate, advisory sub-process within this workflow - see the dedicated " +
            "IDC article for how it works and, importantly, what it does <em>not</em> do.</p>",
            "workflow,document status,check,approval,issue,reject",
            "گردش‌کار مدرک مهندسی",
            "چرخه‌ی تاییدشده‌ی مرحله‌به‌مرحله: Draft → Submitted → IDC Review → Checked → Approved → Issued.",
            "<p>وضعیت مدرک از این مراحل عبور می‌کند: <strong>Draft</strong> (در حال آماده‌سازی) → <strong>Submitted</strong> " +
            "(ارسال‌شده برای Check) → <strong>Checked</strong> (Check گذرانده‌شده؛ اگر بازبینی بین‌دیسیپلینی درخواست شده " +
            "باشد، مدرک در این میان در وضعیت <strong>IDC Review</strong> می‌ماند تا همه‌ی دیسیپلین‌های ارجاع‌شده پاسخ " +
            "دهند) → <strong>Approved</strong> → <strong>Issued</strong>. مدرک همچنین می‌تواند در مرحله‌ی Check یا " +
            "Approval به <strong>Rejected</strong> یا <strong>Revise &amp; Resubmit</strong> برود - حالت دوم رد نهایی " +
            "نیست، صریحاً امکان ارسال دوباره بعد از اصلاح را می‌دهد.</p>" +
            "<p><strong>چه کسی چه کاری می‌تواند بکند</strong> (بر اساس محدودیت‌های نقشِ واقعی سیستم تاییدشده): مرحله‌ی " +
            "Check توسط نقش‌های Engineering Supervisor/Manager/System Admin انجام می‌شود؛ مرحله‌ی تایید رسمی (Approval) " +
            "علاوه‌بر آن نیاز دارد تاییدکننده خودش سازنده/ویرایشگر همان مدرک نباشد (قاعده‌ی تفکیک وظایف)؛ Issue توسط " +
            "نقش‌های Document Controller/Engineering Manager/System Admin انجام می‌شود.</p>" +
            "<p>IDC (بازبینی بین‌دیسیپلینی) یک زیرفرآیند مجزا و مشورتی درون همین گردش‌کار است - برای این‌که چگونه کار " +
            "می‌کند و مهم‌تر، چه کاری <em>انجام نمی‌دهد</em>، مقاله‌ی اختصاصی IDC را ببینید.</p>",
            "گردش کار,وضعیت مدرک,چک,تایید,ابلاغ,رد",
            new[] { "idc-inter-discipline-check", "engineering-documents-details" }),

        new Art("idc-inter-discipline-check", "engineering", "Workflow", null, 5,
            "IDC — Inter-Discipline Check",
            "IDC is an internal, advisory review. A discipline's review result never by itself rejects the document.",
            "<p>When a document is submitted, the originating discipline can nominate other disciplines (e.g. Mechanical, " +
            "Piping, Electrical, Instrumentation, Civil) to review it in parallel. Each nominated discipline records one " +
            "of three outcomes for its own review: <strong>No Comment</strong> (approve), <strong>Approve with " +
            "Comments</strong>, or <strong>Revision Required</strong>.</p>" +
            "<p><strong>This is the single most important rule of IDC:</strong> no individual discipline's review result " +
            "- including \"Revision Required\" - directly sets the document's own status to Rejected. Every discipline's " +
            "result is recorded independently and visible in the IDC tab (with the reviewer's comment and response date). " +
            "Once every nominated discipline has responded, the document moves to the formal <strong>Approval</strong> " +
            "stage (status \"Checked\"), where the actual document owner/approver reviews the consolidated IDC input and " +
            "makes the real decision: approve, formally reject, or return for revision.</p>" +
            "<p>A reviewer who has not yet responded is shown as pending - it is never silently treated as an approval.</p>",
            "idc,inter-discipline check,discipline review,revision required",
            "IDC — بازبینی بین‌دیسیپلینی",
            "IDC یک بازبینی داخلی و مشورتی است. نتیجه‌ی بازبینی یک دیسیپلین هرگز به‌تنهایی مدرک را رد نمی‌کند.",
            "<p>وقتی مدرکی Submit می‌شود، دیسیپلین سازنده می‌تواند سایر دیسیپلین‌ها (مثلاً مکانیک، پایپینگ، برق، ابزاردقیق، " +
            "عمران) را برای بازبینی موازی معرفی کند. هر دیسیپلینِ معرفی‌شده یکی از سه نتیجه را برای بازبینی خودش ثبت می‌کند: " +
            "<strong>بدون نظر</strong> (تایید)، <strong>تایید همراه با نظر</strong>، یا <strong>نیازمند اصلاح</strong>.</p>" +
            "<p><strong>این مهم‌ترین قاعده‌ی IDC است:</strong> نتیجه‌ی بازبینی هیچ دیسیپلین به‌تنهایی - حتی «نیازمند " +
            "اصلاح» - وضعیت خودِ مدرک را مستقیماً Rejected نمی‌کند. نتیجه‌ی هر دیسیپلین مستقل ثبت می‌شود و در تب IDC (همراه " +
            "با نظر و تاریخ پاسخ بازبین) قابل‌مشاهده است. وقتی همه‌ی دیسیپلین‌های معرفی‌شده پاسخ دادند، مدرک به مرحله‌ی رسمی " +
            "<strong>Approval</strong> می‌رود (وضعیت «Checked»)، جایی که سازنده/تأییدکننده‌ی واقعی مدرک با دیدن مجموع " +
            "نظرات IDC، تصمیم واقعی را می‌گیرد: تایید، رد رسمی، یا بازگشت برای اصلاح.</p>" +
            "<p>بازبینی که هنوز پاسخ نداده، «در انتظار» نشان داده می‌شود - هرگز به‌صورت خاموش به‌عنوان تایید در نظر گرفته " +
            "نمی‌شود.</p>",
            "idc,بازبینی بین دیسیپلینی,بازبینی دیسیپلین,نیازمند اصلاح",
            new[] { "document-workflow", "engineering-documents-details" }),

        // ============================= CMMS =============================
        new Art("cmms-overview", "cmms", "Module", null, 0,
            "CMMS Overview",
            "What the CMMS (maintenance) module manages and its core objects.",
            "<p>CMMS covers equipment and maintenance management: the equipment register, functional locations, " +
            "maintenance strategies, work requests, work orders, permits, and maintenance history. It exists to plan, " +
            "authorize, execute and record maintenance work in a traceable way, and to distinguish planned/preventive work " +
            "from reactive/corrective work.</p>",
            "cmms,maintenance,overview",
            "معرفی CMMS",
            "ماژول CMMS (نگهداری و تعمیرات) چه چیزهایی را مدیریت می‌کند و اشیای اصلی آن کدام‌اند.",
            "<p>CMMS مدیریت تجهیزات و نگهداری‌وتعمیرات را پوشش می‌دهد: رجیستر تجهیزات، موقعیت‌های فنی، استراتژی‌های نگهداری، " +
            "درخواست‌های کار، دستورهای کار، مجوزهای کار، و تاریخچه‌ی نگهداری‌وتعمیرات. این ماژول برای برنامه‌ریزی، مجازسازی، " +
            "اجرا و ثبت قابل‌ردیابیِ کارهای نگهداری‌وتعمیرات، و تفکیک کار برنامه‌ریزی‌شده/پیشگیرانه از کار واکنشی/اصلاحی وجود " +
            "دارد.</p>",
            "cmms,نگهداری و تعمیرات,معرفی",
            new[] { "functional-location-vs-equipment", "work-request-vs-work-order" }),

        new Art("functional-location-vs-equipment", "cmms", "Field", "FunctionalLocations.Index", 1,
            "Functional Location vs. Equipment",
            "A Functional Location is a place/role in the plant; Equipment is a physical asset that can be installed there.",
            "<p>A <strong>Functional Location</strong> represents a position or role within the plant's structure (an area, " +
            "a system, a specific installation point) - it stays the same even if the physical equipment installed there is " +
            "replaced. <strong>Equipment</strong> is the physical asset itself, and can optionally be linked to a " +
            "Functional Location to record where it is currently installed.</p>" +
            "<p>Keeping these separate lets maintenance history and planning stay attached to \"this position in the " +
            "plant\" even across equipment swaps, while equipment-specific history (serial number, manufacturer, " +
            "maintenance strategy) stays attached to the physical unit itself.</p>",
            "functional location,equipment,asset",
            "موقعیت فنی در برابر تجهیز",
            "موقعیت فنی یک جایگاه/نقش در واحد است؛ تجهیز یک دارایی فیزیکی است که می‌تواند در آن‌جا نصب شود.",
            "<p><strong>موقعیت فنی (Functional Location)</strong> یک جایگاه یا نقش در ساختار واحد را نشان می‌دهد (یک " +
            "منطقه، یک سیستم، یک نقطه‌ی نصب مشخص) - حتی اگر تجهیز فیزیکی نصب‌شده در آن‌جا عوض شود، همچنان ثابت می‌ماند. " +
            "<strong>تجهیز (Equipment)</strong> خودِ دارایی فیزیکی است، و می‌تواند به‌صورت اختیاری به یک موقعیت فنی وصل شود " +
            "تا محل نصب فعلی‌اش ثبت شود.</p>" +
            "<p>جدانگه‌داشتن این دو باعث می‌شود تاریخچه و برنامه‌ریزی نگهداری حتی در تعویض تجهیز به «این جایگاه در واحد» وصل " +
            "بماند، در حالی‌که تاریخچه‌ی مخصوص تجهیز (شماره‌ی سریال، سازنده، استراتژی نگهداری) به خودِ واحد فیزیکی وصل " +
            "می‌ماند.</p>",
            "موقعیت فنی,تجهیز,دارایی",
            new[] { "cmms-overview" }),

        new Art("work-request-vs-work-order", "cmms", "Workflow", "WorkRequests.Index", 2,
            "Work Request vs. Work Order",
            "A Work Request is a proposal for work; once approved and planned it converts into a Work Order for execution.",
            "<p>A <strong>Work Request</strong> is how maintenance work gets proposed: it moves through Draft → Submitted → " +
            "Under Review → Approved → Planned → Converted (or Rejected/Cancelled along the way). Once planned and " +
            "converted, it becomes a <strong>Work Order</strong> - the record that actually gets executed, which has its " +
            "own separate status flow: Open → Assigned → In Progress → Completed → Closed.</p>" +
            "<p>This separation exists so that \"someone proposed this work and it was reviewed/authorized\" (Work " +
            "Request) stays distinct from \"this is the work actually being carried out right now\" (Work Order) - useful " +
            "for planning, prioritization, and for corrective vs. preventive reporting.</p>",
            "work request,work order,maintenance planning",
            "درخواست کار در برابر دستور کار",
            "درخواست کار پیشنهاد انجام کار است؛ پس از تایید و برنامه‌ریزی، به دستور کار برای اجرا تبدیل می‌شود.",
            "<p><strong>درخواست کار (Work Request)</strong> نحوه‌ی پیشنهاد کار نگهداری‌وتعمیرات است: مراحل Draft → " +
            "Submitted → Under Review → Approved → Planned → Converted را طی می‌کند (یا در مسیر به Rejected/Cancelled " +
            "می‌رود). پس از برنامه‌ریزی و تبدیل، به <strong>دستور کار (Work Order)</strong> تبدیل می‌شود - رکوردی که " +
            "واقعاً اجرا می‌شود و چرخه‌ی وضعیت جداگانه‌ی خودش را دارد: Open → Assigned → In Progress → Completed → " +
            "Closed.</p>" +
            "<p>این تفکیک وجود دارد تا «این کار پیشنهاد شد و بازبینی/مجاز شد» (درخواست کار) از «این کاری است که همین الان " +
            "واقعاً در حال انجام است» (دستور کار) جدا بماند - برای برنامه‌ریزی، اولویت‌بندی، و گزارش‌گیری اصلاحی در برابر " +
            "پیشگیرانه مفید است.</p>",
            "درخواست کار,دستور کار,برنامه‌ریزی نگهداری",
            new[] { "cmms-overview" }),

        new Art("maintenance-strategy-overview", "cmms", "Field", "MaintenanceStrategies.Index", 3,
            "Maintenance Strategy",
            "How a strategy attaches to a specific equipment and drives when maintenance is due.",
            "<p>A Maintenance Strategy is defined per equipment and records the strategy type, the trigger type that " +
            "determines when work is due (e.g. Calendar-based), the review frequency, and - where relevant - the failure " +
            "mode and consequence category it addresses. This is what ties a piece of equipment to a recurring maintenance " +
            "plan rather than relying on ad-hoc work requests alone.</p>",
            "maintenance strategy,preventive maintenance,trigger",
            "استراتژی نگهداری",
            "استراتژی چگونه به یک تجهیز مشخص وصل می‌شود و زمان سررسید نگهداری را تعیین می‌کند.",
            "<p>استراتژی نگهداری به‌ازای هر تجهیز تعریف می‌شود و شامل نوع استراتژی، نوع محرکی که زمان سررسید کار را تعیین " +
            "می‌کند (مثلاً بر اساس تقویم)، دوره‌ی بازبینی، و - در صورت لزوم - حالت خرابی و دسته‌ی پیامدی که پوشش می‌دهد " +
            "می‌باشد. این همان چیزی است که یک تجهیز را به یک برنامه‌ی نگهداری تکرارشونده وصل می‌کند، به‌جای این‌که فقط به " +
            "درخواست‌های کار موردی متکی باشد.</p>",
            "استراتژی نگهداری,نگهداری پیشگیرانه,محرک",
            new[] { "cmms-overview" }),

        // ============================= OPERATIONS =============================
        new Art("operations-production-overview", "operations", "Module", null, 0,
            "Operations & Production Overview",
            "How Production Orders, Runs and Batches relate, and how output quality gates inventory posting.",
            "<p>The Operations &amp; Production module tracks manufacturing execution: a <strong>Production Order</strong> " +
            "is the plan (what should be produced); a <strong>Production Run</strong> is the actual execution against that " +
            "plan and records outputs, losses and downtime events; each output creates or adds to a " +
            "<strong>Production Batch</strong>, which carries its own quality status.</p>" +
            "<p><strong>Verified quality gate:</strong> a batch's output can only be posted into inventory once its " +
            "quality status is <strong>Released</strong> - a batch that is still Produced, Awaiting QC, Blocked or Rejected " +
            "cannot be posted, and the system blocks that action explicitly rather than allowing unreleased material to " +
            "become available stock.</p>",
            "production,operations,batch,quality status,inventory posting",
            "معرفی عملیات و تولید",
            "دستور تولید، اجرا و بچ چگونه به هم مرتبط‌اند، و کیفیت خروجی چگونه ارسال به موجودی را دروازه‌بانی می‌کند.",
            "<p>ماژول عملیات و تولید اجرای تولید را ردیابی می‌کند: <strong>دستور تولید (Production Order)</strong> برنامه " +
            "است (چه چیزی باید تولید شود)؛ <strong>اجرای تولید (Production Run)</strong> اجرای واقعیِ آن برنامه است و " +
            "خروجی‌ها، ضایعات و رویدادهای توقف را ثبت می‌کند؛ هر خروجی یک <strong>بچ تولید (Production Batch)</strong> " +
            "می‌سازد یا به آن اضافه می‌شود، که وضعیت کیفیِ خودش را دارد.</p>" +
            "<p><strong>دروازه‌ی کیفیِ تاییدشده:</strong> خروجی یک بچ فقط زمانی می‌تواند به موجودی ارسال شود که وضعیت کیفی " +
            "آن <strong>Released</strong> باشد - بچی که هنوز Produced، Awaiting QC، Blocked یا Rejected است قابل‌ارسال " +
            "نیست، و سیستم این اقدام را صراحتاً مسدود می‌کند، نه این‌که اجازه دهد ماده‌ی هنوز آزادنشده وارد موجودیِ در دسترس " +
            "شود.</p>",
            "تولید,عملیات,بچ,وضعیت کیفیت,ارسال به موجودی",
            null),

        // ============================= EMPLOYEE / TEAM =============================
        new Art("employee-profile-overview", "administration", "Page", "Employee.Details", 0,
            "Employee Profile",
            "What the Employee Details page shows, and which fields are backed by real data today.",
            "<p>An employee's profile brings together: employment details (role, department, discipline, personnel type - " +
            "Internal/Contractor/External/Temporary), their current team membership (with the team's supervisor shown as " +
            "\"Manager\"), contact information, and skills/qualifications/certifications/labor rate history.</p>" +
            "<p>An employee's \"Manager\" is not a separate stored field - it is derived from their current active team " +
            "membership's team supervisor, so it always reflects the real organizational assignment rather than a " +
            "manually-entered value that could drift out of date.</p>",
            "employee,profile,manager,team,personnel type",
            "پروفایل کارمند",
            "صفحه‌ی جزئیات کارمند چه چیزهایی نشان می‌دهد، و کدام فیلدها امروز داده‌ی واقعی پشت خود دارند.",
            "<p>پروفایل کارمند این موارد را کنار هم می‌آورد: جزئیات استخدام (نقش، دپارتمان، دیسیپلین، نوع پرسنل - " +
            "Internal/Contractor/External/Temporary)، عضویت فعلی در تیم (با سرپرست تیم که به‌عنوان «مدیر» نمایش داده " +
            "می‌شود)، اطلاعات تماس، و تاریخچه‌ی مهارت‌ها/صلاحیت‌ها/گواهینامه‌ها/نرخ دستمزد.</p>" +
            "<p>«مدیر» یک کارمند فیلد ذخیره‌شده‌ی جداگانه‌ای نیست - از سرپرستِ تیمِ عضویتِ فعالِ فعلیِ او مشتق می‌شود، پس " +
            "همیشه انتساب سازمانی واقعی را نشان می‌دهد، نه یک مقدار دستی که ممکن است از واقعیت عقب بماند.</p>",
            "کارمند,پروفایل,مدیر,تیم,نوع پرسنل",
            null),

        // ============================= ADMINISTRATION =============================
        new Art("users-roles-permissions-overview", "administration", "Module", null, 0,
            "Users, Roles and Permissions",
            "How access control works in PEMS: roles, and the difference between a role and a specific permission.",
            "<p>Every user is assigned one or more roles (e.g. System Admin, Engineering Manager, Engineering Supervisor, " +
            "Document Controller). Some actions are gated simply by role membership; others go through a more specific, " +
            "data-driven permission check for a given module and action (for example, whether your role can Create/Edit a " +
            "particular module's records), and some sensitive actions (like formally approving a document) also enforce a " +
            "segregation-of-duties rule - you cannot approve something you created or edited yourself.</p>" +
            "<p>Help content is role-aware where useful (an article can indicate which roles it's most relevant to), but " +
            "this is only a labeling/guidance aid - it never substitutes for or bypasses the actual permission checks " +
            "enforced by the system.</p>",
            "users,roles,permissions,rbac,segregation of duties",
            "کاربران، نقش‌ها و مجوزها",
            "کنترل دسترسی در PEMS چگونه کار می‌کند: نقش‌ها، و تفاوت بین یک نقش و یک مجوز مشخص.",
            "<p>به هر کاربر یک یا چند نقش تخصیص داده می‌شود (مثلاً System Admin، Engineering Manager، Engineering " +
            "Supervisor، Document Controller). برخی اقدامات صرفاً با عضویت در یک نقش مجاز می‌شوند؛ برخی دیگر از یک بررسی " +
            "مجوزِ داده‌محورتر برای یک ماژول و اقدام مشخص عبور می‌کنند (مثلاً این‌که آیا نقش شما اجازه‌ی Create/Edit رکوردهای " +
            "یک ماژول خاص را دارد)، و برخی اقدامات حساس (مثل تایید رسمی یک مدرک) قاعده‌ی تفکیک وظایف را هم اجرا می‌کنند - " +
            "نمی‌توانید چیزی را که خودتان ساخته یا ویرایش کرده‌اید تایید کنید.</p>" +
            "<p>محتوای راهنما در جاهایی که مفید است نقش‌آگاه است (یک مقاله می‌تواند نشان دهد بیشتر برای کدام نقش‌ها مرتبط " +
            "است)، اما این فقط یک ابزار برچسب‌گذاری/راهنمایی است - هرگز جایگزین یا دورزنِ بررسی‌های واقعی مجوز که سیستم اجرا " +
            "می‌کند نیست.</p>",
            "کاربران,نقش‌ها,مجوزها,کنترل دسترسی,تفکیک وظایف",
            null),

        new Art("audit-trail-overview", "administration", "Module", null, 1,
            "Audit Trail",
            "How PEMS records who changed what, automatically, for every tracked record.",
            "<p>Every significant record in PEMS is automatically covered by the audit log: when a record is created, " +
            "modified or deleted, the system captures who made the change, when, and (for edits) which fields changed - " +
            "with no extra action needed from whoever built that feature. This is separate from, and in addition to, any " +
            "workflow-specific history (such as an engineering document's own Check/IDC/Approval/Issue timeline).</p>",
            "audit log,audit trail,change history",
            "ردگیری رویدادها (Audit Trail)",
            "PEMS چگونه به‌صورت خودکار ثبت می‌کند چه کسی چه چیزی را تغییر داده، برای هر رکورد ردگیری‌شده.",
            "<p>هر رکورد مهم در PEMS به‌صورت خودکار توسط گزارش ممیزی (Audit Log) پوشش داده می‌شود: وقتی رکوردی ساخته، " +
            "ویرایش یا حذف می‌شود، سیستم ثبت می‌کند چه کسی تغییر را انجام داده، چه زمانی، و (برای ویرایش‌ها) کدام فیلدها " +
            "تغییر کرده‌اند - بدون نیاز به هیچ اقدام اضافه از سازنده‌ی آن ویژگی. این جدا از، و علاوه‌بر، هر تاریخچه‌ی " +
            "مخصوص گردش‌کار (مثل جدول زمانی Check/IDC/Approval/Issue خودِ یک مدرک مهندسی) است.</p>",
            "گزارش ممیزی,ردگیری رویدادها,تاریخچه تغییرات",
            null),

        // ============================= TROUBLESHOOTING =============================
        new Art("cannot-perform-workflow-action", "troubleshooting", "Troubleshooting", null, 0,
            "I can't perform an action on a document/record",
            "General guidance when a button is missing, disabled, or the action is refused.",
            "<p>If an expected action button (e.g. Approve, Issue, Edit) isn't visible, the most common reasons are: (1) " +
            "the record isn't currently at the workflow stage that action applies to - actions are shown only for the " +
            "current stage; (2) your role doesn't have the required permission for that specific action; (3) for approval-" +
            "type actions, a segregation-of-duties rule may block you specifically because you created or edited that same " +
            "record earlier. If an action is attempted and refused with a message, read the message - PEMS explains the " +
            "specific reason (e.g. wrong current status, missing permission, or a segregation-of-duties conflict) rather " +
            "than a generic error.</p>",
            "cannot approve,action missing,permission denied,segregation of duties",
            "نمی‌توانم روی یک مدرک/رکورد اقدامی انجام دهم",
            "راهنمای عمومی وقتی یک دکمه وجود ندارد، غیرفعال است، یا اقدام رد می‌شود.",
            "<p>اگر یک دکمه‌ی اقدام موردانتظار (مثلاً Approve، Issue، Edit) دیده نمی‌شود، رایج‌ترین دلایل این‌هاست: (۱) " +
            "رکورد در حال حاضر در مرحله‌ی گردش‌کاری که آن اقدام برایش معنا دارد نیست - اقدامات فقط برای مرحله‌ی فعلی نمایش " +
            "داده می‌شوند؛ (۲) نقش شما مجوز لازم برای همان اقدام مشخص را ندارد؛ (۳) برای اقدامات نوع تایید، ممکن است قاعده‌ی " +
            "تفکیک وظایف به‌طور خاص شما را مسدود کند چون خودتان قبلاً همان رکورد را ساخته یا ویرایش کرده‌اید. اگر اقدامی " +
            "انجام و با پیام رد شود، پیام را بخوانید - PEMS دلیل مشخص را توضیح می‌دهد (مثلاً وضعیت فعلی نادرست، مجوز ناقص، " +
            "یا تعارض تفکیک وظایف)، نه یک خطای عمومی.</p>",
            "امکان تایید نیست,دکمه وجود ندارد,دسترسی رد شد,تفکیک وظایف",
            new[] { "engineering-documents-details", "users-roles-permissions-overview" }),

        new Art("required-field-validation-errors", "troubleshooting", "Troubleshooting", null, 1,
            "Required field / validation errors when saving",
            "How to read and resolve a validation error message.",
            "<p>When a save is rejected, PEMS shows the specific reason - a missing required field, an invalid value, or a " +
            "business rule (such as a duplicate code that must be unique). The message names the actual field or " +
            "condition; there is no separate, undocumented validation layer - if a save fails, the displayed message is " +
            "the complete reason.</p>",
            "validation error,required field,duplicate code",
            "خطای فیلد الزامی / اعتبارسنجی هنگام ذخیره",
            "چگونه پیام خطای اعتبارسنجی را بخوانید و رفع کنید.",
            "<p>وقتی ذخیره‌سازی رد می‌شود، PEMS دلیل مشخص را نشان می‌دهد - یک فیلد الزامیِ خالی، مقدار نامعتبر، یا یک قاعده‌ی " +
            "کسب‌وکار (مثل کدی که باید یکتا باشد اما تکراری است). پیام، فیلد یا شرط واقعی را نام می‌برد؛ هیچ لایه‌ی " +
            "اعتبارسنجیِ جداگانه و مستندنشده‌ای وجود ندارد - اگر ذخیره ناموفق باشد، پیام نمایش‌داده‌شده دلیل کامل است.</p>",
            "خطای اعتبارسنجی,فیلد الزامی,کد تکراری",
            null),

        new Art("search-returns-no-results", "troubleshooting", "Troubleshooting", null, 2,
            "Search returns no results",
            "Why a search (global search, Help search, or a picker field) might come back empty.",
            "<p>Search in PEMS matches on specific fields (for example, name/code/email for people, or title/keywords for " +
            "Help articles) - it does not search every field of every record. If a term you expect to match doesn't return " +
            "anything, try a shorter or more specific fragment, check spelling, or search in the other language (Help " +
            "search checks both English and Persian content). For Help search specifically, only published articles are " +
            "returned - an article that exists but hasn't been published by an administrator will not appear.</p>",
            "search,no results,empty search",
            "جستجو نتیجه‌ای برنمی‌گرداند",
            "چرا یک جستجو (جستجوی سراسری، جستجوی راهنما، یا یک فیلد انتخاب‌گر) ممکن است خالی برگردد.",
            "<p>جستجو در PEMS روی فیلدهای مشخصی تطبیق می‌دهد (مثلاً نام/کد/ایمیل برای افراد، یا عنوان/کلیدواژه برای مقالات " +
            "راهنما) - همه‌ی فیلدهای همه‌ی رکوردها را جستجو نمی‌کند. اگر عبارتی که انتظار تطبیق دارید نتیجه‌ای نمی‌دهد، یک " +
            "بخش کوتاه‌تر یا دقیق‌تر امتحان کنید، املا را بررسی کنید، یا به زبان دیگر جستجو کنید (جستجوی راهنما هم محتوای " +
            "انگلیسی و هم فارسی را بررسی می‌کند). به‌طور خاص برای جستجوی راهنما، فقط مقالات منتشرشده برگردانده می‌شوند - " +
            "مقاله‌ای که وجود دارد اما هنوز توسط مدیر منتشر نشده نمایش داده نمی‌شود.</p>",
            "جستجو,بدون نتیجه,جستجوی خالی",
            new[] { "help-center-usage" }),

        // ==========================================================================
        // طبق «PEMS Help System — Phase 2»: گسترش پوششِ Contextual Help به صفحات واقعیِ
        // بیشتر - هر مقاله‌ی زیر دقیقاً با یک HelpKey تازه‌سیم‌شده در کنترلر واقعیِ متناظرش
        // منطبق است (تایید‌شده با Grep مستقیم روی کد قبل از نوشتن محتوا، نه حدس).
        // ==========================================================================

        new Art("equipment-register-overview", "cmms", "Page", "Equipment.Index", 4,
            "Equipment Register",
            "The master list of all equipment: filters, and what the list shows at a glance.",
            "<p>The Equipment Register lists every registered piece of equipment with filters for project, discipline, " +
            "equipment type and status, plus free-text search. From here you open an equipment's own page (its " +
            "\"Digital Equipment Passport\") for full details, or start a new equipment record.</p>" +
            "<p>An equipment record can optionally be linked to a Functional Location to record where it is currently " +
            "installed - the register shows both the equipment itself and, where set, its current position.</p>",
            "equipment,equipment register,equipment list",
            "رجیستر تجهیزات",
            "فهرست اصلی همه‌ی تجهیزات: فیلترها، و این‌که فهرست در یک نگاه چه چیزی نشان می‌دهد.",
            "<p>رجیستر تجهیزات همه‌ی تجهیزات ثبت‌شده را با فیلتر پروژه، دیسیپلین، نوع تجهیز و وضعیت، به‌همراه جستجوی آزاد " +
            "فهرست می‌کند. از این‌جا صفحه‌ی خود تجهیز («شناسنامه‌ی دیجیتال تجهیز») را برای جزئیات کامل باز می‌کنید، یا " +
            "رکورد تجهیز جدید می‌سازید.</p>" +
            "<p>یک رکورد تجهیز می‌تواند به‌صورت اختیاری به یک موقعیت فنی وصل شود تا محل نصب فعلی‌اش ثبت شود - رجیستر هم " +
            "خودِ تجهیز و هم، در صورت تنظیم، موقعیت فعلی‌اش را نشان می‌دهد.</p>",
            "تجهیز,رجیستر تجهیزات,فهرست تجهیزات",
            new[] { "functional-location-vs-equipment", "equipment-passport" }),

        new Art("equipment-passport", "cmms", "Page", "Equipment.Details", 5,
            "Digital Equipment Passport",
            "The equipment's own page: identity, specifications, and its links to maintenance and location.",
            "<p>The Digital Equipment Passport is a single equipment's full record: identity (tag number, type, " +
            "manufacturer, model, serial number), its current Functional Location (if linked), and its related " +
            "maintenance data (strategies, plans, history) reachable from the same page.</p>" +
            "<p>This is the record that a Maintenance Strategy attaches to (see the Maintenance Strategy article) - so " +
            "an equipment's passport is the natural starting point for understanding what recurring maintenance work " +
            "applies to it.</p>",
            "equipment passport,digital passport,equipment details",
            "شناسنامه‌ی دیجیتال تجهیز",
            "صفحه‌ی خودِ تجهیز: هویت، مشخصات، و ارتباط آن با نگهداری و موقعیت.",
            "<p>شناسنامه‌ی دیجیتال تجهیز، رکورد کامل یک تجهیز مشخص است: هویت (شماره‌ی تگ، نوع، سازنده، مدل، شماره‌ی " +
            "سریال)، موقعیت فنی فعلی‌اش (در صورت اتصال)، و داده‌های نگهداری مرتبط (استراتژی‌ها، برنامه‌ها، تاریخچه) که از " +
            "همان صفحه قابل‌دسترس‌اند.</p>" +
            "<p>این همان رکوردی است که یک استراتژی نگهداری به آن وصل می‌شود (مقاله‌ی استراتژی نگهداری را ببینید) - پس " +
            "شناسنامه‌ی تجهیز نقطه‌ی شروع طبیعی برای فهمیدن این است که چه کار نگهداری تکرارشونده‌ای روی آن اعمال می‌شود.</p>",
            "شناسنامه تجهیز,پاسپورت دیجیتال,جزئیات تجهیز",
            new[] { "equipment-register-overview", "maintenance-strategy-overview" }),

        new Art("work-orders-list", "cmms", "Page", "WorkOrders.Index", 6,
            "Work Orders — List",
            "Filtering by status, by equipment, or to only your own assigned orders.",
            "<p>This page lists work orders with filters for status, a specific equipment, and an \"assigned to me\" " +
            "toggle that narrows the list to work orders assigned to the current user. See the Work Request vs. Work " +
            "Order article for how a work order comes to exist and its status flow (Open → Assigned → In Progress → " +
            "Completed → Closed).</p>",
            "work orders,work order list,assigned to me",
            "دستورهای کار — فهرست",
            "فیلتر بر اساس وضعیت، تجهیز، یا فقط دستورهای کاری که به شما تخصیص یافته.",
            "<p>این صفحه دستورهای کار را با فیلتر وضعیت، یک تجهیز مشخص، و یک کلید «تخصیص‌یافته به من» که فهرست را به " +
            "دستورهای کاریِ تخصیص‌یافته به کاربر جاری محدود می‌کند، فهرست می‌کند. برای این‌که دستور کار چگونه ساخته می‌شود " +
            "و چرخه‌ی وضعیتش (Open → Assigned → In Progress → Completed → Closed) مقاله‌ی «درخواست کار در برابر دستور " +
            "کار» را ببینید.</p>",
            "دستور کار,فهرست دستور کار,تخصیص به من",
            new[] { "work-request-vs-work-order" }),

        new Art("work-order-details", "cmms", "Page", "WorkOrders.Details", 7,
            "Work Order Details",
            "The work order's own page: what it tracks and how it relates to ISO 14224 failure fields.",
            "<p>A work order's details page tracks the actual execution of maintenance work against an equipment, " +
            "including labor (via work order labor records) and, for corrective/emergency work, the ISO 14224-aligned " +
            "failure fields (failure mode code, failure cause code, root cause, downtime hours) required for reliability " +
            "analysis such as MTBF and recurring-failure-pattern detection.</p>",
            "work order details,iso 14224,failure mode,downtime",
            "جزئیات دستور کار",
            "صفحه‌ی خودِ دستور کار: چه چیزی را ردیابی می‌کند و چگونه به فیلدهای خرابی ISO 14224 مرتبط است.",
            "<p>صفحه‌ی جزئیات یک دستور کار، اجرای واقعیِ کار نگهداری روی یک تجهیز را ردیابی می‌کند، شامل نیروی کار (از " +
            "طریق رکوردهای نیروی کار دستور کار) و، برای کار اصلاحی/اضطراری، فیلدهای خرابیِ منطبق با ISO 14224 (کد حالت " +
            "خرابی، کد علت خرابی، ریشه‌ی خرابی، ساعات توقف) که برای تحلیل قابلیت‌اطمینان مثل MTBF و تشخیص الگوی خرابی " +
            "تکرارشونده لازم است.</p>",
            "جزئیات دستور کار,iso 14224,حالت خرابی,زمان توقف",
            new[] { "work-orders-list", "work-request-vs-work-order" }),

        new Art("maintenance-plans-overview", "cmms", "Page", "MaintenancePlans.Index", 8,
            "Maintenance Plans",
            "How a Maintenance Plan schedules recurring work from a Maintenance Strategy.",
            "<p>A Maintenance Plan is the schedulable layer built on top of a Maintenance Strategy - it can trigger on a " +
            "calendar basis, a meter/usage basis, or both, and tracks the next due date. This page lists all plans; each " +
            "plan's schedule is what eventually generates recurring maintenance work for its equipment.</p>",
            "maintenance plan,pm schedule,next due date",
            "برنامه‌های نگهداری",
            "برنامه‌ی نگهداری چگونه کار تکرارشونده را از یک استراتژی نگهداری زمان‌بندی می‌کند.",
            "<p>برنامه‌ی نگهداری، لایه‌ی قابل‌زمان‌بندیِ ساخته‌شده روی یک استراتژی نگهداری است - می‌تواند بر اساس تقویم، " +
            "بر اساس کارکرد/مصرف، یا هر دو فعال شود، و تاریخ سررسید بعدی را ردیابی می‌کند. این صفحه همه‌ی برنامه‌ها را " +
            "فهرست می‌کند؛ زمان‌بندی هر برنامه همان چیزی است که نهایتاً کار نگهداریِ تکرارشونده را برای تجهیزش تولید " +
            "می‌کند.</p>",
            "برنامه نگهداری,زمان‌بندی pm,تاریخ سررسید",
            new[] { "maintenance-strategy-overview" }),

        new Art("document-register-overview", "engineering", "Page", "DocumentRegister.Index", 9,
            "Document Register",
            "A project/discipline-filtered register of engineering documents, distinct from the main Engineering Documents list.",
            "<p>The Document Register is a filterable register (by project, discipline, status, search text) of " +
            "engineering documents - it exists as a register-style view alongside the Engineering Documents module's own " +
            "list, useful for a broad, cross-project view of document status.</p>",
            "document register,register",
            "رجیستر مدارک",
            "رجیستری از مدارک مهندسی با فیلتر پروژه/دیسیپلین - جدا از فهرست اصلیِ ماژول مدارک مهندسی.",
            "<p>رجیستر مدارک، یک رجیستر قابل‌فیلتر (بر اساس پروژه، دیسیپلین، وضعیت، متن جستجو) از مدارک مهندسی است - " +
            "به‌عنوان یک نمای رجیستری‌شکل در کنار فهرست خودِ ماژول مدارک مهندسی وجود دارد، برای دیدی گسترده و چندپروژه‌ای " +
            "از وضعیت مدارک مفید است.</p>",
            "رجیستر مدارک,رجیستر",
            new[] { "engineering-documents-overview" }),

        new Art("transmittals-overview", "engineering", "Page", "Transmittals.Index", 10,
            "Document Transmittals",
            "How documents are formally sent to other parties, and what a transmittal records.",
            "<p>A Transmittal is the formal record of documents being sent from one party to another (From/To), with a " +
            "stated Purpose (default \"For Information\") and its own status starting at Draft. It exists to give " +
            "document delivery its own auditable, numbered record - separate from the document's own internal " +
            "Check/IDC/Approval/Issue workflow.</p>",
            "transmittal,document transmittal,for information",
            "نامه‌ی ارسال مدارک (Transmittal)",
            "چگونه مدارک به‌صورت رسمی برای طرف‌های دیگر ارسال می‌شوند، و یک Transmittal چه چیزی را ثبت می‌کند.",
            "<p>Transmittal رکورد رسمیِ ارسال مدارک از یک طرف به طرف دیگر (از/به) است، با یک هدف مشخص (پیش‌فرض «برای " +
            "اطلاع») و وضعیت خودش که از Draft شروع می‌شود. این مفهوم وجود دارد تا تحویل مدرک، رکورد قابل‌ممیزی و " +
            "شماره‌دار خودش را داشته باشد - جدا از گردش‌کار داخلیِ خودِ مدرک (Check/IDC/Approval/Issue).</p>",
            "نامه ارسال مدارک,ترانسمیتال,برای اطلاع",
            new[] { "document-workflow" }),

        new Art("vdr-overview", "engineering", "Page", "VDR.Index", 11,
            "Vendor Document Register (VDR)",
            "How vendor-submitted documents are tracked separately from internally-authored engineering documents.",
            "<p>The Vendor Document Register (VDR) tracks documents submitted by vendors/contractors (as opposed to " +
            "documents authored internally in the Engineering Documents module). Each VDR item has its own workflow " +
            "status - Planned, Submitted, Under Review, Pending Resubmission, or Closed - reflecting the vendor " +
            "submission and review cycle, kept in step with the shared document status field used across the platform.</p>",
            "vdr,vendor document register,vendor submission",
            "رجیستر مدارک فروشنده (VDR)",
            "چگونه مدارک ارسالی فروشنده‌ها جدا از مدارک مهندسیِ داخلی ردیابی می‌شوند.",
            "<p>رجیستر مدارک فروشنده (VDR) مدارک ارسالی توسط فروشنده‌ها/پیمانکاران را (برخلاف مدارکی که داخلاً در ماژول " +
            "مدارک مهندسی تهیه می‌شوند) ردیابی می‌کند. هر آیتم VDR وضعیت گردش‌کار خودش را دارد - Planned، Submitted، " +
            "Under Review، Pending Resubmission، یا Closed - که چرخه‌ی ارسال و بازبینیِ فروشنده را نشان می‌دهد و با " +
            "فیلد وضعیت مشترکِ استفاده‌شده در کل پلتفرم هماهنگ نگه داشته می‌شود.</p>",
            "vdr,رجیستر مدارک فروشنده,ارسال فروشنده",
            new[] { "document-register-overview" }),

        new Art("material-requisitions-overview", "cmms", "Page", "MaterialRequisitions.Index", 12,
            "Material Requisitions (MR)",
            "How a material need moves from request to procurement.",
            "<p>A Material Requisition (MR) records a need for material/spare parts and moves through Draft → Submitted " +
            "→ Under Technical Review → Approved → Issued to Procurement → Closed (or Cancelled along the way). This is " +
            "the bridge between maintenance/engineering identifying a material need and procurement actually sourcing " +
            "it.</p>",
            "material requisition,mr,mrq,procurement",
            "درخواست مواد (MR)",
            "چگونه یک نیاز به مواد از درخواست تا تدارکات پیش می‌رود.",
            "<p>درخواست مواد (MR) نیاز به مواد/قطعات یدکی را ثبت می‌کند و مراحل Draft → Submitted → Under Technical " +
            "Review → Approved → Issued to Procurement → Closed را طی می‌کند (یا در مسیر به Cancelled می‌رود). این پل " +
            "بین تشخیص نیاز به مواد توسط نگهداری/مهندسی و تامین واقعیِ آن توسط تدارکات است.</p>",
            "درخواست مواد,mr,تدارکات",
            null),

        new Art("permits-overview", "cmms", "Workflow", "Permits.Index", 13,
            "Permit to Work",
            "The permit lifecycle, and how equipment isolation (LOTO) fits into it.",
            "<p>A Permit to Work moves through Draft → Submitted → Under Review → Approved → Issued → Active → " +
            "(Suspended if needed) → Closed - or Cancelled/Rejected/Revoked. Where hazardous energy isolation is " +
            "required, isolation points are managed as part of the permit itself (mark isolated → verify → de-isolate " +
            "when work is complete) rather than as a separate standalone module - so isolation status is always tied to " +
            "a specific permit.</p>",
            "permit,permit to work,isolation,loto,lockout tagout",
            "مجوز انجام کار (Permit to Work)",
            "چرخه‌ی عمر مجوز، و این‌که ایزوله‌سازی تجهیز (LOTO) چگونه در آن جای می‌گیرد.",
            "<p>مجوز انجام کار مراحل Draft → Submitted → Under Review → Approved → Issued → Active → (در صورت نیاز " +
            "Suspended) → Closed - یا Cancelled/Rejected/Revoked را طی می‌کند. جایی که ایزوله‌سازی انرژی خطرناک لازم " +
            "است، نقاط ایزوله به‌عنوان بخشی از خودِ مجوز مدیریت می‌شوند (علامت‌گذاری ایزوله ← تایید ← رفع ایزوله بعد از " +
            "اتمام کار) - نه به‌عنوان یک ماژول مستقل جدا - پس وضعیت ایزوله همیشه به یک مجوز مشخص وصل است.</p>",
            "مجوز کار,ایزوله سازی,قفل گذاری و برچسب گذاری",
            null),

        new Art("material-items-overview", "cmms", "Page", "MaterialItems.Index", 14,
            "Material Master / Inventory",
            "The master list of material items, with low-stock and critical filters.",
            "<p>The Material Master page lists material/spare-part items with filters for a text search, category, " +
            "low-stock items only, and critical items only. This is the master data that Material Requisitions, spare " +
            "part usage on work orders, and inventory transactions all reference.</p>",
            "material master,inventory,spare parts,low stock",
            "شناسنامه‌ی مواد / موجودی",
            "فهرست اصلی اقلام مواد، با فیلتر موجودی کم و اقلام بحرانی.",
            "<p>صفحه‌ی شناسنامه‌ی مواد، اقلام مواد/قطعات یدکی را با فیلتر جستجوی متنی، دسته، فقط اقلام با موجودی کم، و " +
            "فقط اقلام بحرانی فهرست می‌کند. این همان داده‌ی مرجعی است که درخواست‌های مواد، مصرف قطعه‌یدکی روی دستورهای " +
            "کار، و تراکنش‌های موجودی همگی به آن ارجاع می‌دهند.</p>",
            "شناسنامه مواد,موجودی,قطعات یدکی,موجودی کم",
            new[] { "material-requisitions-overview" }),

        new Art("departments-overview", "administration", "Page", "Departments.Index", 15,
            "Departments",
            "The organizational hierarchy that Employees and Positions are attached to.",
            "<p>Departments form a real, configurable organizational hierarchy (a department may have a parent " +
            "department) - this is what an Employee's own Department field, and Positions, are attached to. It is " +
            "independent of Discipline, which classifies technical specialty rather than organizational reporting line.</p>",
            "department,organization,hierarchy",
            "دپارتمان‌ها",
            "سلسله‌مراتب سازمانی که کارمندان و سمت‌ها به آن وصل‌اند.",
            "<p>دپارتمان‌ها یک سلسله‌مراتب سازمانیِ واقعی و قابل‌تنظیم تشکیل می‌دهند (یک دپارتمان می‌تواند دپارتمان والد " +
            "داشته باشد) - این همان چیزی است که فیلد دپارتمان خودِ کارمند، و سمت‌ها، به آن وصل‌اند. مستقل از دیسیپلین " +
            "است، که تخصص فنی را دسته‌بندی می‌کند نه خط گزارش‌دهی سازمانی را.</p>",
            "دپارتمان,سازمان,سلسله مراتب",
            new[] { "employee-profile-overview" }),

        new Art("inspections-overview", "cmms", "Page", "Inspections.Index", 16,
            "Inspections",
            "Recorded inspection activity, distinct from a full maintenance work order.",
            "<p>This page lists inspection records - a lighter-weight activity than a full maintenance work order, used " +
            "to record condition-checking activity on equipment/assets.</p>",
            "inspection,inspections",
            "بازرسی‌ها",
            "فعالیت بازرسی ثبت‌شده، جدا از یک دستور کار کامل نگهداری.",
            "<p>این صفحه رکوردهای بازرسی را فهرست می‌کند - فعالیتی سبک‌تر از یک دستور کار کامل نگهداری، برای ثبت فعالیت " +
            "بررسی وضعیت روی تجهیزات/دارایی‌ها.</p>",
            "بازرسی,بازرسی‌ها",
            null),

        new Art("admin-users-overview", "administration", "Page", "Admin.Users.Index", 17,
            "User Administration",
            "Managing login accounts, distinct from managing Employee records.",
            "<p>This page manages application login accounts (username, roles, active/inactive) - distinct from the " +
            "Employees page, which manages personnel records. A user account is optionally linked to an Employee record " +
            "(an Employee can exist without ever needing a login, e.g. a purely field-based technician tracked only for " +
            "labor-cost purposes).</p>",
            "users,user administration,login accounts",
            "مدیریت کاربران",
            "مدیریت حساب‌های ورود، جدا از مدیریت رکوردهای کارمند.",
            "<p>این صفحه حساب‌های ورود برنامه (نام کاربری، نقش‌ها، فعال/غیرفعال) را مدیریت می‌کند - جدا از صفحه‌ی " +
            "کارمندان که رکوردهای پرسنلی را مدیریت می‌کند. یک حساب کاربری به‌صورت اختیاری به یک رکورد کارمند وصل می‌شود " +
            "(یک کارمند می‌تواند بدون نیاز به ورود وجود داشته باشد، مثلاً یک تکنسین صرفاً میدانی که فقط برای هزینه‌ی " +
            "نیروی کار ردیابی می‌شود).</p>",
            "کاربران,مدیریت کاربران,حساب ورود",
            new[] { "users-roles-permissions-overview", "employee-profile-overview" }),

        new Art("admin-roles-overview", "administration", "Page", "Admin.Roles.Index", 18,
            "Role Administration",
            "The list of system roles that permissions and workflow authority are built on.",
            "<p>This page lists the system's roles - the same roles referenced throughout PEMS for both static role " +
            "checks (e.g. who can perform a Check on an engineering document) and, where used, dynamic module " +
            "permissions. Assigning a role to a user is what determines which pages, actions and workflow stages they " +
            "can reach.</p>",
            "roles,role administration,system roles",
            "مدیریت نقش‌ها",
            "فهرست نقش‌های سیستمی که مجوزها و اختیار گردش‌کار روی آن‌ها ساخته شده.",
            "<p>این صفحه نقش‌های سیستم را فهرست می‌کند - همان نقش‌هایی که در سراسر PEMS هم برای بررسی‌های ثابت نقش (مثلاً " +
            "چه کسی می‌تواند Check یک مدرک مهندسی را انجام دهد) و، در جاهایی که استفاده می‌شود، مجوزهای پویای ماژول " +
            "ارجاع داده می‌شوند. تخصیص یک نقش به کاربر همان چیزی است که تعیین می‌کند به کدام صفحات، اقدامات و مراحل " +
            "گردش‌کار دسترسی دارد.</p>",
            "نقش‌ها,مدیریت نقش,نقش سیستمی",
            new[] { "users-roles-permissions-overview", "admin-users-overview", "admin-permission-matrix-overview" }),

        new Art("admin-permission-matrix-overview", "administration", "Page", "Admin.PermissionMatrix.Index", 19,
            "Permission Matrix",
            "A single-screen view of which role can do which action, across modules.",
            "<p>The Permission Matrix gives a single-screen view of the dynamic, module-based permission grants (which " +
            "role can View/Create/Edit/Delete/etc. in which module) - useful for auditing access at a glance instead of " +
            "checking each module's configuration separately.</p>",
            "permission matrix,permissions,access control",
            "ماتریس مجوزها",
            "نمای یک‌صفحه‌ای از این‌که کدام نقش کدام اقدام را در کدام ماژول می‌تواند انجام دهد.",
            "<p>ماتریس مجوزها یک نمای یک‌صفحه‌ای از اعطای مجوزهای پویا و ماژول‌محور ارائه می‌دهد (کدام نقش می‌تواند " +
            "View/Create/Edit/Delete/... را در کدام ماژول انجام دهد) - برای ممیزی دسترسی در یک نگاه، به‌جای بررسی جداگانه‌ی " +
            "تنظیمات هر ماژول، مفید است.</p>",
            "ماتریس مجوز,مجوزها,کنترل دسترسی",
            new[] { "users-roles-permissions-overview", "admin-roles-overview" }),

        new Art("admin-audit-log-page", "administration", "Page", "Admin.AuditLog.Index", 20,
            "Audit Log — Screen Guide",
            "How to filter the audit log by entity or by user.",
            "<p>This screen lets you filter the automatic audit trail by entity name and by the user who made the " +
            "change, with paging for large result sets. See the general Audit Trail article for what gets recorded and " +
            "why.</p>",
            "audit log screen,filter audit log",
            "گزارش ممیزی — راهنمای صفحه",
            "چگونه گزارش ممیزی را بر اساس موجودیت یا کاربر فیلتر کنید.",
            "<p>این صفحه به شما اجازه می‌دهد ردگیری خودکار رویدادها را بر اساس نام موجودیت و کاربری که تغییر را انجام " +
            "داده فیلتر کنید، با صفحه‌بندی برای نتایج بزرگ. برای این‌که چه چیزی ثبت می‌شود و چرا، مقاله‌ی عمومی «ردگیری " +
            "رویدادها» را ببینید.</p>",
            "صفحه گزارش ممیزی,فیلتر ممیزی",
            new[] { "audit-trail-overview" }),
    };
}
