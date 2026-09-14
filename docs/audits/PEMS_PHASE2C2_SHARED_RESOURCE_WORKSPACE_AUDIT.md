# PEMS Phase 2C-2 — Shared Resource Workspace Audit

**تاریخ:** 2026-09-13
**نوع فاز:** Audit-first، بدون تغییر کد. هیچ Component/TagHelper/Entity/Migration ساخته نشد.
**HEAD در زمان این ممیزی:** `aa5a768` (`feat(deploy): UAT hosting audit...`) - همان working
tree که فازهای قبلی این نشست روی آن انباشته شده‌اند، هنوز بدون Commit.

---

## خلاصه‌ی مدیریتی (Executive Summary)

PEMS **از قبل** دو الگوی رقیب برای صفحه‌ی جزئیات یک منبع دارد، نه یک الگوی واحد: (۱) الگوی
Tab-محور EDMS-Family که شامل `EngineeringDocuments`، `VDR`، و - **برخلاف ادعای گزارش قبلی
همین نشست (`PEMS_PHASE2B_INFORMATION_ARCHITECTURE_AUDIT.md`)** - **`Equipment` هم هست**
(۹ Tab واقعی، تأیید مستقیم؛ آن گزارش قبلی این را نادیده گرفته بود چون فقط شمارش
`content-card` را بررسی کرده بود، نه وجود `nav-tabs`)؛ و (۲) الگوی صفحه‌ی بلند تک‌کارتی که
`WorkOrder` و `Permit` استفاده می‌کنند. `Permit` علاوه‌براین یک الگوی سوم و genuinely باارزش
دارد که در هیچ‌جای دیگر پروژه تکرار نشده: یک خلاصه‌ی «موارد نیازمند اقدام شما» با لینک لنگر
به بخش مربوطه‌ی همان صفحه‌ی بلند - بدون Tab، بدون Query جدید.

سه ماژول (`FunctionalLocation`, `MaintenanceStrategy`, `MaintenancePlan`) و یکی با شکل خاص
(`RcmFmea`) اصلاً صفحه‌ی Details ندارند - این‌ها Grid/Master-Data-محورند، نه Resource-محور،
برخلاف فرض ضمنی دستور این فاز که آن‌ها را به‌عنوان نمونه‌ی «Resource» فهرست کرده بود. این
یافته مستقیماً یکی از قواعد صریح این دستور را اجرا می‌کند: «دسته‌بندی را به‌زور جایی که
معماری موجود پشتیبانی نمی‌کند تحمیل نکن.»

یک الگوی واقعاً امن برای استخراج پیدا شد: سه پیاده‌سازی مستقل و کاملاً موازی از «نمودار
مراحل گردش‌کار» (`_WorkflowStagesDiagram`, `_VdrWorkflowStagesDiagram`,
`_WorkOrderStagesDiagram`) که دوتای اول حتی CSS تقریباً یکسان (فقط نام Keyframe متفاوت) دارند
- این نامزد اول استخراج امن است، نه یک Command Bar عمومی (که طبق فاز ۲A هنوز به‌درستی رد
می‌ماند، و این ممیزی با شواهد بیشتر آن رد را تأیید می‌کند - بخش ۷).

**نتیجه‌ی نهایی این ممیزی (بخش ۲۱): آماده‌ی پیاده‌سازی محدود، نه یک بازطراحی کامل.** فقط
یک‌سری واحد کوچک و کاملاً مشخص (Header Contract، Status Token Mapper، Stage-Diagram Partial
عمومی) برای پیاده‌سازی امن آماده‌اند؛ یک Command Bar عمومی یا یک «Resource Workspace»
تک‌کامپوننتی که همه‌چیز را کنترل کند **صریحاً رد می‌شود** (بخش ۲۰).

---

## ۱. ممیزی الزامی مخزن (خلاصه‌ی روش)

طبق دستور صریح این فاز، هر یافته‌ی زیر مستقیماً از خواندن فایل واقعی می‌آید، نه از حافظه‌ی
گزارش‌های قبلی این نشست. فایل‌های زیر به‌طور کامل یا هدفمند خوانده شدند:
`EngineeringDocuments/Details.cshtml` (۱۸۰ خط اول)، `VDR/Details.cshtml` (۱۶۰ خط اول + بخش
Workflow)، `Equipment/Details.cshtml` (۱۰۰ خط اول + فهرست کامل Tab)، `WorkOrder/Details.cshtml`
(۱۰۰ خط اول)، `Permit/Details.cshtml` (۹۰ خط اول)، `Vendor/Details.cshtml`,
`Transmittal/Details.cshtml`, `MaterialRequisition/Details.cshtml`, `WorkRequest/Details.cshtml`,
`Asset/Details.cshtml` (شمارش ساختاری)، `_WorkflowStagesDiagram.cshtml`,
`_VdrWorkflowStagesDiagram.cshtml`, `_WorkOrderStagesDiagram.cshtml` (کامل)،
`DocumentStatusBadge.cs` (کامل). به‌علاوه: `find`/`grep` برای وجود `ViewComponents`/`TagHelpers`،
شمارش `nav-tabs`/`content-card`/`asp-validation-summary`/`onclick="return confirm("` در کل
`Views/`، و شمارش خواص CSS جهت‌دار در `site.css`.

## ۲. طبقه‌بندی منابع (بر اساس شواهد واقعی مخزن)

### الف) صفحات Resource-محور واقعی (دارای اکشن `Details` + View اختصاصی)

| منبع | `Details` وجود دارد؟ | الگوی IA |
|---|---|---|
| Engineering Document | ✔ | Tab (۶ تب) + Command Bar بالای صفحه |
| VDR | ✔ | Tab (۵ تب) + Command Bar **داخل تب Workflow** (نه بالای صفحه) |
| Equipment | ✔ | **Tab (۹ تب) - یافته‌ی تازه، برخلاف گزارش قبلی این نشست** |
| Work Order | ✔ | صفحه‌ی بلند تک‌ستونی + نمودار مراحل + یک اکشن اصلی («Advance to X») |
| Permit | ✔ | صفحه‌ی بلند تک‌ستونی + **خلاصه‌ی «نیاز به اقدام شما» با لینک لنگر** (الگوی سوم، منحصربه‌فرد) |
| Vendor | ✔ | صفحه‌ی بسیار سبک (۳۷ خط، ۱ کارت) - عملاً یک خلاصه‌ی فقط‌خواندنی، نه Workspace |
| Transmittal | ✔ | صفحه‌ی متوسط (۱۹۵ خط، ۳ کارت)، بدون Tab |
| Material Requisition | ✔ | صفحه‌ی متوسط (۱۳۸ خط، ۵ کارت)، بدون Tab |
| Work Request | ✔ | صفحه‌ی کوتاه (۱۲۹ خط، ۳ کارت)، بدون Tab |
| Asset | ✔ | صفحه‌ی متوسط (۲۰۲ خط، ۱۰ کارت)، بدون Tab |
| Document Revision | ✔ (طبق ممیزی فاز ۱) | بدون بررسی مجدد در این فاز - خارج از نمونه‌ی حداقلی |

