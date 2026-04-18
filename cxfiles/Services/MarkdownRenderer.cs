using System.Text;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace CXFiles.Services;

public interface IMarkdownRenderer
{
    IEnumerable<string> RenderForConsole(string markdown, int maxLines);
}

public sealed class MarkdownRenderer : IMarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    public IEnumerable<string> RenderForConsole(string markdown, int maxLines)
    {
        var lines = new List<string>();
        if (string.IsNullOrEmpty(markdown)) return lines;

        MarkdownDocument doc;
        try { doc = Markdown.Parse(markdown, Pipeline); }
        catch { return lines; }

        foreach (var block in doc)
        {
            if (lines.Count >= maxLines) break;
            RenderBlock(block, lines, maxLines);
        }

        if (lines.Count > maxLines) lines.RemoveRange(maxLines, lines.Count - maxLines);
        return lines;
    }

    private static void RenderBlock(Block block, List<string> lines, int maxLines)
    {
        switch (block)
        {
            case HeadingBlock h:
                var prefix = new string('#', Math.Min(h.Level, 6));
                lines.Add($"[bold]{prefix} {Escape(InlineText(h.Inline))}[/]");
                break;

            case ParagraphBlock p:
                lines.Add(RenderInlines(p.Inline));
                break;

            case ListBlock list:
                int index = 1;
                foreach (var item in list)
                {
                    if (item is not ListItemBlock li) continue;
                    var marker = list.IsOrdered ? $"{index}. " : "• ";
                    foreach (var child in li)
                    {
                        if (child is ParagraphBlock childPara)
                            lines.Add(marker + RenderInlines(childPara.Inline));
                        else
                            RenderBlock(child, lines, maxLines);
                    }
                    index++;
                    if (lines.Count >= maxLines) return;
                }
                break;

            case FencedCodeBlock fcb:
                foreach (var line in fcb.Lines.Lines)
                {
                    var s = line.ToString();
                    if (s == null) continue;
                    lines.Add("│ " + Escape(s));
                    if (lines.Count >= maxLines) return;
                }
                break;

            case CodeBlock cb:
                foreach (var line in cb.Lines.Lines)
                {
                    var s = line.ToString();
                    if (s == null) continue;
                    lines.Add("│ " + Escape(s));
                    if (lines.Count >= maxLines) return;
                }
                break;

            case QuoteBlock qb:
                foreach (var child in qb)
                {
                    if (child is ParagraphBlock qp)
                        lines.Add("> " + RenderInlines(qp.Inline));
                }
                break;

            case ThematicBreakBlock:
                lines.Add("[dim]────────────────[/]");
                break;

            default:
                if (block is LeafBlock lb && lb.Inline != null)
                    lines.Add(RenderInlines(lb.Inline));
                break;
        }
    }

    private static string RenderInlines(ContainerInline? inlines)
    {
        if (inlines == null) return string.Empty;
        var sb = new StringBuilder();
        foreach (var inline in inlines)
            AppendInline(inline, sb);
        return sb.ToString();
    }

    private static void AppendInline(Inline inline, StringBuilder sb)
    {
        switch (inline)
        {
            case LiteralInline lit:
                sb.Append(Escape(lit.Content.ToString()));
                break;
            case EmphasisInline em when em.DelimiterCount >= 2:
                sb.Append("[bold]");
                foreach (var child in em) AppendInline(child, sb);
                sb.Append("[/]");
                break;
            case EmphasisInline em:
                sb.Append("[italic]");
                foreach (var child in em) AppendInline(child, sb);
                sb.Append("[/]");
                break;
            case CodeInline code:
                sb.Append("[dim]");
                sb.Append(Escape(code.Content));
                sb.Append("[/]");
                break;
            case LinkInline link when link.IsImage:
                var alt = InlineText(link);
                sb.Append($"[image: {Escape(alt)}]");
                break;
            case LinkInline link:
                foreach (var child in link) AppendInline(child, sb);
                break;
            case LineBreakInline:
                sb.Append(' ');
                break;
            case ContainerInline container:
                foreach (var child in container) AppendInline(child, sb);
                break;
            default:
                break;
        }
    }

    private static string InlineText(ContainerInline? inlines)
    {
        if (inlines == null) return string.Empty;
        var sb = new StringBuilder();
        foreach (var inline in inlines)
        {
            if (inline is LiteralInline lit) sb.Append(lit.Content.ToString());
            else if (inline is ContainerInline ci) sb.Append(InlineText(ci));
        }
        return sb.ToString();
    }

    private static string Escape(string s) => SharpConsoleUI.Parsing.MarkupParser.Escape(s);
}
