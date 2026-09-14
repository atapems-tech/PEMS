# PEMS Shared Resource Workspace — Conceptual Design Specification

**تاریخ:** 2026-09-13
**وضعیت:** Design-only. هیچ Partial/ViewComponent/TagHelper/Entity ساخته نشد. این سند
مستقیماً روی شواهد `docs/audits/PEMS_PHASE2C2_SHARED_RESOURCE_WORKSPACE_AUDIT.md` بنا شده
است - همان یافته‌ها این‌جا تکرار نمی‌شوند مگر برای تعریف دقیق یک قرارداد.

**اصل راهنما (طبق نتیجه‌ی ممیزی، بخش ۲۰/۲۱):** این «Workspace» یک **قرارداد بصری/نام‌گذاری
+ دو واحد کوچک قابل‌استفاده‌ی مجدد** است، نه یک Component رفتاری واحد که همه‌چیز را کنترل
کند. هر بخش زیر صریحاً می‌گوید آیا یک Partial واقعی است یا فقط یک قرارداد ساختاری که هر View
باید دستی رعایت کند.

---

## ۱. آناتومی Workspace

```text
Resource Page (یک View کامل - مثل امروز، بدون تغییر ساختار فایل)
│
├── _Breadcrumb.cshtml (مشترک، از قبل موجود - فقط استفاده‌ی صحیح، بدون بریدکرامب دوم)
│
├── PemsResourceHeader (Partial جدید - بخش ۲)
│   ├── شناسه/عنوان
│   ├── وضعیت (Badge اصلی)
│   ├── Badgeهای شرطی
│   └── خط متادیتای خلاصه
│
├── Command Area (قرارداد چیدمان فقط - بخش ۳؛ بدون Component رفتاری)
│
├── PemsStageDiagram (Partial تعمیم‌یافته، اختیاری - فقط اگر منبع یک گردش‌کار خطی واحد دارد)
│
├── Resource Navigation (قرارداد Tab/Section/Anchor - بخش ۴؛ بدون Component اجباری)
│
├── Content Region (محتوای دامنه‌محور - بدون تغییر، مالکیت کامل با هر ماژول)
│
└── Optional Panels (Attachments/History/Audit/Related - بخش ۶؛ Lazy/Conditional)
```

## ۲. قرارداد Header (`PemsResourceHeader`)

**نوع:** Partial View واقعی (`Views/Shared/_PemsResourceHeader.cshtml`) - **نه** ViewComponent
یا TagHelper، طبق محدودیت معماری تأییدشده در ممیزی (بخش ۵/۱۹: صفر ViewComponent/TagHelper
در کل پروژه؛ معرفی اولین نمونه ریسک/هزینه‌ی یادگیری غیرضروری برای این واحد کوچک دارد).

### مدل ورودی (کلاس C# ساده، نه Entity/DTO دامنه)

```csharp
public class PemsResourceHeaderModel
{
    public string Identifier { get; set; } = "";       // مثلاً DocumentNo، PermitNumber - همیشه dir="ltr"
    public string Title { get; set; } = "";             // نام/عنوان خوانا
    public string StatusText { get; set; } = "";        // متن دقیق وضعیت (هرگز فقط رنگ)
    public PemsStatusTone StatusTone { get; set; }       // enum - بخش ۲.۱
    public string? MetaLine { get; set; }                // خط خلاصه (مثلاً "Project · Discipline · Area")
    public List<PemsHeaderBadge> ConditionalBadges { get; set; } = new();
    public string? IconCssClass { get; set; }            // اختیاری - آیکون بزرگ سمت چپ/راست (الگوی Equipment/Permit)
}

public class PemsHeaderBadge
{
    public string Text { get; set; } = "";
    public PemsStatusTone Tone { get; set; }
    public string? IconCssClass { get; set; }
}
```

### ۲.۱ `PemsStatusTone` — حل تعارض دو استراتژی موجود

طبق یافته‌ی ممیزی (بخش ۵): امروز دو استراتژی ناسازگار وجود دارد - `DocumentStatusBadge`
(کلاس CSS اختصاصی به‌ازای هر رشته‌ی وضعیت) در برابر VDR/WorkOrder/Permit/Equipment (نگاشت
درون‌خطی به `bg-*` بوت‌استرپ). **تصمیم این سند: `PemsStatusTone` یک enum کوچک و ثابت است
که مستقیم به کلاس‌های آماده‌ی بوت‌استرپ نگاشت می‌شود - همان استراتژی‌ای که ۴ از ۵ منبع
بررسی‌شده از قبل به‌صورت مستقل انتخاب کرده‌اند:**