### ب) ماژول‌هایی که دستور این فاز آن‌ها را «Resource» فرض کرده بود ولی شواهد رد می‌کند

| ماژول | شواهد | نتیجه‌ی طبقه‌بندی |
|---|---|---|
| **Functional Location** | `FunctionalLocationController` هیچ اکشن `Details` ندارد؛ فقط `Views/FunctionalLocation/Index.cshtml` وجود دارد | **بخش D (Master Data/Config)، نه Resource** - مدیریت کاملاً از Grid/Index |
| **Maintenance Strategy** | همان - فقط `Index`/`BulkCreate` | **بخش D** |
| **Maintenance Plan** | همان - فقط `Index`/`Create` | **بخش D** |
| **RCM/FMEA** | `Views/RcmFmea/ClassDetail.cshtml` وجود دارد، نه `Details.cshtml` - یعنی واحد تحلیل «هر EquipmentClass» است، نه یک رکورد منفرد | **شکل خودش را دارد - نه Resource استاندارد، نه Master Data خالص؛ خارج از الگوی این ممیزی** |

این یافته دقیقاً همان قاعده‌ی صریح دستور این فاز را اجرا می‌کند: «دسته‌بندی را جایی که معماری
موجود پشتیبانی نمی‌کند تحمیل نکن.» هر طراحی Workspace آینده که این چهار ماژول را هدف بگیرد،
اول باید تصمیم بگیرد آیا اصلاً باید صاحب یک صفحه‌ی Details شوند - این خودش یک تصمیم محصولی
جدا از دامنه‌ی این ممیزی است.

### ج) صفحات Task/Action-محور (طبق شواهد کنترلر، نه فرض)

اکشن‌های `Approve`/`Reject`/`Issue`/`Submit`/`Prepare`/`Acknowledge`/`Complete`/`Respond` در
تمام موارد بررسی‌شده (`EngineeringDocumentsController`, `VDRController`, `PermitController`,
`ShiftHandoverController`) **هرگز صفحه‌ی مستقل خودشان را ندارند** - همیشه `[HttpPost]` روی
همان کنترلر Resource هستند که با `Redirect` به `Details` همان رکورد برمی‌گردند. این یک الگوی
کاملاً یکدست و بدون استثنا در کل نمونه‌ی بررسی‌شده است: **هیچ صفحه‌ی Task/Action مستقلی در
PEMS وجود ندارد** - این دسته صرفاً همان دکمه‌های Command Bar/Section هستند، نه یک نوع صفحه‌ی
جدا. (استثنای ظاهری: `GasTestController`/`IsolationController`/`SimopsController`/
`ExecutionGateController` - اما طبق ممیزی فاز ۲B، این‌ها هم اصلاً View مستقل ندارند؛ فقط
اکشن‌های POST هستند که از داخل `Permit/Details.cshtml` صدا زده می‌شوند.)

### د) صفحات Dashboard/گزارش

`CmmsOperations/Dashboard`, `Reliability/Dashboard`, `CriticalityAssessment/Dashboard`,
`Kpi/Index`, `EngineeringDashboard/Index` - این‌ها هیچ‌کدام «یک رکورد» را نشان نمی‌دهند
(تجمیعی‌اند)؛ **خارج از دامنه‌ی مفهومی Resource Workspace**، طبق تعریف خودِ این دستور.

### ه) صفحات پیکربندی/داده‌ی پایه

`Settings`, `Department`, `Discipline`, `RoutingRules` - Grid ساده + Modal Create/Edit، بدون
هیچ مفهوم Workflow/Status/Tab. **خارج از دامنه.**

### و) صفحات ابزاری/سیستمی

`FilesController` (بدون View)، `CultureController`/`ThemeController` (Endpoint خام)،
`ComingSoonController`. **خارج از دامنه.**

---

## ۳. بهترین پیاده‌سازی‌های موجود (با جزئیات ساختاری واقعی)

### Engineering Document (`Views/EngineeringDocuments/Details.cshtml`)

| بُعد | یافته |
|---|---|
| Header | یک `content-card` با کلاس اختصاصی `pems-doc-header` - شماره‌ی مدرک (`h4`)، Badge وضعیت (`DocumentStatusBadge.CssClass`)، Badge بازبینی، Badge‌های Frozen/Hold شرطی، عنوان با `title="..."` کوتاه‌شده (Clamp)، خط متادیتا (Project·Discipline·Area) |
| شناسه | `Model.DocumentNo` - در Header، نه یک ستون جدا |
| وضعیت | متن + رنگ (هرگز فقط رنگ) - مطابق اصل موجود در سند Baseline |
| اکشن اصلی | زنجیره‌ی `if/else if` روی `Model.Status` - **مستقیماً در Header، همیشه قابل‌مشاهده** |
| اکشن ثانویه | دراپ‌داون «More» (Edit/New Revision/AI Analysis/Freeze/Hold) |
| Tab | ۶ (طبق ممیزی فاز ۱: Overview/Attachments & AI/IDC Review/Revisions/Workflow History/Correspondence) |
| بخش‌های مرتبط | Correspondence Tab (Letters + Transmittals از فاز ۲A همین نشست) |
| Attachments | Tab اختصاصی («Attachments & AI») |
| Workflow/تاریخچه | Tab «Workflow History» + نمودار `_WorkflowStagesDiagram` |
| Audit | همان Workflow History Tab (رکورد خام زیرش نگه داشته شده - طبق ممیزی فاز ۱) |
| مجوز | چک‌های `ViewBag.CanFreeze`/`CanUnfreeze`/`CanHold` از کنترلر می‌آیند - View فقط نمایش می‌دهد |
| **بریدکرامب - یافته‌ی جدید و مهم** | **این صفحه یک `<nav aria-label="breadcrumb">` دستی و مستقل خودش را دارد (خط ۲۲-۲۷)، جدا از `_Breadcrumb.cshtml` مشترکی که `_Layout.cshtml` قبل از `@RenderBody()` بدون‌شرط رندر می‌کند. یعنی این صفحه‌ی خاص دو بریدکرامب همزمان نشان می‌دهد - یک باگ بصری واقعی که هیچ ممیزی قبلی (نه فاز ۱، نه فاز ۲B) آن را ثبت نکرده بود، چون هردو قبلاً `_Breadcrumb.cshtml` را فقط از زاویه‌ی «کم‌عمق بودن»/«دورزده‌شدن» بررسی کرده بودند، نه «دوباره اضافه‌شدن».** |
| تأیید تخریب/Confirm | ترکیبی: اکثر اکشن‌ها Bootstrap Modal (`data-bs-toggle="modal"`)، ولی Unfreeze از `onclick="return confirm(...)"` بومی مرورگر استفاده می‌کند - دو الگوی متفاوت در همان صفحه |

### VDR (`Views/VDR/Details.cshtml`)

