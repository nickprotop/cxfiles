using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using CXFiles.Models;
using CXFiles.Services;

namespace CXFiles.UI;

public record FolderMenuActions(
    Action? Open = null,
    Action? Rename = null,
    Action? Delete = null,
    Action? Properties = null,
    Action? Copy = null,
    Action? Cut = null,
    Action? Paste = null,
    Action? NewFolder = null,
    Action? NewFile = null,
    Action? Refresh = null,
    Action? AddToFavorites = null,
    bool HasClipboard = false,
    bool ShowAddToFavorites = true);

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
    public event Action<FileEntry>? OnAddToFavorites;

    public event Action? OnDismissed;

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
            OnDismissed?.Invoke();
        }
    }

    public void Show(FileEntry entry, Window window, IWindowControl owner,
        int screenX, int screenY, bool hasClipboard, bool showAddToFavorites = false)
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

        if (entry.IsDirectory && showAddToFavorites)
        {
            items.Add(new("-"));
            items.Add(new("Add to Favorites", "^D", () => OnAddToFavorites?.Invoke(entry)));
        }

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
        int screenX, int screenY, FolderMenuActions actions)
    {
        Dismiss(window);

        var items = new List<ContextMenuItem>
        {
            new("Open", "Enter", () => actions.Open?.Invoke()),
            new("Open terminal here", "", () => OnOpenTerminal?.Invoke(folderPath)),
            new("-"),
            new("New Folder", "^⇧N", () => actions.NewFolder?.Invoke()),
            new("New File", "^N", () => actions.NewFile?.Invoke()),
        };

        if (actions.HasClipboard)
            items.Add(new("Paste", "^V", () => actions.Paste?.Invoke()));

        if (actions.ShowAddToFavorites && actions.AddToFavorites != null)
        {
            items.Add(new("-"));
            items.Add(new("Add to Favorites", "^D", () => actions.AddToFavorites?.Invoke()));
        }

        items.AddRange(new ContextMenuItem[]
        {
            new("-"),
            new("Copy", "^C", () => actions.Copy?.Invoke()),
            new("Cut", "^X", () => actions.Cut?.Invoke()),
            new("-"),
            new("Rename", "F2", () => actions.Rename?.Invoke()),
            new("Delete", "Del", () => actions.Delete?.Invoke()),
            new("-"),
            new("Refresh", "F5", () => actions.Refresh?.Invoke()),
            new("-"),
            new("Properties", "F4", () => actions.Properties?.Invoke()),
        });

        ShowPortal(items, window, owner, screenX, screenY);
    }

    public void ShowForBookmark(string bookmarkPath, Window window, IWindowControl owner,
        int screenX, int screenY, Action onOpen, Action onRename, Action onRemove)
    {
        Dismiss(window);

        var items = new List<ContextMenuItem>
        {
            new("Open", "Enter", onOpen),
            new("-"),
            new("Rename", "F2", onRename),
            new("Remove from Favorites", "Del", onRemove),
        };

        ShowPortal(items, window, owner, screenX, screenY);
    }

    private void ShowPortal(List<ContextMenuItem> items, Window window,
        IWindowControl owner, int screenX, int screenY)
    {
        var portal = new ContextMenuPortal(items, screenX, screenY, window);
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
