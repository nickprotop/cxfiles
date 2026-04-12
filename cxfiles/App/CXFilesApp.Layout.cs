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

        // Detail panel (right panel)
        _detailPanel = new DetailPanel(_fs);
        _detailPanel.Control.Visible = _detailVisible;

        // Status line
        _statusLine = new StatusLine();

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
            .Column(col => col.Flex(1).Add(_folderTree.Control))
            .Column(col => col.Flex(3).Add(_fileList.Control))
            .Column(col => col.Flex(1).Add(_detailPanel.Control))
            .WithSplitterAfter(0)
            .WithSplitterAfter(1)
            .Build();
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
    }
}
