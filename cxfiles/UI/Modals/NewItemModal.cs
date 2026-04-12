using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace CXFiles.UI.Modals;

public record NewItemResult(string? Name, bool IsDirectory);

public class NewItemModal : ModalBase<NewItemResult?>
{
    private bool _isDirectory;

    private NewItemModal(ConsoleWindowSystem ws, bool isDirectory, Window? parent)
        : base(ws, parent)
    {
        _isDirectory = isDirectory;
    }

    public static Task<NewItemResult?> ShowAsync(ConsoleWindowSystem ws, bool isDirectory = false, Window? parent = null)
        => new NewItemModal(ws, isDirectory, parent).ShowAsync();

    protected override string GetTitle() => _isDirectory ? "New Folder" : "New File";
    protected override int GetWidth() => 50;
    protected override int GetHeight() => 8;
    protected override NewItemResult? GetDefaultResult() => null;

    protected override void BuildContent()
    {
        var label = _isDirectory ? "Folder name: " : "File name: ";
        var prompt = Controls.Prompt(label)
            .UnfocusOnEnter(false)
            .WithInputWidth(28)
            .WithMargin(1, 1, 1, 0)
            .OnEntered((_, text) =>
            {
                if (!string.IsNullOrWhiteSpace(text))
                    CloseWithResult(new NewItemResult(text, _isDirectory));
            })
            .Build();

        Modal!.AddControl(prompt);

        Modal.AddControl(Controls.Markup()
            .AddLine("[dim]Enter: Create  Esc: Cancel[/]")
            .StickyBottom()
            .WithMargin(1, 0, 1, 0)
            .Build());
    }
}
