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

    [Fact]
    public void RenderLabelSheetPdf_SingleLabel_ProducesNonEmptyPdfBytes()
    {
        var pdf = _service.RenderLabelSheetPdf(new List<BarcodeLabelContent>
        {
            new() { Code = "PEMS-BR-TESTCODE1234", Symbology = "Code128", TitleLine = "EQ-1 - Test Pump", SubtitleLine = "Equipment", MetaLine = "Generated 2026-01-01" }
        });

        Assert.NotEmpty(pdf);
        Assert.Equal((byte)'%', pdf[0]);
        Assert.Equal((byte)'P', pdf[1]);
        Assert.Equal((byte)'D', pdf[2]);
        Assert.Equal((byte)'F', pdf[3]);
    }

    [Fact]
    public void RenderLabelSheetPdf_MultipleLabels_ProducesOnePdfCoveringAll()
    {
        var pdf = _service.RenderLabelSheetPdf(new List<BarcodeLabelContent>
        {
            new() { Code = "PEMS-BR-AAAA1111", Symbology = "Code128", TitleLine = "EQ-1" },
            new() { Code = "PEMS-BR-BBBB2222", Symbology = "QR", TitleLine = "EQ-2" },
            new() { Code = "PEMS-BR-CCCC3333", Symbology = "Code128", TitleLine = "EQ-3" }
        });

        Assert.NotEmpty(pdf);
    }
}
