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
    public event EventHandler<string>? CancelRequested;

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

        // Subscribe to changes
        _operations.OperationsChanged += UpdateContent;

        // Calculate bounds
        int popupW = 45;
        int popupH = Math.Max(5, Math.Min(operations.TotalCount * 2 + 3, 15));

        var pos = PortalPositioner.CalculateFromPoint(
            new Point(anchorX, anchorY),
            new System.Drawing.Size(popupW, popupH),
            new Rectangle(1, 1, windowWidth - 2, windowHeight - 2),
            PortalPlacement.AboveOrBelow,
            new System.Drawing.Size(16, 3));
        PortalBounds = pos.Bounds;
    }

    private void UpdateContent()
    {
        var ops = _operations.Operations;
        var lines = new List<string>();

        lines.Add("[bold]Operations[/]");
        lines.Add("");

        if (ops.Count == 0)
        {
            lines.Add("[dim]No recent operations[/]");
        }
        else
        {
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
                if (desc.Length > 30) desc = desc[..27] + "...";

                if (op.Status == OperationStatus.Running)
                {
                    var bar = BuildProgressBar(op.ProgressPercent, 10);
                    lines.Add($" {icon} {desc}");
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
                        var err = op.ErrorMessage.Length > 35
                            ? op.ErrorMessage[..32] + "..."
                            : op.ErrorMessage;
                        lines.Add($"   [red dim]{SharpConsoleUI.Parsing.MarkupParser.Escape(err)}[/]");
                    }
                }
            }
        }

        lines.Add("");
        lines.Add("[dim]Esc: Close[/]");

        _content.SetContent(lines);
    }

    private static string BuildProgressBar(int percent, int width)
    {
        int filled = (int)(percent / 100.0 * width);
        var bar = new string('━', filled) + new string('─', width - filled);
        return $"[cyan]{bar[..filled]}[/][dim]{bar[filled..]}[/]";
    }

    public new bool ProcessKey(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Escape)
        {
            _operations.OperationsChanged -= UpdateContent;
            Dismissed?.Invoke(this, EventArgs.Empty);
            return true;
        }
        return true; // consume all keys while open
    }

    public override bool ProcessMouseEvent(MouseEventArgs args)
    {
        return base.ProcessMouseEvent(args);
    }
}
