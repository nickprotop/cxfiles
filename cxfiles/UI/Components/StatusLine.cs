using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace CXFiles.UI.Components;

public class StatusLine
{
    private readonly StatusBarControl _bar;
    public StatusBarControl Control => _bar;

    public event Action? OptionsClicked;
    public event Action? DetailToggled;
    public event Action? HiddenToggled;

    public StatusLine()
    {
        _bar = new StatusBarControl(stickyBottom: true)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Margin(1, 0, 1, 0),
            BackgroundColor = Color.Transparent,
            SeparatorChar = "\u2022",
            ShortcutLabelSeparator = ":"
        };
    }

    public void Update(int itemCount, int selectedCount, bool detailVisible, bool showHidden)
    {
        _bar.BatchUpdate(() =>
        {
            _bar.ClearAll();

            // Left: contextual info
            _bar.AddLeftText($"[dim]{itemCount} items[/]");
            if (selectedCount > 0)
            {
                _bar.AddLeftSeparator();
                _bar.AddLeftText($"[cyan]{selectedCount} selected[/]");
            }

            // Right: toggle buttons + options
            var detailColor = detailVisible ? "cyan" : "grey50";
            _bar.AddRightText($"[{detailColor}]Detail[/] [dim]F3[/]",
                () => DetailToggled?.Invoke());

            _bar.AddRightSeparator();

            var hiddenColor = showHidden ? "cyan" : "grey50";
            _bar.AddRightText($"[{hiddenColor}]Hidden[/] [dim]^H[/]",
                () => HiddenToggled?.Invoke());

            _bar.AddRightSeparator();

            _bar.AddRightText("[grey70]Options[/] [dim]^O[/]",
                () => OptionsClicked?.Invoke());
        });
    }
}
