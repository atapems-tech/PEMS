using PEMS.Application.AI.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace PEMS.Application.AI.Parser;

/// <summary>
/// استخراج جدول از PDF های Native (متن Vector واقعی، نه اسکن) صرفاً با هندسه‌ی مختصات کلمات -
/// بدون هیچ فراخوانی مدل زبانی. برای اسناد بزرگ (ده‌ها صفحه) این روش هم سریع‌تر است (بدون درخواست
/// متوالی به Ollama) و هم دقیق‌تر، چون چینش ستونی واقعی کلمات حفظ می‌شود؛ در مسیر AI این چینش با
/// flatten شدن به یک رشته‌ی متن خطی از بین می‌رود.
///
/// عمداً به این سند/الگو وابسته نیست: تشخیص هدر بر اساس واژگان عمومی مهندسی (همان الگوی
/// EngineeringExtractionPromptBuilder.BuildForTableDocument) و تشخیص ستون بر اساس مجاورت X است،
/// نه مختصات یا متن ثابت. اگر یک فایل دیگر با ستون‌بندی متفاوت آپلود شود، همین منطق دوباره اجرا
/// و تطبیق داده می‌شود.
/// </summary>
public static class DeterministicTableExtractor
{
    private const double RowYTolerance = 3;
    private const double ColumnMergeGap = 15;
    private const int MinHeaderKeywordHits = 3;
    private const int MaxHeaderScanRows = 30;

    private static readonly string[] HeaderKeywords =
    {
        "tag", "no", "description", "service", "junction", "box", "cable", "terminal",
        "marshalling", "cabinet", "equipment", "type", "channel", "address", "dcs", "loop",
        "system", "signal", "note", "remark", "spare", "position", "module", "base", "field",
        "instrument"
    };

    public static bool CanHandle(string fileExtension) =>
        string.Equals(fileExtension, ".pdf", StringComparison.OrdinalIgnoreCase);

    public static ExtractedTableData Extract(string filePath)
    {
        var rows = new List<ExtractedTableRow>();
        var warnings = new List<string>();
        ExtractedTableDocumentInfo? docInfo = null;
        int pageCount = 0;
        int pagesWithTable = 0;

        using (var document = PdfDocument.Open(filePath))
        {
            foreach (var page in document.GetPages())
            {
                pageCount++;
                var words = page.GetWords().ToList();
                if (words.Count == 0) continue;

                if (docInfo == null)
                    docInfo = TryExtractDocumentInfo(words);

                var pageRows = ClusterRows(words);
                var (headerStart, headerEnd) = FindHeaderBlock(pageRows);

                if (headerStart < 0)
                {
                    // صفحات جلد/فهرست بدون جدول - رد می‌شوند، نه خطا
                    continue;
                }

                pagesWithTable++;
                var headerRows = pageRows.Skip(headerStart).Take(headerEnd - headerStart + 1).ToList();
                var bins = BuildColumnBins(headerRows);
                var dataRows = pageRows.Skip(headerEnd + 1).ToList();

                rows.AddRange(BuildBlockRows(dataRows, bins));
            }
        }

        if (pagesWithTable == 0)
            warnings.Add("No recognizable table header was found on any page - the document may not be a tabular format, or column headers use unrecognized wording.");

        return new ExtractedTableData
        {
            DocumentInfo = docInfo,
            Rows = rows,
            RowCount = rows.Count,
            Warnings = warnings,
            Summary = $"{rows.Count} rows extracted deterministically from {pagesWithTable}/{pageCount} page(s) - no AI/LLM used."
        };
    }

