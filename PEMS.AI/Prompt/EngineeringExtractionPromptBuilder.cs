namespace PEMS.Application.AI.Prompt;

public static class EngineeringExtractionPromptBuilder
{
    private const int MaxInputCharacters = 6000;
    private const int MaxTableInputCharacters = 12000;

    // برش را روی مرز خط انجام می‌دهد نه وسط یک ردیف جدول - وگرنه آخرین ردیف نصفه به مدل داده می‌شود
    // و مدل مجبور می‌شود حدس بزند یا آن را به‌عنوان یک ردیف ناقص/نادرست گزارش کند
    private static string TrimToLineBoundary(string text, int maxCharacters)
    {
        if (text.Length <= maxCharacters)
            return text;

        var cut = text[..maxCharacters];
        var lastNewLine = cut.LastIndexOf('\n');
        return lastNewLine > 0 ? cut[..lastNewLine] : cut;
    }

    public static string Build(string documentText)
    {
        var trimmedText = documentText.Length > MaxInputCharacters
            ? documentText[..MaxInputCharacters]
            : documentText;

        return $$"""
You are an assistant that extracts structured engineering data from technical documents
(drawings, datasheets, procedures, reports) used in an industrial plant.

Read the document text below and return ONLY a single valid JSON object, with no explanation,
no markdown code fences, and no extra text before or after it. If a field is unknown, use null.
If there are no equipment tags or specifications in the text, return empty arrays for them.

The JSON must follow exactly this shape:

{
  "documentInfo": {
    "documentNo": string or null,
    "title": string or null,
    "category": one of ["Engineering Drawing", "Vendor Drawing", "Vendor Datasheet", "Material Requisition",
      "Material Requisition Query", "Technical Bid", "Purchase Order", "Contract", "Vendor Manual",
      "Inspection Report", "Commissioning Report", "Construction Package", "Correspondence",
      "Meeting Minutes", "Calculation", "Specification", "Other"] or null,
    "documentType": one of ["Drawing", "Datasheet", "Procedure", "Report", "P&ID", "Other"] or null,
    "discipline": one of ["Process", "Mechanical", "Electrical", "Instrumentation", "Piping", "Civil"] or null,
    "revisionNo": string or null,
    "issuePurpose": string or null,
    "vendorName": string or null,
    "relatedMRNumber": string or null,
    "area": string or null,
    "unit": string or null,
    "system": string or null,
    "subsystem": string or null,
    "projectHint": string or null
  },
  "equipmentTags": [
    {
      "tagNumber": string,
      "tagType": string or null,
      "area": string or null,
      "service": string or null,
      "description": string or null
    }
  ],
  "specifications": [
    {
      "relatedTagNumber": string or null,
      "fieldName": string,
      "fieldValue": string or null,
      "unit": string or null
    }
  ],
  "summary": string,
  "keywords": [string]
}

Document text (between the ---START--- and ---END--- markers):
---START---
{{trimmedText}}
---END---

Return only the JSON object now.
""";
    }

    // متن کامل سند را روی مرز خط به چند تکه‌ی هم‌اندازه با سقف کاراکتر مدل تقسیم می‌کند تا برای
    // جداول بزرگ (Wiring Diagram/I-O List با صدها ردیف) هیچ ردیفی حذف یا نصفه پردازش نشود -
    // فراخوان باید BuildForTableDocument را برای هر تکه جدا صدا بزند و rows/warnings را ادغام کند.
    public static IReadOnlyList<string> SplitTableTextIntoChunks(string text, int maxCharactersPerChunk = MaxTableInputCharacters)
    {
        if (string.IsNullOrEmpty(text))
            return new List<string> { text ?? string.Empty };

        var chunks = new List<string>();
        var remaining = text;

        while (remaining.Length > maxCharactersPerChunk)
        {
            var window = remaining[..maxCharactersPerChunk];
            var lastNewLine = window.LastIndexOf('\n');
            var splitAt = lastNewLine > 0 ? lastNewLine : maxCharactersPerChunk;

            chunks.Add(remaining[..splitAt]);
            remaining = remaining[splitAt..].TrimStart('\n');
        }

        if (remaining.Length > 0)
            chunks.Add(remaining);

        return chunks;
    }

