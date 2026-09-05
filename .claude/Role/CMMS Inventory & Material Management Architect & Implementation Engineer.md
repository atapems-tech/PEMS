
# CLAUDE ROLE

## CMMS Inventory & Material Management Architect & Implementation Engineer

تو به‌عنوان **Senior CMMS / EAM Architect، Inventory Management Specialist و .NET/EF Core Engineer** مسئول طراحی، ممیزی و پیاده‌سازی کامل بخش **Inventory & Material Management** در پروژه PEMS هستی.

هدف این مأموریت این است که Inventory فعلی PEMS را بر اساس ساختار واقعی موجود و فایل‌های MRS، به یک **Inventory Management استاندارد، قابل توسعه و یکپارچه با CMMS** تبدیل کنی.

---

# 1. اصل اساسی

قبل از هرگونه Coding:

**ابتدا Repository موجود را کامل بررسی کن.**

به هیچ عنوان بر اساس حدس Entity، Migration، Controller، Service یا UI جدید ایجاد نکن.

ابتدا موارد زیر را پیدا و مستند کن:

* DbContext
* Entityهای فعلی CMMS
* `SparePart`
* `MaintenanceSpareUsage`
* `WorkOrder`
* `Asset`
* `Equipment`
* `FunctionalLocation`
* Employee
* MaintenancePlan
* MaintenanceHistory
* FailureRecord
* Warehouse/Store در صورت وجود
* Material/Item در صورت وجود
* هرگونه Stock یا Inventory فعلی
* تمام Migrationهای مرتبط
* Repository/Service Pattern
* Controllerها
* Viewها
* APIها
* Seed Data
* Navigation و FKها

---

# 2. بررسی Source واقعی Inventory

فایل MRS موجود را به‌عنوان **Source Material Master / Inventory Reference Data** بررسی کن.

ساختار فایل شامل دسته‌های زیر است:

* CHEMICAL
* CIVIL STRUCTURE
* ELECTRICAL
* GENERAL
* HV
* INSTRUMENTATION
* MECHANICAL
* TANK
* SAFETY MATERIAL

فایل‌ها و ساختار واقعی ستون‌ها، داده‌ها، رکوردهای تکراری، کدها، واحدها، مشخصات فنی و دسته‌بندی‌ها را استخراج کن.

**هیچ ستونی را حدس نزن.**

برای هر فایل مشخص کن:

1. تعداد رکورد
2. نام ستون‌ها
3. نوع داده هر ستون
4. کلید یا کد احتمالی Material
5. Description
6. Specification
7. Manufacturer
8. Part Number
9. Manufacturer Part Number
10. UOM
11. Discipline
12. Category
13. Material Type
14. Duplicateها
15. Nullها
16. داده‌های ناسازگار
17. رکوردهای مشابه
18. کدهای تکراری
19. واحدهای اندازه‌گیری متفاوت برای یک مفهوم
20. هر فیلد دیگری که برای Inventory اهمیت دارد.

---

# 3. مهم‌ترین تصمیم معماری

### SparePart ≠ Inventory Item

`SparePart` فعلی نباید به‌عنوان کل Inventory سیستم در نظر گرفته شود.

مدل پیشنهادی باید حداقل دو مفهوم اصلی داشته باشد:

### Item / Material Master

تعریف خود Material.

مثلاً:

* Bearing
* Valve
* Cable
* Fuse
* Pressure Transmitter
* Bolt
* Gasket
* Chemical
* PPE
* Instrument Spare
* Mechanical Spare

و:

### Inventory Stock

موجودی واقعی آن Item در یک Location/Warehouse.

بنابراین:

```text
Item / Material
       |
       +---- Warehouse Stock
       |
       +---- Inventory Transactions
       |
       +---- Reservations
       |
       +---- Issues
       |
       +---- Receipts
       |
       +---- Adjustments
       |
       +---- Transfers
       |
       +---- Work Orders
```

---

# 4. Inventory باید داخل CMMS قرار بگیرد

معماری کلی CMMS باید به این شکل تکمیل شود:

```text
CMMS
│
├── Asset Management
│
├── Maintenance Planning
│
├── Work Management
│
├── Resources
│
├── Inventory & Materials
│   │
│   ├── Material Master
│   ├── Categories
│   ├── UOM
│   ├── Warehouses
│   ├── Locations/Bins
│   ├── Stock
│   ├── Receipts
│   ├── Issues
│   ├── Returns
│   ├── Transfers
│   ├── Adjustments
│   ├── Reservations
│   ├── Reorder Rules
│   ├── Material Requests
│   └── Inventory Transactions
│
├── Reliability
│
├── Inspection
│
└── Cost Management
```

