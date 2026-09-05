# PEMS — Engineering Intelligence
## Master Audit, Architecture & Implementation Directive
### Inspired by Engineering Document Intelligence Patterns — Tagsight Benchmark

**Document Type:** Master Technical Directive  
**System:** PEMS / Engineering Intelligence  
**Target Implementer:** Claude Code  
**Technology Baseline:** ASP.NET Core / C# / EF Core / SQL Server / Razor MVC  
**AI Baseline:** Local AI / Ollama  
**Primary Objective:** Transform Engineering Document Intelligence from a basic document-processing feature into a traceable, cross-document engineering intelligence platform.

---

# 1. دستور اصلی

قبل از هرگونه کدنویسی، کل Repository، Database، EF Core Migrations، Entityها، Services، Controllers، Views، AI Services، Document Processing Pipeline و Architecture موجود PEMS را ممیزی کن.

**ممنوع است قبل از تکمیل Audit اقدام به پیاده‌سازی گسترده کنی.**

هدف این پروژه فقط اضافه‌کردن یک AI Chat یا OCR نیست.

هدف ایجاد یک:

> **Engineering Document Intelligence + Engineering Data Reconciliation + Traceability Platform**

است.

PEMS باید بتواند مدارک مهندسی را بخواند، اطلاعات مهندسی را استخراج کند، Entityها را شناسایی کند، روابط بین آنها را بفهمد، اطلاعات یک Entity را بین مدارک مختلف تطبیق دهد، تناقض‌ها و Missing Data را پیدا کند، Revisionها را مقایسه کند و تمام نتایج را به منبع اصلی قابل ردیابی نگه دارد.

---

# 2. Reference Benchmark — Tagsight

برای طراحی این قابلیت، Tagsight را به‌عنوان **Reference Product** بررسی کن، اما معماری یا کد آن را کپی نکن.

Tagsight در وضعیت فعلی خود روی این مفاهیم تمرکز دارد:

- استخراج اطلاعات از P&ID
- Instrument Index
- I/O List
- Equipment Register
- Line Register
- Valve Register
- استخراج از Datasheet
- Cable Schedule
- Loop Diagram
- JB Schedule
- Cause & Effect
- SLD
- SAMA
- Cross-document reconciliation
- Orphan detection
- Attribute disagreement detection
- Revision comparison
- Added / Removed / Modified detection
- Traceability به Sheet/Page
- Engineer review
- Confidence score
- Export structured engineering data
- As-built reconciliation
- MOC support
- Handover / CMMS preparation

این قابلیت‌ها در سایت فعلی Tagsight نیز مستند شده‌اند.

Reference sources:

- https://tagsight.io/
- https://tagsight.io/about
- https://tagsight.io/help/getting-started
- https://tagsight.io/workflows
- https://tagsight.io/solutions

در ممیزی خود، این Reference را با معماری فعلی PEMS مقایسه کن.

**هدف تقلید از Tagsight نیست؛ هدف ساخت نسخه Enterprise و Multi-Discipline همین Concept داخل PEMS است.**

---

# 3. مسئله اصلی PEMS

PEMS نباید فقط این کار را انجام دهد:

```text
Upload PDF
      ↓
OCR
      ↓
AI Summary
```

این معماری برای Engineering Intelligence کافی نیست.

معماری هدف باید این باشد:

```text
Engineering Documents
        ↓
Document Ingestion
        ↓
Document Classification
        ↓
Page / Sheet Analysis
        ↓
OCR / Vision / Text Extraction
        ↓
Engineering Entity Extraction
        ↓
Entity Resolution
        ↓
Relationship Extraction
        ↓
Cross-Document Reconciliation
        ↓
Conflict Detection
        ↓
Missing / Orphan Detection
        ↓
Revision Comparison
        ↓
Engineer Review
        ↓
Approved Engineering Record
        ↓
Engineering Knowledge Base
        ↓
Search / Analytics / AI Assistant / CMMS / VDR / MDR
```

---

# 4. مفهوم کلیدی: Engineering Entity

سیستم نباید Document را فقط به عنوان فایل ببیند.

هر Document باید منبعی برای استخراج Entity باشد.

حداقل Entityهای اولیه:

