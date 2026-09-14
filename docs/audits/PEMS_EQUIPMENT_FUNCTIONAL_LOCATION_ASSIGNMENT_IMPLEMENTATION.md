# PEMS CMMS — Equipment ↔ Functional Location Assignment & Import

**تاریخ:** 2026-09-14

---

## A. Baseline

- **HEAD before:** `6df8ddb464cfa6bc32e3e9a1eaf3cf1415acf251`
- **HEAD after:** همان (بدون Commit - طبق محدودیت صریح دستور، هیچ تغییری Commit نشد)
- **Branch:** `main`
- **Working tree پیش از این فاز:** `VDR/Details.cshtml` (فاز ۲C-۴B، دست‌نخورده ماند)،
  `ResourceHeaderViewModel.cs`/`_PemsResourceHeader.cshtml` (فاز ۲C-۴B، دست‌نخورده ماندند)،
  به‌همراه فایل‌های مستندسازی فازهای قبلی و آیتم‌های نامرتبط از‌پیش‌موجود
  (`.claude/Role/...`, `src/stitch-temp/` حذف‌شده، آرتیفکت‌های Runtime در `filestore`/
  `uploads`) - هیچ‌کدام در این فاز دست نخوردند.

## B. معماری موجود که مجدداً استفاده شد (نه اختراع‌شده)

ممیزی الزامی قبل از پیاده‌سازی، این یافته‌ی حیاتی را آشکار کرد که مستقیماً روی دامنه‌ی این
فاز اثر گذاشت:

1. **`Equipment.FunctionalLocationId`/`FunctionalLocationRef` از قبل وجود داشت** (FK واقعی،
   `DeleteBehavior.Restrict`) و **`EquipmentController.AssignFoundation` +
   `EquipmentService.AssignFoundationAsync` از قبل تخصیص تکی را کامل پیاده‌سازی کرده بودند** -
   شامل گیت مجوز (`Equipment`/`Edit`، از فاز امنیتی همین نشست) و جلوگیری از حلقه در
   ساختار Parent/Child. **این فاز آن را از صفر نساخت - فقط یک نقص کوچک را رفع کرد** (بخش D).
2. **الگوی Staging→Review→Commit دقیقاً سه‌بار قبلاً پیاده‌سازی شده بود**
   (`FunctionalLocationImportBatch`/`Row` + `IFunctionalLocationImportService`؛
   `EquipmentImportBatch`/`Row` + `IEquipmentImportService`؛ همون الگو در MDR/MRS Import هم).
   این فاز **دقیقاً همین الگو را برای Batch/Row جدید تکرار کرد** (بخش C) - نه یک
   `GenericImportEngine`/`GenericAssignmentEngine`.
3. **`IFunctionalLocationService.GetPickerListAsync`/`GetBreadcrumbAsync`** از قبل مسیر کامل
   "Plant / Area / .../ Code" را حل می‌کردند - منطق مشابه (نه فراخوانی مستقیم سرویس، به‌دلیل
   نیاز به Resolve دسته‌جمعی چند شناسه در هر صفحه، نه یکی‌یکی) در سرویس جدید بازتولید شد.
4. **`AuditLog` یک Interceptor سراسری و خودکار در `PEMSDbContext.SaveChangesAsync` است** -
   هر Insert/Update/Delid از طریق `ChangeTracker` بدون نیاز به کد اضافه Audit می‌شود. **هیچ
   کد Audit دستی در این فاز نوشته نشد** - `equipment.FunctionalLocationId = ...` +
   `SaveChangesAsync()` به‌تنهایی یک ردیف `AuditLog` واقعی (`EntityName="Equipment"`,
   `ChangedFieldsSummary="FunctionalLocationId: X -> Y"`, `UserId`/`UserName`/`Timestamp`)
   تولید می‌کند - تأیید مستقیم با تست (بخش I).