| بُعد | یافته |
|---|---|
| Header | `content-card` ساده‌تر - شماره‌ی مدرک + Rev (h5)، Vendor/Project، Badge وضعیت + Overdue، **بدون اکشن** |
| نمودار مراحل | بلافاصله زیر Header (`_VdrWorkflowStagesDiagram`) |
| Tab | ۵ (Overview/Submissions/Comments/Workflow/Correspondence) |
| **اکشن اصلی گردش‌کار** | **برخلاف EDMS، اکشن‌های واقعی (SubmitForReview/DisciplineRespond/LeadReview/ReturnToVendor) در Header نیستند - همه داخل تب «Workflow»‌اند، پشت یک کلیک اضافه** |
| Attachments | ندارد به‌صورت Tab مجزا؛ داخل Comments (Comment/Reply Sheet Upload) |
| بریدکرامب | فقط همان `_Breadcrumb.cshtml` مشترک - بدون تکرار (برخلاف EDMS) |

### Equipment (`Views/Equipment/Details.cshtml`) — یافته‌ی اصلاحی این فاز

| بُعد | یافته |
|---|---|
| Header | `content-card` با آیکون بزرگ (`kpi-icon`)، Tag+Name (h5)، Badge‌های Criticality/Status/Calibration، خط متادیتا، دکمه‌ی «Explore Relationships» در گوشه |
| Tab | **۹ تب واقعی: Overview, Instrument, Asset, Documents, Procurement, Maintenance, Inspection, Quality, Classification** - بیشترین تعداد Tab در کل پروژه |
| کامنت خودِ فایل | صراحتاً می‌گوید از همان الگوی EngineeringDocuments/VDR پیروی می‌کند - نویسنده‌ی این View آگاهانه از الگوی EDMS کپی کرده |
| اکشن‌ها | داخل هر Tab پخش شده‌اند (نه یک Command Bar متمرکز) - مثلاً `AssignFoundation` یک دکمه‌ی مداد کوچک کنار فیلد مربوطه است، نه یک دکمه‌ی بالای صفحه |

### Work Order (`Views/WorkOrder/Details.cshtml`)

| بُعد | یافته |
|---|---|
| Header | `content-card` جدا برای نمودار مراحل (`_WorkOrderStagesDiagram`)، سپس یک `content-card` دوم برای شماره/Badge‌ها/اکشن |
| اکشن اصلی | یک دکمه‌ی وضعیت‌محور («Advance to X») + Modal شرطی با فیلدهای ISO 14224 (فقط برای Corrective/Emergency) |
| Tab | **ندارد** - صفحه‌ی تک‌ستونی |
| بدون Command Bar متمرکز | فقط یک اکشن اصلی؛ بقیه (احتمالاً Assign/Release) در ادامه‌ی صفحه پخش‌اند |

### Permit (`Views/Permit/Details.cshtml`) — منبع پیچیده‌ترین اکشن‌ها

| بُعد | یافته |
|---|---|
| Header | آیکون بزرگ + شماره+عنوان+Badge وضعیت (h5)، خط «کی/کِی درخواست داد»، ردیف متادیتای Equipment/FunctionalLocation/WorkOrder/Priority |
| **الگوی سوم منحصربه‌فرد** | یک بلوک محاسبه‌شده‌ی `actionItems` (نه Tab، نه Command Bar) که خلاصه‌ای از «چه‌چیزی نیاز به اقدام دارد» را با لینک لنگر (`#loto`, `#simops`, `#executiongate`) به بخش مربوطه‌ی همان صفحه‌ی بلند می‌سازد - از داده‌ی همان `ViewBag` موجود، بدون Query اضافه |
| اکشن پیچیده | ۴ زیرگردش‌کار مستقل (`ExecutionGate`, `GasTest`, `Isolation`, `Simops`) هرکدام کنترلر خودشان را دارند اما همه در همین یک صفحه (بدون Tab) رندر می‌شوند |
| مجوز | چک‌های Eligibility (`ExecutionGateResultDto`) از کنترلر می‌آیند، نه از View |
| Confirm | ترکیب مشابه EDMS (هم Modal، هم `confirm()` بومی) |

---

## ۴. ماتریس مقایسه‌ی Resource Workspace

| Resource | Header | Status | اکشن اصلی | Tab | Workflow | مرتبط | Attachments | History | Audit | مجوز | RTL |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Engineering Document | Present | Present (متن+رنگ) | Present (در Header) | Present (۶) | Present (نمودار+Tab) | Present (Tab) | Present (Tab) | Present (Tab) | Present (همان Tab) | Present (ViewBag از کنترلر) | Partial* |
| VDR | Present | Present | Present (**در Tab، نه Header**) | Present (۵) | Present (نمودار+Tab) | Present (Tab) | Partial (داخل Comments) | Present (Tab) | Present (همان Tab) | Present | Partial* |
| Equipment | Present | Present | Partial (پخش در Tabها) | Present (۹) | Missing (بدون نمودار مراحل - چون Workflow واحد ندارد) | Partial (P&ID لینک) | Present (Tab Documents) | Present (Tab Maintenance/Inspection) | Missing | Present | Partial* |
| Work Order | Present | Present | Present (یک دکمه) | Missing | Present (نمودار) | Partial (Permit لینک متقابل) | Unknown — نیازمند بررسی بیشتر خطوط ۱۰۰+ | Unknown — نیازمند بررسی بیشتر | Unknown | Present (ضمنی - `nextStatus` از کنترلر) | Partial* |
| Permit | Present | Present | Present (چندگانه، پخش‌شده) | Missing | Present (Eligibility-محور، بدون نمودار مراحل) | Present (WorkOrder لینک) | Unknown — نیازمند بررسی بیشتر | Present (`approvalHistory`) | Present (`approvalHistory`) | Present (`Eligibility` DTOها) | Partial* |
| Vendor | Present (سبک) | Present | Missing (صفحه فقط‌خواندنی) | Missing | Not applicable | Missing | Missing | Missing | Missing | Unknown | Partial* |
| Transmittal | Present | Present | Unknown — نیازمند بررسی بیشتر | Missing | Not applicable | Present (Documentها) | Unknown | Unknown | Unknown | Unknown | Partial* |
| Work Request | Present | Present | Unknown | Missing | Unknown | Unknown | Unknown | Unknown | Unknown | Unknown | Partial* |
| FunctionalLocation/MaintenanceStrategy/MaintenancePlan | Not applicable | Not applicable | Not applicable | Not applicable | Not applicable | Not applicable | Not applicable | Not applicable | Not applicable | Not applicable | Not applicable |

\* «Partial» برای RTL یعنی: صفحه از `dir` سراسری `<html>` درست پیروی می‌کند (بدون تنظیم
اختصاصی خراب)، ولی خودِ `site.css` (پایه‌ی مشترک همه‌ی این صفحات) دارای ۱۱ خاصیت CSS
جهت‌دار غیرمنطقی است (بخش ۱۶) - یعنی درستیِ RTL این صفحات به «شانس» بستگی دارد (این‌که آن ۱۱
خاصیت به موقعیت‌های تصادفاً بدون‌اهمیت خورده باشند)، نه یک تضمین معماری.

