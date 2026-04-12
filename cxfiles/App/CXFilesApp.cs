using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;
using CXFiles.Services;
using CXFiles.UI.Components;

namespace CXFiles.App;

public partial class CXFilesApp
{
    private readonly ConsoleWindowSystem _ws;
    private readonly IFileSystemService _fs;
    private readonly IConfigService _config;
    private Window? _mainWindow;

    // UI Components
    private BreadcrumbBar _breadcrumb = null!;
    private FolderTreePanel _folderTree = null!;
    private FileListPanel _fileList = null!;
    private DetailPanel _detailPanel = null!;
    private StatusLine _statusLine = null!;
    private ToolbarControl _toolbar = null!;

    // State
    private string _currentPath;
    private bool _detailVisible;

    public CXFilesApp(ConsoleWindowSystem ws, IFileSystemService fs, IConfigService config)
    {
        _ws = ws;
        _fs = fs;
        _config = config;
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
        _config.Config.ShowDetailPanel = _detailVisible;
        _mainWindow?.Invalidate(true);
    }

    private void Refresh()
    {
        _fileList.Refresh();
        UpdateStatusLine();
    }

    private void UpdateStatusLine()
    {
        var selected = _fileList.GetSelectedEntry();
        _statusLine.Update(
            _fileList.Control.RowCount,
            selected != null ? 1 : 0,
            _currentPath);
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
        }
        _toolbar.AddItem(new SeparatorControl());
        AddToolbarButton(_detailVisible ? "◧ Detail [dim]F3[/]" : "◨ Detail [dim]F3[/]", ToggleDetailPanel);
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
