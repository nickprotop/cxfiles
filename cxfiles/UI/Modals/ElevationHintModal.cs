using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace CXFiles.UI.Modals;

public class ElevationHintResult
{
    public bool OpenSettings { get; init; }
    public bool DontShowAgain { get; init; }
}

/// <summary>
/// Shown when a file operation fails with a permission error but sudo elevation
/// is disabled. Explains the situation and offers to open Settings, with an
/// option to suppress the hint in future.
/// </summary>
public class ElevationHintModal : ModalBase<ElevationHintResult>
{
    private readonly string _operation;
    private bool _dontShowAgain;

    private ElevationHintModal(ConsoleWindowSystem ws, string operation, Window? parent)
        : base(ws, parent)
    {
        _operation = operation;
    }

    public static Task<ElevationHintResult> ShowAsync(
        ConsoleWindowSystem ws, string operation, Window? parent = null)
        => new ElevationHintModal(ws, operation, parent).ShowAsync();

    protected override string GetTitle() => "Permission Denied";
    protected override int GetWidth() => 60;
    protected override int GetHeight() => 16;

    protected override ElevationHintResult GetDefaultResult()
        => new() { OpenSettings = false, DontShowAgain = _dontShowAgain };

    protected override void BuildContent()
    {
        Modal!.AddControl(Controls.Markup()
            .AddLine($"[bold yellow]{SharpConsoleUI.Parsing.MarkupParser.Escape(_operation)} needs elevated privileges.[/]")
            .AddEmptyLine()
            .AddLine("[grey85]This item is owned by another user (such as root), so")
            .AddLine("the operation can't complete without administrator rights.[/]")
            .AddEmptyLine()
            .AddLine("[grey70]Enable [bold]Allow sudo elevation[/] under Settings → Security")
            .AddLine("to retry operations like this with a password prompt.[/]")
            .AddEmptyLine()
            .WithMargin(1, 1, 1, 0)
            .Build());

        var dontShow = Controls.Checkbox("Don't show this again")
            .Checked(_dontShowAgain)
            .Build();
        dontShow.CheckedChanged += (_, _) => _dontShowAgain = dontShow.Checked;
        dontShow.Margin = new Margin(1, 0, 1, 0);
        Modal.AddControl(dontShow);

        var settingsBtn = Controls.Button(" Open Settings ")
            .OnClick((_, _) => CloseWithResult(
                new ElevationHintResult { OpenSettings = true, DontShowAgain = _dontShowAgain }))
            .Build();
        var closeBtn = Controls.Button("  Close  ")
            .OnClick((_, _) => CloseWithResult(
                new ElevationHintResult { OpenSettings = false, DontShowAgain = _dontShowAgain }))
            .Build();

        var buttons = Controls.HorizontalGrid()
            .WithAlignment(HorizontalAlignment.Stretch)
            .Column(col => col.Flex(1).Add(settingsBtn))
            .Column(col => col.Flex(1).Add(closeBtn))
            .Build();
        buttons.Margin = new Margin(1, 0, 1, 0);
        Modal.AddControl(buttons);

        Modal.AddControl(Controls.Markup()
            .AddLine("[dim]Esc: Close[/]")
            .StickyBottom()
            .WithMargin(1, 0, 1, 0)
            .Build());
    }
}