```text
Document
Drawing
Sheet
Tag
Equipment
Instrument
Valve
Line
Loop
Cable
JunctionBox
I/O Point
Motor
Pump
Vessel
Tank
HeatExchanger
Requirement
Specification
Comment
Revision
Discipline
System
Subsystem
Area
Location
```

معماری باید به‌گونه‌ای باشد که بعداً بتوان Entityهای جدید اضافه کرد بدون بازطراحی کل سیستم.

---

# 5. Tag باید First-Class Entity باشد

Tag فقط یک String در Database نباشد.

مثلاً:

```text
PT-101
```

باید بتواند دارای این اطلاعات باشد:

```text
Tag
TagType
NormalizedTag
OriginalTag
Discipline
System
Subsystem
Service
Description
SignalType
EquipmentId
LineId
LoopId
DocumentReferences
SourceLocations
Confidence
Status
Revision
CreatedFromAI
VerifiedBy
VerifiedAt
```

اما مهم‌تر از همه:

**Original Tag باید حفظ شود.**

AI نباید Tag را بدون دلیل به یک استاندارد دیگر تبدیل کند.

مثلاً:

```text
ISA:
PT-101

KKS:
10LAC01CT001

IEC 81346:
=H1+R2-B1
```

هر convention باید قابل نگهداری باشد.

---

# 6. Document Intelligence Pipeline

Pipeline باید به صورت مشخص طراحی شود.

## Stage 1 — Ingestion

ورودی:

```text
PDF
PNG
JPG
TIFF
Excel
DOCX
```

در آینده:

```text
DWG
DGN
CAD exports
```

Document باید دارای:

```text
DocumentId
FileName
FileHash
DocumentType
Discipline
Project
Revision
DocumentNumber
Title
Status
IssueDate
Source
PageCount
ProcessingStatus
```

باشد.

---

# 7. Document Classification

AI باید بتواند نوع مدرک را تشخیص دهد.

نمونه:

```text
P&ID
Instrument Datasheet
Instrument Index
Cable Schedule
JB Schedule
Loop Diagram
Cause & Effect
SLD
Equipment Datasheet
Line List
Motor List
MCC Schedule
HAZOP
SIL
Specification
Procedure
Calculation
Report
Letter
MDR
VDR
```

Classification باید:

- AI-based
- Rule-based fallback
- Confidence scored
- Human override capable

باشد.

---

# 8. Page / Sheet Level Intelligence

Document فقط یک فایل نیست.

هر Page باید Entity مستقل باشد.

مثلاً:

```text
Document
 ├── Page 1
 ├── Page 2
 ├── Page 3
 └── Page 4
```

هر Extraction باید دقیقاً مشخص کند از کدام:

```text
Document
Revision
Page
Sheet
Region
Bounding Box
```

استخراج شده است.

---

# 9. Provenance — الزام حیاتی

هیچ داده AI نباید بدون Source پذیرفته شود.

هر Extraction باید دارای:

```text
SourceDocumentId
SourceRevisionId
SourcePageNumber
SourceRegion
BoundingBox
ExtractionMethod
Model
ModelVersion
Confidence
CreatedAt
ReviewedBy
ReviewedAt
ReviewStatus
```

باشد.

مثلاً:

```text
PT-101
Range = 0-100 bar

Source:
Instrument Datasheet
Document No: INS-DS-001
Revision: 03
Page: 7
Region: X=421,Y=238,W=300,H=110
Confidence: 0.94
```

هدف:

> هر داده مهندسی باید در کمتر از چند ثانیه به محل اصلی آن قابل Trace باشد.

---

# 10. Confidence Score

AI نباید فقط مقدار استخراج‌شده را ذخیره کند.

باید:

```text
Confidence = 0.94
```

نیز ذخیره شود.

سطوح پیشنهادی:

```text
0.90 – 1.00  High
0.75 – 0.89  Medium
0.00 – 0.74  Low
```

اما Confidence به تنهایی مجوز استفاده از داده نیست.

---

# 11. Human-in-the-Loop

AI تصمیم نهایی مهندسی نیست.

چرخه:

```text
AI Extracted
      ↓
AI Classified
      ↓
AI Reconciled
      ↓
Engineer Review
      ↓
Approved / Corrected / Rejected
```

Review باید بتواند:

```text
Accept
Edit
Reject
Add Missing Entity
Merge
Split
Ignore
Mark as Conflict
```