5. **`IPermissionService`/`PermissionService.SeedDefaultsAsync`** - ماژول جدید
   `"EquipmentFunctionalLocationAssignment"` با همون نقش‌هایی که `EquipmentImport`/
   `FunctionalLocationImport` از قبل داشتند (`MaintenanceEngineer`/`EngineeringManager`:
   Create/Delete/Approve؛ `Viewer`: فقط View) اضافه شد - نه یک مکانیزم مجوز جدید.
6. **ClosedXML (`XLWorkbook`)** - همون کتابخانه‌ی Excel که `FunctionalLocationImportService`/
   `EquipmentImportService` از قبل استفاده می‌کردند.
7. **`DataGridPageSize`/`PagedResult<T>`/`_Pagination.cshtml`/`PaginationViewModel`** -
   همون زیرساخت صفحه‌بندی موجود، برای صفحه‌ی بازبینی در مقیاس تجهیزات (۲۱٬۶۲۵+).

**نتیجه:** هیچ‌کدام از شروط توقف بند ۲۹ سند وظیفه واقعاً رخ نداد - یافته‌ی بند ۱ («Equipment
از قبل رابطه‌ی FL مناسب دارد») **بخشی از دامنه را کوچک کرد** (تخصیص تکی نیازی به پیاده‌سازی
جدید نداشت)، نه این‌که کل فاز را مسدود کند؛ بخش باقی‌مانده و واقعاً غایب (ایمپورت دسته‌ای
پیشنهاد-محور با بازبینی/تایید صریح) هیچ پیاده‌سازی موجودی نداشت.

## C. جریان کاری تخصیص (Assignment Workflow)

```
Excel Upload (Equipment Tag, Functional Location Code, [Plant/Area/Unit اختیاری])
    ↓
Parse (ClosedXML + تشخیص خودکار سطر Header)
    ↓
Resolve (پیش‌بارگذاری یک‌باره‌ی Equipment/FunctionalLocation فعال - نه Query به‌ازای هر سطر)
    ↓
Suggestion (هر سطر Excel → یک EquipmentFunctionalLocationAssignmentRow با وضعیت قطعی)
    ↓
Review Grid (صفحه‌بندی‌شده‌ی سمت-SQL + فیلتر بر اساس وضعیت)
    ↓
Engineer/User Approval (IncludeInCommit به‌ازای هر سطر + ReplaceExistingAssignment جدا برای تخصیص‌های موجود)
    ↓
Commit (فقط سطرهای تایید‌شده - یک SaveChangesAsync واحد، نه به‌ازای هر سطر)
    ↓
Audit (خودکار - PEMSDbContext.SaveChangesAsync Interceptor)
```

هیچ مرحله‌ای در این زنجیره بدون بازبینی/تایید صریح مستقیماً جدول `Equipment` را تغییر
نمی‌دهد - `StageFromExcelAsync` فقط ردیف Staging می‌سازد، هرگز `Equipment.FunctionalLocationId`
واقعی را لمس نمی‌کند.

## D. قواعد تطبیق (Matching Rules) - فقط قطعی، بدون Fuzzy/AI

طبق بند صریح «Fuzzy / AI Suggestions» سند وظیفه: هیچ زیرساخت تطبیق فازی/AI مناسبی برای
Tag↔Code در مخزن پیدا نشد (سرویس‌های AI موجود - مثل KnowledgeBase Semantic Search - روی
مدارک مهندسی کار می‌کنند، نه تگ تجهیز)؛ طبق دستور صریح («اگر زیرساخت مناسبی وجود ندارد، برای
همین قابلیت یکی نساز»)، **هیچ تطبیق فازی/AI پیاده‌سازی نشد** - فقط تطبیق قطعی:

1. **Equipment Tag دقیق** (Case-Insensitive، Trim‌شده) روی `Equipment.TagNumber` فعال.
2. **Functional Location Code دقیق** (همون قاعده) روی `FunctionalLocation.Code` فعال.
3. **اعتبارسنجی زمینه‌ای اختیاری** (Plant/Area/Unit) - فقط اگر آن ستون‌ها در فایل باشند و با
   مقدار واقعی FL تطبیق‌یافته ناسازگار باشند.

