using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace CXFiles.UI.Components;

public class StatusLine
{
    private readonly StatusBarControl _bar;
    public StatusBarControl Control => _bar;

    public StatusLine()
    {
        _bar = new StatusBarControl(stickyBottom: true)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Margin(1, 0, 1, 0),
            BackgroundColor = Color.Transparent,
            SeparatorChar = "\u2022"
        };
    }

    public void Update(int itemCount, int selectedCount, string currentPath)
    {
        _bar.ClearAll();
        _bar.AddLeftText($"[dim]{itemCount} items[/]");
        if (selectedCount > 0)
            _bar.AddLeftText($"[cyan]{selectedCount} selected[/]");
        _bar.AddRightText($"[dim]{currentPath}[/]");
    }
}