«Unknown» ها به‌صراحت به این معنی‌اند که فراتر از ۹۰-۱۰۰ خط اول آن فایل خوانده نشد (محدودیت
عمق این نمونه‌گیری، نه ادعای غیبت) - **هر ادعای قطعی درباره‌ی آن‌ها نیازمند بازخوانی کامل آن
فایل‌هاست، نه فرض.**

---

## ۵. فهرست الگوهای تکراری (با تحلیل ریسک استخراج)

| الگو | نمونه‌ها | واقعاً همان الگوی تعاملی؟ | پیاده‌سازی مشترک موجود؟ | ریسک استخراج |
|---|---|---|---|---|
| **نمودار مراحل گردش‌کار** | `_WorkflowStagesDiagram`, `_VdrWorkflowStagesDiagram` (CSS تقریباً بایت‌به‌بایت یکسان - فقط نام Keyframe فرق دارد)، `_WorkOrderStagesDiagram` (CSS متفاوت، منطق مشابه) | **بله - سه پیاده‌سازی مستقل از یک ایده** | خیر (سه فایل جدا) | **کم - امن‌ترین نامزد.** بخش مشترک (رندر لوپ + CSS) کاملاً بدون منطق کسب‌وکار است؛ بخش متفاوت (لیست/برچسب/آیکون/نقش هر مرحله + منطق تعیین `currentIndex`) از قبل در هرکدام Isolate است - می‌تواند به‌عنوان پارامتر ورودی داده شود |
| **Status Badge → رنگ** | `DocumentStatusBadge.CssClass` (نگاشت به کلاس CSS اختصاصی، نیازمند Rule در `admin.css`/`site.css` برای هر رشته‌ی وضعیت) در برابر VDR/`sbTop`، WorkOrder/`statusClass`، Permit/`badgeClass`، Equipment/`calBadgeClass` (هرکدام یک `switch` درون‌خطی به کلاس‌های آماده‌ی Bootstrap `bg-*`) | **بله مفهوماً، ولی دو استراتژی فنی متفاوت** | خیر (۵ پیاده‌سازی مستقل، از دو خانواده‌ی متفاوت) | **متوسط.** استخراج باید یکی از دو استراتژی را انتخاب کند - نه هردو را نگه دارد. توصیه در سند طراحی: استراتژی Bootstrap `bg-*` درون‌خطی (اکثریت - ۴ از ۵) پایه قرار بگیرد، نه استراتژی CSS اختصاصی EDMS |
| **کارت اطلاعات پایه (Header)** | هرکدام از ۷ صفحه‌ی بخش ۳/۴ یک `content-card` دستی با ترکیب متفاوتی از h4/h5 + Badge + خط متادیتا می‌سازند | بله، از نظر مفهومی («این چه چیزی است، چه وضعیتی دارد») - ولی Markup هرکدام کاملاً مستقل نوشته شده | خیر | **کم-متوسط.** یک «Header Contract» (نه یک Partial اجباری) امکان‌پذیر است - بخش ۹/سند طراحی |
| **بریدکرامب** | `_Breadcrumb.cshtml` مشترک (رندرشده توسط `_Layout` برای همه) + بریدکرامب دستی اضافه‌ی EngineeringDocuments (خط ۲۲-۲۷) + طبق ممیزی فاز ۱، ۸ صفحه‌ی دیگر (غیر از این نمونه) هم Markup دستی جای Partial استفاده می‌کنند | خیر یکسان نیست - برخی حذف می‌کنند، یکی اضافه می‌کند | بله (`_Breadcrumb.cshtml`) موجود است ولی درست استفاده نمی‌شود | **کم برای رفع - این یک باگ استفاده‌ی نادرست از یک کامپوننت موجود است، نه نیاز به کامپوننت جدید** |
| **کامنت‌های تخصیص‌یافته (Confirm Dialog)** | ۲۳ فایل از `onclick="return confirm(...)"` بومی استفاده می‌کنند (شامل هر سه‌ی EDMS/Permit/WorkOrder)؛ در کنارش، ده‌ها Modal Bootstrap دستی (`#freezeModal`, `#holdModal`, `#reviseModal`, `#issueModal`, ...) هرکدام جدا نوشته شده‌اند | **خیر - دو الگوی متفاوت (Native confirm در برابر Bootstrap Modal) با معیار انتخاب نامشخص** | خیر | **متوسط - بصری‌ست، نه امنیتی؛ نیازمند یک قاعده‌ی صریح «کِی کدام»، نه فقط یک Partial مشترک** |
| **Validation Summary** | فقط ۷ از ده‌ها فرم Create/Edit از `asp-validation-summary` استفاده می‌کنند | Not applicable - این یک الگوی *غایب* است، نه تکراری | خیر | Not applicable برای این بخش - یافته برای بخش ۹/طراحی فرم، نه Workspace |
| **ViewComponent/TagHelper** | **صفر مورد در کل `PEMS.Web`** (تأیید مستقیم با `find`) | Not applicable | خیر - حتی به‌عنوان مکانیزم فنی، این پروژه هرگز از این دو استفاده نکرده | **این یک محدودیت معماری کلان است، نه یک الگوی تکراری - باید در انتخاب نوع پیاده‌سازی (Partial View، نه ViewComponent/TagHelper) اثر بگذارد** |

---

## ۶. مقایسه‌ی EDMS در برابر CMMS

### آنچه EDMS خوب انجام می‌دهد
- Command Bar متمرکز و همیشه‌قابل‌مشاهده در بالای صفحه (نه پنهان در یک Tab).
- بنر وضعیت برجسته برای حالت‌های استثنایی (Frozen/Hold) با اکشن رفع همان‌جا.
- تفکیک روشن اکشن‌های پرتکرار (Header) در برابر کم‌تکرار (منوی More).

### آنچه CMMS کم دارد (اصلاح‌شده نسبت به فرض قبلی)
- **دیگر درست نیست که «کل CMMS بدون Tab است»** - Equipment از قبل ۹ Tab دارد. آنچه واقعاً کم
  است: **یک Command Bar متمرکز** - حتی در Equipment که Tab دارد، اکشن‌ها پخش‌شده‌اند، نه
  متمرکز در Header (برخلاف EDMS).
- WorkOrder/Permit به‌طور آگاهانه صفحه‌ی بلند مانده‌اند (نه فراموش‌شده) - Permit حتی یک الگوی
  ناوبری جایگزین (لنگر+خلاصه) ساخته که EDMS اصلاً ندارد.

### آنچه می‌تواند استاندارد شود
- ساختار Header (بخش ۹/سند طراحی).
- استراتژی نگاشت Status→رنگ (باید یکی از دو استراتژی موجود انتخاب شود، نه هردو).
- نمودار مراحل گردش‌کار (بخش ۵).