    // برخی قالب‌های نقشه (مثل جفت ترمینال +/- در دیاگرام سیم‌کشی) اطلاعات یک تگ را در چند خط
    // مجاور پخش می‌کنند - بعضی خط‌ها قبل از ردیف اصلی (دارای Tag) و بعضی بعد از آن. به‌جای فرض
    // جهت ثابت (فقط جلو یا فقط عقب)، هر ردیف بدون Tag به نزدیک‌ترین ردیف دارای Tag بر مبنای
    // فاصله‌ی واقعی Y صفحه merge می‌شود - این هندسی و عمومی است، نه فرضی خاص این سند.
    private static List<ExtractedTableRow> BuildBlockRows(List<List<Word>> dataRows, List<ColumnBin> bins)
    {
        var anchors = new List<(double Y, ExtractedTableRow Row)>();
        var orphans = new List<(double Y, ExtractedTableRow Row)>();

        foreach (var dataRow in dataRows)
        {
            var cells = AssignToBins(dataRow, bins);
            if (cells.Values.All(string.IsNullOrWhiteSpace)) continue;

            var candidate = MapToTableRow(cells);
            var y = dataRow[0].BoundingBox.Bottom;

            if (!string.IsNullOrWhiteSpace(candidate.TagNumber))
                anchors.Add((y, candidate));
            else
                orphans.Add((y, candidate));
        }

        if (anchors.Count == 0)
        {
            foreach (var o in orphans)
            {
                ExtractStrayTerminalNumbers(o.Row);
                StripTagDescriptionOverlap(o.Row);
            }
            return orphans.Select(o => o.Row).ToList();
        }

        foreach (var (y, orphanRow) in orphans)
        {
            var nearest = anchors.OrderBy(a => Math.Abs(a.Y - y)).First();
            MergeBlockRow(nearest.Row, orphanRow);
        }

        var result = anchors.OrderByDescending(a => a.Y).Select(a => a.Row).ToList();
        foreach (var row in result)
        {
            ExtractStrayTerminalNumbers(row);
            StripTagDescriptionOverlap(row);
        }
        return result;
    }

    private static readonly System.Text.RegularExpressions.Regex TerminalPolarityPattern =
        new(@"\b\d+\s*\([+-]\)", System.Text.RegularExpressions.RegexOptions.Compiled);

    // قرارداد رایج مهندسی برای ترمینال‌های دیفرانسیل: "17(+)"، "18(-)". این عدد به‌خاطر همپوشانی
    // مرز ستون‌ها گاهی داخل JunctionBox/CableNo/MarshallingCabinet جا می‌ماند - با همین الگوی عمومی
    // (نه چیزی خاص این سند) به فیلد درست Terminal منتقل می‌شود.
    private static void ExtractStrayTerminalNumbers(ExtractedTableRow row)
    {
        var (jb, t1) = SplitOutTerminalTokens(row.JunctionBox);
        var (cable, t2) = SplitOutTerminalTokens(row.CableNo);
        var (mc, t3) = SplitOutTerminalTokens(row.MarshallingCabinet);
        row.JunctionBox = jb;
        row.CableNo = cable;
        row.MarshallingCabinet = mc;

        var terminals = t1.Concat(t2).Concat(t3).ToList();
        if (terminals.Count == 0) return;

        var joined = string.Join(", ", terminals);
        row.Terminal = string.IsNullOrWhiteSpace(row.Terminal) ? joined : $"{row.Terminal}, {joined}";
    }

    // اگر به‌خاطر عرض نابرابر ستون‌ها (Description پهن کنار Tag باریک) یکی-دو کلمه‌ی ابتدایی توضیحات
    // اشتباهاً به انتهای TagNumber همان ردیف چسبیده باشد، همان کلمه دقیقاً ابتدای Description هم
    // هست - این هم‌پوشانی به‌جای حدس زدن مرز ستون، مستقیم با مقایسه‌ی دو فیلد کشف و پاک می‌شود.
    private static void StripTagDescriptionOverlap(ExtractedTableRow row)
    {
        if (string.IsNullOrWhiteSpace(row.TagNumber) || string.IsNullOrWhiteSpace(row.Description)) return;

        for (var guard = 0; guard < 3; guard++)
        {
            var lastSpace = row.TagNumber.TrimEnd().LastIndexOf(' ');
            if (lastSpace < 0) break;

            var trailingWord = row.TagNumber.TrimEnd()[(lastSpace + 1)..];
            if (trailingWord.Length == 0 || !row.Description.TrimStart().StartsWith(trailingWord, StringComparison.OrdinalIgnoreCase))
                break;

            row.TagNumber = row.TagNumber.TrimEnd()[..lastSpace].TrimEnd();
        }
    }

