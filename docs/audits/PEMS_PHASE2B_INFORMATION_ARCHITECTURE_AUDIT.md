# PEMS Phase 2B — Portal Information Architecture + Contextual Help Audit

**تاریخ:** 2026-09-13
**نوع سند:** Audit-first، بدون تغییر کد. هیچ Migration، هیچ Entity جدید، هیچ تغییر رفتار
Workflow/Authorization/Search-Security در این فاز اعمال نشده - طبق محدودیت صریح دامنه‌ی این
فاز.
**پیش‌نیاز خوانده‌شده:** این سند روی یافته‌های `docs/audits/PEMS_PORTAL_UX_AZURE_INSPIRED_AUDIT.md`
(از این پس «ممیزی فاز ۱») بنا می‌شود و آن‌ها را تکرار نمی‌کند - فقط جایی که عمق بیشتری لازم
بود (فهرست کامل ماژول‌ها، درخت ناوبری دقیق، مقایسه‌ی EDMS در برابر CMMS، معماری Help) گسترش
یافته است. برای یافته‌های Shell/Theme/Sidebar-Toggle/CSS Token که قبلاً مستند و رفع شده‌اند،
همان سند مرجع باقی می‌ماند.

---

## ۱. فهرست کامل ماژول‌ها (Complete Module Inventory)

شمارش مستقیم `PEMS.Web/Controllers/*.cs`: **۸۶ کنترلر**. جدول زیر هرکدام را به بخش سایدباری
واقعی‌اش (از `_SidebarMenu.cshtml`) نگاشت می‌کند - نه یک دسته‌بندی مفهومی حدسی.

| بخش سایدبار (Service Nav Group) | کنترلرها |
|---|---|
| **Engineering Management** (۱۴) | EngineeringDashboard, Projects, EngineeringDocuments, DocumentRegister, DocumentRevisions, DocumentWorkflow, VDR, VDRImport, MDRImport, Transmittal, MDB, ProcurementPackage, MaterialRequisition, Discipline |
| **AI & Knowledge** (۶) | DocumentUpload, EngineeringDocumentIntelligence, KnowledgeBase, AISearch, ChatAssistant, AIInsights |
| **Correspondence & Doc Control** (۳) | Letter, DCC, TechnicalQuery |
| **Quality Control** (۲) | QC, MOC |
| **Contract Management** (۲ + ۲ Coming Soon) | Contract, Vendor (+ Contract Milestones، Claims به‌عنوان placeholder) |
| **Project Controls** (۴) | Task, Tag, Report, Meeting |
| **CMMS Integration** (۲۵) | Equipment, EquipmentImport, Instrument, Asset, MaintenanceStrategy, MaintenancePlan, JobPlan, WorkRequest, WorkOrder, SparePart, Team, Crew, MaintenanceMasterData, EquipmentClassification, RcmFmea, EquipmentCriticality, CriticalityAssessment, FunctionalLocation, FunctionalLocationImport, Reliability, Inspection, Kpi, Permit, CmmsReadiness, CmmsOperations |
| **Inventory & Materials** (۶) | Inventory, MaterialItem, InventoryMasterData, InventoryTransaction, StoreRequisition, MrsImport |
| **Administration → AI Administration** (۵) | Intake, OCRProcessing, AIExtraction, TableExtraction, Relationship |
| **Administration → core** (۸) | Settings, Users\*, Roles\*, PermissionMatrix\*, AuditLog\*, RoutingRules\*, Department, Employee (\* = Admin Area) |
| **بالای سایدبار (خارج از هر بخش)** (۲) | Home, QuickSetup |

جمع کنترلرهای **نمایش‌داده‌شده در سایدبار:** ۷۹.

### کنترلرهای خارج از سایدبار (۷ مورد) - طبقه‌بندی‌شده به‌جای فرض «همه یک گپ‌اند»

