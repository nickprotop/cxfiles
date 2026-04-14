using CXFiles.Models;
using CXFiles.Services;

namespace CXFiles.UI.Components;

public sealed record SelectionInfo(
    FileEntry? Entry,
    bool IsLoading,
    string? LoadingName,
    string? LoadingPath)
{
    public static readonly SelectionInfo Empty = new(null, false, null, null);
    public static SelectionInfo Resolved(FileEntry e) => new(e, false, null, null);
    public static SelectionInfo Loading(SearchHit hit) =>
        new(null, true, hit.Name, hit.RelativePath);
}
