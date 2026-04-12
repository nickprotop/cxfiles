using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using CXFiles.Services;
using CXFiles.Models;

namespace CXFiles.UI.Components;

public class FileListPanel
{
    private readonly IFileSystemService _fs;
    private readonly TableControl _table;
    private string _currentPath = "";
    private List<FileEntry> _entries = new();
    private bool _showHidden;

    public TableControl Control => _table;
    public string CurrentPath => _currentPath;

    public event Action<FileEntry>? FileActivated;
    public event Action<FileEntry>? SelectionChanged;

    public FileListPanel(IFileSystemService fs, bool showHidden = false)
    {
        _fs = fs;
        _showHidden = showHidden;

        _table = Controls.Table()
            .AddColumn("Name")
            .AddColumn("Size")
            .AddColumn("Modified")
            .AddColumn("Type")
            .Interactive()
            .WithMultiSelect()
            .WithBorderStyle(BorderStyle.None)
            .Build();
        _table.VerticalAlignment = VerticalAlignment.Fill;

        _table.RowActivated += (_, rowIndex) =>
        {
            if (rowIndex >= 0 && rowIndex < _entries.Count)
                FileActivated?.Invoke(_entries[rowIndex]);
        };

        _table.SelectedRowChanged += (_, idx) =>
        {
            if (idx >= 0 && idx < _entries.Count)
                SelectionChanged?.Invoke(_entries[idx]);
        };
    }

    public void Navigate(string path)
    {
        if (!_fs.DirectoryExists(path)) return;

        _currentPath = path;
        _entries = _fs.ListDirectory(path)
            .Where(e => _showHidden || !e.IsHidden)
            .OrderByDescending(e => e.IsDirectory)
            .ThenBy(e => e.Name)
            .ToList();

        _table.ClearRows();
        foreach (var entry in _entries)
        {
            var nameColor = entry.IsDirectory ? "cyan" : entry.IsHidden ? "grey50" : "white";
            _table.AddRow(
                $"[{nameColor}]{entry.Icon} {SharpConsoleUI.Parsing.MarkupParser.Escape(entry.Name)}[/]",
                entry.DisplaySize,
                entry.DisplayDate,
                entry.TypeDescription);
        }
    }

    public void SetShowHidden(bool show)
    {
        _showHidden = show;
        if (!string.IsNullOrEmpty(_currentPath))
            Navigate(_currentPath);
    }

    public FileEntry? GetSelectedEntry()
    {
        var idx = _table.SelectedRowIndex;
        return idx >= 0 && idx < _entries.Count ? _entries[idx] : null;
    }

    public void Refresh() => Navigate(_currentPath);
}
