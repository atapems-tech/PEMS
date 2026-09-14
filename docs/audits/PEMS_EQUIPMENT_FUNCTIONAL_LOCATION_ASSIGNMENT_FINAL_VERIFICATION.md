# PEMS — Equipment ↔ Functional Location Assignment: Final Integrity Verification

**تاریخ:** 2026-09-14

## 1. نتیجه

```
PASS — WORKSTREAM CLOSED
```

## 2. Migration

- **نام:** `20260914074312_AddEquipmentFunctionalLocationAssignment`
- **جداول:** `EquipmentFunctionalLocationAssignmentBatches`,
  `EquipmentFunctionalLocationAssignmentRows` - هردو تأیید‌شده در `dotnet ef migrations list`
  (آخرین مورد در یک تاریخچه‌ی پیوسته و بدون شکاف) و در خودِ دیتابیس (کوئری مستقیم:
  `__EFMigrationsHistory` شامل این Migration است).
- **بررسی مخرب بودن:** `Up()` فقط شامل دو `CreateTable` + دو `CreateIndex` است - **هیچ**
  `AlterTable`/`DropColumn`/`AlterColumn` روی هیچ جدول موجودی وجود ندارد. `Equipment`/
  `FunctionalLocation` و هر جدول دیگر کاملاً دست‌نخورده مانده‌اند.
- **FK:**
  - `EquipmentFunctionalLocationAssignmentBatches.ImportedById → Employees.EmployeeId`
    (Restrict، Nullable).
  - `EquipmentFunctionalLocationAssignmentRows.EquipmentFunctionalLocationAssignmentBatchId → EquipmentFunctionalLocationAssignmentBatches.Id`
    (Cascade - حذف یک Batch فقط سطرهای Staging خودش را حذف می‌کند).
  - تأیید مستقیم در دیتابیس: دقیقاً ۲ Foreign Key روی این دو جدول جدید وجود دارد - نه
    کم‌تر، نه بیشتر از انتظار.
- **یافته‌ی صریح (نه یک نقص، بلکه یک تصمیم آگاهانه‌ی هم‌راستا با الگوی موجود):**
  `ResolvedEquipmentId`/`ResolvedFunctionalLocationId`/`PreviousFunctionalLocationId` روی
  `...Rows` **در سطح دیتابیس FK ندارند** - عیناً همون الگوی از‌قبل‌موجود
  `EquipmentImportRow.CreatedEquipmentId`/`FunctionalLocationImportRow.CreatedFunctionalLocationId`
  (که هردو هم بدون FK صریح‌اند - تأیید مستقیم با `grep` در `PEMSDbContext.OnModelCreating`).
  محافظت از ارجاع به رکورد ناموجود در لایه‌ی اپلیکیشن انجام می‌شود (`Resolve()`/`CommitBatchAsync`
  همیشه این مقادیر را فقط از نتیجه‌ی Query واقعی روی `Equipments`/`FunctionalLocations`
  می‌نویسند، هرگز از ورودی مستقیم کاربر). این دقیقاً همون سطح تضمینی است که دو پایپ‌لاین
  Import موجود از قبل دارند - این فاز چیزی ضعیف‌تر از الگوی موجود نساخت.

## 3. یکپارچگی داده (Data Integrity Smoke Test)

هر ۶ سناریوی خواسته‌شده از قبل با تست واقعی (نه ادعای بدون‌شاهد) پوشش داده شده‌اند:

| سناریو | تست | نتیجه |
|---|---|---|
| ۱. Equipment بدون FL → تایید → FK معتبر | `CommitBatchAsync_ExactRow_AssignsEquipment` | PASS |
| ۲. Equipment با FL موجود → Import → محافظت‌شده مگر تایید صریح | `CommitBatchAsync_AlreadyAssignedRow_NotReplacedWithoutExplicitApproval` + `_ReplacedOnlyAfterExplicitApproval` | PASS |
| ۳. تخصیص تکراری → بدون تخصیص منطقی دوگانه | `CommitBatchAsync_RepeatedImport_SecondRunIsNoOp` | PASS |
| ۴. Equipment نامعتبر → غیرقابل‌Commit | `CommitBatchAsync_UnresolvedRow_NeverCommitted_EvenIfIncludeForced` | PASS |
| ۵. FunctionalLocation نامعتبر → غیرقابل‌Commit | پوشش سطح Staging (`StageFromExcelAsync_FunctionalLocationNotFound_UnresolvedFunctionalLocation`) + همون گارد دفاعی مشترک در `CommitBatchAsync`/`SetRowIncludeAsync` که سناریوی ۴ را هم پوشش می‌دهد (یک مسیر کد مشترک برای هر دو `Unresolved*`) - **یادداشت صادقانه:** یک تست واحد و جدا در سطح Commit مخصوص همین حالت نوشته نشده بود؛ پوشش از طریق مسیر کد مشترک تضمین می‌شود، نه یک Assertion مستقل تکراری | PASS (با این یادداشت) |
| ۶. تایید بخشی | `CommitBatchAsync_PartialApproval_OnlyIncludedRowsCommitted` | PASS |

