# 12 Standards Gap Index and Compliance Roadmap (PPC, 2026-11-18)

**Status:** ARCHITECTURAL ANALYSIS & GAP CLOSURE INDEX  
**Scope:** PEMS PPC vs. International Standards (ISA-88, ISO 22400, ISA-95, APICS ASCM, GS1 EPCIS)  
**Principle:** هیچ ادعای تطابق و سرتیفیکیت رسمی بدون ممیزی نهادهای مجاز ارائه نمی‌شود. کلیه ارزیابی‌های زیر ناظر بر هم‌راستایی ساختاری و مفهومی معماری (Alignment) است.

---

## 1. ماتریس تفصیلی انطباق با استاندارد ISA-88 / IEC 61512 (Batch Control)

| شناسه شکاف | بند استاندارد | عنوان شاخص استاندارد | وضعیت در PEMS قبلی | راهکار مصوب در سند تکمیلی | وضعیت پس از اجرای طراحی |
|---|---|---|---|---|---|
| **G-ISA88-01** | Clause 4 | Procedural Control Model (Procedure, Unit Procedure, Operation, Phase) | فاقد ساختار مراحل فرآیندی | افزودن `ProcessFormulaOperation` (OD-24) | **هم‌راستا (ALIGNED - Level 3)** |
| **G-ISA88-02** | Clause 4.5 | Phase State Machine (Idle, Running, Held, Aborted) | کنترلر OT وجود ندارد | کنترل فاز سطح PLC به فازهای آتی محول شد | **معاف (NOT APPLICABLE for v1)** |
| **G-ISA88-03** | Clause 5.1 | Control Recipe & Execution Instance | صرفاً جدول تخت `ProductionRun` | افزودن لاگ گام‌به‌گام در `ProductionRunOperationLog` | **هم‌راستا (ALIGNED)** |
| **G-ISA88-04** | Clause 5.6 | Electronic Batch Record (EBR) | عدم ثبت زمان شروع و پایان گام‌ها و انحرافات | لاگ الکترونیکی کامل با ثبت اپراتور و انحرافات پارامتر | **هم‌راستا (ALIGNED)** |
| **G-ISA88-05** | Clause 5.3 | Equipment Requirements & Allocation | ارتباط ضعیف با تجهیزات کارگاهی | پیوند `ProcessRouteOperation` به `ProductionWorkCenter` | **هم‌راستا (ALIGNED)** |

---

## 2. ماتریس تفصیلی انطباق با استاندارد ISO 22400-2 (Manufacturing KPIs)

| شناسه شکاف | بند استاندارد | شاخص و فرمول | وضعیت در PEMS قبلی | راهکار مصوب در سند تکمیلی | وضعیت پس از اجرای طراحی |
|---|---|---|---|---|---|
| **G-ISO22400-01** | Clause 4.2 | Planned Busy Time (PBT) Architecture | فاقد تفکیک انواع زمان تقویمی | ایجاد `ProductionCalendar` و کسر صرفاً PDOT از PBT | **هم‌راستا (ALIGNED)** |
| **G-ISO22400-02** | Clause 4.2 | Setup & Cleaning Loss Modeling | احتساب اشتباه شستشو به عنوان توقف مجاز | دسته‌بندی Setup/Cleaning درون PBT (کاهش APT) | **منطبق با دکترین (ALIGNED)** |
| **G-ISO22400-03** | Clause 6.5 | Overall Equipment Effectiveness (OEE) | فاقد فرمول و محاسبه خودکار OEE | فرمول سه‌گانه ضرب نرخ دسترسی، کارایی و کیفیت (OD-25) | **هم‌راستا (ALIGNED)** |
| **G-ISO22400-04** | Clause 6.6 | First Pass Yield (FPY) | محاسبه غیراستاندارد ضایعات | فیلتر `QualityStatus = Released` بدون دوباره‌کاری | **هم‌راستا (ALIGNED)** |
| **G-ISO22400-05** | Clause 2.1 | Unit Safety in Aggregations | جمع مقادیر با واحدهای اندازه‌گیری مختلف | پالایش سرویس و الزام تبدیل واحد پیش از تجمیع | **بسته شده (CLOSED via W1)** |

