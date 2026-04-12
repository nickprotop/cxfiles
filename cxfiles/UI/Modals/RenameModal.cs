using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace CXFiles.UI.Modals;

public class RenameModal : ModalBase<string?>
{
    private readonly string _currentName;

    private RenameModal(ConsoleWindowSystem ws, string currentName, Window? parent)
        : base(ws, parent)
    {
        _currentName = currentName;
    }

    public static Task<string?> ShowAsync(ConsoleWindowSystem ws, string currentName, Window? parent = null)
        => new RenameModal(ws, currentName, parent).ShowAsync();

    protected override string GetTitle() => "Rename";
    protected override int GetWidth() => 50;
    protected override int GetHeight() => 8;
    protected override string? GetDefaultResult() => null;

    protected override void BuildContent()
    {
        var prompt = Controls.Prompt("New name: ")
            .WithInput(_currentName)
            .UnfocusOnEnter(false)
            .WithInputWidth(30)
            .WithMargin(1, 1, 1, 0)
            .OnEntered((_, text) =>
            {
                if (!string.IsNullOrWhiteSpace(text) && text != _currentName)
                    CloseWithResult(text);
            })
            .Build();

        Modal!.AddControl(prompt);

        Modal.AddControl(Controls.Markup()
            .AddLine("[dim]Enter: Confirm  Esc: Cancel[/]")
            .StickyBottom()
            .WithMargin(1, 0, 1, 0)
            .Build());
    }
}
