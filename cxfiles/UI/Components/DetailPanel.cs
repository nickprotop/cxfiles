using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Imaging;
using SharpConsoleUI.Layout;
using CXFiles.Models;
using CXFiles.Services;

namespace CXFiles.UI.Components;

public class DetailPanel
{
    private readonly IFileSystemService _fs;
    private readonly ScrollablePanelControl _panel;
    private readonly MarkupControl _info;
    private readonly ImageControl _image;

    public ScrollablePanelControl Control => _panel;

    public DetailPanel(IFileSystemService fs)
    {
        _fs = fs;
        _info = Controls.Markup()
            .WithAlignment(HorizontalAlignment.Stretch)
            .Build();
        _info.SetContent(new List<string> { "[dim]Select a file to see details[/]" });

        _image = Controls.Image()
            .Fit()
            .WithAlignment(HorizontalAlignment.Center)
            .WithVerticalAlignment(VerticalAlignment.Top)
            .WithMargin(0, 1, 0, 0)
            .Build();
        _image.Visible = false;

        _panel = Controls.ScrollablePanel()
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithBorderStyle(BorderStyle.None)
            .WithBackgroundColor(new Color(15, 50, 40, 140))
            .WithMargin(1, 0, 0, 0)
            .Build();
        _panel.AddControl(_info);
        _panel.AddControl(_image);
    }

    public void ShowFolder(string path)
    {
        HideImage();
        var lines = new List<string>();
        DirectoryInfo? dir = null;
        try { dir = new DirectoryInfo(path); } catch { }

        if (dir == null || !dir.Exists)
        {
            _info.SetContent(new List<string> { "[dim]No folder selected[/]" });
            return;
        }

        lines.Add($"[bold]▸ {SharpConsoleUI.Parsing.MarkupParser.Escape(dir.Name.Length == 0 ? dir.FullName : dir.Name)}[/]");
        lines.Add("");
        lines.Add($"[dim]Type:[/]     Folder");
        lines.Add($"[dim]Path:[/]     {SharpConsoleUI.Parsing.MarkupParser.Escape(dir.FullName)}");

        int folderCount = 0, fileCount = 0;
        try
        {
            foreach (var _ in dir.EnumerateDirectories()) folderCount++;
            foreach (var _ in dir.EnumerateFiles()) fileCount++;
        }
        catch { }

        lines.Add($"[dim]Contains:[/] {folderCount} folder{(folderCount == 1 ? "" : "s")}, {fileCount} file{(fileCount == 1 ? "" : "s")}");

        try { lines.Add($"[dim]Modified:[/] {dir.LastWriteTime:yyyy-MM-dd HH:mm:ss}"); } catch { }
        try { lines.Add($"[dim]Created:[/]  {dir.CreationTime:yyyy-MM-dd HH:mm:ss}"); } catch { }

        _info.SetContent(lines);
    }

    public void ShowLoading(string name, string relativePath)
    {
        HideImage();
        var lines = new List<string>
        {
            $"[bold]◦ {SharpConsoleUI.Parsing.MarkupParser.Escape(name)}[/]",
            "",
            "[dim]Type:[/]     …",
            $"[dim]Path:[/]     {SharpConsoleUI.Parsing.MarkupParser.Escape(relativePath)}",
            "[dim]Size:[/]     …",
            "[dim]Modified:[/] …",
            "",
            "[dim]Loading metadata…[/]",
        };
        _info.SetContent(lines);
    }

    public void ShowEntry(FileEntry? entry)
    {
        if (entry == null)
        {
            HideImage();
            _info.SetContent(new List<string> { "[dim]Nothing selected[/]" });
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

        if (!entry.IsDirectory && IsImageFile(entry.Extension))
        {
            _info.SetContent(lines);
            ShowImagePreview(entry.FullPath);
            return;
        }

        HideImage();

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

    private void ShowImagePreview(string path)
    {
        try
        {
            var pixels = PixelBuffer.FromFile(path);
            _image.Source = pixels;
            _image.Visible = true;
        }
        catch
        {
            HideImage();
        }
    }

    private void HideImage()
    {
        if (_image.Visible)
        {
            _image.Source = null;
            _image.Visible = false;
        }
    }

    private static bool IsImageFile(string? ext) => ext?.ToLowerInvariant() switch
    {
        ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or
        ".tga" or ".tiff" or ".tif" or ".pbm" => true,
        _ => false
    };

    private static bool IsTextFile(string? ext) => ext?.ToLowerInvariant() switch
    {
        ".txt" or ".md" or ".json" or ".xml" or ".yaml" or ".yml" or ".toml" or
        ".cs" or ".js" or ".ts" or ".py" or ".rs" or ".go" or ".java" or
        ".cpp" or ".c" or ".h" or ".html" or ".css" or ".sql" or ".sh" or
        ".bash" or ".log" or ".cfg" or ".ini" or ".conf" or ".csv" => true,
        _ => false
    };
}
