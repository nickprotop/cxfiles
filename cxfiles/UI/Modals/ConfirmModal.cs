using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace CXFiles.UI.Modals;

public class ConfirmModal : ModalBase<bool>
{
    private readonly string _title;
    private readonly string _message;

    private ConfirmModal(ConsoleWindowSystem ws, string title, string message, Window? parent)
        : base(ws, parent)
    {
        _title = title;
        _message = message;
    }

    public static Task<bool> ShowAsync(ConsoleWindowSystem ws, string title, string message, Window? parent = null)
        => new ConfirmModal(ws, title, message, parent).ShowAsync();

    protected override string GetTitle() => _title;
    protected override int GetWidth() => 50;
    protected override int GetHeight() => 10;
    protected override bool GetDefaultResult() => false;

    protected override void BuildContent()
    {
        Modal!.AddControl(Controls.Markup()
            .AddLine($"[bold]{SharpConsoleUI.Parsing.MarkupParser.Escape(_message)}[/]")
            .AddEmptyLine()
            .WithMargin(1, 1, 1, 0)
            .Build());

        var yesBtn = Controls.Button(" Yes ")
            .OnClick((_, _) => CloseWithResult(true))
            .Build();
        var noBtn = Controls.Button(" No ")
            .OnClick((_, _) => CloseWithResult(false))
            .Build();

        var buttons = Controls.HorizontalGrid()
            .WithAlignment(HorizontalAlignment.Stretch)
            .Column(col => col.Flex(1).Add(yesBtn))
            .Column(col => col.Flex(1).Add(noBtn))
            .Build();
        buttons.Margin = new Margin(1, 0, 1, 0);

        Modal.AddControl(buttons);

        Modal.AddControl(Controls.Markup()
            .AddLine("[dim]Y: Yes  N: No  Esc: Cancel[/]")
            .StickyBottom()
            .WithMargin(1, 0, 1, 0)
            .Build());

        Modal.KeyPressed += (_, e) =>
        {
            if (e.KeyInfo.Key == ConsoleKey.Y) { CloseWithResult(true); e.Handled = true; }
            if (e.KeyInfo.Key == ConsoleKey.N) { CloseWithResult(false); e.Handled = true; }
        };
    }
}
