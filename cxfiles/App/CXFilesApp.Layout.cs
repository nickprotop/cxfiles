using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
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

        // File list (center panel)
        _fileList = new FileListPanel(_fs, _config.Config.ShowHiddenFiles);
        _fileList.FileActivated += entry =>
        {
            if (entry.IsDirectory)
                NavigateTo(entry.FullPath);
        };
        _fileList.SelectionChanged += entry =>
        {
            _detailPanel.ShowEntry(entry);
            UpdateStatusLine();
            UpdateToolbar();
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
            _fileList.SetShowHidden(_config.Config.ShowHiddenFiles);
            _config.Save();
            UpdateStatusLine();
        };
        _statusLine.RefreshClicked += Refresh;
        _statusLine.OptionsClicked += () => _ = ShowOptionsAsync();
        _statusLine.OperationsClicked += ShowOperationsPortal;

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
        _fileList.Control.HorizontalAlignment = HorizontalAlignment.Stretch;
        _detailPanel.Control.HorizontalAlignment = HorizontalAlignment.Stretch;

        // Main grid: tree | splitter | file list | splitter | detail
        _mainGrid = Controls.HorizontalGrid()
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Column(col => col.Width(25).Add(_folderTree.Control))
            .Column(col => col.Flex(1).Add(_fileList.Control))
            .Column(col => col.Width(30).Add(_detailPanel.Control))
            .WithSplitterAfter(0)
            .WithSplitterAfter(1)
            .Build();

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

        // Route preview keys to context menu / operations portal
        _mainWindow.PreviewKeyPressed += (_, e) =>
        {
            if (_contextMenu.ProcessPreviewKey(e)) return;
            if (_opsPortal != null)
            {
                _opsPortal.ProcessKey(e.KeyInfo);
                e.Handled = true;
            }
        };

        // Right-click on file list opens context menu
        _fileList.Control.MouseRightClick += (_, args) =>
        {
            var entry = _fileList.GetSelectedEntry();
            if (entry != null && _mainWindow != null)
            {
                _contextMenu.Show(entry, _mainWindow, _fileList.Control,
                    args.AbsolutePosition.X, args.AbsolutePosition.Y,
                    _clipboard.HasContent);
            }
        };
    }
}
