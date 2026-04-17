using SharpConsoleUI;
using SharpConsoleUI.Events;
using CXFiles.Models;
using CXFiles.UI.Modals;

namespace CXFiles.App;

public partial class CXFilesApp
{
    private void OnGlobalKeyPressed(object? sender, KeyPressedEventArgs e)
    {
        var key = e.KeyInfo;
        bool ctrl = key.Modifiers.HasFlag(ConsoleModifiers.Control);
        bool shift = key.Modifiers.HasFlag(ConsoleModifiers.Shift);
        bool alt = key.Modifiers.HasFlag(ConsoleModifiers.Alt);

        // When the terminal has focus, only allow app-level keys through.
        // The PreviewKeyPressed handler (Layout.cs) already intercepted these
        // keys before the terminal could convert them to escape sequences.
        // For all other keys, let the terminal own them.
        if (_terminal != null && _terminal.HasFocus)
        {
            switch (key.Key)
            {
                case ConsoleKey.F1:
                case ConsoleKey.F6:
                case ConsoleKey.F7:
                case ConsoleKey.F8:
                case ConsoleKey.Oem3 when ctrl:
                case ConsoleKey.Q when ctrl:
                    break; // fall through to app handlers
                default:
                    return; // terminal owns this key
            }
        }

        // When the search bar has focus, defer text-editing keys to it.
        // Without this guard, e.g. Backspace would call NavigateUp() and
        // mutate tab.Path mid-search — the next walker would start from
        // the parent folder instead of the original search root.
        if (ActiveTab.SearchBar.HasFocus)
        {
            switch (key.Key)
            {
                case ConsoleKey.Backspace:
                case ConsoleKey.Delete:
                case ConsoleKey.LeftArrow:
                case ConsoleKey.RightArrow:
                case ConsoleKey.Home:
                case ConsoleKey.End:
                    return; // prompt owns these
                case ConsoleKey.C when ctrl:
                case ConsoleKey.X when ctrl:
                case ConsoleKey.V when ctrl:
                case ConsoleKey.A when ctrl:
                    return; // prompt owns clipboard/select-all
            }
            // Escape and Ctrl+F still need to flow through to our handlers below
            // (Escape cancels the search; Ctrl+F is harmless re-focus).
        }

        switch (key.Key)
        {
            case ConsoleKey.Backspace when !ctrl:
                NavigateUp();
                e.Handled = true;
                break;

            case ConsoleKey.F1:
                _ = ShowHelpAsync();
                e.Handled = true;
                break;

            case ConsoleKey.F2:
                _ = RenameSelectedAsync();
                e.Handled = true;
                break;

            case ConsoleKey.F3:
                ToggleDetailPanel();
                e.Handled = true;
                break;

            case ConsoleKey.F5:
                Refresh();
                e.Handled = true;
                break;

            case ConsoleKey.Delete:
                _ = DeleteSelectedAsync();
                e.Handled = true;
                break;

            case ConsoleKey.F4: // Properties
                _ = ShowPropertiesAsync();
                e.Handled = true;
                break;

            case ConsoleKey.N when ctrl && !shift:
                _ = NewItemAsync(isDirectory: false);
                e.Handled = true;
                break;

            case ConsoleKey.N when ctrl && shift:
                _ = NewItemAsync(isDirectory: true);
                e.Handled = true;
                break;

            case ConsoleKey.OemComma when ctrl: // Ctrl+,
            case ConsoleKey.O when ctrl:       // Ctrl+O for options
                _ = ShowOptionsAsync();
                e.Handled = true;
                break;

            case ConsoleKey.C when ctrl: // Ctrl+C copy
                CopySelected();
                e.Handled = true;
                break;

            case ConsoleKey.X when ctrl: // Ctrl+X cut
                CutSelected();
                e.Handled = true;
                break;

            case ConsoleKey.V when ctrl: // Ctrl+V paste
                _ = PasteAsync();
                e.Handled = true;
                break;

            case ConsoleKey.F when ctrl: // Ctrl+F focus search bar
                ActiveTab.SearchBar.Focus();
                e.Handled = true;
                break;

            case ConsoleKey.Escape when ActiveTab.Search.Restore != null:
                CancelAndRestore(ActiveTab);
                e.Handled = true;
                break;

            case ConsoleKey.H when ctrl: // Ctrl+H toggle hidden
                _config.Config.ShowHiddenFiles = !_config.Config.ShowHiddenFiles;
                foreach (var t in _tabs)
                    t.FileList.SetShowHidden(_config.Config.ShowHiddenFiles);
                _config.Save();
                e.Handled = true;
                break;

            case ConsoleKey.P when ctrl: // Ctrl+P operations portal
                ShowOperationsPortal();
                e.Handled = true;
                break;

            case ConsoleKey.B when ctrl: // Ctrl+B clipboard portal
                if (_clipboard.HasContent) ShowClipboardPortal();
                e.Handled = true;
                break;

            case ConsoleKey.E when ctrl: // Ctrl+E open in editor
                var editorEntry = ActiveFileList.GetSelectedEntry();
                if (editorEntry != null && !editorEntry.IsDirectory)
                    OpenInEditor(editorEntry.FullPath);
                e.Handled = true;
                break;

            case ConsoleKey.Oem3 when ctrl: // Ctrl+` toggle terminal
            case ConsoleKey.F7: // F7 toggle terminal (fallback)
                if (!ActiveTab.ViewingTrash)
                    OpenOrSwitchTerminal();
                e.Handled = true;
                break;

            case ConsoleKey.F8: // Toggle terminal position (right panel / center)
                if (_terminal != null)
                    ToggleTerminalPosition();
                e.Handled = true;
                break;

            case ConsoleKey.F6: // Toggle focus to/from terminal
                if (_terminal != null)
                {
                    if (_terminal.HasFocus)
                        SharpConsoleUI.Extensions.WindowControlExtensions.RequestFocus(
                            ActiveFileList.Control, SharpConsoleUI.Controls.FocusReason.Keyboard);
                    else
                        _mainWindow?.FocusManager?.SetFocus(
                            _terminal, SharpConsoleUI.Controls.FocusReason.Keyboard);
                }
                e.Handled = true;
                break;

            case ConsoleKey.T when ctrl: // Ctrl+T new tab
                NewTab();
                e.Handled = true;
                break;

            case ConsoleKey.W when ctrl: // Ctrl+W close tab
                CloseActiveTab();
                e.Handled = true;
                break;

            case ConsoleKey.RightArrow when alt: // Alt+Right next tab
                if (_tabControl.TabCount > 1)
                    _tabControl.NextTab();
                e.Handled = true;
                break;

            case ConsoleKey.LeftArrow when alt: // Alt+Left prev tab
                if (_tabControl.TabCount > 1)
                    _tabControl.PreviousTab();
                e.Handled = true;
                break;

            case ConsoleKey.D1 when ctrl: JumpToTab(0); e.Handled = true; break;
            case ConsoleKey.D2 when ctrl: JumpToTab(1); e.Handled = true; break;
            case ConsoleKey.D3 when ctrl: JumpToTab(2); e.Handled = true; break;
            case ConsoleKey.D4 when ctrl: JumpToTab(3); e.Handled = true; break;
            case ConsoleKey.D5 when ctrl: JumpToTab(4); e.Handled = true; break;

            case ConsoleKey.Q when ctrl:
                _ws.Shutdown();
                e.Handled = true;
                break;
        }
    }

