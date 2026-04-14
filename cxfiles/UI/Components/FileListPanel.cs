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
    private bool _autoSelectFirstItem;

    public TableControl Control => _table;
    public string CurrentPath => _currentPath;

    public bool AutoSelectFirstItem
    {
        get => _autoSelectFirstItem;
        set => _autoSelectFirstItem = value;
    }

    public event Action<FileEntry>? FileActivated;
    public event Action<FileEntry?>? SelectionChanged;

    public FileListPanel(IFileSystemService fs, bool showHidden = false, bool autoSelectFirstItem = false)
    {
        _fs = fs;
        _showHidden = showHidden;
        _autoSelectFirstItem = autoSelectFirstItem;
        _dataSource = new FileDataSource();

        _table = Controls.Table()
            .WithDataSource(_dataSource)
            .Interactive()
            .WithMultiSelect()
            .WithCheckboxMode()
            .WithSorting()
            .WithFiltering()
            .WithFuzzyFilter()
            .WithColumnResize()
            .WithColumnSeparator('│', Color.Grey27)
            .WithBorderStyle(BorderStyle.None)
            .WithBackgroundColor(Color.Transparent)
            .WithHeaderColors(Color.Grey70, new Color(25, 35, 55))
            .WithHorizontalScrollbar(ScrollbarVisibility.Auto)
            .Build();
        _table.TruncationFade = true;
        _table.VerticalAlignment = VerticalAlignment.Fill;
        _table.HorizontalAlignment = HorizontalAlignment.Stretch;
        _table.ClearSelectionOnEmptyClick = true;

        _table.RowActivated += (_, rowIndex) =>
        {
            var entry = _dataSource.GetEntry(rowIndex);
            if (entry != null)
                FileActivated?.Invoke(entry);
        };

        _table.SelectedRowChanged += (_, idx) =>
        {
            SelectionChanged?.Invoke(_dataSource.GetEntry(idx));
        };
    }

    public void Navigate(string path)
    {
        if (!_fs.DirectoryExists(path)) return;
        _currentPath = path;

        if (_table.DataSource != _dataSource)
            _table.DataSource = _dataSource;

        var entries = _fs.ListDirectory(path)
            .Where(e => _showHidden || !e.IsHidden);

        _dataSource.SetEntries(entries);

        if (_autoSelectFirstItem)
        {
            // SetEntries may have already landed on row 0; make it explicit.
            if (_table.RowCount > 0)
                _table.SelectedRowIndex = 0;
        }
        else
        {
            _table.SelectedRowIndex = -1;
            // Fire a null SelectionChanged so listeners (detail panel, toolbar)
            // reset to the folder-centric view even if the table didn't change row.
            SelectionChanged?.Invoke(null);
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
        return _dataSource.GetEntry(idx);
    }

    public FileEntry? GetEntryAt(int index) => _dataSource.GetEntry(index);

    public void Refresh() => Navigate(_currentPath);
}
