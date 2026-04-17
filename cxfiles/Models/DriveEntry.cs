namespace CXFiles.Models;

public record DriveEntry(
    string Name,
    string RootPath,
    string Label,
    long TotalSize,
    long FreeSpace,
    string DriveType)
{
    public string DisplayFree => FormatSize(FreeSpace);
    public string DisplayTotal => FormatSize(TotalSize);

    public string Icon => DriveType switch
    {
        "Network" => "🌐",
        "CDRom" => "💿",
        "Removable" => "🔌",
        _ => "💾"
    };

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes}B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1}K",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1}M",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F1}G"
    };
}
