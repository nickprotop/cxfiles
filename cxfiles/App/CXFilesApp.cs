using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;
using CXFiles.Models;
using CXFiles.Services;
using CXFiles.UI.Components;
using CXFiles.UI.Modals;

namespace CXFiles.App;

public partial class CXFilesApp
{
    private readonly ConsoleWindowSystem _ws;
    private readonly IFileSystemService _fs;
    private readonly IConfigService _config;
    private readonly OperationManager _operations;
    private readonly ITrashService _trash;
    private readonly SudoService _sudo;
    private readonly LauncherService _launcher;
    private Window? _mainWindow;

    // UI Components
    private BreadcrumbBar _breadcrumb = null!;
    private FolderTreePanel _folderTree = null!;
    private DetailPanel _detailPanel = null!;
    private StatusLine _statusLine = null!;
    private ToolbarControl _toolbar = null!;
    private HorizontalGridControl? _mainGrid;
    private StatusBarControl _treeHeader = null!;
    private TabControl _rightPanelTabs = null!;
    private SharpConsoleUI.Controls.Terminal.TerminalControl? _terminal;
    private string? _terminalFolder;
    private bool _terminalInMiddle;
    private UI.ContextMenuBuilder _contextMenu = null!;
    private UI.OperationsPortal? _opsPortal;
    private LayoutNode? _opsPortalNode;
    private UI.ClipboardPortal? _clipPortal;
    private LayoutNode? _clipPortalNode;

    // Tabs
    private TabControl _tabControl = null!;
    private readonly List<TabState> _tabs = new();

    // State
    private bool _detailVisible;
    private readonly ClipboardState _clipboard = new();

    private TabState ActiveTab => _tabs[Math.Min(_tabControl.ActiveTabIndex, _tabs.Count - 1)];
    private FileListPanel ActiveFileList => ActiveTab.FileList;

    public CXFilesApp(ConsoleWindowSystem ws, IFileSystemService fs, IConfigService config,
        OperationManager operations, ITrashService trash, SudoService sudo, LauncherService launcher)
    {
        _ws = ws;
        _fs = fs;
        _config = config;
        _operations = operations;
        _trash = trash;
        _sudo = sudo;
        _launcher = launcher;
        _detailVisible = config.Config.ShowDetailPanel;
    }

    public void Run()
    {
        BuildUI();
        NavigateTo(_config.Config.DefaultPath);
        _ws.AddWindow(_mainWindow!);
        _ws.Run();
        DisposeTerminal();
    }

    public void NavigateToTrash()
    {
        var tab = ActiveTab;
        tab.ViewingTrash = true;

        if (_detailVisible)
        {
            _rightPanelTabs.Visible = false;
            if (_mainGrid != null)
            {
                var splitters = _mainGrid.Splitters;
                if (splitters.Count >= 2) splitters[1].Visible = false;
                var columns = _mainGrid.Columns;
                if (columns.Count >= 3) columns[2].Visible = false;
            }
        }

        _breadcrumb.Update(_trash.TrashPath);
        _tabControl.SetTabTitle(_tabControl.ActiveTabIndex, "Trash");
        var trashSource = new UI.Components.TrashDataSource();
        trashSource.SetEntries(_trash.ListTrash());
        tab.FileList.Control.DataSource = trashSource;
        UpdateStatusLine();
        UpdateToolbar();
        _mainWindow?.Invalidate(true);
    }

    public void NavigateTo(string path)
    {
        if (!_fs.DirectoryExists(path)) return;

        // Check if directory is readable before navigating
        try
        {
            Directory.GetFileSystemEntries(path, "*", new EnumerationOptions
            {
                IgnoreInaccessible = false,
                RecurseSubdirectories = false,
            });
        }
        catch (UnauthorizedAccessException)
        {
            _ws.NotificationStateService.ShowNotification(
                "Permission denied",
                $"Cannot read {path}",
                SharpConsoleUI.Core.NotificationSeverity.Warning);
            return;
        }
        catch { /* other errors — let Navigate handle them */ }

        var tab = ActiveTab;

        if (tab.ViewingTrash && _detailVisible)
        {
            _rightPanelTabs.Visible = true;
            if (_mainGrid != null)
            {
                var splitters = _mainGrid.Splitters;
                if (splitters.Count >= 2) splitters[1].Visible = true;
                var columns = _mainGrid.Columns;
                if (columns.Count >= 3) columns[2].Visible = true;
            }
        }

        tab.ViewingTrash = false;
        tab.Path = path;
        _breadcrumb.Update(path);
        tab.FileList.Navigate(path);
        _tabControl.SetTabTitle(_tabControl.ActiveTabIndex, tab.TabTitle);
        if (_config.Config.SyncTreeToTab && !_folderTree.NavigatingFromTree)
            _folderTree.ExpandToPath(path, expandTarget: true);
        _detailPanel.ShowEntry(null);
        UpdateStatusLine();
        UpdateToolbar();

        // Restart file watcher for the new directory.
        // The callback captures `tab` so the refresh targets the tab whose
        // folder changed, not whatever tab happens to be active. Skip the
        // refresh if the tab is currently in search mode — we don't want to
        // wipe streaming/displayed search results.
        tab.FileWatcher?.Dispose();
        var watchedTab = tab;
        try
        {
            tab.FileWatcher = _fs.WatchDirectory(path, _ =>
            {
                if (watchedTab.Search.Restore != null) return; // search active — leave it alone
                watchedTab.FileList.RefreshAsync(action => _ws.EnqueueOnUIThread(() =>
                {
                    action();
                    if (_tabs.IndexOf(watchedTab) == _tabControl.ActiveTabIndex)
                        UpdateStatusLine();
                }));
            });
        }
        catch { /* watcher may fail on some filesystems */ }
    }

