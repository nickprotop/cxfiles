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

internal class OperationsPortal : PortalContentContainer
{
    private readonly OperationManager _operations;
    private readonly ConsoleWindowSystem _ws;
    private readonly ScrollablePanelControl _scrollPanel;
    private readonly MarkupControl _headerLabel;
    private readonly MarkupControl _footerLabel;

    private static readonly Color PanelBg = Color.Grey11;
    private static readonly Color PanelFg = Color.Grey93;
    private static readonly Color RowBg = new(30, 35, 50);
    private static readonly Color ProgressFilled = new(0, 180, 220);
    private static readonly Color ProgressEmpty = Color.Grey23;

    public event EventHandler? Dismissed;

    public OperationsPortal(ConsoleWindowSystem ws, OperationManager operations,
        int anchorX, int anchorY, int windowWidth, int windowHeight)
    {
        _ws = ws;
        _operations = operations;

        BackgroundColor = PanelBg;
        ForegroundColor = PanelFg;
        DismissOnOutsideClick = true;
        BorderStyle = BoxChars.Rounded;
        BorderColor = Color.Grey50;
        BorderBackgroundColor = PanelBg;

        _headerLabel = new MarkupControl(new List<string> { "[bold]Operations[/]" });
        _headerLabel.HorizontalAlignment = HorizontalAlignment.Stretch;
        _headerLabel.Margin = new Margin(1, 0, 1, 0);

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
        _scrollPanel.ScrollbarPosition = ScrollbarPosition.Right;

        _footerLabel = new MarkupControl(new List<string> { "[dim]Esc: Close[/]" });
        _footerLabel.HorizontalAlignment = HorizontalAlignment.Stretch;
        _footerLabel.Margin = new Margin(1, 0, 1, 0);

        AddChild(_headerLabel);
        AddChild(headerRule);
        AddChild(_scrollPanel);
        AddChild(_footerLabel);

        RebuildOperationRows();

        _operations.OperationsChanged += OnOperationsChanged;

        int popupW = 50;
        int popupH = Math.Max(8, Math.Min(operations.TotalCount * 4 + 6, 22));

        var pos = PortalPositioner.CalculateFromPoint(
            new Point(anchorX, anchorY),
            new System.Drawing.Size(popupW, popupH),
            new Rectangle(1, 1, windowWidth - 2, windowHeight - 2),
            PortalPlacement.AboveOrBelow,
            new System.Drawing.Size(16, 3));
        PortalBounds = pos.Bounds;
    }

    private void OnOperationsChanged()
    {
        _ws.EnqueueOnUIThread(() =>
        {
            RebuildOperationRows();
            Container?.Invalidate(true);
        });
    }

    private void RebuildOperationRows()
    {
        _scrollPanel.ClearContents();
        var ops = _operations.Operations;
        var running = _operations.RunningOperations;

        if (ops.Count == 0)
        {
            var empty = new MarkupControl(new List<string> { "[dim]No recent operations[/]" });
            empty.Margin = new Margin(1, 1, 1, 0);
            _scrollPanel.AddControl(empty);
        }
        else
        {
            foreach (var op in ops)
                _scrollPanel.AddControl(BuildOperationRow(op));
        }

        var footerLines = new List<string>();
        if (running.Count > 0)
            footerLines.Add("[dim]Esc: Close[/]");
        else
            footerLines.Add("[dim]Esc: Close[/]");
        _footerLabel.SetContent(footerLines);
    }

