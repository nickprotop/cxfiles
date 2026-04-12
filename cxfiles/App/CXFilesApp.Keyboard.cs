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