    private async Task ShowOptionsAsync()
    {
        var changed = await UI.Modals.OptionsModal.ShowAsync(_ws, _config, _mainWindow);
        foreach (var t in _tabs)
            t.FileList.SetShowHidden(_config.Config.ShowHiddenFiles);
        Refresh();
    }

    private async Task DeleteSelectedAsync()
    {
        var entries = GetCheckedEntries();
        if (entries.Count == 0) return;

        string message;
        if (entries.Count == 1)
        {
            var type = entries[0].IsDirectory ? "folder" : "file";
            message = $"Delete {type} \"{entries[0].Name}\"?";
        }
        else
        {
            message = $"Delete {entries.Count} items?";
        }

        var action = await DeleteConfirmModal.ShowAsync(_ws, message, _trash.IsAvailable, _mainWindow);
        if (action == DeleteAction.Cancel) return;

        bool useTrash = action == DeleteAction.Trash;
        var verb = useTrash ? "Trashing" : "Deleting";
        var desc = entries.Count == 1 ? $"{verb} {entries[0].Name}" : $"{verb} {entries.Count} items";
        var op = _operations.StartOperation(Services.OperationType.Delete, desc);
        UpdateStatusLine();

        _ = Task.Run(async () =>
        {
            try
            {
                foreach (var entry in entries)
                {
                    op.Cts.Token.ThrowIfCancellationRequested();
                    op.CurrentFile = entry.Name;

                    if (useTrash)
                        await _trash.TrashAsync(entry.FullPath, op.Cts.Token);
                    else
                        await _fs.DeleteAsync(entry.FullPath, entry.IsDirectory, op.Cts.Token);
                }
                _operations.CompleteOperation(op, Services.OperationStatus.Completed);
                _ws.EnqueueOnUIThread(Refresh);
            }
            catch (OperationCanceledException)
            {
                _operations.CompleteOperation(op, Services.OperationStatus.Cancelled);
            }
            catch (UnauthorizedAccessException) when (_sudo.IsSupported)
            {
                if (useTrash)
                {
                    _operations.CompleteOperation(op, Services.OperationStatus.Failed, "Permission denied");
                    _ws.EnqueueOnUIThread(() => _ws.NotificationStateService.ShowNotification(
                        "Cannot trash", "Insufficient permissions to move to trash. Use Delete to permanently remove with sudo.",
                        SharpConsoleUI.Core.NotificationSeverity.Warning));
                }
                else
                {
                    _operations.RemoveOperation(op);
                    var sudoEntries = entries;
                    _ws.EnqueueOnUIThread(() => PromptSudoDelete(sudoEntries));
                }
            }
            catch (Exception ex)
            {
                _operations.CompleteOperation(op, Services.OperationStatus.Failed, ex.Message);
            }
            _ws.EnqueueOnUIThread(UpdateStatusLine);
        });
    }