```csharp
public enum PemsStatusTone { Neutral, Info, Success, Warning, Danger }

// نگاشت داخل خودِ Partial (یک‌بار، نه در هر View):
// Neutral -> bg-secondary | Info -> bg-info text-dark | Success -> bg-success
// Warning -> bg-warning text-dark | Danger -> bg-danger
```

**پیامد صریح:** `DocumentStatusBadge.CssClass` (EDMS) و `LetterStatusBadge` نباید موتور
جدید را جایگزین اجباری خود کنند - این‌ها به‌عنوان لایه‌ی نگاشت *همان ماژول* باقی می‌مانند
(هرکدام رشته‌ی Status خودشان را به `PemsStatusTone` ترجمه می‌کنند، نه مستقیماً به کلاس CSS)؛
مهاجرت واقعی CSS اختصاصی `badge-status-*` به این مدل، طبق ممیزی (بخش ۲۱/۲۲)، موکول به یک
نوبت جدا با بررسی مصرف‌کننده‌های موجود آن کلاس‌هاست - **این سند فقط قرارداد ورودی/خروجی
Partial جدید را تعریف می‌کند، نه مهاجرت داده‌ی موجود.**

### ۲.۲ فیلدهای همیشه/شرطی/فقط-جزئیات (طبق بخش ۹ ممیزی)

| فیلد | نمایش |
|---|---|
| Identifier، StatusText/Tone، MetaLine | همیشه |
| ConditionalBadges | فقط وقتی لیست غیرخالی است |
| فیلدهای فرعی (Manufacturer/Model و مشابه) | هرگز در Header - همیشه در Content Region |

### ۲.۳ مرز مجوز
**صفر.** این Partial هیچ `IPermissionService`/`ICurrentUserService` را تزریق نمی‌کند. تمام
مقادیر (ازجمله این‌که یک Badge اصلاً نمایش داده شود یا نه) از قبل توسط Controller/View
فراخوان محاسبه و به مدل بالا داده می‌شود - دقیقاً همان الگوی موجود `ViewBag.CanFreeze` که
ممیزی آن را تأیید کرد.

### ۲.۴ دوزبانگی/RTL
- `Identifier` همیشه با `dir="ltr"` صریح رندر می‌شود، **مستقل از جهت کلی صفحه** - طبق
  یافته‌ی جدید ممیزی (بخش ۱۶) که هیچ صفحه‌ی موجود این را رعایت نمی‌کند.
- `Title`/`MetaLine`/`StatusText` بدون `dir` اجباری رندر می‌شوند (از جهت والد ارث می‌برند)
  - چون این‌ها متن طبیعی زبان هستند، نه شناسه.
- تمام Spacing با کلاس‌های منطقی بوت‌استرپ (`gap-*`, `ms-auto`/`me-auto` به‌جای
  `margin-left`/`margin-right` خام) - بدون CSS اختصاصی جدید.

### ۲.۵ واکنش‌گرا
از همان `d-flex flex-wrap gap-2/gap-3` که هر پنج نمونه‌ی بررسی‌شده از قبل استفاده می‌کنند -
بدون Breakpoint جدید.

### ۲.۶ قابل‌استفاده‌ی مجدد؟
**بله - Ready for Implementation** (طبق بخش ۲۱ ممیزی).

---

## ۳. قرارداد Command Area (بدون Component رفتاری)

طبق یافته‌ی صریح ممیزی (بخش ۷): **هیچ Command Bar عمومی ساخته نمی‌شود.** به‌جایش، یک
قرارداد چیدمان/CSS مشترک تعریف می‌شود که هر View همچنان دستی اکشن‌های خودش را می‌نویسد:

- کلاس ظرف مشترک: `pems-resource-actions` (کلاس CSS جدید، فقط چیدمان - `d-flex
  align-items-center gap-2 flex-wrap`، هم‌ارز با آنچه EDMS/Permit امروز دستی می‌نویسند).