    private TabState CreateTab(string path)
    {
        var tab = new TabState(_fs, _config.Config.ShowHiddenFiles, _config.Config.AutoSelectFirstItem, path);
        WireSearchBar(tab);
        tab.FileList.FileActivated += entry =>
        {
            if (entry.IsDirectory)
            {
                if (tab.Search.Restore != null)
                    CancelAndRestore(tab);
                NavigateTo(entry.FullPath);
            }
            else
            {
                _launcher.OpenWithDefault(entry.FullPath);
            }
        };
        tab.FileList.SelectionChanged += info =>
        {
            if (_tabs.IndexOf(tab) == _tabControl.ActiveTabIndex)
            {
                if (info.Entry != null)
                    _detailPanel.ShowEntry(info.Entry);
                else if (info.IsLoading)
                    _detailPanel.ShowLoading(info.LoadingName ?? "", info.LoadingPath ?? "");
                else
                    _detailPanel.ShowFolder(tab.Path);
                UpdateStatusLine();
                UpdateToolbar();
            }
        };
        tab.FileList.Control.MouseRightClick += (_, args) =>
        {
            if (_tabs.IndexOf(tab) != _tabControl.ActiveTabIndex) return;
            var entry = tab.FileList.GetSelectedEntry();
            if (entry != null && _mainWindow != null)
            {
                var showFav = entry.IsDirectory
                    && !_config.Config.Bookmarks.Any(b => b.Path == entry.FullPath);
                _contextMenu.Show(entry, _mainWindow, tab.FileList.Control,
                    args.AbsolutePosition.X, args.AbsolutePosition.Y,
                    _clipboard.HasContent, showFav);
            }
        };
        return tab;
    }

    private void OnTabChanged(TabChangedEventArgs e)
    {
        if (e.NewIndex < 0 || e.NewIndex >= _tabs.Count)
            return; // terminal/editor tab — not a file browser tab
        var tab = _tabs[e.NewIndex];
        _breadcrumb.Update(tab.Path);
        if (_config.Config.SyncTreeToTab && !tab.ViewingTrash)
            _folderTree.ExpandToPath(tab.Path);
        var sel = tab.FileList.GetSelectedEntry();
        if (sel != null)
            _detailPanel.ShowEntry(sel);
        else
            _detailPanel.ShowFolder(tab.Path);
        UpdateStatusLine();
        UpdateToolbar();
    }

    private void NewTab()
    {
        if (_tabs.Count >= _config.Config.MaxTabs) return;
        var path = ActiveTab.Path;
        var tab = CreateTab(path);
        tab.FileList.Navigate(path);
        _tabs.Add(tab);
        _tabControl.AddTab(tab.TabTitle, tab.Container, isClosable: true);
        _tabControl.ActiveTabIndex = _tabs.Count - 1;
        UpdateCloseButtons();
    }

    private void CloseActiveTab()
    {
        if (_tabControl.TabCount <= 1) return;
        var idx = _tabControl.ActiveTabIndex;
        _tabs[idx].Dispose();
        _tabs.RemoveAt(idx);
        _tabControl.RemoveTab(idx);
        UpdateCloseButtons();
        UpdateToolbar();
    }

    // The single remaining tab can't be closed — hide its × button.
    // Called after every tab add/remove so the affordance reflects state.
    private void UpdateCloseButtons()
    {
        bool closable = _tabControl.TabCount >= 2;
        for (int i = 0; i < _tabControl.TabCount; i++)
        {
            var page = _tabControl.GetTab(i);
            if (page != null) page.IsClosable = closable;
        }
        _tabControl.Invalidate();
    }

    private void JumpToTab(int index)
    {
        if (index >= 0 && index < _tabControl.TabCount)
            _tabControl.ActiveTabIndex = index;
    }

    private void NavigateUp()
    {
        var parent = Path.GetDirectoryName(ActiveTab.Path);
        if (!string.IsNullOrEmpty(parent))
            NavigateTo(parent);
    }

    private void OpenSelected()
    {
        var entry = ActiveFileList.GetSelectedEntry();
        if (entry == null) return;

        if (entry.IsDirectory)
            NavigateTo(entry.FullPath);
    }