---

## 3. ماتریس تفصیلی انطباق با استاندارد ISA-95 / IEC 62264 (Enterprise-Control Integration)

| شناسه شکاف | بخش استاندارد | مفهوم استاندارد | وضعیت در PEMS قبلی | راهکار مصوب در سند تکمیلی | وضعیت پس از اجرای طراحی |
|---|---|---|---|---|---|
| **G-ISA95-01** | Part 1 §4.3 | Product Definition & Product Family | صرفاً کد قلم بدون گروه‌بندی کلان | ایجاد `ProductFamily` و اعضای آن با درصد ترکیب | **هم‌راستا (ALIGNED)** |
| **G-ISA95-02** | Part 2 §5 | Production Capability Statement | فاقد توانمندی اعلام ظرفیت کارگاه | افزودن موجودیت `ProductionCapabilityStatement` | **هم‌راستا (ALIGNED)** |
| **G-ISA95-03** | Part 3 §5 | MOM Production Operations Activities | اجرای سفارش بدون لیست اعزام (Dispatch) | طراحی بورد اعزام و اولویت‌بندی در W7 | **برنامه‌ریزی‌شده (PLANNED)** |
| **G-ISA95-04** | Part 4 §6 | Job Order & Work Master Hierarchy | تفکیک‌نشدن سفارش از دستورکار جزئی | نگاشت افزوده‌های جدید به ساختار پایگاه‌داده | **هم‌راستا (ALIGNED)** |

---

## 4. ماتریس تفصیلی انطباق با بدنه دانش APICS ASCM (MRP II Closed-Loop)

| شناسه شکاف | ماژول CPIM | فرآیند استاندارد | وضعیت در PEMS قبلی | راهکار مصوب در سند تکمیلی | وضعیت پس از اجرای طراحی |
|---|---|---|---|---|---|
| **G-APICS-01** | Module 3 | Demand Time Fence & Frozen Zone | امکان تغییر مستقیم سفارشات جاری | فیلدهای DTF/PTF و قفل‌گذاری تغییرات منجمد | **هم‌راستا (ALIGNED)** |
| **G-APICS-02** | Module 3 | Available-to-Promise (ATP) | عدم وجود شاخص تعهد تحویل کالا | فرمول تعهد گسسته و موجودی پیش‌بینی‌شده PAB | **هم‌راستا (ALIGNED)** |
| **G-APICS-03** | Module 4 | Lot-Sizing Policies (L4L, FOQ, EOQ) | سفارش مقادیر دلخواه بدون قاعده ثابت | موجودیت `LotSizingPolicy` با محدودیت‌های حداقل انباشته | **هم‌راستا (ALIGNED)** |
| **G-APICS-04** | Module 4 | MRP Exception Messages | عدم تولید هشدارهای تسریع/تاخیر/لغو | جدول هشدارهای تعاملی `MrpExceptionMessage` | **هم‌راستا (ALIGNED)** |
| **G-APICS-05** | Module 5 | Closed-Loop Feedback to Master Plan | گزارشات ایستا بدون فیدبک به برنامه | ارزیابی مغایرت و بازخورد انحرافات به نسخه فعال MPP | **هم‌راستا (ALIGNED)** |

---

## 5. ماتریس ردیابی GS1 EPCIS 2.0 (تبارشناسی بچ‌ها)

| شناسه شکاف | عنصر EPCIS | قابلیت استاندارد | وضعیت در PEMS قبلی | راهکار مصوب در سند تکمیلی | وضعیت پس از اجرای طراحی |
|---|---|---|---|---|---|
| **G-EPCIS-01** | Transformation Event | پیوند رویدادی مواد ورودی به محصولات نهایی | فقط شناسه یک سطحی در جدول ورودی | ساختار دوطرفه `ProductionBatchGenealogy` | **هم‌راستا (ALIGNED)** |
| **G-EPCIS-02** | Object Event | تغییر وضعیت کیفی و قرنطینه در انبار | تغییر استاتوس بدون رویداد ردیابی | ترخیص صرفاً با ورک‌فلو و ثبت تاریخچه رویداد | **هم‌راستا (ALIGNED)** |
