using CXFiles.Services;

namespace CXFiles.Tests;

public class DebouncedWatcherTests : IDisposable
{
    private readonly IFileSystemService _fs = new FileSystemService();
    private readonly string _scratch;

    public DebouncedWatcherTests()
    {
        _scratch = Path.Combine(Path.GetTempPath(), $"cxfiles-watch-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_scratch);
    }

    public void Dispose()
    {
        try { Directory.Delete(_scratch, recursive: true); } catch { }
    }

    [Fact]
    public async Task RapidWrites_CoalescedIntoFewCallbacks()
    {
        int fires = 0;
        using var watcher = _fs.WatchDirectory(_scratch, _ => Interlocked.Increment(ref fires));

        var target = Path.Combine(_scratch, "stream.bin");
        // Simulate the NFS pattern: many small appends triggering Size/LastWrite events.
        using (var fs = File.Create(target))
        {
            var chunk = new byte[16 * 1024];
            for (int i = 0; i < 40; i++)
            {
                await fs.WriteAsync(chunk);
                await fs.FlushAsync();
                await Task.Delay(5);
            }
        }

        // Wait past the debounce window (400ms) + headroom.
        await Task.Delay(1000);

        // The debounce guarantees at most a handful of callbacks even though dozens of
        // kernel events fired. Allowing some slack for platform variation.
        Assert.InRange(fires, 1, 5);
    }

    [Fact]
    public async Task Pause_SuppressesCallbacks()
    {
        int fires = 0;
        using var watcher = _fs.WatchDirectory(_scratch, _ => Interlocked.Increment(ref fires));

        watcher.Pause();
        var target = Path.Combine(_scratch, "a.txt");
        await File.WriteAllTextAsync(target, "hi");
        await Task.Delay(1000);

        Assert.Equal(0, fires);
    }

    [Fact]
    public async Task Resume_AllowsNewCallbacks()
    {
        int fires = 0;
        using var watcher = _fs.WatchDirectory(_scratch, _ => Interlocked.Increment(ref fires));

        watcher.Pause();
        await File.WriteAllTextAsync(Path.Combine(_scratch, "a.txt"), "hi");
        await Task.Delay(600);
        Assert.Equal(0, fires);

        watcher.Resume();
        await File.WriteAllTextAsync(Path.Combine(_scratch, "b.txt"), "hi");
        await Task.Delay(1000);

        Assert.True(fires >= 1);
    }
}
