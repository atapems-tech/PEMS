# PEMS — CMMS/Vendor Base Authorization Wiring

> **نکته‌ی مهم درباره‌ی نام‌گذاری:** این سند اشتباهاً در ابتدا با عنوان «Phase 2B» ذخیره شده
> بود. **این سند "PEMS Phase 2B — Portal Information Architecture + Contextual Help Audit"
> نیست.** آن فاز (که از قبل درخواست شده بود) جداگانه و تحت
> `docs/audits/PEMS_PHASE2B_INFORMATION_ARCHITECTURE_AUDIT.md` انجام می‌شود. این سند صرفاً
> یک فاز مستقل سخت‌سازی امنیتی (Security Hardening) است که از یافته‌های معوق فاز ۲A
> (Global Search Security) ادامه پیدا کرد - نه بخشی از دنباله‌ی رسمی شماره‌گذاری‌شده‌ی
> فازهای Portal UX.

**تاریخ:** 2026-09-13
**نوع فاز:** ادامه‌ی مستقیم Deferred Items سند «PEMS Phase 2A — Global Search Security + EDMS
Command Bar + Pending Actions» (`PEMS_PHASE2A_SEARCH_SECURITY_AND_EDMS_FOUNDATION.md`)، بخش
یافته‌های معوق: شکاف مجوز پایه در `VDRController`/`VendorController`/`EquipmentController`/
`TechnicalQueryController`/`MOCController`/`SparePartController` (هیچ‌کدام حتی یک بررسی
`HasPermissionAsync`/`[Authorize]` نداشتند).

---

## ۱. دامنه‌ی تصمیم‌گیری‌شده (طبق تأیید صریح کاربر)

چون این فاز، برخلاف فاز ۲A (که فقط رفتار جست‌وجوی موجود را با قوانین از قبل موجود منطبق
می‌کرد)، می‌توانست کاربران واقعی را از صفحاتی که امروز مرور می‌کنند قفل کند، قبل از هرگونه
پیاده‌سازی از کاربر با `AskUserQuestion` پرسیده شد. تصمیم نهایی:

| ماژول | تصمیم | دلیل |
|---|---|---|
| **VDR** | سیم‌کشی با داده‌ی Seed موجود | ردیف‌های `RolePermissions` از قبل در `PermissionService.SeedDefaultsAsync` وجود داشتند (سیاست تعریف‌شده، هرگز وصل‌نشده) |
| **Vendors** | سیم‌کشی با داده‌ی Seed موجود | همان - فقط `ContractManager: Create/Edit` |
| **Equipment** | سیم‌کشی با داده‌ی Seed موجود | همان - فقط `MaintenanceEngineer: Create/Edit` |
| **TechnicalQuery** | رد شد (Skip) | هیچ داده‌ی Seed/سیاست تعریف‌شده‌ای برای این ماژول وجود ندارد - وضع سیاست از صفر، خارج از دامنه‌ی این فاز |
| **MOC** | رد شد (Skip) | همان |
| **SparePart** | رد شد (Skip) | همان |

**قاعده‌ی مشترک برای هر سه ماژول سیم‌کشی‌شده:** فقط اکشن‌های Create و اکشن‌های
تصمیم‌گیری/تغییردهنده‌ی گردش‌کار گیت شدند. صفحات فقط‌خواندنی (**Index/Details**) عمداً
دست‌نخورده ماندند تا هیچ کاربری از صفحاتی که امروز مرور می‌کند قفل نشود - این یک تصمیم صریح
کاربر بود، نه محدودیت فنی.

---

## ۲. یافته‌ی اصلی (قبل از این فاز)

هر سه کنترلر (`VDRController`، `VendorController`، `EquipmentController`) هیچ وابستگی‌ای به
`IPermissionService` نداشتند. هر کاربر احرازهویت‌شده - صرف‌نظر از نقش - می‌توانست:

- مدرک VDR جدید بسازد، شماره‌ی رسمی Contractor/Vendor تولید کند، روی FTP آپلود کند، چرخه‌ی
  بازبینی را شروع کند، به‌عنوان دیسیپلین پاسخ دهد، تصمیم نهایی (Lead Review) بگیرد، یا مدرک
  را رسماً به وندور بازگرداند (که خودش یک نامه‌ی رسمی هم می‌سازد).
- وندور جدید بسازد/ویرایش کند/فعال‌وغیرفعال کند/امتیاز ریسکش را بازمحاسبه کند.
- تجهیز جدید بسازد یا Functional Location/Parent Equipment آن را تغییر دهد.

`MenuAccess` فقط نمایش/عدم‌نمایش آیتم منو را کنترل می‌کرد (بدون اجرای واقعی سمت سرور) - طبق
یافته‌ی فاز ۲A، این هیچ محافظتی در برابر دسترسی مستقیم به URL/فرم POST نبود.

---

## ۳. ماتریس مجوز اعمال‌شده (از داده‌ی Seed موجود، بدون اختراع سیاست جدید)

### VDR (`PermissionService.SeedDefaultsAsync`، ماژول `"VDR"`)

