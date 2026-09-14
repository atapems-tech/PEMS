# PEMS Portal Information Architecture (Future-State Design — Not Implemented)

**تاریخ:** 2026-09-13
**وضعیت:** Design-only. این سند شکل *آینده‌ی پیشنهادی* IA پورتال را ترسیم می‌کند؛ وضعیت
*امروز* در `docs/audits/PEMS_PHASE2B_INFORMATION_ARCHITECTURE_AUDIT.md` مستند شده و این‌جا
تکرار نمی‌شود. هیچ کد/Schema در این سند تغییر نکرده است.

---

## ۱. رابطه‌ی آینده‌ی Help، Search، Tasks، Favorites، Recent و UserPreference

این شش قابلیت اغلب به‌اشتباه به‌عنوان یک «بسته‌ی واحد» دیده می‌شوند (چون همه در Azure Portal
کنار هم در Topbar می‌نشینند). اما در PEMS، هرکدام باید **مستقل** ساخته شوند و فقط در سطح UI
کنار هم قرار بگیرند - نه در سطح داده. دلیل: طبق ممیزی فاز ۱ (بخش F)، دقیقاً همین اشتباه قبلاً
درباره‌ی Notification در برابر Task مطرح و رد شده بود («این دو باید مفهوم جدا بمانند، نه
یک شمارنده‌ی خوانده‌نشده‌ی مشترک»). همان اصل این‌جا به ۶ قابلیت گسترش می‌یابد:

```
                         ┌─────────────────────────┐
                         │   ApplicationUser (PK)  │
                         └───────────┬─────────────┘
                                     │  (هرکدام FK مستقل، بدون جدول واسط مشترک)
       ┌───────────┬─────────────┬──┴──────────┬──────────────┬───────────────┐
       ▼           ▼             ▼             ▼              ▼               ▼
 UserPreference  UserFavorite  RecentItem   (Tasks = بدون    HelpTopic    (Search = بدون
 (۱ ردیف/کاربر)  (چندردیفی،   (چندردیفی،    جدول جدید -      (بدون FK به   جدول جدید -
                  Polymorphic) Polymorphic،  Live Query روی   کاربر، محتوای  همان چارچوب
                               Capped+TTL)   جداول موجود -    عمومی)         Provider فاز۲A)
                                             طبق ممیزی فاز۱
                                             بخش E)
```

### چرا این‌ها جدول جدا هستند، نه یک «UserActivity» عمومی

- **UserPreference** تک‌ردیفی و Overwrite-محور است (آخرین تنظیم برنده است) - رفتار
  Idempotent. اگر در همان جدول Favorites/Recent (که Append-محور و چندردیفی‌اند) قرار بگیرد،
  منطق Query هرکدام غیرضروری پیچیده می‌شود.
- **UserFavorite** یک عمل *صریح* کاربر است (کلیک روی ⭐) - باید تا حذف صریح باقی بماند.
- **RecentItem** یک عمل *ضمنی* سیستم است (هر بازدید، خودکار ثبت می‌شود) - باید سقف تعداد و
  انقضا داشته باشد (طبق سؤال باز §26 در ممیزی فاز ۱ که هنوز پاسخ داده نشده: «چند مورد نگه
  داشته شود؟ آیا موردی که کاربر دیگر دسترسی ندارد باید عقب‌گرد پاک شود؟» - این‌ها تصمیم
  محصول‌اند، این سند فقط ساختار را پیشنهاد می‌دهد).
- **Tasks** طبق تصمیم قبلاً ثبت‌شده (ممیزی فاز ۱، بخش E) **نباید** جدول جدید داشته باشد -
  باید همان الگوی زنده (Live Query روی `WorkOrder.AssignedEmployeeId`،
  `LetterReferral`، `IWorkflowRuntime.GetPendingTasksAsync`) گسترش یابد، نه یک موتور
  Task جدید و موازی موتور Workflow موجود.
