using System.Collections.Specialized;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using CXFiles.Models;

namespace CXFiles.UI.Components;

public class TrashDataSource : ITableDataSource
{
    private List<TrashEntry> _entries = new();

    private static readonly string[] ColumnHeaders = { "Name", "Original Location", "Deleted", "Size" };

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
            0 => $"[{(e.IsDirectory ? "cyan" : "white")}]{(e.IsDirectory ? "▸" : "◦")} {SharpConsoleUI.Parsing.MarkupParser.Escape(e.TrashedName)}[/]",
            1 => SharpConsoleUI.Parsing.MarkupParser.Escape(Path.GetDirectoryName(e.OriginalPath) ?? ""),
            2 => e.DeletionDate.ToString("yyyy-MM-dd HH:mm"),
            3 => e.IsDirectory ? "" : FormatSize(e.Size),
            _ => ""
        };
    }

    public TextJustification GetColumnAlignment(int col) => col == 3
        ? TextJustification.Right
        : TextJustification.Left;

    public int? GetColumnWidth(int col) => col switch
    {
        2 => 18,
        3 => 10,
        _ => null
    };

    public bool CanSort(int col) => true;
    public void Sort(int col, SortDirection dir) { }
    public bool CanFilter => false;
    public void ApplyFilter(string filterText, string? columnName, FilterOperator op) { }
    public void ClearFilter() { }

    public object? GetRowTag(int row) =>
        row >= 0 && row < _entries.Count ? _entries[row] : null;

    public Color? GetRowForegroundColor(int row) => null;

    public TrashEntry? GetEntry(int row) =>
        row >= 0 && row < _entries.Count ? _entries[row] : null;

    public void SetEntries(IReadOnlyList<TrashEntry> entries)
    {
        _entries = entries.ToList();
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes}B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1}K",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1}M",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F1}G"
    };
}