انجام دهد.

---

# 12. Cross-Document Reconciliation

این مهم‌ترین قابلیت پروژه است.

فرض:

```text
PT-101
```

در مدارک زیر وجود دارد:

```text
P&ID
Instrument Index
Datasheet
Loop Diagram
Cable Schedule
JB Schedule
I/O List
Cause & Effect
```

سیستم باید آنها را به یک Entity واحد متصل کند.

نه اینکه 8 رکورد مستقل ایجاد کند.

---

# 13. Golden Engineering Record

برای هر Entity یک Golden Record ایجاد شود.

مثلاً:

```text
PT-101
```

Golden Record:

```text
Tag: PT-101

Type:
Pressure Transmitter

Service:
Reactor Pressure

Range:
0–100 bar

Signal:
4-20mA

Loop:
L-101

Equipment:
V-101

JB:
JB-12

Cable:
C-102

Status:
Under Review
```

اما هر Attribute باید Source داشته باشد.

---

# 14. Attribute-Level Reconciliation

Reconciliation نباید فقط Tag-level باشد.

مثلاً:

```text
PT-101
```

در Datasheet:

```text
Range = 0–100 bar
```

در Instrument Index:

```text
Range = 0–150 bar
```

سیستم باید نتیجه زیر را بسازد:

```text
CONFLICT

Entity:
PT-101

Attribute:
Range

Source A:
Datasheet Rev.03
0–100 bar

Source B:
Instrument Index Rev.02
0–150 bar

Severity:
High

Action:
Engineer Review Required
```

---

# 15. Conflict Types

حداقل:

```text
Tag Conflict
Description Conflict
Type Conflict
Range Conflict
Service Conflict
Equipment Conflict
Line Conflict
Loop Conflict
Signal Conflict
Revision Conflict
Status Conflict
Missing Document
Missing Datasheet
Missing Tag
Orphan Tag
Duplicate Tag
Cross-reference Failure
```

---

# 16. Orphan Detection

سیستم باید بتواند موارد زیر را پیدا کند.

مثلاً:

```text
PT-101 exists in P&ID
BUT
No Datasheet
```

یا:

```text
PT-102 exists in Datasheet
BUT
Not found in P&ID
```

یا:

```text
Cable C-101 references PT-101
BUT
PT-101 has no matching loop
```

اینها باید به صورت:

```text
Orphan
Phantom
Missing Reference
Broken Relationship
```

نمایش داده شوند.

---

# 17. Relationship Graph

Entityها باید رابطه داشته باشند.

مثلاً:

```text
PT-101
   │
   ├── monitors → V-101
   ├── belongs_to → Loop-101
   ├── connected_to → JB-12
   ├── uses → Cable-C102
   ├── appears_in → P&ID-001
   ├── documented_by → DS-PT101
   └── referenced_by → IO-List
```

این Relationship Layer باید پایه Knowledge Graph آینده PEMS باشد.

---

# 18. Revision Intelligence

Revision صرفاً یک Number نباشد.

سیستم باید بتواند:

```text
Revision A
      ↓
Revision B
      ↓
Diff Engine
```

اجرا کند.

نتیجه:

```text
Added
Removed
Modified
Renamed
Moved
Reclassified
Relationship Changed
Attribute Changed
```

---

# 19. Revision Example

مثلاً:

```text
Rev.02

PT-101
Range = 0–100 bar
```

و:

```text
Rev.03

PT-101
Range = 0–150 bar
```

سیستم باید:

```text
Modified Entity

PT-101

Attribute:
Range

Old:
0–100 bar

New:
0–150 bar

Revision:
02 → 03

Impact:
Engineering Review Required
```

---

# 20. Revision Impact Analysis

تغییر یک Entity نباید فقط همان Entity را نشان دهد.

مثلاً:

```text
PT-101 Range Changed
```

باید بررسی شود:

```text
Datasheet
I/O
Loop
Alarm
Cause & Effect
SIL
Control Narrative
Cable
Commissioning
MDR
VDR
```

آیا تحت تأثیر هستند یا خیر.

---

# 21. Change Impact Graph

هدف:

```text
PT-101
  ↓
Range Changed
  ↓
Loop-101
  ↓
AI Channel
  ↓
PLC/DCS
  ↓
Alarm
  ↓
Cause & Effect
```

