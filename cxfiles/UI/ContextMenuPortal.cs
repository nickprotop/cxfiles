using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using Color = SharpConsoleUI.Color;
using Rectangle = System.Drawing.Rectangle;

namespace CXFiles.UI;

internal record ContextMenuItem(string Label, string? Shortcut = null, Action? Action = null, bool Enabled = true)
{
    public bool IsSeparator => Label == "-";
}

internal class ContextMenuPortal : PortalContentContainer
{
    private readonly MenuControl _menu;
    private readonly Dictionary<MenuItem, ContextMenuItem> _menuItemMap = new();

    private static readonly Color MenuBg = Color.Grey11;
    private static readonly Color MenuFg = Color.Grey93;
    private static readonly Color SelBg  = Color.SteelBlue;
    private static readonly Color SelFg  = Color.White;

    public event EventHandler<ContextMenuItem>? ItemSelected;
    public event EventHandler? Dismissed;

    public ContextMenuPortal(List<ContextMenuItem> items, int anchorX, int anchorY,
        int windowWidth, int windowHeight)
    {
        _menu = new MenuControl
        {
            Orientation = MenuOrientation.Vertical,
            DropdownBackgroundColor = MenuBg,
            DropdownForegroundColor = MenuFg,
            DropdownHighlightBackgroundColor = SelBg,
            DropdownHighlightForegroundColor = SelFg,
            MenuBarBackgroundColor = MenuBg,
            MenuBarForegroundColor = MenuFg,
            MenuBarHighlightBackgroundColor = SelBg,
            MenuBarHighlightForegroundColor = SelFg,
        };

        BackgroundColor = MenuBg;
        ForegroundColor = MenuFg;
        DismissOnOutsideClick = true;
        BorderStyle = BoxChars.Rounded;
        BorderColor = Color.Grey50;
        BorderBackgroundColor = MenuBg;

        foreach (var item in items)
        {
            var mi = new MenuItem
            {
                Text = item.Label,
                Shortcut = item.Shortcut,
                IsSeparator = item.IsSeparator,
                IsEnabled = item.Enabled && !item.IsSeparator,
            };
            _menu.AddItem(mi);
            if (!item.IsSeparator)
                _menuItemMap[mi] = item;
        }

        PortalFocusedControl = _menu;

        _menu.ItemSelected += (_, mi) =>
        {
            if (_menuItemMap.TryGetValue(mi, out var contextItem))
                ItemSelected?.Invoke(this, contextItem);
        };

        AddChild(_menu);
        SetFocusOnFirstChild();

        int maxLabelW = 0;
        int maxShortcutW = 0;
        foreach (var item in items)
        {
            if (item.IsSeparator) continue;
            maxLabelW = Math.Max(maxLabelW, item.Label.Length);
            if (item.Shortcut != null)
                maxShortcutW = Math.Max(maxShortcutW, item.Shortcut.Length);
        }

        int contentW = maxLabelW + (maxShortcutW > 0 ? maxShortcutW + 2 : 0) + 4;
        int popupW = Math.Clamp(contentW + 2, 16, 50);
        int popupH = items.Count + 2;

        var pos = PortalPositioner.CalculateFromPoint(
            new Point(anchorX, anchorY),
            new System.Drawing.Size(popupW, popupH),
            new Rectangle(1, 1, windowWidth - 2, windowHeight - 2),
            PortalPlacement.BelowOrAbove,
            new System.Drawing.Size(16, 3));
        PortalBounds = pos.Bounds;
    }

    public override bool ProcessMouseEvent(MouseEventArgs args)
    {
        if (args.HasAnyFlag(SharpConsoleUI.Drivers.MouseFlags.ReportMousePosition))
        {
            if (_menu is IMouseAwareControl mac && mac.WantsMouseEvents)
                mac.ProcessMouseEvent(args);
            return true;
        }
        return base.ProcessMouseEvent(args);
    }

    public new bool ProcessKey(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Escape)
        {
            Dismissed?.Invoke(this, EventArgs.Empty);
            return true;
        }
        if (base.ProcessKey(key))
            return true;
        return true;
    }

    protected override void PaintPortalContent(CharacterBuffer buffer, LayoutRect bounds,
        LayoutRect clipRect, Color defaultFg, Color defaultBg)
    {
        base.PaintPortalContent(buffer, bounds, clipRect, MenuFg, MenuBg);
    }
}
