# راهنمای پیاده‌سازی ماژول VDR (Vendor Document Register)

## چه چیزی ساخته شد

طبق Development Plan سند معماری، گام‌های ۱ تا ۴ (به‌علاوه نسخه‌ی اول گام ۵) پیاده‌سازی شدند:

**۱) مدل داده (Additive):** `Vendor`, `Contract`, `PurchaseOrder`, `VDRItem`, `VDRSubmission`, `VDRImportBatch`, `VDRImportRow`

**۲) موتور Import اکسل واقعی شما:** فایل `VDR_REPORT_07_04_2026.xlsx` را به‌صورت مستقیم می‌خواند —
ستون‌های ثابت مدرک + بلوک‌های تکراری رویژن (که با تکرار ستون «Vendor Transmittal NO.» شناسایی می‌شوند) را
پارس می‌کند، Vendor/PO/Discipline را در صورت نبودن خودکار می‌سازد یا تطبیق می‌دهد، هر ردیف را Validate و
بررسی تکراری می‌کند، و **هیچ‌چیز را مستقیم ذخیره نمی‌کند** — همه‌چیز ابتدا در `VDRImportBatch`/`VDRImportRow`
(Staging) قرار می‌گیرد تا شما در صفحه‌ی Review تایید کنید (الزام "Approval Before Final Storage").

**۳) گردش کار VDR:** AP/NC → Closed؛ AC/CO/RE → PendingResubmission (طبق تایید شما). وضعیت هر آیتم
به‌صورت زنده از تاریخچه‌ی Submission محاسبه می‌شود، نه یک فیلد دستی.

**۴) Dashboard VDR:** KPI های Total/Closed/Pending Resubmission/Under Review/Overdue/On-Time%، به‌علاوه
جدول تاخیر و ریسک هر Vendor — دقیقاً همان چیزی که در شیت‌های Delays/TOTAL STATUS/Procurement Vendor Delay
فایل اصلی شما به‌صورت دستی نگه‌داری می‌شد.

**۵) امتیازدهی ریسک Vendor (نسخه‌ی اول - Heuristic):** ترکیب وزن‌دار نرخ تاخیر (۴۰٪) + نرخ رد/اصلاح CO/AC/RE
(۴۰٪) + نرخ عدم بسته‌شدن (۲۰٪) → سطح Low/Medium/High. دکمه‌ی «Recalculate Risk Score» در صفحه‌ی هر Vendor.

## صفحات و منوی جدید

- **Engineering Management → Vendor Document Register (VDR)**: لیست + فیلتر + Dashboard
- **Engineering Management → Vendor Document Register → Import Excel**: آپلود و بازبینی
- **Contract Management → Contracts**: مدیریت قرارداد (پایه، برای پشتیبانی از VDR)
- **Contract Management → Vendors**: مدیریت Vendor + امتیاز ریسک

## Migration

```
dotnet ef migrations add AddVdrModule --project PEMS.Persistence --startup-project PEMS.Web
dotnet ef database update --project PEMS.Persistence --startup-project PEMS.Web
```

## نحوه‌ی تست با فایل واقعی

1. یک Project در سیستم بسازید (یا از موجود استفاده کنید).
2. به VDR → Import Excel بروید، پروژه را انتخاب و فایل `VDR_REPORT_07_04_2026.xlsx` را آپلود کنید.
3. در صفحه‌ی Review، تعداد ردیف‌های Valid/Warning/Error و Duplicate را ببینید؛ ردیف‌های Error به‌صورت
   پیش‌فرض از Commit خارج هستند (می‌توانید همه را قبل از Commit بررسی کنید).
4. روی «Commit Selected Rows» بزنید — Vendor ها و PO ها خودکار ساخته می‌شوند و VDR Dashboard پر می‌شود.

## به‌روزرسانی: Comment Management با Action Tracking جدا (طبق سند Enterprise Development Guidelines)

طبق تایید صریح شما، رفتار AC بدون تغییر ماند (مثل CO وارد چرخه‌ی Resubmission می‌شود). آنچه از سند جدید
اضافه شد، تفکیک Comment Management از Review Status است:

- **Entity جدید `VDRComment`**: هر Comment مستقل از VDRSubmission ثبت می‌شود و یک `ActionStatus` جدای
  خودش دارد: `OpenComment`, `VendorResponsePending`, `Verified`, `Closed`, `NoActionRequired`.
- این یعنی Review Status مدرک (AP/NC/AC/CO/RE روی VDRSubmission) و پیگیری اجرای هر Comment دو مسیر
  کاملاً جدا هستند — دقیقاً طبق قانون بخش ۶ سند: "Status و Action باید جدا باشند".
- در صفحه‌ی جزئیات هر VDR Item، یک بخش «Comment Management» اضافه شد: ثبت Comment جدید روی هر رویژن،
  تغییر Action Status، و ثبت پاسخ Vendor.

### Migration اضافی

```
dotnet ef migrations add AddVdrCommentManagement --project PEMS.Persistence --startup-project PEMS.Web
dotnet ef database update --project PEMS.Persistence --startup-project PEMS.Web
```

(اگر migration قبلی `AddVdrModule` را هنوز اجرا نکرده‌اید، همان دستور Migration کلی را بزنید؛ EF Core
هر دو تغییر را با هم در یک Migration جدید می‌بیند اگر هنوز `database update` را برای نسخه‌ی قبلی نزده باشید.)

### باقی‌مانده از سند جدید (طبق اولویت‌بندی شما، در نوبت بعدی)

Related Equipment/Contract روی مدارک + Discipline (Tasks/KPI/Dashboard)، سپس Security پایه
(Audit Log + Permission Matrix)، سپس Correspondence Management.


- تشخیص بلوک‌های رویژن بر اساس تکرار نام ستون «Vendor Transmittal NO.» است؛ اگر ساختار یک فایل VDR دیگر
  (مثلاً پروژه‌ی دیگر) اسم ستون‌ها را متفاوت بنویسد، ممکن است نیاز به تنظیم اسامی در `VDRImportService`
  باشد — طراحی به‌گونه‌ای است که این تغییر فقط در یک فایل (نه کل سیستم) لازم است.
- ویرایش تک‌تک مقادیر یک ردیف Staged (قبل از Commit) هنوز در UI وجود ندارد؛ فعلاً فقط می‌توان یک ردیف را
  Include/Exclude کرد. اگر لازم شد، افزودن یک فرم ویرایش سریع ساده است چون داده در PayloadJson ساختاریافته است.
- تحلیل AI برای تطبیق خودکار فایل‌های آپلودی Vendor با آیتم‌های VDR (گام ۵ کامل) و تولید گزارش مدیریتی با AI
  (گام ۶) هنوز پیاده‌سازی نشده‌اند؛ زیرساخت لازم (AI Extraction pipeline، Chat Assistant) از فازهای قبلی آماده
  است و افزودن آن‌ها گام بعدی منطقی است.

## به‌روزرسانی: Digital Equipment Passport + Discipline Workspace

- **Traceability**: `EngineeringDocument` حالا `EquipmentId`/`ContractId` دارد؛ `VDRItem` حالا `EquipmentId` دارد
  (هر دو به‌صورت اختیاری قابل انتخاب در فرم‌های Create/Edit).
- **Digital Equipment Passport** (منو CMMS Integration → Equipment): هر تجهیز به مدارک مهندسی، آیتم‌های VDR،
  MR (بر اساس تطبیق شماره تگ)، و تاریخچه‌ی Work Order وصل می‌شود.
- **Discipline Workspace** (منو Administration → Disciplines): نمای اختصاصی هر دیسیپلین (دیتابیس مشترک،
  فقط نما فیلترشده - طبق بخش ۵ سند راهنما) با KPI (تعداد مدارک/معلق/تجهیز/VDR/Task باز و معوق) +
  مدیریت Task/Activity/Deliverable با مسئول و مهلت.

### Migration (اگر پوشه‌ی Migrations را طبق راهنمای قبلی ریست کردید، این تغییرات از قبل در همان یک Migration اولیه لحاظ شده‌اند؛ در غیر این صورت یک Migration جدید بسازید)

```
dotnet ef migrations add AddTraceabilityAndDisciplineWorkspace --project PEMS.Persistence --startup-project PEMS.Web
dotnet ef database update --project PEMS.Persistence --startup-project PEMS.Web
```

### باقی‌مانده (طبق اولویت‌بندی خودم، در نوبت بعدی)

Security پایه (Audit Log + Permission Matrix)، سپس Correspondence Management.

## به‌روزرسانی: Security پایه (Audit Log + Permission Matrix)

- **Audit Log**: هر Insert/Update/Delete روی هر Entity سیستم، خودکار و بدون نیاز به کد اضافه در هیچ
  سرویسی، در جدول `AuditLogs` ثبت می‌شود (از طریق بازنویسی `SaveChanges`/`SaveChangesAsync` در
  `PEMSDbContext`). فقط فیلدهایی که واقعاً تغییر کرده‌اند ثبت می‌شوند، به‌همراه کاربر و زمان.
  مسیر مشاهده: Administration → Audit Log (فقط SystemAdmin).
- **Permission Matrix**: جدول `RolePermissions` مجوزهای ریزدانه (View/Create/Edit/Delete/Approve) را
  به‌ازای هر نقش و هر ماژول نگه می‌دارد؛ داده‌محور است، یعنی مدیر سیستم بدون نیاز به Deploy کد جدید
  می‌تواند از UI تغییرش دهد. مسیر: Administration → Permission Matrix.
  یک Attribute عمومی (`[RequirePermission("VDR", "Approve")]`) ساخته شد و به‌عنوان نمونه روی دو عملیات
  حساس اعمال شد: Commit کردن Import اکسل VDR، و تغییر وضعیت Contract. اعمال آن روی بقیه‌ی اکشن‌های
  حساس، کاری تدریجی و کم‌ریسک است (فقط افزودن یک خط Attribute به هر اکشن).
- یک مقدار پیش‌فرض منطقی برای ماتریس مجوزها به‌صورت خودکار در اولین اجرا Seed می‌شود.

### Migration

```
dotnet ef migrations add AddSecurityAuditAndPermissions --project PEMS.Persistence --startup-project PEMS.Web
dotnet ef database update --project PEMS.Persistence --startup-project PEMS.Web
```

### باقی‌مانده

Correspondence Management (ماژول کامل جدید طبق فاز ۲) — تنها موردی که از فهرست اولویت‌بندی‌شده باقی مانده.

### محدودیت‌های شفاف این بخش

- Permission Matrix فعلاً فقط روی دو اکشن نمونه اعمال شده، نه همه‌ی اکشن‌های حساس سیستم؛ گسترش آن به
  بقیه‌ی ماژول‌ها یک تصمیم عمدی برای کنترل حجم تغییرات این مرحله بود.
- Backup Strategy (بخشی از Security Requirements) یک مسئولیت زیرساختی/DBA است، نه کد اپلیکیشن؛
  در راهنمای معماری قبلی (خط‌مشی نگهداری کامل + افتراقی + Log) مستند شده و نیازی به کد اضافه ندارد.

## به‌روزرسانی: Engineering Document Intelligence (طراحی الهام‌گرفته از AVEVA Engineering)

طبق درخواست جدید، ماژول Engineering Document بازطراحی شد تا دیگر فقط یک مخزن فایل نباشد:

### جریان کامل (دقیقاً طبق درخواست)
Upload → AI Processing → Classification → Discipline Assignment → Engineering Review →
Comment → Revision → Approval → Knowledge Base

فیلد جدید `EngineeringDocument.AIWorkflowStage` این ۹ مرحله را دنبال می‌کند (مستقل از فیلد قدیمی
`Status` که برای گردش‌کار تایید/رد ساده‌ی قبلی حفظ شده - چیزی خراب نشد).

### چه چیزی استخراج و پیشنهاد می‌شود (همان موتور AI که برای VDR ساخته شد، توسعه‌یافته)
Discipline, Document Type, Revision, Issue Purpose, **Vendor** (جدید), **Related MR** (جدید)،
و **Equipment Tag** (چندتایی - طبق الگوی AVEVA Engineering که یک مدرک می‌تواند به ده‌ها Tag مرتبط
باشد، نه فقط یک تجهیز). هر پیشنهاد یک Confidence Score دارد و **هیچ‌کدام مستقیم روی مدرک نوشته
نمی‌شود** تا مهندس دیسیپلین آن را در صفحه‌ی Review (`AIDocumentClassification` staging) تایید کند -
دقیقاً همان اصل "Human Approval Before Final Registration" که خواسته بودید.