| کنترلر | چرا در سایدبار نیست | نوع |
|---|---|---|
| `AccountController` | Login/Logout/ManagePasskeys - قبل از احرازهویت یا از منوی کاربر | **درست - Global Nav، نه Service Nav** |
| `CultureController` | فقط یک Endpoint سوییچ زبان (`SetCulture`) که از دراپ‌داون Topbar صدا زده می‌شود | **درست - بدون View، بخشی از Topbar** |
| `ThemeController` | همان، برای دراپ‌داون تم | **درست - بدون View، بخشی از Topbar** |
| `MyProfileController` | از منوی آواتار کاربر | **درست - Global Nav (User Menu)، نه Service Nav** |
| `NotificationController` | از زنگوله‌ی Topbar (`Dropdown` partial) | **درست - Global Nav** |
| `DashboardSettingsController` | از دکمه‌ی «Customize» روی خودِ Dashboard | **درست - لینک درون‌صفحه‌ای، نه ناوبری سطح‌بالا** |
| `ComingSoonController` | مقصد لینک‌های Placeholder (Contract Milestones/Claims) | **درست - Utility Target** |
| `FilesController`, `SearchApiController`, `GlobalSearchController` | اولی فقط Endpoint دانلود فایل است (بدون View)؛ دومی فقط API (بدون View)؛ سومی مقصد جعبه‌ی جست‌وجوی Topbar است (`asp-controller="GlobalSearch"` در `_Layout.cshtml` خط ۷۷) | **درست - هرسه یا API هستند یا مقصد یک ورودی Global Nav دیگر** |
| `ExecutionGateController`, `GasTestController`, `IsolationController`, `SimopsController` | هرکدام **هیچ اکشن `Index` ندارند** - فقط اکشن‌های Create/Verify/Resolve/Override که از **داخل صفحه‌ی `Permit/Details`** صدا زده می‌شوند (تأیید شده با خواندن مستقیم هرکدام) | **درست - این‌ها صفحه‌ی مستقل نیستند، بلکه اکشن‌های Resource-Level روی یک Permit خاص‌اند - دقیقاً مثل این‌که در معماری Azure این‌ها دکمه‌های داخل یک Blade باشند، نه آیتم منو** |
| **`ShiftHandoverController`** | **هیچ دلیلی پیدا نشد** - `Index(status)`، `Create` (GET+POST)، `Details(id)`، `Prepare`، `Acknowledge`، `Complete` - یک صفحه‌ی فهرست کامل با فیلتر وضعیت و یک گردش‌کار واقعی (Prepare→Acknowledge→Complete)، دقیقاً هم‌شکل با `WorkRequest`/`Permit` | **یافته‌ی واقعی: شکاف ناوبری.** کاربر فقط با دانستن URL مستقیم `/ShiftHandover` می‌تواند به این ماژول برسد - هیچ لینکی در `_SidebarMenu.cshtml` به آن اشاره نمی‌کند. جای طبیعی آن (کنار `Team`/`Crew`) در بخش CMMS Integration است |
| `AIController` | فقط یک اکشن `Test()` - به‌نظر Endpoint تشخیصی/توسعه، نه یک صفحه‌ی کاربری واقعی | **بدون تصمیم - نیازمند تأیید مالک محصول که آیا Dev-only است یا باید حذف/متصل شود** |

**نتیجه:** برخلاف فرض اولیه‌ی رایج («۷ کنترلر بیرون از سایدبار = ۷ گپ»)، ۱۱ مورد از ۱۲ مورد
بررسی‌شده رفتار **صحیح** معماری اطلاعات دارند (یا Global Nav هستند، یا Resource-Level Action
درون یک صفحه‌ی دیگر). تنها **یک شکاف واقعی و بدون‌ابهام** پیدا شد: `ShiftHandoverController`.

---

## ۲. درخت ناوبری فعلی (Current Navigation Tree)

