namespace CXFiles.Services;

public class CXFilesConfig
{
    public bool ShowHiddenFiles { get; set; } = false;
    public bool ShowDetailPanel { get; set; } = true;
    public string DefaultPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public bool ConfirmDelete { get; set; } = true;
    public string SortColumn { get; set; } = "Name";
    public bool SortAscending { get; set; } = true;
    public int MaxTabs { get; set; } = 5;
    public bool SyncTreeToTab { get; set; } = true;
}

public interface IConfigService
{
    CXFilesConfig Config { get; }
    void Save();
    void Load();
}

public class ConfigService : IConfigService
{
    private readonly string _configPath;

    public CXFilesConfig Config { get; private set; } = new();

    public ConfigService()
    {
        var configDir = GetConfigDirectory();
        Directory.CreateDirectory(configDir);
        _configPath = Path.Combine(configDir, "config.json");
        Load();
    }

    public void Save()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(Config,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_configPath, json);
    }

    public void Load()
    {
        if (File.Exists(_configPath))
        {
            try
            {
                var json = File.ReadAllText(_configPath);
                Config = System.Text.Json.JsonSerializer.Deserialize<CXFilesConfig>(json) ?? new();
            }
            catch { Config = new(); }
        }
    }

    private static string GetConfigDirectory()
    {
        if (OperatingSystem.IsWindows())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "cxfiles");
        if (OperatingSystem.IsMacOS())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "cxfiles");
        // Linux: XDG_CONFIG_HOME or ~/.config
        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrEmpty(xdg))
            return Path.Combine(xdg, "cxfiles");
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "cxfiles");
    }
}