    private void PromptSudoDelete(List<FileEntry> entries)
    {
        var paths = entries.Select(e => e.FullPath).ToList();
        var commonDir = Path.GetDirectoryName(paths[0]) ?? "";
        string what;
        if (entries.Count == 1)
        {
            var type = entries[0].IsDirectory ? "folder" : "file";
            what = $"{type} \"{entries[0].Name}\"";
        }
        else
            what = $"{entries.Count} items";

        var sudoDesc = $"Permanently delete {what} in {commonDir}\n\nThis requires elevated privileges (sudo rm).";

        UI.Modals.SudoDialog.Show(sudoDesc, _ws, result =>
        {
            if (result.Cancelled || !result.Success) return;

            var desc = entries.Count == 1
                ? $"Deleting {entries[0].Name} (sudo)"
                : $"Deleting {entries.Count} items (sudo)";
            var op = _operations.StartOperation(Services.OperationType.Delete, desc);
            UpdateStatusLine();

            _ = Task.Run(async () =>
            {
                try
                {
                    foreach (var entry in entries)
                    {
                        op.Cts.Token.ThrowIfCancellationRequested();
                        op.CurrentFile = entry.Name;

                        var (ok, err) = await _sudo.DeleteAsync(
                            entry.FullPath, entry.IsDirectory, result.Password, op.Cts.Token);
                        if (!ok)
                            throw new InvalidOperationException(err ?? "sudo delete failed");
                    }
                    _operations.CompleteOperation(op, Services.OperationStatus.Completed);
                    _ws.EnqueueOnUIThread(Refresh);
                }
                catch (OperationCanceledException)
                {
                    _operations.CompleteOperation(op, Services.OperationStatus.Cancelled);
                }
                catch (Exception ex)
                {
                    _operations.CompleteOperation(op, Services.OperationStatus.Failed, ex.Message);
                }
                _ws.EnqueueOnUIThread(UpdateStatusLine);
            });
        });
    }