سیستم باید Impact را نشان دهد.

---

# 22. Engineering Register Layer

PEMS باید بتواند Registerهای استاندارد تولید کند.

حداقل:

```text
Instrument Register
I/O List
Equipment List
Line List
Valve List
Motor List
Cable Register
JB Register
Loop Register
```

این Registerها باید از یک مدل داده مشترک تولید شوند.

**نباید برای هر Register یک دیتابیس مستقل و تکراری ساخته شود.**

---

# 23. Engineering Document Intelligence برای همه Disciplineها

Tagsight عمدتاً Controls / P&ID محور است.

PEMS باید Multi-Discipline باشد.

### Process

```text
PFD
P&ID
Process Datasheet
Process Calculation
```

### Mechanical

```text
Equipment Datasheet
Mechanical Datasheet
Vendor Documents
```

### Piping

```text
P&ID
Line List
Isometric
Piping Class
```

### Instrumentation

```text
Instrument Index
Datasheet
Loop Diagram
Cable Schedule
JB Schedule
I/O List
Cause & Effect
```

### Electrical

```text
SLD
Cable Schedule
Load List
MCC
Motor List
Transformer Schedule
```

### Civil / Structural

```text
Foundation
Structural Drawing
Layout
```

### HSE / Process Safety

```text
HAZOP
SIL
Cause & Effect
Safety Requirements
```

---

# 24. MDR / VDR Integration

Engineering Intelligence نباید یک Island باشد.

باید با:

```text
MDR
VDR
Engineering Documents
Correspondence
Comments
Review
Transmittal
Revision
```

ارتباط داشته باشد.

مثلاً:

```text
PT-101
 ↓
Engineering Document
 ↓
MDR
 ↓
VDR
 ↓
Comment
 ↓
Revision
```

---

# 25. Semantic Search

Search فقط Exact Match نباشد.

اگر کاربر وارد کند:

```text
PT-101
```

پیدا کند.

اگر وارد کند:

```text
101
```

نیز موارد مرتبط را پیدا کند.

اگر وارد کند:

```text
pressure transmitter
```

Tagهای مرتبط را پیدا کند.

اگر وارد کند:

```text
transmitter connected to V-101
```

Relationship را نیز جستجو کند.

اگر وارد کند:

```text
range 0-100 bar
```

Attribute search انجام شود.

این قابلیت باید در کل PEMS قابل استفاده باشد:

```text
Engineering Documents
MDR
VDR
Correspondence
Comments
CMMS
Equipment
Tags
Requirements
```

---

# 26. AI Search Architecture

ترکیب:

```text
Exact Search
+
Full Text Search
+
Metadata Search
+
Semantic Search
+
Entity Search
+
Relationship Search
```

استفاده شود.

صرفاً Vector Search کافی نیست.

---

# 27. AI Assistant

AI Assistant باید بتواند سؤال‌هایی مانند این را پاسخ دهد:

> PT-101 در چه مدارکی وجود دارد؟

> آخرین Revision آن چیست؟

> Range آن چیست؟

> آیا بین مدارک تناقض وجود دارد؟

> PT-101 به کدام Equipment متصل است؟

> چه مدارکی هنوز برای PT-101 Missing هستند؟

> چه چیزهایی در Rev.03 تغییر کرده‌اند؟

> این تغییر چه مدارکی را تحت تأثیر قرار می‌دهد؟

AI باید پاسخ را با Source ارائه کند.

مثلاً:

```text
PT-101
Range: 0–100 bar

Source:
INS-DS-101 Rev.03
Page 7

Confidence:
94%

Conflict:
Instrument Index Rev.02 reports 0–150 bar.
```

---

# 28. AI نباید Hallucinate کند

اگر اطلاعات وجود ندارد:

```text
Not Found
```

یا:

```text
Insufficient Evidence
```

اعلام کند.

هرگز:

```text
Guess
```

نکند.

هر پاسخ Engineering باید بتواند به Source متصل شود.

---

# 29. Processing Architecture

یک Pipeline قابل توسعه بساز:

```text
DocumentProcessor
        ↓
DocumentClassifier
        ↓
PageProcessor
        ↓
OCRProcessor
        ↓
VisionProcessor
        ↓
EntityExtractor
        ↓
EntityResolver
        ↓
RelationshipExtractor
        ↓
ReconciliationEngine
        ↓
ConflictDetector
        ↓
RevisionDiffEngine
        ↓
ReviewEngine
        ↓
KnowledgeBase
```

