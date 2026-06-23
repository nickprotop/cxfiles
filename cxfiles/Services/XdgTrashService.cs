using System.Globalization;
using System.Runtime.InteropServices;
using CXFiles.Models;

namespace CXFiles.Services;

public class XdgTrashService : ITrashService
{
    private readonly string _trashDir;
    private readonly string _filesDir;
    private readonly string _infoDir;

    public bool IsAvailable { get; }
    public string TrashPath => _trashDir;

    public XdgTrashService()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            _trashDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".Trash");
        }
        else
        {
            var xdgData = Environment.GetEnvironmentVariable("XDG_DATA_HOME")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".local", "share");
            _trashDir = Path.Combine(xdgData, "Trash");
        }

        _filesDir = Path.Combine(_trashDir, "files");
        _infoDir = Path.Combine(_trashDir, "info");

        try
        {
            Directory.CreateDirectory(_filesDir);
            Directory.CreateDirectory(_infoDir);
            IsAvailable = true;
        }
        catch
        {
            IsAvailable = false;
        }
    }

    public Task TrashAsync(string path, CancellationToken ct)
        => TrashWithMoverAsync(path, (src, dest, _) =>
        {
            if (Directory.Exists(src))
                Directory.Move(src, dest);
            else if (File.Exists(src))
                File.Move(src, dest);
            else
                throw new FileNotFoundException("File not found", src);
            return Task.CompletedTask;
        }, ct);

    public async Task TrashWithMoverAsync(
        string path, Func<string, string, CancellationToken, Task> mover, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var name = Path.GetFileName(path);
        var trashedName = GetUniqueName(name);

        var destPath = Path.Combine(_filesDir, trashedName);
        var infoPath = Path.Combine(_infoDir, trashedName + ".trashinfo");

        // Capture the original path before the move; afterwards it no longer exists.
        var originalFullPath = Path.GetFullPath(path);

        await mover(path, destPath, ct);

        var infoContent = $"""
            [Trash Info]
            Path={Uri.EscapeDataString(originalFullPath)}
            DeletionDate={DateTime.Now:yyyy-MM-ddTHH:mm:ss}
            """;
        File.WriteAllText(infoPath, infoContent);
    }

    public Task RestoreAsync(string trashedName, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var infoPath = Path.Combine(_infoDir, trashedName + ".trashinfo");
        if (!File.Exists(infoPath))
            throw new FileNotFoundException("Trash info not found", infoPath);

        var originalPath = ParseOriginalPath(infoPath);
        if (originalPath == null)
            throw new InvalidOperationException("Cannot determine original path");

        var sourcePath = Path.Combine(_filesDir, trashedName);

        var destDir = Path.GetDirectoryName(originalPath);
        if (destDir != null && !Directory.Exists(destDir))
            Directory.CreateDirectory(destDir);

        if (Directory.Exists(sourcePath))
            Directory.Move(sourcePath, originalPath);
        else if (File.Exists(sourcePath))
            File.Move(sourcePath, originalPath);

        File.Delete(infoPath);

        return Task.CompletedTask;
    }

    public Task EmptyTrashAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        bool permissionDenied = false;

        // Delete each item together with its trashinfo, so items that can't be
        // removed (e.g. root-owned) stay listed instead of becoming orphans.
        foreach (var entry in Directory.EnumerateFileSystemEntries(_filesDir))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (Directory.Exists(entry))
                    Directory.Delete(entry, true);
                else
                    File.Delete(entry);

                var info = Path.Combine(_infoDir, Path.GetFileName(entry) + ".trashinfo");
                try { File.Delete(info); } catch { }
            }
            catch (UnauthorizedAccessException) { permissionDenied = true; }
            catch { }
        }

        if (permissionDenied)
            throw new UnauthorizedAccessException(
                "Some trash items require elevated privileges to delete.");

        return Task.CompletedTask;
    }

    public IReadOnlyList<TrashEntry> ListTrash()
    {
        var entries = new List<TrashEntry>();
        if (!Directory.Exists(_infoDir)) return entries;

        foreach (var infoFile in Directory.EnumerateFiles(_infoDir, "*.trashinfo"))
        {
            try
            {
                var trashedName = Path.GetFileNameWithoutExtension(infoFile);
                var filePath = Path.Combine(_filesDir, trashedName);

                var originalPath = ParseOriginalPath(infoFile) ?? trashedName;
                var deletionDate = ParseDeletionDate(infoFile);

                bool isDir = Directory.Exists(filePath);
                long size = 0;
                if (!isDir && File.Exists(filePath))
                    size = new FileInfo(filePath).Length;

                entries.Add(new TrashEntry(originalPath, trashedName, deletionDate, size, isDir));
            }
            catch { }
        }

        return entries.OrderByDescending(e => e.DeletionDate).ToList();
    }

    public int TrashCount
    {
        get
        {
            try
            {
                return Directory.Exists(_infoDir)
                    ? Directory.EnumerateFiles(_infoDir, "*.trashinfo").Count()
                    : 0;
            }
            catch { return 0; }
        }
    }

    private string GetUniqueName(string name)
    {
        if (!File.Exists(Path.Combine(_filesDir, name)) &&
            !Directory.Exists(Path.Combine(_filesDir, name)))
            return name;

        var baseName = Path.GetFileNameWithoutExtension(name);
        var ext = Path.GetExtension(name);
        int counter = 2;
        while (true)
        {
            var candidate = $"{baseName}.{counter}{ext}";
            if (!File.Exists(Path.Combine(_filesDir, candidate)) &&
                !Directory.Exists(Path.Combine(_filesDir, candidate)))
                return candidate;
            counter++;
        }
    }

    private static string? ParseOriginalPath(string infoPath)
    {
        foreach (var line in File.ReadLines(infoPath))
        {
            if (line.StartsWith("Path=", StringComparison.Ordinal))
                return Uri.UnescapeDataString(line[5..]);
        }
        return null;
    }

    private static DateTime ParseDeletionDate(string infoPath)
    {
        foreach (var line in File.ReadLines(infoPath))
        {
            if (line.StartsWith("DeletionDate=", StringComparison.Ordinal))
            {
                if (DateTime.TryParse(line[13..], CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date))
                    return date;
            }
        }
        return DateTime.MinValue;
    }
}