    private async Task RenameSelectedAsync()
    {
        var entry = ActiveFileList.GetSelectedEntry();
        if (entry == null) return;

        var newName = await RenameModal.ShowAsync(_ws, entry.Name, _mainWindow);
        if (newName != null)
        {
            var newPath = Path.Combine(Path.GetDirectoryName(entry.FullPath)!, newName);
            try
            {
                _fs.Rename(entry.FullPath, newName);
                Refresh();
            }
            catch (UnauthorizedAccessException) when (_sudo.IsSupported)
            {
                var desc = $"Rename \"{entry.Name}\" to \"{newName}\" in {Path.GetDirectoryName(entry.FullPath)}\n\nThis requires elevated privileges (sudo mv).";
                var capturedPath = entry.FullPath;
                UI.Modals.SudoDialog.Show(desc, _ws, result =>
                {
                    if (result.Cancelled || !result.Success) return;
                    _ = Task.Run(async () =>
                    {
                        var (ok, err) = await _sudo.RenameAsync(capturedPath, newPath, result.Password, CancellationToken.None);
                        _ws.EnqueueOnUIThread(() =>
                        {
                            if (ok) Refresh();
                            else _ws.NotificationStateService.ShowNotification(
                                "Error", $"Rename failed: {err}", SharpConsoleUI.Core.NotificationSeverity.Danger);
                        });
                    });
                });
            }
            catch (Exception ex)
            {
                _ws.NotificationStateService.ShowNotification(
                    "Error", $"Rename failed: {ex.Message}", SharpConsoleUI.Core.NotificationSeverity.Danger);
            }
        }
    }

    private void CopySelected()
    {
        var entries = GetCheckedEntries();
        if (entries.Count == 0) return;
        _clipboard.SetCopy(entries.Select(e => e.FullPath));
        UpdateToolbar();
        UpdateStatusLine();
    }

    private void CutSelected()
    {
        var entries = GetCheckedEntries();
        if (entries.Count == 0) return;
        _clipboard.SetCut(entries.Select(e => e.FullPath));
        UpdateToolbar();
        UpdateStatusLine();
    }

    private async Task PasteAsync()
    {
        if (!_clipboard.HasContent) return;

        var isCut = _clipboard.Action == Services.ClipboardAction.Cut;
        var opType = isCut ? Services.OperationType.Move : Services.OperationType.Copy;
        var paths = _clipboard.Paths.ToList();
        var desc = $"{(isCut ? "Moving" : "Copying")} {paths.Count} item{(paths.Count > 1 ? "s" : "")}";
        var op = _operations.StartOperation(opType, desc);
        op.BytesTotal = paths.Count;
        UpdateStatusLine();

        if (isCut) _clipboard.Clear();

        _ = Task.Run(async () =>
        {
            try
            {
                for (int i = 0; i < paths.Count; i++)
                {
                    op.Cts.Token.ThrowIfCancellationRequested();
                    var sourcePath = paths[i];
                    var name = Path.GetFileName(sourcePath);
                    var destPath = Path.Combine(ActiveTab.Path, name);
                    op.CurrentFile = name;
                    _operations.ReportProgress(op, i, paths.Count);

                    if (isCut)
                        await _fs.MoveAsync(sourcePath, destPath, false, op.Cts.Token);
                    else
                        await _fs.CopyAsync(sourcePath, destPath, false, null, op.Cts.Token);
                }
                _operations.ReportProgress(op, paths.Count, paths.Count);
                _operations.CompleteOperation(op, Services.OperationStatus.Completed);
                _ws.EnqueueOnUIThread(Refresh);
            }
            catch (OperationCanceledException)
            {
                _operations.CompleteOperation(op, Services.OperationStatus.Cancelled);
            }
            catch (UnauthorizedAccessException) when (_sudo.IsSupported)
            {
                _operations.RemoveOperation(op);
                var sudoPaths = paths;
                var sudoCut = isCut;
                var destDir = ActiveTab.Path;
                _ws.EnqueueOnUIThread(() => PromptSudoPaste(sudoPaths, sudoCut, destDir));
            }
            catch (Exception ex)
            {
                _operations.CompleteOperation(op, Services.OperationStatus.Failed, ex.Message);
            }
            _ws.EnqueueOnUIThread(UpdateStatusLine);
        });
    }

