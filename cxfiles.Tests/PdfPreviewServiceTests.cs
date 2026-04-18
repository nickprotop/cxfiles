using CXFiles.Services;
using UglyToad.PdfPig.Writer;

namespace CXFiles.Tests;

public class PdfPreviewServiceTests
{
    private readonly IPdfPreviewService _svc = new PdfPreviewService();

    private static string CreatePdfFixture(string text)
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
        var font = builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);
        page.AddText(text, 12, new UglyToad.PdfPig.Core.PdfPoint(50, 750), font);
        var bytes = builder.Build();
        var path = Path.Combine(Path.GetTempPath(), $"cxfiles-pdf-{Guid.NewGuid()}.pdf");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static string CreateMultiPagePdfFixture(int pageCount)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);
        for (int i = 1; i <= pageCount; i++)
        {
            var page = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
            page.AddText($"line {i}", 12, new UglyToad.PdfPig.Core.PdfPoint(50, 750), font);
        }
        var bytes = builder.Build();
        var path = Path.Combine(Path.GetTempPath(), $"cxfiles-pdf-{Guid.NewGuid()}.pdf");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public void ExtractText_SmallPdf_ReturnsText()
    {
        var path = CreatePdfFixture("Hello World");
        try
        {
            var result = _svc.ExtractText(path, maxLines: 10);
            Assert.Null(result.Error);
            Assert.Equal(1, result.PageCount);
            Assert.Contains("Hello", result.Text);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ExtractText_NonPdf_ReturnsError()
    {
        var path = Path.Combine(Path.GetTempPath(), "not-a-pdf-" + Guid.NewGuid() + ".txt");
        File.WriteAllText(path, "plain text");
        try
        {
            var result = _svc.ExtractText(path, 10);
            Assert.NotNull(result.Error);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ExtractText_TruncatesAtMaxLines()
    {
        // Use 100 pages (one line per page) so truncation is deterministic regardless
        // of how PdfPig handles in-page newlines.
        var path = CreateMultiPagePdfFixture(pageCount: 100);
        try
        {
            var result = _svc.ExtractText(path, maxLines: 5);
            var lines = result.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.True(lines.Length <= 5);
            Assert.True(result.Truncated);
        }
        finally { File.Delete(path); }
    }
}
