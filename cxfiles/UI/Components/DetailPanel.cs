using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using CXFiles.Models;
using CXFiles.Services;

namespace CXFiles.UI.Components;

public class DetailPanel
{
    private readonly IFileSystemService _fs;
    private readonly ScrollablePanelControl _panel;
    private readonly MarkupControl _info;

    public ScrollablePanelControl Control => _panel;

    public DetailPanel(IFileSystemService fs)
    {
        _fs = fs;
        _info = Controls.Markup()
            .WithAlignment(HorizontalAlignment.Stretch)
            .Build();
        _info.SetContent(new List<string> { "[dim]Select a file to see details[/]" });

        _panel = Controls.ScrollablePanel()
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithBorderStyle(BorderStyle.None)
            .WithBackgroundColor(new Color(15, 50, 40, 140))
            .WithMargin(1, 0, 0, 0)
            .Build();
        _panel.AddControl(_info);
    }

    public void ShowEntry(FileEntry? entry)
    {
        if (entry == null)
        {
            _info.SetContent(new List<string> { "[dim]Select a file to see details[/]" });
            return;
        }

        var lines = new List<string>
        {
            $"[bold]{entry.Icon} {SharpConsoleUI.Parsing.MarkupParser.Escape(entry.Name)}[/]",
            "",
            $"[dim]Type:[/]     {entry.TypeDescription}",
            $"[dim]Path:[/]     {SharpConsoleUI.Parsing.MarkupParser.Escape(entry.FullPath)}",
        };

        if (!entry.IsDirectory)
            lines.Add($"[dim]Size:[/]     {entry.DisplaySize}");

        lines.Add($"[dim]Modified:[/] {entry.Modified:yyyy-MM-dd HH:mm:ss}");
        lines.Add($"[dim]Created:[/]  {entry.Created:yyyy-MM-dd HH:mm:ss}");

        if (entry.IsHidden) lines.Add("[dim]Hidden:[/]   Yes");
        if (entry.IsReadOnly) lines.Add("[dim]ReadOnly:[/] Yes");
        if (entry.IsSymlink) lines.Add("[dim]Symlink:[/]  Yes");

        // Text file preview
        if (!entry.IsDirectory && IsTextFile(entry.Extension))
        {
            lines.Add("");
            lines.Add("[bold]Preview[/]");
            lines.Add("[dim]─────────────────────────[/]");
            var preview = _fs.GetFilePreview(entry.FullPath, 20);
            foreach (var line in preview.Split('\n'))
            {
                lines.Add($"[dim]{SharpConsoleUI.Parsing.MarkupParser.Escape(line)}[/]");
            }
        }

        _info.SetContent(lines);
    }

    private static bool IsTextFile(string? ext) => ext?.ToLowerInvariant() switch
    {
        ".txt" or ".md" or ".json" or ".xml" or ".yaml" or ".yml" or ".toml" or
        ".cs" or ".js" or ".ts" or ".py" or ".rs" or ".go" or ".java" or
        ".cpp" or ".c" or ".h" or ".html" or ".css" or ".sql" or ".sh" or
        ".bash" or ".log" or ".cfg" or ".ini" or ".conf" or ".csv" => true,
        _ => false
    };
}
