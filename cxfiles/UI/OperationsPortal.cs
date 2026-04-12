using System.Drawing;
using SharpConsoleUI;
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
    private readonly MarkupControl _content;

    private static readonly Color PanelBg = Color.Grey11;
    private static readonly Color PanelFg = Color.Grey93;

    public event EventHandler? Dismissed;

    public OperationsPortal(OperationManager operations, int anchorX, int anchorY,
        int windowWidth, int windowHeight)
    {
        _operations = operations;

        BackgroundColor = PanelBg;
        ForegroundColor = PanelFg;
        DismissOnOutsideClick = true;
        BorderStyle = BoxChars.Rounded;
        BorderColor = Color.Grey50;
        BorderBackgroundColor = PanelBg;

        _content = new MarkupControl(new List<string>());
        _content.HorizontalAlignment = HorizontalAlignment.Stretch;

        AddChild(_content);
        UpdateContent();

        _operations.OperationsChanged += OnOperationsChanged;

        int popupW = 45;
        int popupH = Math.Max(5, Math.Min(operations.TotalCount * 2 + 4, 18));

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
        UpdateContent();
        Container?.Invalidate(true);
    }

    private void UpdateContent()
    {
        var ops = _operations.Operations;
        var running = _operations.RunningOperations;
        var lines = new List<string>();

        lines.Add("[bold]Operations[/]");
        lines.Add("");

        if (ops.Count == 0)
        {
            lines.Add("[dim]No recent operations[/]");
        }
        else
        {
            int runningIdx = 0;
            foreach (var op in ops)
            {
                var icon = op.Status switch
                {
                    OperationStatus.Running => "[yellow]⟳[/]",
                    OperationStatus.Completed => "[green]✓[/]",
                    OperationStatus.Failed => "[red]✗[/]",
                    OperationStatus.Cancelled => "[dim]○[/]",
                    _ => " "
                };

                var desc = SharpConsoleUI.Parsing.MarkupParser.Escape(op.Description);
                if (desc.Length > 28) desc = desc[..25] + "...";

                if (op.Status == OperationStatus.Running)
                {
                    runningIdx++;
                    var bar = BuildProgressBar(op.ProgressPercent, 10);
                    var cancelHint = runningIdx <= 9 ? $" [dim]^{runningIdx}:Cancel[/]" : "";
                    lines.Add($" {icon} {desc}{cancelHint}");
                    lines.Add($"   {bar} {op.StatusText}");
                }
                else
                {
                    var elapsed = op.EndTime.HasValue
                        ? $" ({(op.EndTime.Value - op.StartTime).TotalSeconds:F0}s)"
                        : "";
                    lines.Add($" {icon} {desc} [dim]{op.StatusText}{elapsed}[/]");

                    if (op.Status == OperationStatus.Failed && op.ErrorMessage != null)
                    {
                        var err = op.ErrorMessage.Length > 33
                            ? op.ErrorMessage[..30] + "..."
                            : op.ErrorMessage;
                        lines.Add($"   [red dim]{SharpConsoleUI.Parsing.MarkupParser.Escape(err)}[/]");
                    }
                }
            }
        }

        lines.Add("");
        if (running.Count > 0)
            lines.Add("[dim]^1-^9: Cancel operation  Esc: Close[/]");
        else
            lines.Add("[dim]Esc: Close[/]");

        _content.SetContent(lines);
    }

    private static string BuildProgressBar(int percent, int width)
    {
        int filled = (int)(percent / 100.0 * width);
        filled = Math.Clamp(filled, 0, width);
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

        // Ctrl+1 through Ctrl+9 cancel running operations
        if (key.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            int idx = key.Key switch
            {
                ConsoleKey.D1 => 0,
                ConsoleKey.D2 => 1,
                ConsoleKey.D3 => 2,
                ConsoleKey.D4 => 3,
                ConsoleKey.D5 => 4,
                ConsoleKey.D6 => 5,
                ConsoleKey.D7 => 6,
                ConsoleKey.D8 => 7,
                ConsoleKey.D9 => 8,
                _ => -1
            };
            if (idx >= 0)
            {
                _operations.CancelByIndex(idx);
                return true;
            }
        }

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