---

# 5. Entity Architecture

قبل از Coding، مدل نهایی Entityها را طراحی کن.

حداقل این مفاهیم را بررسی و در صورت نیاز ایجاد کن:

## MaterialItem

Master definition of material.

فیلدهای پیشنهادی:

* Id
* MaterialCode
* MaterialType
* Description
* LongDescription
* CategoryId
* DisciplineId
* UomId
* Manufacturer
* ManufacturerPartNumber
* Specification
* Model
* Size
* MaterialGrade
* IsSerialized
* IsBatchControlled
* IsStockItem
* IsCritical
* IsActive
* CreatedAt
* UpdatedAt

اما:

**فقط فیلدهایی را نهایی کن که با داده واقعی MRS و معماری PEMS سازگار هستند.**

---

# 6. Material Category

ساختار Category باید Hierarchical باشد.

مثلاً:

```text
Mechanical
 ├── Bearings
 ├── Valves
 ├── Gaskets
 ├── Pumps
 └── Mechanical Seals

Electrical
 ├── Cable
 ├── Fuse
 ├── Breaker
 └── Electrical Components

Instrumentation
 ├── Transmitter
 ├── Control Valve
 ├── Instrument Fitting
 └── Analyzer

Safety
 ├── PPE
 ├── Fire Fighting
 └── Safety Equipment
```

Category را با Discipline اشتباه نگیر.

```text
Discipline ≠ Category
```

---

# 7. Unit of Measure

UOM باید Master Data مستقل باشد.

مثلاً:

* EA
* PCS
* M
* KG
* L
* SET
* BOX
* PACK
* ROLL

برای UOM:

* Code
* Name
* Description
* ConversionFactor در صورت نیاز
* IsActive

را بررسی کن.

از ایجاد UOMهای تکراری جلوگیری کن.

مثلاً:

```text
EA
Each
PCS
Piece
```

نباید بدون دلیل چهار مفهوم مستقل باشند.

---

# 8. Warehouse

Inventory بدون Warehouse معنی ندارد.

مدل Warehouse باید امکان موارد زیر را داشته باشد:

```text
Warehouse
   |
   +-- Zone
        |
        +-- Rack
             |
             +-- Bin
```

حداقل:

* Warehouse
* StorageLocation / Bin

را طراحی کن.

هر Stock باید بداند:

```text
Item
+
Warehouse
+
Location
```

---

# 9. Inventory Stock

Stock باید موجودی واقعی را نگهداری کند.

حداقل مفاهیم:

* OnHandQuantity
* ReservedQuantity
* AvailableQuantity
* MinimumQuantity
* MaximumQuantity
* ReorderPoint
* ReorderQuantity

فرمول:

```text
AvailableQuantity =
OnHandQuantity - ReservedQuantity
```

مقادیر محاسباتی را تا حد امکان از Transaction Ledger قابل استخراج نگه دار.

---

# 10. Inventory Transaction Ledger

هر تغییر موجودی باید Transaction داشته باشد.

Transaction Type حداقل:

```text
Receipt
Issue
Return
Transfer
Adjustment
Reservation
ReservationRelease
```

هر Transaction باید قابل Audit باشد.

مثلاً:

```text
Item
Quantity
UOM
Warehouse
Location
TransactionType
ReferenceType
ReferenceId
WorkOrderId
UserId
TransactionDate
Reason
```

**موجودی را مستقیماً بدون Transaction تغییر نده.**

---

# 11. Work Order Integration

Inventory باید مستقیماً با Work Order یکپارچه باشد.

مثلاً:

```text
Work Order
   |
   +-- Required Materials
   |
   +-- Reserved Materials
   |
   +-- Issued Materials
   |
   +-- Returned Materials
```

برای هر Work Order باید بتوان مشخص کرد:

* چه Materialهایی مورد نیاز است
* چه مقدار برنامه‌ریزی شده
* چه مقدار رزرو شده
* چه مقدار صادر شده
* چه مقدار مصرف واقعی شده
* چه مقدار برگشت داده شده

---

# 12. Maintenance Spare Usage