```
PEMS Portal
├── [Global Nav — Topbar، در همه‌ی صفحات ثابت]
│   ├── Brand/Home
│   ├── Global Search box  → GlobalSearchController (سیستم اول، جدا از SearchApiController)
│   ├── Theme dropdown (5 گزینه)
│   ├── Language dropdown (en/fa)
│   ├── Notifications bell (SignalR زنده)
│   └── User avatar menu → My Profile · Manage Passkeys · Logout
│
├── [Service Nav — Sidebar Accordion، دائمی سمت چپ/راست]
│   ├── Dashboard (بدون بخش، تکی)
│   ├── Quick Setup (بدون بخش، مجوزمحور - Setup/View)
│   ├── Engineering Management (۱۴ آیتم)
│   ├── AI & Knowledge (۶ آیتم)
│   ├── Correspondence & Doc Control (۳ آیتم، + یک زیرحالت "My Referrals")
│   ├── Quality Control (۲ آیتم)
│   ├── Contract Management (۲ آیتم + ۲ Coming Soon)
│   ├── Project Controls (۴ آیتم)
│   ├── CMMS Integration (۲۵ آیتم)
│   ├── Inventory & Materials (۶ آیتم)
│   └── Administration (۱ آیتم مستقیم + ۳ زیربخش: User & Access / Organization / AI
│       Administration / System Monitoring)
│
└── [Resource Nav — درون هر صفحه‌ی Details/Index]
    ├── Breadcrumb: Dashboard / {Section} / {Title}  ← فقط ۲ سطح، وسط غیرقابل‌کلیک
    ├── الگوی EDMS-Family (EngineeringDocuments، VDR): Tabs + Command Bar
    └── الگوی بقیه (CMMS/Vendor/...): کارت‌های عمودی پشت‌سرهم (بدون Tab)
```

هر ۱۰ بخش سایدبار با `MenuAccess.CanSee(User, MenuAccess.<Section>)` گیت می‌شوند (نقش‌محور،
ایستا) - این جدا از لایه‌ی مجوز واقعی سمت سرور (`IPermissionService`) است که فقط نمایش/عدم‌نمایش
لینک را کنترل می‌کند، نه دسترسی واقعی (طبق یافته‌ی فاز ۲A، هنوز هم درست است).

---

## ۳. ناوبری سراسری در برابر ناوبری سرویس در برابر ناوبری منبع

این تفکیک (الهام از مدل Azure Portal: Global Nav / Service Menu / Resource Menu) **از قبل به‌صورت
ضمنی در PEMS پیاده‌سازی شده**، فقط هرگز به این زبان نام‌گذاری نشده بود:

| لایه | در PEMS چیست | ثبات | وضعیت |
|---|---|---|---|
| **Global Navigation** | Topbar (`_Layout.cshtml`) - در همه‌ی صفحات، همیشه یکسان | کاملاً ثابت | **بالغ.** تنها نقص: بدون Help entry point (تأیید مجدد - هیچ آیکون/دراپ‌داون Help در ۵ آیتم فعلی Topbar نیست) |
| **Service Navigation** | Sidebar Accordion (`_SidebarMenu.cshtml`) - فهرست ماژول‌های هر دامنه | نیمه‌ثابت (باز/بسته‌شدن Accordion بر اساس مسیر جاری، نه انتخاب کاربر) | **بالغ ولی غیرقابل‌شخصی‌سازی.** بدون Favorites/Recent (طبق ممیزی فاز ۱، هنوز باز) |
| **Resource Navigation** | Breadcrumb + (گاهی) Tabs درون یک صفحه‌ی Details خاص | **ناهمگون** - فقط ماژول‌های خانواده‌ی EDMS این را دارند | **دوپاره.** جزئیات در بخش‌های ۴/۵ |

