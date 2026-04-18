// cxfiles.Tests/MarkdownRendererTests.cs
using CXFiles.Services;

namespace CXFiles.Tests;

public class MarkdownRendererTests
{
    private readonly IMarkdownRenderer _r = new MarkdownRenderer();

    private List<string> Render(string md, int max = 50) => _r.RenderForConsole(md, max).ToList();

    [Fact]
    public void Heading_LevelOneRendersAsBold()
    {
        var lines = Render("# Hello");
        Assert.Contains(lines, l => l.Contains("[bold]") && l.Contains("Hello"));
    }

    [Fact]
    public void UnorderedList_RendersBulletGlyph()
    {
        var lines = Render("- one\n- two");
        Assert.Contains(lines, l => l.Contains("•") && l.Contains("one"));
        Assert.Contains(lines, l => l.Contains("•") && l.Contains("two"));
    }

    [Fact]
    public void OrderedList_KeepsNumbering()
    {
        var lines = Render("1. first\n2. second");
        Assert.Contains(lines, l => l.Contains("1.") && l.Contains("first"));
        Assert.Contains(lines, l => l.Contains("2.") && l.Contains("second"));
    }

    [Fact]
    public void Bold_MapsToBoldTag()
    {
        var lines = Render("**strong**");
        Assert.Contains(lines, l => l.Contains("[bold]") && l.Contains("strong"));
    }

    [Fact]
    public void InlineCode_MapsToDim()
    {
        var lines = Render("`code`");
        Assert.Contains(lines, l => l.Contains("[dim]") && l.Contains("code"));
    }

    [Fact]
    public void CodeBlock_PrefixedWithBar()
    {
        var lines = Render("```\nline1\nline2\n```");
        Assert.Contains(lines, l => l.StartsWith("│ ") && l.Contains("line1"));
        Assert.Contains(lines, l => l.StartsWith("│ ") && l.Contains("line2"));
    }

    [Fact]
    public void Link_DisplaysTextDropsUrl()
    {
        var lines = Render("[click](https://example.com)");
        Assert.DoesNotContain(lines, l => l.Contains("https://example.com"));
        Assert.Contains(lines, l => l.Contains("click"));
    }

    [Fact]
    public void Image_RendersAsPlaceholder()
    {
        var lines = Render("![alt text](img.png)");
        Assert.Contains(lines, l => l.Contains("[image: alt text]"));
    }

    [Fact]
    public void Blockquote_RendersWithMarker()
    {
        var lines = Render("> quoted");
        Assert.Contains(lines, l => l.StartsWith("> ") && l.Contains("quoted"));
    }

    [Fact]
    public void ObeysMaxLines()
    {
        var longMd = string.Join("\n", Enumerable.Range(1, 50).Select(i => $"line {i}"));
        var lines = Render(longMd, max: 10);
        Assert.True(lines.Count <= 10);
    }

    [Fact]
    public void Malformed_DoesNotThrow()
    {
        var ex = Record.Exception(() => _r.RenderForConsole("**unterminated [link](", 20).ToList());
        Assert.Null(ex);
    }
}
