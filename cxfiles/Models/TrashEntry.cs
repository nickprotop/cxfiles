namespace CXFiles.Models;

public record TrashEntry(
    string OriginalPath,
    string TrashedName,
    DateTime DeletionDate,
    long Size,
    bool IsDirectory);