    private void PromptSudoPaste(List<string> paths, bool isCut, string destDir)
    {
        var verb = isCut ? "Move" : "Copy";
        var cmd = isCut ? "sudo mv" : "sudo cp";
        var what = paths.Count == 1 ? $"\"{Path.GetFileName(paths[0])}\"" : $"{paths.Count} items";
        var sudoDesc = $"{verb} {what} to {destDir}\n\nThis requires elevated privileges ({cmd}).";

        UI.Modals.SudoDialog.Show(sudoDesc, _ws, result =>
        {
            if (result.Cancelled || !result.Success) return;

            var desc = $"{verb}ing {paths.Count} item{(paths.Count > 1 ? "s" : "")} (sudo)";
            var opType = isCut ? Services.OperationType.Move : Services.OperationType.Copy;
            var op = _operations.StartOperation(opType, desc);
            UpdateStatusLine();

            _ = Task.Run(async () =>
            {
                try
                {
                    foreach (var sourcePath in paths)
                    {
                        op.Cts.Token.ThrowIfCancellationRequested();
                        var destPath = Path.Combine(destDir, Path.GetFileName(sourcePath));
                        op.CurrentFile = Path.GetFileName(sourcePath);

                        var (ok, err) = isCut
                            ? await _sudo.MoveAsync(sourcePath, destPath, result.Password, op.Cts.Token)
                            : await _sudo.CopyAsync(sourcePath, destPath, result.Password, op.Cts.Token);
                        if (!ok)
                            throw new InvalidOperationException(err ?? $"{verb} failed");
                    }
                    _operations.CompleteOperation(op, Services.OperationStatus.Completed);
                    _ws.EnqueueOnUIThread(Refresh);
                }
                catch (OperationCanceledException)
                {
                    _operations.CompleteOperation(op, Services.OperationStatus.Cancelled);
                }
                catch (Exception ex)
                {
                    _operations.CompleteOperation(op, Services.OperationStatus.Failed, ex.Message);
                }
                _ws.EnqueueOnUIThread(UpdateStatusLine);
            });
        });
    }

    private async Task ShowPropertiesAsync()
    {
        var entry = ActiveFileList.GetSelectedEntry();
        if (entry == null)
        {
            // Null selection → show properties for the current folder.
            try { entry = _fs.GetFileInfo(ActiveTab.Path); }
            catch { return; }
        }
        await PropertiesModal.ShowAsync(_ws, _fs, entry, _mainWindow);
    }

    private async Task ShowHelpAsync()
    {
        await HelpModal.ShowAsync(_ws, _mainWindow);
    }

    private async Task NewItemAsync(bool isDirectory)
    {
        var result = await NewItemModal.ShowAsync(_ws, isDirectory, _mainWindow);
        if (result?.Name != null)
        {
            var path = Path.Combine(ActiveTab.Path, result.Name);
            var isDir = result.IsDirectory;
            try
            {
                if (isDir)
                    _fs.CreateDirectory(path);
                else
                    _fs.CreateFile(path);
                Refresh();
            }
            catch (UnauthorizedAccessException) when (_sudo.IsSupported)
            {
                var type = isDir ? "folder" : "file";
                var cmd = isDir ? "sudo mkdir" : "sudo touch";
                var desc = $"Create {type} \"{result.Name}\" in {ActiveTab.Path}\n\nThis requires elevated privileges ({cmd}).";
                UI.Modals.SudoDialog.Show(desc, _ws, sudoResult =>
                {
                    if (sudoResult.Cancelled || !sudoResult.Success) return;
                    _ = Task.Run(async () =>
                    {
                        var (ok, err) = isDir
                            ? await _sudo.CreateDirectoryAsync(path, sudoResult.Password, CancellationToken.None)
                            : await _sudo.CreateFileAsync(path, sudoResult.Password, CancellationToken.None);
                        _ws.EnqueueOnUIThread(() =>
                        {
                            if (ok) Refresh();
                            else _ws.NotificationStateService.ShowNotification(
                                "Error", $"Create failed: {err}", SharpConsoleUI.Core.NotificationSeverity.Danger);
                        });
                    });
                });
            }
            catch (Exception ex)
            {
                _ws.NotificationStateService.ShowNotification(
                    "Error", $"Create failed: {ex.Message}", SharpConsoleUI.Core.NotificationSeverity.Danger);
            }
        }
    }
}