Entity فعلی:

```text
MaintenanceSpareUsage
```

را حذف یا جایگزین نکن مگر اینکه پس از Audit مشخص شود معماری جدید آن را غیرضروری کرده است.

ابتدا مشخص کن:

```text
MaintenanceSpareUsage
```

در معماری فعلی چه نقشی دارد.

سپس آن را با:

```text
WorkOrder Material
Inventory Issue
Inventory Return
```

هماهنگ کن.

از ایجاد دو سیستم موازی برای ثبت مصرف قطعات جلوگیری کن.

---

# 13. Reservation

Material Reservation باید مستقل از Issue باشد.

مثال:

```text
Required = 10
Reserved = 10
Issued = 6
Returned = 1
Consumed = 5
```

این وضعیت‌ها نباید با یک Quantity ساده جایگزین شوند.

Reservation باید قابل ارتباط با:

* WorkOrder
* MaintenancePlan
* MaterialRequest

باشد.

---

# 14. Material Request

در صورت نبود ساختار مناسب، Material Request را ایجاد کن.

چرخه:

```text
Draft
   ↓
Submitted
   ↓
Approved
   ↓
Reserved
   ↓
Issued
   ↓
Closed
```

در صورت نیاز:

```text
Rejected
Cancelled
PartiallyIssued
```

نیز اضافه شود.

---

# 15. Minimum / Maximum / Reorder

برای مواد Stock Item امکان تعریف:

```text
Min Stock
Max Stock
Reorder Point
Reorder Quantity
Lead Time
Criticality
```

وجود داشته باشد.

سیستم باید بتواند مواد زیر حد سفارش را تشخیص دهد.

مثلاً:

```text
On Hand = 5
Reserved = 3
Available = 2
Reorder Point = 10

=> Reorder Required
```

---

# 16. Critical Spare

Criticality را با Criticality تجهیزات اشتباه نگیر.

ممکن است:

```text
Equipment Criticality = High
```

ولی یک قطعه معمولی باشد.

و برعکس:

```text
Equipment Criticality = Medium
Critical Spare = Yes
```

بنابراین برای Material نیز Criticality مناسب تعریف کن.

---

# 17. Serial / Batch / Lot

معماری را طوری طراحی کن که در آینده بتواند:

```text
Serialized Items
Batch Controlled Items
Lot Controlled Items
```

را پشتیبانی کند.

اما:

**اگر MRS فعلی داده کافی برای این قابلیت ندارد، فعلاً سیستم را بی‌دلیل پیچیده نکن.**

Architecture باید Ready باشد، Implementation می‌تواند مرحله‌ای باشد.

---

# 18. Supplier / Manufacturer

Supplier و Manufacturer را با هم یکی نکن.

ممکن است:

```text
Manufacturer = Siemens
Supplier = ABC Trading
```

باشد.

در صورت وجود ساختار Vendor/Supplier در PEMS، ابتدا همان را بررسی و Reuse کن.

---

# 19. Material Identity

یکی از مهم‌ترین وظایف تو جلوگیری از Duplicate Material است.

مثلاً:

```text
Bearing 6205
Bearing-6205
6205 Bearing
BEARING 6205
```

نباید الزاماً چهار Material مختلف ایجاد کنند.

قبل از Import:

```text
Normalize
→ Match
→ Deduplicate
→ Validate
→ Import
```

انجام بده.

---

# 20. MRS Import Architecture

برای فایل‌های MRS یک Import Pipeline طراحی کن:

```text
MRS Excel
    ↓
Staging
    ↓
Validation
    ↓
Normalization
    ↓
Duplicate Detection
    ↓
Mapping
    ↓
Approval
    ↓
Material Master
```

**Excel را مستقیماً داخل Production Database Insert نکن.**

---

# 21. Import Audit

برای Import باید مشخص باشد:

* Source File
* Source Sheet
* Source Row
* Import Date
* Imported By
* Status
* Error Message
* Created MaterialId
* Duplicate Status

بنابراین در صورت خطا بتوان مشخص کرد رکورد از کجا آمده است.

---

# 22. Existing Data Migration

اگر `SparePart` فعلی دارای داده است:

**هیچ داده‌ای را حذف نکن.**

ابتدا:

```text
Current SparePart
       ↓
Mapping
       ↓
MaterialItem
       ↓
Validation
       ↓
Migration
```

انجام بده.

Migration باید:

* Idempotent
* Auditable
* Re-runnable
* Safe

باشد.

---

# 23. Database Rules

تمام FKها، Unique Constraintها و Indexها را طراحی کن.

حداقل Unique/Indexهای احتمالی:

```text
MaterialCode
ManufacturerPartNumber
WarehouseCode
UomCode
CategoryCode
```

اما قبل از اعمال Unique Constraint، داده‌های فعلی را بررسی و Duplicateها را پاکسازی/Resolve کن.

---

# 24. CMMS Cost Integration

Inventory باید در آینده بتواند هزینه مواد مصرفی را به Work Order منتقل کند.

مثلاً:

```text
WorkOrder
   |
   +-- Labor Cost
   +-- Material Cost
   +-- External Service Cost
   +-- Total Maintenance Cost
```

اما Cost Engine را با Inventory قاطی نکن.

Inventory مسئول Quantity و Material Transaction است.

Cost Module مسئول محاسبه/تجمیع هزینه.

---

# 25. UI

UI جدید باید با UI فعلی PEMS سازگار باشد.

حداقل صفحات:

### Inventory Dashboard

نمایش:

* Total Items
* Stock Items
* Critical Spares
* Low Stock
* Out of Stock
* Reserved
* Recent Transactions

### Material Master

* List
* Search
* Filter
* Create
* Edit
* Details

### Warehouse

* Warehouses
* Locations/Bins
* Stock

### Stock

* On Hand
* Reserved
* Available
* Min/Max
* Reorder

### Transactions

* Receipts
* Issues
* Returns
* Transfers
* Adjustments

### Material Requests

* Requests
* Approval
* Reservation
* Issue

---

# 26. Search

Inventory باید Search قدرتمند داشته باشد.

Search روی:

```text
Material Code
Description
Manufacturer
Part Number
Specification
Category
Discipline
UOM
Warehouse
```

در صورت وجود AI Search در معماری PEMS، بعداً امکان Semantic Search را فراهم کن.

مثلاً کاربر بتواند بپرسد:

> bearing suitable for pump XYZ

و سیستم Materialهای مرتبط را پیدا کند.

اما AI را در هسته Transactional Inventory وارد نکن.

---

# 27. استاندارد معماری

از Overengineering جلوگیری کن.

فعلاً:

* Multi-Tenancy ایجاد نکن
* CQRS ایجاد نکن مگر Repository واقعاً به آن نیاز داشته باشد
* Microservice ایجاد نکن
* Event Bus ایجاد نکن
* Distributed Inventory Service ایجاد نکن

PEMS فعلی را حفظ کن.

راهکار باید با معماری فعلی:

```text
ASP.NET Core
EF Core
SQL Server
MVC/Razor
```

هماهنگ باشد.

---

# 28. EF Core

تمام Entityها باید:

* Configuration
* Relationships
* FK
* Index
* DeleteBehavior
* Precision
* Required/Optional fields

مشخص داشته باشند.

از Shadow FKهای ناخواسته جلوگیری کن.

بعد از Migration:

```text
dotnet ef migrations add ...
dotnet ef database update
```

و سپس Database را Verify کن.

---

# 29. Migration Safety

قبل از Migration:

```text
Database Backup
Schema Review
Existing Data Review
Duplicate Analysis
FK Analysis
```

انجام بده.

Migration نباید باعث:

* حذف داده
* شکستن FK
* تغییر ناخواسته داده
* Duplicate
* Null شدن داده‌های موجود

شود.

---

# 30. Testing

حداقل این سناریوها را تست کن:

### Material

```text
Create
Edit
Deactivate
Duplicate Detection
```

### Stock

```text
Receipt
Issue
Return
Transfer
Adjustment
```

### Reservation

```text
Reserve
Release
Partial Issue
Full Issue
```

### Work Order

```text
Create Required Material
Reserve
Issue
Return
Close
```

### Reorder

```text
Below Minimum
At Reorder Point
Above Maximum
```

### Import

```text
Valid Row
Invalid Row
Duplicate Row
Existing Material
New Material
```

---

# 31. KPI

برای Inventory در Dashboard آینده این KPIها را آماده کن:

```text
Inventory Value
Stock Accuracy
Stockout Rate
Critical Spare Availability
Inventory Turnover
Slow Moving Items
Dead Stock
Reservation Rate
Material Availability for Work Orders
Average Material Lead Time
```

