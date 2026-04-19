using CXFiles.Models;

namespace CXFiles.Services;

public record FileSystemChangeEvent(string Path, WatcherChangeTypes ChangeType);

public record DirectorySizeProgress(
    long BytesSoFar,
    long FilesScanned,
    long InaccessibleEntries,
    bool IsFinal,
    IReadOnlyDictionary<string, long>? PerChildBytes = null);

public sealed record SearchHit(
    string Name,
    string FullPath,
    string RelativePath,
    bool IsDirectory);

public sealed class SearchProgress
{
    public int DirsScanned { get; init; }
    public int MatchesFound { get; init; }
    public bool LimitReached { get; init; }
}

public interface IDirectoryWatcher : IDisposable
{
    void Pause();
    void Resume();
}

public interface IFileSystemService
{
    IReadOnlyList<DriveEntry> GetDrives();
    IReadOnlyList<FileEntry> ListDirectory(string path);
    FileEntry GetFileInfo(string path);
    bool DirectoryExists(string path);
    bool FileExists(string path);

    IAsyncEnumerable<SearchHit> SearchAsync(
        string root,
        string query,
        bool recurse,
        bool showHidden,
        IProgress<SearchProgress>? progress,
        CancellationToken ct);

    Task<FileEntry?> HydrateAsync(string fullPath, CancellationToken ct);

    Task CopyAsync(string source, string dest, bool overwrite,
        IProgress<(long bytes, long total)>? progress, CancellationToken ct);
    Task MoveAsync(string source, string dest, bool overwrite,
        IProgress<(long bytes, long total)>? progress, CancellationToken ct);
    Task DeleteAsync(string path, bool recursive, CancellationToken ct);
    void Rename(string path, string newName);
    void CreateDirectory(string path);
    void CreateFile(string path);

    string GetFilePreview(string path, int maxLines);
    long GetDirectorySize(string path, CancellationToken ct);
    void EnumerateDirectorySize(string path,
        IProgress<DirectorySizeProgress> progress,
        CancellationToken ct);

    IDirectoryWatcher WatchDirectory(string path, Action<FileSystemChangeEvent> onChange);
}