    private static (string? remaining, List<string> extracted) SplitOutTerminalTokens(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return (value, new List<string>());

        var matches = TerminalPolarityPattern.Matches(value);
        if (matches.Count == 0) return (value, new List<string>());

        var extracted = matches.Select(m => m.Value.Replace(" ", "")).ToList();
        var remaining = TerminalPolarityPattern.Replace(value, "");
        remaining = System.Text.RegularExpressions.Regex.Replace(remaining, @"[\s;]{2,}", "; ").Trim(' ', ';');
        return (string.IsNullOrWhiteSpace(remaining) ? null : remaining, extracted);
    }

    // مقادیر غیرخالی ردیف مبدا را در ردیف مقصد ادغام می‌کند - اگر فیلدی از قبل مقدار دارد، به‌جای
    // بازنویسی به آن append می‌شود؛ Reflection عمداً استفاده شده تا با اضافه‌شدن فیلد جدید به
    // ExtractedTableRow در آینده، این تابع بدون تغییر همچنان درست کار کند.
    private static void MergeBlockRow(ExtractedTableRow target, ExtractedTableRow source)
    {
        foreach (var prop in typeof(ExtractedTableRow).GetProperties())
        {
            if (prop.Name == nameof(ExtractedTableRow.RawExtra)) continue;

            if (prop.PropertyType == typeof(string))
            {
                var sourceValue = (string?)prop.GetValue(source);
                if (string.IsNullOrWhiteSpace(sourceValue)) continue;
                var targetValue = (string?)prop.GetValue(target);
                prop.SetValue(target, string.IsNullOrWhiteSpace(targetValue) ? sourceValue : $"{targetValue}; {sourceValue}");
            }
            else if (prop.PropertyType == typeof(bool?))
            {
                if ((bool?)prop.GetValue(source) == true) prop.SetValue(target, true);
            }
        }

        if (source.RawExtra == null) return;
        target.RawExtra ??= new Dictionary<string, string>();
        foreach (var (key, value) in source.RawExtra)
        {
            var finalKey = target.RawExtra.ContainsKey(key) ? $"{key}_{target.RawExtra.Count}" : key;
            target.RawExtra[finalKey] = value;
        }
    }

    private static List<List<Word>> ClusterRows(List<Word> words)
    {
        var rows = new List<List<Word>>();
        foreach (var w in words.OrderByDescending(w => w.BoundingBox.Bottom))
        {
            var row = rows.FirstOrDefault(r => Math.Abs(r[0].BoundingBox.Bottom - w.BoundingBox.Bottom) < RowYTolerance);
            if (row == null)
            {
                row = new List<Word>();
                rows.Add(row);
            }
            row.Add(w);
        }
        foreach (var r in rows) r.Sort((a, b) => a.BoundingBox.Left.CompareTo(b.BoundingBox.Left));
        return rows;
    }

    // ردیف‌های متوالی که کلیدواژه‌ی هدر دارند یک "بلوک هدر" را تشکیل می‌دهند (چون هدر جدول‌های
    // مهندسی معمولاً چند خط روی هم است، مثل "Marshalling / Cabinet / Information")
    private static (int start, int end) FindHeaderBlock(List<List<Word>> rows)
    {
        int firstHeaderRow = -1;
        int lastHeaderRow = -1;

        for (int i = 0; i < Math.Min(rows.Count, MaxHeaderScanRows); i++)
        {
            int hits = rows[i].Count(w => HeaderKeywords.Any(k =>
                string.Equals(w.Text.Trim('.', ':', ','), k, StringComparison.OrdinalIgnoreCase)));

            if (hits >= MinHeaderKeywordHits)
            {
                if (firstHeaderRow < 0) firstHeaderRow = i;
                lastHeaderRow = i;
            }
            else if (firstHeaderRow >= 0 && i > lastHeaderRow + 1)
            {
                break;
            }
        }

        return (firstHeaderRow, lastHeaderRow);
    }

