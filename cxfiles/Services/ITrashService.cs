using CXFiles.Models;

namespace CXFiles.Services;

public interface ITrashService
{
    bool IsAvailable { get; }
    string TrashPath { get; }
    Task TrashAsync(string path, CancellationToken ct);
    Task RestoreAsync(string trashedName, CancellationToken ct);
    Task EmptyTrashAsync(CancellationToken ct);
    IReadOnlyList<TrashEntry> ListTrash();
    int TrashCount { get; }
}
