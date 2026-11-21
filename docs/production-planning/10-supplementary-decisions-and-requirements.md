# 10 Supplementary Standards Gap Analysis & Decision Register (PPC, 2026-11-18)

**Status:** STANDARDS-BASED DECISIONS & SPECIFICATIONS  
**Applicable Standards:** ISA-88 (IEC 61512), ISO 22400-1/2, APICS ASCM CPIM (MRP II), ISA-95 (IEC 62264), GS1 EPCIS 2.0  
**Governing Context:** Answers to Q-NEW-01 through Q-NEW-06, establishing decisions OD-24 through OD-29 and requirements REQ-30 through REQ-46.

---

## 1. تفکیک و پاسخ رسمی به پرسش‌های شش‌گانه بر اساس استانداردهای بین‌المللی

### Q-NEW-01: مدل فاز و عملیات در فرمول فرآیندی (ISA-88 / IEC 61512)
* **سؤال:** آیا مدل فاز (Phase Model) طبق استاندارد ISA-88 در `ProcessFormula` برای نسخه v1 الزامی است؟
* **پاسخ استاندارد:**
  * استاندارد **ISA-88 (IEC 61512-1)** سلسله‌مراتب کنترل رویه‌ای (Procedural Control Model) را به صورت زیر تفکیک می‌کند:
    $$\text{Procedure} \rightarrow \text{Unit Procedure} \rightarrow \text{Operation} \rightarrow \text{Phase}$$
  * در سطح PLC/DCS، فاز (Phase) دارای ماشین وضعیت دقیق (`Idle`, `Running`, `Complete`, `Held`, `Aborted`) است. اما از آنجا که PEMS در نسخه فعلی فاقد اتصال مستقیم OT/DCS است (طبق ممیزی A6)، پیاده‌سازی ماشین وضعیت سطح فاز در هسته v1 افزونگی ایجاد می‌کند.
  * **تصمیم نهایی (OD-24):** پیاده‌سازی **مدل سبک عملیات (Lightweight Operation Model)** در قالب `ProcessFormulaOperation` متصل به `ProcessFormula`.
    * هر عملیات دارای توالی (`OperationSequence`)، نام رویه (`OperationName`: مانند Charge, Heat, React, Cool, Drain)، مدت‌زمان استاندارد، و مشخصات پارامتری در قالب JSON (`CriticalParametersJson` برای بازه دما، فشار و دور همزن) خواهد بود.
    * برای تأمین الزامات پرونده الکترونیکی دسته تولید (Electronic Batch Record - EBR طبق بند 5.6 استاندارد ISA-88)، موجودیت لاگ اجرای دسته به نام `ProductionRunOperationLog` افزوده می‌شود تا زمان شروع، خاتمه، اپراتور مسئول و انحرافات ثبت شوند.

---

### Q-NEW-02: مدل زمانی و فرمول محاسبه OEE (ISO 22400-2)
* **سؤال:** OEE چگونه محاسبه می‌شود و جایگاه توقف‌های برنامه‌ریزی‌شده (مانند شستشو و تنظیمات بین بچ‌ها) در زمان‌بندی چیست؟
* **پاسخ استاندارد:**
  * بر اساس **ISO 22400-2 (بند 4.2 و بند 6.5)**، تجزیه زمانی استاندارد عبارت است از:
    1. **Total Calendar Time (TCT):** کل زمان دوره (۲۴ ساعته).
    2. **Planned Operation Time (POT):** زمان تقویمی منهای شیفت‌های تعطیل عمومی یا زمان‌های کارشناسی‌نشده.
    3. **Planned Busy Time (PBT):** زمان برنامه‌ریزی‌شده اشتغال؛ یعنی $\text{POT} - \text{Planned Downtime (PDOT)}$. در استاندارد، صرفاً تعمیرات اساسی پیشگیرانه دوره‌ای (PM) یا توقفات کلان سایت (Turnaround) مجاز به کسر شدن از PBT هستند.
    4. **شستشو، تغییر خط (Changeover/Setup) و تنظیمات:** طبق دکترین ISO 22400 و اصول TPM/SMED، این موارد **نباید از مخرج PBT کسر شوند**، بلکه اتلاف عملیاتی درون خط تلقی شده و فاکتور **Availability** را کاهش می‌دهند تا عدم کارایی پنهان نماند.
  * **فرمول‌های محاسبه:**
    $$\text{Availability} = \frac{\text{Actual Production Time (APT)}}{\text{Planned Busy Time (PBT)}}$$
    $$\text{Effectiveness} = \frac{\text{Ideal Cycle Time (PRI)} \times \text{Total Processed Quantity (PQ)}}{\text{Actual Production Time (APT)}}$$
    $$\text{Quality Ratio} = \frac{\text{Good Quantity (GQ)}}{\text{Total Processed Quantity (PQ)}}$$
    $$\text{OEE} = \text{Availability} \times \text{Effectiveness} \times \text{Quality Ratio} = \frac{\text{PRI} \times \text{GQ}}{\text{PBT}}$$
  * **تصمیم نهایی (OD-25):** تصویب مدل زمانی ISO 22400 از طریق موجودیت‌های `ProductionCalendar`, `CalendarShift` و `CalendarEvent` با تفکیک انواع رویداد (`PlannedMaintenance`, `Turnaround`, `Setup`, `Cleaning`, `UnplannedBreakdown`).

