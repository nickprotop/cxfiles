using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using CXFiles.Services;
using Color = SharpConsoleUI.Color;
using Rectangle = System.Drawing.Rectangle;

namespace CXFiles.UI;

internal class ClipboardPortal : PortalContentContainer
{
    private readonly ClipboardState _clipboard;
    private readonly ScrollablePanelControl _scrollPanel;

    private static readonly Color PanelBg = Color.Grey11;
    private static readonly Color PanelFg = Color.Grey93;

    public event EventHandler? Dismissed;
    public event EventHandler? ClearRequested;
    public event Action<string>? RemoveRequested;

    public ClipboardPortal(ClipboardState clipboard, int anchorX, int anchorY,
        int windowWidth, int windowHeight)
    {
        _clipboard = clipboard;

        BackgroundColor = PanelBg;
        ForegroundColor = PanelFg;
        DismissOnOutsideClick = true;
        BorderStyle = BoxChars.Rounded;
        BorderColor = Color.Grey50;
        BorderBackgroundColor = PanelBg;

        var actionLabel = clipboard.Action == ClipboardAction.Cut ? "Cut" : "Copied";
        var header = new MarkupControl(new List<string>
        {
            $"[bold]Clipboard[/] [dim]({clipboard.Paths.Count} {actionLabel.ToLower()})[/]"
        });
        header.HorizontalAlignment = HorizontalAlignment.Stretch;
        header.Margin = new Margin(1, 0, 1, 0);

        var headerRule = Controls.Rule();
        headerRule.Color = Color.Grey27;

        _scrollPanel = Controls.ScrollablePanel()
            .WithVerticalScroll()
            .WithMouseWheel()
            .WithBackgroundColor(PanelBg)
            .Build();
        _scrollPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
        _scrollPanel.VerticalAlignment = VerticalAlignment.Fill;
        _scrollPanel.ShowScrollbar = true;

        AddChild(header);
        AddChild(headerRule);
        AddChild(_scrollPanel);

        BuildItems();

        var footer = new StatusBarControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            BackgroundColor = PanelBg,
            Margin = new Margin(1, 0, 1, 0)
        };
        footer.AddRightText("[dim]Clear All[/]", () => ClearRequested?.Invoke(this, EventArgs.Empty));
        AddChild(footer);

        int popupW = 50;
        int popupH = Math.Max(6, Math.Min(clipboard.Paths.Count + 5, 18));

        var pos = PortalPositioner.CalculateFromPoint(
            new Point(anchorX, anchorY),
            new System.Drawing.Size(popupW, popupH),
            new Rectangle(1, 1, windowWidth - 2, windowHeight - 2),
            PortalPlacement.AboveOrBelow,
            new System.Drawing.Size(16, 3));
        PortalBounds = pos.Bounds;
    }

    private void BuildItems()
    {
        _scrollPanel.ClearContents();

        foreach (var path in _clipboard.Paths)
        {
            var name = Path.GetFileName(path);
            var dir = Path.GetDirectoryName(path) ?? "";
            var escapedName = SharpConsoleUI.Parsing.MarkupParser.Escape(name);
            var escapedDir = SharpConsoleUI.Parsing.MarkupParser.Escape(
                dir.Length > 30 ? "..." + dir[^27..] : dir);

            var bar = new StatusBarControl
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                BackgroundColor = PanelBg,
                Margin = new Margin(1, 0, 1, 0)
            };
            bar.AddLeftText($"[white]{escapedName}[/] [dim]{escapedDir}[/]");

            var p = path;
            bar.AddRightText("[dim]✕[/]", () => RemoveRequested?.Invoke(p));

            _scrollPanel.AddControl(bar);
        }
    }

    public new bool ProcessKey(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Escape)
        {
            Dismissed?.Invoke(this, EventArgs.Empty);
            return true;
        }
        return true;
    }

    public override bool ProcessMouseEvent(MouseEventArgs args)
    {
        return base.ProcessMouseEvent(args);
    }
}
