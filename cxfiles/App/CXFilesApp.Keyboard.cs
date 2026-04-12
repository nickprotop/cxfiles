using SharpConsoleUI;
using SharpConsoleUI.Events;

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
                // TODO: rename modal
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
                // TODO: delete confirm modal
                e.Handled = true;
                break;

            case ConsoleKey.N when ctrl && !shift:
                // TODO: new file modal
                e.Handled = true;
                break;

            case ConsoleKey.N when ctrl && shift:
                // TODO: new folder modal
                e.Handled = true;
                break;

            case ConsoleKey.Q when ctrl:
                _ws.Shutdown();
                e.Handled = true;
                break;
        }
    }
}