    private void ToggleDetailPanel()
    {
        _detailVisible = !_detailVisible;
        _rightPanelTabs.Visible = _detailVisible;

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
        _config.Save();
        UpdateStatusLine();
        _mainWindow?.Invalidate(true);
    }

    private void OpenOrSwitchTerminal() => OpenTerminalAt(ActiveTab.Path);

    private void OpenTerminalAt(string path)
    {
        if (!_detailVisible && !_terminalInMiddle)
            ToggleDetailPanel();

        // If terminal exists for a different folder, dispose and recreate
        if (_terminal != null && _terminalFolder != path)
            DisposeTerminal();

        if (_terminal == null)
            CreateTerminal(path);

        // Switch to terminal tab in whichever TabControl it lives in
        if (_terminalInMiddle)
            _tabControl.SwitchToTab("Terminal");
        else
            _rightPanelTabs.SwitchToTab("Terminal");

        // Focus terminal so keystrokes go directly to it
        _mainWindow?.FocusManager?.SetFocus(_terminal, SharpConsoleUI.Controls.FocusReason.Keyboard);
        UpdateStatusLine();
    }

    private void OpenInEditor(string filePath)
    {
        // Resolve editor command
        var cmd = _config.Config.EditorCommand;
        if (string.IsNullOrWhiteSpace(cmd))
            cmd = Environment.GetEnvironmentVariable("EDITOR");
        if (string.IsNullOrWhiteSpace(cmd))
            cmd = "nano";

        // Dispose existing terminal if any — editor takes the same slot
        DisposeTerminal();

        if (!_detailVisible && !_terminalInMiddle)
            ToggleDetailPanel();

        _terminal = Controls.Terminal()
            .WithExe(cmd)
            .WithArgs(filePath)
            .WithWorkingDirectory(Path.GetDirectoryName(filePath) ?? ActiveTab.Path)
            .Build();
        _terminalFolder = null; // editor, not a folder terminal

        var captured = _terminal;
        captured.ProcessExited += (_, _) => _ws.EnqueueOnUIThread(() =>
        {
            if (_terminal == captured) DisposeTerminal();
        });

        _rightPanelTabs.AddTab("Editor", _terminal!, isClosable: true);
        _rightPanelTabs.SwitchToTab("Editor");
        UpdateStatusLine();
    }

    private void CreateTerminal(string path)
    {
        _terminal = Controls.Terminal()
            .WithWorkingDirectory(path)
            .Build();
        _terminalFolder = path;

        var captured = _terminal;
        captured.ProcessExited += (_, _) => _ws.EnqueueOnUIThread(() =>
        {
            if (_terminal == captured) DisposeTerminal();
        });

        if (_terminalInMiddle)
            _tabControl.AddTab("Terminal", _terminal!, isClosable: true);
        else
            _rightPanelTabs.AddTab("Terminal", _terminal!, isClosable: true);
    }

    private void ToggleTerminalPosition()
    {
        if (_terminal == null) return;

        _terminal.NudgeOnNextResize();

        var tabName = _terminalFolder != null ? "Terminal" : "Editor";
        var source = _terminalInMiddle ? _tabControl : _rightPanelTabs;
        var target = _terminalInMiddle ? _rightPanelTabs : _tabControl;

        // Extract from source
        for (int i = 0; i < source.TabCount; i++)
        {
            if (source.GetTab(i)?.Content == _terminal)
            {
                source.ExtractTab(i);
                break;
            }
        }

        // Add to target
        _terminalInMiddle = !_terminalInMiddle;
        target.AddTab(tabName, _terminal, isClosable: true);
        target.SwitchToTab(tabName);

        // Ensure detail panel is visible when moving back to right panel
        if (!_terminalInMiddle && !_detailVisible)
            ToggleDetailPanel();

        // Force layout recalculation so the terminal resizes to new panel dimensions
        _mainWindow?.ForceRebuildLayout();
        _mainWindow?.Invalidate(true);

        // Focus the terminal so keystrokes go to it
        _mainWindow?.FocusManager?.SetFocus(_terminal, SharpConsoleUI.Controls.FocusReason.Keyboard);
        UpdateStatusLine();
    }

    private void DisposeTerminal()
    {
        if (_terminal == null) return;

        var term = _terminal;
        _terminal = null;
        _terminalFolder = null;

        // Extract tab (without disposing content) from whichever TabControl it's in
        var targetTabs = _terminalInMiddle ? _tabControl : _rightPanelTabs;
        for (int i = 0; i < targetTabs.TabCount; i++)
        {
            if (targetTabs.GetTab(i)?.Content == term)
            {
                targetTabs.ExtractTab(i);
                break;
            }
        }

        _terminalInMiddle = false;
        term.Dispose();
        UpdateStatusLine();
    }