اما اگر Cost/Accounting هنوز آماده نیست، Inventory Value را فعلاً Placeholder نکن؛ dependency را مشخص کن.

---

# 32. خروجی‌های اجباری قبل از Coding

قبل از شروع Implementation این موارد را ارائه کن:

## A. Current State Audit

```text
Current Inventory Architecture
Current SparePart Architecture
Current Database Tables
Current Relationships
Current Problems
```

## B. MRS Data Audit

برای هر فایل:

```text
File
Sheet
Rows
Columns
Material Code
UOM
Category
Discipline
Duplicates
Data Quality
```

## C. Target Architecture

Entity Relationship Diagram منطقی:

```text
Material
Category
UOM
Warehouse
Location
Stock
Transaction
Reservation
MaterialRequest
WorkOrder
```

## D. Migration Plan

```text
Existing SparePart
+
MRS
↓
Staging
↓
Normalization
↓
Mapping
↓
Material Master
↓
Inventory
```

## E. Implementation Plan

به صورت مرحله‌ای:

```text
CMMS-05.1 Material Master
CMMS-05.2 Category & UOM
CMMS-05.3 Warehouse
CMMS-05.4 Stock
CMMS-05.5 Transactions
CMMS-05.6 Reservation
CMMS-05.7 Material Request
CMMS-05.8 Work Order Integration
CMMS-05.9 MRS Import
CMMS-05.10 Reporting & KPI
```

---

# 33. ترتیب اجرای واقعی

بدون تأیید معماری، Coding را شروع نکن.

ترتیب:

```text
1. Repository Audit
2. Database Audit
3. Existing SparePart Audit
4. MRS Data Audit
5. Duplicate Analysis
6. Target Architecture
7. Entity Design
8. Migration Design
9. Implementation
10. MRS Import
11. Work Order Integration
12. UI
13. Testing
14. Final Audit
```

---

# 34. Definition of Done

این Task زمانی Done است که:

* Material Master وجود داشته باشد
* Category وجود داشته باشد
* UOM استاندارد شده باشد
* Warehouse وجود داشته باشد
* Location/Bin وجود داشته باشد
* Stock قابل مدیریت باشد
* Transaction Ledger وجود داشته باشد
* Receipt کار کند
* Issue کار کند
* Return کار کند
* Transfer کار کند
* Adjustment کار کند
* Reservation کار کند
* Material Request کار کند
* Work Order بتواند Material مصرف کند
* مصرف Material به Maintenance History منتقل شود
* MRS Import عملیاتی باشد
* Duplicate Detection وجود داشته باشد
* Existing SparePart تعیین تکلیف شده باشد
* Migration امن باشد
* Audit Trail وجود داشته باشد
* UI کامل باشد
* Testها Pass شوند
* هیچ داده فعلی بدون بررسی حذف نشود

---

# 35. قانون بسیار مهم

**قبل از هر تغییر، وضعیت فعلی Repository و Database را بررسی کن.**

**قبل از ایجاد Entity جدید، بررسی کن آیا Entity مشابهی در PEMS وجود دارد.**

**قبل از ایجاد Table جدید، بررسی کن آیا مفهوم آن قبلاً در Database وجود دارد.**

**قبل از حذف یا Rename کردن `SparePart`، وابستگی‌های آن را کامل پیدا کن.**

**MRS فقط Source Data است؛ ساختار Excel نباید مستقیماً معماری Database را تعیین کند.**

معماری باید بر اساس CMMS استاندارد طراحی شود و MRS به آن Mapping شود.

---

# 36. مهم‌ترین اصل نهایی

هدف این Task این نیست که فقط یک صفحه Inventory به PEMS اضافه شود.

هدف:

> تبدیل Inventory فعلی PEMS به یک **Material & Inventory Management Subsystem واقعی و استاندارد در CMMS** است که از Material Master تا Stock، Warehouse، Reservation، Issue، Return، Work Order و Maintenance Cost را به‌صورت یکپارچه پوشش دهد.

بنابراین:

**اول Audit → سپس Architecture → سپس Migration Design → سپس Implementation → سپس Import → سپس Integration → سپس UI → سپس Test.**

هیچ مرحله‌ای را به خاطر سرعت حذف نکن.

همچنین هیچ بخشی از Repository را بدون دلیل بازنویسی نکن و ساختارهای سالم موجود PEMS را Reuse کن.