---

### Q-NEW-03: قابلیت تعهد به مشتری (ATP) و حصارهای زمانی (APICS ASCM)
* **سؤال:** آیا قابلیت Available-to-Promise (ATP) و حصارهای زمانی در زمان‌بندی کلان تولید (MPS) نسخه v1 نیاز است؟
* **پاسخ استاندارد:**
  * در استانداردهای **APICS ASCM CPIM (Module 3)**، سیستم MPS برای جلوگیری از بی‌ثباتی در کارگاه و امکان پاسخگویی به تعهدات فروش نیازمند دو مکانیزم محوری است:
    1. **Demand Time Fence (DTF):** درون این افق، برنامه کاملاً منجمد (Frozen) است، پیش‌بینی‌ها نادیده گرفته شده و صرفاً سفارشات قطعی محاسبه می‌شوند. تغییر در این بازه نیازمند گردش‌کار و مجوز سرپرستی است.
    2. **Planning Time Fence (PTF):** بعد از DTF تا زمان سررسید تجمعی اقلام. برنامه‌ریزی مجدد خودکار با نظارت امکان‌پذیر است.
  * **فرمول مانده موجودی قابل تعهد (Discrete ATP):**
    * دوره اول:
      $$\text{ATP}_1 = (\text{On-Hand} - \text{SafetyStock} + \text{MPS}_1) - \sum \text{CustomerOrders until next MPS receipt}$$
    * دوره‌های آتی دارای رسید MPS:
      $$\text{ATP}_n = \text{MPS}_n - \sum \text{CustomerOrders in period } n \text{ until next MPS receipt}$$
  * **تصمیم نهایی (OD-26):** ساختار **ATP-Ready**؛ افزودن فیلدهای `DemandTimeFenceDays` و `PlanningTimeFenceDays` به `PlanVersion` و فیلدهای محاسباتی `AvailableToPromise` و `ProjectedAvailableBalance` به `PlanItem`.

---

### Q-NEW-04: خانواده محصول در برنامه‌ریزی کلان تولید (ISA-95 Level 4)
* **سؤال:** آیا در برنامه‌ریزی کلان تولید (APP)، برنامه‌ریزی باید بر اساس خانواده محصول (Product Family) باشد یا صرفاً MaterialItem؟
* **پاسخ استاندارد:**
  * طبق **ISA-95 (IEC 62264-1 بند 4.3)**، در برنامه‌ریزی سطح کلان (APP / Level 4) با افق ۶ الی ۱۲ ماهه، پیش‌بینی‌ها و ظرفیت‌ها بر روی کدهای تکی اقلام همگرا نمی‌شوند، بلکه بر روی **Product Family** (خانواده محصول) بسته می‌شوند. سپس در مرحله تبدیل APP به MPS (افق کوتاه‌مدت ۴ تا ۱۲ هفته)، مقادیر سرشکن (Disaggregate) می‌شوند.
  * **تصمیم نهایی (OD-27):** تعریف ساختار دوگانه:
    * برای برنامه‌های از نوع `APP`: اقلام برنامه (`PlanItem`) به `ProductFamily` لینک می‌شوند.
    * برای برنامه‌های از نوع `MPS`: اقلام برنامه مستقیماً به `MaterialItem` لینک می‌شوند.
    * ایجاد موجودیت `ProductFamily` و `ProductFamilyMember` با ذخیره ضریب تاریخی سرشکن‌سازی (`HistoricalMixPercent`).

---

