using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using CXFiles.Models;
using CXFiles.Services;

namespace CXFiles.UI;

public class ContextMenuBuilder
{
    private ContextMenuPortal? _portal;
    private LayoutNode? _portalNode;
    private IWindowControl? _portalOwner;

    public event Action<FileEntry>? OnOpen;
    public event Action? OnRename;
    public event Action? OnDelete;
    public event Action? OnProperties;
    public event Action? OnCopy;
    public event Action? OnCut;
    public event Action? OnPaste;
    public event Action<bool>? OnNewItem;
    public event Action? OnRefresh;
    public event Action<FileEntry>? OnOpenInEditor;
    public event Action<string>? OnOpenTerminal;

    public bool IsOpen => _portal != null;

    public bool ProcessPreviewKey(SharpConsoleUI.KeyPressedEventArgs e)
    {
        if (_portal != null)
        {
            _portal.ProcessKey(e.KeyInfo);
            e.Handled = true;
            return true;
        }
        return false;
    }

    public void Dismiss(Window window)
    {
        if (_portalNode != null && _portalOwner != null)
        {
            window.RemovePortal(_portalOwner, _portalNode);
            _portalNode = null;
            _portal = null;
            _portalOwner = null;
        }
    }

    public void Show(FileEntry entry, Window window, IWindowControl owner,
        int screenX, int screenY, bool hasClipboard)
    {
        Dismiss(window);

        var items = new List<ContextMenuItem>
        {
            new("Open", "Enter", () => OnOpen?.Invoke(entry)),
        };

        if (!entry.IsDirectory)
            items.Add(new("Open in editor", "^E", () => OnOpenInEditor?.Invoke(entry)));

        items.Add(new("Open terminal here", "", () => OnOpenTerminal?.Invoke(
            entry.IsDirectory ? entry.FullPath : Path.GetDirectoryName(entry.FullPath) ?? "")));
        items.Add(new("-"));
        items.Add(new("Copy", "^C", () => OnCopy?.Invoke()));
        items.Add(new("Cut", "^X", () => OnCut?.Invoke()));

        if (hasClipboard)
            items.Add(new("Paste", "^V", () => OnPaste?.Invoke()));

        items.AddRange(new ContextMenuItem[]
        {
            new("-"),
            new("Rename", "F2", () => OnRename?.Invoke()),
            new("Delete", "Del", () => OnDelete?.Invoke()),
            new("-"),
            new("New File", "^N", () => OnNewItem?.Invoke(false)),
            new("New Folder", "^⇧N", () => OnNewItem?.Invoke(true)),
            new("-"),
            new("Properties", "F4", () => OnProperties?.Invoke()),
        });

        ShowPortal(items, window, owner, screenX, screenY);
    }

    public void ShowForFolder(string folderPath, Window window, IWindowControl owner,
        int screenX, int screenY, Action<string> onNavigate, bool hasClipboard)
    {
        Dismiss(window);

        var items = new List<ContextMenuItem>
        {
            new("Open", "Enter", () => onNavigate(folderPath)),
            new("Open terminal here", "", () => OnOpenTerminal?.Invoke(folderPath)),
            new("-"),
            new("New Folder", "^⇧N", () => OnNewItem?.Invoke(true)),
            new("New File", "^N", () => OnNewItem?.Invoke(false)),
        };

        if (hasClipboard)
            items.Add(new("Paste", "^V", () => OnPaste?.Invoke()));

        items.AddRange(new ContextMenuItem[]
        {
            new("-"),
            new("Rename", "F2", () => OnRename?.Invoke()),
            new("Delete", "Del", () => OnDelete?.Invoke()),
            new("-"),
            new("Refresh", "F5", () => OnRefresh?.Invoke()),
        });

        ShowPortal(items, window, owner, screenX, screenY);
    }

    private void ShowPortal(List<ContextMenuItem> items, Window window,
        IWindowControl owner, int screenX, int screenY)
    {
        var portal = new ContextMenuPortal(items, screenX, screenY, window.Width, window.Height);
        portal.Container = window;
        _portal = portal;
        _portalOwner = owner;
        _portalNode = window.CreatePortal(owner, portal);

        portal.ItemSelected += (_, item) =>
        {
            Dismiss(window);
            item.Action?.Invoke();
        };

        portal.Dismissed += (_, _) => Dismiss(window);
        portal.DismissRequested += (_, _) => Dismiss(window);
    }
}
