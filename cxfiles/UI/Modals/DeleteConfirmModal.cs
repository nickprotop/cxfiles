using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace CXFiles.UI.Modals;

public enum DeleteAction { Cancel, Trash, PermanentDelete }

public class DeleteConfirmModal : ModalBase<DeleteAction>
{
    private readonly string _message;
    private readonly bool _trashAvailable;

    private DeleteConfirmModal(ConsoleWindowSystem ws, string message, bool trashAvailable, Window? parent)
        : base(ws, parent)
    {
        _message = message;
        _trashAvailable = trashAvailable;
    }

    public static Task<DeleteAction> ShowAsync(ConsoleWindowSystem ws, string message,
        bool trashAvailable, Window? parent = null)
        => new DeleteConfirmModal(ws, message, trashAvailable, parent).ShowAsync();

    protected override string GetTitle() => "Delete";
    protected override int GetWidth() => 55;
    protected override int GetHeight() => _trashAvailable ? 13 : 10;
    protected override DeleteAction GetDefaultResult() => DeleteAction.Cancel;

    protected override void BuildContent()
    {
        Modal!.AddControl(Controls.Markup()
            .AddLine($"[bold]{SharpConsoleUI.Parsing.MarkupParser.Escape(_message)}[/]")
            .AddEmptyLine()
            .WithMargin(1, 1, 1, 0)
            .Build());

        if (_trashAvailable)
        {
            var trashBtn = Controls.Button(" Move to Trash ")
                .OnClick((_, _) => CloseWithResult(DeleteAction.Trash))
                .Build();
            var deleteBtn = Controls.Button(" Delete Permanently ")
                .WithForegroundColor(Color.Red)
                .OnClick((_, _) => CloseWithResult(DeleteAction.PermanentDelete))
                .Build();
            var cancelBtn = Controls.Button("  Cancel  ")
                .OnClick((_, _) => CloseWithResult(DeleteAction.Cancel))
                .Build();

            var buttons = Controls.HorizontalGrid()
                .WithAlignment(HorizontalAlignment.Stretch)
                .Column(col => col.Flex(1).Add(trashBtn))
                .Column(col => col.Flex(1).Add(deleteBtn))
                .Column(col => col.Flex(1).Add(cancelBtn))
                .Build();
            buttons.Margin = new Margin(1, 0, 1, 0);
            Modal.AddControl(buttons);

            Modal.AddControl(Controls.Markup()
                .AddLine("[dim]T: Trash  D: Delete permanently  Esc: Cancel[/]")
                .StickyBottom()
                .WithMargin(1, 0, 1, 0)
                .Build());

            Modal.KeyPressed += (_, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.T) { CloseWithResult(DeleteAction.Trash); e.Handled = true; }
                if (e.KeyInfo.Key == ConsoleKey.D) { CloseWithResult(DeleteAction.PermanentDelete); e.Handled = true; }
            };
        }
        else
        {
            var deleteBtn = Controls.Button("  Yes  ")
                .OnClick((_, _) => CloseWithResult(DeleteAction.PermanentDelete))
                .Build();
            var cancelBtn = Controls.Button("  No  ")
                .OnClick((_, _) => CloseWithResult(DeleteAction.Cancel))
                .Build();

            var buttons = Controls.HorizontalGrid()
                .WithAlignment(HorizontalAlignment.Stretch)
                .Column(col => col.Flex(1).Add(deleteBtn))
                .Column(col => col.Flex(1).Add(cancelBtn))
                .Build();
            buttons.Margin = new Margin(1, 0, 1, 0);
            Modal.AddControl(buttons);

            Modal.AddControl(Controls.Markup()
                .AddLine("[dim][yellow]Warning: No trash available — this is permanent[/][/]")
                .AddLine("[dim]Y: Yes  N: No  Esc: Cancel[/]")
                .StickyBottom()
                .WithMargin(1, 0, 1, 0)
                .Build());

            Modal.KeyPressed += (_, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Y) { CloseWithResult(DeleteAction.PermanentDelete); e.Handled = true; }
                if (e.KeyInfo.Key == ConsoleKey.N) { CloseWithResult(DeleteAction.Cancel); e.Handled = true; }
            };
        }
    }
}
