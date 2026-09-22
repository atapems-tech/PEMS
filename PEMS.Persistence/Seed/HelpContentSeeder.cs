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

        // طبق «PEMS Help Phase 4» بخش ۲۳ (ایدمپوتنت، بدون تغییر مخرب): این مقاله در فاز ۲ بدون
        // HelpKey seed شده بود (صفحه‌ی متناظرش - Production/Dashboard - وقتی هنوز HelpKey نداشت).
        // این‌جا فقط اگر مقاله از‌قبل وجود دارد و هنوز HelpKey ندارد، آن را ست می‌کنیم - محتوای
        // موجود دست‌نخورده می‌ماند، و اجرای دوباره هیچ تغییری اضافه نمی‌کند.
        var operationsOverview = await db.HelpArticles.FirstOrDefaultAsync(x => x.Code == "operations-production-overview");
        if (operationsOverview != null && string.IsNullOrEmpty(operationsOverview.HelpKey))
        {
            operationsOverview.HelpKey = "Production.Dashboard";
            await db.SaveChangesAsync();
        }

        // طبق «PEMS Help Phase 4 - Increment 3C» بند ۹ (Outcome B، اصلاح هدفمند، نه نوشتن مقاله‌ی
        // جدید): این پچ ایدمپوتنت است - فقط اگر متنِ دقیقِ نسخه‌ی قبل از اصلاح هنوز موجود باشد
        // (یعنی هیچ ادمینی محتوا را دستی تغییر نداده) آن را با نسخه‌ی اصلاح‌شده جایگزین می‌کند؛
        // اجرای دوباره هیچ تغییری اضافه نمی‌کند چون متن قدیمی دیگر پیدا نمی‌شود.
        await PatchReliabilityOverviewGapCaveatsAsync(db);

        // پیوندهای «مقاله‌ی مرتبط» - بعد از این‌که همه‌ی مقالات (چه تازه‌ساخته چه قبلاً موجود) شناسه دارند
        await LinkRelatedArticlesAsync(db, articles);
    }

    private static async Task PatchReliabilityOverviewGapCaveatsAsync(PEMSDbContext db)
    {
        var article = await db.HelpArticles
            .Include(x => x.Translations)
            .FirstOrDefaultAsync(x => x.Code == "reliability-overview");
        if (article == null) return;

        var en = article.Translations.FirstOrDefault(t => t.LanguageCode == "en");
        if (en != null)
        {
            const string oldAvailabilityEn = "<li><strong>Availability</strong> = MTBF / (MTBF + MTTR) × 100 - the standard Inherent Availability " +
                "formula, calculated only when both MTBF and MTTR are available.</li>";
            const string newAvailabilityEn = "<li><strong>Availability</strong> = MTBF / (MTBF + MTTR) × 100 - the standard Inherent Availability " +
                "formula, calculated only when both MTBF and MTTR are available. <strong>Verified limitation:</strong> this " +
                "result is not mathematically bounded to 0-100% - if a linked Work Order has an invalid duration (Completion " +
                "Date recorded before Start Date), MTTR can go negative and push Availability above 100%; PEMS does not " +
                "currently guard against this.</li>";
            const string oldParetoAnchorEn = "</ul>" +
                "<p><strong>Critical data-integrity fact (verified):</strong> a Failure Record is <em>never</em> created";
            const string newParetoAnchorEn = "</ul>" +
                "<p><strong>Known limitation (verified):</strong> the Failure Mode and Failure Cause Pareto charts on this " +
                "page, and the Failure Taxonomy Coverage indicator on the Data Quality page, rely on a structured " +
                "failure-mode/cause classification field that is not currently populated by any PEMS workflow. These charts " +
                "and this indicator may show little or no data even when the free-text failure mode/cause used by Bad Actors " +
                "above is fully populated - this is a data-wiring gap, not a sign of a healthy or unhealthy fleet.</p>" +
                "<p><strong>Critical data-integrity fact (verified):</strong> a Failure Record is <em>never</em> created";

            if (en.Content.Contains(oldAvailabilityEn))
                en.Content = en.Content.Replace(oldAvailabilityEn, newAvailabilityEn);
            if (en.Content.Contains(oldParetoAnchorEn))
                en.Content = en.Content.Replace(oldParetoAnchorEn, newParetoAnchorEn);
        }

        var fa = article.Translations.FirstOrDefault(t => t.LanguageCode == "fa");
        if (fa != null)
        {
            const string oldAvailabilityFa = "<li><strong>Availability</strong> = MTBF / (MTBF + MTTR) × ۱۰۰ - فرمول استاندارد Inherent Availability، " +
                "فقط وقتی هر دو MTBF و MTTR در دسترس باشند محاسبه می‌شود.</li>";
            const string newAvailabilityFa = "<li><strong>Availability</strong> = MTBF / (MTBF + MTTR) × ۱۰۰ - فرمول استاندارد Inherent Availability، " +
                "فقط وقتی هر دو MTBF و MTTR در دسترس باشند محاسبه می‌شود. <strong>محدودیت تاییدشده:</strong> این نتیجه از نظر " +
                "ریاضی به بازه‌ی ۰-۱۰۰٪ محدود نیست - اگر دستور کارِ مرتبط مدت نامعتبری داشته باشد (تاریخ تکمیل قبل از تاریخ " +
                "شروع ثبت شده باشد)، MTTR می‌تواند منفی شود و Availability را بالای ۱۰۰٪ ببرد؛ PEMS در حال حاضر در برابر این " +
                "حالت محافظتی ندارد.</li>";
            const string oldParetoAnchorFa = "</ul>" +
                "<p><strong>واقعیت حیاتیِ یکپارچگیِ داده (تاییدشده):</strong> یک رکورد خرابی <em>هرگز</em> از طریق فرم";
            const string newParetoAnchorFa = "</ul>" +
                "<p><strong>محدودیت تاییدشده:</strong> نمودارهای Pareto حالت خرابی و علت خرابی در این صفحه، و شاخص پوشش " +
                "طبقه‌بندیِ خرابی (Failure Taxonomy Coverage) در صفحه‌ی کیفیت داده، به یک فیلد طبقه‌بندیِ ساختاریافته‌ی " +
                "حالت/علت خرابی وابسته‌اند که در حال حاضر توسط هیچ روندی در PEMS پر نمی‌شود. این نمودارها و این شاخص ممکن " +
                "است داده‌ی کم یا هیچ نشان دهند حتی وقتی فیلد متنیِ حالت/علت خرابی (که Bad Actors بالا از آن استفاده می‌کند) " +
                "کاملاً پر شده باشد - این یک شکاف در اتصال داده است، نه نشانه‌ی سلامت یا ناسلامتیِ ناوگان.</p>" +
                "<p><strong>واقعیت حیاتیِ یکپارچگیِ داده (تاییدشده):</strong> یک رکورد خرابی <em>هرگز</em> از طریق فرم";

            if (fa.Content.Contains(oldAvailabilityFa))
                fa.Content = fa.Content.Replace(oldAvailabilityFa, newAvailabilityFa);
            if (fa.Content.Contains(oldParetoAnchorFa))
                fa.Content = fa.Content.Replace(oldParetoAnchorFa, newParetoAnchorFa);
        }

        await db.SaveChangesAsync();
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

        // طبق «PEMS Site-Wide UI/UX Redesign»: راهنمای یکپارچه‌ی الگوهای رابط کاربری (دوزبانه) - فقط توضیح می‌دهد کاربر
        // چه چیزی را کجا ببیند؛ هیچ فرایند کسب‌وکاری تغییر نکرده است.
        new Art("ui-conventions", "general", "Module", null, 3,
            "Screen layout and status conventions",
            "How pages, search, filters, statuses and action buttons are laid out consistently across PEMS.",
            "<p>Every PEMS page follows the same layout. The page title sits in one compact row at the top (with the module name beside it); " +
            "there is no address bar-style path above it - use the sidebar to move between modules. The sidebar shows icons only and " +
            "slides open when you move the mouse over it, and closes when you move away.</p>" +
            "<p>Lists have one search box; extra criteria live behind the Filters button and appear as removable chips once applied. " +
            "Each page has one main (filled) action; secondary actions are outlined, and destructive actions such as Delete are separated " +
            "and ask for confirmation.</p>" +
            "<p>A status is always shown as icon + text + colour - never colour alone: green with a check means approved/completed/active, " +
            "red with a cross means rejected/critical/overdue, amber with a triangle means attention/on hold, blue means in progress or " +
            "informational, grey means draft/inactive/cancelled. The same status looks the same in every module. Identifiers such as tag " +
            "numbers, document numbers, revisions and dates always read left-to-right, even on Persian pages.</p>",
            "layout,status,colors,search,filters,sidebar,rtl",
            "چیدمان صفحه‌ها و قراردادهای نمایش وضعیت",
            "چگونه صفحه‌ها، جستجو، فیلترها، وضعیت‌ها و دکمه‌های عملیات در همه‌ی PEMS یکسان چیده شده‌اند.",
            "<p>همه‌ی صفحه‌های PEMS چیدمان یکسانی دارند. عنوان صفحه در یک ردیف فشرده در بالا قرار دارد (نام ماژول کنارش) و بالای آن " +
            "مسیر یا آدرس فنی نمایش داده نمی‌شود - برای جابه‌جایی بین ماژول‌ها از نوار کناری استفاده کنید. نوار کناری فقط آیکون نشان می‌دهد، " +
            "با رفتن ماوس روی آن باز می‌شود و با دور شدن ماوس بسته می‌شود.</p>" +
            "<p>هر فهرست یک کادر جستجو دارد؛ معیارهای بیشتر پشت دکمه‌ی «فیلترها» هستند و پس از اعمال به‌شکل چیپ‌های قابل‌حذف دیده می‌شوند. " +
            "هر صفحه یک عملیات اصلی (پُر) دارد؛ عملیات فرعی به‌صورت خط‌دار و عملیات مخرب مانند حذف جدا شده و تأیید می‌خواهند.</p>" +
            "<p>وضعیت همیشه با آیکون + متن + رنگ نشان داده می‌شود و هرگز فقط رنگ نیست: سبز با تیک یعنی تأییدشده/تکمیل‌شده/فعال، قرمز با ضربدر یعنی ردشده/بحرانی/" +
            "عقب‌افتاده، کهربایی با مثلث یعنی نیازمند توجه/متوقف، آبی یعنی در جریان یا اطلاع‌رسانی، خاکستری یعنی پیش‌نویس/غیرفعال/لغوشده. یک وضعیت " +
            "در همه‌ی ماژول‌ها یکسان دیده می‌شود. شناسه‌هایی مانند شماره‌ی تگ، شماره‌ی مدرک، ویرایش و تاریخ همیشه چپ‌به‌راست خوانده می‌شوند، حتی در صفحه‌های فارسی.</p>",
            "چیدمان,وضعیت,رنگ,جستجو,فیلتر,نوار کناری",
            new[] { "help-center-usage" }),

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

        // ============================= PHASE 4: ENGINEERING COMMUNICATION =============================
        new Art("moc-overview", "engineering", "Workflow", "MOC.Index", 12,
            "Management of Change (MOC)",
            "The verified MOC lifecycle: Risk Assessment, Approval, a risk-based Senior Approval, and a gated Closure.",
            "<p>A <strong>Management of Change (MOC)</strong> request is raised against a project/discipline, optionally " +
            "linked to a related engineering document or piece of equipment, and given a <strong>Risk Level</strong> of " +
            "<strong>Low</strong>, <strong>Medium</strong> or <strong>High</strong>. Its verified status flow is: " +
            "<strong>Pending Risk Assessment</strong> → (risk assessment recorded) → <strong>Pending Approval</strong> " +
            "→ <strong>Approved</strong>. If, and only if, the Risk Level is <strong>High</strong>, approval does not " +
            "finish the workflow - it instead moves to a dedicated <strong>Pending Senior Approval</strong> stage requiring " +
            "an independent System Admin/Engineering Manager decision. Low/Medium-risk MOCs skip that stage automatically.</p>" +
            "<p>Approval (of either kind) can instead end in <strong>Rejected</strong>. Once approved, implementation is " +
            "recorded (status <strong>Pending Closure</strong>), and <strong>Close</strong> is verified to be blocked by the " +
            "system unless the affected documents have actually been marked updated, and - if the MOC was flagged as " +
            "requiring training - unless that training has actually been marked complete.</p>",
            "moc,management of change,risk assessment,senior approval",
            "مدیریت تغییر (MOC)",
            "چرخه‌ی تاییدشده‌ی MOC: ارزیابی ریسک، تایید، تایید ارشد (فقط ریسک بالا)، و بستنی که مشروط است.",
            "<p>یک درخواست <strong>مدیریت تغییر (MOC)</strong> برای یک پروژه/دیسیپلین ثبت می‌شود، به‌صورت اختیاری به یک " +
            "مدرک مهندسی یا تجهیز مرتبط وصل می‌شود، و یک <strong>سطح ریسک</strong> <strong>Low</strong>، " +
            "<strong>Medium</strong> یا <strong>High</strong> می‌گیرد. چرخه‌ی وضعیت تاییدشده‌ی آن: " +
            "<strong>Pending Risk Assessment</strong> ← (ارزیابی ریسک ثبت می‌شود) → <strong>Pending " +
            "Approval</strong> → <strong>Approved</strong>. اگر و فقط اگر سطح ریسک <strong>High</strong> باشد، تایید " +
            "به پایان گردش‌کار نمی‌رسد - در عوض به مرحله‌ی اختصاصی <strong>Pending Senior Approval</strong> می‌رود که " +
            "نیازمند تصمیم مستقل System Admin/Engineering Manager است. MOC های با ریسک Low/Medium این مرحله را به‌صورت " +
            "خودکار رد می‌کنند.</p>" +
            "<p>تایید (از هر نوع) می‌تواند به‌جای آن به <strong>Rejected</strong> ختم شود. پس از تایید، اجرا ثبت می‌شود " +
            "(وضعیت <strong>Pending Closure</strong>)، و تاییدشده است که <strong>Close</strong> توسط سیستم مسدود می‌شود " +
            "مگر این‌که مدارک متاثر واقعاً به‌روزرسانی‌شده علامت‌گذاری شده باشند، و - اگر MOC نیازمند آموزش علامت‌گذاری شده " +
            "بود - مگر این‌که آن آموزش واقعاً تکمیل‌شده علامت‌گذاری شده باشد.</p>",
            "moc,مدیریت تغییر,ارزیابی ریسک,تایید ارشد",
            new[] { "document-workflow" }),

        new Art("technical-query-overview", "engineering", "Workflow", "TechnicalQuery.Index", 13,
            "TQ / TSR / RFI",
            "The verified query-response lifecycle, including the Reopen loop back to the responding discipline.",
            "<p>A query is raised with one of three verified types - <strong>TQ</strong>, <strong>TSR</strong> or " +
            "<strong>RFI</strong> (the codebase defines TQ as \"Technical Query\"; it does not define an expansion for " +
            "TSR, so it is shown here as-is rather than guessed) - against a project and discipline. Its verified status " +
            "flow is: <strong>Open</strong> → (the responsible discipline responds) → <strong>Pending " +
            "Confirmation</strong> → either <strong>Closed</strong> (the raiser confirms the response) or " +
            "<strong>Reopened</strong> (the raiser rejects the response, which sends it back to the responding discipline " +
            "for another answer).</p>",
            "tq,tsr,rfi,technical query,reopen",
            "TQ / TSR / RFI",
            "چرخه‌ی تاییدشده‌ی پرسش-پاسخ، شامل حلقه‌ی بازگشتی Reopen به دیسیپلین پاسخ‌دهنده.",
            "<p>یک پرسش با یکی از سه نوع تاییدشده - <strong>TQ</strong>، <strong>TSR</strong> یا <strong>RFI</strong> " +
            "(کد پایه TQ را «Technical Query» تعریف می‌کند؛ بسطی برای TSR در کد تعریف نشده، پس همین‌طور که هست نمایش " +
            "داده می‌شود، نه حدس‌زده) - برای یک پروژه و دیسیپلین ثبت می‌شود. چرخه‌ی وضعیت تاییدشده‌ی آن: " +
            "<strong>Open</strong> → (دیسیپلین مسئول پاسخ می‌دهد) → <strong>Pending Confirmation</strong> → " +
            "یا <strong>Closed</strong> (ثبت‌کننده پاسخ را تایید می‌کند) یا <strong>Reopened</strong> (ثبت‌کننده پاسخ را " +
            "رد می‌کند، که آن را برای پاسخ دوباره به دیسیپلین پاسخ‌دهنده برمی‌گرداند).</p>",
            "tq,tsr,rfi,پرسش فنی,بازگشایی",
            new[] { "engineering-documents-overview" }),

        new Art("correspondence-overview", "engineering", "Workflow", "Letter.Index", 14,
            "Correspondence",
            "The verified letter lifecycle from Draft to Closed, and what Refer does (and does not) mean.",
            "<p>A letter's verified status flow is: <strong>Draft</strong> (a draft can only be saved while it is Draft " +
            "or ReturnedForRevision) → <strong>Pending Approval</strong> → either <strong>Approved</strong> or " +
            "<strong>Returned For Revision</strong> (which can only be resubmitted, sending it back to Pending Approval) " +
            "or <strong>Rejected</strong>. An approved letter is then <strong>Issued</strong> and finally " +
            "<strong>Closed</strong>.</p>" +
            "<p><strong>Referring</strong> a letter to another employee is a separate, parallel concept from this status " +
            "flow - it creates a per-recipient referral (with its own read/completion tracking) without changing the " +
            "letter's own status. A reply letter is linked to its parent, and creating one marks the parent as " +
            "\"Referred\".</p>",
            "correspondence,letter,referral,approval",
            "مکاتبات",
            "چرخه‌ی تاییدشده‌ی نامه از Draft تا Closed، و این‌که Refer دقیقاً به چه معناست (و به چه معنایی نیست).",
            "<p>چرخه‌ی وضعیت تاییدشده‌ی یک نامه: <strong>Draft</strong> (پیش‌نویس فقط وقتی Draft یا ReturnedForRevision " +
            "باشد قابل‌ذخیره است) → <strong>Pending Approval</strong> → یا <strong>Approved</strong> یا " +
            "<strong>Returned For Revision</strong> (که فقط قابل ارسال دوباره است و به Pending Approval برمی‌گردد) یا " +
            "<strong>Rejected</strong>. نامه‌ی تاییدشده سپس <strong>Issued</strong> و در نهایت <strong>Closed</strong> " +
            "می‌شود.</p>" +
            "<p><strong>ارجاع (Refer)</strong> نامه به کارمند دیگر مفهومی جدا و موازی از این چرخه‌ی وضعیت است - یک ارجاعِ " +
            "مختص به هر گیرنده (با ردگیری خواندن/تکمیل خودش) می‌سازد بدون این‌که وضعیت خودِ نامه را تغییر دهد. نامه‌ی " +
            "پاسخ به والدش وصل است، و ساختن آن والد را «ارجاع‌شده» علامت می‌زند.</p>",
            "مکاتبات,نامه,ارجاع,تایید",
            null),

        new Art("contracts-overview", "engineering", "Page", "Contract.Index", 15,
            "Contracts",
            "What a contract record tracks, and the verified status set offered by the UI.",
            "<p>A contract links a project to a vendor and records the contract number, scope of work, value/currency, " +
            "and start/end dates. Its status is changed via a dropdown offering exactly four verified values - " +
            "<strong>Draft</strong>, <strong>Active</strong>, <strong>Completed</strong>, <strong>Terminated</strong> - " +
            "and, unlike most other PEMS lifecycles in this Help system, the system does not currently enforce a strict " +
            "order between them; any of the four can be selected from any other. Changing status requires the " +
            "\"Contracts / Approve\" permission.</p>",
            "contract,vendor,scope of work",
            "قراردادها",
            "یک رکورد قرارداد چه چیزی را ردیابی می‌کند، و مجموعه‌ی وضعیتِ تاییدشده‌ای که رابط کاربری ارائه می‌دهد.",
            "<p>یک قرارداد یک پروژه را به یک وندور وصل می‌کند و شماره‌ی قرارداد، محدوده‌ی کار، ارزش/ارز، و تاریخ‌های " +
            "شروع/پایان را ثبت می‌کند. وضعیت آن از طریق یک منوی کشویی با دقیقاً چهار مقدار تاییدشده تغییر می‌کند - " +
            "<strong>Draft</strong>، <strong>Active</strong>، <strong>Completed</strong>، <strong>Terminated</strong> - " +
            "و، برخلاف بیشتر چرخه‌های دیگر PEMS در این راهنما، سیستم در حال حاضر ترتیب سخت‌گیرانه‌ای بین آن‌ها اجرا " +
            "نمی‌کند؛ هر کدام از این چهار مورد از هر مورد دیگری قابل‌انتخاب است. تغییر وضعیت نیازمند مجوز «Contracts / " +
            "Approve» است.</p>",
            "قرارداد,وندور,محدوده کار",
            new[] { "vendors-overview" }),

        new Art("vendors-overview", "engineering", "Page", "Vendor.Index", 16,
            "Vendors and Risk Score",
            "How a vendor's Risk Score is actually calculated - entirely from its VDR submission history.",
            "<p>A vendor's <strong>Risk Score</strong> (0 = lowest risk, 1 = highest) is a weighted, verified formula: " +
            "<strong>40%</strong> the vendor's overdue-submission rate across its Vendor Document Register (VDR) items, " +
            "<strong>40%</strong> its resubmission rate (how often a reviewed submission was found to require " +
            "resubmission), and <strong>20%</strong> the inverse of its VDR item closure rate. The resulting score maps " +
            "to a <strong>Risk Level</strong> of <strong>Low</strong> (&lt; 0.25), <strong>Medium</strong> (&lt; 0.55) or " +
            "<strong>High</strong> (≥ 0.55). A vendor with no VDR items at all is shown as <strong>Unknown</strong>, " +
            "not as low-risk. This means the score reflects only VDR document performance - it is not a general vendor " +
            "quality or financial rating.</p>",
            "vendor,risk score,vdr,resubmission",
            "وندورها و امتیاز ریسک",
            "امتیاز ریسک یک وندور واقعاً چگونه محاسبه می‌شود - کاملاً بر اساس تاریخچه‌ی ارسال VDR آن.",
            "<p><strong>امتیاز ریسک (Risk Score)</strong> یک وندور (۰ = کم‌ترین ریسک، ۱ = بیشترین ریسک) یک فرمول وزن‌دار " +
            "و تاییدشده است: <strong>۴۰٪</strong> نرخ تاخیر ارسال در آیتم‌های VDR آن وندور، <strong>۴۰٪</strong> نرخ " +
            "اصلاح دوباره (چند بار یک ارسالِ بازبینی‌شده نیازمند ارسال دوباره تشخیص داده شده)، و <strong>۲۰٪</strong> " +
            "معکوسِ نرخ بسته‌شدن آیتم‌های VDR آن. امتیاز نهایی به <strong>سطح ریسک</strong> <strong>Low</strong> " +
            "(&lt; 0.25)، <strong>Medium</strong> (&lt; 0.55) یا <strong>High</strong> (≥ 0.55) نگاشت می‌شود. " +
            "وندوری که اصلاً آیتم VDR ندارد <strong>Unknown</strong> نمایش داده می‌شود، نه کم‌ریسک. این یعنی امتیاز فقط " +
            "عملکرد مدارک VDR را نشان می‌دهد - یک رتبه‌بندی کلی کیفیت یا مالی وندور نیست.</p>",
            "وندور,امتیاز ریسک,vdr,اصلاح دوباره",
            new[] { "vdr-overview", "contracts-overview" }),

        new Art("document-revisions-overview", "engineering", "Page", "DocumentRevisions.Index", 17,
            "Document Revision History",
            "A cross-document revision browser - and why new revisions are created elsewhere, not here.",
            "<p>This page is a read-only, filterable history of revisions across all engineering documents (or, when " +
            "opened from a specific document, just that document's own revisions) - revision number, previous revision " +
            "number, status, issue purpose, revision date, and who prepared/checked/approved it.</p>" +
            "<p><strong>Verified deliberately:</strong> this page does not let you create a new revision directly - " +
            "attempting to reaches a redirect back to the document's own Details page with a message pointing to the " +
            "\"Create New Revision\" action there instead. That is by design: creating a revision through the document's " +
            "own workflow is what correctly numbers it, records who created it, and supersedes the previous revision - a " +
            "shortcut that bypassed this was found and closed. There is currently no revision comparison/diff feature.</p>",
            "revision,document revision,history",
            "تاریخچه‌ی بازنگری مدرک",
            "یک مرورگر بازنگری بین‌مدرکی - و این‌که چرا بازنگری‌های جدید جای دیگری ساخته می‌شوند، نه این‌جا.",
            "<p>این صفحه یک تاریخچه‌ی فقط-خواندنی و قابل‌فیلتر از بازنگری‌ها در همه‌ی مدارک مهندسی است (یا، وقتی از یک " +
            "مدرک مشخص باز شود، فقط بازنگری‌های همان مدرک) - شماره‌ی بازنگری، شماره‌ی بازنگری قبلی، وضعیت، هدف صدور، تاریخ " +
            "بازنگری، و این‌که چه کسی آن را تهیه/بررسی/تایید کرده.</p>" +
            "<p><strong>عمداً تاییدشده:</strong> این صفحه اجازه‌ی ساخت مستقیم یک بازنگری جدید را نمی‌دهد - تلاش برای این " +
            "کار به بازگشت به صفحه‌ی جزئیات خودِ مدرک با پیامی که به اقدام «Create New Revision» همان‌جا اشاره می‌کند " +
            "ختم می‌شود. این عمدی است: ساخت بازنگری از طریق گردش‌کار خودِ مدرک است که به‌درستی شماره‌گذاری می‌کند، ثبت " +
            "می‌کند چه کسی ساخته، و بازنگری قبلی را جایگزین می‌کند - میان‌بری که این را دور می‌زد پیدا و بسته شد. در حال " +
            "حاضر قابلیت مقایسه/Diff بازنگری‌ها وجود ندارد.</p>",
            "بازنگری,بازنگری مدرک,تاریخچه",
            new[] { "document-workflow" }),

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

        new Art("work-request-workflow", "cmms", "Workflow", "WorkRequests.Details", 3,
            "How a Work Request moves through the workflow",
            "The approval flow of a Work Request is configured in the central Workflow Platform; the page shows whatever stages that configuration defines.",
            "<p>A Work Request is created as a <strong>Draft</strong>. <em>Submit to Workflow</em> starts a workflow instance using the " +
            "Work Request workflow that is explicitly bound for new requests. The timeline on the Work Request page lists the stages of that " +
            "workflow version, highlights the current stage and shows who can act on it.</p>" +
            "<p>The buttons under <em>Actions</em> are the actions the workflow offers for the current stage. An action that you are not " +
            "permitted to perform is shown disabled, and the server checks your permission again when you press it. " +
            "<em>Convert to Work Order</em> is a Work Request business action (it needs a linked equipment and the Planned stage).</p>" +
            "<p><strong>Versions:</strong> when an administrator publishes a new workflow version (for example one that adds a review stage) " +
            "and binds it, only Work Requests submitted afterwards use it. Requests already in progress stay on the version they started with " +
            "and keep showing its stages. Requests that were in progress before the workflow platform was introduced have no workflow instance " +
            "and are marked as legacy records; they continue with the previous status buttons.</p>" +
            "<p><strong>History:</strong> <em>Workflow History</em> lists workflow actions; <em>Business Audit</em> lists status changes of the " +
            "Work Request itself.</p>",
            "work request,workflow,approval,timeline",
            "مسیر گردش‌کار درخواست کار",
            "مسیر تأیید درخواست کار در پلتفرم مرکزی گردش‌کار پیکربندی می‌شود؛ صفحه دقیقاً همان مراحل پیکربندی را نشان می‌دهد.",
            "<p>درخواست کار ابتدا به‌صورت <strong>پیش‌نویس</strong> ساخته می‌شود. دکمه‌ی <em>ارسال به گردش‌کار</em> یک نمونه‌ی گردش‌کار را با " +
            "گردش‌کاری که به‌صورت صریح برای درخواست‌های جدید متصل شده شروع می‌کند. نوار مراحل در صفحه‌ی درخواست کار، مراحل همان نسخه‌ی " +
            "گردش‌کار را فهرست می‌کند، مرحله‌ی جاری را مشخص می‌کند و نشان می‌دهد چه کسانی می‌توانند اقدام کنند.</p>" +
            "<p>دکمه‌های بخش <em>عملیات</em> همان اقدام‌هایی هستند که گردش‌کار برای مرحله‌ی جاری ارائه می‌دهد. اقدامی که مجوزش را ندارید " +
            "غیرفعال نمایش داده می‌شود و سرور هنگام زدن دکمه دوباره مجوز شما را بررسی می‌کند. <em>تبدیل به دستور کار</em> یک اقدام کسب‌وکاری " +
            "درخواست کار است (نیازمند تجهیز مرتبط و مرحله‌ی برنامه‌ریزی‌شده).</p>" +
            "<p><strong>نسخه‌ها:</strong> وقتی مدیر نسخه‌ی جدیدی از گردش‌کار (مثلاً با یک مرحله‌ی بازبینی اضافه) منتشر و متصل می‌کند، فقط " +
            "درخواست‌هایی که بعد از آن ارسال می‌شوند از آن استفاده می‌کنند. درخواست‌های در جریان، روی نسخه‌ای که با آن شروع شده‌اند می‌مانند و " +
            "همان مراحل را نشان می‌دهند. درخواست‌هایی که پیش از معرفی پلتفرم گردش‌کار در جریان بوده‌اند نمونه‌ی گردش‌کار ندارند، به‌عنوان " +
            "رکورد قدیمی علامت‌گذاری می‌شوند و با دکمه‌های وضعیت قبلی ادامه می‌دهند.</p>" +
            "<p><strong>تاریخچه:</strong> <em>تاریخچه گردش‌کار</em> اقدام‌های گردش‌کار را فهرست می‌کند و <em>ممیزی کسب‌وکاری</em> تغییر وضعیت خود " +
            "درخواست کار را.</p>",
            "درخواست کار,گردش‌کار,تأیید,نوار مراحل",
            new[] { "work-request-vs-work-order" }),

        new Art("work-order-lifecycle", "cmms", "Workflow", "WorkOrders.Lifecycle", 4,
            "How a Work Order moves through its lifecycle",
            "A Work Order is governed by the central Workflow Platform: planning, preparation, Operations release, execution, completion, verification by the originating discipline, final approval and closure.",
            "<p>New Work Orders start a lifecycle workflow that is explicitly bound and version-pinned. The timeline on the Work Order page shows " +
            "the stages of that version: <em>Created → Maintenance Planning → Preparation / Assignment → Operations Issue / Release → In Execution → " +
            "Completion → Originating-Discipline Verification → Final Approval → Closed</em>. <em>On Hold</em>, <em>Cancelled</em> and <em>Rejected</em> are " +
            "workflow transitions. The Work Order status you see in lists is a projection of the current stage.</p>" +
            "<p><strong>Who does what.</strong> Maintenance plans, prepares and executes the work; Operations issues / releases it for execution; " +
            "Engineering appears only as the <em>originating discipline</em> when the request came from Engineering, and verifies and finally approves the " +
            "result. Warehouse has no workflow authority. The originating discipline (who asked for the work) and the responsible maintenance " +
            "discipline (who does it) are recorded separately; Engineering / Mechanical is not the same as Maintenance / Mechanical.</p>" +
            "<p><strong>Separation of duties.</strong> The server enforces: the planner cannot execute, the Operations issuer cannot execute, the executor " +
            "cannot verify, the verifier cannot give final approval and the creator cannot close. Creator and executor may be the same person. " +
            "Managers and deputies may cancel or reject, but cannot final-approve work they executed or verified. Emergency Work Orders follow the " +
            "same path - there is no bypass.</p>" +
            "<p><strong>Actions.</strong> The buttons under <em>Actions</em> come from the workflow for the current stage; an action you cannot perform is " +
            "shown disabled with the reason, and the server re-checks it. A running multi-discipline review is a separate process: it blocks completion, and " +
            "cancelling the Work Order cancels it. Work Orders that existed before the workflow platform are <em>legacy</em> records and keep the previous status buttons.</p>" +
            "<p><strong>Versions.</strong> If an administrator publishes and binds a new version (for example adding a review stage), only Work Orders created afterwards " +
            "use it; existing Work Orders stay on the version they started with.</p>",
            "work order,lifecycle,workflow,separation of duties,originating discipline",
            "مسیر چرخه‌عمر دستور کار",
            "دستور کار توسط پلتفرم مرکزی گردش‌کار اداره می‌شود: برنامه‌ریزی، آماده‌سازی، صدور توسط عملیات، اجرا، تکمیل، تأیید دیسیپلین مبدأ، تصویب نهایی و بسته‌شدن.",
            "<p>دستور کار جدید یک گردش‌کار چرخه‌عمر را شروع می‌کند که به‌صورت صریح متصل و روی یک نسخه ثابت شده است. نوار مراحل صفحه‌ی دستور کار مراحل همان نسخه را " +
            "نشان می‌دهد: <em>ایجادشده ← برنامه‌ریزی نگهداری ← آماده‌سازی / تخصیص ← صدور / آزادسازی توسط عملیات ← در حال اجرا ← تکمیل ← تأیید دیسیپلین مبدأ ← تصویب نهایی ← " +
            "بسته‌شده</em>. <em>متوقف</em>، <em>لغو</em> و <em>رد</em> گذارهای گردش‌کار هستند. وضعیت دستور کار در فهرست‌ها نمایشی از مرحله‌ی جاری است.</p>" +
            "<p><strong>چه کسی چه کاری می‌کند.</strong> نگهداری برنامه‌ریزی، آماده‌سازی و اجرا را انجام می‌دهد؛ عملیات کار را برای اجرا صادر / آزاد می‌کند؛ مهندسی فقط زمانی " +
            "به‌عنوان <em>دیسیپلین مبدأ</em> ظاهر می‌شود که درخواست از مهندسی آمده باشد و نتیجه را تأیید و تصویب نهایی می‌کند. انبار اختیار گردش‌کاری ندارد. دیسیپلین مبدأ (درخواست‌دهنده) و " +
            "دیسیپلین مسئول نگهداری (اجراکننده) جداگانه ثبت می‌شوند؛ مهندسی / مکانیک با نگهداری / مکانیک یکسان نیست.</p>" +
            "<p><strong>تفکیک وظایف.</strong> سرور این موارد را اعمال می‌کند: برنامه‌ریز نمی‌تواند اجرا کند، صادرکننده‌ی عملیات نمی‌تواند اجرا کند، مجری نمی‌تواند تأیید کند، تأییدکننده نمی‌تواند تصویب نهایی کند و " +
            "ایجادکننده نمی‌تواند ببندد. ایجادکننده و مجری می‌توانند یک نفر باشند. مدیر و معاون می‌توانند لغو یا رد کنند اما نمی‌توانند کاری را که خودشان اجرا یا تأیید کرده‌اند تصویب نهایی کنند. " +
            "دستور کار اضطراری همین مسیر را می‌رود؛ میان‌بری وجود ندارد.</p>" +
            "<p><strong>اقدام‌ها.</strong> دکمه‌های بخش <em>عملیات</em> از گردش‌کار مرحله‌ی جاری می‌آیند؛ اقدامی که مجاز نیستید همراه با دلیل غیرفعال نمایش داده می‌شود و سرور دوباره آن را بررسی می‌کند. " +
            "بازبینی چند-دیسیپلینه‌ی در جریان یک فرآیند جداست: تکمیل را مسدود می‌کند و لغو دستور کار آن را هم لغو می‌کند. دستور کارهای پیش از پلتفرم گردش‌کار، رکورد <em>قدیمی</em> هستند و با دکمه‌های وضعیت قبلی ادامه می‌دهند.</p>" +
            "<p><strong>نسخه‌ها.</strong> اگر مدیر نسخه‌ی جدیدی (مثلاً با یک مرحله‌ی بازبینی اضافه) منتشر و متصل کند، فقط دستور کارهای ایجادشده بعد از آن از آن استفاده می‌کنند؛ دستور کارهای موجود روی نسخه‌ی خودشان می‌مانند.</p>",
            "دستور کار,چرخه‌عمر,گردش‌کار,تفکیک وظایف,دیسیپلین مبدأ",
            new[] { "work-request-workflow", "work-request-vs-work-order" }),

        new Art("workflow-management", "administration", "Workflow", "Admin.WorkflowManagement", 1,
            "Workflow Management",
            "Administrators can inspect workflows and safely change them by cloning a version into a draft, validating, publishing and binding it - without editing code.",
            "<p><strong>Where:</strong> Administration → Workflow Management. Access needs the SystemAdmin role <em>and</em> the matching WorkflowManagement permission (View, Edit, Validate, Publish, Bind, Retire); the server checks both, hiding a button is not the protection.</p>" +
            "<p><strong>Definition and version.</strong> A <em>workflow definition</em> (for example Work Order Lifecycle) has numbered <em>versions</em>: Draft → Validated → Published → Retired. A published or retired version is <em>immutable</em>. To change a workflow, <em>clone</em> a published version into a new <em>draft</em> (only one open draft per definition), edit it, validate it, publish it and then bind it. A draft you no longer need can be <em>discarded</em> (kept for audit, no longer editable).</p>" +
            "<p><strong>Designer.</strong> The graph is drawn from the real stages and transitions of the version (automatic layout, nothing is stored). Select a stage or a transition to edit it. <em>Stage</em>: name, Persian name, business status, order, type, <em>responsible role</em> and <em>responsible employee</em>, approval strategy, timeout. <em>Transition</em>: from, to (empty = start / end), action, label, Persian label, <em>required permission</em>, condition, priority. There is no separate participant table: responsibility is the stage's responsible role/employee, and authorization of an action is the transition's required permission.</p>" +
            "<p><strong>Validation.</strong> The platform validator checks the draft (reachable stages, terminal transition, known roles and permissions, registered actions and more). Errors block publication; warnings do not. The server validates again when you publish.</p>" +
            "<p><strong>Publishing and four-eyes.</strong> The last editor of a draft cannot publish it; another administrator must. Publishing makes the version immutable.</p>" +
            "<p><strong>Binding and version pinning.</strong> The binding decides which published version <em>new</em> instances use. Changing it needs an explicit confirmation that shows the current and target version and the running instances. Running instances stay pinned to the version they started with; nothing is rewritten.</p>" +
            "<p><strong>Rollback.</strong> There is no rollback button. Roll back by binding again to an older published version (or clone it, validate, publish and bind the clone). Old versions are never modified.</p>" +
            "<p><strong>Concurrency.</strong> If another administrator saved the same draft after you opened it, your save is rejected with a message; reload and repeat your change. Nothing is overwritten silently.</p>" +
            "<p><strong>Release 1 scope.</strong> Editable: Work Request, Work Order Lifecycle and Work Order Multi-Discipline Review. Other definitions (legacy EDMS, VDR, TQ, MOC, NCR, Permit) are <em>View Only</em>; having no active binding is a normal state, not an error.</p>",
            "workflow,administration,designer,version,draft,publish,binding,pinning",
            "مدیریت گردش‌کار",
            "مدیران می‌توانند گردش‌کارها را ببینند و با کپی نسخه به پیش‌نویس، اعتبارسنجی، انتشار و اتصال، بدون تغییر کد و به‌صورت ایمن تغییر دهند.",
            "<p><strong>مسیر:</strong> مدیریت ← مدیریت گردش‌کار. دسترسی هم نقش SystemAdmin و هم مجوز متناظر WorkflowManagement (مشاهده، ویرایش، اعتبارسنجی، انتشار، اتصال، بازنشسته‌سازی) را می‌خواهد؛ سرور هر دو را بررسی می‌کند و پنهان‌کردن دکمه محافظت نیست.</p>" +
            "<p><strong>تعریف و نسخه.</strong> هر <em>تعریف گردش‌کار</em> (مثلاً چرخه‌عمر دستور کار) <em>نسخه</em>های شماره‌دار دارد: پیش‌نویس ← اعتبارسنجی‌شده ← منتشرشده ← بازنشسته. نسخه‌ی منتشرشده یا بازنشسته <em>تغییرناپذیر</em> است. برای تغییر، یک نسخه‌ی منتشرشده را به یک <em>پیش‌نویس</em> جدید <em>کپی</em> کنید (هر تعریف فقط یک پیش‌نویس باز دارد)، ویرایش، اعتبارسنجی، منتشر و سپس متصل کنید. پیش‌نویسی که لازم نیست <em>رها</em> می‌شود (برای ممیزی می‌ماند و دیگر قابل ویرایش نیست).</p>" +
            "<p><strong>طراح.</strong> گراف از مراحل و گذارهای واقعی نسخه رسم می‌شود (چیدمان خودکار و بدون ذخیره). یک مرحله یا گذار را انتخاب و ویرایش کنید. <em>مرحله</em>: نام، نام فارسی، وضعیت کسب‌وکاری، ترتیب، نوع، <em>نقش مسئول</em> و <em>کارمند مسئول</em>، راهبرد تأیید، مهلت. <em>گذار</em>: مبدأ، مقصد (خالی = شروع / پایان)، اقدام، برچسب، برچسب فارسی، <em>مجوز لازم</em>، شرط، اولویت. جدول جداگانه‌ای برای شرکت‌کننده وجود ندارد: مسئولیت همان نقش/کارمند مسئول مرحله است و مجاز بودن اقدام همان مجوز لازم گذار.</p>" +
            "<p><strong>اعتبارسنجی.</strong> اعتبارسنج پلتفرم پیش‌نویس را بررسی می‌کند (مراحل قابل‌دسترسی، گذار پایانی، نقش‌ها و مجوزهای شناخته‌شده، اقدام‌های ثبت‌شده و ...). خطا انتشار را مسدود می‌کند و هشدار نه. سرور هنگام انتشار دوباره اعتبارسنجی می‌کند.</p>" +
            "<p><strong>انتشار و چهارچشمی.</strong> آخرین ویرایشگر پیش‌نویس نمی‌تواند آن را منتشر کند؛ مدیر دیگری باید منتشر کند. انتشار نسخه را تغییرناپذیر می‌کند.</p>" +
            "<p><strong>اتصال و ثابت‌ماندن نسخه.</strong> اتصال تعیین می‌کند نمونه‌های <em>جدید</em> از کدام نسخه‌ی منتشرشده استفاده کنند. تغییر آن تأیید صریح می‌خواهد و نسخه‌ی جاری و هدف و نمونه‌های در جریان را نشان می‌دهد. نمونه‌های در جریان روی نسخه‌ی شروعشان می‌مانند و چیزی بازنویسی نمی‌شود.</p>" +
            "<p><strong>بازگشت.</strong> دکمه‌ی بازگشت وجود ندارد. با اتصال دوباره به یک نسخه‌ی منتشرشده‌ی قدیمی‌تر (یا کپی، اعتبارسنجی، انتشار و اتصال آن) برگردید. نسخه‌های قدیمی هرگز تغییر نمی‌کنند.</p>" +
            "<p><strong>هم‌زمانی.</strong> اگر مدیر دیگری پس از باز کردن شما همان پیش‌نویس را ذخیره کرده باشد، ذخیره‌ی شما با پیام رد می‌شود؛ صفحه را دوباره بارگذاری و تغییر را تکرار کنید. هیچ چیز بی‌صدا بازنویسی نمی‌شود.</p>" +
            "<p><strong>دامنه‌ی انتشار ۱.</strong> قابل‌ویرایش: درخواست کار، چرخه‌عمر دستور کار و بازبینی چند-دیسیپلینه‌ی دستور کار. تعریف‌های دیگر (EDMS قدیمی، VDR، TQ، MOC، NCR، Permit) <em>فقط مشاهده</em> هستند؛ نداشتن اتصال فعال وضعیت عادی است، نه خطا.</p>",
            "گردش‌کار,مدیریت,طراح,نسخه,پیش‌نویس,انتشار,اتصال",
            new[] { "work-order-lifecycle", "work-request-workflow" }),

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

        // ============================= PHASE 4: QC/QA =============================
        new Art("qc-qa-overview", "cmms", "Module", "QC.Index", 17,
            "QC / QA",
            "Inspection & Test Plans and Non-Conformance Reports - what each one is for.",
            "<p>The QC/QA hub covers two verified object types. An <strong>Inspection &amp; Test Plan (ITP)</strong> " +
            "starts as <strong>Draft</strong>, is approved to become <strong>Active</strong>, and its individual line " +
            "items move from <strong>Pending</strong> to <strong>Notified</strong> (a hold point has been raised) to " +
            "either <strong>Passed</strong> or <strong>Failed</strong> once an inspection result is recorded. A " +
            "<strong>Non-Conformance Report (NCR)</strong> is raised with a <strong>Severity</strong> of " +
            "<strong>Minor</strong>, <strong>Major</strong> or <strong>Critical</strong> and a QC discipline (a separate " +
            "concept from engineering disciplines) - see the dedicated NCR article for its verified disposition/closure " +
            "workflow, including the extra approval step that only applies to Critical NCRs.</p>",
            "qc,qa,itp,ncr,inspection and test plan",
            "QC / QA",
            "برنامه‌ی بازرسی و آزمون (ITP) و گزارش عدم‌انطباق (NCR) - هرکدام برای چه چیزی است.",
            "<p>مرکز QC/QA دو نوع شیء تاییدشده را پوشش می‌دهد. <strong>برنامه‌ی بازرسی و آزمون (ITP)</strong> به‌صورت " +
            "<strong>Draft</strong> شروع می‌شود، با تایید به <strong>Active</strong> می‌رود، و آیتم‌های خط آن از " +
            "<strong>Pending</strong> به <strong>Notified</strong> (یک نقطه‌ی توقف اعلام شده) و سپس با ثبت نتیجه‌ی " +
            "بازرسی به <strong>Passed</strong> یا <strong>Failed</strong> می‌روند. <strong>گزارش عدم‌انطباق (NCR)</strong> " +
            "با یک <strong>شدت (Severity)</strong> <strong>Minor</strong>، <strong>Major</strong> یا " +
            "<strong>Critical</strong> و یک دیسیپلین QC (مفهومی جدا از دیسیپلین‌های مهندسی) ثبت می‌شود - برای گردش‌کار " +
            "تاییدشده‌ی Disposition/بستن آن، شامل مرحله‌ی تایید اضافه‌ای که فقط برای NCR های Critical اعمال می‌شود، مقاله‌ی " +
            "اختصاصی NCR را ببینید.</p>",
            "qc,qa,itp,ncr,برنامه بازرسی و آزمون",
            new[] { "ncr-critical-approval-workflow" }),

        new Art("ncr-critical-approval-workflow", "cmms", "Workflow", null, 18,
            "NCR Disposition and Critical Approval",
            "The verified NCR lifecycle, and the extra independent-approval step required only for Critical severity.",
            "<p>A Non-Conformance Report's verified status flow is: <strong>Open</strong> → (a disposition is " +
            "recorded) → for <strong>Minor</strong>/<strong>Major</strong> severity, directly to " +
            "<strong>Dispositioned</strong>; for <strong>Critical</strong> severity, first to <strong>Pending " +
            "Approval</strong>, requiring an independent QA Manager/System Admin decision before it can reach " +
            "<strong>Dispositioned</strong> (a Reject sends it back to <strong>Open</strong>, not to Dispositioned). " +
            "Only once <strong>Dispositioned</strong> can an NCR be <strong>Closed</strong>. This extra approval gate " +
            "exists, per the code's own reasoning, so a critical non-conformance cannot be closed out on the sole " +
            "authority of the person who recorded its disposition.</p>",
            "ncr,non-conformance,disposition,critical,qa approval",
            "بستن و تایید NCR بحرانی",
            "چرخه‌ی تاییدشده‌ی NCR، و مرحله‌ی تایید مستقل اضافه که فقط برای شدت Critical لازم است.",
            "<p>چرخه‌ی وضعیت تاییدشده‌ی یک گزارش عدم‌انطباق: <strong>Open</strong> → (یک Disposition ثبت می‌شود) " +
            "→ برای شدت <strong>Minor</strong>/<strong>Major</strong>، مستقیماً به <strong>Dispositioned</strong>؛ " +
            "برای شدت <strong>Critical</strong>، ابتدا به <strong>Pending Approval</strong>، که نیازمند تصمیم مستقل " +
            "مدیر QA/System Admin پیش از رسیدن به <strong>Dispositioned</strong> است (رد آن را به <strong>Open</strong> " +
            "برمی‌گرداند، نه به Dispositioned). فقط پس از <strong>Dispositioned</strong> است که یک NCR می‌تواند " +
            "<strong>Closed</strong> شود. این دروازه‌ی تایید اضافه، طبق استدلال خودِ کد، وجود دارد تا یک عدم‌انطباق " +
            "بحرانی نتواند صرفاً با اختیار همان کسی که Disposition آن را ثبت کرده بسته شود.</p>",
            "ncr,عدم انطباق,disposition,بحرانی,تایید qa",
            new[] { "qc-qa-overview" }),

        // ============================= PHASE 4 INCREMENT 2 =============================
        new Art("permit-details", "cmms", "Page", "Permit.Details", 19,
            "Permit Details - Isolation, Gas Test and the Approval Chain",
            "How the Permit's own status, its internal approval workflow, and its embedded Isolation/Gas Test safety data fit together.",
            "<p>A permit's own verified status flow is: <strong>Draft</strong> → <strong>Submitted</strong> → " +
            "<strong>Under Review</strong> → <strong>Approved</strong> → <strong>Issued</strong> → " +
            "<strong>Active</strong> → <strong>Closed</strong>, with <strong>Suspended</strong> reachable from " +
            "Active (and returning to Active), and exception exits <strong>Cancelled</strong> (from Draft/Submitted), " +
            "<strong>Rejected</strong> (from Under Review, which can also send it back to <strong>Draft</strong> for " +
            "rework rather than a final rejection), and <strong>Revoked</strong> (from Approved/Issued/Active). The " +
            "nine verified permit types are Cold Work, Hot Work, Confined Space, Electrical, Excavation, Working at " +
            "Height, Radiography, Line Breaking and Other.</p>" +
            "<p>While a permit is Under Review, a separate multi-stage approval chain (HSE Review → Maintenance " +
            "Approval → Operations Approval - see its own workflow diagram) actually runs on the workflow engine; " +
            "the Details page shows that chain's live progress and history.</p>" +
            "<p>This page also embeds two verified safety sub-processes rather than having their own separate pages: " +
            "<strong>Isolation Points</strong> (Lock-Out/Tag-Out) each move through <strong>Requested</strong> → " +
            "<strong>Isolated</strong> → <strong>Verified</strong> → <strong>Deisolated</strong> (plus " +
            "Requested → Cancelled) - verifying an isolation requires a WebAuthn (passkey) step-up confirmation, " +
            "not just being logged in, since it is the safety-critical confirmation that a point is genuinely isolated. " +
            "<strong>Gas Tests</strong> are recorded against the permit and can be invalidated. Starting actual work " +
            "under the permit, and its emergency-override path, are also WebAuthn-gated for the same reason.</p>",
            "permit,isolation,loto,gas test,webauthn,simops",
            "جزئیات مجوز کار - ایزوله‌سازی، تست گاز و زنجیره‌ی تایید",
            "این‌که وضعیت خودِ مجوز، گردش‌کار تایید داخلی آن، و داده‌ی ایمنیِ تعبیه‌شده‌ی ایزوله‌سازی/تست گاز چگونه کنار هم می‌آیند.",
            "<p>چرخه‌ی وضعیت تاییدشده‌ی خودِ مجوز: <strong>Draft</strong> → <strong>Submitted</strong> → " +
            "<strong>Under Review</strong> → <strong>Approved</strong> → <strong>Issued</strong> → " +
            "<strong>Active</strong> → <strong>Closed</strong>، با <strong>Suspended</strong> که از Active قابل‌دسترس " +
            "است (و به Active برمی‌گردد)، و خروجی‌های استثنایی <strong>Cancelled</strong> (از Draft/Submitted)، " +
            "<strong>Rejected</strong> (از Under Review، که می‌تواند آن را برای اصلاح به <strong>Draft</strong> هم " +
            "برگرداند، نه فقط رد نهایی)، و <strong>Revoked</strong> (از Approved/Issued/Active). نُه نوع مجوز تاییدشده: " +
            "Cold Work، Hot Work، Confined Space، Electrical، Excavation، Working at Height، Radiography، Line Breaking " +
            "و Other.</p>" +
            "<p>وقتی مجوز در Under Review است، یک زنجیره‌ی تایید چندمرحله‌ای جدا (HSE Review → Maintenance Approval " +
            "→ Operations Approval - نمودار گردش‌کار اختصاصی خودش را ببینید) واقعاً روی موتور گردش‌کار اجرا می‌شود؛ " +
            "صفحه‌ی جزئیات پیشرفت و تاریخچه‌ی زنده‌ی آن زنجیره را نشان می‌دهد.</p>" +
            "<p>این صفحه همچنین دو زیرفرآیند ایمنیِ تاییدشده را به‌جای صفحه‌ی جداگانه، تعبیه‌شده نشان می‌دهد: " +
            "<strong>نقاط ایزوله‌سازی (Isolation Points / LOTO)</strong> هرکدام از <strong>Requested</strong> → " +
            "<strong>Isolated</strong> → <strong>Verified</strong> → <strong>Deisolated</strong> عبور می‌کنند " +
            "(به‌علاوه‌ی Requested → Cancelled) - تایید یک ایزوله‌سازی نیازمند تاییدِ گام دومِ WebAuthn (پس‌کلید) " +
            "است، نه صرفاً واردشدن به سیستم، چون این همان تایید ایمنی‌بحرانیِ واقعاً ایزوله‌بودن یک نقطه است. " +
            "<strong>تست‌های گاز</strong> در برابر مجوز ثبت می‌شوند و قابل‌ابطال‌اند. شروع واقعیِ کار زیرِ مجوز، و مسیر " +
            "بازگشایی اضطراری آن، هم به همین دلیل با WebAuthn محافظت می‌شوند.</p>",
            "مجوز کار,ایزوله سازی,loto,تست گاز,webauthn,simops",
            new[] { "permits-overview" }),

        new Art("criticality-assessment-overview", "cmms", "Workflow", "CriticalityAssessment.Dashboard", 20,
            "Criticality Assessment",
            "The verified scoring rule - the worst dimension governs, not the average - and how approval updates the equipment's real criticality.",
            "<p>An assessment scores a piece of equipment on three dimensions - <strong>Safety</strong>, " +
            "<strong>Production</strong> and <strong>Environment</strong> - each from 1 to 5. Its verified rule: the " +
            "resulting class comes from the <strong>maximum</strong> of the three scores, not their average, so an " +
            "equipment with a severe safety consequence is never diluted by low production/environmental impact. Score " +
            "5 or 4 → Class A, score 3 → Class B, score 1 or 2 → Class C. This methodology (PEMS-CRIT-1.0) " +
            "is a project-defined scoring convention informed by common risk-assessment practice - it is not presented " +
            "as certified compliance with a specific external standard.</p>" +
            "<p>Its verified status flow is: <strong>Draft</strong> (only one Draft/Under-Review assessment is allowed " +
            "per equipment at a time; all three scores must be set before it can be submitted) → <strong>Under " +
            "Review</strong> → <strong>Approved</strong> or back to <strong>Draft</strong> (Reject). " +
            "<strong>Approving is what actually updates the equipment's real Criticality Class</strong> - it also " +
            "automatically supersedes whichever previous assessment was Approved for that equipment, keeping exactly " +
            "one current approved assessment per equipment.</p>",
            "criticality,criticality assessment,safety score,class a,class b,class c",
            "ارزیابی بحرانی‌بودن",
            "قاعده‌ی امتیازدهیِ تاییدشده - بدترین بُعد حاکم است، نه میانگین - و این‌که تایید چگونه بحرانی‌بودن واقعیِ تجهیز را به‌روزرسانی می‌کند.",
            "<p>یک ارزیابی یک تجهیز را در سه بُعد نمره‌گذاری می‌کند - <strong>ایمنی</strong>، <strong>تولید</strong> و " +
            "<strong>محیط‌زیست</strong> - هرکدام از ۱ تا ۵. قاعده‌ی تاییدشده‌ی آن: کلاس نهایی از <strong>بیشینه</strong>ی " +
            "سه امتیاز می‌آید، نه میانگین آن‌ها، پس تجهیزی با پیامد ایمنیِ شدید هرگز به‌خاطر پیامد پایین تولید/محیط‌زیست " +
            "کم‌رنگ نمی‌شود. امتیاز ۵ یا ۴ → کلاس A، امتیاز ۳ → کلاس B، امتیاز ۱ یا ۲ → کلاس C. این " +
            "روش‌شناسی (PEMS-CRIT-1.0) یک قرارداد امتیازدهیِ پروژه‌ای متاثر از رویه‌ی رایج ارزیابی ریسک است - به‌عنوان " +
            "انطباق تاییدشده با یک استاندارد بیرونی مشخص ارائه نمی‌شود.</p>" +
            "<p>چرخه‌ی وضعیت تاییدشده‌ی آن: <strong>Draft</strong> (فقط یک ارزیابی Draft/Under-Review در هر لحظه برای " +
            "هر تجهیز مجاز است؛ پیش از ارسال باید هر سه امتیاز ست شده باشند) → <strong>Under Review</strong> " +
            "→ <strong>Approved</strong> یا بازگشت به <strong>Draft</strong> (Reject). <strong>تایید همان چیزی " +
            "است که کلاس بحرانی‌بودن واقعیِ تجهیز را به‌روزرسانی می‌کند</strong> - همچنین به‌صورت خودکار هر ارزیابیِ " +
            "قبلیِ Approved همان تجهیز را جایگزین می‌کند، تا دقیقاً یک ارزیابیِ تاییدشده‌ی جاری برای هر تجهیز باقی بماند.</p>",
            "بحرانی بودن,ارزیابی بحرانی بودن,امتیاز ایمنی,کلاس a,کلاس b,کلاس c",
            new[] { "equipment-register-overview" }),

        new Art("rcm-fmea-overview", "cmms", "Workflow", "RcmFmea.Index", 21,
            "RCM / FMEA / FMECA",
            "How a class-level FMEA feeds an RCM analysis, and how an approved RCM decision becomes a real Maintenance Strategy.",
            "<p>An <strong>FMEA</strong> (Failure Mode and Effects Analysis) is built per <strong>equipment class</strong> " +
            "(not per individual equipment). Each analyzed failure mode gets a <strong>Risk Priority Number</strong>, " +
            "calculated (never stored, so it is never stale) as the classic <strong>Severity × Occurrence × " +
            "Detectability</strong> formula, each scored 1-10 by an engineer - a widely-used FMEA convention, not a " +
            "claim of formal ISO/SAE compliance. An FMEA item can optionally cite a Standard Reference record (from " +
            "Maintenance Master Data) that informed it - this is an admin-configured reference, not evidence that PEMS " +
            "itself implements that standard. An FMEA's own status flow matches the verified Draft → Under Review " +
            "→ Approved (or back to Draft) pattern, superseding the previous Approved analysis for that class.</p>" +
            "<p>An <strong>RCM</strong> (Reliability-Centered Maintenance) analysis can only be started from an " +
            "<strong>Approved</strong> FMEA, and follows the same Draft/Submit/Approve pattern at both the whole-analysis " +
            "level and for each individual maintenance-task <strong>Decision</strong> within it (which reuses the " +
            "existing Maintenance Strategy Type taxonomy, not a new one). <strong>Applying</strong> an approved RCM " +
            "decision is the verified bridge to real maintenance planning: it creates an actual Maintenance Strategy " +
            "for one specific piece of equipment in that class.</p>",
            "rcm,fmea,fmeca,risk priority number,reliability centered maintenance",
            "RCM / FMEA / FMECA",
            "چگونه یک FMEA در سطح کلاس یک تحلیل RCM را تغذیه می‌کند، و چگونه یک تصمیم تاییدشده‌ی RCM به یک استراتژی نگهداری واقعی تبدیل می‌شود.",
            "<p><strong>FMEA</strong> (تحلیل حالات خرابی و اثرات آن) به‌ازای هر <strong>کلاس تجهیز</strong> ساخته می‌شود " +
            "(نه به‌ازای هر تجهیز مجزا). هر حالت خرابیِ تحلیل‌شده یک <strong>عدد اولویت ریسک (RPN)</strong> می‌گیرد، " +
            "محاسبه‌شده (هرگز ذخیره نشده، پس هرگز کهنه نمی‌شود) به‌عنوان فرمول کلاسیک <strong>شدت × وقوع × " +
            "قابلیت‌تشخیص</strong>، هرکدام توسط یک مهندس از ۱ تا ۱۰ نمره‌گذاری می‌شود - یک قرارداد رایج FMEA، نه ادعای " +
            "انطباق رسمی با ISO/SAE. یک آیتم FMEA می‌تواند به‌صورت اختیاری به یک رکورد Standard Reference (از " +
            "Maintenance Master Data) که به آن کمک کرده اشاره کند - این یک مرجع پیکربندی‌شده توسط ادمین است، نه شاهدی " +
            "بر این‌که خودِ PEMS آن استاندارد را پیاده‌سازی می‌کند. چرخه‌ی وضعیت خودِ FMEA با الگوی تاییدشده‌ی Draft " +
            "→ Under Review → Approved (یا بازگشت به Draft) مطابقت دارد، و تحلیل Approved قبلیِ همان کلاس را " +
            "جایگزین می‌کند.</p>" +
            "<p>تحلیل <strong>RCM</strong> (نگهداری متمرکز بر قابلیت‌اطمینان) فقط از یک FMEA <strong>Approved</strong> " +
            "قابل‌شروع است، و همان الگوی Draft/Submit/Approve را هم در سطح کل تحلیل و هم برای هر <strong>تصمیم</strong> " +
            "منفردِ کار نگهداری درون آن دنبال می‌کند (که از تاکسونومی موجود Maintenance Strategy Type استفاده می‌کند، نه " +
            "تاکسونومی جدید). <strong>Apply کردن</strong> یک تصمیم تاییدشده‌ی RCM همان پل تاییدشده به برنامه‌ریزی نگهداری " +
            "واقعی است: یک استراتژی نگهداری واقعی برای یک تجهیز مشخص در آن کلاس می‌سازد.</p>",
            "rcm,fmea,fmeca,عدد اولویت ریسک,نگهداری متمرکز بر قابلیت اطمینان",
            new[] { "maintenance-strategy-overview" }),

        new Art("instrument-passport", "cmms", "Page", "Instrument.Passport", 22,
            "Instrument Passport",
            "The full instrumentation record: calibration history, alarms with cause-and-effect, control loop, and a direct path to raise a Work Request.",
            "<p>An instrument's Passport page brings together its full history in one place: append-only <strong>" +
            "Calibration History</strong> (each new calibration also refreshes the instrument's cached Last/Next " +
            "Calibration Due dates - never edited or deleted after the fact), latest maintenance history, its assigned " +
            "<strong>Control Loop</strong> (verified as at most one per instrument), I/O Points, and its " +
            "<strong>Alarms</strong> - each of which can have one or more <strong>Cause &amp; Effect</strong> entries " +
            "(cause → effect → action, including Trip/Shutdown actions and an optional trip signal).</p>" +
            "<p><strong>Verified cross-module link:</strong> a Cause &amp; Effect entry can directly raise a Work " +
            "Request against the instrument's linked equipment - the request's title, description and priority are " +
            "generated from that alarm/cause/effect data (Critical or High alarm priority carries through as High " +
            "priority on the resulting Work Request).</p>",
            "instrument,calibration,alarm,cause and effect,control loop",
            "پاسپورت ابزار دقیق",
            "رکورد کامل ابزار دقیق: تاریخچه‌ی کالیبراسیون، آلارم‌ها با علت و اثر، حلقه‌ی کنترل، و مسیر مستقیم ثبت درخواست کار.",
            "<p>صفحه‌ی پاسپورت یک ابزار دقیق، تاریخچه‌ی کامل آن را در یک‌جا کنار هم می‌آورد: <strong>تاریخچه‌ی " +
            "کالیبراسیون</strong> فقط-افزایشی (هر کالیبراسیون جدید تاریخ‌های کش‌شده‌ی آخرین/بعدی سررسید کالیبراسیون " +
            "ابزار را هم تازه می‌کند - هرگز پس از ثبت ویرایش یا حذف نمی‌شود)، آخرین تاریخچه‌ی نگهداری، " +
            "<strong>حلقه‌ی کنترل (Control Loop)</strong> تخصیص‌یافته‌اش (تاییدشده حداکثر یکی به‌ازای هر ابزار)، " +
            "نقاط I/O، و <strong>آلارم‌های</strong> آن - هرکدام می‌توانند یک یا چند رکورد <strong>علت و اثر</strong> " +
            "داشته باشند (علت → اثر → اقدام، شامل اقدامات Trip/Shutdown و یک سیگنال Trip اختیاری).</p>" +
            "<p><strong>پیوند بین‌ماژولیِ تاییدشده:</strong> یک رکورد علت و اثر می‌تواند مستقیماً یک درخواست کار در " +
            "برابر تجهیز متصل به ابزار ثبت کند - عنوان، توضیح و اولویت درخواست از همان داده‌ی آلارم/علت/اثر ساخته " +
            "می‌شود (اولویت آلارم Critical یا High به‌عنوان اولویت High روی درخواست کار نتیجه منتقل می‌شود).</p>",
            "ابزار دقیق,کالیبراسیون,آلارم,علت و اثر,حلقه کنترل",
            null),

        new Art("spare-parts-overview", "cmms", "Page", "SparePart.Index", 23,
            "Spare Parts",
            "A standalone parts catalog with its own stock levels - verified as not yet linked to Equipment or to Work Order spare consumption.",
            "<p>The Spare Parts register tracks a part catalog with real stock fields - <strong>Quantity On " +
            "Hand</strong>, <strong>Minimum Stock Level</strong>, and a <strong>Reorder Point</strong> (a part is " +
            "flagged Low Stock once its Quantity On Hand falls to or below its Reorder Point).</p>" +
            "<p><strong>Verified gap worth knowing:</strong> a Spare Part record currently has no link to a specific " +
            "Equipment record, and when a Work Order records spare-part consumption (\"Add Spare Usage\"), it draws " +
            "from <strong>Material Items</strong> (the Inventory &amp; Materials module) rather than from this Spare " +
            "Parts catalog. Treat this page as an independent parts-catalog/stock reference today, not as the source " +
            "actually consumed by work order execution.</p>",
            "spare part,stock,reorder point,quantity on hand",
            "قطعات یدکی",
            "یک کاتالوگ مستقل قطعات با سطح موجودی خودش - تاییدشده که هنوز به تجهیز یا مصرف قطعه‌ی دستور کار وصل نیست.",
            "<p>رجیستر قطعات یدکی یک کاتالوگ قطعه را با فیلدهای موجودیِ واقعی ردیابی می‌کند - <strong>موجودی فعلی " +
            "(Quantity On Hand)</strong>، <strong>حداقل سطح موجودی</strong>، و یک <strong>نقطه‌ی سفارش مجدد</strong> " +
            "(قطعه‌ای کمبود موجودی علامت می‌خورد وقتی موجودی فعلی‌اش به نقطه‌ی سفارش مجدد برسد یا کمتر شود).</p>" +
            "<p><strong>نکته‌ی تاییدشده‌ی قابل‌توجه:</strong> یک رکورد قطعه‌ی یدکی در حال حاضر هیچ پیوندی به یک رکورد " +
            "تجهیز مشخص ندارد، و وقتی یک دستور کار مصرف قطعه ثبت می‌کند («Add Spare Usage»)، از <strong>اقلام مواد " +
            "(Material Items)</strong> (ماژول موجودی و مواد) استفاده می‌کند، نه از این کاتالوگ قطعات یدکی. این صفحه را " +
            "امروز به‌عنوان یک مرجع مستقل کاتالوگ/موجودی قطعات در نظر بگیرید، نه منبع واقعیِ مصرف‌شده توسط اجرای دستور کار.</p>",
            "قطعه یدکی,موجودی,نقطه سفارش مجدد,موجودی فعلی",
            null),

        new Art("job-plans-overview", "cmms", "Page", "JobPlan.Index", 24,
            "Job Plans",
            "Reusable work templates, and how they attach to a Maintenance Plan rather than directly to a Work Order.",
            "<p>A Job Plan is a reusable template - scope, estimated duration, safety requirements, a list of tasks, and " +
            "required skills/materials - with no status or approval workflow of its own; it exists to standardize how a " +
            "recurring job should be carried out.</p>" +
            "<p><strong>Verified linkage:</strong> a Job Plan attaches to a Maintenance Plan (optionally), not directly " +
            "to a Work Order. When a Maintenance Plan that references a Job Plan generates a Work Order, that is how the " +
            "template's content reaches actual execution.</p>",
            "job plan,work template,maintenance plan",
            "برنامه‌های کار (Job Plan)",
            "قالب‌های کار قابل‌استفاده‌ی مجدد، و این‌که چگونه به یک برنامه‌ی نگهداری وصل می‌شوند، نه مستقیم به دستور کار.",
            "<p>Job Plan یک قالب قابل‌استفاده‌ی مجدد است - محدوده، مدت‌زمان تخمینی، الزامات ایمنی، فهرست وظایف، و " +
            "مهارت‌ها/مواد لازم - بدون هیچ وضعیت یا گردش‌کار تاییدی برای خودش؛ برای استانداردسازیِ این‌که یک کار " +
            "تکرارشونده چگونه باید انجام شود وجود دارد.</p>" +
            "<p><strong>پیوند تاییدشده:</strong> یک Job Plan (به‌صورت اختیاری) به یک برنامه‌ی نگهداری وصل می‌شود، نه " +
            "مستقیم به یک دستور کار. وقتی برنامه‌ی نگهداری‌ای که به یک Job Plan ارجاع می‌دهد یک دستور کار می‌سازد، " +
            "این‌طوری محتوای قالب به اجرای واقعی می‌رسد.</p>",
            "برنامه کار,قالب کار,برنامه نگهداری",
            new[] { "maintenance-plans-overview" }),

        new Art("store-requisitions-overview", "cmms", "Workflow", "StoreRequisition.Index", 25,
            "Store Requisitions",
            "The verified Draft-to-Closed flow, and why \"Reserved\" and \"Issued\" are two distinct, separately-tracked steps.",
            "<p>A Store Requisition's verified status flow is strictly linear: <strong>Draft</strong> → " +
            "<strong>Submitted</strong> → <strong>Approved</strong> → <strong>Reserved</strong> → " +
            "<strong>Issued</strong> → <strong>Closed</strong>, with Reject/Cancel as exception exits.</p>" +
            "<p><strong>Verified and deliberate:</strong> Reserved and Issued are not the same thing. Reaching " +
            "<strong>Reserved</strong> only raises each requested line's Reserved Quantity against warehouse stock (and " +
            "fails with a clear error if not enough is actually available to reserve) - it does not yet reduce real " +
            "on-hand stock. Only reaching <strong>Issued</strong> actually reduces on-hand quantity. Both steps create " +
            "an Inventory Transaction record, so stock never changes without a traceable transaction; a Reject or " +
            "Cancel at any point releases any reservation already made. Every status change is recorded in a full " +
            "from/to/who/when/reason history.</p>",
            "store requisition,reserve,issue,inventory transaction",
            "درخواست انبار",
            "چرخه‌ی تاییدشده‌ی Draft تا Closed، و این‌که چرا «رزرو» و «صدور» دو گام جدا و مجزا هستند.",
            "<p>چرخه‌ی وضعیت تاییدشده‌ی یک درخواست انبار کاملاً خطی است: <strong>Draft</strong> → " +
            "<strong>Submitted</strong> → <strong>Approved</strong> → <strong>Reserved</strong> → " +
            "<strong>Issued</strong> → <strong>Closed</strong>، با Reject/Cancel به‌عنوان خروجی‌های استثنایی.</p>" +
            "<p><strong>تاییدشده و عمدی:</strong> رزرو (Reserved) و صدور (Issued) یک چیز نیستند. رسیدن به " +
            "<strong>Reserved</strong> فقط مقدار رزروشده‌ی هر ردیف درخواستی را در برابر موجودی انبار بالا می‌برد (و اگر " +
            "واقعاً موجودی کافی برای رزرو در دسترس نباشد، با خطای روشن شکست می‌خورد) - هنوز موجودی واقعیِ در دست را کم " +
            "نمی‌کند. فقط رسیدن به <strong>Issued</strong> واقعاً مقدار در دست را کم می‌کند. هر دو گام یک رکورد " +
            "تراکنش موجودی می‌سازند، پس موجودی هرگز بدون یک تراکنشِ قابل‌ردیابی تغییر نمی‌کند؛ یک Reject یا Cancel در " +
            "هر نقطه هر رزروِ قبلاً انجام‌شده را آزاد می‌کند. هر تغییر وضعیت در یک تاریخچه‌ی کاملِ از/به/چه‌کسی/چه‌زمانی/" +
            "دلیل ثبت می‌شود.</p>",
            "درخواست انبار,رزرو,صدور,تراکنش موجودی",
            new[] { "material-items-overview" }),

        new Art("import-batches-overview", "cmms", "Concept", "Imports.StagedBatchOverview", 26,
            "How Staged Imports Work",
            "The universal staged-batch pattern shared by every Excel/data importer in PEMS: upload, review, then commit or discard.",
            "<p>This applies to every staged importer in PEMS (Equipment, Functional Location, MDR, VDR, Maintenance " +
            "History, and - with one verified difference noted below - MRS/Material). Uploading a file never writes " +
            "directly into real records; it creates a <strong>Batch</strong> in a <strong>Staging</strong> status with " +
            "one row per parsed spreadsheet row. Each row gets a validation status, and rows the importer auto-detects " +
            "as duplicates within the same file default to <strong>excluded</strong> from commit - the human reviewer " +
            "must explicitly include them after checking, they are never auto-committed.</p>" +
            "<p>On the Review screen, each row can be individually included or excluded (\"Toggle Row\"). " +
            "<strong>Commit</strong> processes only the included rows: a row matches an existing record (verified, for " +
            "Equipment, by Tag Number + Description together) is <strong>updated</strong>; a row with no match is " +
            "<strong>created</strong> new; a row missing required data is <strong>skipped</strong> - the commit result " +
            "reports exact created/updated/skipped counts. <strong>Discard</strong> abandons the whole batch instead. " +
            "Raw reference fields captured from the file (such as an MR or PO number on an equipment import) are stored " +
            "as plain text only - no other record is automatically created or linked from them; real links are made " +
            "later by a human, through the Relationship Explorer.</p>" +
            "<p><strong>Verified difference:</strong> the MRS/Material importer uses a slightly different reviewer " +
            "flow - rows carry a match-status filter and are committed or rejected in bulk by explicit selection, " +
            "rather than the per-row include/exclude-then-single-commit pattern the other importers use. The review " +
            "grid is paginated (like every large PEMS list) so uploading a large file never requires splitting it " +
            "manually - use the Match Status filter (New / Matched / Duplicate / Invalid) and its \"Commit ALL ... in " +
            "entire batch\" / \"Reject ALL ... in entire batch\" buttons to act on every row of that category across " +
            "the whole file in one action, or select rows individually on the current page for finer-grained review.</p>" +
            "<p><strong>VDR import - Vendor master-data conflict:</strong> a VDR row's Vendor is resolved by name only " +
            "(trimmed, case-insensitive), the same identity rule used everywhere else Vendors are auto-matched in PEMS. " +
            "If more than one existing Vendor record already shares that name, the importer does not guess which one to " +
            "use - that row is skipped and its Validation column shows a Vendor master-data conflict message naming the " +
            "duplicate Vendor IDs. Resolve the duplicate Vendor records first, then re-import.</p>",
            "import,staging,batch,commit,discard,mdr import,vdr import,equipment import",
            "واردات مرحله‌ای چگونه کار می‌کند",
            "الگوی جهانیِ Batch مرحله‌ای که هر Import‌کننده‌ی اکسل/داده در PEMS به اشتراک می‌گذارد: آپلود، بازبینی، سپس Commit یا Discard.",
            "<p>این الگو روی هر Import‌کننده‌ی مرحله‌ای در PEMS اعمال می‌شود (تجهیز، موقعیت فنی، MDR، VDR، تاریخچه‌ی " +
            "نگهداری، و - با یک تفاوت تاییدشده که پایین ذکر شده - MRS/مواد). آپلود یک فایل هرگز مستقیم در رکوردهای " +
            "واقعی نمی‌نویسد؛ یک <strong>Batch</strong> در وضعیت <strong>Staging</strong> با یک ردیف به‌ازای هر ردیف " +
            "تجزیه‌شده‌ی صفحه‌گسترده می‌سازد. هر ردیف یک وضعیت اعتبارسنجی می‌گیرد، و ردیف‌هایی که Import‌کننده به‌صورت " +
            "خودکار به‌عنوان تکراری درون همان فایل تشخیص می‌دهد، پیش‌فرض از Commit <strong>مستثنی</strong> می‌شوند - " +
            "بازبین انسانی باید پس از بررسی، صریحاً آن‌ها را شامل کند، هرگز خودکار Commit نمی‌شوند.</p>" +
            "<p>در صفحه‌ی بازبینی، هر ردیف می‌تواند جداگانه شامل یا مستثنی شود («Toggle Row»). <strong>Commit</strong> " +
            "فقط ردیف‌های شامل‌شده را پردازش می‌کند: ردیفی که با یک رکورد موجود مطابقت دارد (برای تجهیز، تاییدشده با " +
            "ترکیب شماره‌ی تگ + توضیح) <strong>به‌روزرسانی</strong> می‌شود؛ ردیفی بدون تطابق تازه <strong>ساخته</strong> " +
            "می‌شود؛ ردیفی با داده‌ی الزامیِ ناقص <strong>رد</strong> می‌شود - نتیجه‌ی Commit شمارش دقیق " +
            "ساخته/به‌روزرسانی/ردشده را گزارش می‌دهد. <strong>Discard</strong> کل Batch را به‌جای آن کنار می‌گذارد. " +
            "فیلدهای مرجع خام گرفته‌شده از فایل (مثل شماره‌ی MR یا PO روی یک ایمپورت تجهیز) فقط به‌صورت متن ساده ذخیره " +
            "می‌شوند - هیچ رکورد دیگری به‌صورت خودکار از آن‌ها ساخته یا وصل نمی‌شود؛ پیوندهای واقعی بعداً توسط یک انسان، " +
            "از طریق کاوشگر روابط، ساخته می‌شوند.</p>" +
            "<p><strong>تفاوت تاییدشده:</strong> Import‌کننده‌ی MRS/مواد یک روند بازبینیِ کمی متفاوت استفاده می‌کند - " +
            "ردیف‌ها یک فیلتر وضعیت تطابق دارند و به‌صورت گروهی با انتخاب صریح Commit یا رد می‌شوند، نه الگوی " +
            "شامل/مستثنی-به‌ازای-هر-ردیف-سپس-یک-Commit که بقیه‌ی Import‌کننده‌ها استفاده می‌کنند. جدول بازبینی " +
            "صفحه‌بندی‌شده است (مثل هر فهرست بزرگ دیگر در PEMS)، پس آپلود یک فایل بزرگ هرگز نیازمند تقسیم دستی نیست - " +
            "از فیلتر Match Status (New / Matched / Duplicate / Invalid) و دکمه‌های «Commit ALL ... in entire batch» / " +
            "«Reject ALL ... in entire batch» آن برای اقدام روی همه‌ی ردیف‌های آن دسته در کل فایل با یک اقدام استفاده " +
            "کنید، یا برای بازبینی دقیق‌تر، ردیف‌ها را تک‌تک در همان صفحه انتخاب کنید.</p>" +
            "<p><strong>Import VDR - تعارض داده‌ی مرجع Vendor:</strong> Vendor هر ردیف VDR فقط از روی نام تطبیق داده " +
            "می‌شود (بعد از Trim، بی‌تفاوت به بزرگی/کوچکی حروف) - همان قاعده‌ی هویتی که در همه‌جای دیگر PEMS برای " +
            "تطبیق خودکار Vendor استفاده می‌شود. اگر بیش از یک رکورد Vendor موجود با همان نام باشد، Import‌کننده حدس " +
            "نمی‌زند کدام را استفاده کند - آن ردیف Skip می‌شود و ستون Validation پیام تعارض داده‌ی مرجع Vendor را با " +
            "شناسه‌ی رکوردهای تکراری نشان می‌دهد. ابتدا رکوردهای تکراری Vendor را برطرف کنید، سپس دوباره Import بزنید.</p>",
            "ایمپورت,مرحله بندی,batch,commit,discard,ایمپورت mdr,ایمپورت vdr,ایمپورت تجهیز",
            null),

        // ============================= PHASE 4 INCREMENT 3B: RELIABILITY =============================
        new Art("reliability-overview", "cmms", "Workflow", "Reliability.Overview", 27,
            "Reliability Analysis (MTBF / MTTR / Availability / Bad Actors)",
            "The exact formulas PEMS actually calculates, and the one data-entry habit they silently depend on.",
            "<p>This page (and the richer Enterprise Dashboard, which shares this same Help content) shows fleet- and " +
            "class-level reliability metrics computed <strong>entirely from real Failure Records</strong> - nothing here " +
            "is manually entered or estimated.</p>" +
            "<p><strong>Verified formulas (exact, from the calculation code):</strong></p>" +
            "<ul>" +
            "<li><strong>MTBF</strong> (Mean Time Between Failures) requires at least 2 failure records for a piece of " +
            "equipment: it is the time span from the first to the last recorded failure, divided by (failure count − 1). " +
            "An equipment with 0 or 1 failure record has no MTBF value.</li>" +
            "<li><strong>MTTR</strong> (Mean Time To Repair) is the average of the actual Work Order repair durations " +
            "(Start Date to Completion Date) for the Work Orders linked to those failures - only failures with a linked, " +
            "completed Work Order contribute.</li>" +
            "<li><strong>Availability</strong> = MTBF / (MTBF + MTTR) × 100 - the standard Inherent Availability " +
            "formula, calculated only when both MTBF and MTTR are available. <strong>Verified limitation:</strong> this " +
            "result is not mathematically bounded to 0-100% - if a linked Work Order has an invalid duration (Completion " +
            "Date recorded before Start Date), MTTR can go negative and push Availability above 100%; PEMS does not " +
            "currently guard against this.</li>" +
            "<li><strong>Bad Actors</strong> (top-N worst equipment) are ranked by failure count, then downtime hours, " +
            "within the selected date range; each one's \"repeat failure\" count uses a verified 90-day window rule - the " +
            "same failure mode recurring within 90 days counts as a repeat.</li>" +
            "</ul>" +
            "<p><strong>Known limitation (verified):</strong> the Failure Mode and Failure Cause Pareto charts on this " +
            "page, and the Failure Taxonomy Coverage indicator on the Data Quality page, rely on a structured " +
            "failure-mode/cause classification field that is not currently populated by any PEMS workflow. These charts " +
            "and this indicator may show little or no data even when the free-text failure mode/cause used by Bad Actors " +
            "above is fully populated - this is a data-wiring gap, not a sign of a healthy or unhealthy fleet.</p>" +
            "<p><strong>Critical data-integrity fact (verified):</strong> a Failure Record is <em>never</em> created " +
            "through its own form. It is automatically synced only when a Work Order is completed with its ISO 14224 " +
            "failure-coding fields (Failure Mode Code, Failure Cause Code, Root Cause, Downtime Hours) filled in - by " +
            "design, so Failure Record is never a second, independently-editable source of truth. This means: if " +
            "technicians skip those fields when closing a Corrective/Emergency Work Order, that failure is invisible to " +
            "every metric on this page - MTBF, MTTR, Availability and Bad Actors will silently understate the real " +
            "picture for that equipment, with no error or warning shown anywhere.</p>",
            "reliability,mtbf,mttr,availability,bad actor,failure record",
            "تحلیل قابلیت‌اطمینان (MTBF / MTTR / Availability / Bad Actors)",
            "فرمول‌هایی که PEMS واقعاً محاسبه می‌کند، و همان یک عادتِ ثبت داده که همه‌ی این‌ها بی‌صدا به آن وابسته‌اند.",
            "<p>این صفحه (و داشبورد سازمانیِ غنی‌تر، که همین محتوای راهنما را به اشتراک می‌گذارد) معیارهای قابلیت‌اطمینان " +
            "در سطح ناوگان و کلاس را نشان می‌دهد که <strong>کاملاً از روی رکوردهای واقعیِ خرابی</strong> محاسبه شده‌اند - " +
            "هیچ‌کدام دستی وارد یا تخمین زده نشده‌اند.</p>" +
            "<p><strong>فرمول‌های تاییدشده (دقیق، از خودِ کد محاسبه):</strong></p>" +
            "<ul>" +
            "<li><strong>MTBF</strong> (میانگین زمان بین خرابی‌ها) نیازمند حداقل ۲ رکورد خرابی برای یک تجهیز است: " +
            "بازه‌ی زمانی از اولین تا آخرین خرابیِ ثبت‌شده، تقسیم‌بر (تعداد خرابی − ۱). تجهیزی با ۰ یا ۱ رکورد خرابی " +
            "مقدار MTBF ندارد.</li>" +
            "<li><strong>MTTR</strong> (میانگین زمان تعمیر) میانگینِ مدت واقعیِ تعمیر دستورهای کار (تاریخ شروع تا تاریخ " +
            "تکمیل) مرتبط با آن خرابی‌هاست - فقط خرابی‌هایی که یک دستور کارِ تکمیل‌شده‌ی مرتبط دارند سهیم‌اند.</li>" +
            "<li><strong>Availability</strong> = MTBF / (MTBF + MTTR) × ۱۰۰ - فرمول استاندارد Inherent Availability، " +
            "فقط وقتی هر دو MTBF و MTTR در دسترس باشند محاسبه می‌شود. <strong>محدودیت تاییدشده:</strong> این نتیجه از نظر " +
            "ریاضی به بازه‌ی ۰-۱۰۰٪ محدود نیست - اگر دستور کارِ مرتبط مدت نامعتبری داشته باشد (تاریخ تکمیل قبل از تاریخ " +
            "شروع ثبت شده باشد)، MTTR می‌تواند منفی شود و Availability را بالای ۱۰۰٪ ببرد؛ PEMS در حال حاضر در برابر این " +
            "حالت محافظتی ندارد.</li>" +
            "<li><strong>Bad Actors</strong> (بدترین N تجهیز) بر اساس تعداد خرابی، سپس ساعات توقف، در بازه‌ی زمانیِ " +
            "انتخاب‌شده رتبه‌بندی می‌شوند؛ تعداد «خرابیِ تکراری» هر کدام از یک قاعده‌ی تاییدشده‌ی پنجره‌ی ۹۰روزه استفاده " +
            "می‌کند - تکرار همان حالت خرابی درون ۹۰ روز، تکراری شمرده می‌شود.</li>" +
            "</ul>" +
            "<p><strong>محدودیت تاییدشده:</strong> نمودارهای Pareto حالت خرابی و علت خرابی در این صفحه، و شاخص پوشش " +
            "طبقه‌بندیِ خرابی (Failure Taxonomy Coverage) در صفحه‌ی کیفیت داده، به یک فیلد طبقه‌بندیِ ساختاریافته‌ی " +
            "حالت/علت خرابی وابسته‌اند که در حال حاضر توسط هیچ روندی در PEMS پر نمی‌شود. این نمودارها و این شاخص ممکن " +
            "است داده‌ی کم یا هیچ نشان دهند حتی وقتی فیلد متنیِ حالت/علت خرابی (که Bad Actors بالا از آن استفاده می‌کند) " +
            "کاملاً پر شده باشد - این یک شکاف در اتصال داده است، نه نشانه‌ی سلامت یا ناسلامتیِ ناوگان.</p>" +
            "<p><strong>واقعیت حیاتیِ یکپارچگیِ داده (تاییدشده):</strong> یک رکورد خرابی <em>هرگز</em> از طریق فرم " +
            "مخصوص خودش ساخته نمی‌شود. فقط وقتی یک دستور کار با فیلدهای کدگذاریِ خرابیِ ISO 14224 خودش (کد حالت خرابی، " +
            "کد علت خرابی، علت ریشه‌ای، ساعات توقف) پرشده تکمیل شود، به‌صورت خودکار همگام می‌شود - عمداً، تا رکورد خرابی " +
            "هرگز منبع حقیقتِ دومِ مستقلاً قابل‌ویرایش نباشد. یعنی: اگر تکنسین‌ها هنگام بستن یک دستور کار اصلاحی/اضطراری " +
            "آن فیلدها را رد کنند، آن خرابی برای همه‌ی معیارهای این صفحه نامرئی می‌ماند - MTBF، MTTR، Availability و Bad " +
            "Actors بی‌صدا تصویر واقعی آن تجهیز را کم‌تر از واقع نشان می‌دهند، بدون هیچ خطا یا هشداری.</p>",
            "قابلیت اطمینان,mtbf,mttr,availability,bad actor,رکورد خرابی",
            new[] { "rcm-fmea-overview", "criticality-assessment-overview", "maintenance-strategy-overview" }),

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
