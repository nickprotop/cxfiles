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

        var services = new ServiceCollection();

        // Window system
        var driver = new NetConsoleDriver(RenderMode.Buffer);
        var ws = new ConsoleWindowSystem(driver,
            options: new ConsoleWindowSystemOptions(
                ShowTopPanel: false,
                ShowBottomPanel: false));
        services.AddSingleton(ws);

        // Services
        services.AddSingleton<IFileSystemService, FileSystemService>();
        services.AddSingleton<IConfigService, ConfigService>();

        var provider = services.BuildServiceProvider();

        // Run
        var app = new CXFilesApp(
            provider.GetRequiredService<ConsoleWindowSystem>(),
            provider.GetRequiredService<IFileSystemService>(),
            provider.GetRequiredService<IConfigService>());

        app.Run();

        // Save config on exit
        provider.GetRequiredService<IConfigService>().Save();

        return 0;
    }
}