    private UI.FolderMenuActions BuildFolderActions(string folderPath)
    {
        return new UI.FolderMenuActions(
            Open: () => NavigateTo(folderPath),
            Rename: () => _ = RenameFolderAsync(folderPath),
            Delete: () => _ = DeletePathAsync(folderPath),
            Properties: () => _ = ShowPropertiesForPathAsync(folderPath),
            Copy: () =>
            {
                _clipboard.SetCopy(new[] { folderPath });
                UpdateToolbar();
                UpdateStatusLine();
            },
            Cut: () =>
            {
                _clipboard.SetCut(new[] { folderPath });
                UpdateToolbar();
                UpdateStatusLine();
            },
            Paste: () => _ = PasteIntoAsync(folderPath),
            NewFolder: () => _ = NewItemInAsync(folderPath, true),
            NewFile: () => _ = NewItemInAsync(folderPath, false),
            Refresh: () =>
            {
                _folderTree.RefreshNode(_folderTree.Control.SelectedNode);
                ActiveFileList.Refresh();
                UpdateStatusLine();
            },
            AddToFavorites: () => AddFolderToFavorites(folderPath),
            ShowAddToFavorites: !_config.Config.Bookmarks.Any(b => b.Path == folderPath),
            HasClipboard: _clipboard.HasContent
        );
    }

    private void AddCurrentFolderToFavorites() => AddFolderToFavorites(ActiveTab.Path);

    private void AddFolderToFavorites(string path)
    {
        var existing = _config.Config.Bookmarks.FirstOrDefault(b => b.Path == path);
        if (existing != null)
        {
            _ws.NotificationStateService.ShowNotification(
                "Favorites",
                $"'{existing.Name}' is already in Favorites",
                SharpConsoleUI.Core.NotificationSeverity.Info);
            return;
        }
        _config.AddBookmark(path);
        var added = _config.Config.Bookmarks.Last();
        _ws.NotificationStateService.ShowNotification(
            "Favorites",
            $"Added '{added.Name}' to Favorites",
            SharpConsoleUI.Core.NotificationSeverity.Info);
    }

    private void RemoveBookmark(string path)
    {
        var match = _config.Config.Bookmarks.FirstOrDefault(b => b.Path == path);
        if (match == null) return;
        _config.RemoveBookmark(path);
        _ws.NotificationStateService.ShowNotification(
            "Favorites",
            $"Removed '{match.Name}' from Favorites",
            SharpConsoleUI.Core.NotificationSeverity.Info);
    }

    private async Task RenameBookmarkAsync(string path)
    {
        var match = _config.Config.Bookmarks.FirstOrDefault(b => b.Path == path);
        if (match == null) return;
        var newName = await UI.Modals.RenameModal.ShowAsync(_ws, match.Name, _mainWindow);
        if (!string.IsNullOrWhiteSpace(newName))
            _config.RenameBookmark(path, newName);
    }

    private UI.Portals.BookmarksPortal? _bookmarksPortal;
    private LayoutNode? _bookmarksPortalNode;

    private void ShowBookmarksPortal()
    {
        if (_mainWindow == null) return;
        DismissBookmarksPortal();

        var anchorX = _mainWindow.Left + _mainWindow.Width - 4;
        var anchorY = _mainWindow.Top + 2;

        _bookmarksPortal = new UI.Portals.BookmarksPortal(
            _config.Config.Bookmarks, anchorX, anchorY, _mainWindow);
        _bookmarksPortal.Container = _mainWindow;
        _bookmarksPortalNode = _mainWindow.CreatePortal(_breadcrumb.Control, _bookmarksPortal);

        _bookmarksPortal.BookmarkSelected += (_, bm) =>
        {
            DismissBookmarksPortal();
            if (Directory.Exists(bm.Path))
            {
                NavigateTo(bm.Path);
            }
            else
            {
                _ws.NotificationStateService.ShowNotification(
                    "Favorites",
                    $"Path no longer exists: {bm.Path}",
                    SharpConsoleUI.Core.NotificationSeverity.Warning);
            }
        };

        _bookmarksPortal.Dismissed += (_, _) => DismissBookmarksPortal();
    }

    private void DismissBookmarksPortal()
    {
        if (_bookmarksPortalNode != null && _mainWindow != null)
        {
            _mainWindow.RemovePortal(_breadcrumb.Control, _bookmarksPortalNode);
            _bookmarksPortalNode = null;
            _bookmarksPortal = null;
        }
    }

    private CXFiles.UI.Portals.PathCompletionPortal? _pathPortal;
    private LayoutNode? _pathPortalNode;

    // Tracks whether the user has actively navigated (Up/Down/PgUp/PgDn) the
    // completion portal since it opened. Determines Enter behavior:
    //   - not navigated → Enter commits the typed path (browser URL-bar style)
    //   - navigated     → Enter picks the highlighted candidate
    internal bool _pathPortalUserNavigated;

    // Debounced auto-open of the path completion portal: a short pause after
    // typing (200 ms) opens the portal without firing on every keystroke.
    private const int PathDebounceMs = 200;
    private System.Threading.Timer? _pathDebounceTimer;
    private string _pathDebouncePending = string.Empty;