- **قاعده‌ی محل قرارگیری (نه محتوا):** اگر منبع یک زنجیره‌ی Status خطی ساده دارد (مثل EDMS)،
  Command Area داخل Header قرار می‌گیرد. اگر منبع چند زیرگردش‌کار مستقل دارد (مثل Permit)،
  Command Area **در هر Section مربوطه** تکرار می‌شود، نه در Header. این تصمیم به‌ازای هر
  منبع توسط توسعه‌دهنده‌ی همان View گرفته می‌شود - **این سند این تصمیم را استاندارد
  نمی‌کند، فقط نامش را می‌گذارد تا در آینده قابل‌جست‌وجو باشد.**
- **قاعده‌ی Confirm (رفع ناهماهنگی بخش ۵/۱۰/۱۷ ممیزی):** برای اکشن‌های **بازگشت‌پذیر و
  کم‌ریسک** (Unfreeze، لغو یک فرم پیش‌نویس) از `onclick="return confirm(...)"` بومی استفاده
  شود (ساده‌تر، Accessibility پیش‌فرض بهتر). برای اکشن‌هایی که **فیلد ورودی اضافه نیاز
  دارند** (Reason/Comment/تاریخ) از Bootstrap Modal استفاده شود (چون `confirm()` بومی
  نمی‌تواند فرم نشان دهد). این قاعده صرفاً *مستندسازی* الگوی موجود است (هردو الگو در
  کدبیس زنده‌اند)، نه اختراع یک مکانیزم سوم.

## ۴. قرارداد Resource Navigation

طبق بخش ۸ ممیزی، سه الگوی اثبات‌شده وجود دارد؛ این سند هرکدام را رسمیت می‌بخشد، **هیچ‌کدام
را اجباری نمی‌کند:**

| الگو | معیار انتخاب (طبق ممیزی) | پیاده‌سازی |
|---|---|---|
| **Tab** | محتوا حجیم + کاربر معمولاً فقط یک زیربخش را در هر بازدید می‌خواهد | Bootstrap `nav-tabs`/`tab-pane` دستی (همان امروز - بدون تغییر) |
| **Section** | محتوای کوتاه، کاربر همه را با هم می‌خواهد | `content-card` پشت‌سرهم (همان امروز) |
| **Action-Items Anchor** (الگوی Permit، تعمیم‌پذیر) | صفحه‌ی بلند با چند زیرگردش‌کار مستقل که هرکدام گاهی نیاز به توجه فوری دارند | یک بلوک کوچک بالای صفحه با `<a href="#anchor-id">` به بخش مربوطه - از داده‌ی همان `ViewBag` موجود، **بدون Query جدید** (دقیقاً الگوی موجود Permit، فقط قابل کپی به VDR/EDMS در آینده) |

**محدودیت صریح که نباید حل شود در این فاز:** هیچ Tab در PEMS امروز Deep-Link (مسیر-محور)
نیست (بخش ۸ ممیزی). این سند یک راه‌حل Deep-Link پیشنهاد نمی‌دهد چون نیازمند تغییر
Route/JavaScript در سطحی فراتر از دامنه‌ی این فاز است - **موکول به فاز ۲C-3 در صورت تأیید
اولویت آن.**

## ۵. قرارداد Content Region

**بدون تغییر.** این ناحیه ۱۰۰٪ مالکیت هر ماژول است (طبق بخش ۱۴ ممیزی/دستور اصلی). هیچ
قرارداد مشترکی روی محتوای دامنه‌محور (فیلدهای ISO 14224 در WorkOrder، Eligibility در Permit،
Discipline Review در VDR) تحمیل نمی‌شود.

## ۶. پنل‌های اختیاری مشترک (Attachments/History/Audit/Related)

**تصمیم این سند: این‌ها Component مشترک نمی‌شوند، فقط یک قاعده‌ی بارگذاری مشترک می‌گیرند
(طبق ریسک عملکردی بخش ۱۸ ممیزی):**

- هر پنل اختیاری باید **Lazy/Conditional** باشد: یا داخل یک Tab که فقط با کلیک کاربر محتوایش
  دیده می‌شود (داده از قبل توسط Controller آماده شده - بدون Query اضافه در Client، دقیقاً
  مثل امروز)، یا در Controller فقط وقتی مقدار مرتبط غیرخالی/معتبر است بارگذاری شود (مثل
  الگوی موجود `ViewBag.PIDDocument` در Equipment که فقط اگر پیوند واقعی وجود داشته باشد
  Query می‌زند).
- **هرگز** یک "بارگذاری خودکار همه‌چیز" برای هر پنل روی هر صفحه - این دقیقاً همان ضدالگوی
  رد‌شده در بخش ۲۰ ممیزی است.
