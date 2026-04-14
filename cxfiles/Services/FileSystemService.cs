using System.Runtime.CompilerServices;
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

    // Hard cap on entries inspected per search. Prevents a runaway walk over
    // /home or / from chewing CPU forever; the user is expected to refine the
    // query when the cap is reached.
    private const int MaxSearchScan = 200_000;

    // Well-known Linux virtual / pseudo filesystems and noisy mount points that
    // should never be recursed into during a search. DriveInfo.GetDrives() may
    // not always report all of them, so the explicit list is a safety net.
    private static readonly string[] VirtualFsRoots =
    {
        "/proc", "/sys", "/dev", "/run", "/snap",
        "/var/lib/docker", "/var/lib/containers", "/var/lib/lxcfs"
    };

    private static bool IsBlacklistedVirtualFs(string fullPath)
    {
        foreach (var v in VirtualFsRoots)
        {
            if (fullPath == v) return true;
            if (fullPath.Length > v.Length &&
                fullPath.StartsWith(v, StringComparison.Ordinal) &&
                (fullPath[v.Length] == '/' || fullPath[v.Length] == Path.DirectorySeparatorChar))
                return true;
        }
        return false;
    }

    public async IAsyncEnumerable<SearchHit> SearchAsync(
        string root,
        string query,
        bool recurse,
        bool showHidden,
        IProgress<SearchProgress>? progress,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (!Directory.Exists(root))
        {
            progress?.Report(new SearchProgress());
            yield break;
        }

        // Cross-filesystem boundaries to skip when recursing under `root`.
        // Reuses the same helper the directory-size scanner uses.
        var foreignMounts = recurse ? GetForeignMountsUnder(root) : Array.Empty<string>();

        int scanned = 0;
        int matches = 0;
        bool limitHit = false;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        long lastReportMs = 0;
        const long reportIntervalMs = 250;
        const int reportEveryN = 500;

        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0 && !limitHit)
        {
            ct.ThrowIfCancellationRequested();
            var dir = stack.Pop();

            IEnumerable<FileSystemInfo>? entries = null;
            try
            {
                entries = new DirectoryInfo(dir).EnumerateFileSystemInfos("*", new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = false
                });
            }
            catch (DirectoryNotFoundException) { }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
            if (entries == null) continue;

            IEnumerator<FileSystemInfo>? it = null;
            try { it = entries.GetEnumerator(); }
            catch (UnauthorizedAccessException) { continue; }
            catch (IOException) { continue; }
            if (it == null) continue;

            using (it)
            {
                while (true)
                {
                    ct.ThrowIfCancellationRequested();

                    bool moved;
                    FileSystemInfo? info = null;
                    try
                    {
                        moved = it.MoveNext();
                        if (moved) info = it.Current;
                    }
                    catch (UnauthorizedAccessException) { break; }
                    catch (IOException) { break; }

                    if (!moved || info == null) break;

                    scanned++;
                    if (scanned >= MaxSearchScan)
                    {
                        limitHit = true;
                        break;
                    }

                    bool isDir = (info.Attributes & FileAttributes.Directory) != 0;
                    bool isReparse = (info.Attributes & FileAttributes.ReparsePoint) != 0;
                    bool hidden = IsHiddenInfo(info);

                    // Match check (skip hidden if the user asked).
                    if (!(hidden && !showHidden) &&
                        !string.IsNullOrEmpty(query) &&
                        info.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                    {
                        matches++;
                        yield return new SearchHit(
                            info.Name,
                            info.FullName,
                            Path.GetRelativePath(root, info.FullName),
                            isDir);
                    }

                    // Decide whether to descend.
                    if (recurse && isDir)
                    {
                        if (isReparse) continue;                            // never follow symlinks (loops)
                        if (hidden && !showHidden) continue;                // skip hidden subtrees
                        var sub = info.FullName;
                        if (IsBlacklistedVirtualFs(sub)) continue;          // /proc, /sys, /dev, …
                        if (IsUnderForeignMount(sub, foreignMounts)) continue; // cross-filesystem boundary
                        stack.Push(sub);
                    }

                    var nowMs = sw.ElapsedMilliseconds;
                    if (nowMs - lastReportMs >= reportIntervalMs || scanned % reportEveryN == 0)
                    {
                        lastReportMs = nowMs;
                        progress?.Report(new SearchProgress
                        {
                            DirsScanned = scanned,
                            MatchesFound = matches,
                            LimitReached = limitHit
                        });
                        await Task.Yield();
                    }
                }
            }
        }

        progress?.Report(new SearchProgress
        {
            DirsScanned = scanned,
            MatchesFound = matches,
            LimitReached = limitHit
        });
    }

    public Task<FileEntry?> HydrateAsync(string fullPath, CancellationToken ct)
    {
        return Task.Run<FileEntry?>(() =>
        {
            try { return GetFileInfo(fullPath); }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            catch (IOException) { return null; }
        }, ct);
    }

    private static bool IsHiddenInfo(FileSystemInfo info) =>
        info.Name.StartsWith('.') ||
        (info.Attributes & FileAttributes.Hidden) != 0;

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
