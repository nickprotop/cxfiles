using CXFiles.Models;

namespace CXFiles.Services;

public interface ITrashService
{
    bool IsAvailable { get; }
    string TrashPath { get; }
    Task TrashAsync(string path, CancellationToken ct);

    /// <summary>
    /// Moves <paramref name="path"/> into the trash using a caller-supplied mover
    /// (e.g. an elevated <c>sudo mv</c>) to relocate the item, while this service
    /// still computes the trash destination and writes the trashinfo metadata.
    /// </summary>
    Task TrashWithMoverAsync(string path, Func<string, string, CancellationToken, Task> mover, CancellationToken ct);

    Task RestoreAsync(string trashedName, CancellationToken ct);
    Task EmptyTrashAsync(CancellationToken ct);
    IReadOnlyList<TrashEntry> ListTrash();
    int TrashCount { get; }
}