- `PemsStageDiagram` (بخش ۷) تنها پنل با Partial واقعی مشترک است؛ Attachments/History/Audit
  همچنان Markup اختصاصی هر ماژول می‌مانند (چون شکل داده‌شان - `VDRSubmissionDto` در برابر
  `WorkflowHistoryDto` در برابر ISO 14224 فیلدها - کاملاً متفاوت است؛ اجبار به یک شکل مشترک
  دقیقاً ریسک «ViewModel واحد برای همه» در بخش ۲۰ ممیزی است).

## ۷. `PemsStageDiagram` — تعمیم سه پیاده‌سازی موجود

**نوع:** Partial View (`Views/Shared/_PemsStageDiagram.cshtml`) - جایگزین سه فایل موجود
(`_WorkflowStagesDiagram`, `_VdrWorkflowStagesDiagram`, `_WorkOrderStagesDiagram`) **در
پیاده‌سازی آینده**، نه در این فاز.

```csharp
public class PemsStageDiagramModel
{
    public List<PemsStageItem> Stages { get; set; } = new();
    public int CurrentIndex { get; set; }
    public bool IsBranchedBack { get; set; }       // معادل "Rejected"/"PendingResubmission"
    public string? BranchedBackMessage { get; set; } // پیام بنر بالای نمودار، در صورت IsBranchedBack
}

public class PemsStageItem
{
    public string Label { get; set; } = "";
    public string IconCssClass { get; set; } = "";
    public string? RoleLabel { get; set; }
}
```

- **مسئولیت:** فقط رندر بصری (حلقه‌ی `for` + CSS مشترک `pems-wf-*` که امروز بین دو نسخه‌ی
  اول تقریباً یکسان است).
- **ورودی:** هر ماژول (EDMS/VDR/WorkOrder) منطق تعیین `Stages`/`CurrentIndex`/
  `IsBranchedBack` خودش را (که کاملاً دامنه‌محور است - مثلاً VDR از `CurrentStageName` موتور
  می‌خواند، EDMS از رشته‌ی `Status`) در `@{ }` بلوک View خودش نگه می‌دارد و فقط نتیجه را به
  این Partial پاس می‌دهد - **صفر تغییر در منطق تعیین وضعیت هر ماژول.**
- **مرز مجوز:** صفر.
- **دوزبانگی:** `Label`/`RoleLabel` رشته‌های از‌قبل‌ترجمه‌شده‌اند (همان‌طور که امروز است -
  هرکدام از سه فایل موجود از قبل رشته‌ی فارسی/انگلیسی مستقیم دارند).
- **قابل‌استفاده‌ی مجدد؟** بله - Ready for Implementation (بخش ۲۱ ممیزی).

---

## ۸. نمونه‌های کاربرد (طبق دستور صریح - حداقل ۵ منبع)

### Engineering Document
```
PemsResourceHeaderModel {
    Identifier = "ENG-PID-0042", Title = "Process Flow Diagram - Unit 200",
    StatusText = "IDC Review", StatusTone = Info,
    MetaLine = "Project X · Process · Unit 200",
    ConditionalBadges = [ {Text="REV 03", Tone=Neutral}, {Text="Frozen", Tone=Info, Icon="bi-snow"} ]
}
// Command Area: در Header (طبق قاعده‌ی بخش ۳ - زنجیره‌ی Status خطی ساده)
// Navigation: Tab (۶ - بدون تغییر)
// PemsStageDiagram: بله (از رشته‌ی Status)
```

### VDR
```
PemsResourceHeaderModel {
    Identifier = "J-85-FSK-ME-DSH-0611-0001-001-0", Title = "Vendor Co. — Pump Datasheet",
    StatusText = "UnderReview", StatusTone = Info,
    MetaLine = "Vendor Co. · Project X",
    ConditionalBadges = [ {Text="Overdue", Tone=Danger} ]
}
// Command Area: داخل Tab «Workflow» می‌ماند (بدون تغییر - طبق قاعده‌ی بخش ۳، این تصمیم
// دامنه‌محور محفوظ می‌ماند، نه جابه‌جایی اجباری به Header)
// Navigation: Tab (۵ - بدون تغییر)
// PemsStageDiagram: بله (از CurrentStageName موتور)
```

