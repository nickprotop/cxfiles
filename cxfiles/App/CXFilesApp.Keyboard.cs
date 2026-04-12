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
        var entry = _fileList.GetSelectedEntry();
        if (entry == null) return;

        var type = entry.IsDirectory ? "folder" : "file";
        var confirmed = await ConfirmModal.ShowAsync(_ws,
            "Delete",
            $"Delete {type} \"{entry.Name}\"?",
            _mainWindow);

        if (confirmed)
        {
            try
            {
                await _fs.DeleteAsync(entry.FullPath, entry.IsDirectory, CancellationToken.None);
                Refresh();
            }
            catch (Exception ex)
            {
                _ws.NotificationStateService.ShowNotification(
                    "Error", $"Delete failed: {ex.Message}", SharpConsoleUI.Core.NotificationSeverity.Danger);
            }
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