- **Help** (طبق سند دوم) کاملاً بدون‌کاربر است - محتوای آن برای همه یکسان است؛ تنها نقطه‌ی
  تلاقی با کاربر، زبان نمایش (از کوکی، نه از UserPreference) است.
- **Search** طبق فاز ۲A از قبل یک چارچوب Provider واقعی دارد (۱۹ `ISearchableEntityProvider`)
  - این سند چیزی به آن اضافه نمی‌کند جز این‌که در بخش ۲ زیر، محل تلاقی آن با Help/Favorites
    را روشن می‌کند.

### نقاط تلاقی واقعی (فقط در UI/Query، نه در Schema)

| تلاقی | شکل پیشنهادی | ریسک اگر در سطح داده ادغام شود |
|---|---|---|
| Search نتایج Help را هم نشان دهد | یک بیستمین `ISearchableEntityProvider` جدید (`HelpTopicSearchProvider`) که دقیقاً از همان چارچوب فاز ۲A پیروی می‌کند - **بدون** نیاز به هماهنگی خاص، چون `HelpTopic` بدون‌کاربر است (نیازی به Scope کردن بر اساس کاربر جاری ندارد، برخلاف ۱۸ Provider دیگر که این فاز/فاز۲A رویشان کار کرد) | اگر Help را داخل جدول Search دیگری بریزیم، منطق Permission-Scoping بی‌ربط (که برای Documents/WorkOrder لازم است) اشتباهاً روی محتوای عمومی Help هم اعمال می‌شود |
| Favorites یک آیتم Search را Star کند | `UserFavorite.EntityType` از همان `EntityType` رشته‌ای استفاده کند که `ISearchableEntityProvider` پیاده‌سازی‌ها امروز برمی‌گردانند (یکدست‌سازی رشته‌ها، نه یک Enum جدا) | دو منبع حقیقت برای «این نوع موجودیت چه نام دارد» - یکی برای Search، یکی برای Favorites |
| Recent Items از History ناوبری واقعی بیاید | هر Controller Details موجود (بدون تغییر منطق کسب‌وکار) یک فراخوان یک‌خطی به یک سرویس مرکزی `IRecentItemService.RecordViewAsync(userId, entityType, entityId)` اضافه کند - دقیقاً مثل الگوی `SeedMissingRolesAsync` که در این نشست برای مجوز استفاده شد: یک نقطه‌ی تزریق کوچک، نه بازنویسی هر Controller | اگر به‌جای این، از لاگ HTTP Request عمومی استخراج شود، داده‌ی نادرست (مثل بازدیدهای API/AJAX که کاربر واقعاً «ندیده») وارد Recent می‌شود |
| Tasks یک لینک «راهنمای این کار چیست؟» داشته باشد | هر ردیف Task (خروجی Live Query) از قبل `Module`/`BusinessObjectType` دارد (طبق `PendingTaskDto` موجود در `IWorkflowRuntime`) - این مستقیماً به `HelpTopic.ModuleKey` نگاشت می‌شود، بدون جدول واسط | - |
| UserPreference پیش‌فرض بخش‌های باز/بسته‌ی Sidebar را نگه دارد | یک ستون JSON ساده (`CollapsedSections`) روی همان تک‌ردیف `UserPreference` - نه یک جدول جدا برای «هر بخش سایدبار» | پیچیدگی غیرضروری برای دادهٔ کوچک و کم‌تغییر |

**اصل کلی که این جدول به آن پایبند است:** هر قابلیت جدید باید **اول** ببیند آیا یک کلید/فیلد
شناسایی‌کننده (`EntityType`, `ModuleKey`, `BusinessObjectType`) از قبل جای دیگری در پروژه
تعریف شده - و از همان استفاده کند، نه یک نسخه‌ی موازی. این دقیقاً همان درسی است که فاز ۲A
درباره‌ی دو سیستم Search موازی (`GlobalSearchController` در برابر `SearchApiController`)
آموخت.

