using System.Collections.Specialized;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using CXFiles.Models;
using CXFiles.Services;

namespace CXFiles.UI.Components;

public sealed class SearchRow
{
    public required SearchHit Hit { get; init; }
    public FileEntry? Full { get; set; }
}

public class SearchResultsDataSource : ITableDataSource
{
    private readonly List<SearchRow> _rows = new();
    private int _firstFileIndex;

    private static readonly string[] ColumnHeaders =
        { "Name", "Path", "Size", "Modified", "Type" };

    private const string Placeholder = "…";

    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public event Action<SearchRow>? RowHydrated;

    public int RowCount => _rows.Count;
    public int ColumnCount => 5;

    public string GetColumnHeader(int col) => ColumnHeaders[col];

    public string GetCellValue(int row, int col)
    {
        if (row < 0 || row >= _rows.Count) return "";
        var r = _rows[row];
        var full = r.Full;
        var hit = r.Hit;

        return col switch
        {
            0 => full != null
                ? $"[{NameColor(full)}]{full.Icon} {SharpConsoleUI.Parsing.MarkupParser.Escape(full.Name)}[/]"
                : $"[{(hit.IsDirectory ? "cyan" : "white")}]{(hit.IsDirectory ? "▸" : "◦")} {SharpConsoleUI.Parsing.MarkupParser.Escape(hit.Name)}[/]",
            1 => SharpConsoleUI.Parsing.MarkupParser.Escape(hit.RelativePath),
            2 => full != null ? full.DisplaySize : (hit.IsDirectory ? "" : Placeholder),
            3 => full != null ? full.DisplayDate : Placeholder,
            4 => full != null ? full.TypeDescription : (hit.IsDirectory ? "Folder" : Placeholder),
            _ => ""
        };
    }

    public TextJustification GetColumnAlignment(int col) => col == 2
        ? TextJustification.Right
        : TextJustification.Left;

    public int? GetColumnWidth(int col) => col switch
    {
        2 => 10,
        3 => 18,
        4 => 14,
        _ => null
    };

    public bool CanSort(int col) => false;
    public bool CanFilter => false;

    public object? GetRowTag(int row)
    {
        if (row < 0 || row >= _rows.Count) return null;
        var r = _rows[row];
        return (object?)r.Full ?? r;
    }

    private static readonly Color OddRowBg = new(18, 22, 35);

    public Color? GetRowBackgroundColor(int row) =>
        row % 2 == 1 ? OddRowBg : null;

    public Color? GetRowForegroundColor(int row)
    {
        if (row < 0 || row >= _rows.Count) return null;
        var r = _rows[row];
        if (r.Hit.IsDirectory) return Color.Cyan;
        return null;
    }

    private static string NameColor(FileEntry e) =>
        e.IsDirectory ? "cyan" : e.IsHidden ? "grey50" : "white";

    public IReadOnlyList<SearchRow> AppendHits(IEnumerable<SearchHit> batch)
    {
        var added = new List<SearchRow>();
        foreach (var hit in batch)
        {
            var row = new SearchRow { Hit = hit };
            if (hit.IsDirectory)
            {
                _rows.Insert(_firstFileIndex, row);
                _firstFileIndex++;
            }
            else
            {
                _rows.Add(row);
            }
            added.Add(row);
        }

        if (added.Count > 0)
            CollectionChanged?.Invoke(this,
                new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

        return added;
    }

    public void Clear()
    {
        _rows.Clear();
        _firstFileIndex = 0;
        CollectionChanged?.Invoke(this,
            new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    public void SetHydrated(SearchRow row, FileEntry full)
    {
        row.Full = full;
        RowHydrated?.Invoke(row);
        // Do NOT fire CollectionChanged here — caller coalesces refreshes
        // to avoid reset-storm flicker. Call NotifyChanged() after a drain tick.
    }

    public void NotifyChanged()
    {
        CollectionChanged?.Invoke(this,
            new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    public int IndexOfRow(SearchRow row) => _rows.IndexOf(row);
}