**یافته‌ی کلیدی که در ممیزی فاز ۱ به این عمق نرسیده بود:** مرز بین «صفحاتی که Tab/Command-Bar
دارند» و «صفحاتی که ندارند» **دقیقاً با مرز خانواده‌ی ماژولی EDMS همخوانی دارد، نه با
اندازه/تازگی صفحه.** `VDRController` و `EngineeringDocumentsController` هر دو از یک معماری
گردش‌کار مشترک ارث می‌برند (`VdrWorkflowService` در کامنت خودش می‌گوید «دقیقاً همان معماری
DocumentWorkflowService») و هر دو صفحه‌ی Details‌شان Tab دارند (`VDR/Details.cshtml`: ۶ رخداد
`nav-tabs`/`data-bs-toggle="tab"`؛ `EngineeringDocuments/Details.cshtml`: طبق ممیزی فاز ۱).
در مقابل، صفحات CMMS اصلی (`Permit/Details.cshtml`: ۷۹۰ خط/۹ کارت، `Equipment/Details.cshtml`:
۷۱۹ خط/۱۹ کارت، `WorkOrder/Details.cshtml`: ۶۱۴ خط/۱۰ کارت) **هیچ Tab ندارند** - هرکدام یک
صفحه‌ی طولانی از کارت‌های عمودی پشت‌سرهم است که فقط با اسکرول دیده می‌شود.

---

## ۴. معماری اطلاعات مدارک مهندسی (Engineering Documents IA)

طبق بخش I ممیزی فاز ۱ (که این‌جا تکرار نمی‌شود)، `EngineeringDocuments/Details.cshtml`
(۱۳۴۷ خط) پخته‌ترین صفحه‌ی کل پروژه از نظر IA است:

- **Resource Menu واقعی:** ۶ Tab (Overview / Attachments & AI / IDC Review / Revisions /
  Workflow History / Correspondence) - هرکدام یک زیرمجموعه‌ی معنادار از اطلاعات یک مدرک، نه
  یک تقسیم‌بندی دلبخواهی.
  - **افزوده‌شده در فاز ۲A همین جلسه:** یک بخش «Related Transmittals» داخل همان Tab
    Correspondence (نه یک Tab جدا) - طبق `ITransmittalService.GetForDocumentAsync` و
    `TransmittalRelatedDocumentQueryTests`.
- **Command Bar واقعی:** اکشن‌های اصلی بر اساس `Model.Status` به‌صورت پویا رندر می‌شوند
  (if/else if)، اکشن‌های کم‌تکرار در یک دراپ‌داون «More ▼» جمع شده‌اند.
- **مسیر تصمیم‌گیری فاز ۲A:** استخراج این Command Bar به یک Partial/TagHelper عمومی، به‌دلیل
  نبود هیچ الگوی TagHelper-با-Child-Content در کل کدبیس، **STOP** شد (ریسک واقعی رگرسیون در
  برابر منفعت نامشخص). این تصمیم هنوز پابرجاست و این فاز آن را دوباره باز نکرد.
- **نقطه‌ی ضعف:** هیچ‌کدام از این اجزا (Tab Strip، Command Bar، Timeline) به یک Partial
  مستقل قابل‌استفاده‌ی مجدد تبدیل نشده‌اند - همه Markup درون همین یک فایل ۱۳۴۷ خطی زندگی
  می‌کنند. اگر فاز آینده‌ای بخواهد الگوی Tab را به CMMS هم بیاورد (بخش ۵)، باید این استخراج را
  اول انجام دهد.

## ۵. معماری اطلاعات CMMS (CMMS IA)

بخش CMMS Integration بزرگ‌ترین بخش سایدبار (۲۵ آیتم) است اما از نظر عمق IA، **یک نسل عقب‌تر**
از EDMS/VDR است:

- **بدون Resource Menu:** هیچ صفحه‌ی Details در این بخش (Equipment، WorkOrder، Permit، Asset،
  MaintenancePlan، ...) از الگوی Tab استفاده نمی‌کند. همه یک صفحه‌ی طولانی کارت‌به‌کارت‌اند
  که کاربر باید اسکرول کند تا به بخش موردنظرش برسد (مثلاً در `Equipment/Details.cshtml` با
  ۱۹ کارت، رسیدن به «Maintenance Intelligence» در انتهای صفحه ممکن است چند اسکرول طول بکشد).