بدون زیرساخت تست جدید - همون ۲۷ تست فاز پیاده‌سازی دوباره ارزیابی شدند، چیزی تکرار نشد.

**تأیید مستقیم دیتابیس (نه فقط InMemory تست):** `Equipments` = ۴۰٬۸۰۹ ردیف،
`FunctionalLocations` = ۳۲ ردیف - هردو دست‌نخورده، هیچ داده‌ای حذف/تغییر نکرده.

## 4. مجوز

بدون تغییر معماری مجوز. ۱۰ تست Authorization از فاز پیاده‌سازی دوباره تأیید می‌کنند:
`Commit`/`ReplaceExisting` نیاز به `Approve` دارند (پرریسک‌ترین اکشن‌ها)، `Upload`/`ToggleRow`
نیاز به `Create`، `Discard` نیاز به `Delete`، `Index`/`Review` فقط `View` - و نقش‌های بدون
مجوز کافی، `Forbid` می‌گیرند. نمایش/عدم‌نمایش UI هیچ‌وقت جایگزین این بررسی سمت سرور نیست
(بدون تغییر). **NO CHANGE REQUIRED.**

## 5. Audit

بدون مکانیزم Audit دوم. تست `CommitBatchAsync_CommittedAssignment_CreatesAuditLogEntry`
مستقیماً تأیید می‌کند که یک تخصیص Commit‌شده یک ردیف واقعی `AuditLog`
(`EntityName="Equipment"`, `Action="Update"`,
`ChangedFieldsSummary` شامل `"FunctionalLocationId: ... -> ..."`) از طریق همون Interceptor
سراسری `PEMSDbContext.SaveChangesAsync` تولید می‌کند - بدون کد Audit دستی جدید.

## 6. Performance Sanity

| بررسی | نتیجه |
|---|---|
| بارگذاری کل ۴۰٬۸۰۹ ردیف Equipment؟ | فقط یک بار در هر Stage، فقط ستون‌های لازم (Id/Tag/FLId/Description) - عیناً همون الگوی `FunctionalLocationImportService` برای جدول FL |
| بارگذاری کل FunctionalLocation؟ | بله، ولی جدول کوچک است (۳۲ ردیف واقعی) - بی‌اثر |
| N+1 به‌ازای هر سطر اکسل؟ | خیر - یک Dictionary در حافظه یک‌بار ساخته می‌شود، Resolve هر سطر O(1) است |
| `SaveChanges()` به‌ازای هر سطر؟ | خیر - یک `SaveChangesAsync` واحد در پایان `StageFromExcelAsync` و یکی در پایان `CommitBatchAsync` |
| رندر هزاران ردیف در یک صفحه؟ | خیر - `GetBatchAsync` صفحه‌بندی واقعی سمت-SQL دارد (`Skip`/`Take`)، همون `_Pagination.cshtml`/`PagedResult<T>` موجود |
| تطبیق نامحدود در حافظه؟ | خیر - فقط Lookup روی Dictionary از‌پیش‌ساخته |

**PASS** - دقیقاً همون الگوهای اثبات‌شده‌ی Import موجود؛ بدون نیاز به بهینه‌سازی.

## 7. تست‌های موجود

```
Targeted: 27/27 (قبلاً تأیید‌شده در فاز پیاده‌سازی)
Full Regression: 1239/1239 (قبلاً تأیید‌شده در فاز پیاده‌سازی)
```

این فاز دوباره کل Regression را اجرا نکرد - **هیچ کد اپلیکیشنی تغییر نکرد**، فقط بازرسی
مستقیم Migration/دیتابیس/تست‌های موجود انجام شد (طبق دستور صریح: «Do NOT rerun the full
regression suite unless code changes are actually required»).

## 8. تصمیم نهایی

```
Equipment ↔ Functional Location Assignment:
CLOSED AND APPROVED
```
