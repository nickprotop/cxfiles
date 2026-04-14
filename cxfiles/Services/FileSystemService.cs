using CXFiles.Models;

namespace CXFiles.Services;

public class FileSystemService : IFileSystemService
{
    public IReadOnlyList<DriveEntry> GetDrives()
    {
        var result = new List<DriveEntry>();
        foreach (var d in DriveInfo.GetDrives())
        {
            if (!d.IsReady) continue;
            try
            {
                result.Add(new DriveEntry(
                    d.Name,
                    d.RootDirectory.FullName,
                    d.VolumeLabel,
                    d.TotalSize,
                    d.AvailableFreeSpace,
                    d.DriveType.ToString()));
            }
            catch { }
        }
        return result;
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

    public void EnumerateDirectorySize(string path,
        IProgress<DirectorySizeProgress> progress,
        CancellationToken ct)
    {
        long bytes = 0;
        long files = 0;
        long inaccessible = 0;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        long lastReportMs = 0;
        const long reportIntervalMs = 150;

        // Key "" = files directly under the scan root.
        // Other keys = names of immediate child directories of the scan root.
        var childBytes = new Dictionary<string, long>(StringComparer.Ordinal) { [""] = 0 };

        // Foreign-mount prefixes under this scan root — never descended into.
        var foreignMounts = GetForeignMountsUnder(path);

        // Each stack frame carries the top-level-child key its files belong to.
        var stack = new Stack<(string Dir, string Key)>();
        stack.Push((path, ""));

        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var (dir, parentKey) = stack.Pop();

            IEnumerable<string>? subdirs = null;
            try { subdirs = Directory.EnumerateDirectories(dir); }
            catch (UnauthorizedAccessException) { inaccessible++; }
            catch (DirectoryNotFoundException) { inaccessible++; }
            catch (IOException) { inaccessible++; }

            if (subdirs != null)
            {
                IEnumerator<string>? it = null;
                try { it = subdirs.GetEnumerator(); } catch { inaccessible++; }
                if (it != null)
                {
                    using (it)
                    {
                        while (true)
                        {
                            ct.ThrowIfCancellationRequested();
                            bool moved;
                            try { moved = it.MoveNext(); }
                            catch (UnauthorizedAccessException) { inaccessible++; break; }
                            catch (IOException) { inaccessible++; break; }
                            if (!moved) break;

                            var sub = it.Current;

                            // Cross-filesystem guard.
                            if (IsUnderForeignMount(sub, foreignMounts)) continue;

                            // When descending from the scan root, establish the top-level key.
                            string childKey;
                            if (dir == path)
                            {
                                childKey = Path.GetFileName(sub);
                                if (string.IsNullOrEmpty(childKey)) childKey = sub;
                                if (!childBytes.ContainsKey(childKey))
                                    childBytes[childKey] = 0;
                            }
                            else
                            {
                                childKey = parentKey;
                            }

                            stack.Push((sub, childKey));
                        }
                    }
                }
            }

            IEnumerable<string>? fileList = null;
            try { fileList = Directory.EnumerateFiles(dir); }
            catch (UnauthorizedAccessException) { inaccessible++; }
            catch (DirectoryNotFoundException) { inaccessible++; }
            catch (IOException) { inaccessible++; }

            if (fileList != null)
            {
                IEnumerator<string>? fit = null;
                try { fit = fileList.GetEnumerator(); } catch { inaccessible++; }
                if (fit != null)
                {
                    using (fit)
                    {
                        while (true)
                        {
                            ct.ThrowIfCancellationRequested();
                            bool moved;
                            try { moved = fit.MoveNext(); }
                            catch (UnauthorizedAccessException) { inaccessible++; break; }
                            catch (IOException) { inaccessible++; break; }
                            if (!moved) break;

                            try
                            {
                                long len = new FileInfo(fit.Current).Length;
                                bytes += len;
                                files++;
                                childBytes[parentKey] = childBytes.TryGetValue(parentKey, out var cur) ? cur + len : len;
                            }
                            catch (UnauthorizedAccessException) { inaccessible++; }
                            catch (FileNotFoundException) { /* race: file deleted */ }
                            catch (IOException) { inaccessible++; }

                            var nowMs = sw.ElapsedMilliseconds;
                            if (nowMs - lastReportMs >= reportIntervalMs)
                            {
                                lastReportMs = nowMs;
                                progress.Report(new DirectorySizeProgress(
                                    bytes, files, inaccessible, false,
                                    new Dictionary<string, long>(childBytes, StringComparer.Ordinal)));
                            }
                        }
                    }
                }
            }
        }

        progress.Report(new DirectorySizeProgress(
            bytes, files, inaccessible, true,
            new Dictionary<string, long>(childBytes, StringComparer.Ordinal)));
    }

    private static string[] GetForeignMountsUnder(string scanRoot)
    {
        try
        {
            // The deepest mount whose root is a prefix of scanRoot is the scan's own filesystem.
            string normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(scanRoot));
            string? ownRoot = null;
            int ownLen = -1;
            var drives = DriveInfo.GetDrives();
            foreach (var d in drives)
            {
                if (!d.IsReady) continue;
                var r = Path.TrimEndingDirectorySeparator(d.RootDirectory.FullName);
                if (normalized.StartsWith(r, StringComparison.Ordinal) && r.Length > ownLen)
                {
                    ownRoot = r;
                    ownLen = r.Length;
                }
            }

            var result = new List<string>();
            foreach (var d in drives)
            {
                if (!d.IsReady) continue;
                var r = Path.TrimEndingDirectorySeparator(d.RootDirectory.FullName);
                if (ownRoot != null && r == ownRoot) continue;
                if (r.Length > normalized.Length && r.StartsWith(normalized, StringComparison.Ordinal))
                    result.Add(r);
            }
            return result.ToArray();
        }
        catch { return Array.Empty<string>(); }
    }

    private static bool IsUnderForeignMount(string path, string[] foreignMounts)
    {
        if (foreignMounts.Length == 0) return false;
        string normalized = Path.TrimEndingDirectorySeparator(path);
        foreach (var m in foreignMounts)
        {
            if (normalized == m) return true;
            if (normalized.StartsWith(m, StringComparison.Ordinal) &&
                normalized.Length > m.Length &&
                (normalized[m.Length] == Path.DirectorySeparatorChar || normalized[m.Length] == Path.AltDirectorySeparatorChar))
                return true;
        }
        return false;
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
            info.LastAccessTime,
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