هر پیشنهاد یک وضعیت قطعی و قابل‌توضیح دارد (نه امتیاز عددی):

| وضعیت | معنی |
|---|---|
| `Exact` | تطبیق قطعی هر دو طرف؛ تجهیز فعلاً هیچ FL ندارد - آماده‌ی Commit |
| `NoChange` | تجهیز از قبل دقیقاً همین FL را دارد - Idempotent، نیازی به تغییر نیست |
| `AlreadyAssigned` | تجهیز از قبل یک FL **متفاوت** دارد - پیش‌فرض محافظت‌شده |
| `UnresolvedEquipment` | هیچ تجهیزی با این Tag پیدا نشد |
| `UnresolvedFunctionalLocation` | هیچ FL ای با این Code پیدا نشد |
| `ConflictEquipment` | چند تجهیز با همین Tag پیدا شد (چون Schema فعلی Uniqueness ندارد) |
| `ConflictFunctionalLocation` | چند FL با همین Code پیدا شد |
| `ValidationError` | فیلد الزامی خالی، یا ناسازگاری زمینه‌ای (Plant/Area/Unit) |

## E. قواعد تعارض و محافظت از تخصیص موجود

- `AlreadyAssigned` هرگز پیش‌فرض `IncludeInCommit=true` نمی‌گیرد - فقط با فراخوانی صریح و
  جدای `ReplaceExisting` (که خودش مجوز `Approve` می‌خواهد، نه فقط `Create`) فعال می‌شود.
- `SetRowIncludeAsync` صریحاً هر تلاش برای روشن‌کردن یک سطر `Unresolved*`/`Conflict*`/
  `ValidationError` را رد می‌کند - این وضعیت‌ها فقط با اصلاح فایل منبع و Import دوباره قابل
  حل‌اند، نه با یک کلیک روی صفحه‌ی بازبینی.
- `CommitBatchAsync` وضعیت واقعی تجهیز را **دوباره در لحظه‌ی Commit** (نه فقط عکس فوری زمان
  Staging) بررسی می‌کند - اگر بین Review و Commit یک مسیر دیگر همان تجهیز را تغییر داده
  باشد، رفتار محافظتی همچنان اعمال می‌شود.
- تخصیص تکی موجود (`AssignFoundation`) اکنون تخصیص فعلی را صریحاً نشان می‌دهد
  (`Currently: FINISHING / 85A07`) - قبلاً فقط به‌صورت ضمنی از طریق گزینه‌ی از‌قبل‌انتخاب‌شده‌ی
  Select دیده می‌شد؛ این یک اصلاح مستندسازی‌شده‌ی کوچک بود (بخش K).

## F. مجوز

ماژول `"EquipmentFunctionalLocationAssignment"` به `PermissionService.SeedDefaultsAsync`
اضافه شد - **دقیقاً همون نقش‌هایی که `EquipmentImport` از قبل داشت**:
`MaintenanceEngineer`/`EngineeringManager` → Create/Delete/Approve؛ `Viewer` → فقط View.
تصمیم صریح: یک ماژول جدا (نه استفاده‌ی مجدد از خودِ `"EquipmentImport"`) چون این پایپ‌لاین
مفهوماً مستقل است (تخصیص رابطه‌ی موجود، نه ساخت Equipment جدید) - دقیقاً همون قاعده‌ای که
MDR/Equipment/MRS/FunctionalLocation Import هرکدام ماژول جدای خودشان را دارند. Commit و
ReplaceExisting هردو `Approve` می‌خواهند (پرریسک‌ترین اکشن‌ها)؛ Upload/ToggleRow نیاز به
`Create` دارند؛ Discard نیاز به `Delete` دارد؛ Index/Review فقط `View`. تخصیص تکی
(`AssignFoundation`) دست‌نخورده ماند - همچنان `Equipment`/`Edit` می‌خواهد.

## G. یکپارچگی داده (Data Integrity)