    private void SchedulePathDebounce(string text)
    {
        _pathDebouncePending = text;
        if (_pathDebounceTimer == null)
        {
            _pathDebounceTimer = new System.Threading.Timer(_ =>
            {
                _ws.EnqueueOnUIThread(() =>
                {
                    // Only open if edit mode is still active and the portal
                    // hasn't been opened by another path (Tab/Ctrl+Space).
                    if (!_breadcrumb.InEditMode) return;
                    if (_pathPortal != null) return;
                    ShowOrUpdatePathPortal(_pathDebouncePending);
                });
            }, null, System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
        }
        _pathDebounceTimer.Change(PathDebounceMs, System.Threading.Timeout.Infinite);
    }

    private void CancelPathDebounce()
    {
        _pathDebounceTimer?.Change(
            System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
    }

    private void ShowOrUpdatePathPortal(string text)
    {
        if (_mainWindow == null) return;

        var (parent, fragment) = CXFiles.UI.PathCompleter.SplitFragment(text);
        if (string.IsNullOrEmpty(parent)) { DismissPathPortal(); return; }

        var candidates = CXFiles.UI.PathCompleter.Complete(
            parent, fragment, _config.Config.ShowHiddenFiles);
        if (candidates.Count == 0) { DismissPathPortal(); return; }

        // Always dismiss-and-recreate so the portal's width, height, and border
        // are sized freshly for the current candidate list. Reusing via
        // UpdateCandidates left stale bounds that clipped the border.
        DismissPathPortal();

        int anchorX = _mainWindow.Left + 2;
        int anchorY = _mainWindow.Top + 2;

        // Fresh portal — user hasn't navigated yet.
        _pathPortalUserNavigated = false;

        _pathPortal = new CXFiles.UI.Portals.PathCompletionPortal(
            candidates, anchorX, anchorY, _mainWindow);
        _pathPortal.Container = _mainWindow;
        _pathPortalNode = _mainWindow.CreatePortal(_breadcrumb.Control, _pathPortal);

        _pathPortal.CandidateSelected += (_, candidate) =>
        {
            var (p2, _) = CXFiles.UI.PathCompleter.SplitFragment(_breadcrumb.EditInput.Input);
            var newInput = Path.Combine(p2, candidate) + Path.DirectorySeparatorChar;
            _breadcrumb.EditInput.Input = newInput;
            DismissPathPortal();
            // If the newly drilled-into directory has children to complete,
            // reopen the portal so the user can keep navigating without
            // pressing Tab again.
            ShowOrUpdatePathPortal(newInput);
        };

        _pathPortal.Dismissed += (_, _) => DismissPathPortal();
    }

    private void DismissPathPortal()
    {
        if (_pathPortalNode != null && _mainWindow != null)
        {
            _mainWindow.RemovePortal(_breadcrumb.Control, _pathPortalNode);
            _pathPortalNode = null;
            _pathPortal = null;
        }
    }

    private void SubmitPath(string text)
    {
        var resolved = CXFiles.UI.PathCompleter.Resolve(text, ActiveTab.Path);
        if (Directory.Exists(resolved))
        {
            DismissPathPortal();
            _breadcrumb.ExitEditMode();
            NavigateTo(resolved);
            ReturnFocusToFileList();
        }
        else
        {
            _ws.NotificationStateService.ShowNotification(
                "Go to path",
                $"Path not found: {resolved}",
                SharpConsoleUI.Core.NotificationSeverity.Warning);
        }
    }

    private void ReturnFocusToFileList()
    {
        SharpConsoleUI.Extensions.WindowControlExtensions.RequestFocus(
            ActiveFileList.Control, SharpConsoleUI.Controls.FocusReason.Keyboard);
    }

    private void HandleBreadcrumbTab()
    {
        CancelPathDebounce();
        var text = _breadcrumb.EditInput.Input;
        var (parent, fragment) = CXFiles.UI.PathCompleter.SplitFragment(text);
        if (string.IsNullOrEmpty(parent)) return;

        var candidates = CXFiles.UI.PathCompleter.Complete(
            parent, fragment, _config.Config.ShowHiddenFiles);

        if (candidates.Count == 0) return;

        if (candidates.Count == 1)
        {
            _breadcrumb.EditInput.Input = Path.Combine(parent, candidates[0]) + Path.DirectorySeparatorChar;
            DismissPathPortal();
            return;
        }

        var lcp = CXFiles.UI.PathCompleter.LongestCommonPrefix(candidates);
        if (lcp.Length > fragment.Length)
            _breadcrumb.EditInput.Input = Path.Combine(parent, lcp);

        ShowOrUpdatePathPortal(_breadcrumb.EditInput.Input);
    }

    private async Task RenameFolderAsync(string path)
    {
        var name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(name)) return;
        var newName = await UI.Modals.RenameModal.ShowAsync(_ws, name, _mainWindow);
        if (newName != null)
        {
            try
            {
                _fs.Rename(path, newName);
                _folderTree.Refresh();
                Refresh();
            }
            catch (Exception ex)
            {
                _ws.NotificationStateService.ShowNotification(
                    "Error", $"Rename failed: {ex.Message}", SharpConsoleUI.Core.NotificationSeverity.Danger);
            }
        }
    }

    private async Task DeletePathAsync(string path)
    {
        var name = Path.GetFileName(path);
        var isDir = _fs.DirectoryExists(path);
        var type = isDir ? "folder" : "file";
        var action = await UI.Modals.DeleteConfirmModal.ShowAsync(
            _ws, $"Delete {type} \"{name}\"?", _trash.IsAvailable, _mainWindow);
        if (action == UI.Modals.DeleteAction.Cancel) return;

        bool useTrash = action == UI.Modals.DeleteAction.Trash;
        try
        {
            if (useTrash)
                await _trash.TrashAsync(path, CancellationToken.None);
            else
                await _fs.DeleteAsync(path, isDir, CancellationToken.None);
            _folderTree.Refresh();
            Refresh();
        }
        catch (Exception ex)
        {
            _ws.NotificationStateService.ShowNotification(
                "Error", $"Delete failed: {ex.Message}", SharpConsoleUI.Core.NotificationSeverity.Danger);
        }
    }

    private async Task ShowPropertiesForPathAsync(string path)
    {
        try
        {
            var entry = _fs.GetFileInfo(path);
            await UI.Modals.PropertiesModal.ShowAsync(_ws, _fs, entry, _mainWindow);
        }
        catch { }
    }

    private async Task PasteIntoAsync(string targetFolder)
    {
        if (!_clipboard.HasContent) return;
        var isCut = _clipboard.Action == Services.ClipboardAction.Cut;
        var paths = _clipboard.Paths.ToList();
        var opType = isCut ? Services.OperationType.Move : Services.OperationType.Copy;
        var desc = $"{(isCut ? "Moving" : "Copying")} {paths.Count} item{(paths.Count == 1 ? "" : "s")} to {Path.GetFileName(targetFolder)}";
        var op = _operations.StartOperation(opType, desc);
        UpdateStatusLine();

        _ = Task.Run(async () =>
        {
            try
            {
                foreach (var src in paths)
                {
                    op.Cts.Token.ThrowIfCancellationRequested();
                    op.CurrentFile = Path.GetFileName(src);
                    var dest = Path.Combine(targetFolder, Path.GetFileName(src));
                    if (isCut)
                        await _fs.MoveAsync(src, dest, false, null, op.Cts.Token);
                    else
                        await _fs.CopyAsync(src, dest, false, null, op.Cts.Token);
                }
                _operations.CompleteOperation(op, Services.OperationStatus.Completed);
                if (isCut) _clipboard.Clear();
                _ws.EnqueueOnUIThread(() => { _folderTree.Refresh(); Refresh(); });
            }
            catch (OperationCanceledException)
            {
                _operations.CompleteOperation(op, Services.OperationStatus.Cancelled);
            }
            catch (Exception ex)
            {
                _operations.CompleteOperation(op, Services.OperationStatus.Failed, ex.Message);
            }
        });
    }

    private async Task NewItemInAsync(string parentFolder, bool isDirectory)
    {
        var result = await UI.Modals.NewItemModal.ShowAsync(_ws, isDirectory, _mainWindow);
        if (result?.Name != null)
        {
            var path = Path.Combine(parentFolder, result.Name);
            try
            {
                if (result.IsDirectory)
                    _fs.CreateDirectory(path);
                else
                    _fs.CreateFile(path);
                _folderTree.Refresh();
                Refresh();
            }
            catch (Exception ex)
            {
                _ws.NotificationStateService.ShowNotification(
                    "Error", $"Create failed: {ex.Message}", SharpConsoleUI.Core.NotificationSeverity.Danger);
            }
        }
    }

    private void SaveColumnWidths()
    {
        var columns = _mainGrid.Columns;
        if (columns.Count >= 3)
        {
            if (columns[0].Width is int treeW)
                _config.Config.TreePanelWidth = treeW;
            if (columns[2].Width is int detailW)
                _config.Config.DetailPanelWidth = detailW;
            _config.Save();
        }
    }

    private void Refresh()
    {
        // If a search is active on the current tab, do NOT refresh the file list.
        // Refresh() calls Navigate() which would swap the data source back to the
        // browsing FileDataSource and wipe the search results out from under the
        // user — typically triggered silently by a FileSystemWatcher event.
        if (ActiveTab.Search.Restore == null)
            ActiveFileList.Refresh();
        _folderTree.RefreshNode(_folderTree.Control.SelectedNode);
        UpdateStatusLine();
    }

    private void ShowOperationsPortal()
    {
        if (_mainWindow == null) return;
        DismissOperationsPortal();

        // Anchor at bottom-left of the status bar area (screen coords)
        int anchorX = _mainWindow.Left + 2;
        int anchorY = _mainWindow.Top + _mainWindow.Height - 3;

        _opsPortal = new UI.OperationsPortal(_ws, _operations, anchorX, anchorY,
            _mainWindow);
        _opsPortal.Container = _mainWindow;
        _opsPortalNode = _mainWindow.CreatePortal(_statusLine.Control, _opsPortal);

        _opsPortal.Dismissed += (_, _) => DismissOperationsPortal();
        _opsPortal.DismissRequested += (_, _) => DismissOperationsPortal();
    }

    private void ShowClipboardPortal()
    {
        if (_mainWindow == null || !_clipboard.HasContent) return;
        DismissClipboardPortal();

        int anchorX = _mainWindow.Left + 2;
        int anchorY = _mainWindow.Top + _mainWindow.Height - 3;

        _clipPortal = new UI.ClipboardPortal(_clipboard, anchorX, anchorY,
            _mainWindow);
        _clipPortal.Container = _mainWindow;
        _clipPortalNode = _mainWindow.CreatePortal(_statusLine.Control, _clipPortal);

        _clipPortal.Dismissed += (_, _) => DismissClipboardPortal();
        _clipPortal.ClearRequested += (_, _) =>
        {
            _clipboard.Clear();
            DismissClipboardPortal();
            UpdateStatusLine();
            UpdateToolbar();
        };
        _clipPortal.RemoveRequested += path =>
        {
            _clipboard.Paths.Remove(path);
            if (_clipboard.Paths.Count == 0)
            {
                _clipboard.Clear();
                DismissClipboardPortal();
            }
            else
            {
                DismissClipboardPortal();
                ShowClipboardPortal();
            }
            UpdateStatusLine();
            UpdateToolbar();
        };
    }

    private void DismissClipboardPortal()
    {
        if (_clipPortalNode != null && _mainWindow != null)
        {
            _mainWindow.RemovePortal(_statusLine.Control, _clipPortalNode);
            _clipPortalNode = null;
            _clipPortal = null;
        }
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
            ActiveFileList.Control.RowCount,
            checkedCount,
            _detailVisible,
            _config.Config.ShowHiddenFiles,
            _operations.ActiveCount,
            _operations.TotalCount,
            _operations.FailedCount,
            _clipboard.HasContent ? _clipboard.Paths.Count : 0,
            _clipboard.HasContent ? (_clipboard.Action == Services.ClipboardAction.Cut ? "Cut" : "Copy") : null,
            _terminal != null,
            _terminalInMiddle);
    }

