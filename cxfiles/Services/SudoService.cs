using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CXFiles.Services;

public class SudoService
{
    public bool IsSupported => !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public async Task<bool> AreCachedAsync(CancellationToken ct = default)
    {
        if (!IsSupported) return false;
        try
        {
            var psi = new ProcessStartInfo("sudo", "-n true")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var process = Process.Start(psi);
            if (process == null) return false;
            await process.WaitForExitAsync(ct);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<(bool Success, string? Error)> ExecuteAsync(
        string command, string? password, CancellationToken ct)
    {
        if (!IsSupported)
            return (false, "Sudo is not supported on Windows. Run as Administrator.");

        var args = password != null ? $"-S -- {command}" : $"-- {command}";
        var psi = new ProcessStartInfo("sudo", args)
        {
            RedirectStandardInput = password != null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            var process = Process.Start(psi);
            if (process == null)
                return (false, "Failed to start sudo process");

            if (password != null)
            {
                await process.StandardInput.WriteLineAsync(password.AsMemory(), ct);
                process.StandardInput.Close();
            }

            var stderr = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            if (process.ExitCode == 0)
                return (true, null);

            return (false, stderr.Trim());
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public Task<(bool Success, string? Error)> DeleteAsync(
        string path, bool isDirectory, string? password, CancellationToken ct)
    {
        var cmd = isDirectory ? $"rm -rf '{EscapePath(path)}'" : $"rm -f '{EscapePath(path)}'";
        return ExecuteAsync(cmd, password, ct);
    }

    public Task<(bool Success, string? Error)> MoveAsync(
        string source, string dest, string? password, CancellationToken ct)
    {
        return ExecuteAsync($"mv '{EscapePath(source)}' '{EscapePath(dest)}'", password, ct);
    }

    public Task<(bool Success, string? Error)> CopyAsync(
        string source, string dest, string? password, CancellationToken ct)
    {
        return ExecuteAsync($"cp -a '{EscapePath(source)}' '{EscapePath(dest)}'", password, ct);
    }

    private static string EscapePath(string path) => path.Replace("'", "'\\''");
}
