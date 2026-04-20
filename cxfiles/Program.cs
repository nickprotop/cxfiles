using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using CXFiles.App;
using CXFiles.Services;

namespace CXFiles;

class Program
{
    static int Main(string[] args)
    {
        SharpConsoleUI.PtyShim.RunIfShim(args);

        try
        {
            if (Console.WindowWidth <= 0 || Console.WindowHeight <= 0)
            {
                Console.Error.WriteLine("cxfiles requires an interactive terminal.");
                return 1;
            }
        }
        catch
        {
            Console.Error.WriteLine("cxfiles requires an interactive terminal.");
            return 1;
        }

        string? startupPath = null;
        if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
        {
            var raw = args[0];
            if (raw == "~" || raw.StartsWith("~/") || raw.StartsWith("~\\"))
                raw = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                    + raw.Substring(1);
            try
            {
                startupPath = Path.GetFullPath(raw);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"cxfiles: invalid path '{args[0]}': {ex.Message}");
                return 1;
            }
            if (!Directory.Exists(startupPath))
            {
                Console.Error.WriteLine($"cxfiles: not a directory: {startupPath}");
                return 1;
            }
        }

        var services = new ServiceCollection();

        // Window system
        var driver = new NetConsoleDriver(RenderMode.Buffer);
        var ws = new ConsoleWindowSystem(driver,
            options: new ConsoleWindowSystemOptions(
                ShowTopPanel: false,
                ShowBottomPanel: false,
                WindowCycleKey: null));
        services.AddSingleton(ws);

        // Services
        services.AddSingleton<IFileSystemService, FileSystemService>();
        services.AddSingleton<IConfigService, ConfigService>();
        services.AddSingleton<OperationManager>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            services.AddSingleton<ITrashService, WindowsTrashService>();
        else
            services.AddSingleton<ITrashService, XdgTrashService>();
        services.AddSingleton<SudoService>();
        services.AddSingleton<LauncherService>();

        var provider = services.BuildServiceProvider();

        // Run
        var app = new CXFilesApp(
            provider.GetRequiredService<ConsoleWindowSystem>(),
            provider.GetRequiredService<IFileSystemService>(),
            provider.GetRequiredService<IConfigService>(),
            provider.GetRequiredService<OperationManager>(),
            provider.GetRequiredService<ITrashService>(),
            provider.GetRequiredService<SudoService>(),
            provider.GetRequiredService<LauncherService>(),
            startupPath);

        app.Run();

        // Save config on exit
        provider.GetRequiredService<IConfigService>().Save();

        return 0;
    }
}
