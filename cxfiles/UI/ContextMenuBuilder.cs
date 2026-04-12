using SharpConsoleUI;
using SharpConsoleUI.Controls;
using CXFiles.Models;

namespace CXFiles.UI;

public class ContextMenuBuilder
{
    private readonly ConsoleWindowSystem _ws;
    private readonly Window _parentWindow;
    private readonly Action<FileEntry> _onRename;
    private readonly Action<FileEntry> _onDelete;
    private readonly Action<bool> _onNewItem;
    private readonly Action _onRefresh;

    public ContextMenuBuilder(
        ConsoleWindowSystem ws,
        Window parentWindow,
        Action<FileEntry> onRename,
        Action<FileEntry> onDelete,
        Action<bool> onNewItem,
        Action onRefresh)
    {
        _ws = ws;
        _parentWindow = parentWindow;
        _onRename = onRename;
        _onDelete = onDelete;
        _onNewItem = onNewItem;
        _onRefresh = onRefresh;
    }

    public void Show(FileEntry entry, int screenX, int screenY)
    {
        var items = new List<(string label, Action action)>
        {
            ("Open", () => { /* handled by caller */ }),
            ("---", () => { }),
            ("Rename  F2", () => _onRename(entry)),
            ("Delete  Del", () => _onDelete(entry)),
            ("---", () => { }),
            ("New File     ^N", () => _onNewItem(false)),
            ("New Folder  ^⇧N", () => _onNewItem(true)),
            ("---", () => { }),
            ("Refresh  F5", () => _onRefresh()),
        };

        // TODO: show as portal menu at (screenX, screenY)
        // For now, this is a placeholder — needs MenuControl + portal integration
    }
}
