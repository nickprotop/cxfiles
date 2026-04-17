using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using CXFiles.Services;

namespace CXFiles.UI.Components;

public class FolderTreePanel
{
    private readonly IFileSystemService _fs;
    private readonly TreeControl _tree;
    private const string PlaceholderTag = "__placeholder__";

    private bool _showHidden;
    private bool _navigatingFromTree;

    public TreeControl Control => _tree;
    public bool NavigatingFromTree => _navigatingFromTree;
    public event Action<string>? FolderSelected;
    public event Action<string, MouseEventArgs>? FolderRightClicked;

    public FolderTreePanel(IFileSystemService fs, bool showHidden = false)
    {
        _fs = fs;
        _showHidden = showHidden;
        _tree = Controls.Tree()
            .WithGuide(TreeGuide.Line)
            .WithHighlightColors(Color.White, new Color(40, 60, 100))
            .WithBackgroundColor(Color.Transparent)
            .WithScrollbarVisibility(ScrollbarVisibility.Auto)
            .Build();
        _tree.VerticalAlignment = VerticalAlignment.Fill;
        _tree.HoverEnabled = false;

        _tree.SelectedNodeChanged += (_, e) =>
        {
            if (e?.Node?.Tag is string path && path != PlaceholderTag && _fs.DirectoryExists(path))
            {
                _navigatingFromTree = true;
                try { FolderSelected?.Invoke(path); }
                finally { _navigatingFromTree = false; }
            }
        };

        _tree.NodeExpandCollapse += (_, e) =>
        {
            if (e?.Node == null) return;
            if (e.Node.IsExpanded)
                LazyLoadChildren(e.Node);
        };

        _tree.MouseRightClick += (_, args) =>
        {
            var node = _tree.LastRightClickedNode;
            if (node?.Tag is string path)
                FolderRightClicked?.Invoke(path, args);
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
            node.IsExpanded = false;
            AddPlaceholderIfNeeded(node, drive.RootPath);
        }
    }

    public void ExpandToPath(string targetPath, bool expandTarget = false)
    {
        var normalized = Path.GetFullPath(targetPath);
        var segments = BuildPathSegments(normalized);

        TreeNode? current = null;
        foreach (var segment in segments)
        {
            var nodes = current == null ? _tree.RootNodes : current.Children;
            TreeNode? match = null;
            foreach (var node in nodes)
            {
                if (node.Tag is string nodePath &&
                    string.Equals(Path.GetFullPath(nodePath), segment, StringComparison.OrdinalIgnoreCase))
                {
                    match = node;
                    break;
                }
            }

            if (match == null) break;

            bool isLastSegment = segment == segments[^1];
            if (!isLastSegment || expandTarget)
            {
                if (!match.IsExpanded)
                {
                    match.IsExpanded = true;
                    LazyLoadChildren(match);
                }
            }
            current = match;
        }

        if (current != null)
            _tree.SelectNode(current);
    }

    public void RefreshNode(TreeNode? node = null)
    {
        if (node == null)
        {
            // Refresh all roots
            var expandedPaths = CollectExpandedPaths();
            LoadRoots();
            RestoreExpansion(expandedPaths);
            return;
        }

        if (node.Tag is not string path) return;

        var wasExpanded = node.IsExpanded;
        var expandedChildren = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (wasExpanded)
            CollectExpandedPaths(node, expandedChildren);

        node.ClearChildren();
        AddPlaceholderIfNeeded(node, path);

        if (wasExpanded)
        {
            LazyLoadChildren(node);
            RestoreExpansion(node, expandedChildren);
        }
    }

    public void SetShowHidden(bool show)
    {
        if (_showHidden == show) return;
        _showHidden = show;
        Refresh();
    }

    public void Refresh()
    {
        var selectedPath = _tree.SelectedNode?.Tag as string;
        var expandedPaths = CollectExpandedPaths();
        LoadRoots();
        RestoreExpansion(expandedPaths);
        if (selectedPath != null)
            ExpandToPath(selectedPath);
    }

    private void LazyLoadChildren(TreeNode node)
    {
        if (node.Tag is not string path) return;

        // Remove placeholder if present
        if (node.Children.Count == 1 && node.Children[0].Tag is string t && t == PlaceholderTag)
            node.RemoveChild(node.Children[0]);

        // Already loaded real children
        if (node.Children.Count > 0) return;

        try
        {
            var dirs = _fs.ListDirectory(path)
                .Where(e => e.IsDirectory && (_showHidden || !e.IsHidden))
                .OrderBy(e => e.Name)
                .Take(500);

            foreach (var dir in dirs)
            {
                var child = node.AddChild($"[cyan]{SharpConsoleUI.Parsing.MarkupParser.Escape(dir.Name)}[/]");
                child.Tag = dir.FullPath;
                child.IsExpanded = false;
                AddPlaceholderIfNeeded(child, dir.FullPath);
            }
        }
        catch { }
    }

    private void AddPlaceholderIfNeeded(TreeNode node, string path)
    {
        if (HasSubdirectories(path))
        {
            var placeholder = node.AddChild("[dim]…[/]");
            placeholder.Tag = PlaceholderTag;
        }
    }

    private bool HasSubdirectories(string path)
    {
        try { return Directory.EnumerateDirectories(path).Any(); }
        catch { return false; }
    }

    private static List<string> BuildPathSegments(string fullPath)
    {
        var segments = new List<string>();
        var root = Path.GetPathRoot(fullPath);
        if (root == null) return segments;

        segments.Add(Path.GetFullPath(root));
        var relative = Path.GetRelativePath(root, fullPath);
        if (relative == ".") return segments;

        var accumulated = root;
        foreach (var part in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            accumulated = Path.Combine(accumulated, part);
            segments.Add(Path.GetFullPath(accumulated));
        }
        return segments;
    }

    private HashSet<string> CollectExpandedPaths()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in _tree.RootNodes)
            CollectExpandedPaths(root, paths);
        return paths;
    }

    private void CollectExpandedPaths(TreeNode node, HashSet<string> paths)
    {
        if (node.IsExpanded && node.Tag is string path)
        {
            paths.Add(path);
            foreach (var child in node.Children)
                CollectExpandedPaths(child, paths);
        }
    }

    private void RestoreExpansion(HashSet<string> expandedPaths)
    {
        foreach (var root in _tree.RootNodes)
            RestoreExpansion(root, expandedPaths);
    }

    private void RestoreExpansion(TreeNode node, HashSet<string> expandedPaths)
    {
        if (node.Tag is string path && expandedPaths.Contains(path))
        {
            node.IsExpanded = true;
            LazyLoadChildren(node);
            foreach (var child in node.Children)
                RestoreExpansion(child, expandedPaths);
        }
    }
}