- **بدون Command Bar پویا:** اکشن‌های تغییردهنده (مثل `AssignFoundation` که در این جلسه گیت
  مجوزی گرفت) به‌صورت دکمه‌های پراکنده داخل کارت‌های مربوطه‌اند، نه یک نوار اکشن مرکزی
  بالای صفحه.
- **زنجیره‌ی Permit یک استثنای جالب توجه است:** `Permit/Details.cshtml` عملاً میزبان چهار
  زیرگردش‌کار مجزا (`ExecutionGate`، `GasTest`، `Isolation`، `Simops`) است که هرکدام کنترلر
  خودشان را دارند اما هیچ Tab‌ای آن‌ها را از هم جدا نمی‌کند - همه در همان ۹ کارت عمودی جا
  شده‌اند. این نامزد طبیعی اولین ماژول CMMS برای الگوی Tab است، دقیقاً به‌همان‌دلیلی که
  Engineering Documents به آن نیاز داشت (طبق کامنت خودِ Details.cshtml: «قبلاً یک صفحه‌ی طولانی
  اسکرول‌شونده بود»).
- **نقطه‌ی قوت واقعی CMMS که EDMS ندارد:** تعداد صفحات Dashboard اختصاصی بیشتر
  (`CmmsOperations/Dashboard`، `Reliability/Dashboard`، `CriticalityAssessment/Dashboard`،
  `Kpi/Index`) - یعنی از نظر Analytics/Aggregation IA، CMMS جلوتر است؛ فقط از نظر
  Single-Record IA عقب‌تر است.

**نتیجه‌ی مقایسه‌ای:** این دو حوزه از دو نسل طراحی متفاوت‌اند، نه یک تصمیم آگاهانه‌ی معماری. اگر
فاز ۲C بخواهد IA را یکدست کند، جهت درست «آوردن الگوی EDMS/VDR به CMMS» است، نه برعکس - چون
Tab/Command Bar از قبل اثبات‌شده و در حال کارکردن است.

---

## ۶. قابلیت‌های فعلی Help

**کاملاً غایب** - این یافته‌ی ممیزی فاز ۱ (بخش H) در این فاز با بررسی مستقل دوباره تأیید شد:

- بدون `HelpController`، بدون `Views/Help/`، بدون هیچ Entity به‌نام Help/FAQ/Article.
- بدون هیچ آیکون/دراپ‌داون Help در Topbar (تأیید مستقیم با خواندن کامل `_Layout.cshtml` در
  همین فاز - فقط ۵ آیتم موجودند: Theme، Language، Notifications، User Menu، و جعبه‌ی
  جست‌وجو؛ هیچ‌کدام Help نیستند).
- **ریسک نام‌گذاری تازه‌کشف‌شده:** `KnowledgeBaseController` (زیر بخش «AI & Knowledge»)
  می‌تواند به‌اشتباه به‌جای Help تصور شود، اما در واقع یک **جست‌وجوی معنایی روی مدارک مهندسی
  با Embedding محلی** است (طبق کامنت خودش: «Semantic Search واقعی روی Knowledge Base... طبق
  AI Architecture Volume VIII»)، نه یک مخزن مقالات راهنمای کاربر. هر طراحی آینده‌ی Help باید
  از این دو نام به‌وضوح فاصله بگیرد (پیشنهاد نام‌گذاری در سند طراحی Help).
- سند مرجع طراحی (`.github/Role/PEMS — Enterprise UIUX Design Baseline.md`) فقط دو اشاره‌ی
  تک‌خطی به «Help» (خط ۲۷۰، به‌عنوان یکی از ۹ آیتم موردنیاز Topbar) و «Tooltip» (خط ۴۰۰، در
  فهرست Core Components) دارد - **هیچ مشخصات، مدل داده، یا رفتاری تعریف نشده.** یعنی طراحی
  Help واقعاً Greenfield است، نه چیزی که فقط باید طبق یک سند موجود پیاده‌سازی شود.

## ۷. الگوی فعلی Tooltip (به‌عنوان زیرساخت آماده برای استفاده‌ی مجدد)

