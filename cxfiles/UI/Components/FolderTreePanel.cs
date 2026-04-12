using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using CXFiles.Services;

namespace CXFiles.UI.Components;

public class FolderTreePanel
{
    private readonly IFileSystemService _fs;
    private readonly TreeControl _tree;

    public TreeControl Control => _tree;
    public event Action<string>? FolderSelected;

    public FolderTreePanel(IFileSystemService fs)
    {
        _fs = fs;
        _tree = Controls.Tree()
            .WithGuide(TreeGuide.Line)
            .WithHighlightColors(Color.White, new Color(40, 60, 100))
            .Build();
        _tree.VerticalAlignment = VerticalAlignment.Fill;

        _tree.NodeActivated += (_, e) =>
        {
            if (e.Node.Tag is string path && _fs.DirectoryExists(path))
                FolderSelected?.Invoke(path);
        };
    }

    public void LoadRoots()
    {
        _tree.Clear();
        var drives = _fs.GetDrives();
        foreach (var drive in drives)
        {
            var label = string.IsNullOrEmpty(drive.Label)
                ? drive.RootPath
                : $"{drive.Label} ({drive.RootPath})";
            var node = _tree.AddRootNode($"[cyan]{label}[/]");
            node.Tag = drive.RootPath;
            LoadChildren(node, drive.RootPath);
        }
    }

    private void LoadChildren(TreeNode parent, string path)
    {
        try
        {
            var dirs = _fs.ListDirectory(path)
                .Where(e => e.IsDirectory && !e.IsHidden)
                .OrderBy(e => e.Name)
                .Take(200);

            foreach (var dir in dirs)
            {
                var child = parent.AddChild($"[cyan]{dir.Name}[/]");
                child.Tag = dir.FullPath;
                if (HasSubdirectories(dir.FullPath))
                    child.AddChild("[dim]...[/]");
            }
        }
        catch { }
    }

    private bool HasSubdirectories(string path)
    {
        try { return Directory.EnumerateDirectories(path).Any(); }
        catch { return false; }
    }
}
