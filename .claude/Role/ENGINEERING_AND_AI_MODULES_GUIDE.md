# راهنمای توسعه‌ی کامل «Engineering Management» و «AI Engineering Intelligence»

## چه چیزهایی اضافه شد

### Engineering Management (کامل)
| زیرمنو | وضعیت | توضیح |
|---|---|---|
| Projects | ✅ فعال | CRUD کامل (کد پروژه، کارفرما، محل، تاریخ، وضعیت) |
| Engineering Documents | ✅ فعال (قبلی) | بدون تغییر در منطق، فقط داخل Layout جدید |
| Document Register | ✅ فعال | نمای فیلترپذیر (پروژه/دیسیپلین/وضعیت/جستجو) + Export Excel |
| Document Revision | ✅ فعال | تاریخچه‌ی Revision هر مدرک + ثبت Revision جدید |
| Document Workflow | ✅ فعال | صف تایید سراسری با Approve/Reject سریع + تاریخچه‌ی گردش کار |
| Transmittal | ✅ فعال | ساخت بسته‌ی ارسال رسمی مدارک به گیرنده، با ثبت شماره Revision در لحظه‌ی ارسال |
| MR Management | ✅ فعال | ثبت MR با آیتم‌های متعدد، تغییر وضعیت |
| MRQ Management | ✅ فعال | ثبت استعلام قیمت روی هر MR + ارزیابی فنی/بازرگانی/تایید نهایی |

### AI Engineering Intelligence (کامل، آفلاین با Ollama)
| زیرمنو | وضعیت | توضیح |
|---|---|---|
| Document Upload | ✅ فعال | آپلود دسته‌جمعی چند فایل؛ برای هرکدام مدرک جدید ساخته و AI صف می‌شود |
| OCR Processing | ✅ فعال | داشبورد صف پردازش (Pending/Processing/Completed/Failed) + Retry |
| AI Extraction | ✅ فعال | بازبینی و تایید/رد تگ‌های تجهیز و مشخصات فنی استخراج‌شده توسط AI |
| AI Search | ✅ فعال | جستجوی ترکیبی (عنوان/شماره مدرک/توضیحات/خلاصه AI/کلیدواژه AI) + فیلتر |
| Engineering Chat Assistant | ✅ فعال | چت مبتنی بر Ollama که فقط بر اساس داده‌ی واقعی دیتابیس (نه حدس) پاسخ می‌دهد |

موتور AI (خواندن PDF/Word/Excel/OCR + استخراج ساختاریافته با Ollama) دقیقاً همان معماری‌ای است که قبلاً طراحی شد: فایل → استخراج متن → Ollama با خروجی JSON اجباری → ذخیره در جداول Staging (`AIExtractedTags`, `AIExtractedSpecifications`) → بازبینی انسانی → تبدیل به `EquipmentTag` واقعی.

## پیشنهادهایی که خودم اضافه کردم
- **جدول Staging برای داده‌ی AI**: به‌جای ورود مستقیم و خودکار AI به جداول اصلی، یک لایه‌ی تایید انسانی گذاشتم (چون مدل محلی کوچک قابل اعتماد ۱۰۰٪ نیست).
- **پردازش پس‌زمینه (غیرمسدودکننده)**: آپلود فایل بلافاصله برمی‌گردد و AI در پس‌زمینه اجرا می‌شود، نه اینکه کاربر منتظر چند ثانیه تا چند دقیقه بماند.
- **Chat Assistant مبتنی بر Snapshot واقعی دیتابیس**: به‌جای اینکه AI آزادانه حدس بزند، یک خلاصه‌ی واقعی از وضعیت سیستم (تعداد مدارک بر اساس وضعیت، پروژه‌های فعال، MR باز و ...) به آن داده می‌شود تا پاسخ‌ها درست و قابل استناد باشند.
- **AI Search با امتیازدهی شفاف**: هر نتیجه دلیل تطابق (Match Reason) و درصد Relevance را نشان می‌دهد، نه یک "جعبه سیاه".
- **Export Excel برای Document Register**: چون این یک نیاز واقعی و رایج در پروژه‌های EPC است.
- **Dashboard به‌روزرسانی شد**: کارت‌های MR/MRQ و Transmittal که قبلاً N/A بودند، حالا عدد واقعی نشان می‌دهند.

## Migration (Additive - هیچ داده‌ای پاک نمی‌شود)

جداول/فیلدهای جدید: `Transmittals`, `TransmittalItems`, `MaterialRequisitions`, `MaterialRequisitionItems`, `MaterialRequisitionQuotations`, `AIExtractedTags`, `AIExtractedSpecifications`, `ChatMessages`, و چند فیلد جدید روی `DocumentAttachments` (AIError, AIProcessedDate, AITags و ...).

```
dotnet restore
dotnet ef migrations add AddEngineeringAndAIModules --project PEMS.Persistence --startup-project PEMS.Web
dotnet ef database update --project PEMS.Persistence --startup-project PEMS.Web
```

## پیش‌نیاز اجرای AI (بدون تغییر نسبت به قبل)
- Ollama نصب و مدل `qwen2.5:3b` (یا بزرگ‌تر) دانلود شده باشد: `ollama pull qwen2.5:3b`
- برای OCR: پوشه‌ی `tessdata` کنار خروجی برنامه + فایل‌های `fas.traineddata` و `eng.traineddata` (از `github.com/tesseract-ocr/tessdata`)

## نکات مهم پیاده‌سازی
- منطق ساخت/ویرایش عمداً در لایه‌ی `PEMS.Application`/`PEMS.Persistence` نگه داشته شده (نه در Web) طبق الزام معماری شما.
- `Contract Management`، `CMMS Integration` (فراتر از KPI داشبورد) و بخش‌های `Disciplines/Departments/System Settings` در Administration همچنان «Coming Soon» هستند چون خارج از دو بخش درخواستی این مرحله بودند.
- چون امکان اجرای `dotnet build` واقعی در این محیط نیست، لطفاً بعد از `dotnet restore` یک بار build بگیرید؛ در صورت وجود خطا برایم بفرستید تا فوراً رفع کنم.
