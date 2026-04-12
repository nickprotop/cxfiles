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
        // TODO: expand tree to path
        UpdateStatusLine();
        UpdateToolbar();

        // Restart file watcher for the new directory
        _fileWatcher?.Dispose();
        try
        {
            _fileWatcher = _fs.WatchDirectory(path, _ => Refresh());
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
        UpdateStatusLine();
    }

    private void UpdateStatusLine()
    {
        var checkedCount = GetCheckedCount();
        _statusLine.Update(
            _fileList.Control.RowCount,
            checkedCount,
            _detailVisible,
            _config.Config.ShowHiddenFiles,
            _operations.ActiveCount);
    }

    private int GetCheckedCount()
    {
        var selected = _fileList.GetSelectedEntry();
        return selected != null ? 1 : 0;
    }

    private List<FileEntry> GetCheckedEntries()
    {
        var result = new List<FileEntry>();
        var selected = _fileList.GetSelectedEntry();
        if (selected != null) result.Add(selected);
        return result;
    }

    private void UpdateToolbar()
    {
        if (_toolbar == null) return;
        _toolbar.Clear();

        var hasSelection = _fileList.GetSelectedEntry() != null;

        AddToolbarButton("◈ Open [dim]Enter[/]", OpenSelected);
        AddToolbarButton("↑ Up [dim]Bksp[/]", NavigateUp);
        _toolbar.AddItem(new SeparatorControl());
        AddToolbarButton("⊕ New [dim]^N[/]", () => _ = NewItemAsync(false));
        if (hasSelection)
        {
            AddToolbarButton("⧫ Rename [dim]F2[/]", () => _ = RenameSelectedAsync());
            AddToolbarButton("✕ Delete [dim]Del[/]", () => _ = DeleteSelectedAsync());
            _toolbar.AddItem(new SeparatorControl());
            AddToolbarButton("⊟ Copy [dim]^C[/]", CopySelected);
            AddToolbarButton("⊠ Cut [dim]^X[/]", CutSelected);
        }
        if (_clipboard.HasContent)
        {
            var label = _clipboard.Action == Services.ClipboardAction.Cut
                ? $"⊡ Paste ({_clipboard.Paths.Count}) [dim]^V[/]"
                : $"⊡ Paste ({_clipboard.Paths.Count}) [dim]^V[/]";
            AddToolbarButton(label, () => _ = PasteAsync());
        }
        _toolbar.AddItem(new SeparatorControl());
        AddToolbarButton("↻ Refresh [dim]F5[/]", Refresh);
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