### آنچه باید ماژول‌محور بماند
- محل قرارگیری اکشن‌های گردش‌کار (Header در برابر Tab در برابر پخش‌شده) - چون این‌ها به
  تعداد/پیچیدگی واقعی اکشن‌های هر ماژول وابسته‌اند، نه یک تصمیم بصری دلبخواه. تحمیل الگوی
  EDMS به Permit (که ۴ زیرگردش‌کار مستقل دارد) می‌تواند یک Command Bar شلوغ و غیرقابل‌استفاده
  بسازد.
- تصمیم Tab در برابر صفحه‌ی بلند (بخش ۸).

---

## ۷. ممیزی Command Bar

| Resource | Primary | Secondary | Destructive | Workflow | Permission-Gated | Disabled | Conditional | نیازمند Confirm |
|---|---|---|---|---|---|---|---|---|
| Engineering Document | وضعیت‌محور (if/else) | منوی More | ندارد به‌صراحت (Reject نزدیک است ولی بازگشت‌پذیر) | بله (Submit/Check/Approve/Issue) | بله (`ViewBag.CanFreeze` و مشابه) | ضمنی (دکمه‌ها فقط وقتی شرط برقرار است رندر می‌شوند - نه Disable واقعی) | بله (بر اساس Status) | بله (اکثراً Modal، Unfreeze با `confirm()`) |
| VDR | داخل Tab Workflow | ندارد جدا | ندارد | بله | بله (فاز امنیتی این نشست) | همان الگوی EDMS | بله (بر اساس `currentStageName`) | Modal‌های Comment/Reject |
| Permit | چندگانه، پخش‌شده | ندارد متمرکز | Revoke/Cancel وجود دارد (طبق Seed مجوز) | بله (Approve/Reject/Issue/...) | بله (`ExecutionGateResultDto`/`Eligibility`) | بله (دکمه‌های Eligibility-محور) | بله (خیلی شدید - ۸+ DTO Eligibility جدا) | ترکیبی |
| Work Order | یک دکمه («Advance to X») | Unknown | Unknown | بله | ضمنی (`nextStatus` از کنترلر) | بله (اگر `nextStatus == null`) | بله | Modal برای فیلدهای ISO 14224 |

**نتیجه‌ی صریح طبق بند ۷ دستور این فاز:** شواهد این ممیزی (برخلاف امکان بازبینی) **همان
نتیجه‌ی فاز ۲A را تقویت می‌کند، نه رد**: هیچ Command Bar عمومی امن نیست. تفاوت میان EDMS
(زنجیره‌ی خطی if/else روی یک رشته‌ی Status)، VDR (سوییچ روی نام مرحله‌ی موتور)، و Permit
(۸+ DTO مستقل Eligibility، بدون هیچ رشته‌ی Status محوری) آنقدر بنیادی است که یک انتزاع
عمومی («یک Component که لیست اکشن‌ها را می‌گیرد و رندر می‌کند») یا باید آنقدر ساده باشد که
هیچ سودی نداشته باشد (فقط `foreach` روی یک لیست دکمه که خودِ View از قبل به همین سادگی
می‌نویسد)، یا آنقدر پیچیده شود که منطق تصمیم‌گیری (کدام دکمه کِی نشان داده شود) را از
Controller/DTO به داخل یک Component عمومی بکشد - که دقیقاً همان ریسکی است که بند ۱۳ همین
دستور («هرگز مجوز کسب‌وکار را داخل UI مشترک نبر») منع می‌کند. **بدون Command Bar عمومی -
فقط یک قرارداد نام‌گذاری/چیدمان بصری مشترک (بخش ۹ سند طراحی)، نه یک Component رفتاری
مشترک.**

## ۸. ممیزی Tab و ناوبری منبع

| نوع ناوبری | نمونه | مسیر-محور؟ | Query-محور؟ | Client-Side؟ | مجوز حفظ می‌شود؟ | Workflow مستقل درون هر Tab؟ |
|---|---|---|---|---|---|---|
| Bootstrap Tab (`data-bs-toggle="tab"`) | EDMS (۶)، VDR (۵)، Equipment (۹) | خیر - تمام Tabها در یک درخواست HTTP بارگذاری می‌شوند؛ تغییر Tab فقط نمایش/عدم‌نمایش سمت کلاینت است | خیر (بدون `?tab=`) | **بله - کاملاً Client-Side، بدون Deep-Link** | بله (کل صفحه یک‌بار طبق مجوز کنترلر رندر می‌شود؛ محتوای هر Tab هم از قبل توسط همان کنترلر آماده شده) | بله (VDR Workflow Tab یک زیرگردش‌کار کامل درون خودش دارد) |
| صفحه‌ی بلند + اسکرول | WorkOrder | Not applicable | Not applicable | Not applicable | بله | بله |
| صفحه‌ی بلند + لنگر خلاصه‌ساز | Permit | خیر (لنگر HTML خام `#id`) | خیر | بله (پیمایش نرم مرورگر) | بله | بله (هر لنگر به یک زیرگردش‌کار جدا اشاره می‌کند) |
| Child Resource جدا (صفحه‌ی مستقل) | `GasTest`/`Isolation`/`Simops`/`ExecutionGate` نسبت به `Permit` - این‌ها اصلاً View ندارند، فقط Action هستند که در همان صفحه‌ی والد رندر می‌شوند | Not applicable | Not applicable | Not applicable | بله | بله |

**یافته‌ی مهم:** هیچ Tab در کل PEMS مسیر-محور (Deep-Linkable با URL، مثل
`/EngineeringDocuments/Details/5?tab=attachments`) نیست - همه صرفاً نمایش/پنهان‌سازی
Client-Side هستند. این یعنی امروز نمی‌توان مستقیماً به «Tab بایگانی مدرک X» لینک داد -
یک محدودیت واقعی برای هر لینک‌دهی آینده از Help (سند Phase 2B) یا Search results به یک Tab
خاص.

**معیار پیشنهادی Tab در برابر Section در برابر Child Resource در برابر صفحه‌ی گردش‌کار جدا
(بر اساس شواهد، نه اختراع):**

| معیار | Tab | Section (کارت درون همان صفحه) | Child Resource (کنترلر/صفحه‌ی جدا) | صفحه‌ی گردش‌کار جدا |
|---|---|---|---|---|
| چه‌وقت مناسب است | محتوا واقعاً حجیم است و کاربر معمولاً فقط یکی را در هر بازدید می‌خواهد (مثل EDMS Attachments/History) | محتوا کوتاه است و کاربر معمولاً همه را با هم می‌خواهد (مثل Permit metadata) | یک موجودیت مستقل با شناسه/چرخه‌حیات خودش است، فقط از این صفحه لینک می‌خورد (مثل Equipment→EngineeringDocuments P&ID) | یک زیرگردش‌کار با مراحل/تأییدهای خودش دارد که به‌ندرت با بقیه‌ی صفحه هم‌زمان دیده می‌شود |
| نمونه‌ی موجود | EDMS/VDR/Equipment | Permit/WorkOrder/Vendor | Equipment→P&ID، Permit→WorkOrder | (در PEMS امروز دیده نشد - `GasTest`/`Isolation` علی‌رغم پیچیدگی، همچنان Section‌اند نه صفحه‌ی جدا) |

