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
    private readonly FileDataSource _dataSource;
    private string _currentPath = "";
    private bool _showHidden;

    public TableControl Control => _table;
    public string CurrentPath => _currentPath;

    public event Action<FileEntry>? FileActivated;
    public event Action<FileEntry>? SelectionChanged;

    public FileListPanel(IFileSystemService fs, bool showHidden = false)
    {
        _fs = fs;
        _showHidden = showHidden;
        _dataSource = new FileDataSource();

        _table = Controls.Table()
            .WithDataSource(_dataSource)
            .Interactive()
            .WithMultiSelect()
            .WithSorting()
            .WithFiltering()
            .WithBorderStyle(BorderStyle.None)
            .Build();
        _table.VerticalAlignment = VerticalAlignment.Fill;
        _table.HorizontalAlignment = HorizontalAlignment.Stretch;

        _table.RowActivated += (_, rowIndex) =>
        {
            var entry = _dataSource.GetEntry(rowIndex);
            if (entry != null)
                FileActivated?.Invoke(entry);
        };

        _table.SelectedRowChanged += (_, idx) =>
        {
            var entry = _dataSource.GetEntry(idx);
            if (entry != null)
                SelectionChanged?.Invoke(entry);
        };
    }

    public void Navigate(string path)
    {
        if (!_fs.DirectoryExists(path)) return;
        _currentPath = path;

        var entries = _fs.ListDirectory(path)
            .Where(e => _showHidden || !e.IsHidden);

        _dataSource.SetEntries(entries);
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
        return _dataSource.GetEntry(idx);
    }

    public FileEntry? GetEntryAt(int index) => _dataSource.GetEntry(index);

    public void Refresh() => Navigate(_currentPath);
}
