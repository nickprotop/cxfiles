using CXFiles.Models;

namespace CXFiles.Services;

public class FileSystemService : IFileSystemService
{
    public IReadOnlyList<DriveEntry> GetDrives()
    {
        return DriveInfo.GetDrives()
            .Where(d => d.IsReady)
            .Select(d => new DriveEntry(
                d.Name,
                d.RootDirectory.FullName,
                d.VolumeLabel,
                d.TotalSize,
                d.AvailableFreeSpace,
                d.DriveType.ToString()))
            .ToList();
    }

    public IReadOnlyList<FileEntry> ListDirectory(string path)
    {
        var dir = new DirectoryInfo(path);
        if (!dir.Exists) return Array.Empty<FileEntry>();

        var entries = new List<FileEntry>();

        try
        {
            foreach (var d in dir.EnumerateDirectories())
            {
                try
                {
                    entries.Add(CreateEntry(d));
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }

            foreach (var f in dir.EnumerateFiles())
            {
                try
                {
                    entries.Add(CreateEntry(f));
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }

        return entries;
    }

    public FileEntry GetFileInfo(string path)
    {
        if (Directory.Exists(path))
            return CreateEntry(new DirectoryInfo(path));
        return CreateEntry(new FileInfo(path));
    }

    public bool DirectoryExists(string path) => Directory.Exists(path);
    public bool FileExists(string path) => File.Exists(path);

    public async Task CopyAsync(string source, string dest, bool overwrite,
        IProgress<(long bytes, long total)>? progress, CancellationToken ct)
    {
        if (Directory.Exists(source))
        {
            await CopyDirectoryAsync(source, dest, overwrite, progress, ct);
        }
        else
        {
            var fileInfo = new FileInfo(source);
            var total = fileInfo.Length;
            using var sourceStream = File.OpenRead(source);
            using var destStream = File.Create(dest);
            var buffer = new byte[81920];
            long copied = 0;
            int bytesRead;
            while ((bytesRead = await sourceStream.ReadAsync(buffer, ct)) > 0)
            {
                await destStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                copied += bytesRead;
                progress?.Report((copied, total));
            }
        }
    }

    public Task MoveAsync(string source, string dest, bool overwrite, CancellationToken ct)
    {
        if (Directory.Exists(source))
            Directory.Move(source, dest);
        else
            File.Move(source, dest, overwrite);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string path, bool recursive, CancellationToken ct)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive);
        else if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    public void Rename(string path, string newName)
    {
        var dir = Path.GetDirectoryName(path)!;
        var newPath = Path.Combine(dir, newName);
        if (Directory.Exists(path))
            Directory.Move(path, newPath);
        else
            File.Move(path, newPath);
    }

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public void CreateFile(string path) => File.Create(path).Dispose();

    public string GetFilePreview(string path, int maxLines)
    {
        try
        {
            if (!File.Exists(path)) return "";
            var lines = File.ReadLines(path).Take(maxLines);
            return string.Join('\n', lines);
        }
        catch
        {
            return "(binary or unreadable)";
        }
    }

    public long GetDirectorySize(string path, CancellationToken ct)
    {
        long size = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                try { size += new FileInfo(file).Length; } catch { }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { }
        return size;
    }

    public IDisposable WatchDirectory(string path, Action<FileSystemChangeEvent> onChange)
    {
        var watcher = new FileSystemWatcher(path)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                           NotifyFilters.LastWrite | NotifyFilters.Size,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true
        };

        void Handler(object s, FileSystemEventArgs e) =>
            onChange(new FileSystemChangeEvent(e.FullPath, e.ChangeType));

        watcher.Created += Handler;
        watcher.Deleted += Handler;
        watcher.Changed += Handler;
        watcher.Renamed += (s, e) => onChange(new FileSystemChangeEvent(e.FullPath, e.ChangeType));

        return watcher;
    }

    private static FileEntry CreateEntry(FileSystemInfo info)
    {
        bool isDir = info is DirectoryInfo;
        long size = isDir ? 0 : ((FileInfo)info).Length;
        bool isHidden = (info.Attributes & FileAttributes.Hidden) != 0 ||
                        info.Name.StartsWith('.');
        bool isSymlink = (info.Attributes & FileAttributes.ReparsePoint) != 0;
        bool isReadOnly = !isDir && (info.Attributes & FileAttributes.ReadOnly) != 0;

        return new FileEntry(
            info.Name,
            info.FullName,
            isDir,
            size,
            info.LastWriteTime,
            info.CreationTime,
            isHidden,
            isSymlink,
            isReadOnly,
            isDir ? null : info.Extension);
    }

    private async Task CopyDirectoryAsync(string source, string dest, bool overwrite,
        IProgress<(long bytes, long total)>? progress, CancellationToken ct)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source))
        {
            ct.ThrowIfCancellationRequested();
            var destFile = Path.Combine(dest, Path.GetFileName(file));
            await CopyAsync(file, destFile, overwrite, progress, ct);
        }
        foreach (var dir in Directory.GetDirectories(source))
        {
            ct.ThrowIfCancellationRequested();
            var destDir = Path.Combine(dest, Path.GetFileName(dir));
            await CopyDirectoryAsync(dir, destDir, overwrite, progress, ct);
        }
    }
}