---

## ۹. طراحی Header منبع

### فیلدهای همیشه‌قابل‌مشاهده (طبق تقاطع همه‌ی نمونه‌های بررسی‌شده)
شناسه/شماره، وضعیت (متن+رنگ)، یک خط متادیتای خلاصه (نوع/پروژه/دیسیپلین یا معادل ماژول).

### فیلدهای شرطی‌قابل‌مشاهده
Badge‌های استثنایی (Frozen/Hold/Overdue/Criticality) - فقط وقتی مقدار غیرپیش‌فرض دارند
(الگوی EDMS/VDR/Equipment، هرسه این را رعایت می‌کنند - **یک اجماع واقعی، نه فرض**).

### فقط در Tab/جزئیات
توضیحات کامل، تاریخچه‌ی کامل، فیلدهای فرعی (مثل `Manufacturer`/`Model` در Equipment که
داخل یک ردیف دوم است، نه سطر اول Header).

### وابسته‌به‌زمینه
دکمه‌ی اکشن اصلی - طبق شواهد بخش ۷، این نمی‌تواند یک قرارداد ثابت باشد؛ فقط *محل* آن (سمت
راست/چپ Header، طبق جهت) قابل استانداردسازی است، نه *محتوای* آن.

---

## ۱۰. تراکم اطلاعات و سلسله‌مراتب بصری

مشکلات مشخص با ارجاع فایل:

- **`Equipment/Details.cshtml`**: ۹ Tab با نام‌های مشابه (`Documents` در برابر
  `Procurement` در برابر `Quality` - مرز محتوایی این سه برای کاربر تازه‌کار روشن نیست بدون
  باز کردن هرکدام؛ نیازمند بازبینی محتوایی، نه فقط بصری).
- **`Permit/Details.cshtml`**: تراکم بسیار بالای متغیر (۸+ DTO Eligibility جدا در همان
  بلوک `@{ }` - خطوط ۴۳-۵۰) - این خودِ صفحه را برای نگهداری آینده سنگین می‌کند، هرچند
  خروجی بصری آن (خلاصه‌ی Action Items) یک نقطه‌ی قوت واقعی است.
- **دو الگوی Confirm (بخش ۵)** یک ناهماهنگی بصری واقعی است که کاربر در همان صفحه هم Modal
  استاندارد پروژه و هم پنجره‌ی خام مرورگر را می‌بیند (`EngineeringDocuments/Details.cshtml`
  خط ۱۶۸).
- **بریدکرامب دوگانه در EDMS (بخش ۳)** دقیقاً یک نمونه‌ی «برچسب تکراری»/eng «تراکم اضافه»
  است که این بخش از دستور صریحاً خواسته بود مستند شود.
- بدون شواهد کافی برای ادعای «اسکرول بیش‌ازحد» یا «Card بیش‌ازاندازه بزرگ» به‌صورت کمّی
  (نیازمند رندر واقعی/اسکرین‌شات که در این محیط CLI ممکن نبود) - **این محدودیت صریحاً اعلام
  می‌شود، نه پنهان.**

## ۱۱. اصول Azure-Inspired قابل‌استفاده (نه کپی)

قبلاً در `PEMS_PORTAL_UX_AZURE_INSPIRED_AUDIT.md` (فاز ۱ همین نشست) کاملاً پوشش داده شده -
این ممیزی فقط یک نکته‌ی جدید اضافه می‌کند: **الگوی «Action Items خلاصه + لنگر» در
`Permit/Details.cshtml` عملاً معادل بومی PEMS برای مفهوم Azure «Recommendations»/«What needs
attention» است - از قبل وجود دارد، فقط تعمیم‌نیافته.** این یک نامزد خوب برای تعمیم به سایر
منابع پیچیده (VDR/EDMS) است، **نه از Azure کپی کردن، بلکه تعمیم چیزی که PEMS خودش ساخته.**

## ۱۲. همگرایی EDMS در برابر CMMS

پوشش کامل در بخش ۶.

## ۱۳. مرز مجوز

هر سه نمونه‌ی بررسی‌شده (EDMS/VDR/Permit) دقیقاً همان اصل صحیح را رعایت می‌کنند: **View هرگز
تصمیم مجوز نمی‌گیرد، فقط یک `bool`/DTO آماده از `ViewBag` را نمایش/عدم‌نمایش می‌کند**
(`ViewBag.CanFreeze`, `ExecutionGateResultDto.Result == "Eligible"`, `idcStatusForPage`).
**این ممیزی هیچ نمونه‌ی نقض این اصل پیدا نکرد** - یعنی هر طراحی Workspace آینده می‌تواند با
اطمینان همین قرارداد را ادامه دهد: **Component مشترک فقط یک `bool`/enum ورودی می‌گیرد، هرگز
خودش `IPermissionService`/`IAccessControlService` را صدا نمی‌زند.**

## ۱۴. مرز گردش‌کار

طبق شواهد بخش ۳/۷، مرز طبیعی همین است که دستور این فاز پیشنهاد داده بود:

```text
Work Order / Permit / VDR / Engineering Document
├── Resource Workspace مشترک (پیشنهادی)
│   ├── Header (شماره/عنوان/وضعیت/متادیتای خلاصه)
│   ├── نمودار مراحل (در صورت وجود یک گردش‌کار خطی واحد)
│   └── چیدمان Tab/Breadcrumb
│
└── دامنه‌ی اختصاصی ماژول (بدون تغییر)
    ├── EDMS: Freeze/Hold/IDC Review/Revision Controls
    ├── VDR: Discipline Review موازی/Return-to-Vendor
    ├── Permit: Isolation/GasTest/Simops/ExecutionGate Eligibility
    └── WorkOrder: ISO 14224 Failure Fields/Spares/Skills
```

هیچ منطق تصمیم‌گیری گردش‌کار (چه اکشنی مجاز است، چه فیلدی لازم است) هرگز نباید به داخل
Workspace مشترک منتقل شود - فقط *نمایش* نتیجه‌ی از‌قبل‌محاسبه‌شده.

## ۱۵. ملاحظات واکنش‌گرا (Responsive)

**محدودیت صریح:** هیچ مرورگر/ابزار رندر واقعی در این محیط CLI در دسترس نبود؛ رفتار
واکنش‌گرا **از طریق کلاس‌های Bootstrap Grid موجود در Markup استنتاج شد، نه دیده شد.** شواهد
استخراج‌شده: تمام صفحات بررسی‌شده از `row g-2/g-3` + `col-md-*` بدون `col-sm-*`/`col-*`
پایه‌ی صریح استفاده می‌کنند (یعنی زیر `md` Breakpoint، همه‌ی ستون‌ها به‌طور پیش‌فرض
Bootstrap تمام‌عرض می‌شوند - رفتار قابل‌قبول ولی آزمایش‌نشده). Tabها (`nav-tabs`) در موبایل
طبق رفتار پیش‌فرض Bootstrap فقط Wrap/Scroll افقی می‌کنند - هیچ نسخه‌ی موبایل اختصاصی (مثل
تبدیل Tab به Accordion) پیاده‌سازی نشده.

