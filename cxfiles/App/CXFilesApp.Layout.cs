using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;
using CXFiles.Services;
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
        _breadcrumb.FavoritesClicked += ShowBookmarksPortal;
        _breadcrumb.PathSubmitted += SubmitPath;
        _breadcrumb.EditCancelled += () =>
        {
            DismissPathPortal();
            CancelPathDebounce();
        };
        // If portal is already open, refresh it live on every keystroke.
        // If closed, schedule a debounced open so it appears after the user
        // pauses typing (fast explicit trigger: Tab or Ctrl+Space).
        _breadcrumb.EditTextChanged += text =>
        {
            if (_pathPortal != null) ShowOrUpdatePathPortal(text);
            else SchedulePathDebounce(text);
        };

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
        _folderTree = new FolderTreePanel(_fs, _config.Config.ShowHiddenFiles);
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
        _tabControl.ShowTabHeader = true;

        // Create first tab
        var firstTab = CreateTab(_config.Config.DefaultPath);
        _tabs.Add(firstTab);
        _tabControl.AddTab(firstTab.TabTitle, firstTab.Container, isClosable: false);

        // Tab events
        _tabControl.TabChanged += (_, e) => OnTabChanged(e);
        _tabControl.TabCloseRequested += (_, e) =>
        {
            // Check if this is the terminal tab (in middle column)
            if (_terminalInMiddle && e.TabPage.Content == _terminal)
            {
                DisposeTerminal();
                return;
            }
            if (_tabControl.TabCount > 1)
            {
                _tabs[e.Index].Dispose();
                _tabs.RemoveAt(e.Index);
                _tabControl.RemoveTab(e.Index);
                UpdateCloseButtons();
                UpdateToolbar();
            }
        };

        // Context menu
        _contextMenu = new UI.ContextMenuBuilder();
        _contextMenu.OnOpen += entry =>
        {
            if (entry.IsDirectory)
                NavigateTo(entry.FullPath);
            else
                _launcher.OpenWithDefault(entry.FullPath);
        };
        _contextMenu.OnOpenInEditor += entry => OpenInEditor(entry.FullPath);
        _contextMenu.OnOpenTerminal += dir => OpenTerminalAt(dir);
        _contextMenu.OnRename += () => _ = RenameSelectedAsync();
        _contextMenu.OnDelete += () => _ = DeleteSelectedAsync();
        _contextMenu.OnProperties += () => _ = ShowPropertiesAsync();
        _contextMenu.OnCopy += CopySelected;
        _contextMenu.OnCut += CutSelected;
        _contextMenu.OnPaste += () => _ = PasteAsync();
        _contextMenu.OnNewItem += isDir => _ = NewItemAsync(isDir);
        _contextMenu.OnRefresh += Refresh;
        _contextMenu.OnAddToFavorites += entry => AddFolderToFavorites(entry.FullPath);
        _contextMenu.OnDismissed += () => _folderTree.ClearContextHighlight();

        // Detail panel (right panel)
        _detailPanel = new DetailPanel(_fs, new PdfPreviewService(), new MarkdownRenderer());

        // Status line
        _statusLine = new StatusLine();
        _statusLine.DetailToggled += ToggleDetailPanel;
        _statusLine.HiddenToggled += () =>
        {
            _config.Config.ShowHiddenFiles = !_config.Config.ShowHiddenFiles;
            foreach (var t in _tabs)
                t.FileList.SetShowHidden(_config.Config.ShowHiddenFiles);
            _folderTree.SetShowHidden(_config.Config.ShowHiddenFiles);
            _config.Save();
            UpdateStatusLine();
        };
        _statusLine.RefreshClicked += Refresh;
        _statusLine.OptionsClicked += () => _ = ShowOptionsAsync();
        _statusLine.OperationsClicked += ShowOperationsPortal;
        _statusLine.ClipboardClicked += ShowClipboardPortal;
        _statusLine.HelpClicked += () => _ = ShowHelpAsync();
        _statusLine.TerminalToggled += () =>
        {
            if (!ActiveTab.ViewingTrash)
                OpenOrSwitchTerminal();
        };
        _statusLine.TerminalDockToggled += () =>
        {
            if (_terminal != null)
                ToggleTerminalPosition();
        };

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

        // Panel headers (cxpost pattern)
        var panelHeaderBg = new Color(40, 50, 70, 160);

        _treeHeader = Controls.StatusBar()
            .AddLeftText("[grey70]Folders[/]")
            .WithMargin(1, 0, 0, 0)
            .Build();
        _treeHeader.BackgroundColor = panelHeaderBg;
        _treeHeader.HorizontalAlignment = HorizontalAlignment.Stretch;

        // Right panel: tabbed Preview + Terminal
        _rightPanelTabs = new TabControlBuilder()
            .WithHeaderStyle(TabHeaderStyle.Classic)
            .Fill()
            .Build();
        _rightPanelTabs.HorizontalAlignment = HorizontalAlignment.Stretch;
        _rightPanelTabs.VerticalAlignment = VerticalAlignment.Fill;
        _rightPanelTabs.BackgroundColor = Color.Transparent;
        _rightPanelTabs.TabCloseRequested += (_, e) =>
        {
            if (e.TabPage.Content == _terminal)
                DisposeTerminal();
        };
        _rightPanelTabs.AddTab("Preview", _detailPanel.Control, isClosable: false);

        // Main grid: tree | splitter | file list | splitter | detail
        _mainGrid = Controls.HorizontalGrid()
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Column(col => col.Width(_config.Config.TreePanelWidth).Add(_treeHeader).Add(_folderTree.Control))
            .Column(col => col.Flex(1).Add(_tabControl))
            .Column(col => col.Width(_config.Config.DetailPanelWidth).Add(_rightPanelTabs))
            .WithSplitterAfter(0)
            .WithSplitterAfter(1)
            .Build();

        foreach (var splitter in _mainGrid.Splitters)
            splitter.ForegroundColor = Color.Grey27;

        // Persist column widths when splitters are moved
        foreach (var splitter in _mainGrid.Splitters)
            splitter.SplitterMoved += (_, _) => SaveColumnWidths();

        // Apply initial visibility for detail panel
        if (!_detailVisible)
        {
            _rightPanelTabs.Visible = false;
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

        // Compositor overlay: paints search hint/state messages over the file list area.
        _mainWindow.PostBufferPaint += PaintSearchOverlay;

        // Auto-exit breadcrumb edit mode when focus moves off the input.
        // Window-level FocusManager is more reliable than the control-level
        // LostFocus event (which can miss transitions in some cases).
        _mainWindow.FocusManager.FocusChanged += (_, args) =>
        {
            if (_breadcrumb.InEditMode
                && ReferenceEquals(args.Previous, _breadcrumb.EditInput)
                && !ReferenceEquals(args.Current, _breadcrumb.EditInput))
            {
                _breadcrumb.ExitEditMode();
            }
        };

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
                return;
            }

            // Breadcrumb edit mode: intercept Esc / Tab / Ctrl+Space / Enter /
            // Up / Down / PgUp / PgDn BEFORE the PromptControl sees them so they
            // drive the completion portal (or exit edit) instead of being typed
            // into the input or firing its Entered event.
            if (_breadcrumb.InEditMode && _breadcrumb.EditInput.HasFocus)
            {
                var bk = e.KeyInfo;
                bool bctrl = bk.Modifiers.HasFlag(ConsoleModifiers.Control);

                if (bk.Key == ConsoleKey.Escape)
                {
                    if (_pathPortal != null)
                    {
                        DismissPathPortal();
                    }
                    else
                    {
                        _breadcrumb.ExitEditMode();
                        ReturnFocusToFileList();
                    }
                    e.Handled = true;
                    return;
                }

                if (bk.Key == ConsoleKey.Tab
                    || (bctrl && bk.Key == ConsoleKey.Spacebar))
                {
                    HandleBreadcrumbTab();
                    e.Handled = true;
                    return;
                }

                if (_pathPortal != null)
                {
                    switch (bk.Key)
                    {
                        case ConsoleKey.UpArrow:
                        case ConsoleKey.DownArrow:
                        case ConsoleKey.PageUp:
                        case ConsoleKey.PageDown:
                            _pathPortalUserNavigated = true;
                            _pathPortal.ProcessKey(bk);
                            e.Handled = true;
                            return;

                        case ConsoleKey.Enter:
                            if (_pathPortalUserNavigated)
                            {
                                // User picked a candidate — let the portal select.
                                _pathPortal.ProcessKey(bk);
                            }
                            else
                            {
                                // URL-bar convention: Enter commits the typed path
                                // even though a candidate is visually highlighted.
                                DismissPathPortal();
                                SubmitPath(_breadcrumb.EditInput.Input);
                            }
                            e.Handled = true;
                            return;
                    }
                }
            }

            // When the terminal has focus, intercept app-level keys BEFORE
            // the terminal's ProcessKey converts them to escape sequences
            // (otherwise e.g. F8 → \e[19~ leaks '~' into bash).
            if (_terminal != null && _terminal.HasFocus)
            {
                bool ctrl = e.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.Control);
                switch (e.KeyInfo.Key)
                {
                    case ConsoleKey.F1:
                    case ConsoleKey.F6:
                    case ConsoleKey.F7:
                    case ConsoleKey.F8:
                    case ConsoleKey.Oem3 when ctrl:
                    case ConsoleKey.Q when ctrl:
                        // Handled=true blocks the terminal, then we forward
                        // to the global handler ourselves.
                        e.Handled = true;
                        OnGlobalKeyPressed(null, e);
                        return;
                }
            }
        };

        // Right-click on folder tree opens folder context menu
        _folderTree.FolderRightClicked += (path, args) =>
        {
            if (_mainWindow != null)
            {
                var actions = BuildFolderActions(path);
                _contextMenu.ShowForFolder(path, _mainWindow, _folderTree.Control,
                    args.AbsolutePosition.X, args.AbsolutePosition.Y, actions);
            }
        };

        _folderTree.BookmarkRightClicked += (path, args) =>
        {
            if (_mainWindow == null) return;
            _contextMenu.ShowForBookmark(
                path, _mainWindow, _folderTree.Control,
                args.AbsolutePosition.X, args.AbsolutePosition.Y,
                onOpen: () =>
                {
                    if (Directory.Exists(path)) NavigateTo(path);
                    else _ws.NotificationStateService.ShowNotification(
                        "Favorites", $"Path no longer exists: {path}",
                        SharpConsoleUI.Core.NotificationSeverity.Warning);
                },
                onRename: () => _ = RenameBookmarkAsync(path),
                onRemove: () => RemoveBookmark(path));
        };

        _folderTree.MissingBookmarkClicked += (path) =>
        {
            _ws.NotificationStateService.ShowNotification(
                "Favorites",
                $"Path no longer exists: {path}",
                SharpConsoleUI.Core.NotificationSeverity.Warning);
        };

        _config.BookmarksChanged += (_, _) =>
        {
            _folderTree.SetBookmarks(_config.Config.Bookmarks);
        };

        _folderTree.SetBookmarks(_config.Config.Bookmarks);
    }
}
