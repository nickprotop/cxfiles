using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using Color = SharpConsoleUI.Color;
using Rectangle = System.Drawing.Rectangle;

namespace CXFiles.UI.Portals;

internal class PathCompletionPortal : PortalContentContainer
{
    private readonly MenuControl _menu;
    private readonly Dictionary<MenuItem, string> _itemCandidates = new();
    private readonly int _anchorX;
    private readonly int _anchorY;
    private readonly Window _window;

    public event EventHandler<string>? CandidateSelected;
    public event EventHandler? Dismissed;

    public PathCompletionPortal(IReadOnlyList<string> candidates, int anchorX, int anchorY, Window window)
    {
        _anchorX = anchorX;
        _anchorY = anchorY;
        _window = window;

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

        PopulateMenu(candidates);

        PortalFocusedControl = _menu;

        _menu.ItemSelected += (_, mi) =>
        {
            if (_itemCandidates.TryGetValue(mi, out var candidate))
                CandidateSelected?.Invoke(this, candidate);
        };

        AddChild(_menu);
        SetFocusOnFirstChild();

        ResizeAndPosition(candidates);
    }

    public void UpdateCandidates(IReadOnlyList<string> candidates)
    {
        _menu.ClearItems();
        _itemCandidates.Clear();
        PopulateMenu(candidates);
        ResizeAndPosition(candidates);
    }

    private void PopulateMenu(IReadOnlyList<string> candidates)
    {
        foreach (var c in candidates)
        {
            var mi = new MenuItem { Text = $"▸ {c}", IsEnabled = true };
            _menu.AddItem(mi);
            _itemCandidates[mi] = c;
        }
    }

    private void ResizeAndPosition(IReadOnlyList<string> candidates)
    {
        int maxW = 16;
        foreach (var c in candidates) maxW = Math.Max(maxW, c.Length + 4);
        int popupW = Math.Clamp(maxW + 2, 20, 70);
        int popupH = Math.Max(candidates.Count, 1) + 2;

        int bufferW = _window.Width - 2;
        int bufferH = _window.Height - 2;
        int bufX = _anchorX - _window.Left - 1;
        int bufY = _anchorY - _window.Top - 1;

        var pos = PortalPositioner.CalculateFromPoint(
            new Point(bufX, bufY),
            new System.Drawing.Size(popupW, popupH),
            new Rectangle(0, 0, bufferW, bufferH),
            PortalPlacement.BelowOrAbove,
            new System.Drawing.Size(16, 3));
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
