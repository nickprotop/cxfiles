using CXFiles.Models;

namespace CXFiles.Services;

public record FileSystemChangeEvent(string Path, WatcherChangeTypes ChangeType);

public record DirectorySizeProgress(
    long BytesSoFar,
    long FilesScanned,
    long InaccessibleEntries,
    bool IsFinal,
    IReadOnlyDictionary<string, long>? PerChildBytes = null);

public interface IFileSystemService
{
    IReadOnlyList<DriveEntry> GetDrives();
    IReadOnlyList<FileEntry> ListDirectory(string path);
    FileEntry GetFileInfo(string path);
    bool DirectoryExists(string path);
    bool FileExists(string path);

    Task CopyAsync(string source, string dest, bool overwrite,
        IProgress<(long bytes, long total)>? progress, CancellationToken ct);
    Task MoveAsync(string source, string dest, bool overwrite, CancellationToken ct);
    Task DeleteAsync(string path, bool recursive, CancellationToken ct);
    void Rename(string path, string newName);
    void CreateDirectory(string path);
    void CreateFile(string path);

    string GetFilePreview(string path, int maxLines);
    long GetDirectorySize(string path, CancellationToken ct);
    void EnumerateDirectorySize(string path,
        IProgress<DirectorySizeProgress> progress,
        CancellationToken ct);

    IDisposable WatchDirectory(string path, Action<FileSystemChangeEvent> onChange);
}