هر بخش باید Interface مستقل داشته باشد.

---

# 30. AI Model Abstraction

AI Provider را Hard-Code نکن.

مثلاً:

```text
IAIProvider
```

با Implementations:

```text
OllamaProvider
OpenAIProvider
AzureOpenAIProvider
FutureProvider
```

Embedding نیز مستقل باشد:

```text
IEmbeddingProvider
```

---

# 31. Database Architecture

قبل از ایجاد Entity جدید، Database موجود را بررسی کن.

از ایجاد Duplicate Entity جلوگیری کن.

اگر Entity مشابه وجود دارد:

```text
Reuse
Refactor
Extend
```

و فقط در صورت ضرورت:

```text
Create New
```

---

# 32. Audit Trail

تمام عملیات مهم باید قابل ثبت باشند:

```text
AI Extraction
AI Correction
Engineer Review
Entity Merge
Entity Split
Attribute Change
Conflict Resolution
Revision Comparison
Approval
Rejection
```

حداقل:

```text
Who
When
What
Old Value
New Value
Source
Reason
```

---

# 33. Status Machine

Processing status را استاندارد کن:

```text
Uploaded
Queued
Processing
Classified
Extracted
Reconciled
ConflictDetected
UnderReview
Reviewed
Approved
Rejected
Superseded
KnowledgeBase
```

از Stringهای پراکنده استفاده نکن.

---

# 34. Dashboard

Engineering Intelligence Dashboard باید حداقل این موارد را نشان دهد:

```text
Documents
Processing
AI Confidence
Entities
Conflicts
Orphans
Missing Documents
Unresolved Reviews
Revision Changes
Approved Records
```

مثلاً:

```text
Documents: 1,284

Processed: 1,201

Entities: 42,381

Conflicts: 183

Orphans: 76

Pending Review: 241

Low Confidence: 94

Revision Changes: 327
```

---

# 35. Engineering Review Workspace

این صفحه بسیار مهم است.

کاربر باید بتواند:

```text
Source Drawing
        │
        ↓
Extracted Entity
        │
        ↓
Golden Record
        │
        ↓
Conflicts
        │
        ↓
Other Documents
```

را کنار هم ببیند.

Review نباید صرفاً یک جدول Excel-like باشد.

---

# 36. Source Viewer

کاربر باید بتواند:

```text
Open Source
```

کند و دقیقاً به:

```text
Document
Page
Region
Bounding Box
```

برود.

اگر PDF است:

- Page navigation
- Zoom
- Highlight extracted region
- Highlight Entity
- Show confidence
- Show conflicts

ضروری است.

---

# 37. UI/UX

UI باید:

```text
Modern
Professional
Engineering-oriented
RTL
Responsive
```

باشد.

اما در این مرحله:

**UI Polish اولویت دوم است.**

اول:

```text
Architecture
Data Model
Pipeline
Traceability
Reconciliation
Review
```

باید درست شود.

---

# 38. CMMS Integration

Engineering Intelligence باید در آینده به CMMS متصل شود.

مثلاً:

```text
Equipment
   ↓
Engineering Intelligence
   ↓
Equipment Register
   ↓
CMMS Asset
```

از همین ابتدا Data Model را طوری طراحی کن که Asset و Equipment قابل Federation باشند.

---

# 39. خروجی نهایی مورد انتظار

برای هر Document Package سیستم باید بتواند چنین خروجی تولید کند:

```text
DOCUMENT PACKAGE
        ↓
DOCUMENT INVENTORY
        ↓
CLASSIFICATION
        ↓
ENTITY REGISTER
        ↓
RELATIONSHIP GRAPH
        ↓
CROSS-DOCUMENT RECONCILIATION
        ↓
CONFLICT REPORT
        ↓
MISSING / ORPHAN REPORT
        ↓
REVISION CHANGE REPORT
        ↓
ENGINEER REVIEW
        ↓
APPROVED ENGINEERING DATA
```

---

# 40. گزارش Reconciliation

نمونه:

| Entity | Attribute | Source A | Source B | Result | Severity |
|---|---|---|---|---|---|
| PT-101 | Range | 0-100 bar | 0-150 bar | Conflict | High |
| PT-102 | Loop | L-102 | L-102 | Match | None |
| FT-201 | Datasheet | Exists | Missing | Orphan | Medium |
| XV-301 | Tag | Exists | Renamed | Changed | Medium |

---

# 41. Quality Gates

هیچ داده‌ای نباید مستقیم وارد Golden Record شود مگر اینکه:

```text
Source Exists
+
Extraction Valid
+
Entity Resolved
+
Confidence Acceptable
+
Conflict Evaluated
```

و در صورت نیاز:

```text
Engineer Approved
```

باشد.

---

# 42. مهم‌ترین اصل معماری

**AI Layer نباید مالک Business Data باشد.**

AI فقط:

```text
Extract
Classify
Suggest
Match
Compare
Explain
```

می‌کند.

Business Layer تصمیم نهایی را می‌گیرد.

---

# 43. چیزهایی که نباید انجام دهی

ممنوع:

1. ایجاد Entityهای Duplicate
2. ایجاد Serviceهای موازی برای یک مفهوم
3. ذخیره JSON خام به جای مدل داده واقعی
4. وابستگی مستقیم Controller به AI
5. وابستگی مستقیم Entity به Ollama
6. حذف Source Traceability
7. ذخیره AI Result بدون Confidence
8. ذخیره AI Result بدون Document Reference
9. Exact Search به عنوان تنها Search
10. Vector Search به عنوان تنها Search
11. بازنویسی Tag به ISA بدون حفظ Original
12. تغییر Migrationهای قبلی بدون بررسی اثر
13. حذف Data موجود
14. تغییر معماری فعلی بدون Audit
15. پیاده‌سازی UI قبل از تثبیت Data Model
16. استفاده از AI برای حدس Engineering Data
17. ایجاد CQRS/Microservices فقط برای پیچیده‌کردن معماری
18. ایجاد Multi-Tenancy مگر اینکه نیاز واقعی پروژه ثابت شود.

---

# 44. ترتیب اجرای پروژه

## Phase 0 — Repository Audit

ابتدا:

```text
Repository
Database
Entities
DbContext
Migrations
Services
Controllers
Views
AI
Document Processing
Search
MDR
VDR
CMMS
```

را بررسی کن.

خروجی:

```text
CURRENT ARCHITECTURE MAP
```

---

## Phase 1 — Gap Analysis

مقایسه:

```text
CURRENT PEMS
        vs
TARGET ENGINEERING INTELLIGENCE
```

خروجی:

```text
Missing
Weak
Duplicate
Broken
Reusable
Refactor Required
New Required
```

---

## Phase 2 — Domain Model

قبل از Coding:

```text
Entity Model
Relationship Model
Document Model
Revision Model
Provenance Model
Review Model
Conflict Model
```

را طراحی کن.

---

## Phase 3 — Migration Plan

Migrationها را مرحله‌بندی کن.

هیچ Migration destructive بدون دلیل و approval انجام نده.

---

## Phase 4 — Extraction Engine

ابتدا:

```text
Document
Page
Text
OCR
Classification
Entity Extraction
```

---

## Phase 5 — Reconciliation Engine

سپس:

```text
Entity Resolution
Attribute Matching
Cross-document Matching
Conflict Detection
Orphan Detection
```

---

## Phase 6 — Revision Engine

بعد:

```text
Revision Diff
Added
Removed
Modified
Renamed
Impact
```

---

## Phase 7 — Review Workspace

بعد:

```text
Source Viewer
Extraction Review
Conflict Review
Approval
Correction
```

---

## Phase 8 — Semantic Search

بعد:

```text
Exact
Full Text
Metadata
Semantic
Entity
Relationship
```

---

## Phase 9 — AI Assistant

در نهایت AI Assistant روی داده‌های validated قرار گیرد.

---

# 45. Definition of Done

این پروژه زمانی کامل تلقی می‌شود که بتوانیم:

### Test 1

یک P&ID آپلود کنیم.

سیستم بتواند:

```text
Tags
Equipment
Lines
Valves
Loops
```

را استخراج کند.

---

### Test 2

برای:

```text
PT-101
```

حداقل چند Document را به یک Entity متصل کند.

---

### Test 3

اگر:

```text
Datasheet = 0-100 bar
Index = 0-150 bar
```

باشد:

```text
CONFLICT
```

تولید شود.

---

### Test 4

اگر Tag فقط در یک Document باشد:

```text
ORPHAN
```

تولید شود.

---

### Test 5

اگر Revision تغییر کند:

```text
ADDED
REMOVED
MODIFIED
```

تشخیص داده شود.

---

### Test 6

هر Extraction باید بتواند:

```text
Open Source
```

را انجام دهد و به Page/Region صحیح برسد.

---

### Test 7

AI Assistant سؤال زیر را پاسخ دهد:

> What documents reference PT-101?

و Source ارائه کند.

---

### Test 8

AI Assistant سؤال زیر را پاسخ دهد:

> Does PT-101 have any conflicting attributes?

و Conflict واقعی را نشان دهد.

---

# 46. خروجی Audit که قبل از Coding باید به من ارائه شود

قبل از پیاده‌سازی، یک گزارش با این ساختار تولید کن:

```text
1. Current Architecture
2. Current Engineering Document Architecture
3. Existing Entities
4. Existing Services
5. Existing AI Pipeline
6. Existing Search
7. Existing MDR/VDR Integration
8. Existing CMMS Integration
9. Existing Revision Handling
10. Existing Traceability
11. Existing Migration State
12. Duplicate Components
13. Architectural Weaknesses
14. Missing Components
15. Tagsight Benchmark Comparison
16. Target Architecture
17. Target Domain Model
18. Target Data Flow
19. Migration Strategy
20. Implementation Phases
21. Risk Register
22. Test Strategy
23. Definition of Done
```

---

# 47. قانون مهم برای Claude

**قبل از اینکه چیزی را بسازی، ثابت کن که چیزی مشابه آن در PEMS وجود ندارد.**

برای هر Component جدید بنویس:

```text
Component:
Why Required:
Existing Equivalent:
Reuse / Refactor / New:
Dependencies:
Database Impact:
Migration Impact:
API Impact:
UI Impact:
AI Impact:
```

---

# 48. اصل نهایی

PEMS نباید تبدیل به:

> "ChatGPT for PDFs"

شود.

PEMS باید تبدیل شود به:

> **Engineering Intelligence Platform**

که بتواند:

```text
READ
UNDERSTAND
EXTRACT
CLASSIFY
CONNECT
RECONCILE
COMPARE
DETECT
TRACE
REVIEW
APPROVE
SEARCH
EXPLAIN
```

را روی Engineering Data انجام دهد.

---

# 49. دستور نهایی به Claude

ابتدا فقط **Audit و Architecture Assessment** را انجام بده.

در این مرحله:

**کد جدید گسترده ننویس.**

ابتدا Repository و Database را کامل بررسی کن.

سپس گزارش Audit را ارائه کن.

بعد از تأیید معماری:

```text
Domain Model
        ↓
Database
        ↓
Services
        ↓
AI Pipeline
        ↓
Reconciliation
        ↓
Revision
        ↓
Review
        ↓
Search
        ↓
UI
```

را مرحله‌به‌مرحله پیاده‌سازی کن.

در تمام مراحل:

> **Preserve existing functionality. Do not break MDR, VDR, Engineering Documents, Correspondence, CMMS or existing workflows.**

و هر مرحله باید با:

```text
Build
Migration
Unit Test
Integration Test
Regression Test
Manual Verification
```

تحویل شود.

**هیچ قابلیت AI را بدون Source Traceability، Confidence، Review State و Audit Trail نهایی تلقی نکن.**

---

# 50. معیار نهایی موفقیت

اگر یک مهندس از PEMS بپرسد:

> «PT-101 را برای من بررسی کن.»

سیستم باید بتواند پاسخ دهد:

```text
PT-101

Type:
Pressure Transmitter

Service:
Reactor Pressure

Equipment:
V-101

Loop:
L-101

Signal:
4-20mA

Documents:
7

Latest Revision:
Rev.03

Conflicts:
1

Missing Documents:
0

Orphans:
0

Confidence:
94%

Changed Since Previous Revision:
Range

Impact:
Loop / I/O Review Required
```

و برای هر مورد بتواند:

```text
View Source
```

را انجام دهد.

**این استاندارد هدف Engineering Intelligence در PEMS است.**