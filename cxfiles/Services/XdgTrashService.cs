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
    {
        ct.ThrowIfCancellationRequested();

        var name = Path.GetFileName(path);
        var trashedName = GetUniqueName(name);

        var destPath = Path.Combine(_filesDir, trashedName);
        var infoPath = Path.Combine(_infoDir, trashedName + ".trashinfo");

        if (Directory.Exists(path))
            Directory.Move(path, destPath);
        else if (File.Exists(path))
            File.Move(path, destPath);
        else
            throw new FileNotFoundException("File not found", path);

        var infoContent = $"""
            [Trash Info]
            Path={Uri.EscapeDataString(Path.GetFullPath(path))}
            DeletionDate={DateTime.Now:yyyy-MM-ddTHH:mm:ss}
            """;
        File.WriteAllText(infoPath, infoContent);

        return Task.CompletedTask;
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

        foreach (var file in Directory.EnumerateFiles(_filesDir))
        {
            ct.ThrowIfCancellationRequested();
            try { File.Delete(file); } catch { }
        }
        foreach (var dir in Directory.EnumerateDirectories(_filesDir))
        {
            ct.ThrowIfCancellationRequested();
            try { Directory.Delete(dir, true); } catch { }
        }
        foreach (var info in Directory.EnumerateFiles(_infoDir, "*.trashinfo"))
        {
            try { File.Delete(info); } catch { }
        }

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
