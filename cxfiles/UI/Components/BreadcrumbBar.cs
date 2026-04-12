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
            BackgroundColor = new Color(20, 28, 45),
            ForegroundColor = new Color(180, 190, 210),
            SeparatorChar = " \u25b8 ",
            StickyPosition = StickyPosition.Top
        };
    }

    public void Update(string path)
    {
        _bar.ClearAll();

        var root = Path.GetPathRoot(path) ?? "/";
        _bar.AddLeftText("[bold cyan]◈ cxfiles[/]", onClick: () => SegmentClicked?.Invoke(root));

        var parts = path.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        var accumulated = root;

        for (int i = 0; i < parts.Length; i++)
        {
            accumulated = Path.Combine(accumulated, parts[i]);
            var clickPath = accumulated; // capture for closure

            if (i == parts.Length - 1)
            {
                _bar.AddLeftText($"[bold]{parts[i]}[/]");
            }
            else
            {
                _bar.AddLeftText(parts[i], onClick: () => SegmentClicked?.Invoke(clickPath));
            }
        }
    }
}