---

## ۲. معماری آینده‌ی پیشنهادی پورتال PEMS

```
┌──────────────────────────────────────────────────────────────────────────┐
│ GLOBAL NAV (Topbar) — بدون تغییر ساختاری، فقط تکمیل نقاط خالی            │
│  Brand │ Unified Search* │ My Work/Tasks* │ Notifications │ Quick Create*│
│        │ Help* │ Theme │ Language │ User Menu                            │
│  (* = جدید یا نیازمند گسترش - جزئیات در بخش ۳)                          │
├──────────────────────────────────────────────────────────────────────────┤
│ SERVICE NAV (Sidebar) — ساختار فعلی حفظ می‌شود، با دو افزوده              │
│  [Favorites — پین‌شده‌های کاربر، بالای همه‌ی بخش‌ها]*                     │
│  Dashboard                                                                │
│  Quick Setup                                                              │
│  Engineering Management  ← IA بالغ (Tab+CommandBar) — الگوی مرجع          │
│  AI & Knowledge                                                           │
│  Correspondence & Doc Control                                            │
│  Quality Control                                                          │
│  Contract Management                                                     │
│  Project Controls                                                        │
│  CMMS Integration        ← IA عقب‌مانده — نامزد اول یکدست‌سازی            │
│  Inventory & Materials                                                   │
│  [ShiftHandover ← باید به این بخش (کنار Team/Crew) اضافه شود - رفع سریع] │
│  Administration                                                          │
│  [Recent — پایین سایدبار یا یک Drawer جدا]*                              │
├──────────────────────────────────────────────────────────────────────────┤
│ RESOURCE NAV (درون هر صفحه‌ی Details) — یکدست‌شده در فاز ۲C               │
│  Breadcrumb (عمیق‌تر: Dashboard/Section/Module/Record، هرسه قابل‌کلیک)    │
│  Tab Strip + Command Bar (تعمیم‌یافته از EDMS/VDR به CMMS)                │
│  [Help Hint "؟" کنار عنوان هر Tab]*                                       │
└──────────────────────────────────────────────────────────────────────────┘
* = نیازمند Schema جدید یا گسترش قابل‌توجه (بخش ۴)
```

## ۳. جزئیات لایه‌ی Global Nav آینده

| آیتم | امروز | آینده |
|---|---|---|
| Search | دو سیستم موازی (`GlobalSearchController` در برابر `SearchApiController`+Providers) | یک Aggregator روی چارچوب Provider موجود فاز ۲A، با نتایج برچسب‌گذاری‌شده به نوع (طبق §P ممیزی فاز ۱) - **بدون Schema جدید**، صرفاً یک لایه‌ی تجمیع |
| My Work/Tasks | فقط در Dashboard (نه Topbar)، فقط ۴ ماژول (Permit/WorkOrder/Letters/EngineeringDocuments - آخری در فاز ۲A اضافه شد) | یک آیکون در Topbar که همان Query موجود Dashboard را نشان دهد (بدون منبع داده‌ی جدید) + گسترش به VDR/TQ/MOC (طبق فاز ۲A، بخش معوق) |
| Quick Create | کاملاً غایب | یک دراپ‌داون که به فرم‌های Create پرتکرار لینک می‌دهد (Engineering Document, Letter, Work Order, ...) - هیچ منطق جدید، فقط میان‌بر ناوبری |
| Help | کاملاً غایب | طبق سند دوم |

## ۴. جدول تصمیم‌گیری Schema

