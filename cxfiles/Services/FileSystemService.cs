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
            if (!OperatingSystem.IsWindows() && !ShouldShowDrive(d)) continue;
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

    private static bool ShouldShowDrive(DriveInfo d)
    {
        var path = d.RootDirectory.FullName;

        if (path == "/") return true;
        if (path == "/home" || path == "/home/") return true;

        if (IsBlacklistedVirtualFs(path)) return false;
        if (path.StartsWith("/boot", StringComparison.Ordinal)) return false;

        if (path.StartsWith("/media/", StringComparison.Ordinal)) return true;
        if (path.StartsWith("/mnt/", StringComparison.Ordinal)) return true;

        if (d.DriveType == DriveType.Network) return true;
        if (d.DriveType == DriveType.Removable) return true;
        if (d.DriveType == DriveType.CDRom) return true;

        return false;
    }

    public IReadOnlyList<FileEntry> ListDirectory(string path)
    {
        var dir = new DirectoryInfo(path);
        if (!dir.Exists) return Array.Empty<FileEntry>();

        var entries = new List<FileEntry>();
        var mountIndex = BuildMountIndex(path);

        try
        {
            foreach (var d in dir.EnumerateDirectories())
            {
                try
                {
                    entries.Add(EnrichWithMount(CreateEntry(d), mountIndex));
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

    // Snapshot of DriveInfo keyed by normalized mount path, plus the parent
    // directory's own mount root. Used by ListDirectory to decide whether each
    // child directory (after resolving symlinks) lands on a different filesystem
    // than the parent — a common setup is `~/Downloads` being a symlink to an
    // NFS mount at `/mnt/...`, where the path itself is not a mount but its
    // target is.
    private sealed record MountIndex(string ParentMount, Dictionary<string, DriveInfo> ByRoot);

    private static MountIndex BuildMountIndex(string parentPath)
    {
        var byRoot = new Dictionary<string, DriveInfo>(StringComparer.Ordinal);
        foreach (var d in DriveInfo.GetDrives())
        {
            if (!d.IsReady) continue;
            try
            {
                var root = Path.TrimEndingDirectorySeparator(d.RootDirectory.FullName);
                if (!byRoot.ContainsKey(root)) byRoot[root] = d;
            }
            catch { }
        }
        var parentResolved = ResolveFullTarget(parentPath);
        var parentMount = FindMountRoot(parentResolved, byRoot.Keys);
        return new MountIndex(parentMount, byRoot);
    }

    private static FileEntry EnrichWithMount(FileEntry entry, MountIndex idx)
    {
        if (!entry.IsDirectory) return entry;
        var resolved = ResolveFullTarget(entry.FullPath);
        var mount = FindMountRoot(resolved, idx.ByRoot.Keys);
        if (!string.IsNullOrEmpty(mount) && !string.Equals(mount, idx.ParentMount, StringComparison.Ordinal))
        {
            string? format = null;
            if (idx.ByRoot.TryGetValue(mount, out var drive))
            {
                try { format = drive.DriveFormat; } catch { }
            }
            return entry with { IsForeignMount = true, MountFormat = format };
        }
        return entry;
    }

    // Follows a symlink chain to its final target (or returns the normalized path
    // if not a link). Wrapped in try/catch because broken/circular links throw.
    internal static string ResolveFullTarget(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                var di = new DirectoryInfo(path);
                if (di.LinkTarget != null)
                {
                    var target = di.ResolveLinkTarget(returnFinalTarget: true);
                    if (target != null) return Path.TrimEndingDirectorySeparator(target.FullName);
                }
                return Path.TrimEndingDirectorySeparator(di.FullName);
            }
            if (File.Exists(path))
            {
                var fi = new FileInfo(path);
                if (fi.LinkTarget != null)
                {
                    var target = fi.ResolveLinkTarget(returnFinalTarget: true);
                    if (target != null) return Path.TrimEndingDirectorySeparator(target.FullName);
                }
                return Path.TrimEndingDirectorySeparator(fi.FullName);
            }
        }
        catch { }
        try { return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)); }
        catch { return path; }
    }

    // Longest-prefix match of a resolved path against known mount roots.
    internal static string FindMountRoot(string resolvedPath, IEnumerable<string> mountRoots)
    {
        string best = "";
        int bestLen = -1;
        foreach (var root in mountRoots)
        {
            if (string.IsNullOrEmpty(root)) continue;
            if (resolvedPath.Length < root.Length) continue;
            if (!resolvedPath.StartsWith(root, StringComparison.Ordinal)) continue;
            if (resolvedPath.Length != root.Length &&
                resolvedPath[root.Length] != Path.DirectorySeparatorChar) continue;
            if (root.Length > bestLen) { bestLen = root.Length; best = root; }
        }
        return best;
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
            await CopyDirectoryAsync(source, dest, overwrite, progress, ct);
        else
            await CopyFileStreamingAsync(source, dest, overwrite, progress, ct);
    }

    public async Task MoveAsync(string source, string dest, bool overwrite,
        IProgress<(long bytes, long total)>? progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        bool isDir = Directory.Exists(source);

        // Fast path: same mount → rename (atomic, microseconds even for GB-sized trees).
        if (AreSameMount(source, dest))
        {
            if (isDir) Directory.Move(source, dest);
            else File.Move(source, dest, overwrite);
            return;
        }

        // Cross-mount: .NET's File.Move silently degrades to synchronous copy+delete,
        // blocking the thread with no cancellation and no progress. Do it ourselves
        // so paste stays cancellable and the UI stays responsive.
        try
        {
            if (isDir)
                await CopyDirectoryAsync(source, dest, overwrite, progress, ct);
            else
                await CopyFileStreamingAsync(source, dest, overwrite, progress, ct);
        }
        catch
        {
            TryDeletePath(dest);
            throw;
        }

        ct.ThrowIfCancellationRequested();

        if (isDir) Directory.Delete(source, recursive: true);
        else File.Delete(source);
    }

    private static async Task CopyFileStreamingAsync(string source, string dest, bool overwrite,
        IProgress<(long bytes, long total)>? progress, CancellationToken ct)
    {
        var fileInfo = new FileInfo(source);
        var total = fileInfo.Length;
        var mode = overwrite ? FileMode.Create : FileMode.CreateNew;
        using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var destStream = new FileStream(dest, mode, FileAccess.Write, FileShare.None,
            81920, FileOptions.Asynchronous);
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

    private static void TryDeletePath(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            else if (File.Exists(path)) File.Delete(path);
        }
        catch { }
    }

    internal static bool AreSameMount(string a, string b)
    {
        var ma = GetMountPoint(a);
        var mb = GetMountPoint(b);
        return !string.IsNullOrEmpty(ma) && ma == mb;
    }

    private static string GetMountPoint(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            var withSep = full.EndsWith(Path.DirectorySeparatorChar) ? full : full + Path.DirectorySeparatorChar;
            string best = "";
            int bestLen = -1;
            foreach (var d in DriveInfo.GetDrives())
            {
                if (!d.IsReady) continue;
                var root = Path.TrimEndingDirectorySeparator(d.Name);
                if (string.IsNullOrEmpty(root)) root = Path.DirectorySeparatorChar.ToString();
                var rootWithSep = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
                if (withSep.StartsWith(rootWithSep, StringComparison.Ordinal) && root.Length > bestLen)
                {
                    bestLen = root.Length;
                    best = root;
                }
            }
            return best;
        }
        catch { return ""; }
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

        // Snapshot the mount table once. A child dir is "foreign" when its resolved
        // target's mount root differs from the scan root's resolved mount — this
        // catches both literal mount subdirs and symlinks that hop filesystems
        // (e.g. ~/Downloads → /mnt/nick/Downloads on NFS).
        var mountRoots = new List<string>();
        foreach (var d in DriveInfo.GetDrives())
        {
            if (!d.IsReady) continue;
            try { mountRoots.Add(Path.TrimEndingDirectorySeparator(d.RootDirectory.FullName)); } catch { }
        }
        var ownMount = FindMountRoot(ResolveFullTarget(path), mountRoots);

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

                            // Cross-filesystem guard: skip if resolved target is on a
                            // different mount than the scan root.
                            if (!string.IsNullOrEmpty(ownMount))
                            {
                                var subMount = FindMountRoot(ResolveFullTarget(sub), mountRoots);
                                if (!string.IsNullOrEmpty(subMount) &&
                                    !string.Equals(subMount, ownMount, StringComparison.Ordinal))
                                    continue;
                            }

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

    public IDirectoryWatcher WatchDirectory(string path, Action<FileSystemChangeEvent> onChange)
        => new DebouncedDirectoryWatcher(path, onChange, debounceMs: 400);

    // Coalesces FileSystemWatcher events with a trailing-edge debounce. Large writes on
    // slow filesystems (NFS, USB) emit dozens of Changed events as Size/LastWrite tick;
    // without this, each one triggered a synchronous directory listing on the UI thread
    // and starved the main loop. Pause/Resume lets the caller suppress self-inflicted
    // events during a paste whose destination is the watched directory.
    private sealed class DebouncedDirectoryWatcher : IDirectoryWatcher
    {
        private readonly FileSystemWatcher _fsw;
        private readonly Action<FileSystemChangeEvent> _onChange;
        private readonly Timer _timer;
        private readonly int _debounceMs;
        private readonly object _lock = new();
        private FileSystemChangeEvent? _pending;
        private volatile bool _paused;
        private volatile bool _disposed;

        public DebouncedDirectoryWatcher(string path, Action<FileSystemChangeEvent> onChange, int debounceMs)
        {
            _onChange = onChange;
            _debounceMs = debounceMs;
            _timer = new Timer(Fire, null, Timeout.Infinite, Timeout.Infinite);

            _fsw = new FileSystemWatcher(path)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                               NotifyFilters.LastWrite | NotifyFilters.Size,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };
            _fsw.Created += (_, e) => Schedule(new FileSystemChangeEvent(e.FullPath, e.ChangeType));
            _fsw.Deleted += (_, e) => Schedule(new FileSystemChangeEvent(e.FullPath, e.ChangeType));
            _fsw.Changed += (_, e) => Schedule(new FileSystemChangeEvent(e.FullPath, e.ChangeType));
            _fsw.Renamed += (_, e) => Schedule(new FileSystemChangeEvent(e.FullPath, e.ChangeType));
        }

        private void Schedule(FileSystemChangeEvent ev)
        {
            if (_paused || _disposed) return;
            lock (_lock) _pending = ev;
            _timer.Change(_debounceMs, Timeout.Infinite);
        }

        private void Fire(object? _)
        {
            if (_paused || _disposed) return;
            FileSystemChangeEvent? ev;
            lock (_lock) { ev = _pending; _pending = null; }
            if (ev is null) return;
            try { _onChange(ev); } catch { }
        }

        public void Pause()
        {
            _paused = true;
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
            lock (_lock) _pending = null;
        }

        public void Resume() => _paused = false;

        public void Dispose()
        {
            _disposed = true;
            try { _fsw.EnableRaisingEvents = false; } catch { }
            _fsw.Dispose();
            _timer.Dispose();
        }
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
