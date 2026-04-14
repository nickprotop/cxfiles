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
    private SearchResultsDataSource? _searchDataSource;
    private string _currentPath = "";
    private bool _showHidden;
    private bool _autoSelectFirstItem;

    public TableControl Control => _table;
    public string CurrentPath => _currentPath;
    public bool InSearchMode => _table.DataSource is SearchResultsDataSource;

    public bool AutoSelectFirstItem
    {
        get => _autoSelectFirstItem;
        set => _autoSelectFirstItem = value;
    }

    public event Action<FileEntry>? FileActivated;
    public event Action<SelectionInfo>? SelectionChanged;

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
            var entry = ResolveEntry(rowIndex, sync: true);
            if (entry != null)
                FileActivated?.Invoke(entry);
        };

        _table.SelectedRowChanged += (_, idx) =>
        {
            var tag = _table.DataSource?.GetRowTag(idx);
            SelectionInfo info = tag switch
            {
                FileEntry fe              => SelectionInfo.Resolved(fe),
                SearchRow { Full: { } f } => SelectionInfo.Resolved(f),
                SearchRow row             => SelectionInfo.Loading(row.Hit),
                _                         => SelectionInfo.Empty
            };
            SelectionChanged?.Invoke(info);
        };
    }

    private FileEntry? ResolveEntry(int rowIndex, bool sync)
    {
        var tag = _table.DataSource?.GetRowTag(rowIndex);
        return tag switch
        {
            FileEntry fe              => fe,
            SearchRow { Full: { } f } => f,
            SearchRow row             => sync ? SafeGetFileInfo(row.Hit.FullPath) : null,
            _                         => null
        };
    }

    private FileEntry? SafeGetFileInfo(string fullPath)
    {
        try { return _fs.GetFileInfo(fullPath); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
        catch (IOException) { return null; }
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
            // Fire an empty SelectionChanged so listeners (detail panel, toolbar)
            // reset to the folder-centric view even if the table didn't change row.
            SelectionChanged?.Invoke(SelectionInfo.Empty);
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
        return ResolveEntry(idx, sync: true);
    }

    public FileEntry? GetEntryAt(int index) => ResolveEntry(index, sync: true);

    public void Refresh() => Navigate(_currentPath);

    // --- Search-mode hooks ---

    public void EnterSearchMode(SearchResultsDataSource results)
    {
        if (_searchDataSource != null && _searchDataSource != results)
            _searchDataSource.RowHydrated -= OnRowHydrated;

        _searchDataSource = results;
        results.RowHydrated += OnRowHydrated;

        if (_table.DataSource != results)
            _table.DataSource = results;
    }

    public void ExitSearchMode()
    {
        if (_searchDataSource != null)
        {
            _searchDataSource.RowHydrated -= OnRowHydrated;
            _searchDataSource = null;
        }
        if (_table.DataSource != _dataSource)
            _table.DataSource = _dataSource;
    }

    private void OnRowHydrated(SearchRow row)
    {
        if (_searchDataSource == null) return;
        int idx = _searchDataSource.IndexOfRow(row);
        if (idx >= 0 && idx == _table.SelectedRowIndex && row.Full != null)
            SelectionChanged?.Invoke(SelectionInfo.Resolved(row.Full));
    }

    public FileListSnapshot CaptureSnapshot() => new(
        _dataSource.CaptureState(),
        _table.SelectedRowIndex,
        0);

    public void RestoreSnapshot(FileListSnapshot snapshot)
    {
        _dataSource.RestoreState(snapshot.DataState);
        if (snapshot.SelectedIndex >= 0 && snapshot.SelectedIndex < _table.RowCount)
            _table.SelectedRowIndex = snapshot.SelectedIndex;
    }
}

public sealed record FileListSnapshot(
    FileDataSourceState DataState,
    int SelectedIndex,
    int ScrollOffset);
