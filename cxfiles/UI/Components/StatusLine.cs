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
    public event Action? OperationsClicked;
    public event Action? ClipboardClicked;
    public event Action? RefreshClicked;

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

    public void Update(int itemCount, int selectedCount, bool detailVisible, bool showHidden,
        int activeOps = 0, int totalOps = 0, int clipboardCount = 0, string? clipboardAction = null)
    {
        _bar.BatchUpdate(() =>
        {
            _bar.ClearAll();

            // Left: item count + selection
            _bar.AddLeftText($"[dim]{itemCount} items[/]");
            if (selectedCount > 0)
            {
                _bar.AddLeftSeparator();
                _bar.AddLeftText($"[cyan]{selectedCount} selected[/]");
            }

            // Left: clipboard indicator
            if (clipboardCount > 0)
            {
                _bar.AddLeftSeparator();
                var actionLabel = clipboardAction == "Cut" ? "cut" : "copied";
                _bar.AddLeftText($"[cyan]{clipboardCount} {actionLabel}[/] [grey50]^B[/]",
                    () => ClipboardClicked?.Invoke());
            }

            // Left: operations indicator
            if (activeOps > 0)
            {
                _bar.AddLeftSeparator();
                _bar.AddLeftText($"[yellow]⟳ {activeOps} running[/] [grey50]^P[/]",
                    () => OperationsClicked?.Invoke());
            }
            else if (totalOps > 0)
            {
                _bar.AddLeftSeparator();
                _bar.AddLeftText($"[green]✓ {totalOps} done[/] [grey50]^P[/]",
                    () => OperationsClicked?.Invoke());
            }

            // Right: actions + toggle buttons
            _bar.AddRightText("[grey70]Refresh[/] [grey50]F5[/]",
                () => RefreshClicked?.Invoke());

            _bar.AddRightSeparator();

            var detailColor = detailVisible ? "cyan1" : "grey70";
            _bar.AddRightText($"[{detailColor}]Detail[/] [grey50]F3[/]",
                () => DetailToggled?.Invoke());

            _bar.AddRightSeparator();

            var hiddenColor = showHidden ? "cyan1" : "grey70";
            _bar.AddRightText($"[{hiddenColor}]Hidden[/] [grey50]^H[/]",
                () => HiddenToggled?.Invoke());

            _bar.AddRightSeparator();

            _bar.AddRightText("[grey70]Options[/] [grey50]^O[/]",
                () => OptionsClicked?.Invoke());
        });
    }
}
