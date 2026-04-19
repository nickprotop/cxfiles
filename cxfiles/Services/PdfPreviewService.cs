using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

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
                foreach (var line in GroupWordsIntoLines(page.GetWords()))
                {
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

    // PDFs store positioned glyphs, not line breaks. Reconstruct lines by clustering
    // words whose baselines share a Y within half their height, emitted top-to-bottom
    // (PDF Y grows upward, so higher Y = higher on page).
    private static IEnumerable<string> GroupWordsIntoLines(IEnumerable<Word> words)
    {
        var sorted = words
            .Where(w => !string.IsNullOrWhiteSpace(w.Text))
            .OrderByDescending(w => w.BoundingBox.Bottom)
            .ToList();

        var lines = new List<List<Word>>();
        foreach (var w in sorted)
        {
            if (lines.Count > 0)
            {
                var last = lines[^1];
                var refY = last[0].BoundingBox.Bottom;
                var tol = Math.Max(last[0].BoundingBox.Height * 0.5, 1.0);
                if (Math.Abs(refY - w.BoundingBox.Bottom) <= tol)
                {
                    last.Add(w);
                    continue;
                }
            }
            lines.Add(new List<Word> { w });
        }

        foreach (var line in lines)
        {
            yield return string.Join(' ',
                line.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text));
        }
    }
}