| قابلیت | نیاز به Schema جدید؟ | نوع تغییر | ریسک داده‌ی موجود |
|---|---|---|---|
| Search Aggregator | خیر | فقط کد | هیچ |
| My Work/Tasks گسترش‌یافته | خیر | فقط کد (Query جدید) | هیچ |
| Quick Create | خیر | فقط کد (View) | هیچ |
| ShiftHandover Sidebar Link | خیر | یک خط در `_SidebarMenu.cshtml` | هیچ |
| Breadcrumb عمیق‌تر | خیر | فقط View/Helper | هیچ |
| Tab-Strip تعمیم به CMMS | خیر (ولی حجم کار زیاد) | Refactor View، نه Schema | رگرسیون بصری اگر بی‌دقت انجام شود - نیازمند تست دستی هر صفحه |
| **HelpTopic** | **بله** | جدول جدید، بدون FK به Workflow | کم (جدول مستقل) |
| **UserPreference** | **بله** | جدول جدید، ۱ ردیف/کاربر | کم (جدول مستقل) |
| **UserFavorite** | **بله** | جدول جدید *یا* تعمیم `LetterStar` موجود | متوسط اگر تعمیم `LetterStar` انتخاب شود (نیاز به مهاجرت داده‌ی موجود) |
| **RecentItem** | **بله** | جدول جدید، Capped+TTL | کم (جدول مستقل، ولی نیاز به سیاست پاک‌سازی) |

هر چهار مورد Schema-دار **دقیقاً همان چهار مورد**اند که ممیزی فاز ۱ (بخش P/Q) از قبل
STOP-and-confirm علامت زده بود. این سند آن فهرست را تغییر نمی‌دهد، فقط رابطه‌ی میان‌شان
(بخش ۱) و توالی پیاده‌سازی (بخش ۵) را دقیق‌تر می‌کند.

## ۵. اولویت پیاده‌سازی و توصیه‌ی فاز ۲C

| ترتیب | کار | Schema؟ | ریسک | چرا این ترتیب |
|---|---|---|---|---|
| **۱** | رفع لینک سایدبار `ShiftHandoverController` | خیر | صفر | تک‌خطی، بدون ابهام، رفع فوری یک شکاف ناوبری واقعی |
| **۲** | تصمیم مالک محصول روی مدل `HelpTopic` (سند دوم) | - | - | پیش‌نیاز هر کاری که به Help مربوط است؛ بدون این تصمیم، فاز ۲C نمی‌تواند شروع شود |
| **۳** | استخراج Tab-Strip/Command-Bar از `EngineeringDocuments/Details.cshtml` به یک Partial مشترک | خیر (Refactor) | متوسط - نیازمند تست بصری کامل، طبق تصمیم قبلی STOP در فاز ۲A که این استخراج را «نیازمند یک نوبت جدا» دانست | پیش‌نیاز فنی مستقیم برای گسترش IA به CMMS (بخش ۲) - بدون این، هر ماژول CMMS باید Tab را از صفر Copy-Paste کند |
| **۴** | در صورت تأیید گام ۲: Migration `HelpTopic` + Service + ViewComponent + Seed پایلوت (پیشنهاد ماژول: Engineering Documents) | بله | کم | جدول مستقل، بدون FK به Workflow |
| **۵** | Search Aggregator روی چارچوب فاز ۲A (بازنشستگی تدریجی `GlobalSearchController`) | خیر | متوسط - نیازمند تست رگرسیون کامل روی UX جست‌وجوی موجود | ارزش کاربری بالا، اما وابسته به این‌که فاز ۲A از قبل امنیت Providerها را رفع کرده (شده) |
| **۶** | گسترش Tab-Strip (نتیجه‌ی گام ۳) به اولین ماژول CMMS پایلوت (پیشنهاد: `Permit`، چون میزبان ۴ زیرگردش‌کار است و بیشترین سود را از Tab می‌برد) | خیر | متوسط-بالا | اثبات صحت الگوی تعمیم‌یافته قبل از گسترش به ۲۴ ماژول دیگر CMMS |
| **۷** | `UserPreference` (پس از تأیید مالک محصول) | بله | کم | مستقل از بقیه؛ می‌تواند موازی با گام‌های ۴-۶ پیش برود |
| **۸** | `UserFavorite`/`RecentItem` (پس از تأیید مالک محصول، شامل تصمیم درباره‌ی مهاجرت `LetterStar`) | بله | متوسط (اگر مهاجرت داده لازم شود) | آخرین گام چون به تصمیم محصولی حساس (نگهداری/پاک‌سازی) وابسته است که هنوز باز است |

