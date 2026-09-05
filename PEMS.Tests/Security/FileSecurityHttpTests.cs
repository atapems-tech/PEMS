using Microsoft.AspNetCore.Mvc.Testing;

namespace PEMS.Tests.Security;

/// <summary>
/// طبق ممیزی امنیتی (P0 - Engineering File Security Containment): دو سناریویی که فقط با یک
/// Pipeline واقعی HTTP قابل اثبات‌اند (نه با فراخوانی مستقیم Controller، چون فیلتر سراسری
/// RequireAuthenticatedUser و Middleware مسدودکننده‌ی /uploads هر دو بخشی از خودِ Pipeline
/// هستند، نه منطق داخل یک اکشن): درخواست کاملاً ناشناس، و دسترسی مستقیم به مسیر استاتیک قدیمی.
/// اولین تست‌های سطح Controller/HTTP در کل این پروژه - قبلاً چنین زیرساختی وجود نداشت.
/// </summary>
public class FileSecurityHttpTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public FileSecurityHttpTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("https_port", "5443");
        });
    }

    // ==================== 1. Anonymous download → denied ====================
    [Fact]
    public async Task AnonymousRequest_ToFilesDownload_IsDeniedNotServedDirectly()
    {
        // BaseAddress باید https باشد - در غیر این صورت UseHttpsRedirection (که قبل از هر
        // Middleware امنیتی دیگری در Program.cs اجرا می‌شود) خودش یک ۳۰۷ برمی‌گرداند و تست
        // هرگز واقعاً به Middleware/Controller مورد آزمایش نمی‌رسد
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var response = await client.GetAsync("/Files/Download/1");

        // طبق فیلتر سراسری RequireAuthenticatedUser (Program.cs) - کاربر ناشناس هرگز نباید
        // مستقیم به بایت‌های فایل برسد؛ یا ۳۰۲ به صفحه‌ی ورود یا ۴۰۱/۴۰۳ - هرگز ۲۰۰
        Assert.NotEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            response.StatusCode == System.Net.HttpStatusCode.Redirect ||
            response.StatusCode == System.Net.HttpStatusCode.Found ||
            response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
            response.StatusCode == System.Net.HttpStatusCode.Forbidden,
            $"Expected a denial status for anonymous access, got {response.StatusCode}");
    }

    // ==================== 9. Direct /uploads URL → unavailable ====================
    [Theory]
    [InlineData("/uploads/documents/anything.pdf")]
    [InlineData("/uploads/engineering-comments/comments/anything.pdf")]
    [InlineData("/uploads/vdr/comments/anything.pdf")]
    [InlineData("/uploads/moc/anything.pdf")]
    [InlineData("/uploads/technical-queries/anything.pdf")]
    [InlineData("/uploads/letters/anything.pdf")]
    [InlineData("/uploads/mrs-import/anything.xlsx")]
    [InlineData("/uploads/intake/anything.pdf")]
    public async Task DirectUploadsUrl_ForMigratedBusinessFolders_ReturnsNotFound_EvenAnonymously(string path)
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var response = await client.GetAsync(path);

        // طبق Middleware اضافه‌شده در Program.cs (قبل از UseStaticFiles) - این مسیرها باید همیشه
        // ۴۰۴ بدهند، حتی اگر (فرضاً) یک فایل واقعی هنوز آن‌جا فیزیکی باشد
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }
}
