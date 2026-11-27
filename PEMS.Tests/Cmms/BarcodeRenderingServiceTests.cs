using PEMS.Application.Interfaces;
using PEMS.Persistence.Services;

namespace PEMS.Tests.Cmms;

/// <summary>
/// طبق «PEMS — Enterprise Barcode System» بند «Rendering»: خروجی SVG/PNG/PDF محلی (بدون هیچ
/// سرویس بیرونی) باید واقعاً تولید شود و حاوی محتوای معتبر باشد.
/// </summary>
public class BarcodeRenderingServiceTests
{
    private readonly IBarcodeRenderingService _service = new BarcodeRenderingService();

    [Theory]
    [InlineData("Code128")]
    [InlineData("QR")]
    public void RenderSvg_ProducesNonEmptySvgMarkup(string symbology)
    {
        var svg = _service.RenderSvg("PEMS-BR-TESTCODE1234", symbology);

        Assert.False(string.IsNullOrWhiteSpace(svg));
        Assert.Contains("<svg", svg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RenderPng_ProducesNonEmptyPngBytes()
    {
        var png = _service.RenderPng("PEMS-BR-TESTCODE1234", "Code128");

        Assert.NotEmpty(png);
        // PNG magic number
        Assert.Equal(0x89, png[0]);
        Assert.Equal((byte)'P', png[1]);
        Assert.Equal((byte)'N', png[2]);
        Assert.Equal((byte)'G', png[3]);
    }
}