    private int GetCheckedCount()
    {
        var indices = ActiveFileList.Control.GetSelectedIndices();
        return indices.Count > 0 ? indices.Count : (ActiveFileList.GetSelectedEntry() != null ? 1 : 0);
    }

    private List<FileEntry> GetCheckedEntries()
    {
        var result = new List<FileEntry>();
        var indices = ActiveFileList.Control.GetSelectedIndices();
        if (indices.Count > 0)
        {
            foreach (var idx in indices)
            {
                var entry = ActiveFileList.GetEntryAt(idx);
                if (entry != null) result.Add(entry);
            }
        }
        else
        {
            var selected = ActiveFileList.GetSelectedEntry();
            if (selected != null) result.Add(selected);
        }
        return result;
    }

    private void UpdateToolbar()
    {
        if (_toolbar == null) return;
        _toolbar.Clear();

        if (ActiveTab.ViewingTrash)
        {
            UpdateTrashToolbar();
            return;
        }

        var checkedCount = GetCheckedCount();
        var hasSelection = checkedCount > 0;
        var multiSelect = checkedCount > 1;

        AddToolbarButton("◈ Open [grey50]Enter[/]", OpenSelected);
        AddToolbarButton("↑ Up [grey50]Bksp[/]", NavigateUp);
        if (_tabs.Count < _config.Config.MaxTabs)
            AddToolbarButton("❒ New Tab [grey50]^T[/]", NewTab);
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
        else
        {
            AddToolbarButton("⊞ Folder Props [grey50]F4[/]", () => _ = ShowPropertiesAsync());
        }
        if (_clipboard.HasContent)
        {
            AddToolbarButton($"⊡ Paste ({_clipboard.Paths.Count}) [grey50]^V[/]",
                () => _ = PasteAsync());
        }
        _toolbar.AddItem(new SeparatorControl());
        AddToolbarButton("↻ Refresh [grey50]F5[/]", Refresh);
    }