## ۱۶. دوزبانگی و RTL/LTR

- `site.css` (بارگذاری‌شده در همه‌ی صفحات، طبق `_Layout.cshtml`) دارای **۱۱ خاصیت CSS
  جهت‌دار غیرمنطقی** (`margin-left`/`padding-right`/و مشابه، به‌جای معادل منطقی) است -
  شمارش مستقیم تأیید شد.
- تمام صفحات بررسی‌شده متن انگلیسی خام دارند برای اکثر برچسب‌ها (`"Approve"`, `"Reject"`,
  "Advance to Completed") - فقط چند رشته‌ی پراکنده فارسی خام دارند (اغلب پیام‌های
  TempData/Placeholder، نه برچسب دائمی UI) - **یعنی این صفحات هنوز کاملاً دوزبانه نیستند،
  صرف‌نظر از این‌که `dir` درست تغییر می‌کند.** این با یافته‌ی ممیزی فاز ۱ (بخش K) هم‌خوان
  است.
- شماره‌های مدرک/تگ (`DocumentNo`, `TagNumber`, `PermitNumber`) در هر دو جهت باید LTR بمانند
  (طبق قرارداد بین‌المللی برای شناسه‌های الفبایی-عددی) - **هیچ صفحه‌ی بررسی‌شده صراحتاً
  `dir="ltr"` را روی این مقادیر Force نمی‌کند** - در حالت RTL این می‌تواند به‌اشتباه معکوس
  رندر شود اگر شامل ترکیب حروف/اعداد خاصی باشد (ریسک بصری، نه امنیتی) - **یافته‌ی جدید،
  قبلاً مستند نشده بود.**

## ۱۷. دسترس‌پذیری (Accessibility)

- `role="tablist"`/`role="tabpanel"`/`role="presentation"` به‌درستی روی Tabها استفاده
  می‌شود (Bootstrap پیش‌فرض) - **مثبت.**
- `aria-current="page"` روی آیتم فعال بریدکرامب دستی EDMS استفاده شده - **مثبت، ولی چون
  این بریدکرامب دوم/تکراری است (بخش ۳)، این مزیت با هزینه‌ی سردرگمی دوباره خنثی می‌شود.**
- دکمه‌های آیکون‌فقط (مثل مداد `AssignFoundation` در Equipment) `title="..."` دارند (برای
  Tooltip) اما بدون `aria-label` صریح - در بیشتر مرورگرها/screen reader ها `title` جایگزین
  ضعیف‌تری نسبت به `aria-label` است.
- تأیید تخریب دوگانه (بخش ۵/۱۰): `confirm()` بومی مرورگر واقعاً از نظر Accessibility
  **بهتر** از اکثر Modalهای دستی است (Focus Trap تضمینی، خواندن توسط Screen Reader
  تضمینی) - این یعنی یکدست‌سازی باید *به‌جای* حذف کامل `confirm()`، رفتار Modalهای دستی را
  به همان استاندارد برساند، نه برعکس.
- بدون شواهد کافی برای ناوبری کامل با کیبورد/فوکوس مرئی روی خودِ Command Bar (نیازمند
  بررسی CSS/JS عمیق‌تر که خارج از این نمونه‌گیری بود).

## ۱۸. عملکرد (Performance)

- `EngineeringDocumentsController.Details`، `EquipmentController.Details`، و
  `PermitController.Details` (طبق شواهد ViewBag در Details.cshtml هرکدام) هرکدام **حداقل
  ۵-۸ منبع داده‌ی جدا** را برای رندر یک صفحه فرا می‌خوانند (مثلاً Equipment: Passport +
  FunctionalLocations + Reliability + Inspections + MaintenanceIntelligence +
  PendingClassificationSuggestion + ClassificationExplanation - طبق کد کنترلر که در فاز
  ۲B همین نشست کامل خوانده شده بود).
- **ریسک مستقیم برای هر Workspace مشترک آینده:** اگر یک Component/Partial مشترک به‌صورت
  پیش‌فرض «هر پنل اختیاری» (History/Audit/Related/Attachments) را بدون قید بارگذاری کند،
  این عدد برای منابعی مثل Equipment که از قبل ۸ منبع دارد، دوباره اضافه می‌شود - **قاعده‌ی
  طراحی: هر پنل اختیاری باید Lazy/Conditional باشد (فقط وقتی Tab مربوطه باز می‌شود یا
  ViewBag آن مقدار غیرخالی دارد)، نه یک بارگذاری خودکار روی هر صفحه.**
- بدون شواهد مستقیم N+1 در سطح EF Core (نیازمند بررسی SQL Profiler/زمان اجرا که در این
  محیط ممکن نبود) - **محدودیت صریح.**

---

## ۱۹. قرارداد Workspace مشترک پیشنهادی (مفهومی، غیرقابل‌پیاده‌سازی در این فاز)

نام‌گذاری بر اساس ترمینولوژی موجود PEMS (نه اختراع تازه) - `content-card` از قبل واحد بصری
پایه‌ی همه‌جا است؛ پیشنهاد فقط یک لایه‌ی نازک روی همان است:

```text
PemsResourceHeader        (Partial View - نه ViewComponent، طبق بخش ۵ محدودیت معماری)
├── مسئولیت: رندر شناسه/وضعیت/متادیتای خلاصه/Badgeهای شرطی به‌شکل یکدست
├── ورودی: یک مدل ساده (نه Entity/DTO دامنه) - {Title, Identifier, StatusText, StatusTone
│         (enum: Success/Warning/Danger/Info/Neutral - نه رشته‌ی خام)، MetaLine,
│         ConditionalBadges[]}
├── خروجی: HTML - بدون State، بدون Side-Effect
├── وابستگی: هیچ (فقط داده‌ی ورودی)
├── مرز مجوز: صفر - این Partial هرگز IPermissionService را نمی‌بیند
├── دوزبانگی: متن‌های ورودی از قبل ترجمه‌شده باید باشند (توسط Controller/View فراخوان)؛
│             خودِ Partial فقط `dir="ltr"` را روی `Identifier` Force می‌کند (بخش ۱۶)
├── واکنش‌گرا: از همان `d-flex flex-wrap gap-*` موجود استفاده می‌کند - بدون CSS جدید
├── قابل‌استفاده‌ی مجدد؟ بله - این تنها واحدی است که در این ممیزی «آماده» تشخیص داده شد
└── فقط‌مفهومی؟ خیر - این‌جا Ready for Implementation است (بخش ۲۱)

PemsStageDiagram (تعمیم‌یافته از ۳ نسخه‌ی موجود)
├── مسئولیت: رندر بصری پیشرفت یک گردش‌کار خطی
├── ورودی: {Stages: [(Key, Label, Icon, Role)], CurrentIndex, IsBranchedBack: bool}
├── وابستگی: هیچ
├── مرز مجوز: صفر
└── قابل‌استفاده‌ی مجدد؟ بله - نامزد دوم آماده

(هرچیز دیگری - Command Area، Navigation، Content - طبق شواهد بخش ۷/۸ فقط قرارداد بصری/نامی
هستند، نه Component رفتاری مشترک؛ جزئیات کامل در سند طراحی، بخش‌های مربوطه)
```