### طراحی UI (مطابق الگوی موفق VDR که خواستید)
- **Dashboard** (`/EngineeringDocumentIntelligence`): تعداد مدارک به تفکیک مرحله + صف بازبینی معلق.
- **Review Screen**: پیشنهادهای AI با Checkbox برای انتخاب کدام فیلدها اعمال شوند، لیست Tag های
  پیشنهادی برای تایید/رد جداگانه، دکمه‌های پیشروی مرحله، و Comment Management کامل (Action Status
  مستقل از Review Status، دقیقاً مثل الگوی VDRComment).

### داده‌محوری به‌سبک AVEVA Engineering
- `EngineeringDocumentTagLink`: ارتباط چندبه‌چند مدرک↔Tag (نه یک FK ساده)، با Confidence و منشا
  (AI Proposed / Manual).
  - `EngineeringDocument.VendorId` و `RelatedMRId` مدرک را مستقیم به داده‌ی مرکزی سازمان وصل می‌کند،
  نه فقط یک فایل مستقل.

### Migration

```
dotnet ef migrations add AddEngineeringDocumentIntelligence --project PEMS.Persistence --startup-project PEMS.Web
dotnet ef database update --project PEMS.Persistence --startup-project PEMS.Web
```

### محدودیت شفاف

"Knowledge Base" فعلاً فقط به‌عنوان مرحله‌ی نهایی گردش‌کار ثبت می‌شود؛ تبدیل واقعی به Vector
Database/RAG (طبق AI Architecture سند اول) هنوز پیاده‌سازی نشده و یک فاز جدا و بزرگ است.

## به‌روزرسانی بزرگ: Enterprise Document Core (یکپارچه‌سازی کامل، طبق PEMS Development Specification)

طبق تصمیم شما، به‌جای راه‌حل کم‌ریسک پل مشترک، **یکپارچه‌سازی کامل** انجام شد:

### چه چیزی تغییر کرد
- یک Entity پایه‌ی جدید **`Document`** ساخته شد (Enterprise Document Core) با فیلدهای کاملاً مشترک:
  DocumentNo, Title, DocumentType, Project, Discipline, **Area** (جدید، از MDR)، RevisionNo, Status,
  **CommentStatus** (جدید، جدا از Status)، **IsFrozen/FrozenDate** (جدید، Freeze Status از MDR)،
  PlannedDate/ActualDate/**DelayDays محاسبه‌شده**، AIWorkflowStage.
- `EngineeringDocument` و `VDRItem` (Vendor Document) هر دو با EF Core **Table-Per-Type** از `Document`
  ارث می‌برند. یک جدول `Documents` مرکزی + دو جدول تخصصی، دقیقاً طبق نمودار سند شما.
- دو نوع جدید حداقلی هم اضافه شد تا هر ۴ نوع اصلی سند پوشش داده شود: `ContractDocument`,
  `MaintenanceDocument` (توسعه‌ی کامل آن‌ها فاز بعدی Contract/CMMS خواهد بود).
- **`DocumentComment`** جایگزین دو Entity قبلی (EngineeringDocumentComment + VDRComment) شد - حالا
  Comment Management برای هر نوع مدرکی یکسان کار می‌کند.
- **`DocumentTagLink`** جایگزین EngineeringDocumentTagLink شد - همان ارتباط چندبه‌چند AVEVA-style
  حالا برای Vendor Document هم در دسترس است، نه فقط Engineering Document.
- `AIDocumentClassification` حالا به `Document` پایه وصل است، نه فقط `EngineeringDocument` - یعنی
  موتور AI Classification که برای مدارک مهندسی ساختیم، از نظر معماری برای VDR هم آماده است.

### طراحی ایمن (هیچ کدی که قبلاً کار می‌کرد خراب نشد)
تمام Controller ها و View های VDR/Engineering Documents/Equipment/Discipline Workspace که در فازهای
قبلی ساختیم **بدون تغییر کار می‌کنند**، چون:
1. فیلدهای مشترک (DocumentNo, Title, ProjectId, DisciplineId, Status, CreatedDate, IsActive) روی
   `EngineeringDocument` و `VDRItem` **همان اسم قبلی را حفظ کردند** (فقط از کلاس پایه به ارث رسیدند)
   — دسترسی به `document.DocumentNo` در کد قدیمی بدون تغییر کار می‌کند.
2. DTO ها (`VDRItemDto`, `EngineeringDocumentDto`, `VDRCommentDto`, ...) دست‌نخورده ماندند؛ فقط
   Service هایی که این DTO ها را از موجودیت‌های جدید پر می‌کنند به‌روزرسانی شدند. یعنی هیچ View ای
   نیاز به تغییر نداشت.
3. فقط `VDRItem.VDRItemId` به `VDRItem.DocumentId` تغییر کرد (چون PK حالا مشترک است) - همه‌ی
   Service های مرتبط (VDRService, VDRImportService, EquipmentService, DisciplineWorkspaceService)
   برای این یک تغییر اصلاح شدند.

### فیلدهای جدید MDR در UI
Area، Comment Status، Freeze و Delay محاسبه‌شده حالا در فرم ایجاد مدرک مهندسی و در Document Register
قابل مشاهده/ورود هستند.

### Migration (به‌خاطر تغییر ساختاری جدول‌ها، حتماً طبق راهنمای ریست کامل Migration که قبلاً دادم عمل کنید)

```
rmdir /s /q PEMS.Persistence\Migrations
dotnet ef migrations add InitialCreate --project PEMS.Persistence --startup-project PEMS.Web
dotnet ef database drop --project PEMS.Persistence --startup-project PEMS.Web --force
dotnet ef database update --project PEMS.Persistence --startup-project PEMS.Web
```

### باقی‌مانده و محدودیت‌های شفاف
- Import Engine اختصاصی برای فایل MDR واقعی (شبیه VDR Import Wizard) هنوز ساخته نشده - این منطقاً
  گام بعدی است چون حالا مدل داده برای آن آماده است.
- `ContractDocument` و `MaintenanceDocument` نسخه‌ی حداقلی هستند (بدون Controller/View)؛ توسعه‌ی کامل
  آن‌ها بخشی از فاز Contract Management و تعمیق CMMS خواهد بود.
- `DocumentRevision` (تاریخچه‌ی رویژن مدرک مهندسی) و `VDRSubmission` (تاریخچه‌ی ارسال VDR) عمداً
  یکپارچه نشدند - این دو مفهوم مشابه ولی متفاوت هستند (رویژن رسمی در برابر چرخه‌ی ارسال/بازبینی) و
  یکپارچه‌سازی زودهنگام آن‌ها ریسک بی‌دلیل داشت.

## به‌روزرسانی: MDR Import Wizard (UI کامل)

طبق الگوی دقیق VDR Import (Upload → Staging → Review → Commit)، حالا برای فایل واقعی MDR هم پیاده شد:

- می‌تواند فایل چندشیتی واقعی MDR شما را بخواند: شیت‌های رجیستر اصلی (مثل "ABS & Common"، "Rubber")،
  شیت "Freezed Docs"، و شیت‌های "Not Issued Docs" — همه با یک منطق واحد بر اساس **نام ستون‌ها**
  (نه موقعیت ثابت) پردازش می‌شوند تا با چیدمان‌های مختلف هر شیت سازگار باشد.
- شیت‌های خلاصه/گزارش (Report, Annex, Sheet2) خودکار نادیده گرفته می‌شوند.
- خروجی مستقیم روی `EngineeringDocument` (همان Enterprise Document Core) می‌نشیند: Area، Discipline،
  Revision، Comment Status، Freeze، و Status محاسبه‌شده از Comment Status (طبق تصمیم قبلی: AC مثل CO).
- مسیر: **Engineering Management → MDR Import**.

### Migration

```
dotnet ef migrations add AddMDRImport --project PEMS.Persistence --startup-project PEMS.Web
dotnet ef database update --project PEMS.Persistence --startup-project PEMS.Web
```

## گام بعدی (طبق دستور صریح شما، هنوز اجرا نشده)

سند «AI Engineering Intelligence Redesign» خوانده شد. خلاصه‌ی تغییرات معماری بزرگی که می‌خواهد:
AI Intake Queue (آپلود ≠ ساخت خودکار Engineering Document)، AI Classification کامل قبل از ورود به هر
جدول، Routing هوشمند به ماژول مناسب (MDR/VDR/MR/MRQ/Contract)، AI Relationship Engine، و ارتقای
Chat Assistant به RAG واقعی روی کل Knowledge Base. این یک بازطراحی معماری بزرگ دیگر است (شبیه
Document Core) که در پیام بعدی، با تحلیل و طراحی قبل از کدنویسی (طبق روال قبلی)، شروع می‌شود.

## بازطراحی بزرگ: AI Intake Queue + Intelligent Routing (طبق AI Engineering Intelligence Redesign)

### رفع باگ اصلی
`DocumentUploadController` دیگر مستقیم `EngineeringDocument` نمی‌سازد. `DocumentAttachment.DocumentId`
حالا **Nullable** است - یک فایل می‌تواند مستقل از هر مدرک تجاری در سیستم وجود داشته باشد.

### جریان جدید کامل
Upload → **AI Intake Queue** (`DocumentAttachment` با `DocumentId = null`) → AI Classification کامل
(Category از ۱۶ نوع سند، Discipline، Area، Unit، System، Subsystem، Vendor، MR، Equipment Tag) →
**Confidence Gate** (High/Medium/Low، آستانه‌ها از `AIConfidenceSettings` قابل تنظیم) →
- **High + قانون Auto-Route فعال** → مسیریابی خودکار (فقط MDR/VDR که موتور ساخت رکورد کامل دارند)
- **Medium** → صف "Pending Engineering Review"
- **Low یا خطای AI** → صف "Pending Manual Classification"

سپس مهندس در صفحه‌ی Review، پیشنهاد AI را تایید/اصلاح می‌کند و روی دکمه‌ی **Confirm & Route** می‌زند.

### Routing داده‌محور
جدول `RoutingRule` (Category→Module) کاملاً از **Administration → AI Routing Rules** قابل تغییر است
(طبق قانون "Workflow Engine باید Configurable باشد، نه Hardcode") — همان‌جا آستانه‌های Confidence هم
تنظیم می‌شوند.

### مسیرهای UI جدید
- **AI Engineering Intelligence → AI Intake Queue**: صف کامل + فیلتر بر اساس مرحله + صفحه‌ی Review
- **Administration → AI Routing Rules**: جدول Category→Module + آستانه‌های Confidence

### محدودیت شفاف این نسخه
مسیریابی خودکار کامل (ساخت واقعی رکورد) فقط برای **MDR** و **VDR** پیاده شده، چون این دو ماژول از
قبل موتور استخراج ساختاریافته‌ی کافی دارند. برای MR/MRQ/Contract/CMMS، سیستم فقط «برچسب‌گذاری و
هدایت» می‌کند (فایل به همراه پیشنهاد AI در دسترس می‌ماند) و ساخت رکورد نهایی دستی است — چون این
ماژول‌ها فیلدهای ساختاریافته‌ی بیشتری نیاز دارند که فعلاً از سند AI استخراج نمی‌شوند. **AI Relationship
Engine** (زنجیره‌ی کامل تجهیز→مدارک→Vendor→Contract→CMMS) و ارتقای Chat Assistant به RAG واقعی هنوز
پیاده‌سازی نشده‌اند - این‌ها فاز بعدی هستند.

### Migration

```
dotnet ef migrations add AddAIIntakeAndRouting --project PEMS.Persistence --startup-project PEMS.Web
dotnet ef database update --project PEMS.Persistence --startup-project PEMS.Web
```

## فاز بعدی: AI Relationship Engine + ارتقای Chat Assistant

### AI Relationship Engine
به‌جای یک جدول Graph جدید و پرریسک برای همه‌چیز، زنجیره‌ی روابط (طبق مثال سند: Pump P-101 →
Datasheet → Vendor → PO → Contract → MR → MRQ → CMMS) از روی **FK های موجود** که از قبل قابل‌اعتماد
هستند Query می‌شود (بدون خطای AI، بدون نیاز به Migration پرریسک). یک جدول جدید `EngineeringRelationship`
هم اضافه شد، مخصوص روابطی که AI در آینده کشف می‌کند ولی FK صریحی ندارند (مثلاً یک نقشه به نقشه‌ی
دیگر ارجاع می‌دهد) - زیرساخت آماده است، الگوریتم تشخیص خودکار آن فاز بعدی است.

**UI**: صفحه‌ی جدید **AI Engineering Intelligence → Relationship Explorer** - از یک شماره‌تگ تجهیز،
شماره مدرک یا نام Vendor شروع می‌کند و کل زنجیره را نشان می‌دهد. دکمه‌ی "Explore Relationships" هم
به صفحات Equipment Details و Vendor Details اضافه شد.

### ارتقای Chat Assistant (RAG سبک)
قبلاً فقط یک خلاصه‌ی آماری کلی به مدل می‌داد. حالا وقتی سوال شامل یک شماره‌تگ/نام Vendor/شماره مدرک
باشد (مثل «مدارک مرتبط با پمپ P-101 را نشان بده»)، اول از AI Relationship Engine یک زمینه‌ی کاملاً
متمرکز و واقعی می‌سازد و همان را به مدل می‌دهد - نه فقط آمار کلی. اگر موجودیت خاصی پیدا نشود، به
همان خلاصه‌ی آماری قبلی برمی‌گردد (که تعداد فایل‌های در انتظار در Intake Queue هم به آن اضافه شد).

### محدودیت شفاف
این یک RAG واقعی مبتنی بر Embedding/Vector Database نیست (که در سند AI Architecture اول پیشنهاد شده
بود) - یک "RAG سبک" مبتنی بر بازیابی داده‌ی ساختاریافته از دیتابیس است. برای سوالاتی که دقیقاً یک
موجودیت مشخص (تگ/Vendor/شماره مدرک) در آن‌ها نباشد (مثل "خلاصه‌ی نظرات Vendor" بدون گفتن کدام Vendor)،
هنوز به خلاصه‌ی آماری کلی برمی‌گردد. افزودن Embedding واقعی روی محتوای متن مدارک، فاز بعدی است.

### Migration

```
dotnet ef migrations add AddRelationshipEngine --project PEMS.Persistence --startup-project PEMS.Web
dotnet ef database update --project PEMS.Persistence --startup-project PEMS.Web
```

## گام ۱ از سند Engineering Intelligence Redesign: Procurement Package (یکپارچه‌سازی MR + MRQ)

طبق تصمیم شما، این اولین گام از سه‌گانه‌ی تعیین‌شده بود (Procurement Package → MDR Intelligence →
MDR Dashboard).

### چه چیزی ساخته شد
- **`ProcurementPackage`**: Entity والد جدید که MR، MRQ، ارتباطات (Vendor Inquiry/Technical
  Clarification/Engineering Question/Response) و تاریخچه‌ی تایید را زیر یک سقف یکپارچه می‌کند.
- **`ProcurementCommunication`**: یک Entity واحد برای هر ۴ نوع ارتباط (به‌جای ۴ جدول جدا)، با
  Action Status مستقل (Open/Answered/Closed) طبق همان قانون تفکیک Status/Action که در VDR اعمال شد.
- **`ProcurementApproval`**: تاریخچه‌ی تایید/رد.
- **پیشرفت خودکار مرحله**: وقتی یک Vendor Inquiry یا Technical Clarification یا Response ثبت
  می‌شود، مرحله‌ی Package خودکار جلو می‌رود (طبق زنجیره‌ی سند: MR → Vendor Inquiry → Technical
  Clarification → MRQ → Response → Approval → Closeout).

### سازگاری با گذشته (چیزی خراب نشد)
`MaterialRequisition` و `MaterialRequisitionQuotation` موجود فقط یک FK اختیاری
(`ProcurementPackageId`) گرفتند - همه‌ی MR/MRQ های قبلی و صفحات آن‌ها بدون تغییر کار می‌کنند.
منوی «MR Management (legacy)» / «MRQ Management (legacy)» برای دسترسی مستقیم باقی ماند؛ منوی جدید
**«Procurement Package»** مسیر توصیه‌شده‌ی از این به بعد است.

### UI
مسیر: **Engineering Management → Procurement Package**. صفحه‌ی Details یک صفحه‌ی واحد است: نوار
پیشرفت مرحله (قابل کلیک برای پیشروی دستی)، لیست MR/MRQ های پیوندشده، تاریخچه‌ی کامل ارتباطات با
فرم پاسخ‌دهی، تاریخچه‌ی تایید، و دکمه‌ی Closeout.

### Migration

```
dotnet ef migrations add AddProcurementPackage --project PEMS.Persistence --startup-project PEMS.Web
dotnet ef database update --project PEMS.Persistence --startup-project PEMS.Web
```

### گام بعدی
طبق ترتیبی که تعیین کردید: **MDR Intelligence** (Engineering Object Detection + Relationship
Creation خودکار حین Routing برای MDR، دقیقاً هم‌سطح VDR) سپس **MDR Dashboard**.

## به‌روزرسانی: دو استاندارد کم‌ریسک از PEMS Architecture & Development Bible v1.0

طبق سند جدید (که علاوه بر `prompt_for_claude.docx`، یک "Bible" ۱۲ جلدی هم شامل می‌شد)، دو مورد
کم‌ریسک و مکانیکی از ~۱۵ مورد فنی مشخص آن پیاده‌سازی شد:

### ۱) `BaseEntity` استاندارد (Volume V)
یک کلاس پایه‌ی جدید در `PEMS.Domain/Common/BaseEntity.cs` با فیلدهای واقعاً جدید:
- **`CreatedBy` / `ModifiedBy`**: تا الان فقط تاریخ ذخیره می‌شد، نه اینکه چه کسی. حالا خودکار از
  کاربر لاگین‌شده پر می‌شود (همان `SaveChanges` Interceptor موجود برای Audit Log گسترش یافت).
- **`RowVersion`**: Concurrency واقعی - جلوگیری از این‌که دو کاربر هم‌زمان یک رکورد را رونویسی کنند
  بدون اطلاع از تغییر یکدیگر (قبلاً نداشتیم).
- **`TenantId`**: آماده‌سازی برای چندشرکتی/چندسازمانی آینده - فعلاً Null می‌ماند، استفاده نمی‌شود.

**عمداً روی همه‌ی ۷۰ Entity اعمال نشد** - این یک تصمیم مدیریت ریسک آگاهانه بود: `CreatedDate`/`IsActive`
موجود با معناهای کمی متفاوت در ده‌ها Query پخش شده‌اند و یکپارچه‌سازی فوری آن‌ها ریسک شکستن کد فعلی
داشت. به‌جایش، روی هسته‌ی مرکزی که واقعاً کار می‌کنیم اعمال شد: `Document` (و در نتیجه هر ۴ نوع مدرک
به‌صورت خودکار)، `Vendor`، `Contract`، `PurchaseOrder`، `ProcurementPackage`، `MaterialRequisition`،
`Equipment`. Entity های قدیمی‌تر (Instrument, Valve, PipingLine و ...) وقتی واقعاً روی آن‌ها کار شود
مهاجرت می‌کنند.

### ۲) جداسازی `IEntityTypeConfiguration<T>` (Volume V, XI)
`OnModelCreating` قبلاً یک متد ~۱۱۰۰ خطی بود. حالا پیکربندی هسته‌ی مرکزی (همان لیست بالا + VDR/MDR
Import و Comment/Approval های مرتبط) به کلاس‌های جدا در `PEMS.Persistence/Configurations/` منتقل
شد (`DocumentCoreConfigurations.cs`, `ProcurementConfigurations.cs`, `VendorConfiguration.cs`,
`ContractConfiguration.cs`, `PurchaseOrderConfiguration.cs`) و با
`modelBuilder.ApplyConfigurationsFromAssembly(...)` بارگذاری می‌شوند. Entity های قدیمی‌تر همچنان
در `OnModelCreating` پیکربندی می‌شوند تا زمانی که مهاجرت آن‌ها ارزش عملی داشته باشد.

### چیزی خراب نشد
هیچ نام یا رفتار موجودی تغییر نکرد - این فقط جابه‌جایی کد و افزودن فیلدهای جدید (Nullable) بود.

### Migration

```
dotnet ef migrations add AddBaseEntityAndConfigSplit --project PEMS.Persistence --startup-project PEMS.Web
dotnet ef database update --project PEMS.Persistence --startup-project PEMS.Web
```

### باقی‌مانده از سند (تصمیمات استراتژیک، منتظر تایید صریح شما)
Multi-Tenancy کامل، CQRS، Redis Caching، Hangfire Background Jobs، Serilog، Vector DB/RAG واقعی،
Knowledge Graph رسمی، Docker/Kubernetes/CI-CD.

## به‌روزرسانی: Background Jobs (Hangfire) + Structured Logging (Serilog)

از بین موارد باقی‌مانده‌ی سند، این دو را انتخاب کردم چون **یک مشکل واقعی و موجود در کد فعلی** را
رفع می‌کنند (نه فقط چک‌کردن یک آیتم از لیست سند) و هر دو در Tech Stack همان سند اولیه صراحتاً نام
برده شده بودند.

### مشکل واقعی که رفع شد
تا الان، هر پردازش AI (Intake Queue, AI Extraction, OCR Retry) با یک `Task.Run` ساده در پس‌زمینه
اجرا می‌شد. این یعنی: **اگر IIS/Application Pool در حین پردازش Restart شود، آن کار برای همیشه گم
می‌شود** - بدون Retry، بدون اطلاع، بدون امکان رصد. این دقیقاً همان چیزی بود که سند به آن اشاره داشت.

### راه‌حل: Hangfire
- هر ۳ نقطه‌ای که `Task.Run` داشتند (`EngineeringDocumentsController`, `DocumentUploadController`,
  `OCRProcessingController`) به `Hangfire.BackgroundJob.Enqueue(...)` تبدیل شدند.
- صف کارها الان روی همان دیتابیس SQL Server ذخیره می‌شود - Restart برنامه دیگر کار را از بین نمی‌برد.
- Retry خودکار در صورت خطا.
- **Dashboard مدیریتی** در مسیر `/hangfire` (فقط SystemAdmin) - رصد زنده‌ی صف پردازش AI، لینک هم
  در Administration اضافه شد.

### Serilog
Logging پیش‌فرض با Serilog جایگزین شد - خروجی هم‌زمان روی Console و فایل روزانه در `logs/pems-.log`
(نگه‌داری ۳۰ روز)، برای عیب‌یابی و ممیزی بهتر در Production.

### Migration / نصب

Hangfire یک جدول‌سازی خودش را در دیتابیس (`HangFire.*` schema) اولین بار که برنامه اجرا شود خودکار
می‌سازد - **نیازی به Migration دستی EF Core ندارد**. فقط کافی‌ست بعد از `dotnet restore`،
`dotnet run` را بزنید؛ Hangfire جداول لازم را خودش می‌سازد.

### باقی‌مانده از سند
Multi-Tenancy کامل، CQRS، Redis Caching، Vector DB/RAG واقعی، Knowledge Graph رسمی،
Docker/Kubernetes/CI-CD - این‌ها یا تصمیمات استراتژیک بزرگ‌تری هستند یا در این محیط (بدون
اینترنت/Deploy واقعی) قابل تست معنادار نیستند.

## بررسی و افزودن: Docker / docker-compose / CI (طبق Bible Volume X)

### نکته‌ی مهم قبل از هرچیز
هنگام بررسی سند، متوجه شدم **Serilog و Hangfire از قبل کاملاً پیاده‌سازی شده بودند** (هر دو در
`Program.cs` فعال، هر سه Controller مربوط به AI از `Task.Run` به `Hangfire.BackgroundJob.Enqueue`
مهاجرت کرده‌اند، Dashboard امن `/hangfire` فقط برای SystemAdmin). چیزی برای اضافه‌کردن نبود -
فقط تایید و مستند شد که این دو مورد کامل و درست هستند.

### چیزی که واقعاً جدید اضافه شد: Docker
- **`Dockerfile`**: Build دومرحله‌ای (SDK برای Build، ASP.NET Runtime برای اجرا)، شامل نصب
  Tesseract OCR (فارسی+انگلیسی) که PEMS.AI به آن نیاز دارد.
- **`docker-compose.yml`**: سه سرویس - `pems-web`، `sqlserver` (SQL Server 2022 در Container)،
  `ollama` (موتور AI محلی، کاملاً Offline طبق الزام سند). Volume های Persistent برای دیتابیس،
  فایل‌های آپلودی، و لاگ‌ها.
- **`.github/workflows/build.yml`**: CI پایه (Restore + Build روی هر Push/PR).
- **`.dockerignore`**: جلوگیری از کپی `bin/obj/.git` به داخل Image.

### چرا این و نه بقیه‌ی موارد سند
Multi-Tenancy، CQRS، Redis همگی یا نیاز به تصمیم معماری بزرگ دارند یا به زیرساخت بیرونی (Redis
Server) وابسته‌اند که راه‌اندازی و تست‌شان در این محیط ممکن نیست. Docker برعکس، هیچ کد برنامه را
لمس نمی‌کند (ریسک صفر) و مستقیماً قابل استفاده است.

### نحوه‌ی اجرا (روی سیستم خودتان، بعد از نصب Docker Desktop)

```
docker compose up -d --build
docker exec -it pems-ollama ollama pull qwen2.5:3b
```

بعد از چند دقیقه، برنامه روی `http://localhost:8080` بالا می‌آید.

## به‌روزرسانی بزرگ: Semantic Search / RAG واقعی (طبق Volume VIII - Bible)

بزرگ‌ترین خلأ باقی‌مانده از سند AI که در چند مرحله‌ی قبل صادقانه گفته بودم "این RAG سبک است، نه
واقعی" - حالا واقعی شد، بدون نیاز به هیچ Vector Database بیرونی:

- **`IOllamaService.GenerateEmbeddingAsync`**: از همان Ollama محلی موجود، حالا Embedding هم تولید
  می‌کند (مدل پیش‌فرض `nomic-embed-text`، در `appsettings.json` قابل تغییر).
- **`KnowledgeBaseEntry`**: بردار Embedding به‌صورت JSON در همان SQL Server ذخیره می‌شود.
- **`KnowledgeBaseService`**: جستجو با محاسبه‌ی شباهت کسینوسی واقعی بین بردارها - نه فقط تطابق
  کلمات کلیدی.
- **Indexing خودکار**: هر بار AI Intake Queue یک فایل را طبقه‌بندی می‌کند، خلاصه‌اش خودکار در
  Knowledge Base نمایه می‌شود.
- **Chat Assistant ارتقا یافت**: حالا سه لایه دارد - ۱) اگر یک موجودیت مشخص (تگ/Vendor/مدرک) در
  سوال باشد، از Relationship Engine؛ ۲) در غیر این صورت Semantic Search واقعی روی Knowledge Base؛
  ۳) اگر هیچ‌کدام نتیجه نداد، همان خلاصه‌ی آماری کلی.
- **صفحه‌ی جدید `AI Engineering Intelligence → Knowledge Base Search`**: برای تست/رصد مستقیم.

### نکته‌ی مهم عملیاتی
باید مدل Embedding را هم Pull کنید (علاوه بر مدل اصلی):
```
ollama pull nomic-embed-text
```
اگر این مدل نصب نباشد، سیستم خطا نمی‌دهد - فقط آن ورودی Index نمی‌شود و Chat Assistant به همان
خلاصه‌ی آماری قدیمی برمی‌گردد (Fail-safe، نه Fail-loud).

### Migration

```
dotnet ef migrations add AddKnowledgeBaseSemanticSearch --project PEMS.Persistence --startup-project PEMS.Web
dotnet ef database update --project PEMS.Persistence --startup-project PEMS.Web
```

### محدودیت شفاف
جستجو با بارگذاری همه‌ی رکوردها در حافظه و محاسبه‌ی شباهت انجام می‌شود - در مقیاس فعلی (چند هزار
مدرک) کاملاً کافی و سریع است؛ اگر روزی به صدها هزار رکورد رسید، همین جدول بدون تغییر Interface به
یک Vector Database واقعی (مثل pgvector یا SQL Server Native Vector) قابل مهاجرت است.

## به‌روزرسانی: لایه‌ی Caching آماده‌ی Redis (طبق Volume II-Bible)

`PEMS.Infrastructure` که تا الان تقریباً خالی بود (فقط پوشه‌های placeholder)، حالا اولین استفاده‌ی
واقعی‌اش را دارد:

- **`ICacheService`** (در `PEMS.Application`): یک Interface ساده، `GetOrCreateAsync`/`RemoveAsync`.
- **`MemoryCacheService`**: پیاده‌سازی پیش‌فرض با `IMemoryCache` - بدون نیاز به هیچ زیرساخت بیرونی.
- **`RedisCacheService`**: پیاده‌سازی جایگزین با Redis واقعی - **فقط** وقتی فعال می‌شود که
  `ConnectionStrings:Redis` در `appsettings.json` پر شود. الان خالی است، یعنی سیستم از In-Memory
  استفاده می‌کند؛ هر وقت Redis واقعی راه‌اندازی کردید، فقط همان یک خط تنظیمات را پر کنید - هیچ کد
  دیگری نیاز به تغییر ندارد.
- اولین محل استفاده: `RoutingRuleService` (که روی **هر تک آپلود** در AI Intake Queue صدا زده
  می‌شود) حالا Cache می‌شود؛ تغییر از UI مدیریت Routing Rules هم بلافاصله Cache را باطل می‌کند.

### Migration
نیازی به Migration ندارد (فقط کد جدید، بدون تغییر در دیتابیس).

## جمع‌بندی مسیر باقی‌مانده
از فهرست بزرگ سند Bible، هر آنچه بدون نیاز به تصمیم استراتژیک بزرگ یا زیرساخت بیرونی اجباری
قابل‌ساخت بود، ساخته شد: BaseEntity، جداسازی EF Config، Docker، Semantic Search/RAG، Caching.
سه مورد باقی‌مانده (**Multi-Tenancy کامل**، **CQRS**، **Knowledge Graph رسمی به‌جای زنجیره‌ی
Relationship موجود**) هرکدام یک تصمیم معماری بزرگ با اثر گسترده روی کد فعلی هستند و عمداً بدون
تایید صریح شما شروع نشدند.

## به‌روزرسانی: تکمیل حلقه‌ی Knowledge Graph

جدول `EngineeringRelationship` و متد `AddAIProposedRelationshipAsync` چند مرحله قبل ساخته شدند
ولی هیچ‌جا صدا زده نمی‌شدند - یک زیرساخت نیمه‌کاره. الان کامل شد:

- بعد از این‌که یک مدرک با AI Routing به MDR یا VDR وارد شد، خلاصه‌ی متن آن برای ارجاع به مدارک/
  تجهیزات دیگر **همان پروژه** اسکن می‌شود (تطبیق شماره مدرک/تگ تجهیز در متن).
- هر ارجاع پیدا‌شده به‌عنوان یک رابطه‌ی AI-Proposed در `EngineeringRelationship` ثبت می‌شود.
- **Relationship Explorer** حالا هم روابط قطعی (مبتنی بر FK) و هم روابط کشف‌شده‌ی AI را نشان
  می‌دهد - با یک نشان زرد <i class="bi bi-robot"></i> AI برای تفکیک شفاف («این از پایگاه‌داده
  نیست، AI حدس زده»).
- خطای این بخش (مثلاً اگر Ollama در دسترس نباشد) هرگز خود Routing را متوقف نمی‌کند - Fail-safe.

### Migration
نیازی به Migration جدید ندارد (جدول از قبل ساخته شده بود، فقط استفاده‌اش کامل شد).

## جمع‌بندی نهایی این دور
Multi-Tenancy کامل و CQRS همچنان عمداً دست‌نخورده ماندند - این دو، برخلاف بقیه، واقعاً بازطراحی
گسترده‌ی کد فعلی را می‌طلبند و بدون یک تصمیم صریح («آیا واقعاً چند سازمان جدا روی این سیستم کار
می‌کنند؟» / «آیا واقعاً نیاز به تفکیک Command/Query در این مقیاس داریم؟») شروع آن‌ها فقط ریسک
بی‌دلیل به کدی که کار می‌کند اضافه می‌کند.

## ماژول جدید: Official Correspondence (Letter Management)

طبق درخواست صریح شما، ساختار معماری موجود دست‌نخورده ماند و هرجا ممکن بود از سرویس‌ها/زیرساخت
موجود استفاده شد:

### چه چیزی از قبل استفاده مجدد شد
- **`DocumentAttachment`**: پیوست نامه‌ها از همان جدول/زیرساخت آپلود موجود استفاده می‌کند (فقط
  یک FK جدید `LetterId`، بدون کد آپلود جدید).
- **`Department` / `Employee`**: هردو از قبل در سیستم بودند؛ مقصد ارجاع مستقیم از آن‌ها استفاده می‌کند.
- **`AuditLog`**: خودکار روی هر تغییر Letter/Referral کار می‌کند - نیازی به کد اضافه نبود.
- **Permission Matrix**: یک ماژول جدید («Correspondence») به همان جدول موجود اضافه شد؛ همان
  `[RequirePermission]` روی تایید نامه‌های صادره اعمال شد.
- **BaseEntity / IEntityTypeConfiguration**: Entity های جدید همان استاندارد تازه‌ساخته‌شده را دنبال کردند.

### Entity های جدید (Additive)
- **`Letter`**: یکپارچه برای Incoming/Outgoing/Internal (با `LetterType`) - به‌جای سه جدول تقریباً
  یکسان.
- **`LetterReferral`**: گردش‌کار ارجاع (به کاربر یا دپارتمان)، دستورالعمل، مهلت، ردیابی خوانده/
  نخوانده. هر ارجاع جدید یک رکورد است، پس «تاریخچه‌ی ارجاع» خودکار همین جدول است.
- **`Notification`**: اعلان درون‌برنامه‌ای عمومی - عمداً طوری طراحی شد که هر ماژول آینده هم بتواند
  از همین سرویس استفاده کند، نه فقط مکاتبات.

### UI
- **Correspondence → Correspondence**: Dashboard (KPI) + جستجوی کامل (شماره/موضوع/فرستنده/گیرنده/
  پروژه/دیسیپلین/بازه‌ی تاریخ/وضعیت) + سه دکمه‌ی ثبت سریع (Incoming/Outgoing/Internal).
- **Correspondence → My Referrals**: نامه‌های ارجاع‌شده به کاربر جاری.
- صفحه‌ی Details: گردش‌کار Outgoing (Draft→PendingApproval→Approved→Issued/Rejected)، پیوست‌ها،
  پنل کامل ارجاع (ثبت/تکمیل/علامت‌گذاری خوانده‌شده)، دکمه‌ی **Export PDF** (QuestPDF، بسته‌ی جدید
  و سبک).
- **زنگ اعلان واقعی** در نوار بالای همه‌ی صفحات (قبلاً فقط تزئینی بود) - حالا Dropdown واقعی با
  اعلان‌های نخوانده را نشان می‌دهد.

### محرمانگی (Security requirement #8)
سطح محرمانگی (`ConfidentialityLevel`) ذخیره و در لیست با آیکون قفل مشخص می‌شود. کنترل دسترسی
سطح‌بالا (چه کسی اصلاً نامه را می‌بیند) از همان Permission Matrix موجود عبور می‌کند؛ فیلتر ریزدانه‌تر
(مثلاً «فقط گیرندگان مستقیم محرمانه را ببینند») به‌عمد ساده نگه داشته شد تا پیچیدگی غیرضروری اضافه
نشود - در صورت نیاز واقعی، فاز بعدی است.

### Migration

```
dotnet ef migrations add AddCorrespondenceModule --project PEMS.Persistence --startup-project PEMS.Web
dotnet ef database update --project PEMS.Persistence --startup-project PEMS.Web
```

### ادغام با ماژول‌های دیگر (طبق درخواست)
Project/Discipline از طریق FK مستقیم روی `Letter`. ادغام با Engineering Documents/MR-MRQ به‌صورت
مرجع (یک نامه می‌تواند به یک پروژه/دیسیپلین مشترک با یک مدرک/MR اشاره کند) - پیوند مستقیم دوطرفه
(مثلاً «این نامه دقیقاً به این MR مرتبط است») در فاز بعدی و در صورت نیاز واقعی اضافه می‌شود تا از
پیچیدگی زودهنگام پرهیز شود.

## به‌روزرسانی: پیوند مستقیم دوطرفه‌ی نامه↔مدرک و نامه↔MR

طبق تایید شما، آن تصمیم آگاهانه‌ای که قبلاً ساده نگه داشته بودم را کامل کردم:

- `Letter` حالا `RelatedDocumentId` (به هسته‌ی مشترک Document Core - هم مدرک مهندسی، هم Vendor
  Document) و `RelatedMRId` را دارد.
- در فرم ثبت نامه، انتخاب مستقیم مدرک/MR مرتبط (اختیاری) اضافه شد.
- **واقعاً دوطرفه**: صفحه‌ی Details مدرک مهندسی و صفحه‌ی Details هر MR حالا یک پنل «Related
  Correspondence» دارند که همه‌ی نامه‌های مرتبط را نشان می‌دهد - نه فقط نامه مدرک را می‌بیند،
  مدرک هم نامه‌هایش را می‌بیند.

### Migration
همان Migration قبلی ماژول مکاتبات را اگر هنوز اجرا نکرده‌اید بزنید؛ اگر قبلاً زده‌اید، یک
Migration کوچک اضافه لازم است:

```
dotnet ef migrations add AddLetterRelatedDocumentAndMR --project PEMS.Persistence --startup-project PEMS.Web
dotnet ef database update --project PEMS.Persistence --startup-project PEMS.Web
```

## گام ۱: اعمال اصلاحات فیلترینگ (طبق اولویت Critical گزارش ممیزی)

سه مورد با شدت Critical که در گزارش ممیزی فیلترینگ مشخص شده بود، اعمال شد:

### ۶.۱ — جستجوی سراسری واقعی شد
جعبه‌ی جستجوی بالای هر صفحه که قبلاً کاملاً تزئینی بود (بدون action)، حالا به `GlobalSearchController`
وصل است: هم‌زمان روی Projects، Engineering Documents، VDR، Equipment، Vendors، و Correspondence
جستجو می‌کند و نتایج را دسته‌بندی‌شده نشان می‌دهد.

### ۶.۳ — ریسک گم‌شدن بی‌صدای داده رفع شد
`IntakeService.GetQueueAsync` و `LetterService.SearchAsync` که قبلاً `.Take(200)`/`.Take(300)`
بی‌صدا داشتند، حالا از یک الگوی مشترک و استاندارد **`PagedResult<T>`** استفاده می‌کنند - دقیقاً
همان الگویی که در Audit Log وجود داشت، حالا به یک **کامپوننت مشترک قابل‌استفاده‌ی مجدد**
(`_Pagination.cshtml` + `PaginationViewModel`) تبدیل شد تا هر صفحه‌ی دیگری هم به‌راحتی از همان
استفاده کند - دقیقاً پاسخ به بند ۶.۴ گزارش («هیچ الگوی مشترکی وجود ندارد»).

### ۶.۵ — محرمانگی نامه‌ها حالا واقعاً اعمال می‌شود
`LetterService.SearchAsync` دیگر فقط ConfidentialityLevel را نمایش نمی‌دهد - در خودِ Query
اعمال می‌کند: کاربر بدون مجوز Approve روی ماژول Correspondence، فقط نامه‌های Public/Internal،
یا نامه‌هایی که خودش ثبت کرده، یا نامه‌هایی که به او ارجاع شده را می‌بیند.

### اضافه‌ها
- دکمه‌ی **Reset** به فرم‌های فیلتر Correspondence و AI Intake Queue اضافه شد.

### آنچه هنوز باقی مانده (طبق اولویت High/Medium گزارش، برای فاز بعدی)
فیلتر پایه روی Projects/Equipment/Vendors/Contracts/Procurement Package/MR (فعلاً صفر)، Autocomplete
روی Dropdown های Lookup، و گسترش Pagination مشترک به بقیه‌ی صفحات فهرست‌دار.

### Migration
نیازی به Migration ندارد (فقط تغییر کد سرویس/کنترلر/View، بدون تغییر Schema).

## بازطراحی بنیادین UI/UX

طبق درخواست شما، سه تغییر اساسی اعمال شد:

### ۱) سایدبار کشویی شد
هر ۵ بخش اصلی منو (Engineering Management، AI Engineering Intelligence، Contract Management،
CMMS Integration، Administration) حالا Accordion هستند - با کلیک روی عنوان بخش باز/بسته می‌شوند.
فقط بخشی که صفحه‌ی جاری در آن قرار دارد به‌طور پیش‌فرض باز است؛ بقیه بسته‌اند تا اسکرول طولانی
سایدبار از بین برود.

### ۲) جدول‌های بلند
- تراکم ردیف کم شد (Padding کمتر) تا سطر بیشتری در یک صفحه جا شود.
- هدر جدول‌ها Sticky شد - در جدول‌های بلند، عنوان ستون‌ها هنگام اسکرول گم نمی‌شود.
- یک کلاس مشترک جدید `.table-scroll-area` اضافه شد (ارتفاع محدود + اسکرول داخلی به‌جای صفحه‌ی
  نامحدود بلند) - روی Document Register اعمال شد؛ برای هر صفحه‌ی دیگر هم فقط افزودن همین یک کلاس کافی است.

### ۳) بازطراحی کامل Engineering Documents (به سلیقه‌ی خودم، الهام از EDMS های رقیب مثل AVEVA/Aconex/Poupack)
اینجا نکته‌ی مهمی پیدا شد: صفحه‌ی **لیست** Engineering Documents از ابتدای این پروژه هرگز واقعاً
بازطراحی نشده بود - همان اسکلت پایه‌ی Bootstrap ساده بود (بدون فیلتر، بدون KPI، بدون هماهنگی با
بقیه‌ی برنامه)، برخلاف صفحه‌ی Details آن که به‌مرور خیلی بزرگ و شلوغ شده بود. هر دو اصلاح شدند:

- **Index (لیست)**: نوار KPI بالای صفحه (Total/Approved/Pending/Rejected/Frozen/Delayed)، جستجوی
  آنی بدون رفرش صفحه + فیلتر Discipline/Status/Area + دکمه‌ی Reset، جدول متراکم با Badge رنگی
  برای Status، دکمه‌های اقدام به‌صورت آیکون به‌جای متن.
- **Details**: از یک صفحه‌ی ۸۸۶ خطی با اسکرول پیوسته، به **۵ تب** تبدیل شد: Overview، Attachments
  & AI، Revisions، Workflow History، Correspondence. دکمه‌های اصلی گردش‌کار (Submit/Approve/Reject/
  Create Revision) همیشه بالای صفحه و مستقل از تب فعال قابل مشاهده‌اند. هیچ Action یا فیلدی حذف
  نشد - فقط سازمان‌دهی مجدد شد.

### محدودیت شفاف
این بازطراحی روی Engineering Documents (چون صریحاً درخواست شده بود) و بهبودهای عمومی (سایدبار،
CSS جدول) اعمال شد. اعمال همین الگو روی بقیه‌ی صفحات طولانی (VDR Details، Procurement Package
Details) قدم منطقی بعدی است، در صورت درخواست.

### Migration
نیازی به Migration ندارد (فقط تغییرات View/CSS، بدون تغییر داده).

## دسته ۱ از بازبینی معماری سه‌گانه (Business Analyst / Architect / Engineering Consultant)

### ریشه‌ی واقعی "Discipline Filter Bug" پیدا و رفع شد
مدارکی که دیسیپلین‌شان (هنگام Import یا Routing خودکار AI) با جدول Discipline تطبیق نخورده،
`DisciplineId = null` می‌مانند و در هر فیلتر دیسیپلینی **بی‌صدا ناپدید می‌شدند** - نه خطا، فقط
غیرقابل مشاهده. حالا این‌ها زیر یک سطل مشخص «Unassigned» قابل فیلتر و مشاهده‌اند.

### Multi-Discipline Filter + Saved Filters + Quick Filters
- فیلتر دیسیپلین در Document Register از تک‌انتخابی به چندانتخابی (Checkbox Dropdown) تبدیل شد.
- **`SavedFilter`** یک Entity/سرویس عمومی و قابل‌استفاده‌ی مجدد است (نه فقط برای این صفحه) -
  کاربر می‌تواند فیلتر فعلی را با یک نام ذخیره کند، و اختیاری آن را «پیش‌فرض من» علامت بزند.
- دکمه‌های Quick Filter (Approved، Pending Review) اضافه شد.

### Automatic Document Numbering + Manual Override با مجوز
- **`DocumentNumberingService`**: شماره‌ی مدرک خودکار طبق الگوی `{ProjectCode}-{DisciplineCode}-
  {TypeCode}-{Sequence:0000}` تولید می‌شود.
- یک مجوز جدید در Permission Matrix اضافه شد: **`CanManualNumber`** (ستون «Manual #»). فقط
  نقش‌هایی که این مجوز را دارند (پیش‌فرض: Engineering Manager، Document Controller) می‌توانند در
  فرم ایجاد مدرک، تیک «Enter document number manually» را بزنند؛ در غیر این صورت فیلد غیرفعال
  است و شماره‌گذاری کاملاً خودکار انجام می‌شود.

### MDR/VDR/Transmittal - یک ماژول واحد از نظر ناوبری
یک نوار دسترسی سریع (Documents | Register | Import MDR | VDR | Transmittals) به بالای صفحات
Engineering Documents و Document Register اضافه شد - بدون تغییر مسیر/URL موجود (بدون ریسک شکستن
Bookmark یا لینک‌های قبلی)، فقط تجربه‌ی «یک ماژول واحد» را می‌سازد، دقیقاً همان‌طور که در سطح داده
از قبل (Document Core مشترک) درست بود.

### Migration

```
dotnet ef migrations add AddSavedFiltersAndManualNumbering --project PEMS.Persistence --startup-project PEMS.Web
dotnet ef database update --project PEMS.Persistence --startup-project PEMS.Web
```

### باقی‌مانده از این بازبینی
دسته‌ی ۲ (Revision استاندارد + Audit Trail، Personalized Dashboard، Discipline Workspace کامل،
Bulk Operations) و دسته‌ی ۳ (Correspondence کامل، Bilingual/RTL که نیمه‌کاره مانده بود، Design
System رسمی، CMMS کامل) هنوز باقی‌اند - منتظر می‌مانم بگویید کدام را ادامه بدهم.

## دسته ۲ از بازبینی معماری سه‌گانه

### Revision استاندارد + Audit Trail (بند ۷ و ۱۲)
`DocumentRevision` فیلدهای جدید گرفت: **Reason، Change Description، Reviewer، Created By/Date،
Issued Date، Effective Date، Superseded Revision (زنجیره‌ی کامل به رویژن قبلی)، و AI Revision
Summary**. دکمه‌ی «Create New Revision» حالا یک Modal است (Reason + Change Description)، نه یک
دکمه‌ی خام. تب Revisions در صفحه‌ی Details همه‌ی این فیلدها را نشان می‌دهد. Audit Trail خودکار
همان زیرساخت AuditLog موجود است - نیاز به کد جدید نبود.

### Personalized Dashboard (بند ۲)
- **`UserDashboardWidget`**: چیدمان شخصی هر کاربر (کدام Widget، چه ترتیبی، چه اندازه‌ای).
- کاتالوگ ۸ Widget آماده (Pending Documents، My Referrals، Overdue Letters، Recent Documents،
  VDR Summary، Intake Summary، My Discipline Tasks، Knowledge Base Shortcut) - همه از داده‌ی
  واقعی موجود، نه Mock.
- صفحه‌ی جدید **«Customize Dashboard»**: نمایش/عدم‌نمایش، ترتیب، و اندازه (Small/Medium/Large)
  هر Widget؛ دکمه‌ی Reset به حالت پیش‌فرض.
- این بخش کاملاً **افزودنی** به Dashboard اصلی است - هیچ منطق موجود Home/Index تغییر نکرد، فقط
  یک بخش «My Dashboard» جدید به انتهای صفحه اضافه شد.

### Bulk Operations (پیشنهاد تکمیلی)
Document Register حالا چک‌باکس انتخاب ردیف + «Select All» + دکمه‌ی **Export Selected** دارد -
اولین قدم واقعی Bulk Operations (Export گروهی فقط روی ردیف‌های انتخاب‌شده).

### محدودیت شفاف این دسته
**Discipline Workspace اختصاصی کامل (بند ۱)** دست‌نخورده ماند - آنچه از فازهای قبلی داریم
(Dashboard/KPI/Documents/VDR/MR/Tasks به‌تفکیک دیسیپلین) قبلاً بخش زیادی از این بند را پوشش
می‌دهد؛ AI Insights اختصاصی و Engineering Calendar هنوز کم دارد. Bulk Operations هم فقط روی
Export پیاده شد، نه تغییر وضعیت گروهی/حذف گروهی - این‌ها اگر لازم باشد قدم بعدی‌اند.

### Migration

```
dotnet ef migrations add AddRevisionStandardAndPersonalizedDashboard --project PEMS.Persistence --startup-project PEMS.Web
dotnet ef database update --project PEMS.Persistence --startup-project PEMS.Web
```

## دسته ۳ (اولویت اول): Bilingual Platform واقعی (انگلیسی/فارسی + RTL خودکار)

طبق بند ۹ بازبینی معماری، این‌بار کامل و واقعی پیاده‌سازی شد (نه فقط نیمه‌کاره مثل تلاش قبلی):

### زیرساخت
- **`AddLocalization` + `AddViewLocalization` + `AddDataAnnotationsLocalization`** در `Program.cs`.
- **`RequestLocalizationOptions`** با دو فرهنگ پشتیبانی‌شده (`en`, `fa`) و `CookieRequestCultureProvider`
  - زبان انتخابی کاربر در یک Cookie ذخیره می‌شود، نه Session (بعد از بستن مرورگر هم می‌ماند).
- **`CultureController.SetCulture`**: سوییچر زبان که فقط یک Cookie می‌سازد و برمی‌گردد به همان صفحه.
- سوییچر زبان (🌐 English / فارسی) در نوار بالای همه‌ی صفحات، کنار زنگ اعلان.

### RTL خودکار
- `_Layout.cshtml` حالا `dir="rtl"`/`dir="ltr"` و `lang` را بر اساس فرهنگ جاری Render می‌کند.
- وقتی فارسی فعال است، **`bootstrap.rtl.min.css`** (به‌جای نسخه‌ی معمولی) بارگذاری می‌شود - این
  فایل از قبل در پروژه موجود بود، فقط استفاده نمی‌شد.
- **`admin.rtl.css`** جدید: فقط رگه‌های واقعاً جهت‌دار (Border فعال Sidebar، Padding زیرمنو،
  جهت اعداد/KPI) را برمی‌گرداند - بقیه‌ی چیدمان (Flexbox Row) خودکار با `dir` برمی‌گردد.

### ترجمه‌ی واقعی (Resource-based، نه فقط زیرساخت خالی)
- **`SharedResource.resx`** (انگلیسی) و **`SharedResource.fa.resx`** (فارسی) با ۵۹ عبارت مشترک.
- **کل سایدبار** (۵ عنوان بخش + همه‌ی آیتم‌های اصلی منو) کاملاً دوزبانه شد.
- **صفحه‌ی Engineering Documents** (چون تازه بازطراحی شده بود) به‌عنوان نمونه‌ی کامل دوزبانه -
  KPI ها، فیلترها، هدر جدول - ترجمه شد.

### محدودیت‌های شفاف (طبق تصمیم آگاهانه‌ی مدیریت ریسک)
- **باقی صفحات** (~۸۵ View دیگر) هنوز انگلیسی هستند. الگو کاملاً جااُفتاده و اثبات‌شده است -
  افزودن هر صفحه‌ی جدید یعنی: تزریق `IStringLocalizer<SharedResource>` + افزودن کلید در دو فایل
  resx + جایگزینی متن ثابت با `@Localizer["Key"]`. این کاری مکانیکی و کم‌ریسک است، فقط زمان‌بر.
- **تقویم شمسی**: طبق بند ۹ («Date Format باید چندزبانه باشد») - تبدیل کامل تاریخ‌ها به تقویم
  جلالی در سراسر برنامه (DatePicker ها، فرمت نمایش، ورودی) عمداً انجام نشد - این یک تغییر بسیار
  گسترده‌تر است (تقریباً هر فرم و هر جدول تاریخ‌دار را لمس می‌کند) و باید جداگانه تصمیم‌گیری شود.
- **Template/PDF/AI Prompt چندزبانه**: هنوز اعمال نشد (نمونه نداشتیم که آماده باشد).

### Migration
نیازی به Migration ندارد (فقط زیرساخت Localization + View، بدون تغییر دیتابیس).

## Dark Mode (بخشی قابل‌تحویل از Design System، بند ۸)

- متغیرهای CSS معنایی جدید در `:root` اضافه شد (`--pems-surface`, `--pems-text`, `--pems-table-header-bg`
  و...)؛ `.content-card`, `.kpi-card`, `.pems-topbar`, و جدول‌های Enterprise حالا از این متغیرها
  استفاده می‌کنند به‌جای رنگ ثابت.
- یک بلوک `[data-theme="dark"]` همان متغیرها را برمی‌گرداند - نیازی به نوشتن دوباره‌ی هر Selector نبود.
- **`ThemeController`**: یک Cookie ساده می‌سازد؛ `_Layout.cshtml` همان لحظه‌ی Render سمت سرور آن را
  می‌خواند تا هیچ Flash محتوای بدون استایل هنگام رفرش صفحه رخ ندهد.
- دکمه‌ی سوییچ (🌙/☀️) کنار سوییچر زبان در نوار بالا.

### محدودیت شفاف
این یک رول‌اوت اولیه‌ی Dark Mode است، نه ممیزی کامل بصری هر جزء - سطح اصلی (صفحه، کارت‌ها، جدول‌ها،
فرم‌ها، Modal ها) پوشش داده شد؛ ممکن است چند کامپوننت خاص (مثلاً یک Badge خیلی روشن روی پس‌زمینه‌ی
تیره) نیاز به ریزتنظیم بصری داشته باشد - طبیعی برای هر Dark Mode Rollout اولیه.
«Design System رسمی» (مستندسازی کامل Tokens/Typography/Spacing به‌عنوان یک سند جدا) هنوز ساخته نشد.

### Migration
نیازی به Migration ندارد.

## بازطراحی Correspondence بر اساس نمونه‌ی واقعی (کارتابل Fonix/PARGAR)

طبق تصویری که فرستادید (کارتابل شرکت پتروشیمی جم)، صفحه‌ی اصلی Correspondence بازطراحی شد:

- **برچسب‌های رنگی چندگانه داخل هر ردیف**: نوع نامه، دیسیپلین، اولویت (فقط اگر High/Urgent)،
  محرمانگی (فقط اگر غیر Public/Internal)، مدرک/MR مرتبط، و تعداد ارجاع باز - همه زیر موضوع نامه،
  دقیقاً همان الگوی چند-Badge کارتابل مرجع.
- **ستاره‌گذاری شخصی** (`LetterStar` جدید): هر کاربر می‌تواند نامه را ستاره بزند؛ فقط ستاره‌های
  خودش را می‌بیند - قابلیت واقعی، نه فقط تزئینی.
- **میانبر نوع پیام** (All / Incoming / Outgoing / Internal / Draft) بالای لیست - دقیقاً مثل
  کارپوشه‌ی مرجع (Inbox/Sent/Draft).
- **میانبرهای دیسیپلین** در ستون کناری - از همان جدول Discipline واقعی سیستم (نه لیست ثابت).
- جدول متراکم‌تر با انتخاب چندگانه (Checkbox) آماده برای عملیات گروهی آینده.

### چیزی که عمداً کپی نشد
"رونوشت" در برابر "نامه" (Message Type: Copy vs Original) در نمونه‌ی مرجع یک مفهوم اضافه‌ی
گیرندگان متعدد است که مدل داده‌ی فعلی ما (`Letter` تک‌گیرنده) پشتیبانی نمی‌کند - افزودن گیرندگان
چندگانه/رونوشت یک تغییر داده‌ای واقعی است، نه فقط UI، و باید جداگانه تصمیم‌گیری شود.

### Migration

```
dotnet ef migrations add AddLetterStar --project PEMS.Persistence --startup-project PEMS.Web
dotnet ef database update --project PEMS.Persistence --startup-project PEMS.Web
```

## اضافه شد: چند گیرنده (To / Cc) روی هر نامه - دقیقاً "نامه" در برابر "رونوشت" نمونه‌ی مرجع

آن تفاوتی که در بازطراحی قبلی گفتم «عمداً کپی نشد»، الان اضافه شد:

- **`LetterRecipient`**: هر نامه می‌تواند چند گیرنده داشته باشد - هرکدام `To` (گیرنده‌ی اصلی) یا
  `Cc` (رونوشت)، به یک کارمند، دپارتمان، یا نام بیرونی (Vendor) وصل می‌شود، با وضعیت خوانده/نخوانده‌ی
  **مستقل برای هر گیرنده** (نه یک وضعیت مشترک برای کل نامه).
- در **لیست Correspondence**، اگر کاربر جاری یکی از گیرندگان باشد، یک نشان **To** (آبی) یا **Cc**
  (خاکستری) کنار موضوع نامه نشان داده می‌شود - دقیقاً همان چیزی که در تصویر مرجع دیدید.
- در صفحه‌ی Details، یک پنل «Recipients» جدید: افزودن گیرنده (کارمند یا دپارتمان)، و علامت‌گذاری
  «خوانده شد» برای هرکدام جداگانه.

### Migration

```
dotnet ef migrations add AddLetterRecipients --project PEMS.Persistence --startup-project PEMS.Web
dotnet ef database update --project PEMS.Persistence --startup-project PEMS.Web
```

## سیستم برچسب عمومی (Tags) + وظایف مستقل (Task Actions)

طبق نمونه‌ی کارتابل مرجع (Pargar/Fonix)، این دو قابلیت که در تصویر دیده می‌شدند اضافه شدند:

### برچسب دلخواه (Tags)
- **`Tag`**: برخلاف میانبرهای دیسیپلین (ثابت)، این برچسب‌ها را خود کاربران می‌سازند - نام + رنگ دلخواه.
- **`LetterTag`**: ارتباط چندبه‌چند نامه↔برچسب.
- در صفحه‌ی Details هر نامه: افزودن برچسب موجود یا ساخت‌و‌افزودن آنی یک برچسب جدید، با دکمه‌ی حذف
  روی هر برچسب.
- برچسب‌ها در **لیست Correspondence** هم کنار بقیه‌ی Badge ها نشان داده می‌شوند.
- صفحه‌ی مدیریت **Tags** (منوی جدید): همه‌ی برچسب‌های سیستم + تعداد استفاده + حذف.

### وظایف مستقل (Task Actions / پیگیری وظایف)
- **`TaskItem`**: کاملاً مستقل از گردش‌کار ارجاع نامه؛ می‌تواند تنها باشد یا اختیاری به هر
  موجودیتی (نامه، مدرک و...) وصل شود (`RelatedType`/`RelatedId`، همان الگوی Notification).
- صفحه‌ی جدید **Tasks** با دو تب: **Action Items** (وظایفی که باید من انجام دهم) و **Follow-up**
  (وظایفی که من به دیگران محول کرده‌ام و پیگیری می‌کنم) - دقیقاً همان دو منوی جدای «اقدام وظایف» و
  «پیگیری وظایف» نمونه‌ی مرجع.
- اعلان خودکار به فرد مسئول هنگام محول‌شدن وظیفه (از همان `INotificationService` موجود).

### Migration

```
dotnet ef migrations add AddTagsAndTasks --project PEMS.Persistence --startup-project PEMS.Web
dotnet ef database update --project PEMS.Persistence --startup-project PEMS.Web
```

### باقی‌مانده از نمونه‌ی مرجع
Reports (گزارش‌ها)، Meeting Management (گفتگوها/جلسات)، و امضای دیجیتال واقعی هنوز ساخته نشده‌اند.

## Reports + Meeting Management + امضای الکترونیکی سبک

سه مورد باقی‌مانده از نمونه‌ی کارتابل مرجع، هرکدام با یک تصمیم شفاف در طراحی:

### Reports (گزارش‌ها)
صفحه‌ی جدید **Reports** - کاملاً از داده‌ی موجود Letter/LetterReferral/Tag ساخته شده، بدون Entity
جدید: حجم مکاتبات ۱۲ ماه اخیر (Incoming/Outgoing)، توزیع بر اساس دیسیپلین و وضعیت، پرکاربردترین
برچسب‌ها، پرتکرارترین فرستنده‌ها، میانگین زمان تکمیل ارجاع، و تعداد ارجاع‌های معوق - با Chart.js
(همان کتابخانه‌ی از قبل استفاده‌شده در Dashboard اصلی).

### Meeting Management (جلسات)
- **`Meeting`** + **`MeetingAttendee`**: دستورجلسه، صورت‌جلسه، حاضرین (داخلی یا بیرونی) با
  ردیابی حضور.
- **اقدامات پیگیری هر جلسه از همان `TaskItem`/`ITaskService` موجود استفاده می‌کنند** (با
  `RelatedType="Meeting"`) - به‌جای ساختن یک سیستم Task جدای دیگر، دقیقاً طبق اصل «از سرویس‌های
  موجود استفاده کن».

### امضای الکترونیکی سبک (نه PKI واقعی)
روی هر نامه‌ی Outgoing، یک فرم «Sign / Acknowledge» با تایپ نام کامل + مهر زمانی خودکار. این
**صریحاً یک تایید الکترونیکی ساده است، نه امضای دیجیتال رمزنگاری‌شده‌ی واقعی** - همین در خود
رابط کاربری هم نوشته شده تا کسی فکر نکند این حقوقاً معادل امضای دیجیتال واقعی (PKI) است.

### Migration

```
dotnet ef migrations add AddReportsAndMeetings --project PEMS.Persistence --startup-project PEMS.Web
dotnet ef database update --project PEMS.Persistence --startup-project PEMS.Web
```

### جمع‌بندی نهایی نسبت به نمونه‌ی مرجع (Pargar/Fonix)
با این سه مورد، تقریباً تمام موارد قابل‌شناسایی از تصویر اولیه (Portfolio، برچسب، ستاره،
To/Cc، ارجاع، وظایف، گزارش، جلسات) پوشش داده شدند. آنچه واقعاً باقی می‌ماند (و آگاهانه به این
سادگی قابل‌افزودن نیست) امضای دیجیتال PKI واقعی با گواهی سازمانی، و یکپارچگی با زیرساخت
دبیرخانه‌ی رسمی سازمانی (شماره‌گذاری رسمی نامه با مهر و امضای هیئت‌مدیره) است.

## ادیتور متن غنی برای بدنه‌ی نامه (نسخه‌ی کوچک‌شده‌ی Word)

طبق درخواست شما، متن ساده (`<textarea>`) برای بدنه‌ی نامه با یک ادیتور واقعی WYSIWYG جایگزین شد:

### چرا Quill.js (نه CKEditor)
بررسی کردم - CKEditor 4 از ژوئن ۲۰۲۳ به پایان عمر رسیده و نسخه‌های جدیدش (۴.۲۳ به بعد) نیاز به
License Key تجاری دارند. **Quill.js** رایگان، متن‌باز، فعال، و از همه مهم‌تر **پشتیبانی بومی و
رسمی از راست‌چین/چپ‌چین** دارد (نه یک افزونه‌ی دست‌ساز و تست‌نشده).

### امکانات (طبق درخواست "تمام امکانات نامه‌نگاری مدرن")
نوع فونت (Tahoma، Arial، Times New Roman، B Nazanin، Serif/Sans-serif/Monospace)، اندازه‌ی فونت
(۱۰ تا ۴۰ پیکسل)، Bold/Italic/Underline/Strikethrough، رنگ متن و پس‌زمینه، Subscript/Superscript،
عنوان‌بندی (H1-H3)، لیست‌های شماره‌دار/نقطه‌ای، Indent، **دکمه‌ی راست‌چین/چپ‌چین واقعی** (نه فقط
تراز بصری - جهت متن واقعی هر پاراگراف)، تراز (چپ/وسط/راست/Justify)، نقل‌قول، لینک، پاک‌کردن فرمت.
جهت پیش‌فرض ادیتور خودکار از زبان فعلی رابط کاربری (فارسی = راست‌چین) تنظیم می‌شود.

### ساخته شده به‌صورت یک پارشیال مشترک قابل‌استفاده‌ی مجدد
`_RichTextEditorScripts.cshtml` - نه فقط برای نامه؛ هر فرم دیگری در آینده (Meeting Minutes، Task
Description) هم می‌تواند همین را صدا بزند.

### امنیت (نکته‌ی مهم)
چون حالا کاربر HTML واقعی وارد می‌کند، خطر XSS واقعی است. **`HtmlSanitizer`** (کتابخانه‌ی معتبر و
پرکاربرد NuGet) قبل از ذخیره، هر Tag/Attribute غیرمجاز (مثل `<script>`) را حذف می‌کند - فقط
Tag های لازم برای فرمت‌بندی متن مجاز هستند.

### ویرایش بعد از ایجاد
در صفحه‌ی Details، دکمه‌ی «Edit» روی بدنه‌ی نامه، همان ادیتور را باز می‌کند - نه فقط هنگام ایجاد.

### محدودیت شفاف: PDF Export
QuestPDF (کتابخانه‌ی تولید PDF که قبلاً اضافه کردیم) قابلیت رندر HTML ندارد. برای PDF، بدنه‌ی
نامه به متن ساده (بدون فرمت‌بندی غنی) تبدیل می‌شود - یعنی رنگ/فونت/لیست در PDF نهایی نیست، فقط
متن خام. رندر کامل HTML در PDF نیاز به یک موتور رندر جدا (مثل wkhtmltopdf) دارد که فاز جداست.

### Migration
نیازی به Migration Schema ندارد (Body از قبل یک ستون string بود، فقط محتوایش حالا HTML است).
فقط بسته‌ی NuGet جدید نیاز به Restore دارد:
```
dotnet restore
```

## رفع باگ جدی: DbUpdateConcurrencyException هنگام ویرایش مدرک + بازطراحی کامل Edit

### ریشه‌ی واقعی باگ
وقتی `RowVersion` (Concurrency Token واقعی) به `BaseEntity` اضافه شد، هر فرم Edit که یک شیء کامل
از HTML Model-Bind می‌کرد و مستقیم `_context.Update(entity)` می‌زد، عملاً `RowVersion` را خالی
می‌فرستاد (چون فرم HTML چنین فیلدی نداشت) - EF Core این را «رکورد توسط کاربر دیگری تغییر کرده»
تفسیر می‌کرد و `DbUpdateConcurrencyException` می‌داد. **این دقیقاً هر بار رخ می‌داد، نه فقط در
تداخل واقعی دو کاربر.**

### رفع (الگوی درست، نه حذف Concurrency Protection)
`EngineeringDocumentRepository.UpdateAsync` و `VendorService.UpdateAsync` بازنویسی شدند: به‌جای
Attach کردن شیء جداشده‌ی فرم، رکورد واقعی و ردیابی‌شده را از دیتابیس می‌خوانند و فقط فیلدهای
قابل‌ویرایش را رویش کپی می‌کنند. `RowVersion` واقعی همراه همان رکورد ردیابی‌شده می‌ماند، پس
Concurrency Protection واقعی (برای تداخل واقعی دو کاربر) دست‌نخورده باقی می‌ماند.

### بازطراحی کامل Edit (طبق درخواست شما)
صفحه‌ی Edit مدرک مهندسی مثل Index قبلی، هرگز واقعاً بازطراحی نشده بود (۶ فیلد خام، بدون
هماهنگی بصری). حالا:
- هماهنگ با Create/Details (content-card، بخش‌بندی Basic Info / Engineering Info / Traceability).
- فیلدهای گم‌شده اضافه شدند: Project، Discipline، Area، Related MR.
- Status/Revision/Freeze **عمداً فقط‌خواندنی** نشان داده می‌شوند - این‌ها از طریق گردش‌کار
  (Submit/Approve/Reject/Create Revision) مدیریت می‌شوند، نه ویرایش مستقیم اینجا.
- بعد از ذخیره، به Details برمی‌گردد (نه لیست) با پیام موفقیت.

### باقی‌مانده: بررسی سایر Entity های دارای RowVersion
`Contract`, `Equipment`, `MaterialRequisition`, `ProcurementPackage` هم `RowVersion` دارند؛
جست‌وجوی کاملی زدم و **فقط همین دو مورد** (EngineeringDocument, Vendor) از الگوی پرریسک
`_context.Update(entity)` روی شیء جداشده استفاده می‌کردند - بقیه از الگوی درست (خواندن رکورد
ردیابی‌شده، تغییر فیلد خاص) استفاده می‌کنند.

### Migration
نیازی به Migration ندارد (فقط تغییر منطق سرویس/View).

## بررسی کامل چرخه‌ی مدرک مهندسی (از ثبت تا صدور) + دیدن فقط دیسیپلین خودی

### باگ‌های واقعی پیدا و رفع‌شده
1. **دیدن فقط مدارک دیسیپلین خودی**: `EngineeringDocumentsController`/`DocumentRegisterController` صفر
   فیلتر داشتند. طبق تجربه‌ی AVEVA/Aconex/ProjectWise: فقط نقش اجرایی (`DisciplineEngineer`) به
   دیسیپلین خودش محدود می‌شود؛ نقش‌های نظارتی (Manager/Supervisor/Document Controller/Admin)
   برای بررسی کامل MDR همچنان همه‌چیز را می‌بینند. اعمال شد روی: لیست، Document Register،
   Export Excel، **و دسترسی مستقیم به URL جزئیات** (تا کسی دور نزند).
2. **کامنت وندور در رویژن جدید وجود نداشت** - فیلد `VendorComment` اضافه شد.
3. **«توسط چه کاربر» در رویژن و در Approve/Reject اصلاً کار نمی‌کرد** (نه فقط رویژن) -
   `DocumentRevision.CreatedById` و `DocumentWorkflow.ActionById` از قبل روی Entity بودند ولی
   هیچ‌جا مقداردهی نمی‌شدند؛ در **دو مسیر تایید موازی** (صفحه‌ی مدرک + داشبورد سراسری Workflow)
   رفع شد.
4. **بعد از Approve هیچ اقدام «Issue» نبود** - اضافه شد (Approved → Issued + IssuedDate روی
   آخرین رویژن).
5. **رویژن قبلی هیچ‌وقت Superseded نمی‌شد** - رفع شد.

### Popup به‌جای صفحه‌ی جدا (طبق درخواست، با یک تصمیم آگاهانه)
Vendor، Contract، Equipment، Procurement Package، Meeting، Projects → Popup روی Index خودشان.
Engineering Document، Letter، Transmittal، MR/VDR (Legacy) عمداً صفحه‌ی جدا ماندند - طبق همان
الگویی که Aconex/ProjectWise هم برای فرم‌های پیچیده یا انتخاب از رجیستر بزرگ استفاده می‌کنند.

### Migration

```
dotnet ef migrations add DocumentLifecycleFixes --project PEMS.Persistence --startup-project PEMS.Web
dotnet ef database update --project PEMS.Persistence --startup-project PEMS.Web
```

## گردش‌کار سه‌مرحله‌ای واقعی تایید مدرک (Prepared → Checked → Approved)

طبق بررسی نهایی چرخه‌ی مدرک، سه شکاف واقعی رفع شد:

1. **مرحله‌ی Check مستقل**: قبلاً `PreparedBy`/`CheckedBy`/`ApprovedBy` فقط رشته‌ی متنی دستی بودند،
   بدون ارتباط با گردش‌کار. الان: `Submitted → Check → Checked → Approve → Approved → Issue`.
   این فیلدها دیگر دستی نیستند - خودکار از کاربر واقعی هر اقدام + تاریخ پر می‌شوند. اعمال شد
   در **هر دو مسیر تایید** (صفحه‌ی مدرک + صف سراسری Document Workflow) تا رگرسیون ایجاد نشود.
2. **اختیار تایید بر اساس دیسیپلین**: Engineering Supervisor فقط دیسیپلین خودش را می‌تواند
   تایید کند؛ Engineering Manager/Admin بدون محدودیت.
3. **تایید کارفرما جدا از تایید داخلی** (IFR در برابر IFA): بعد از Issue، فرم «Record Client
   Response» مستقل (`ClientApprovalStatus`/`ClientComment`) - وضعیت داخلی مدرک را تغییر نمی‌دهد.

### Migration

```
dotnet ef migrations add ThreeStageApprovalWorkflow --project PEMS.Persistence --startup-project PEMS.Web
dotnet ef database update --project PEMS.Persistence --startup-project PEMS.Web
```

## رفع: Dashboard با وضعیت واقعی مدارک هماهنگ نبود

### ریشه‌ی واقعی
ایمپورت MDR وضعیت‌هایی مثل `"Revise & Resubmit"` و `"Under Review"` تولید می‌کرد - رشته‌هایی
که **هیچ‌جای دیگر سیستم** (Dashboard، رنگ Badge، گردش‌کار Submit/Approve/Reject) نمی‌شناخت.
نتیجه: مدرکی که واقعاً "رد شده" بود، در هیچ شمارنده‌ای دیده نمی‌شد و حتی نمی‌شد از طریق
گردش‌کار عادی دوباره Submit شود (چون Submit فقط از Draft/Rejected مجاز است).

### رفع
- نگاشت `ComputeStatusFromCommentStatus` بازنویسی شد تا دقیقاً همان وضعیت‌های واقعی گردش‌کار
  را تولید کند: AP/NC → Approved، CO/AC/RE → Rejected (طبق تصمیم مستند «AC مثل CO رفتار
  می‌کند»)، W.F.C → Submitted.
- دکمه‌ی «Fix Status Values» در صفحه‌ی MDR Import برای اصلاح مدارک قبلاً ایمپورت‌شده با
  وضعیت اشتباه، بدون نیاز به ایمپورت دوباره.
- همه‌ی جاهایی که KPI بر اساس Status می‌ساختند (Dashboard اصلی، Widget شخصی، KPI صفحه‌ی
  Engineering Documents، Discipline Workspace، فیلتر سریع Document Register) به‌روز شدند تا
  وضعیت جدید `Checked` (از گردش‌کار سه‌مرحله‌ای) را هم بشمارند.

### Migration

```
dotnet ef migrations add DisciplineAliasesAndStatusFix --project PEMS.Persistence --startup-project PEMS.Web
dotnet ef database update --project PEMS.Persistence --startup-project PEMS.Web
```

## تکمیل نهایی این نشست

### جلوگیری از شماره‌ی تکراری رویژن
Index یکتا در دیتابیس روی `(DocumentId, RevisionNo)` + محاسبه‌ی شماره‌ی بعدی از بزرگ‌ترین
شماره‌ی واقعی موجود (نه فقط آخرین رکورد درج‌شده) + مدیریت زیبای خطای Race Condition.

### گزارش‌گیری VDR و Engineering Documents
ماژول Reports گسترش یافت با نوار تب: Correspondence | Engineering Documents | VDR - هرکدام
KPI + نمودار Chart.js مخصوص خودش.

### Comment Sheet / Reply Sheet استاندارد صنعت EPC
طبق استاندارد واقعی Technip/Saipem/Petrofac: شماره‌ی یکتای کامنت (C-001...)، Sheet/Zone/
Reference، Category، Severity، Required Action، Due Date، و پیوست فایل واقعی برای هم کامنت
هم پاسخ (Reply Sheet) - نه فقط متن دستی. هر دو بخش (افزودن کامنت، پاسخ به کامنت) اکنون Popup
هستند.

همچنین Client Transmittal No از VDR واقعاً به ماژول Transmittal وصل شد (TransmittalItem.Document
به هسته‌ی مشترک Document عمومی شد تا هم مدارک مهندسی هم اقلام VDR بپذیرد)، و MR/MRQ خودکار از
MDR/VDR Import ساخته می‌شوند (با شماره‌ی مشترک، طبق تایید شما).

### Migration نهایی

```
dotnet ef migrations add FinalUpdatesReportsCommentsUniqueRevision --project PEMS.Persistence --startup-project PEMS.Web
dotnet ef database update --project PEMS.Persistence --startup-project PEMS.Web
```

## این نشست: سه ویژگی Enterprise + اصلاحات واقعی

### Toast Notifications + SignalR + PDF Pin Comments
- Toast متمرکز در Layout (۳۰+ بنر تکراری صفحه حذف شدند).
- SignalR با معماری درست (`INotificationBroadcaster` در Application، پیاده‌سازی در Web - نه
  وابستگی برعکس Persistence به Web).
- PDF.js با Pin Comment - هم در VDR، هم حالا در Engineering Documents.

### یکپارچه‌سازی سرویس کامنت
`IVDRCommentService` به `IDocumentCommentService` تغییر نام یافت (کار روی جدول مشترک
`DocumentComment` از اول هم برای VDR هم Engineering Document بود، فقط اسمش گمراه‌کننده بود).
حالا **مدارک مهندسی هم دقیقاً همان چرخه‌ی کامنت/ریپلای/پیوست/Pin که VDR دارد را دارند**.

### رفع باگ‌های واقعی گزارش‌شده
- **رویژن تکراری VDR**: هم در سرویس هم Index یکتای دیتابیس مسدود شد (مثل قبلاً برای مدرک
  مهندسی). شماره Transmittal هم اجباری شد.
- **تطبیق ستون‌های ایمپورت**: هم MDR هم VDR اکنون نام‌های رایج بیشتری برای Comment Sheet/
  Transmittal را می‌شناسند (مطابقت دقیق قبلی خیلی از فایل‌های واقعی را نمی‌شناخت).
- **گم‌شدن پین‌های PDF**: یک باگ Casing واقعی (camelCase در برابر PascalCase در JSON) قبل از
  تحویل پیدا و رفع شد.
- **لیست مدارک Transmittal**: فیلتر خودکار بر اساس پروژه + جست‌وجوی زنده + شمارنده، به‌جای
  نمایش همه‌ی مدارک سیستم بدون فیلتر.

### Migration نهایی این نشست

```
dotnet ef migrations add CommentSheetPinCommentsAndFixes --project PEMS.Persistence --startup-project PEMS.Web
dotnet ef database update --project PEMS.Persistence --startup-project PEMS.Web
```

## این نشست: Disciplines، جداول طولانی، Equipment Import (اکسل + PDF)

- **Disciplines** از Administration به Engineering Management منتقل شد.
- **باگ واقعی رفع شد**: لینک‌های «Documents» و «VDR Items» در Discipline Workspace متن ساده
  بودند (نه Link) - حالا مثل Equipment که درست بود، کلیک‌پذیرند.
- **جداول طولانی**: Discipline Workspace، Employee، VDR اصلی حالا در پنجره‌ی محدود با اسکرول
  عمودی هستند (نه کشیدن کل صفحه).
- **Equipment Import (CMMS)**: بر اساس بررسی واقعی دو فایل نمونه ساخته شد.
  - **اکسل**: تشخیص خودکار سطر Header (فایل Mechanical شما هدر را در ردیف ۲ داشت، نه ۱ -
    این باگ واقعی پیدا و رفع شد)، تطبیق ستون‌های منعطف بین قالب‌های مختلف (Mechanical/
    Instrument)، اتصال خودکار MR (با همان `MaterialRequisitionLinker` مشترک).
  - **PDF**: یک تشخیص «بهترین تلاش» شماره‌تگ (نه استخراج کامل جدول) - متن PDF با OCR/PdfPig
    موجود پروژه استخراج و دنبال الگوی تگ صنعتی (`XX-XXX-######`) می‌گردد؛ نتیجه برای بازبینی
    و انتخاب کاربر نشان داده می‌شود، نه ایمپورت خودکار کور.

### Migration

```
dotnet ef migrations add EquipmentImportAndFixes --project PEMS.Persistence --startup-project PEMS.Web
dotnet ef database update --project PEMS.Persistence --startup-project PEMS.Web
```

## این نشست: تکمیل CMMS (Maintenance Strategy/Plan، Work Order، Spare Parts)

### یافته‌ی مهم قبل از شروع
`WorkOrder`، `MaintenancePlan`، `SparePart`، `MaintenanceStrategy`، `MaintenanceSpareUsage` از
قبل به‌عنوان Entity کامل در پروژه وجود داشتند (Migration هم قبلاً اجرا شده بود) - فقط هیچ
Controller/View مدیریتی نداشتند. کار این نشست، وصل‌کردن رابط کاربری به مدل داده‌ی از قبل
موجود بود، نه طراحی مدل جدید.

### ساخته شد
- **Maintenance Strategy**: تعریف نوع نگهداری (Preventive/Predictive/Corrective/CBM) به‌ازای
  هر تجهیز.
- **Maintenance Plan**: برنامه‌ی زمان‌بندی‌شده با شماره‌ی خودکار (`PM-{Tag}-{seq}`)، تاریخ
  سررسید، هایلایت Overdue، و دکمه‌ی «Generate Work Order».
- **Work Order**: گردش‌کار کامل `Open → Assigned → InProgress → Completed → Closed`، شماره‌ی
  خودکار (`WO-{Year}-{seq}`)، KPI بالای صفحه، ثبت قطعات یدکی مصرف‌شده با هزینه.
- **Spare Parts**: کاتالوگ قطعات + شمارنده‌ی «چند بار استفاده شده» در Work Order ها.
- دکمه‌ی «New Work Order» به صفحه‌ی Equipment Details اضافه شد؛ تاریخچه‌ی نگهداری حالا Link
  واقعی به جزئیات Work Order دارد (قبلاً متن ساده بود).

### تصمیم آگاهانه
لینک «Asset Register» عمداً حذف شد (نه ساخته شد) - چون Equipment از قبل دقیقاً همین کار را
می‌کند؛ ساختن صفحه‌ی موازی فقط سردرگمی ایجاد می‌کرد.

### محدودیت شفاف
`SparePart` فعلاً فقط کاتالوگ قطعات است، موجودی انبار (Quantity in Stock/Reorder Level) ندارد.

### Migration
نیازی به Migration جدید ندارد (همه‌ی Entity ها و جدول‌هایشان از قبل در دیتابیس موجودند - فقط
Controller/View اضافه شد).

## این نشست: رفع باگ Chat Assistant (بدون پاسخ) + بهینه‌سازی برای سخت‌افزار ضعیف

### باگ واقعی رفع‌شده
`ChatAssistantService.AskAsync` فقط تماس نهایی با Ollama را Try/Catch می‌کرد؛ اگر خطایی در
مراحل قبلی (Relationship Engine یا Knowledge Base Search) رخ می‌داد، کل درخواست با خطای خام
سرور (۵۰۰) شکست می‌خورد و **هیچ پیامی به کاربر نمی‌رسید**. الان کل مسیر محافظت می‌شود.

### بهینه‌سازی برای سخت‌افزار ضعیف (طبق گزارش واقعی کاربر)
- `num_predict` (سقف طول پاسخ) به مدل Ollama اضافه شد - چت الان حداکثر ~۳۰۰ توکن می‌خواهد.
- نتایج Semantic Search از ۵ به ۳ کاهش یافت (کانتکست کوچک‌تر = سریع‌تر).
- UI حین انتظار صادق‌تر شد: هر ۱۵ ثانیه پیام «همچنان در حال پردازش...» به‌روزرسانی می‌شود.
- **تصمیم آگاهانه**: Timeout مشترک ۱۵ دقیقه‌ای دست‌نخورده ماند - چون بین چت و پردازش OCR/
  خلاصه‌سازی مدارک بزرگ مشترک است؛ کوتاه‌کردنش می‌توانست آن پردازش را خراب کند.

### باقی‌مانده (تصمیم با شماست)
اگر با این بهینه‌سازی‌ها هم هنوز کند است، تنها راه‌حل واقعی تعویض به مدل کوچک‌تر است (مثلاً
`qwen2.5:1.5b` به‌جای `3b` در `appsettings.json` → `Ollama:Model`).

### پیشنهاد بعدی (هنوز اجرا نشده - منتظر تایید شما)
گسترش همین دستیار برای پاسخ به سوالات «چطور کار می‌کند» (نه فقط داده‌ی مهندسی) با افزودن
مقالات راهنما به همان جدول Knowledge Base - منتظر مشخص‌کردن دامنه‌ی راهنما هستم.

## این نشست: رفع باگ‌های واقعی + بازنگری کامل UI + استخراج MRQ/VDR از فایل تجهیزات

### رفع باگ‌های واقعی گزارش‌شده
- **FK Violation در Equipment Import**: همان کلاس باگ UserId/EmployeeId، این‌بار در Equipment/
  VDR/MDR Import (سه‌جا) - همه با `GetCurrentEmployeeIdAsync()` رفع شدند.
- **رویژن تکراری VDR**: علتش برخورد شماره‌ی بلوک fallback با رویژن صریح بود - قبل از Commit
  ادغام می‌شوند؛ محافظت سطح ردیف هم برای داده‌ی قدیمی اضافه شد.
- **زبان فارسی کار نمی‌کرد**: باگ کلاسیک ASP.NET Core - کلاس SharedResource هم‌نام با پوشه‌ی
  Resources بود، مسیر جست‌وجو دوبار تکرار می‌شد. به فضای‌نام ریشه منتقل شد.
- **دارک مود - جداول سفید**: قانون Dark Mode فقط رنگ متن را عوض می‌کرد، نه پس‌زمینه (و متغیرهای
  داخلی Bootstrap 5) - رفع شد.
- **ردیابی «چه کسی آپلود کرد»**: فیلد UploadedById واقعاً اضافه و متصل شد.

### بازنگری سیستماتیک UI (نه سطحی)
با اسکریپت روی کل پروژه: **۴۴ صفحه** (۳۶+ جدول) بدون محدودیت ارتفاع/اسکرول پیدا و یکجا با
همان الگوی یکسان (۶۵vh + اسکرول) رفع شدند. نتیجه در کل لایه‌ی View: صفر مشکل ساختاری.

### بخش تنظیمات
صفحه‌ی «Settings» یکپارچه - My Preferences (زبان/داشبورد/تم) + System Administration (پیوند
سازمان‌یافته به Users/Departments/Employees/Permission Matrix/Roles/Audit Log/Routing Rules).

### گزارش‌گیری مستقل هر بخش
۵ گزارش با فیلترهای مخصوص خودشان: Correspondence، Engineering، VDR، **Procurement** (جدید)،
**CMMS** (جدید).

### استخراج MRQ و ساخت خودکار VDR از فایل اکسل تجهیزات (تست‌شده روی فایل واقعی شما)
از فایل Mechanical: ۹۳۱ قلم VDR واقعی. از فایل Instrument: ۸۷۶ (بعد از رفع یک باگ کیفیت داده
که در همین تست پیدا شد - ۹۷ مقدار «Vendor» که واقعاً شماره‌ی Tag/بسته بودند، نه نام شرکت).

### Migration نهایی

```
dotnet ef migrations add FinalUIAndDataQualityFixes --project PEMS.Persistence --startup-project PEMS.Web
dotnet ef database update --project PEMS.Persistence --startup-project PEMS.Web
```