**توصیه‌ی صریح این سند برای شروع فاز ۲C:** گام‌های ۱ و ۲ را با هم شروع کنید (هردو بدون
ریسک و مستقل از هم) - گام ۱ بلافاصله قابل‌انجام است، گام ۲ یک تصمیم مالک محصول است که هرچه
زودتر گرفته شود، بقیه‌ی توالی زودتر آزاد می‌شود. **گام ۳ (استخراج Tab-Strip) را قبل از هر
تلاش برای گسترش IA به CMMS انجام دهید** - انجام‌ندادن آن یعنی هر ماژول CMMS دوباره ۱۳۰۰ خط
Markup را Copy-Paste می‌کند، دقیقاً همان ریسکی که فاز ۲A را از استخراج زودهنگام منصرف کرد.

---

## ۶. خلاصه‌ی نهایی

**چه چیزی بررسی شد:** رابطه‌های احتمالی میان ۶ قابلیت آینده (Help/Search/Tasks/Favorites/
Recent/UserPreference) در برابر معماری داده‌ی موجود؛ نگاشت این قابلیت‌ها به لایه‌های
Global/Service/Resource Navigation؛ توالی وابستگی فنی میان آن‌ها.

**چه چیزی از قبل وجود دارد:** Tasks (Live Query - نباید Schema بگیرد)، Search (چارچوب
Provider فاز ۲A - فقط نیاز به Aggregator)، الگوی نزدیک به Preference/Favorites
(`SavedFilter`/`UserDashboardWidget`).

**چه چیزی غایب است:** هر چهار Entity (`HelpTopic`, `UserPreference`, `UserFavorite`,
`RecentItem`)؛ یک Aggregator واحد Search؛ Tab-Strip در CMMS؛ لینک سایدبار ShiftHandover.

**چه چیزی قابل‌استفاده‌ی مجدد است:** چارچوب ۱۹ Provider فاز ۲A برای Search (و برای
`HelpTopicSearchProvider` آینده)؛ الگوی `PendingTaskDto.Module`/`BusinessObjectType` برای
نگاشت Task→Help؛ الگوی `SeedMissingRolesAsync` به‌عنوان مرجع «یک نقطه‌ی تزریق کوچک، نه
بازنویسی هر Controller» برای هر سرویس آینده مثل `IRecentItemService`.

**چه چیزی باید گسترش یابد:** `_Breadcrumb.cshtml` (به Resource Menu واقعی)؛ Dashboard My
Work (به Topbar)؛ `_SidebarMenu.cshtml` (خط ShiftHandover).

**چه چیزی باید موکول شود:** هر چهار Entity جدید تا تأیید صریح مالک محصول؛ گسترش IA به کل
CMMS (کار چندهفته‌ای، باید به چند فاز پایلوت‌محور تقسیم شود، نه یک‌باره).

**چه چیزی نیازمند تغییر Schema در آینده است:** `HelpTopic`، `UserPreference`،
`UserFavorite` (یا تعمیم `LetterStar`)، `RecentItem` - دقیقاً همان چهار مورد که ممیزی فاز ۱
هم شناسایی کرده بود؛ این سند فقط توالی و رابطه‌ی میان‌شان را دقیق‌تر کرد، فهرست را تغییر
نداد.

**فاز ۲C باید چه کاری انجام دهد:** طبق جدول بخش ۵، با گام‌های ۱ و ۲ (بدون ریسک، بدون
Schema) شروع کند، سپس گام ۳ (استخراج Tab-Strip) را به‌عنوان دروازه‌ی فنی پیش از هر گسترش IA
به CMMS تکمیل کند.
