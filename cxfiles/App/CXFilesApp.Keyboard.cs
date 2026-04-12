using SharpConsoleUI;
using SharpConsoleUI.Events;
using CXFiles.UI.Modals;

namespace CXFiles.App;

public partial class CXFilesApp
{
    private void OnGlobalKeyPressed(object? sender, KeyPressedEventArgs e)
    {
        var key = e.KeyInfo;
        bool ctrl = key.Modifiers.HasFlag(ConsoleModifiers.Control);
        bool shift = key.Modifiers.HasFlag(ConsoleModifiers.Shift);

        switch (key.Key)
        {
            case ConsoleKey.Backspace when !ctrl:
                NavigateUp();
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

            case ConsoleKey.H when ctrl: // Ctrl+H toggle hidden
                _config.Config.ShowHiddenFiles = !_config.Config.ShowHiddenFiles;
                _fileList.SetShowHidden(_config.Config.ShowHiddenFiles);
                _config.Save();
                e.Handled = true;
                break;

            case ConsoleKey.Q when ctrl:
                _ws.Shutdown();
                e.Handled = true;
                break;
        }
    }

    private async Task ShowOptionsAsync()
    {
        var changed = await UI.Modals.OptionsModal.ShowAsync(_ws, _config, _mainWindow);
        _fileList.SetShowHidden(_config.Config.ShowHiddenFiles);
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
        var confirmed = await ConfirmModal.ShowAsync(_ws, "Delete", message, _mainWindow);

        if (confirmed)
        {
            var desc = entries.Count == 1 ? $"Deleting {entries[0].Name}" : $"Deleting {entries.Count} items";
            var op = _operations.StartOperation(Services.OperationType.Delete, desc);
            UpdateStatusLine();

            _ = Task.Run(async () =>
            {
                try
                {
                    foreach (var entry in entries)
                    {
                        op.Cts.Token.ThrowIfCancellationRequested();
                        await _fs.DeleteAsync(entry.FullPath, entry.IsDirectory, op.Cts.Token);
                    }
                    _operations.CompleteOperation(op, Services.OperationStatus.Completed);
                    Refresh();
                }
                catch (OperationCanceledException)
                {
                    _operations.CompleteOperation(op, Services.OperationStatus.Cancelled);
                }
                catch (Exception ex)
                {
                    _operations.CompleteOperation(op, Services.OperationStatus.Failed, ex.Message);
                }
                UpdateStatusLine();
            });
        }
    }

    private async Task RenameSelectedAsync()
    {
        var entry = _fileList.GetSelectedEntry();
        if (entry == null) return;

        var newName = await RenameModal.ShowAsync(_ws, entry.Name, _mainWindow);
        if (newName != null)
        {
            try
            {
                _fs.Rename(entry.FullPath, newName);
                Refresh();
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
    }

    private void CutSelected()
    {
        var entries = GetCheckedEntries();
        if (entries.Count == 0) return;
        _clipboard.SetCut(entries.Select(e => e.FullPath));
        UpdateToolbar();
    }

    private async Task PasteAsync()
    {
        if (!_clipboard.HasContent) return;

        var isCut = _clipboard.Action == Services.ClipboardAction.Cut;
        var opType = isCut ? Services.OperationType.Move : Services.OperationType.Copy;
        var paths = _clipboard.Paths.ToList();
        var desc = $"{(isCut ? "Moving" : "Copying")} {paths.Count} item{(paths.Count > 1 ? "s" : "")}";
        var op = _operations.StartOperation(opType, desc);
        UpdateStatusLine();

        if (isCut) _clipboard.Clear();

        _ = Task.Run(async () =>
        {
            try
            {
                foreach (var sourcePath in paths)
                {
                    op.Cts.Token.ThrowIfCancellationRequested();
                    var name = Path.GetFileName(sourcePath);
                    var destPath = Path.Combine(_currentPath, name);

                    if (isCut)
                        await _fs.MoveAsync(sourcePath, destPath, false, op.Cts.Token);
                    else
                        await _fs.CopyAsync(sourcePath, destPath, false,
                            new Progress<(long bytes, long total)>(p =>
                            {
                                _operations.ReportProgress(op, p.bytes, p.total);
                            }), op.Cts.Token);
                }
                _operations.CompleteOperation(op, Services.OperationStatus.Completed);
                Refresh();
            }
            catch (OperationCanceledException)
            {
                _operations.CompleteOperation(op, Services.OperationStatus.Cancelled);
            }
            catch (Exception ex)
            {
                _operations.CompleteOperation(op, Services.OperationStatus.Failed, ex.Message);
            }
            UpdateStatusLine();
        });
    }

    private async Task ShowPropertiesAsync()
    {
        var entry = _fileList.GetSelectedEntry();
        if (entry == null) return;
        await PropertiesModal.ShowAsync(_ws, entry, _mainWindow);
    }

    private async Task NewItemAsync(bool isDirectory)
    {
        var result = await NewItemModal.ShowAsync(_ws, isDirectory, _mainWindow);
        if (result?.Name != null)
        {
            try
            {
                var path = Path.Combine(_currentPath, result.Name);
                if (result.IsDirectory)
                    _fs.CreateDirectory(path);
                else
                    _fs.CreateFile(path);
                Refresh();
            }
            catch (Exception ex)
            {
                _ws.NotificationStateService.ShowNotification(
                    "Error", $"Create failed: {ex.Message}", SharpConsoleUI.Core.NotificationSeverity.Danger);
            }
        }
    }
}