| نقش | View | Create | Edit | Approve |
|---|---|---|---|---|
| EngineeringManager | ✔ | ✔ | ✔ | ✔ |
| DocumentController | ✔ | ✔ | ✔ | ✘ |
| DisciplineEngineer | ✔ | ✘ | ✔ | ✔ |
| Viewer | ✔ | ✘ | ✘ | ✘ |
| (سایر نقش‌ها) | ✘ | ✘ | ✘ | ✘ |

اکشن‌های گیت‌شده در `VDRController`:

| اکشن | عملیات مورد نیاز | توضیح |
|---|---|---|
| `Create` (POST) | Create | ثبت مدرک VDR جدید |
| `GenerateContractorNumber` | Edit | تولید شماره‌ی رسمی فرمت CONTRACTOR |
| `GenerateVendorNumber` | Edit | تولید شماره‌ی رسمی فرمت SUB-CONTRACTOR/Vendor |
| `UploadToFtp` | Edit | آپلود واقعی روی FTP Server |
| `SubmitForReview` | Edit | شروع چرخه‌ی بازبینی |
| `DisciplineRespond` | Edit | پاسخ یک دیسیپلین به بازبینی موازی |
| `LeadReview` | **Approve** | تصمیم نهایی بازبین ارشد |
| `ReturnToVendor` | **Approve** | بازگشت رسمی به وندور (+ نامه‌ی خودکار) |

**دست‌نخورده (عمداً):** `Index`، `Details` (GET)، و کل چرخه‌ی Comment/Reply
(`AddComment`/`SetCommentAction`/`RecordCommentResponse`/`UploadCommentSheet`/
`UploadReplySheet`) - این آخری‌ها دقیقاً منطبق با پیش‌ینه‌ی موجود در
`EngineeringDocumentsController` هستند که اکشن‌های معادل کامنت را هم بدون بررسی مجوز دارد؛
یکدست نگه‌داشتن این رفتار (نه سخت‌گیرتر کردن یک‌طرفه‌ی VDR) خارج از دامنه‌ی این فاز است.

### Vendors (ماژول `"Vendors"`)

| نقش | View | Create | Edit |
|---|---|---|---|
| ContractManager | ✔ | ✔ | ✔ |
| (سایر نقش‌ها، ازجمله Viewer) | ✘* | ✘ | ✘ |

\* طبق طراحی امروز `SeedMissingRolesAsync`، نقشی که اصلاً در آرایه‌ی Seed یک ماژول ذکر نشود
هیچ ردیفی نمی‌گیرد (نه حتی View) - اما چون `Index`/`Details` طبق تصمیم کاربر گیت نشدند، این
عملاً روی رفتار امروز اثر ندارد.

اکشن‌های گیت‌شده در `VendorController`: `Create` (GET+POST) → Create؛ `Edit` (GET+POST)،
`ToggleActive`، `RecalculateRisk` → Edit. `Index`/`Details` دست‌نخورده.

### Equipment (ماژول `"Equipment"`)

| نقش | View | Create | Edit |
|---|---|---|---|
| MaintenanceEngineer | ✔ | ✔ | ✔ |
| (سایر نقش‌ها) | ✘* | ✘ | ✘ |

اکشن‌های گیت‌شده در `EquipmentController`: `Create` (GET+POST) → Create؛
`AssignFoundation` (تغییر Functional Location/Parent Equipment) → Edit. `Index`/`Details`
دست‌نخورده (`Details` علاوه‌براین به چند سرویس دیگر - Reliability/Inspection/
Classification/MaintenanceIntelligence - وابسته است که خارج از دامنه‌ی این فاز هستند).

---

## ۴. الگوی پیاده‌سازی

هر سه کنترلر یک متد خصوصی یکسان اضافه کردند (نام و امضا در هرکدام تطبیق‌یافته با سبک موجود
همان فایل):

```csharp
private async Task<bool> EnsurePermissionAsync(string action, /* ... */, string friendlyMessage)
{
    if (await _permissionService.HasPermissionAsync(_currentUser.Roles, "<Module>", action))
        return true;
    TempData["Error"] = friendlyMessage;
    return false;
}
```

هر اکشن گیت‌شده این بررسی را **در همان خط اول بدنه** (قبل از هر Query/Side-Effect) صدا
می‌زند و در صورت رد، بدون اجرای منطق کسب‌وکار، به `Details`/`Index` با پیام فارسی
`TempData["Error"]` بازمی‌گردد - دقیقاً همان سبک خطای موجود در این کنترلرها (نه `Forbid()`
خام)، تا با UX امروز (که با Toast/Alert مبتنی بر TempData کار می‌کند) یکدست بماند.

---

## ۵. تست‌ها (۳۳ تست جدید، همه Pass)