    public static string BuildForTableDocument(string documentText, string? documentHint = null)
    {
        var trimmedText = TrimToLineBoundary(documentText, MaxTableInputCharacters);

        var hintBlock = string.IsNullOrWhiteSpace(documentHint)
            ? ""
            : $"""
               Document context (use as hint, do not invent data):
               {documentHint}
               """;

        return $$"""
You are an expert instrumentation engineer extracting structured data from industrial engineering tables
(Wiring Connection Diagrams, Instrument Index, Marshalling Lists, I/O Lists, Loop Drawings).

Your job is to read the table text below and return ONLY one valid JSON object.
No markdown, no code fences, no explanation before or after the JSON.

Rules:
1. Extract EVERY instrument/tag row you can find. Do not skip rows.
2. Prefer exact tag numbers as written (e.g. 80-ZX-33700, 80-TT-100101, ESD-3001).
3. If a column value is missing or unreadable, use null. Never invent values.
4. Normalize empty cells to null.
5. One row in the source table = one object in "rows".
6. If a row has no tag number and is only a continuation of the previous row's text (e.g. a wrapped
   description line), do NOT invent a tag number for it - either merge its text into the previous row's
   "remarks"/"description", or omit the row entirely. Never fabricate a tagNumber to satisfy the schema.
7. If the text is only a partial page/chunk, still extract all complete rows visible in this chunk, and
   add a note to "warnings" if the chunk appears to be cut off mid-table.
8. Keep original spelling of cabinets, cables, terminals (e.g. 80-EM-001, 80-EM-003).

Target JSON shape:

{
  "documentInfo": {
    "documentNo": string or null,
    "title": string or null,
    "documentType": "Wiring Diagram" | "Instrument Index" | "I/O List" | "Marshalling List" | "Loop Drawing" | "Other" | null,
    "discipline": "Instrumentation" | "Electrical" | "Process" | "Other" | null,
    "revisionNo": string or null,
    "vendorName": string or null,
    "area": string or null,
    "unit": string or null,
    "system": string or null
  },
  "rows": [
    {
      "tagNumber": string or null,
      "description": string or null,
      "instrumentType": string or null,
      "service": string or null,
      "signalType": string or null,
      "signalClass": "AI" | "AO" | "DI" | "DO" | "Pulse" | "Communication" | "Other" | null,
      "junctionBox": string or null,
      "terminal": string or null,
      "cableNo": string or null,
      "marshallingCabinet": string or null,
      "marshallingTerminal": string or null,
      "systemCabinet": string or null,
      "ioCard": string or null,
      "ioChannel": string or null,
      "ioAddress": string or null,
      "dcsTagNo": string or null,
      "loopNo": string or null,
      "location": string or null,
      "system": "DCS" | "ESD" | "F&G" | "PLC" | "Other" | null,
      "spareOrHotSpare": boolean or null,
      "remarks": string or null,
      "rawExtra": {
        "anyOtherColumnName": "value"
      }
    }
  ],
  "summary": string,
  "rowCount": number,
  "warnings": [string]
}

Mapping guidance (common column headers → fields):
- Instrument Tag No / Tag No / Tag → tagNumber
- Description / Service / Function → description or service
- Signal Type / Signal → signalType
- AI/AO/DI/DO → signalClass
- Junction Box / JB → junctionBox
- Terminal / TB → terminal
- Cable No / Cable → cableNo
- Marshalling Cabinet / Marshaling / MC → marshallingCabinet
- System Cabinet / System Cab → systemCabinet
- I/O Card / Module / Slot → ioCard
- Channel / Ch → ioChannel
- Address → ioAddress
- DCS Tag / System Tag → dcsTagNo
- Loop No / Loop → loopNo
- HOT SPARE / Spare → spareOrHotSpare = true

{{hintBlock}}

Table / document text:
---START---
{{trimmedText}}
---END---

Return only the JSON object now.
""";
    }
}
