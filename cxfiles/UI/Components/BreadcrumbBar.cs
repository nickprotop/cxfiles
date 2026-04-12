using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace CXFiles.UI.Components;

public class BreadcrumbBar
{
    private readonly StatusBarControl _left;
    private readonly MarkupControl _right;
    private readonly HorizontalGridControl _container;

    public HorizontalGridControl Control => _container;

    public event Action<string>? SegmentClicked;

    public BreadcrumbBar()
    {
        _left = Controls.StatusBar()
            .AddLeftText("[cyan1]◈ cxfiles[/]")
            .Build();
        _left.SeparatorChar = "\u203a";
        _left.BackgroundColor = Color.Transparent;
        _left.HorizontalAlignment = HorizontalAlignment.Left;
        _left.Margin = new Margin(1, 0, 0, 0);

        _right = Controls.Markup("[dim]...[/]").Build();
        _right.HorizontalAlignment = HorizontalAlignment.Right;
        _right.Margin = new Margin(0, 0, 1, 0);

        _container = Controls.HorizontalGrid()
            .StickyTop()
            .WithAlignment(HorizontalAlignment.Stretch)
            .Column(col => col.Add(_left))
            .Column(col => col.Add(_right))
            .Build();
        _container.BackgroundColor = new Color(20, 28, 45);
        _container.ForegroundColor = Color.Grey93;
    }

    public void Update(string path)
    {
        // Left: breadcrumb
        _left.ClearAll();

        var root = Path.GetPathRoot(path) ?? "/";
        _left.AddLeftText("[cyan1]◈ cxfiles[/]", () => SegmentClicked?.Invoke(root));

        var parts = path.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        var accumulated = root;

        for (int i = 0; i < parts.Length; i++)
        {
            accumulated = Path.Combine(accumulated, parts[i]);
            var clickPath = accumulated;

            _left.AddLeftSeparator();

            if (i == parts.Length - 1)
                _left.AddLeftText($"[bold]{parts[i]}[/]");
            else
                _left.AddLeftText(parts[i], () => SegmentClicked?.Invoke(clickPath));
        }

        // Right: item count
        try
        {
            var dirInfo = new DirectoryInfo(path);
            var itemCount = dirInfo.Exists
                ? dirInfo.EnumerateFileSystemInfos().Count()
                : 0;
            _right.SetContent(new List<string> { $"[dim]{itemCount} items[/]" });
        }
        catch
        {
            _right.SetContent(new List<string> { "" });
        }
    }
}
