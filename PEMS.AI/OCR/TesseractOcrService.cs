using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tesseract;

namespace PEMS.Application.AI.OCR;

public class TesseractOcrService : IOcrService, IDisposable
{
    private readonly OcrOptions _options;
    private readonly ILogger<TesseractOcrService> _logger;
    private readonly object _engineLock = new();
    private TesseractEngine? _engine;

    public TesseractOcrService(IOptions<OcrOptions> options, ILogger<TesseractOcrService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> RecognizeAsync(byte[] imageBytes)
    {
        return await Task.Run(() =>
        {
            try
            {
                var engine = GetOrCreateEngine();
                using var img = Pix.LoadFromMemory(imageBytes);
                using var page = engine.Process(img);
                return page.GetText() ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OCR روی یک تصویر با شکست مواجه شد");
                return string.Empty;
            }
        });
    }

    public async Task<string> RecognizeFileAsync(string imagePath)
    {
        return await Task.Run(() =>
        {
            try
            {
                var engine = GetOrCreateEngine();
                using var img = Pix.LoadFromFile(imagePath);
                using var page = engine.Process(img);
                return page.GetText() ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OCR روی فایل {ImagePath} با شکست مواجه شد", imagePath);
                return string.Empty;
            }
        });
    }

    // IOcrService به‌صورت Scoped ثبت شده (یک نمونه در طول هر درخواست/پردازش یک سند) - Engine یک‌بار
    // برای کل آن Scope ساخته و در همه‌ی صفحات/تصاویر همان سند reuse می‌شود، نه per-image. ساخت
    // TesseractEngine یعنی لود trained-data از دیسک که تکرارش به‌ازای هر تصویر کاملاً غیرضروری بود.
    private TesseractEngine GetOrCreateEngine()
    {
        if (_engine != null)
            return _engine;

        lock (_engineLock)
        {
            _engine ??= CreateEngine();
            return _engine;
        }
    }

    private TesseractEngine CreateEngine()
    {
        return new TesseractEngine(_options.TessDataPath, _options.Languages, EngineMode.Default);
    }

    public void Dispose()
    {
        lock (_engineLock)
        {
            _engine?.Dispose();
            _engine = null;
        }
    }
}