- هیچ Equipment/FunctionalLocation جدیدی هرگز ساخته نمی‌شود - فقط FK موجود به‌روز می‌شود.
- بدون Cascade Delete نامناسب - `EquipmentFunctionalLocationAssignmentRow→Batch` تنها رابطه‌ی
  Cascade است (حذف یک Batch، فقط سطرهای Staging خودش را حذف می‌کند، هرگز Equipment/FL را).
- بدون `foreach { SaveChanges() }` - `CommitBatchAsync` یک `SaveChangesAsync` واحد در پایان
  دارد؛ تمام تغییرات همان Batch در یک Transaction اتمی EF Core اعمال می‌شوند.

## H. Idempotency

آزمون مستقیم (بخش I، `CommitBatchAsync_RepeatedImport_SecondRunIsNoOp`): آپلود دوباره‌ی همون
فایل بعد از Commit اول → دومین Stage آن سطر را `NoChange` تشخیص می‌دهد (نه `Exact` دوباره)،
و دومین Commit صفر تخصیص جدید ثبت می‌کند - بدون رکورد تکراری، بدون نویز Audit اضافی (چون
`equipment.FunctionalLocationId` اصلاً دست‌نخورده می‌ماند وقتی از قبل برابر مقدار پیشنهادی
است - `ChangeTracker` هرگز آن Property را Modified علامت نمی‌زند).

## I. تست‌ها

| فایل | تعداد |
|---|---|
| `PEMS.Tests/Cmms/EquipmentFunctionalLocationAssignmentServiceTests.cs` | ۱۷ |
| `PEMS.Tests/Authorization/EquipmentFunctionalLocationAssignmentControllerAuthorizationTests.cs` | ۱۰ |
| **جمع تست‌های جدید** | **۲۷** |

پوشش: تطبیق دقیق (Exact/NoChange)، عدم‌یافتن (Unresolved×۲)، تعارض (Conflict×۲)، فیلد خالی
(ValidationError)، محافظت از تخصیص موجود (AlreadyAssigned پیش‌فرض محافظت‌شده + فقط با تایید
صریح جایگزین می‌شود)، تایید بخشی (Partial Approval)، رد قطعی سطرهای نامعتبر حتی با تلاش
اجباری، Idempotency، Discard بدون اثر روی Equipment، و **تأیید مستقیم تولید خودکار AuditLog**
- بدون هیچ زیرساخت تست جدید (همون الگوی `PemsDbContextFactory.CreateInMemory` +
`FakeCurrentUserService` که در کل این نشست استفاده شده).

## J. رگرسیون

```
PEMS.Web build:    0 Errors, 0 Warnings
PEMS.Tests build:  0 Errors, 3 Warnings (همه از‌قبل‌موجود، بی‌ربط)
Targeted:          27/27
Full Regression:   1239/1239 (Passed: 1239, Failed: 0, Skipped: 0, Duration: 9m 5s)
```

مطابقت دقیق: ۱۲۱۲ Baseline (پایان فاز ۲C-۴B) + ۲۷ تست جدید این فاز = **۱۲۳۹**. صفر شکست.

## K. موارد موکول‌شده (واقعاً موکول، نه اختراع‌شده)

- **یافته‌ی Equipment `bg-success` هاردکد** (از فاز ۲C-۴A/۴C) - این فاز به آن دست نزد؛
  خارج از دامنه‌ی صریح این وظیفه بود.
- **ادغام Resource Header** - عمداً استفاده نشد؛ کارگاه Resource Header بسته ماند (طبق فاز
  ۲C-۴C)؛ صفحات این فاز از همون معماری موجود Equipment Details/Import پیروی کردند.
- **UI بازبینی بصری واقعی (مرورگر)** - بدون مرورگر در این محیط؛ اعتبارسنجی فقط سطح Build/
  Test/بازرسی مستقیم Markup انجام شد - صادقانه اعلام می‌شود، نه ادعای بیش‌ازحد.
