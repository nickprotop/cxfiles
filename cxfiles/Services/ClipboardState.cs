namespace CXFiles.Services;

public enum ClipboardAction { Copy, Cut }

public class ClipboardState
{
    public List<string> Paths { get; set; } = new();
    public ClipboardAction Action { get; set; }
    public bool HasContent => Paths.Count > 0;

    public void SetCopy(IEnumerable<string> paths)
    {
        Paths = paths.ToList();
        Action = ClipboardAction.Copy;
    }

    public void SetCut(IEnumerable<string> paths)
    {
        Paths = paths.ToList();
        Action = ClipboardAction.Cut;
    }

    public void Clear() => Paths.Clear();
}
