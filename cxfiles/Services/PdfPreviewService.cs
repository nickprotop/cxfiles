using System.Text;
using UglyToad.PdfPig;

namespace CXFiles.Services;

public sealed record PdfPreviewResult(int PageCount, string Text, bool Truncated, string? Error);

public interface IPdfPreviewService
{
    PdfPreviewResult ExtractText(string path, int maxLines);
}

public sealed class PdfPreviewService : IPdfPreviewService
{
    public PdfPreviewResult ExtractText(string path, int maxLines)
    {
        try
        {
            using var doc = PdfDocument.Open(path);
            var pageCount = doc.NumberOfPages;
            var sb = new StringBuilder();
            int emittedLines = 0;
            bool truncated = false;

            for (int i = 1; i <= pageCount && !truncated; i++)
            {
                var page = doc.GetPage(i);
                var pageText = page.Text ?? string.Empty;
                foreach (var rawLine in pageText.Split('\n'))
                {
                    var line = rawLine.TrimEnd();
                    if (line.Length == 0) continue;
                    if (emittedLines >= maxLines) { truncated = true; break; }
                    sb.AppendLine(line);
                    emittedLines++;
                }
            }

            return new PdfPreviewResult(pageCount, sb.ToString(), truncated, null);
        }
        catch (Exception ex)
        {
            return new PdfPreviewResult(0, string.Empty, false, ex.Message);
        }
    }
}
