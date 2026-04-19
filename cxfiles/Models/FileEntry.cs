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
    // True when this directory is itself a mount point whose filesystem differs
    // from its parent (e.g. an NFS share mounted under ~/Downloads). Set by
    // FileSystemService.ListDirectory so the UI can flag it and avoid scanning it.
    public bool IsForeignMount { get; init; }

    // When IsForeignMount is true, the mount's filesystem kind (e.g. "nfs4",
    // "ext4", "cifs"), surfaced via DriveInfo.DriveFormat. Used for the file
    // list marker and the Properties "other filesystems" section.
    public string? MountFormat { get; init; }

    public string DisplaySize => IsDirectory ? "" : FormatSize(Size);

    public string DisplayDate => Modified.ToString("yyyy-MM-dd HH:mm");

    public string TypeDescription => IsForeignMount
        ? (string.IsNullOrEmpty(MountFormat) ? "Mount" : $"Mount ({MountFormat})")
        : IsDirectory ? "Folder"
        : string.IsNullOrEmpty(Extension) ? "File"
        : Extension.ToUpperInvariant().TrimStart('.') + " File";

    public string Icon => IsDirectory ? (IsForeignMount ? "⇆" : IsSymlink ? "↗" : "📁") :
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
