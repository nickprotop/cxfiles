using System.Diagnostics;

namespace CXFiles.Services;

public class LauncherService
{
    private readonly IConfigService _config;

    public LauncherService(IConfigService config)
    {
        _config = config;
    }

    /// <summary>
    /// Opens a file with the OS default handler (xdg-open / open / start).
    /// </summary>
    public void OpenWithDefault(string filePath)
    {
        try
        {
            ProcessStartInfo psi;
            if (OperatingSystem.IsWindows())
            {
                psi = new ProcessStartInfo(filePath) { UseShellExecute = true };
            }
            else
            {
                // Launch via xdg-open/open explicitly so we can redirect stdout/stderr
                // and prevent the child process from polluting our TUI.
                var opener = OperatingSystem.IsMacOS() ? "open" : "xdg-open";
                psi = new ProcessStartInfo(opener, filePath)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
            }
            Process.Start(psi);
        }
        catch { }
    }

    /// <summary>
    /// Opens a file with the configured editor, falling back to $EDITOR, then system default.
    /// </summary>
    public void OpenWithEditor(string filePath)
    {
        var cmd = _config.Config.EditorCommand;

        if (string.IsNullOrWhiteSpace(cmd))
            cmd = Environment.GetEnvironmentVariable("EDITOR");

        if (string.IsNullOrWhiteSpace(cmd))
        {
            OpenWithDefault(filePath);
            return;
        }

        try
        {
            var psi = new ProcessStartInfo(cmd, filePath)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            Process.Start(psi);
        }
        catch { /* fallback to system default on failure */ OpenWithDefault(filePath); }
    }

    /// <summary>
    /// Opens an external terminal at the given directory.
    /// </summary>
    public void OpenTerminalExternal(string workingDirectory)
    {
        var cmd = _config.Config.ExternalTerminalCommand;

        if (string.IsNullOrWhiteSpace(cmd))
            cmd = DetectTerminal();

        if (cmd == null) return;

        try
        {
            var psi = new ProcessStartInfo(cmd)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            Process.Start(psi);
        }
        catch { }
    }

    private static string? DetectTerminal()
    {
        if (OperatingSystem.IsWindows())
            return "wt.exe";
        if (OperatingSystem.IsMacOS())
            return "open -a Terminal";
        // Linux: try common terminal emulators
        foreach (var t in new[] { "x-terminal-emulator", "gnome-terminal", "konsole", "xfce4-terminal", "xterm" })
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo("which", t)
                    { RedirectStandardOutput = true, UseShellExecute = false });
                p?.WaitForExit(500);
                if (p?.ExitCode == 0) return t;
            }
            catch { }
        }
        return null;
    }
}
