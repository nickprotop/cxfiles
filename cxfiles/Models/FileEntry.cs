namespace CXFiles.Models;

public record FileEntry(
    string Name,
    string FullPath,
    bool IsDirectory,
    long Size,
    DateTime Modified,
    DateTime Created,
    DateTime Accessed,
    bool IsHidden,
    bool IsSymlink,
    bool IsReadOnly,
    string? Extension)
{
    public string DisplaySize => IsDirectory ? "" : FormatSize(Size);

    public string DisplayDate => Modified.ToString("yyyy-MM-dd HH:mm");

    public string TypeDescription => IsDirectory ? "Folder" :
        string.IsNullOrEmpty(Extension) ? "File" :
        Extension.ToUpperInvariant().TrimStart('.') + " File";

    public string Icon => IsDirectory ? (IsSymlink ? "↗" : "▸") :
        IsSymlink ? "↗" :
        Extension?.ToLowerInvariant() switch
        {
            ".exe" or ".sh" or ".bat" or ".cmd" => "◈",
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or ".svg" => "◪",
            ".zip" or ".tar" or ".gz" or ".7z" or ".rar" or ".bz2" => "⊞",
            ".cs" or ".js" or ".ts" or ".py" or ".rs" or ".go" or ".java" or ".cpp" or ".c" or ".h"
                or ".md" or ".txt" or ".json" or ".xml" or ".yaml" or ".yml" or ".toml"
                or ".html" or ".css" or ".sql" or ".sh" or ".bash" => "≡",
            _ => "◦"
        };

    internal static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes}B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1}K",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1}M",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F1}G"
    };
}
