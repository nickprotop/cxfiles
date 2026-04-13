using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace CXFiles.UI.Components;

public class BreadcrumbBar
{
    private readonly StatusBarControl _left;
    private readonly StatusBarControl _right;
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

        _right = Controls.StatusBar().Build();
        _right.SeparatorChar = "\u2022";
        _right.BackgroundColor = Color.Transparent;
        _right.HorizontalAlignment = HorizontalAlignment.Right;
        _right.Margin = new Margin(0, 0, 1, 0);

        var locations = new (string Label, string Path)[]
        {
            ("Home", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
            ("Desktop", Environment.GetFolderPath(Environment.SpecialFolder.Desktop)),
            ("Docs", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),
            ("Downloads", Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")),
        };

        bool first = true;
        foreach (var (label, locPath) in locations)
        {
            if (string.IsNullOrEmpty(locPath) || !Directory.Exists(locPath)) continue;
            if (!first) _right.AddRightSeparator();
            first = false;
            var p = locPath;
            _right.AddRightText($"[grey70]{label}[/]", () => NavigateTo(p));
        }

        _container = Controls.HorizontalGrid()
            .StickyTop()
            .WithAlignment(HorizontalAlignment.Stretch)
            .Column(col => col.Flex(1).Add(_left))
            .Column(col => col.Add(_right))
            .Build();
        _container.BackgroundColor = Color.Grey15;
        _container.ForegroundColor = Color.Grey93;
    }

    private void NavigateTo(string path)
    {
        if (Directory.Exists(path))
            SegmentClicked?.Invoke(path);
    }

    public void Update(string path)
    {
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
    }
}
