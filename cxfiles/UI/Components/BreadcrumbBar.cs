using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace CXFiles.UI.Components;

public class BreadcrumbBar
{
    private readonly StatusBarControl _bar;
    public StatusBarControl Control => _bar;

    public event Action<string>? SegmentClicked;

    public BreadcrumbBar()
    {
        _bar = new StatusBarControl(stickyBottom: false)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Margin(1, 0, 1, 0),
            BackgroundColor = Color.Transparent,
            SeparatorChar = " \u25b8 "
        };
    }

    public void Update(string path)
    {
        _bar.ClearAll();
        _bar.AddLeftText("[bold cyan]◈ cxfiles[/]", onClick: () => SegmentClicked?.Invoke("/"));

        var parts = path.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        var accumulated = Path.GetPathRoot(path) ?? "/";

        for (int i = 0; i < parts.Length; i++)
        {
            var segment = parts[i];
            var fullPath = accumulated;
            if (i == parts.Length - 1)
            {
                _bar.AddLeftText($"[bold]{segment}[/]");
            }
            else
            {
                _bar.AddLeftText(segment, onClick: () => SegmentClicked?.Invoke(fullPath));
            }
            accumulated = Path.Combine(accumulated, segment);
        }
    }
}