    // هدر جدول‌های مهندسی معمولاً چند خط "مکمل" است، نه تکراری - مثلاً یک زیرستون ممکن است
    // فقط روی یکی از خط‌های هدر برچسب داشته باشد و روی خط دیگر نه. پس ابتدا هر خط جدا خوشه‌بندی
    // می‌شود (تا کلمات خطوط مختلف با هم قاطی نشوند)، سپس بین‌های خطوط مختلف فقط وقتی ادغام
    // می‌شوند که محدوده‌ی X شان واقعاً هم‌پوشانی داشته باشد (نه صرفاً نزدیک باشند) - این هم‌پوشانی
    // دقیقاً همان چینش عمودی واقعی صفحه است، نه یک آستانه‌ی حدسی.
    private static List<ColumnBin> BuildColumnBins(List<List<Word>> headerRows)
    {
        var perLineBins = headerRows
            .Select(line => BuildBinsForSingleLine(line.OrderBy(w => w.BoundingBox.Left).ToList()))
            .Where(b => b.Count > 0)
            .ToList();

        if (perLineBins.Count == 0) return new List<ColumnBin>();

        var flat = perLineBins
            .SelectMany((bins, lineIndex) => bins.Select(b => (bin: b, lineIndex)))
            .ToList();

        var parent = Enumerable.Range(0, flat.Count).ToArray();
        int Find(int x) => parent[x] == x ? x : (parent[x] = Find(parent[x]));
        void Union(int a, int b) { a = Find(a); b = Find(b); if (a != b) parent[a] = b; }

        for (int i = 0; i < flat.Count; i++)
        for (int j = i + 1; j < flat.Count; j++)
        {
            if (flat[i].lineIndex == flat[j].lineIndex) continue; // فقط بین خطوط مختلف ادغام می‌شود
            if (flat[i].bin.Left <= flat[j].bin.Right && flat[j].bin.Left <= flat[i].bin.Right)
                Union(i, j);
        }

        var bins = Enumerable.Range(0, flat.Count)
            .GroupBy(Find)
            .Select(group =>
            {
                var members = group.Select(idx => flat[idx]).OrderBy(m => m.lineIndex).ToList();
                return new ColumnBin
                {
                    Left = members.Min(m => m.bin.Left),
                    Right = members.Max(m => m.bin.Right),
                    Label = string.Join(" ", members.Select(m => m.bin.Label))
                };
            })
            .OrderBy(b => b.Left)
            .ToList();

        // مرز واقعی تخصیص هر ستون به داده، نقطه‌ی میانی با ستون مجاور است - نه لبه‌ی خام هرکدام.
        // لبه‌ی خام (برگرفته از عرض برچسب‌های ادغام‌شده‌ی چندخطی) می‌تواند به قلمرو ستون بعدی سرریز
        // کند و باعث شود اولین کلمه‌ی آن ستون (مثلاً اولین کلمه‌ی Description) به این ستون بچسبد.
        for (int i = 0; i < bins.Count - 1; i++)
        {
            var mid = (bins[i].Right + bins[i + 1].Left) / 2;
            bins[i].Right = mid;
            bins[i + 1].Left = mid;
        }

        // برچسب‌های تکراری (چند ستون "Equipment No." برای گروه‌های مختلف) با شماره متمایز می‌شوند
        // تا دیتای هیچ گروهی روی گروه دیگر بازنویسی نشود
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var bin in bins)
        {
            if (seen.TryGetValue(bin.Label, out var count))
            {
                seen[bin.Label] = count + 1;
                bin.Label = $"{bin.Label} ({count + 1})";
            }
            else
            {
                seen[bin.Label] = 1;
            }
        }

