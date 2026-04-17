using System.Diagnostics;

namespace CXFiles.Services;

public class LauncherService
{
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
                psi = new ProcessStartInfo(opener)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                psi.ArgumentList.Add(filePath);
            }
            Process.Start(psi);
        }
        catch { }
    }

}