یک مکانیزم عمومی و Idempotent از قبل وجود دارد:

```js
// wwwroot/js/site.js
window.PemsTooltips = {
    init: function (root) {
        var scope = root || document;
        scope.querySelectorAll('[data-bs-toggle="tooltip"]').forEach(function (el) {
            if (!bootstrap.Tooltip.getInstance(el)) new bootstrap.Tooltip(el);
        });
    }
};
```

- روی `DOMContentLoaded` خودکار صدا زده می‌شود؛ هر صفحه‌ای که بعداً محتوای پویا اضافه می‌کند
  (مثلاً بعد از باز شدن یک Modal) می‌تواند دوباره `window.PemsTooltips.init(container)` را صدا
  بزند بدون خطر Double-Init.
- امروز فقط برای برچسب‌های کوتاه دسترسی‌پذیری استفاده می‌شود (`title="Edit"`,
  `title="SHA-256: ..."`) - **هرگز برای محتوای راهنمای توضیحی.** این دقیقاً همان مرز روشنی
  است که سند طراحی Help باید به‌صراحت حفظ کند (بخش «Tooltip در برابر Help Article» در سند
  دوم).
- **EXISTING — REUSE مستقیم:** هر Tooltip کوتاه در معماری Help آینده باید از همین
  `window.PemsTooltips` استفاده کند، نه یک کتابخانه‌ی جدید.

## ۸. مدل دوزبانگی فعلی (Bilingual Model)

- `Program.cs` (خط ۷۴۰-۷۶۴): `RequestLocalizationOptions` با دقیقاً دو فرهنگ پشتیبانی‌شده
  (`en`, `fa`)، پیش‌فرض `en`، منبع تشخیص فقط **Cookie** (`CookieRequestCultureProvider`) -
  بدون `Accept-Language` Header، بدون تشخیص خودکار.
- **تصمیم عمدی و صحیح که باید حفظ شود:** تقویم فرهنگ `fa` صریحاً به میلادی Override شده
  (`faCulture.DateTimeFormat.Calendar = new GregorianCalendar()`) - طبق کامنت خودِ کد، این
  از تبدیل خاموش Timestampهای AuditLog/نام فایل‌های Export به تقویم شمسی جلوگیری می‌کند. **هر
  محتوای Help که تاریخ نمایش می‌دهد باید از همین قرارداد پیروی کند.**
  - **این محدودیت به‌روشنی در حافظه‌ی این نشست هم ثبت شده بود** (طبق سابقه‌ی این پروژه) و در
    این ممیزی مستقلاً هم با خواندن مستقیم کد تأیید شد.
- `SharedResource.resx`/`SharedResource.fa.resx` (طبق ممیزی فاز ۱: ۹۴ ردیف هرکدام، تناظر
  کامل EN/FA) - این یک فایل **ترجمه‌ی برچسب رابط کاربری** است (کلیدهایی مثل `"Dashboard"`,
  `"Search"`), نه یک مخزن محتوای بلند. فقط ۴ ردیف بلندتر از یک عبارت‌اند و آن‌ها هم زیرنویس
  KPI هستند، نه محتوای راهنما. **این یعنی محتوای Help (که ذاتاً پاراگرافی و طولانی است) نباید
  در این فایل‌های `.resx` جا داده شود** - نیاز به مدل ذخیره‌سازی جداگانه دارد (بخش مربوطه در
  سند طراحی Help).

## ۹. رفتار RTL/LTR فعلی

- `_Layout.cshtml` خط ۱۱ و ۲۳: `dir` مستقیماً از `CultureInfo.CurrentUICulture` مشتق می‌شود
  (`fa` → `rtl`, در غیر این صورت `ltr`) - روی خودِ تگ `<html>`، یعنی کل صفحه (نه فقط بخشی از
  آن) جهت می‌گیرد؛ Flexbox/Grid به‌صورت خودکار Flip می‌شوند.