    private void UpdateTrashToolbar()
    {
        var hasSelection = ActiveFileList.Control.SelectedRowIndex >= 0;

        if (hasSelection)
        {
            AddToolbarButton("↩ Restore [grey50]Enter[/]", () => _ = RestoreFromTrashAsync());
            AddToolbarButton("✕ Delete Permanently [grey50]Del[/]", () => _ = DeleteFromTrashAsync());
            _toolbar.AddItem(new SeparatorControl());
        }
        AddToolbarButton("⊗ Empty Trash", () => _ = EmptyTrashAsync());
        _toolbar.AddItem(new SeparatorControl());
        AddToolbarButton("↑ Back [grey50]Bksp[/]", () => NavigateTo(ActiveTab.Path));
        if (_tabs.Count < _config.Config.MaxTabs)
            AddToolbarButton("❒ New Tab [grey50]^T[/]", NewTab);
        _toolbar.AddItem(new SeparatorControl());
        AddToolbarButton("↻ Refresh [grey50]F5[/]", Refresh);
    }

    private async Task RestoreFromTrashAsync()
    {
        var idx = ActiveFileList.Control.SelectedRowIndex;
        if (ActiveFileList.Control.DataSource is not UI.Components.TrashDataSource source) return;
        var entry = source.GetEntry(idx);
        if (entry == null) return;

        var op = _operations.StartOperation(Services.OperationType.Move, $"Restoring {entry.TrashedName}");
        try
        {
            await _trash.RestoreAsync(entry.TrashedName, CancellationToken.None);
            _operations.CompleteOperation(op, Services.OperationStatus.Completed);
        }
        catch (UnauthorizedAccessException) when (_sudo.IsSupported && _config.Config.AllowSudoElevation)
        {
            _operations.RemoveOperation(op);
            PromptSudoRestore(entry);
            return;
        }
        catch (Exception ex)
        {
            _operations.CompleteOperation(op, Services.OperationStatus.Failed, ex.Message);
        }
        NavigateToTrash();
    }