### Q-NEW-05: یکپارچگی تقویم خاموشی/اورهال (Turnaround) با نگهداری و تعمیرات (CMMS)
* **سؤال:** فرآیند تعمیرات اساسی (Turnaround/Shutdown) چگونه در تقویم مدل شده و با بورد زمان‌بندی CMMS (ردیف 0227) هماهنگ می‌گردد؟
* **پاسخ استاندارد:**
  * در صنایع فرآیندی/پتروشیمی، پنجره‌های TAR (Turnaround) رویدادهای بحرانی هستند که مستلزم توقف کامل خطوط، صدور مجوزهای پرمخاطره و اجرای پکیج‌های حجیم دستورکار است. تقویم تولید باید منبع واحد حقیقت (Single Source of Truth) برای ظرفیت در دسترس خط باشد.
  * **تصمیم نهایی (OD-28):** تقویم یکپارچه `ProductionCalendar`.
    * تعریف رویداد با نوع `Turnaround` در `CalendarEvent` با تاریخ تثبیت محدوده (`ScopeFreezeDate`).
    * ارتباط کلید خارجی اختیاری با دستورکارهای تعمیراتی (`MaintenanceWorkOrderId`).
    * اعمال اثر قطعی در محاسبات ظرفیت خشن (RCCP در Wave W5) به صورتی که ظرفیت مرکز کاری در بازه TAR صفر منظور گردد.

---

### Q-NEW-06: سیاست‌های تعیین اندازه انباشته (Lot-Sizing Policy) در فرآیندهای صنعتی
* **سؤال:** سیستم برنامه‌ریزی احتیاجات مواد (MRP) کدام مدل‌های اندازه‌گیری بهینه انباشته (Lot-Sizing) را باید پوشش دهد؟
* **پاسخ استاندارد:**
  * در استانداردهای **APICS ASCM CPIM (Module 4)**، الگوهای انباشته برای ساخت فرآیندی تفاوت بنیادین با مونتاژ گسسته دارند:
    1. **Lot-for-Lot (L4L):** سفارش معادل دقیق نیاز خالص دوره؛ مناسب مواد حساس و گران‌قیمت یا تولید سفارشی (MTO).
    2. **Fixed Order Quantity (FOQ):** مقدار سفارش ثابت؛ الزامی برای صنایع فرآیندی به دلیل محدودیت فیزیکی راکتورها و مخازن (`BaseBatchSize`).
    3. **Economic Order Quantity (EOQ):** محاسبه اقتصادی تعادل میان هزینه سفارش‌گذاری/راه‌اندازی و هزینه نگهداری کالا.
       $$\text{EOQ} = \sqrt{\frac{2 \times D \times S}{H}}$$
  * **تصمیم نهایی (OD-29):** تعریف موجودیت `LotSizingPolicy` برای هر قلم کالا (`MaterialItem`) با متدهای استاندارد `LotForLot` (پیش‌فرض تولید)، `FixedOrderQuantity` و `EconomicOrderQuantity` برای اقلام خریدی و ملزومات بسته‌بندی.

---

## 2. جدول ثبت تصمیمات معماری جدید (OD-24 تا OD-29)

| شناسه | شرح تصمیم | استاندارد مرجع | وضعیت پیاده‌سازی |
|---|---|---|---|
| **OD-24** | پیاده‌سازی مدل سبک عملیات فرآیندی (`ProcessFormulaOperation`) و پرونده الکترونیکی سوابق بچ (`ProductionRunOperationLog`) | ISA-88 / IEC 61512 | طراحی‌شده (موجودی در W2) |
| **OD-25** | پذیرش مدل تجزیه زمانی و فرمول‌های محاسبه شاخص اثربخشی کلی تجهیزات (OEE) | ISO 22400-2:2014 | طراحی‌شده (موجودی در W3) |
| **OD-26** | تعریف حصارهای زمانی (DTF/PTF) و منطق محاسبه مقدار قابل تعهد (ATP) در جدول اقلام برنامه | APICS ASCM CPIM | طراحی‌شده (موجودی در W4) |
| **OD-27** | تفکیک سطح برنامه‌ریزی کلان و خرد: پشتیبانی از `ProductFamily` در APP و سرشکن‌سازی به `MaterialItem` در MPS | ISA-95 (IEC 62264-1) | طراحی‌شده (موجودی در W4) |
| **OD-28** | تقویم تولید مشترک میان CMMS و PPC با قابلیت ثبت رویداد اورهال اساسی (Turnaround) و مسدودسازی ظرفیت RCCP | ISO 22400 + Best Practice پتروشیمی | طراحی‌شده (موجودی در W3/W5) |
| **OD-29** | پشتیبانی از سه الگوی انباشته‌سازی (Lot-for-Lot, FOQ, EOQ) از طریق موجودیت `LotSizingPolicy` | APICS ASCM CPIM | طراحی‌شده (موجودی در W6) |
