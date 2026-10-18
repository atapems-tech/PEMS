using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Tesseract;

// usage:
//   PemsLinuxProbe ocr <image> <tessdataDir> <languages> [expectedWord ...]
//   PemsLinuxProbe dp-decrypt <keyDir> <applicationName> <ciphertextFile> [purpose]
// Exit code 0 = success, 1 = failure (reason printed). Secrets are never printed.
if (args.Length == 0) { Console.Error.WriteLine("usage: ocr <image> <tessdata> <lang> [expected...] | dp-decrypt <keyDir> <appName> <cipherFile> [purpose]"); return 2; }

switch (args[0])
{
    case "ocr":
    {
        if (args.Length < 4) { Console.Error.WriteLine("ocr <image> <tessdata> <lang> [expected...]"); return 2; }
        try
        {
            using var engine = new TesseractEngine(args[2], args[3], EngineMode.Default);
            using var img = Pix.LoadFromFile(args[1]);
            using var page = engine.Process(img);
            var text = (page.GetText() ?? "").Trim();
            Console.WriteLine($"OCR_TEXT_LENGTH={text.Length}  CONFIDENCE={page.GetMeanConfidence():0.00}");
            Console.WriteLine("OCR_TEXT=" + text.Replace("\r", " ").Replace("\n", " | "));
            var expected = args.Skip(4).ToArray();
            var missing = expected.Where(w => !text.Contains(w, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (expected.Length > 0) Console.WriteLine(missing.Length == 0 ? "OCR_RESULT=PASS (all expected words found)" : "OCR_RESULT=FAIL missing: " + string.Join(",", missing));
            return missing.Length == 0 && text.Length > 0 ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine("OCR_RESULT=FAIL " + Describe(ex));
            if (Environment.GetEnvironmentVariable("PROBE_VERBOSE") == "1") { var t = ex.ToString(); Console.WriteLine(t.Length > 1800 ? t[..1800] : t); }
            return 1;
        }
    }
    case "dp-decrypt":
    {
        if (args.Length < 4) { Console.Error.WriteLine("dp-decrypt <keyDir> <appName> <cipherFile> [purpose]"); return 2; }
        var purpose = args.Length > 4 ? args[4] : "PEMS.StorageProvider.Credentials.v1";
        try
        {
            var services = new ServiceCollection();
            var dp = services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(args[1]));
            if (args[2] != "-") dp.SetApplicationName(args[2]);     // "-" = no explicit application name (library default)
            using var sp = services.BuildServiceProvider();
            var protector = sp.GetRequiredService<IDataProtectionProvider>().CreateProtector(purpose);
            var cipher = File.ReadAllText(args[3]).Trim();
            Console.WriteLine($"CIPHER format prefix={cipher[..Math.Min(5, cipher.Length)]} length={cipher.Length}");
            var plain = protector.Unprotect(cipher);
            Console.WriteLine($"DP_DECRYPT=OK plaintextLength={plain.Length} (value not displayed)");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("DP_DECRYPT=FAIL " + ex.GetType().Name + ": " + ex.Message.Split('\n')[0]);
            return 1;
        }
    }
    case "hash-password":
    {
        // creates an ASP.NET Identity v3 hash of $PROBE_PASSWORD for a THROWAWAY scratch-database test user (password comes from the environment, never argv)
        var pw = Environment.GetEnvironmentVariable("PROBE_PASSWORD");
        if (string.IsNullOrEmpty(pw)) { Console.Error.WriteLine("PROBE_PASSWORD is not set"); return 2; }
        Console.WriteLine(new Microsoft.AspNetCore.Identity.PasswordHasher<object>().HashPassword(new object(), pw));
        return 0;
    }
    default:
        Console.Error.WriteLine("unknown command"); return 2;
}

static string Describe(Exception ex)
{
    var parts = new List<string>();
    for (var e = ex; e != null; e = e.InnerException) parts.Add(e.GetType().Name + ": " + e.Message.Split('\n')[0].Trim());
    return string.Join("  <-  ", parts);
}