- بارگذاری شرطی Bootstrap: نسخه‌ی `bootstrap.rtl.min.css` وقتی `isRtl` باشد، وگرنه نسخه‌ی
  معمولی - به‌همراه یک برگه‌ی سبک اضافه‌ی RTL-only (`admin.rtl.css`) فقط برای استثناهای
  جهتی واقعی.
- **یافته‌ی فاز ۱ که هنوز باز است:** `cmms-rtl.css` وجود دارد ولی هرگز `<link>` نمی‌شود -
  کد مرده. این فاز آن را دوباره تأیید کرد اما اصلاح نکرد (خارج از دامنه).
- **نتیجه برای Help:** هر کامپوننت جدید Help (پنل/Modal/Drawer) باید فقط از خاصیت‌های CSS
  منطقی (`margin-inline-start` به‌جای `margin-left` و مشابه) استفاده کند تا بدون نیاز به
  برگه‌ی RTL اختصاصی جدید، هم در `fa` هم در `en` درست کار کند - دقیقاً همان جهتی که بقیه‌ی
  پروژه (به‌جز چند استثنای قدیمی) از قبل می‌رود.

## ۱۰. الگوهای قابل‌استفاده‌ی مجدد و تکراری (تکمیلی بر بخش M/N ممیزی فاز ۱)

علاوه بر فهرست کامل بخش M/N ممیزی فاز ۱ (که این‌جا تکرار نمی‌شود)، این فاز موارد زیر را
به‌طور خاص برای طراحی Help/IA آینده مرتبط تشخیص داد:

| مورد | وضعیت | ربط به Help/IA |
|---|---|---|
| `window.PemsTooltips` (site.js) | آماده، عمومی | پایه‌ی مستقیم Tooltip در معماری Help |
| `_Breadcrumb.cshtml` | آماده ولی کم‌عمق (۲ سطح) | باید قبل از افزودن Resource Menu واقعی به CMMS، عمیق‌تر شود |
| `ISavedFilterService`/`SavedFilter` (فقط در `DocumentRegisterController` وصل شده) | آماده، عمومی‌طراحی‌شده، کم‌استفاده | نزدیک‌ترین الگوی موجود به یک «UserPreference» - نمونه‌ی خوبی برای الگوی پیاده‌سازی آینده |
| `UserDashboardWidget`/`IUserDashboardService` | آماده، فقط Layout ذخیره می‌کند | نزدیک‌ترین الگوی موجود به «Favorites» (از ۸ Widget، کاربر انتخاب می‌کند کدام دیده شود) |
| `Notification`/`NotificationHub` (SignalR) | آماده، کاملاً کارکردن | زیرساخت مناسب برای اعلان «مقاله‌ی راهنمای جدید منتشر شد»، بدون نیاز به کانال جدید |
| Tab-Strip موجود در `EngineeringDocuments/Details.cshtml` و `VDR/Details.cshtml` | آماده ولی استخراج‌نشده | اگر Help به‌صورت یک Tab کنار Overview طراحی شود (نه فقط یک Drawer)، این الگو باید اول استخراج شود |

---

## ۱۱. خلاصه‌ی نهایی (طبق ساختار درخواستی)

**چه چیزی بررسی شد:**
تمام ۸۶ کنترلر `PEMS.Web` (فهرست کامل + نگاشت به سایدبار)؛ `_SidebarMenu.cshtml` به‌طور
کامل (۵۵۳ خط)؛ `_Layout.cshtml` به‌طور کامل (۲۶۵ خط)؛ `_Breadcrumb.cshtml`؛ تنظیمات
Localization در `Program.cs`؛ `window.PemsTooltips` در `site.js`؛ ساختار صفحات Details برای
`EngineeringDocuments`، `VDR`، `Permit`، `Equipment`، `WorkOrder` (شمارش خط و کارت/Tab)؛
`KnowledgeBaseController`؛ کنترلرهای بدون ورودی سایدبار (`ExecutionGate`, `GasTest`,
`Isolation`, `Simops`, `ShiftHandover`, `AI`, و ۵ مورد Global-Nav-صحیح)؛ سند
`.github/Role/PEMS — Enterprise UIUX Design Baseline.md` (بخش‌های Help/Tooltip/Sidebar).

