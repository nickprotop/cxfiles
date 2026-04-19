using CXFiles.Services;

namespace CXFiles.Tests;

public class FileSystemServiceMoveTests : IDisposable
{
    private readonly IFileSystemService _fs = new FileSystemService();
    private readonly string _scratch;

    public FileSystemServiceMoveTests()
    {
        _scratch = Path.Combine(Path.GetTempPath(), $"cxfiles-move-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_scratch);
    }

    public void Dispose()
    {
        try { Directory.Delete(_scratch, recursive: true); } catch { }
    }

    [Fact]
    public async Task MoveAsync_SameMount_RenamesFile()
    {
        var src = Path.Combine(_scratch, "a.txt");
        var dst = Path.Combine(_scratch, "b.txt");
        await File.WriteAllTextAsync(src, "hello");

        await _fs.MoveAsync(src, dst, overwrite: false, progress: null, CancellationToken.None);

        Assert.False(File.Exists(src));
        Assert.True(File.Exists(dst));
        Assert.Equal("hello", await File.ReadAllTextAsync(dst));
    }

    [Fact]
    public async Task MoveAsync_AlreadyCancelled_ThrowsBeforeTouchingDisk()
    {
        var src = Path.Combine(_scratch, "a.txt");
        var dst = Path.Combine(_scratch, "b.txt");
        await File.WriteAllTextAsync(src, "hello");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _fs.MoveAsync(src, dst, overwrite: false, progress: null, cts.Token));

        Assert.True(File.Exists(src));
        Assert.False(File.Exists(dst));
    }

    [Fact]
    public async Task CopyAsync_CancelledMidCopy_PartialDestRemains_ButSourceUntouched()
    {
        // Use a large-ish file so we can cancel during streaming.
        var src = Path.Combine(_scratch, "big.bin");
        var dst = Path.Combine(_scratch, "big-copy.bin");
        var bytes = new byte[8 * 1024 * 1024];
        new Random(42).NextBytes(bytes);
        await File.WriteAllBytesAsync(src, bytes);

        using var cts = new CancellationTokenSource();
        long reported = 0;
        var progress = new TestProgress<(long bytes, long total)>(p =>
        {
            reported = p.bytes;
            if (p.bytes > 64 * 1024) cts.Cancel();
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _fs.CopyAsync(src, dst, overwrite: false, progress, cts.Token));

        Assert.True(File.Exists(src));
        Assert.True(reported > 0);
    }

    [Fact]
    public async Task CopyAsync_ReportsByteProgress()
    {
        var src = Path.Combine(_scratch, "a.bin");
        var dst = Path.Combine(_scratch, "b.bin");
        var bytes = new byte[200_000];
        new Random(7).NextBytes(bytes);
        await File.WriteAllBytesAsync(src, bytes);

        var samples = new List<(long b, long t)>();
        var progress = new TestProgress<(long b, long t)>(p => samples.Add(p));

        await _fs.CopyAsync(src, dst, overwrite: false, progress, CancellationToken.None);

        Assert.NotEmpty(samples);
        Assert.Equal(bytes.Length, samples[^1].b);
        Assert.Equal(bytes.Length, samples[^1].t);
    }

    private sealed class TestProgress<T> : IProgress<T>
    {
        private readonly Action<T> _h;
        public TestProgress(Action<T> h) { _h = h; }
        public void Report(T v) => _h(v);
    }
}
