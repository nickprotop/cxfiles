using System.Collections.Specialized;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using CXFiles.Models;

namespace CXFiles.UI.Components;

public sealed record FileDataSourceState(
    IReadOnlyList<FileEntry> AllEntries,
    int SortColumn,
    SortDirection SortDirection,
    string? FilterText);

public class FileDataSource : ITableDataSource
{
    private List<FileEntry> _allEntries = new();
    private List<FileEntry> _entries = new();
    private int _sortColumn = 0;
    private SortDirection _sortDirection = SortDirection.Ascending;
    private string? _filterText;

    public FileDataSourceState CaptureState() =>
        new(_allEntries.ToList(), _sortColumn, _sortDirection, _filterText);

    public void RestoreState(FileDataSourceState s)
    {
        _allEntries = s.AllEntries.ToList();
        _sortColumn = s.SortColumn;
        _sortDirection = s.SortDirection;
        _filterText = s.FilterText;
        ApplySortAndFilter();
    }

    private static readonly string[] ColumnHeaders = { "Name", "Size", "Modified", "Type" };

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public int RowCount => _entries.Count;
    public int ColumnCount => 4;

    public string GetColumnHeader(int col) => ColumnHeaders[col];

    public string GetCellValue(int row, int col)
    {
        if (row < 0 || row >= _entries.Count) return "";
        var e = _entries[row];
        return col switch
        {
            0 => $"[{GetNameColor(e)}]{e.Icon} {SharpConsoleUI.Parsing.MarkupParser.Escape(e.Name)}[/]",
            1 => e.DisplaySize,
            2 => e.DisplayDate,
            3 => e.TypeDescription,
            _ => ""
        };
    }

    public TextJustification GetColumnAlignment(int col) => col == 1
        ? TextJustification.Right
        : TextJustification.Left;

    public int? GetColumnWidth(int col) => col switch
    {
        1 => 10,
        2 => 18,
        3 => 14,
        _ => null
    };

    public bool CanSort(int col) => true;

    public void Sort(int col, SortDirection dir)
    {
        _sortColumn = col;
        _sortDirection = dir;
        ApplySortAndFilter();
    }

    public bool CanFilter => true;

    public void ApplyFilter(string filterText, string? columnName, FilterOperator op)
    {
        _filterText = filterText;
        ApplySortAndFilter();
    }

    public void ClearFilter()
    {
        _filterText = null;
        ApplySortAndFilter();
    }

    public object? GetRowTag(int row) =>
        row >= 0 && row < _entries.Count ? _entries[row] : null;

    private static readonly Color OddRowBg = new(18, 22, 35);

    public Color? GetRowBackgroundColor(int row) =>
        row % 2 == 1 ? OddRowBg : null;

    public Color? GetRowForegroundColor(int row)
    {
        if (row < 0 || row >= _entries.Count) return null;
        var e = _entries[row];
        if (e.IsHidden) return new Color(100, 100, 100);
        if (e.IsDirectory) return Color.Cyan;
        return null;
    }

    public FileEntry? GetEntry(int row) =>
        row >= 0 && row < _entries.Count ? _entries[row] : null;

    public void SetEntries(IEnumerable<FileEntry> entries)
    {
        _allEntries = entries.ToList();
        ApplySortAndFilter();
    }

    private void ApplySortAndFilter()
    {
        IEnumerable<FileEntry> result = _allEntries;

        // Filter
        if (!string.IsNullOrEmpty(_filterText))
        {
            var filter = _filterText;
            result = result.Where(e => e.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        // Sort — directories always first
        result = _sortDirection == SortDirection.Descending
            ? result.OrderByDescending(e => e.IsDirectory).ThenByDescending(e => GetSortKey(e, _sortColumn))
            : result.OrderByDescending(e => e.IsDirectory).ThenBy(e => GetSortKey(e, _sortColumn));

        _entries = result.ToList();
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    private static object GetSortKey(FileEntry e, int col) => col switch
    {
        0 => e.Name,
        1 => (object)e.Size,
        2 => e.Modified,
        3 => e.TypeDescription,
        _ => e.Name
    };

    private static string GetNameColor(FileEntry e) =>
        e.IsDirectory ? "cyan" : e.IsHidden ? "grey50" : "white";
}
