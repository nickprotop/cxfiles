using CXFiles.Models;

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
    public bool AutoSelectFirstItem { get; set; } = false;
    public string EditorCommand { get; set; } = "";
    public bool AllowSudoElevation { get; set; } = false;
    public int TreePanelWidth { get; set; } = 25;
    public int DetailPanelWidth { get; set; } = 30;
    public List<BookmarkEntry> Bookmarks { get; set; } = new();
}

public interface IConfigService
{
    CXFilesConfig Config { get; }
    void Save();
    void Load();

    void AddBookmark(string path, string? name = null);
    void RemoveBookmark(string path);
    void RenameBookmark(string path, string newName);
    event EventHandler? BookmarksChanged;
}

public class ConfigService : IConfigService
{
    private readonly string _configPath;

    public CXFilesConfig Config { get; private set; } = new();
    public event EventHandler? BookmarksChanged;

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
                Config.Bookmarks ??= new();
            }
            catch { Config = new(); }
        }
    }

    public void AddBookmark(string path, string? name = null)
    {
        if (Config.Bookmarks.Any(b => string.Equals(b.Path, path, StringComparison.Ordinal)))
            return;
        var leafName = string.IsNullOrWhiteSpace(name)
            ? (Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)) is string n && n.Length > 0 ? n : path)
            : name;
        Config.Bookmarks.Add(new BookmarkEntry(leafName, path));
        Save();
        BookmarksChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveBookmark(string path)
    {
        var idx = Config.Bookmarks.FindIndex(b => string.Equals(b.Path, path, StringComparison.Ordinal));
        if (idx < 0) return;
        Config.Bookmarks.RemoveAt(idx);
        Save();
        BookmarksChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RenameBookmark(string path, string newName)
    {
        var idx = Config.Bookmarks.FindIndex(b => string.Equals(b.Path, path, StringComparison.Ordinal));
        if (idx < 0) return;
        Config.Bookmarks[idx] = Config.Bookmarks[idx] with { Name = newName };
        Save();
        BookmarksChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string GetConfigDirectory()
    {
        if (OperatingSystem.IsWindows())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "cxfiles");
        if (OperatingSystem.IsMacOS())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "cxfiles");
        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrEmpty(xdg))
            return Path.Combine(xdg, "cxfiles");
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "cxfiles");
    }
}
