using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;
using CXFiles.Models;
using CXFiles.Services;
using CXFiles.UI.Components;

namespace CXFiles.App;

public partial class CXFilesApp
{
    private readonly ConsoleWindowSystem _ws;
    private readonly IFileSystemService _fs;
    private readonly IConfigService _config;
    private readonly OperationManager _operations;
    private Window? _mainWindow;

    // UI Components
    private BreadcrumbBar _breadcrumb = null!;
    private FolderTreePanel _folderTree = null!;
    private FileListPanel _fileList = null!;
    private DetailPanel _detailPanel = null!;
    private StatusLine _statusLine = null!;
    private ToolbarControl _toolbar = null!;
    private HorizontalGridControl? _mainGrid;
    private UI.ContextMenuBuilder _contextMenu = null!;
    private UI.OperationsPortal? _opsPortal;
    private LayoutNode? _opsPortalNode;

    // State
    private string _currentPath;
    private bool _detailVisible;
    private IDisposable? _fileWatcher;
    private readonly ClipboardState _clipboard = new();

    public CXFilesApp(ConsoleWindowSystem ws, IFileSystemService fs, IConfigService config, OperationManager operations)
    {
        _ws = ws;
        _fs = fs;
        _config = config;
        _operations = operations;
        _currentPath = config.Config.DefaultPath;
        _detailVisible = config.Config.ShowDetailPanel;
    }

    public void Run()
    {
        BuildUI();
        NavigateTo(_currentPath);
        _ws.AddWindow(_mainWindow!);
        _ws.Run();
    }

    public void NavigateTo(string path)
    {
        if (!_fs.DirectoryExists(path)) return;
        _currentPath = path;
        _breadcrumb.Update(path);
        _fileList.Navigate(path);
        _folderTree.ExpandToPath(path);
        UpdateStatusLine();
        UpdateToolbar();

        // Restart file watcher for the new directory
        _fileWatcher?.Dispose();
        try
        {
            _fileWatcher = _fs.WatchDirectory(path, _ => _ws.EnqueueOnUIThread(Refresh));
        }
        catch { /* watcher may fail on some filesystems */ }
    }

    private void NavigateUp()
    {
        var parent = Path.GetDirectoryName(_currentPath);
        if (!string.IsNullOrEmpty(parent))
            NavigateTo(parent);
    }

    private void OpenSelected()
    {
        var entry = _fileList.GetSelectedEntry();
        if (entry == null) return;

        if (entry.IsDirectory)
            NavigateTo(entry.FullPath);
    }

    private void ToggleDetailPanel()
    {
        _detailVisible = !_detailVisible;
        _detailPanel.Control.Visible = _detailVisible;

        // Also hide/show the right splitter (between file list and detail)
        if (_mainGrid != null)
        {
            var splitters = _mainGrid.Splitters;
            if (splitters.Count >= 2)
                splitters[1].Visible = _detailVisible;

            // Hide/show the detail column container
            var columns = _mainGrid.Columns;
            if (columns.Count >= 3)
                columns[2].Visible = _detailVisible;
        }

        _config.Config.ShowDetailPanel = _detailVisible;
        UpdateStatusLine();
        _mainWindow?.Invalidate(true);
    }

    private void Refresh()
    {
        _fileList.Refresh();
        _folderTree.RefreshNode(_folderTree.Control.SelectedNode);
        UpdateStatusLine();
    }

    private void ShowOperationsPortal()
    {
        if (_mainWindow == null) return;
        DismissOperationsPortal();

        // Anchor at bottom-left of the status bar area
        int anchorX = 2;
        int anchorY = _mainWindow.Height - 3;

        _opsPortal = new UI.OperationsPortal(_ws, _operations, anchorX, anchorY,
            _mainWindow.Width, _mainWindow.Height);
        _opsPortal.Container = _mainWindow;
        _opsPortalNode = _mainWindow.CreatePortal(_statusLine.Control, _opsPortal);

        _opsPortal.Dismissed += (_, _) => DismissOperationsPortal();
        _opsPortal.DismissRequested += (_, _) => DismissOperationsPortal();
    }

    private void DismissOperationsPortal()
    {
        if (_opsPortalNode != null && _mainWindow != null)
        {
            _mainWindow.RemovePortal(_statusLine.Control, _opsPortalNode);
            _opsPortalNode = null;
            _opsPortal = null;
        }
    }

    private void UpdateStatusLine()
    {
        var checkedCount = GetCheckedCount();
        _statusLine.Update(
            _fileList.Control.RowCount,
            checkedCount,
            _detailVisible,
            _config.Config.ShowHiddenFiles,
            _operations.ActiveCount,
            _operations.TotalCount);
    }

    private int GetCheckedCount()
    {
        var indices = _fileList.Control.GetSelectedIndices();
        return indices.Count > 0 ? indices.Count : (_fileList.GetSelectedEntry() != null ? 1 : 0);
    }

    private List<FileEntry> GetCheckedEntries()
    {
        var result = new List<FileEntry>();
        var indices = _fileList.Control.GetSelectedIndices();
        if (indices.Count > 0)
        {
            foreach (var idx in indices)
            {
                var entry = _fileList.GetEntryAt(idx);
                if (entry != null) result.Add(entry);
            }
        }
        else
        {
            var selected = _fileList.GetSelectedEntry();
            if (selected != null) result.Add(selected);
        }
        return result;
    }

    private void UpdateToolbar()
    {
        if (_toolbar == null) return;
        _toolbar.Clear();

        var checkedCount = GetCheckedCount();
        var hasSelection = checkedCount > 0;
        var multiSelect = checkedCount > 1;

        AddToolbarButton("◈ Open [grey50]Enter[/]", OpenSelected);
        AddToolbarButton("↑ Up [grey50]Bksp[/]", NavigateUp);
        _toolbar.AddItem(new SeparatorControl());
        AddToolbarButton("⊕ New [grey50]^N[/]", () => _ = NewItemAsync(false));
        if (hasSelection)
        {
            if (!multiSelect)
            {
                AddToolbarButton("⧫ Rename [grey50]F2[/]", () => _ = RenameSelectedAsync());
            }
            AddToolbarButton("✕ Delete" + (multiSelect ? $" ({checkedCount})" : "") + " [grey50]Del[/]",
                () => _ = DeleteSelectedAsync());
            if (!multiSelect)
            {
                AddToolbarButton("⊞ Props [grey50]F4[/]", () => _ = ShowPropertiesAsync());
            }
            _toolbar.AddItem(new SeparatorControl());
            AddToolbarButton("⊟ Copy" + (multiSelect ? $" ({checkedCount})" : "") + " [grey50]^C[/]", CopySelected);
            AddToolbarButton("⊠ Cut" + (multiSelect ? $" ({checkedCount})" : "") + " [grey50]^X[/]", CutSelected);
        }
        if (_clipboard.HasContent)
        {
            AddToolbarButton($"⊡ Paste ({_clipboard.Paths.Count}) [grey50]^V[/]",
                () => _ = PasteAsync());
        }
    }

    private void AddToolbarButton(string label, Action action)
    {
        var btn = Controls.Button()
            .WithText(label)
            .WithBorder(ButtonBorderStyle.None)
            .WithBackgroundColor(Color.Transparent)
            .WithBorderBackgroundColor(Color.Transparent)
            .OnClick((_, _) => action())
            .Build();
        _toolbar.AddItem(btn);
    }
}