### Equipment
```
PemsResourceHeaderModel {
    Identifier = "P-1001", Title = "Centrifugal Pump",
    StatusText = "Active", StatusTone = Success,
    MetaLine = "Pump · Project X · Mechanical · Unit 200",
    ConditionalBadges = [ {Text="Class A", Tone=Danger}, {Text="Cal: Overdue", Tone=Danger, Icon="bi-speedometer2"} ]
}
// Command Area: پخش در Tabها می‌ماند (بدون تغییر - Equipment گردش‌کار خطی واحد ندارد)
// Navigation: Tab (۹ - بدون تغییر)
// PemsStageDiagram: خیر (Not applicable - بدون گردش‌کار خطی واحد، طبق ماتریس ممیزی)
```

### Work Order
```
PemsResourceHeaderModel {
    Identifier = "WO-2026-0088", Title = "Corrective — Vibration on Pump P-1001",
    StatusText = "InProgress", StatusTone = Warning,
    MetaLine = "Corrective · Critical",
    ConditionalBadges = []
}
// Command Area: در Header (یک دکمه‌ی وضعیت‌محور - طبق قاعده‌ی بخش ۳)
// Navigation: بدون Tab (صفحه‌ی بلند - بدون تغییر)
// PemsStageDiagram: بله (از رشته‌ی Status + HasPendingReview)
```

### Permit
```
PemsResourceHeaderModel {
    Identifier = "PTW-2026-0141", Title = "Hot Work — Unit 200 Pipe Rack",
    StatusText = "Active", StatusTone = Success,
    MetaLine = "Hot Work · Requested by ...",
    ConditionalBadges = []
}
// Command Area: پخش در Sectionها می‌ماند (بدون تغییر - ۴ زیرگردش‌کار مستقل)
// Navigation: Action-Items Anchor (الگوی موجود Permit - بدون تغییر، فقط رسمیت‌یافته)
// PemsStageDiagram: خیر (Permit گردش‌کار خطی واحد ندارد - Eligibility-محور است)
```

---

## ۹. قیدهای عملکردی

- `PemsResourceHeader`/`PemsStageDiagram` هرگز به `PEMSDbContext`/هیچ سرویسی وابسته نیستند -
  فقط مدل C# ساده می‌گیرند؛ **صفر Query جدید** با معرفی این دو واحد.
- هیچ پنل اختیاری (بخش ۶) به‌طور پیش‌فرض بارگذاری نمی‌شود.
- بدون فراخوانی AJAX/Fetch جدید برای این دو واحد (هر دو کاملاً Server-Rendered، همسو با
  سبک کل پروژه).

## ۱۰. قواعد توسعه‌پذیری

- هر منبع جدید که می‌خواهد `PemsResourceHeader` را استفاده کند، باید مدل ورودی را از داده‌ی
  از‌قبل‌موجود در Controller/DTO خودش بسازد - **هرگز** `PemsResourceHeader` را با منطق
  تصمیم‌گیری داخلی (مثل خواندن مستقیم Entity) گسترش ندهید.
- `PemsStatusTone` باید همیشه به ۵ مقدار ثابت محدود بماند - اگر یک ماژول ادعا کند به «رنگ
  ششم» نیاز دارد، این یک نشانه است که آن حالت باید `ConditionalBadges` باشد، نه `StatusTone`
  جدید.
- اگر یک منبع آینده نیاز به Command Area/Navigation الگویی داشت که در بخش‌های ۳/۴ نیست
  (مثلاً یک الگوی چهارم ناوبری)، **باید یک تجدیدنظر مستند در این سند باشد، نه یک استثنای
  خاموش در کد.**

## ۱۱. آنچه این سند عمداً حل نمی‌کند (طبق دامنه‌ی صریح فاز)

- استخراج واقعی سه فایل نمودار مراحل موجود به `_PemsStageDiagram.cshtml` واحد.
- مهاجرت `EngineeringDocuments`/`VDR`/`Equipment`/`WorkOrder`/`Permit` به مدل
  `PemsResourceHeaderModel`.
- رفع بریدکرامب دوگانه‌ی EDMS.
- تصمیم محصولی درباره‌ی این‌که آیا الگوی «Action-Items Anchor» به VDR/EDMS تعمیم یابد.
- Deep-Link کردن Tabها.

این‌ها همگی کاندیدای **فاز ۲C-3** هستند، به‌ترتیب اولویت مشخص‌شده در بخش ۲۱ ممیزی.