    private async Task DeleteFromTrashAsync()
    {
        var idx = ActiveFileList.Control.SelectedRowIndex;
        if (ActiveFileList.Control.DataSource is not UI.Components.TrashDataSource source) return;
        var entry = source.GetEntry(idx);
        if (entry == null) return;

        var confirmed = await ConfirmModal.ShowAsync(_ws, "Delete Permanently",
            $"Permanently delete \"{entry.TrashedName}\"? This cannot be undone.", _mainWindow);
        if (!confirmed) return;

        try
        {
            var path = Path.Combine(_trash.TrashPath, "files", entry.TrashedName);
            if (Directory.Exists(path)) Directory.Delete(path, true);
            else if (File.Exists(path)) File.Delete(path);

            var infoPath = Path.Combine(_trash.TrashPath, "info", entry.TrashedName + ".trashinfo");
            if (File.Exists(infoPath)) File.Delete(infoPath);
        }
        catch (Exception ex)
        {
            _ws.NotificationStateService.ShowNotification(
                "Error", $"Delete failed: {ex.Message}", SharpConsoleUI.Core.NotificationSeverity.Danger);
        }
        NavigateToTrash();
    }

    private void PromptSudoRestore(Models.TrashEntry entry)
    {
        var sourcePath = Path.Combine(_trash.TrashPath, "files", entry.TrashedName);
        var desc = $"Restore \"{entry.TrashedName}\" to {entry.OriginalPath}\n\nThis requires elevated privileges (sudo mv).";

        UI.Modals.SudoDialog.Show(desc, _ws, result =>
        {
            if (result.Cancelled || !result.Success) return;

            var op = _operations.StartOperation(Services.OperationType.Move, $"Restoring {entry.TrashedName} (sudo)");
            UpdateStatusLine();

            _ = Task.Run(async () =>
            {
                try
                {
                    // Ensure destination directory exists
                    var destDir = Path.GetDirectoryName(entry.OriginalPath);
                    if (destDir != null && !Directory.Exists(destDir))
                    {
                        var (mkOk, mkErr) = await _sudo.CreateDirectoryAsync(destDir, result.Password, CancellationToken.None);
                        if (!mkOk)
                            throw new InvalidOperationException(mkErr ?? "Failed to create destination directory");
                    }

                    // Move from trash to original location
                    var (ok, err) = await _sudo.MoveAsync(sourcePath, entry.OriginalPath, result.Password, CancellationToken.None);
                    if (!ok)
                        throw new InvalidOperationException(err ?? "Restore failed");

                    // Clean up trash info file (user-owned, no sudo needed)
                    var infoPath = Path.Combine(_trash.TrashPath, "info", entry.TrashedName + ".trashinfo");
                    try { File.Delete(infoPath); } catch { }

                    _operations.CompleteOperation(op, Services.OperationStatus.Completed);
                    _ws.EnqueueOnUIThread(NavigateToTrash);
                }
                catch (Exception ex)
                {
                    _operations.CompleteOperation(op, Services.OperationStatus.Failed, ex.Message);
                }
                _ws.EnqueueOnUIThread(UpdateStatusLine);
            });
        });
    }

    private async Task EmptyTrashAsync()
    {
        var count = _trash.TrashCount;
        if (count == 0) return;

        var confirmed = await ConfirmModal.ShowAsync(_ws, "Empty Trash",
            $"Permanently delete all {count} items in trash? This cannot be undone.", _mainWindow);
        if (!confirmed) return;

        var op = _operations.StartOperation(Services.OperationType.Delete, $"Emptying trash ({count} items)");
        try
        {
            await _trash.EmptyTrashAsync(CancellationToken.None);
            _operations.CompleteOperation(op, Services.OperationStatus.Completed);
        }
        catch (Exception ex)
        {
            _operations.CompleteOperation(op, Services.OperationStatus.Failed, ex.Message);
        }
        NavigateToTrash();
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