        return bins;
    }

    private static readonly string[] BareSequenceLabels = { "no", "no.", "sno", "s.no", "s.no.", "sl.no", "sl.no.", "item", "item no", "item no.", "#" };

    private static bool IsBareSequenceLabel(string label) =>
        BareSequenceLabels.Contains(label.Trim().ToLowerInvariant());

    private static List<ColumnBin> BuildBinsForSingleLine(List<Word> lineWords)
    {
        var bins = new List<ColumnBin>();
        foreach (var w in lineWords)
        {
            var bin = bins.LastOrDefault();

            // یک ستون تنهای "No." در چپ‌ترین موقعیت هدر تقریباً همیشه شماره‌ی ردیف (Item No./Sl. No.)
            // است، نه بخشی از یک برچسب ترکیبی مثل "Instrument Tag No." - حتی اگر فاصله‌اش با کلمه‌ی
            // بعدی کمتر از آستانه‌ی معمول باشد، هرگز نباید به جلو ادغام شود؛ وگرنه شماره‌ی ردیف با
            // مقدار واقعی تگ قاطی می‌شود (مثلاً "1 80-ZSX-520109" به‌جای "80-ZSX-520109").
            var isLeadingStandaloneNo = bins.Count == 1
                && IsBareSequenceLabel(bin!.Label);

            if (bin != null && !isLeadingStandaloneNo && w.BoundingBox.Left <= bin.Right + ColumnMergeGap)
            {
                bin.Right = Math.Max(bin.Right, w.BoundingBox.Right);
                bin.Label += " " + w.Text;
            }
            else
            {
                bins.Add(new ColumnBin { Left = w.BoundingBox.Left, Right = w.BoundingBox.Right, Label = w.Text });
            }
        }
        return bins;
    }

    private static Dictionary<string, string> AssignToBins(List<Word> row, List<ColumnBin> bins)
    {
        var result = bins.ToDictionary(b => b.Label, b => "");
        foreach (var w in row)
        {
            var center = (w.BoundingBox.Left + w.BoundingBox.Right) / 2;

            // اول ستونی انتخاب می‌شود که محدوده‌اش واقعاً این کلمه را در بر بگیرد - نه صرفاً
            // نزدیک‌ترین مرکز. وقتی عرض ستون‌ها خیلی متفاوت است (مثلاً Description پهن کنار
            // Tag باریک)، مقایسه‌ی فاصله تا مرکز می‌تواند کلمه را به ستون همسایه‌ی اشتباه بدهد
            // (مثلاً متن توضیحات کوتاه که نزدیک لبه‌ی چپ ستون Description است، اشتباهاً به Tag).
            var containing = bins.Where(b => center >= b.Left && center <= b.Right).ToList();
            var target = containing.Count switch
            {
                1 => containing[0],
                > 1 => containing.OrderBy(b => b.Right - b.Left).First(), // همپوشانی چندگانه - محدودترین ستون
                _ => bins.OrderBy(b => Math.Abs(((b.Left + b.Right) / 2) - center)).First()
            };

            result[target.Label] = string.IsNullOrEmpty(result[target.Label]) ? w.Text : result[target.Label] + " " + w.Text;
        }
        return result;
    }

    // نگاشت برچسب‌های خام ستون به فیلدهای شناخته‌شده - بر اساس واژگان کلی (همان الگوی Mapping
    // guidance در EngineeringExtractionPromptBuilder)، نه متن دقیق این سند. اگر یک فیلد از قبل
    // پر شده باشد (چند ستون خام معنای مشابه دارند)، مقدار دوم به‌جای بازنویسی در RawExtra می‌رود.
    private static ExtractedTableRow MapToTableRow(Dictionary<string, string> cells)
    {
        var row = new ExtractedTableRow();
        var setFields = new HashSet<string>();

        void SetOnce(Func<string?> get, Action<string> set, string fieldName, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (setFields.Contains(fieldName) || get() != null)
            {
                (row.RawExtra ??= new Dictionary<string, string>())[fieldName + "_extra_" + (row.RawExtra?.Count ?? 0)] = value;
                return;
            }
            set(value);
            setFields.Add(fieldName);
        }

        foreach (var (rawLabel, value) in cells)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            var label = rawLabel.ToLowerInvariant();

            if (label.Contains("dcs") && label.Contains("tag"))
                SetOnce(() => row.DcsTagNo, v => row.DcsTagNo = v, nameof(row.DcsTagNo), value);
            else if (label.Contains("tag") && !label.Contains("dcs"))
                SetOnce(() => row.TagNumber, v => row.TagNumber = v, nameof(row.TagNumber), value);
            else if (label.Contains("description") || label.Contains("service"))
                SetOnce(() => row.Description, v => row.Description = v, nameof(row.Description), value);
            else if (label.Contains("junction"))
                SetOnce(() => row.JunctionBox, v => row.JunctionBox = v, nameof(row.JunctionBox), value);
            else if (label.Contains("marshalling") && label.Contains("terminal"))
                SetOnce(() => row.MarshallingTerminal, v => row.MarshallingTerminal = v, nameof(row.MarshallingTerminal), value);
            else if (label.Contains("marshalling"))
                SetOnce(() => row.MarshallingCabinet, v => row.MarshallingCabinet = v, nameof(row.MarshallingCabinet), value);
            else if (label.Contains("system") && label.Contains("cabinet"))
                SetOnce(() => row.SystemCabinet, v => row.SystemCabinet = v, nameof(row.SystemCabinet), value);
            else if (label.Contains("cable"))
                SetOnce(() => row.CableNo, v => row.CableNo = v, nameof(row.CableNo), value);
            else if (label.Contains("terminal"))
                SetOnce(() => row.Terminal, v => row.Terminal = v, nameof(row.Terminal), value);
            else if ((label.Contains("i/o") || label.Contains("io")) && (label.Contains("card") || label.Contains("base") || label.Contains("module")))
                SetOnce(() => row.IoCard, v => row.IoCard = v, nameof(row.IoCard), value);
            else if (label.Contains("channel"))
                SetOnce(() => row.IoChannel, v => row.IoChannel = v, nameof(row.IoChannel), value);
            else if (label.Contains("address"))
                SetOnce(() => row.IoAddress, v => row.IoAddress = v, nameof(row.IoAddress), value);
            else if (label.Contains("loop"))
                SetOnce(() => row.LoopNo, v => row.LoopNo = v, nameof(row.LoopNo), value);
            else if (label.Contains("note") || label.Contains("remark"))
                SetOnce(() => row.Remarks, v => row.Remarks = v, nameof(row.Remarks), value);
            else if (label.Contains("signal") && (label.Contains("class") || label.Contains("type")))
                SetOnce(() => row.SignalClass, v => row.SignalClass = v, nameof(row.SignalClass), value);
            else if (label.Contains("system"))
                SetOnce(() => row.System, v => row.System = v, nameof(row.System), value);
            else
                (row.RawExtra ??= new Dictionary<string, string>())[rawLabel] = value;
        }

        if (cells.Values.Any(v => v.Contains("SPARE", StringComparison.OrdinalIgnoreCase)))
            row.SpareOrHotSpare = true;

        return row;
    }

    // بلوک تیتر صفحه‌ی اول را برای Doc. No./Rev/Project No. با همان روش کلیدواژه‌ای می‌کاود -
    // اختیاری است؛ اگر پیدا نشد، DocumentInfo با فیلدهای null برمی‌گردد نه خطا
    private static ExtractedTableDocumentInfo? TryExtractDocumentInfo(List<Word> firstPageWords)
    {
        var text = string.Join(" ", firstPageWords.OrderByDescending(w => w.BoundingBox.Bottom).ThenBy(w => w.BoundingBox.Left).Select(w => w.Text));
        var info = new ExtractedTableDocumentInfo
        {
            DocumentNo = ExtractAfter(text, "Doc. No.:") ?? ExtractAfter(text, "Doc No:"),
            RevisionNo = ExtractAfter(text, "Rev :") ?? ExtractAfter(text, "Rev:"),
        };
        return info.DocumentNo != null || info.RevisionNo != null ? info : null;
    }

    private static string? ExtractAfter(string text, string marker)
    {
        var idx = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var rest = text[(idx + marker.Length)..].TrimStart();
        var end = rest.IndexOf(' ');
        return end > 0 ? rest[..end] : rest;
    }

    private class ColumnBin
    {
        public string Label = "";
        public double Left;
        public double Right;
    }
}