## ۲۰. ضدالگوهایی که این ممیزی صریحاً رد می‌کند

- **یک ابرکامپوننت Resource که همه‌چیز را کنترل کند** - شواهد بخش ۷ ثابت کرد که حتی «کجا
  دکمه‌ی اکشن اصلی می‌رود» بین سه ماژول اصلی متفاوت است؛ یک ابرکامپوننت این تفاوت را یا
  نادیده می‌گیرد یا با پیچیدگی داخلی غیرقابل‌نگهداری جبران می‌کند.
- **یک ViewModel برای همه‌ی منابع** - `EngineeringDocumentDto`، `VDRItemDto`،
  `EquipmentPassportDto`، `PermitDetailDto` هرکدام شکل کاملاً متفاوت و دامنه‌محور دارند؛
  یک ViewModel عمومی یا باید `object`/`dynamic` بگیرد (بی‌فایده) یا به یک لایه‌ی نگاشت
  اضافه نیاز دارد که خودش منبع باگ می‌شود.
- **جایگزینی گردش‌کار دامنه با یک CRUD عمومی** - رد شد؛ Permit/VDR/EDMS هرکدام موتور
  گردش‌کار واقعی و متفاوت (`IWorkflowRuntime` به‌روش‌های متفاوت) دارند.
- **مجوز داخل Component مشترک** - رد شد؛ بخش ۱۳ ثابت کرد الگوی فعلی (bool از ViewBag) از
  قبل درست است و نباید شکسته شود.
- **منطق کسب‌وکار در Razor** - هیچ نمونه‌ی نقض در بررسی این فاز پیدا نشد؛ باید همین‌طور
  بماند.
- **Command Handler عمومی برای دامنه‌های نامرتبط** - رد شد (بخش ۷).
- **Tab اجباری در همه‌جا** - رد شد؛ Vendor (۳۷ خط) با Tab فقط پیچیده‌تر می‌شد بدون فایده.
- **Command Bar اجباری در همه‌جا** - رد شد (بخش ۷).
- **بارگذاری خودکار همه‌ی داده‌های مرتبط** - رد شد (بخش ۱۸).
- **سیستم Localization دوم** - هیچ نیازی کشف نشد؛ سیستم موجود (`SharedResource.resx` +
  Cookie) کافی است.
- **سیستم CSS Token دوم** - قبلاً در ممیزی فاز ۱ به‌عنوان یک مشکل موجود (نه پیشنهاد جدید)
  مستند شده؛ این ممیزی آن را تکرار نمی‌کند، فقط تأیید می‌کند که هرگونه Workspace جدید نباید
  یک چهارمین منبع Token بسازد.
- **منطق ناوبری جاوااسکریپت تکراری** - `PemsTooltips` الگوی درستی برای تقلید است (یک
  Namespace سراسری، Idempotent)؛ هر جاوااسکریپت جدید Workspace باید همین الگو را دنبال کند،
  نه یک سیستم جدا.

## ۲۱. ارزیابی آمادگی پیاده‌سازی

**نتیجه: بخشی آماده‌ی پیاده‌سازی، بخشی نیازمند تصمیم محصولی - نه یک بله/خیر یکپارچه.**

| واحد پیشنهادی | وضعیت |
|---|---|
| `PemsStageDiagram` مشترک (تعمیم سه نسخه‌ی موجود) | **آماده‌ی پیاده‌سازی** - کمترین ریسک، بیشترین شواهد پشتیبان |
| رفع بریدکرامب دوگانه‌ی EDMS | **آماده‌ی پیاده‌سازی** - یک‌خطی، بدون ابهام |
| یکسان‌سازی استراتژی Status→رنگ (انتخاب استراتژی Bootstrap `bg-*`) | **آماده‌ی پیاده‌سازی برای اعضای جدید؛ مهاجرت EDMS/Letter به آن نیازمند بررسی هر مصرف‌کننده‌ی `badge-status-*` در CSS - ریسک کم اما نه صفر** |
| `PemsResourceHeader` Partial | **آماده‌ی پیاده‌سازی برای منابع جدید/بازطراحی‌شده؛ اعمال روی EDMS/VDR/Equipment موجود نیازمند تکرار طراحی (چون فیلدهای دقیق Header هرکدام فرق دارد) - نیازمند یک نوبت طراحی مصرف‌کننده به‌ازای هر منبع، نه یک تغییر یک‌باره** |
| Command Bar عمومی | **مناسب برای انتزاع مشترک نیست** (بخش ۷/۲۰) - رد شد |
| قرارداد Tab-محور اجباری | **مناسب برای انتزاع مشترک نیست** (بخش ۸/۲۰) - رد شد |
| تعمیم الگوی «Action Items خلاصه» Permit به VDR/EDMS | **نیازمند یک نوبت طراحی جدا** - ایده‌ی خوب، ولی جزئیات (کدام شرایط، کدام لنگر) دامنه‌محور است |

**کوچک‌ترین هدف اول امن برای پیاده‌سازی (در صورت تأیید):** `PemsStageDiagram` - چون (۱)
سه پیاده‌سازی موجود از قبل ثابت کرده‌اند این ایده کار می‌کند، (۲) صفر منطق کسب‌وکار/مجوز
دارد، (۳) رگرسیون‌پذیر با مقایسه‌ی تصویری ساده (همان سه صفحه، قبل/بعد) قابل تأیید است.

## ۲۲. موارد موکول‌شده

- بازخوانی کامل `WorkOrder/Details.cshtml`/`Permit/Details.cshtml` فراتر از خط ۱۰۰ (برای
  رفع ردیف‌های «Unknown» ماتریس بخش ۴).
- بررسی کامل `Transmittal`/`MaterialRequisition`/`WorkRequest`/`Asset` Details (فقط شمارش
  ساختاری انجام شد، نه محتوا).
- تصمیم محصولی درباره‌ی این‌که آیا `FunctionalLocation`/`MaintenanceStrategy`/
  `MaintenancePlan` اصلاً باید صفحه‌ی Details بگیرند.
- طراحی دقیق قاعده‌ی «کِی Native Confirm، کِی Bootstrap Modal» (بخش ۵/۱۷).
- اعتبارسنجی واقعی Responsive/Accessibility با مرورگر (این محیط CLI امکانش را نداشت).
- بررسی SQL/Profiler برای تأیید کمّی N+1 (بخش ۱۸).
- مهاجرت واقعی EDMS/Letter از `badge-status-*` به استراتژی مشترک (اگر تصویب شود).

---

**سند بعدی این فاز:** `docs/design/PEMS_SHARED_RESOURCE_WORKSPACE_SPEC.md` - قرارداد کامل
Header/Command Area/Navigation/Content بر اساس دقیقاً همین شواهد.
