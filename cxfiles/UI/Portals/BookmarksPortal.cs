using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using CXFiles.Models;
using Color = SharpConsoleUI.Color;
using Rectangle = System.Drawing.Rectangle;

namespace CXFiles.UI.Portals;

internal class BookmarksPortal : PortalContentContainer
{
    private readonly MenuControl _menu;
    private readonly Dictionary<MenuItem, BookmarkEntry> _itemMap = new();

    public event EventHandler<BookmarkEntry>? BookmarkSelected;
    public event EventHandler? Dismissed;

    public BookmarksPortal(IReadOnlyList<BookmarkEntry> bookmarks, int anchorX, int anchorY, Window window)
    {
        _menu = new MenuControl
        {
            Orientation = MenuOrientation.Vertical,
            DropdownBackgroundColor = Color.Grey11,
            DropdownForegroundColor = Color.Grey93,
            DropdownHighlightBackgroundColor = Color.SteelBlue,
            DropdownHighlightForegroundColor = Color.White,
            MenuBarBackgroundColor = Color.Grey11,
            MenuBarForegroundColor = Color.Grey93,
            MenuBarHighlightBackgroundColor = Color.SteelBlue,
            MenuBarHighlightForegroundColor = Color.White,
        };

        BackgroundColor = Color.Grey11;
        ForegroundColor = Color.Grey93;
        DismissOnOutsideClick = true;
        BorderStyle = BoxChars.Rounded;
        BorderColor = Color.Grey50;
        BorderBackgroundColor = Color.Grey11;

        if (bookmarks.Count == 0)
        {
            var empty = new MenuItem
            {
                Text = "No favorites. Press Ctrl+D on a folder to add.",
                IsEnabled = false,
            };
            _menu.AddItem(empty);
        }
        else
        {
            foreach (var bm in bookmarks)
            {
                bool exists = false;
                try { exists = Directory.Exists(bm.Path); } catch { }
                var label = exists
                    ? $"★ {bm.Name}"
                    : $"★ {bm.Name} (missing)";
                var mi = new MenuItem { Text = label, IsEnabled = true };
                _menu.AddItem(mi);
                _itemMap[mi] = bm;
            }
        }

        PortalFocusedControl = _menu;

        _menu.ItemSelected += (_, mi) =>
        {
            if (_itemMap.TryGetValue(mi, out var bm))
                BookmarkSelected?.Invoke(this, bm);
        };

        AddChild(_menu);
        SetFocusOnFirstChild();

        int maxW = 24;
        foreach (var bm in bookmarks)
            maxW = Math.Max(maxW, bm.Name.Length + 12);
        int popupW = Math.Clamp(maxW + 2, 28, 56);
        int popupH = Math.Max(bookmarks.Count, 1) + 2;

        int bufferW = window.Width - 2;
        int bufferH = window.Height - 2;
        int bufX = anchorX - window.Left - 1;
        int bufY = anchorY - window.Top - 1;

        var pos = PortalPositioner.CalculateFromPoint(
            new Point(bufX, bufY),
            new System.Drawing.Size(popupW, popupH),
            new Rectangle(0, 0, bufferW, bufferH),
            PortalPlacement.BelowOrAbove,
            new System.Drawing.Size(20, 3));
        PortalBounds = pos.Bounds;
    }

    public new bool ProcessKey(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Escape)
        {
            Dismissed?.Invoke(this, EventArgs.Empty);
            return true;
        }
        if (base.ProcessKey(key)) return true;
        return true;
    }

    protected override void PaintPortalContent(CharacterBuffer buffer, LayoutRect bounds,
        LayoutRect clipRect, Color defaultFg, Color defaultBg)
    {
        base.PaintPortalContent(buffer, bounds, clipRect, Color.Grey93, Color.Grey11);
    }
}