- **Index مجزا روی `Equipment.TagNumber`/`FunctionalLocation.Code`** - عمداً اضافه نشد؛ طبق
  محدودیت صریح («فقط اگر Schema واقعاً کم دارد Migration بساز»)، الگوی اثبات‌شده‌ی
  پیش‌بارگذاری کامل به یک Dictionary در حافظه (همون الگوی `FunctionalLocationImportService`)
  کافی تشخیص داده شد؛ در صورت رشد آینده‌ی حجم Equipment، این یک آیتم بازبینی جداگانه است.

---

## EQUIPMENT ↔ FUNCTIONAL LOCATION ASSIGNMENT STATUS

```
Repository:
PEMS-cmms-edit-empoleeys-master

Branch:
main

HEAD Before:
6df8ddb464cfa6bc32e3e9a1eaf3cf1415acf251

HEAD After:
6df8ddb464cfa6bc32e3e9a1eaf3cf1415acf251 (uncommitted working-tree changes only)

Working Tree:
New Domain/Application/Persistence/Web files added; PEMSDbContext, Program.cs,
PermissionService.cs, _SidebarMenu.cshtml, Equipment/Details.cshtml modified; one new EF
migration created and applied to the local dev database

Architecture Audit:
PASS

Single Equipment Assignment:
COMPLETE (pre-existing AssignFoundation flow, enhanced with an explicit "Currently: <FL>" hint)

Excel Import:
COMPLETE

Suggestion Layer:
COMPLETE

Engineer Review:
COMPLETE

Partial Approval:
COMPLETE

Existing Assignment Protection:
PASS

Idempotency:
PASS

Audit Trail:
PASS (automatic via PEMSDbContext.SaveChangesAsync interceptor - verified by test)

Authorization:
PASS

Database/Schema Changes:
Two new tables: EquipmentFunctionalLocationAssignmentBatches,
EquipmentFunctionalLocationAssignmentRows (migration
20260914074312_AddEquipmentFunctionalLocationAssignment, applied)

Application Files Changed:
PEMS.Domain/Entities/EquipmentFunctionalLocationAssignmentBatch.cs (new)
PEMS.Domain/Entities/EquipmentFunctionalLocationAssignmentRow.cs (new)
PEMS.Application/DTOs/EquipmentFunctionalLocationAssignmentDtos.cs (new)
PEMS.Application/Interfaces/IEquipmentFunctionalLocationAssignmentService.cs (new)
PEMS.Persistence/Servises/EquipmentFunctionalLocationAssignmentService.cs (new)
PEMS.Persistence/Context/PEMSDbContext.cs (modified: 2 DbSets + relationship config)
PEMS.Persistence/Servises/PermissionService.cs (modified: new module seed)
PEMS.Web/Program.cs (modified: DI registration)
PEMS.Web/Controllers/EquipmentFunctionalLocationAssignmentController.cs (new)
PEMS.Web/Views/EquipmentFunctionalLocationAssignment/Index.cshtml (new)
PEMS.Web/Views/EquipmentFunctionalLocationAssignment/Review.cshtml (new)
PEMS.Web/Views/Shared/_SidebarMenu.cshtml (modified: one new nav link)
PEMS.Web/Views/Equipment/Details.cshtml (modified: "Currently: <FL>" hint in AssignFoundation modal)
PEMS.Tests/Cmms/EquipmentFunctionalLocationAssignmentServiceTests.cs (new)
PEMS.Tests/Authorization/EquipmentFunctionalLocationAssignmentControllerAuthorizationTests.cs (new)

Migration:
PEMS.Persistence/Migrations/20260914074312_AddEquipmentFunctionalLocationAssignment.cs
(created and applied to local dev DB via dotnet ef database update)

Targeted Tests:
27/27

Full Regression:
1239/1239

Documentation:
docs/audits/PEMS_EQUIPMENT_FUNCTIONAL_LOCATION_ASSIGNMENT_IMPLEMENTATION.md

Data Destructive Operations:
NONE

Commit:
NO

Push:
NO

Overall Status:
COMPLETE
```
