using System.Runtime.InteropServices;
using CXFiles.Models;

namespace CXFiles.Services;

public class WindowsTrashService : ITrashService
{
    public bool IsAvailable => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    public string TrashPath => "shell:RecycleBinFolder";

    public Task TrashAsync(string path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException();

        var fullPath = Path.GetFullPath(path);
        int result = NativeMethods.MoveToRecycleBin(fullPath);
        if (result != 0)
            throw new IOException($"Failed to move to Recycle Bin (error {result})");

        return Task.CompletedTask;
    }

    public Task RestoreAsync(string trashedName, CancellationToken ct) =>
        throw new NotSupportedException("Restore from Windows Recycle Bin is not supported");

    public Task EmptyTrashAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException();

        NativeMethods.SHEmptyRecycleBin(IntPtr.Zero, null, 0x07); // SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND
        return Task.CompletedTask;
    }

    public IReadOnlyList<TrashEntry> ListTrash() => Array.Empty<TrashEntry>();

    public int TrashCount => 0;

    private static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public uint wFunc;
            public string pFrom;
            public string pTo;
            public ushort fFlags;
            public int fAnyOperationsAborted;
            public IntPtr hNameMappings;
            public string lpszProgressTitle;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

        private const uint FO_DELETE = 0x0003;
        private const ushort FOF_ALLOWUNDO = 0x0040;
        private const ushort FOF_NOCONFIRMATION = 0x0010;
        private const ushort FOF_SILENT = 0x0004;
        private const ushort FOF_NOERRORUI = 0x0400;

        public static int MoveToRecycleBin(string path)
        {
            var op = new SHFILEOPSTRUCT
            {
                wFunc = FO_DELETE,
                pFrom = path + '\0' + '\0',
                fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI
            };
            return SHFileOperation(ref op);
        }
    }
}
