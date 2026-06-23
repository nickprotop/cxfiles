using CXFiles.Services;

namespace CXFiles.Tests;

public class XdgTrashServiceTests : IDisposable
{
    private readonly string _scratch;
    private readonly string? _prevXdg;

    public XdgTrashServiceTests()
    {
        _scratch = Path.Combine(Path.GetTempPath(), $"cxfiles-trash-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_scratch);
        _prevXdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", Path.Combine(_scratch, "xdg"));
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", _prevXdg);
        try { Directory.Delete(_scratch, recursive: true); } catch { }
    }

    [Fact]
    public async Task TrashWithMoverAsync_UsesProvidedMover_AndWritesInfo()
    {
        // The XDG trash layout (~/.local/share/Trash/{files,info}) is Linux-specific.
        if (OperatingSystem.IsMacOS() || OperatingSystem.IsWindows()) return;

        var trash = new XdgTrashService();
        Assert.True(trash.IsAvailable);

        var src = Path.Combine(_scratch, "victim.txt");
        await File.WriteAllTextAsync(src, "bye");

        var moverCalls = new List<(string Src, string Dest)>();
        await trash.TrashWithMoverAsync(src, (s, d, _) =>
        {
            moverCalls.Add((s, d));
            File.Move(s, d);
            return Task.CompletedTask;
        }, CancellationToken.None);

        // The injected mover performed the relocation, not the service itself.
        var call = Assert.Single(moverCalls);
        Assert.Equal(src, call.Src);
        Assert.Contains(Path.Combine("Trash", "files"), call.Dest);
        Assert.True(File.Exists(call.Dest));
        Assert.False(File.Exists(src));

        // The service still wrote the trashinfo metadata pointing at the original path.
        var entry = Assert.Single(trash.ListTrash());
        Assert.Equal(Path.GetFullPath(src), entry.OriginalPath);
    }

    [Fact]
    public async Task TrashAsync_DefaultMover_MovesFileIntoTrash()
    {
        if (OperatingSystem.IsMacOS() || OperatingSystem.IsWindows()) return;

        var trash = new XdgTrashService();
        var src = Path.Combine(_scratch, "note.txt");
        await File.WriteAllTextAsync(src, "hi");

        await trash.TrashAsync(src, CancellationToken.None);

        Assert.False(File.Exists(src));
        Assert.Equal(1, trash.TrashCount);
    }

    [Fact]
    public async Task EmptyTrashAsync_RemovesAllItems_WhenDeletable()
    {
        if (OperatingSystem.IsMacOS() || OperatingSystem.IsWindows()) return;

        var trash = new XdgTrashService();

        var file = Path.Combine(_scratch, "a.txt");
        await File.WriteAllTextAsync(file, "x");
        var dir = Path.Combine(_scratch, "sub");
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "inner.txt"), "y");

        await trash.TrashAsync(file, CancellationToken.None);
        await trash.TrashAsync(dir, CancellationToken.None);
        Assert.Equal(2, trash.TrashCount);

        await trash.EmptyTrashAsync(CancellationToken.None);

        Assert.Equal(0, trash.TrashCount);
        Assert.Empty(trash.ListTrash());
    }
}