**چه چیزی از قبل وجود دارد:**
یک تفکیک ضمنی و کارکننده‌ی Global/Service/Resource Navigation؛ یک الگوی Tab+Command-Bar بالغ
در خانواده‌ی EDMS (EngineeringDocuments + VDR)؛ یک زیرساخت Tooltip عمومی و آماده؛ یک مدل
دوزبانگی صحیح و آگاهانه (با محافظت درست از تقویم)؛ یک استراتژی RTL/LTR صحیح در سطح `<html>`؛
چند الگوی نزدیک به Preference/Favorites (`SavedFilter`, `UserDashboardWidget`) که فقط
کم‌استفاده‌اند، نه خراب.

**چه چیزی غایب است:**
هر نوع سیستم Help/مقاله‌ی راهنما (کاملاً Greenfield)؛ الگوی Tab/Command-Bar در کل CMMS
(۲۵ آیتم سایدبار)؛ یک ورودی Help در Topbar؛ یک لینک سایدبار برای `ShiftHandoverController`؛
یک Resource Menu عمیق‌تر از ۲ سطح breadcrumb؛ (تکراری از فاز ۱: Favorites عمومی، Recent
Items، `UserPreference`، ادغام دو سیستم Search).

**چه چیزی قابل‌استفاده‌ی مجدد است:**
`window.PemsTooltips`، ساختار Tab-Strip خانواده‌ی EDMS (پس از استخراج به Partial)، `Notification`
pipeline (برای اطلاع‌رسانی محتوای Help جدید)، `SavedFilter`/`UserDashboardWidget` به‌عنوان الگوی
اثبات‌شده‌ی «داده‌ی per-user، حداقلی، بدون درهم‌تنیدگی با Workflow».

**چه چیزی باید گسترش یابد:**
`_Breadcrumb.cshtml` به یک Resource Menu واقعی؛ افزودن لینک سایدبار برای ShiftHandover
(تغییر تک‌خطی، بدون ریسک)؛ استخراج Tab-Strip EDMS به یک Partial مشترک تا CMMS هم بتواند از آن
استفاده کند (پیش‌نیاز فاز ۲C، نه این فاز).

**چه چیزی باید موکول شود:**
هرگونه پیاده‌سازی واقعی Help/Favorites/Recent/UserPreference (نیازمند Entity جدید - طبق
محدودیت صریح این فاز)؛ یکسان‌سازی IA در CMMS (کار بزرگ، فاز جدا).

**چه چیزی نیازمند تغییر Schema در آینده است:**
`HelpTopic` (یا معادل)، `UserFavorite`/`RecentItem` تعمیم‌یافته از `LetterStar`، و
`UserPreference` - هرسه در ممیزی فاز ۱ (بخش P) هم به همین شکل «STOP و منتظر تأیید» علامت
خورده بودند و این فاز آن تصمیم را تغییر نمی‌دهد؛ سند طراحی Help (سند دوم) دقیقاً شکل پیشنهادی
`HelpTopic` را مشخص می‌کند، اما پیاده‌سازی نمی‌کند.

**فاز ۲C باید چه کاری انجام دهد:**
جزئیات کامل در `docs/design/PEMS_PORTAL_INFORMATION_ARCHITECTURE.md`، بخش «اولویت پیاده‌سازی».
خلاصه: (۱) رفع تک‌خطی لینک ShiftHandover، (۲) تصمیم مالک محصول درباره‌ی تأیید مدل `HelpTopic`
پیشنهادی، (۳) در صورت تأیید، ساخت Entity + Seed محتوای اولیه برای حداقل یک ماژول پایلوت
(پیشنهاد: Engineering Documents، چون از قبل پخته‌ترین IA را دارد)، (۴) استخراج Tab-Strip EDMS
به Partial به‌عنوان پیش‌نیاز فنی گسترش IA به CMMS.