الگوی Setup عیناً همان `AccessControlParityTests`/`SearchProviderSecurityTests`/
`JobPlanControllerAuthorizationTests` است: نقش‌های واقعی Seed می‌شوند،
`PermissionService.SeedDefaultsAsync` + `AccessControlSeeder.SeedFromLegacyAsync` روی داده‌ی
واقعی اجرا می‌شود (نه یک Fixture دستی جدا از رفتار واقعی سیستم). سرویس‌های وابسته‌ی هر
کنترلر (Workflow/Numbering/Ftp/Reliability/...) با Spy/Throwing Fake جایگزین شدند تا این
تست‌ها فقط مسیر مجوز را بسنجند - اگر گیت اشتباه باز بماند، خودِ Fake با
`InvalidOperationException`/`NotImplementedException` تست را می‌شکند (نه فقط یک Assert
خاموش).

- **`PEMS.Tests/Authorization/VDRControllerAuthorizationTests.cs`** (۱۶ تست): Create
  (Viewer رد/DocumentController مجاز)، SubmitForReview/DisciplineRespond (Viewer رد)،
  LeadReview/ReturnToVendor (Viewer رد، DocumentController رد چون Approve ندارد،
  DisciplineEngineer/EngineeringManager مجاز)، GenerateContractorNumber/UploadToFtp (Viewer
  رد - سرویس Throwing هرگز صدا نمی‌شود)، Index/Details (Viewer همچنان در دسترس - اثبات
  عدم‌رگرسیون).
- **`PEMS.Tests/Authorization/VendorControllerAuthorizationTests.cs`** (۱۲ تست): Create
  GET+POST، Edit GET+POST، ToggleActive، RecalculateRisk (هرکدام Viewer رد / ContractManager
  مجاز)، Index/Details (Viewer همچنان در دسترس).
- **`PEMS.Tests/Authorization/EquipmentControllerAuthorizationTests.cs`** (۵ تست): Create
  GET+POST، AssignFoundation (Viewer رد / MaintenanceEngineer مجاز)، Index (Viewer همچنان در
  دسترس).

### نتیجه‌ی رگرسیون کامل

```
Passed!  - Failed: 0, Passed: 1203, Skipped: 0, Total: 1203, Duration: 7 m 46 s
```

مطابقت دقیق: ۱۱۷۰ Baseline (پایان فاز ۲A) + ۳۳ تست جدید این فاز (۱۶ VDR + ۱۲ Vendor + ۵
Equipment) = **۱۲۰۳**. صفر شکست، صفر Skip - هیچ تست موجودی توسط گیت‌های جدید نشکست، که
اثبات می‌کند Index/Details و اکشن‌های کامنت واقعاً دست‌نخورده ماندند.

---

## ۶. تغییرات دیتابیس/Schema

**هیچ.** هیچ Migration، هیچ ستون/جدول جدید، هیچ تغییر داده‌ای اعمال نشد. این فاز صرفاً
ردیف‌های `RolePermissions` را که از قبل توسط توسعه‌دهنده‌ی قبلی Seed شده بودند (ولی هرگز به
هیچ کنترلری وصل نشده بودند) به کنترلرهای واقعی متصل کرد.

---

## ۷. فایل‌های تغییریافته

- `PEMS.Web/Controllers/VDRController.cs` — تزریق `IPermissionService`، متد
  `EnsurePermissionAsync`، گیت روی ۷ اکشن تغییردهنده + `Create`.
- `PEMS.Web/Controllers/VendorController.cs` — تزریق `IPermissionService`/
  `ICurrentUserService`، متد `EnsurePermissionAsync`، گیت روی Create/Edit/ToggleActive/
  RecalculateRisk.
- `PEMS.Web/Controllers/EquipmentController.cs` — تزریق `IPermissionService`، متد
  `EnsurePermissionAsync`، گیت روی Create/AssignFoundation.
- `PEMS.Tests/Authorization/VDRControllerAuthorizationTests.cs` (جدید)
- `PEMS.Tests/Authorization/VendorControllerAuthorizationTests.cs` (جدید)
- `PEMS.Tests/Authorization/EquipmentControllerAuthorizationTests.cs` (جدید)

---

## ۸. یافته‌ی معوق (مستندسازی صریح، نه فراموشی)

`TechnicalQueryController`، `MOCController`، `SparePartController` **همچنان بدون هیچ بررسی
مجوزی** باقی می‌مانند. برخلاف VDR/Vendors/Equipment، هیچ ردیف `RolePermissions` از قبل برای
این سه ماژول وجود ندارد (نه در آرایه‌ی `Modules` ثابت `PermissionService`، نه در هیچ بلوک
Seed دیگر) - یعنی وضع هرگونه سیاست مجوز برای این‌ها به معنای **اختراع** یک قانون تازه از صفر
است، نه سیم‌کشی چیزی که قبلاً تصمیم‌گیری شده. طبق تصمیم صریح کاربر، این کار از دامنه‌ی فاز
۲B خارج ماند. هر فاز آینده‌ای که بخواهد این شکاف را ببندد باید:

1. اول با ذی‌نفع کسب‌وکار (نه حدس مهندسی) مشخص کند کدام نقش‌ها باید Create/Edit/Approve این
   سه ماژول را داشته باشند.
2. سپس دقیقاً همان الگوی این فاز (Seed در `PermissionService` + `EnsurePermissionAsync` در
   کنترلر + تست‌های Spy/Throwing) را تکرار کند.
