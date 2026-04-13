using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;
using CXFiles.UI.Components;

namespace CXFiles.App;

public partial class CXFilesApp
{
    private void BuildUI()
    {
        // Breadcrumb bar
        _breadcrumb = new BreadcrumbBar();
        _breadcrumb.SegmentClicked += path => NavigateTo(path);
        _breadcrumb.TrashClicked += NavigateToTrash;

        // Toolbar
        _toolbar = Controls.Toolbar()
            .StickyTop()
            .WithSpacing(1)
            .WithWrap()
            .WithMargin(1, 0, 1, 0)
            .WithBackgroundColor(Color.Transparent)
            .WithBelowLineColor(Color.Grey27)
            .Build();

        // Folder tree (left panel)
        _folderTree = new FolderTreePanel(_fs);
        _folderTree.FolderSelected += path => NavigateTo(path);
        _folderTree.LoadRoots();

        // Tab control for file list (center panel)
        _tabControl = new TabControlBuilder()
            .WithHeaderStyle(TabHeaderStyle.Classic)
            .Fill()
            .Build();
        _tabControl.HorizontalAlignment = HorizontalAlignment.Stretch;
        _tabControl.VerticalAlignment = VerticalAlignment.Fill;
        _tabControl.BackgroundColor = Color.Transparent;
        _tabControl.ShowTabHeader = false;

        // Create first tab
        var firstTab = CreateTab(_config.Config.DefaultPath);
        _tabs.Add(firstTab);
        _tabControl.AddTab(firstTab.TabTitle, firstTab.Container, isClosable: true);

        // Tab events
        _tabControl.TabChanged += (_, e) => OnTabChanged(e);
        _tabControl.TabCloseRequested += (_, e) =>
        {
            if (_tabControl.TabCount > 1)
            {
                _tabs[e.Index].Dispose();
                _tabs.RemoveAt(e.Index);
                _tabControl.RemoveTab(e.Index);
                UpdateTabHeader();
            }
        };

        // Context menu
        _contextMenu = new UI.ContextMenuBuilder();
        _contextMenu.OnOpen += entry => { if (entry.IsDirectory) NavigateTo(entry.FullPath); };
        _contextMenu.OnRename += () => _ = RenameSelectedAsync();
        _contextMenu.OnDelete += () => _ = DeleteSelectedAsync();
        _contextMenu.OnProperties += () => _ = ShowPropertiesAsync();
        _contextMenu.OnCopy += CopySelected;
        _contextMenu.OnCut += CutSelected;
        _contextMenu.OnPaste += () => _ = PasteAsync();
        _contextMenu.OnNewItem += isDir => _ = NewItemAsync(isDir);
        _contextMenu.OnRefresh += Refresh;

        // Detail panel (right panel)
        _detailPanel = new DetailPanel(_fs);
        _detailPanel.Control.Visible = _detailVisible;

        // Status line
        _statusLine = new StatusLine();
        _statusLine.DetailToggled += ToggleDetailPanel;
        _statusLine.HiddenToggled += () =>
        {
            _config.Config.ShowHiddenFiles = !_config.Config.ShowHiddenFiles;
            foreach (var t in _tabs)
                t.FileList.SetShowHidden(_config.Config.ShowHiddenFiles);
            _config.Save();
            UpdateStatusLine();
        };
        _statusLine.RefreshClicked += Refresh;
        _statusLine.OptionsClicked += () => _ = ShowOptionsAsync();
        _statusLine.OperationsClicked += ShowOperationsPortal;
        _statusLine.ClipboardClicked += ShowClipboardPortal;

        // Top rule
        var topRule = Controls.RuleBuilder()
            .StickyTop()
            .WithColor(Color.Grey27)
            .Build();

        // Bottom rule
        var bottomRule = Controls.RuleBuilder()
            .StickyBottom()
            .WithColor(Color.Grey27)
            .Build();

        // Set controls to stretch horizontally
        _folderTree.Control.HorizontalAlignment = HorizontalAlignment.Stretch;
        _detailPanel.Control.HorizontalAlignment = HorizontalAlignment.Stretch;

        // Panel headers (cxpost pattern)
        var panelHeaderBg = new Color(40, 50, 70, 160);

        _treeHeader = Controls.StatusBar()
            .AddLeftText("[grey70]Folders[/]")
            .WithMargin(1, 0, 0, 0)
            .Build();
        _treeHeader.BackgroundColor = panelHeaderBg;
        _treeHeader.HorizontalAlignment = HorizontalAlignment.Stretch;

        _detailHeader = Controls.StatusBar()
            .AddLeftText("[grey70]Detail[/]")
            .WithMargin(1, 0, 0, 0)
            .Build();
        _detailHeader.BackgroundColor = panelHeaderBg;
        _detailHeader.HorizontalAlignment = HorizontalAlignment.Stretch;

        // Main grid: tree | splitter | file list | splitter | detail
        _mainGrid = Controls.HorizontalGrid()
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Column(col => col.Width(25).Add(_treeHeader).Add(_folderTree.Control))
            .Column(col => col.Flex(1).Add(_tabControl))
            .Column(col => col.Width(30).Add(_detailHeader).Add(_detailPanel.Control))
            .WithSplitterAfter(0)
            .WithSplitterAfter(1)
            .Build();

        foreach (var splitter in _mainGrid.Splitters)
            splitter.ForegroundColor = Color.Grey27;

        // Apply initial visibility for detail panel
        if (!_detailVisible)
        {
            _detailPanel.Control.Visible = false;
            var splitters = _mainGrid.Splitters;
            if (splitters.Count >= 2)
                splitters[1].Visible = false;
            var columns = _mainGrid.Columns;
            if (columns.Count >= 3)
                columns[2].Visible = false;
        }
        var mainGrid = _mainGrid;

        // Background gradient (cxpost style)
        var gradient = ColorGradient.FromColors(new Color(25, 32, 52), new Color(7, 7, 13));

        // Build window
        _mainWindow = new WindowBuilder(_ws)
            .HideTitle()
            .HideTitleButtons()
            .WithBorderStyle(BorderStyle.Rounded)
            .WithBorderColor(Color.Grey27)
            .Maximized()
            .Movable(false)
            .Resizable(false)
            .Minimizable(false)
            .Maximizable(false)
            .WithBackgroundGradient(gradient, GradientDirection.Vertical)
            .AddControl(_breadcrumb.Control)
            .AddControl(topRule)
            .AddControl(_toolbar)
            .AddControl(mainGrid)
            .AddControl(bottomRule)
            .AddControl(_statusLine.Control)
            .OnKeyPressed(OnGlobalKeyPressed)
            .Build();

        // Route preview keys to context menu / operations / clipboard portals
        _mainWindow.PreviewKeyPressed += (_, e) =>
        {
            if (_contextMenu.ProcessPreviewKey(e)) return;
            if (_opsPortal != null)
            {
                _opsPortal.ProcessKey(e.KeyInfo);
                e.Handled = true;
                return;
            }
            if (_clipPortal != null)
            {
                _clipPortal.ProcessKey(e.KeyInfo);
                e.Handled = true;
            }
        };

        // Right-click on folder tree opens folder context menu
        _folderTree.FolderRightClicked += path =>
        {
            if (_mainWindow != null)
            {
                var node = _folderTree.Control.SelectedNode;
                var pos = _folderTree.Control.SelectedIndex;
                _contextMenu.ShowForFolder(path, _mainWindow, _folderTree.Control,
                    3, pos + 2, NavigateTo, _clipboard.HasContent);
            }
        };
    }
}
