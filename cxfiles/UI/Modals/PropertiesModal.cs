using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using CXFiles.Models;

namespace CXFiles.UI.Modals;

public class PropertiesModal : ModalBase<bool>
{
    private readonly FileEntry _entry;

    private PropertiesModal(ConsoleWindowSystem ws, FileEntry entry, Window? parent)
        : base(ws, parent)
    {
        _entry = entry;
    }

    public static Task<bool> ShowAsync(ConsoleWindowSystem ws, FileEntry entry, Window? parent = null)
        => new PropertiesModal(ws, entry, parent).ShowAsync();

    protected override string GetTitle() => "Properties";
    protected override int GetWidth() => 55;
    protected override int GetHeight() => 16;
    protected override bool GetDefaultResult() => false;

    protected override void BuildContent()
    {
        var lines = new List<string>
        {
            $"[bold]{_entry.Icon} {SharpConsoleUI.Parsing.MarkupParser.Escape(_entry.Name)}[/]",
            "",
            $"  [dim]Type:[/]       {_entry.TypeDescription}",
            $"  [dim]Location:[/]   {SharpConsoleUI.Parsing.MarkupParser.Escape(Path.GetDirectoryName(_entry.FullPath) ?? "")}",
        };

        if (!_entry.IsDirectory)
            lines.Add($"  [dim]Size:[/]       {_entry.DisplaySize} ({_entry.Size:N0} bytes)");

        lines.Add($"  [dim]Modified:[/]   {_entry.Modified:yyyy-MM-dd HH:mm:ss}");
        lines.Add($"  [dim]Created:[/]    {_entry.Created:yyyy-MM-dd HH:mm:ss}");
        lines.Add("");

        var attrs = new List<string>();
        if (_entry.IsHidden) attrs.Add("Hidden");
        if (_entry.IsReadOnly) attrs.Add("Read-only");
        if (_entry.IsSymlink) attrs.Add("Symlink");
        if (attrs.Count > 0)
            lines.Add($"  [dim]Attributes:[/] {string.Join(", ", attrs)}");
        else
            lines.Add($"  [dim]Attributes:[/] None");

        // Unix permissions
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                var info = new FileInfo(_entry.FullPath);
                lines.Add($"  [dim]Permissions:[/] {info.UnixFileMode}");
            }
        }
        catch { }

        var markup = Controls.Markup().WithMargin(1, 1, 1, 0).Build();
        markup.SetContent(lines);
        Modal!.AddControl(markup);

        Modal.AddControl(Controls.Markup()
            .AddLine("[dim]Esc or Enter: Close[/]")
            .StickyBottom()
            .WithMargin(1, 0, 1, 0)
            .Build());

        Modal.KeyPressed += (_, e) =>
        {
            if (e.KeyInfo.Key == ConsoleKey.Enter)
            {
                CloseWithResult(true);
                e.Handled = true;
            }
        };
    }
}