    private IWindowControl BuildOperationRow(FileOperation op)
    {
        var panel = Controls.ScrollablePanel()
            .WithBackgroundColor(RowBg)
            .Build();
        panel.HorizontalAlignment = HorizontalAlignment.Stretch;
        panel.Padding = new Padding(1, 0, 1, 0);
        panel.Margin = new Margin(1, 0, 1, 0);
        panel.ShowScrollbar = false;
        panel.BorderStyle = SharpConsoleUI.BorderStyle.None;

        var icon = op.Status switch
        {
            OperationStatus.Running => "[yellow]⟳[/]",
            OperationStatus.Completed => "[green]✓[/]",
            OperationStatus.Failed => "[red]✗[/]",
            OperationStatus.Cancelled => "[dim]○[/]",
            _ => " "
        };

        var desc = SharpConsoleUI.Parsing.MarkupParser.Escape(op.Description);

        // Line 1: icon + description
        var titleLine = new MarkupControl(new List<string> { $" {icon} {desc}" });
        titleLine.HorizontalAlignment = HorizontalAlignment.Stretch;
        panel.AddControl(titleLine);

        // Line 2: current file / error / status detail
        if (op.Status == OperationStatus.Running)
        {
            var fileText = op.CurrentFile != null
                ? SharpConsoleUI.Parsing.MarkupParser.Escape(
                    op.CurrentFile.Length > 38 ? op.CurrentFile[..35] + "..." : op.CurrentFile)
                : "preparing...";
            var fileLine = new MarkupControl(new List<string> { $"   [dim]{fileText}[/]" });
            fileLine.HorizontalAlignment = HorizontalAlignment.Stretch;
            panel.AddControl(fileLine);
        }
        else if (op.Status == OperationStatus.Failed && op.ErrorMessage != null)
        {
            var err = SharpConsoleUI.Parsing.MarkupParser.Escape(
                op.ErrorMessage.Length > 38 ? op.ErrorMessage[..35] + "..." : op.ErrorMessage);
            var errLine = new MarkupControl(new List<string> { $"   [red dim]{err}[/]" });
            errLine.HorizontalAlignment = HorizontalAlignment.Stretch;
            panel.AddControl(errLine);
        }
        else
        {
            var elapsed = $"{op.Elapsed.TotalSeconds:F0}s";
            var statusText = op.Status switch
            {
                OperationStatus.Completed => $"[green]Completed[/] [dim]in {elapsed}[/]",
                OperationStatus.Cancelled => $"[dim]Cancelled after {elapsed}[/]",
                _ => ""
            };
            var detailLine = new MarkupControl(new List<string> { $"   {statusText}" });
            detailLine.HorizontalAlignment = HorizontalAlignment.Stretch;
            panel.AddControl(detailLine);
        }

        // Line 3: status bar with progress + action
        var bar = new StatusBarControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            BackgroundColor = RowBg
        };

        if (op.Status == OperationStatus.Running)
        {
            if (op.BytesTotal > 0)
            {
                var progressText = BuildProgressBar(op.ProgressPercent, 16);
                bar.AddLeftText($"{progressText} [dim]{op.ProgressPercent}%[/]");
            }
            else
            {
                bar.AddLeftText("[dim]working…[/]");
            }
            bar.AddRightText("[red]Cancel[/]", () => _operations.CancelOperation(op));
        }
        else
        {
            bar.AddRightText("[dim]Dismiss[/]", () => _operations.RemoveOperation(op));
        }

        panel.AddControl(bar);

        return panel;
    }

    private static string BuildProgressBar(int percent, int width)
    {
        int filled = Math.Clamp((int)(percent / 100.0 * width), 0, width);
        var filledStr = new string('━', filled);
        var emptyStr = new string('─', width - filled);
        return $"[cyan]{filledStr}[/][dim]{emptyStr}[/]";
    }

    public new bool ProcessKey(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Escape)
        {
            Cleanup();
            Dismissed?.Invoke(this, EventArgs.Empty);
            return true;
        }

        // Tab cycles focus among interactive children in the portal
        if (key.Key == ConsoleKey.Tab)
        {
            base.ProcessKey(key);
            return true;
        }

        // Delegate to focused child (cancel buttons)
        if (base.ProcessKey(key))
            return true;

        return true; // consume all keys while open
    }

    private void Cleanup()
    {
        _operations.OperationsChanged -= OnOperationsChanged;
    }

    public override bool ProcessMouseEvent(MouseEventArgs args)
    {
        return base.ProcessMouseEvent(args);
    }
}
