using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Layout;

namespace CXFiles.UI.Components;

public class BreadcrumbBar
{
    private readonly StatusBarControl _left;
    private readonly StatusBarControl _right;
    private readonly HorizontalGridControl _container;
    private readonly HorizontalGridControl _leftGrid;
    private readonly PromptControl _editInput;
    private readonly ButtonControl _editButton;

    private string _currentPath = string.Empty;
    private bool _inEditMode;

    public HorizontalGridControl Control => _container;
    public bool InEditMode => _inEditMode;
    public PromptControl EditInput => _editInput;

    public event Action<string>? SegmentClicked;
    public event Action? TrashClicked;
    public event Action? FavoritesClicked;
    public event Action<string>? PathSubmitted;
    public event Action? EditCancelled;
    public event Action<string>? EditTextChanged;

    public BreadcrumbBar()
    {
        _left = Controls.StatusBar()
            .AddLeftText("[cyan1]◈ /[/]")
            .Build();
        _left.SeparatorChar = "\u203a";
        _left.ItemSpacing = 1;
        _left.BackgroundColor = Color.Transparent;
        _left.HorizontalAlignment = HorizontalAlignment.Left;
        _left.Margin = new Margin(1, 0, 0, 0);

        _editInput = new PromptControl
        {
            UnfocusOnEnter = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            InputBackgroundColor = new Color(20, 30, 45),
            InputForegroundColor = new Color(230, 240, 255),
            InputFocusedBackgroundColor = new Color(30, 44, 70),
            InputFocusedForegroundColor = Color.White,
            Visible = false,
            Prompt = "[grey50]Path:[/] ",
        };
        _editInput.Entered += (_, _) => PathSubmitted?.Invoke(_editInput.Input);
        _editInput.InputChanged += (_, text) => EditTextChanged?.Invoke(text);

        _editButton = new ButtonControl
        {
            Text = "❯ Go to [grey50]^L[/]",
            BackgroundColor = Color.Transparent,
            ForegroundColor = new Color(140, 200, 255),
            FocusedBackgroundColor = Color.Transparent,
            FocusedForegroundColor = Color.White,
        };
        _editButton.Click += (_, _) => EnterEditMode();

        _right = Controls.StatusBar().Build();
        _right.SeparatorChar = "\u2022";
        _right.BackgroundColor = Color.Transparent;
        _right.HorizontalAlignment = HorizontalAlignment.Right;
        _right.Margin = new Margin(0, 0, 1, 0);

        var locations = new (string Label, string Path)[]
        {
            ("Home", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
            ("Desktop", Environment.GetFolderPath(Environment.SpecialFolder.Desktop)),
            ("Docs", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),
            ("Downloads", Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")),
        };

        bool first = true;
        foreach (var (label, locPath) in locations)
        {
            if (string.IsNullOrEmpty(locPath) || !Directory.Exists(locPath)) continue;
            if (!first) _right.AddRightSeparator();
            first = false;
            var p = locPath;
            _right.AddRightText($"[grey70]{label}[/]", () => NavigateTo(p));
        }

        if (!first) _right.AddRightSeparator();
        _right.AddRightText("[grey70]Trash[/]", () => TrashClicked?.Invoke());

        _right.AddRightSeparator();
        _right.AddRightText("[yellow]⭐ ▾[/]", () => FavoritesClicked?.Invoke());

        var separator = Controls.Markup()
            .AddLine("[grey50]│[/]")
            .WithAlignment(HorizontalAlignment.Center)
            .Build();

        _leftGrid = Controls.HorizontalGrid()
            .WithAlignment(HorizontalAlignment.Stretch)
            .Column(col => col.Width(12).Add(_editButton))
            .Column(col => col.Width(3).Add(separator))
            .Column(col => col.Flex(1).Add(_left))
            .Column(col => col.Flex(1).Add(_editInput))
            .Build();

        _container = Controls.HorizontalGrid()
            .StickyTop()
            .WithAlignment(HorizontalAlignment.Stretch)
            .Column(col => col.Flex(2).Add(_leftGrid))
            .Column(col => col.Add(_right))
            .Build();
        _container.BackgroundColor = Color.Grey15;
        _container.ForegroundColor = Color.Grey93;

        // Edit-input column hidden by default; breadcrumb takes the flex space.
        if (_leftGrid.Columns.Count >= 4)
            _leftGrid.Columns[3].Visible = false;
    }

    private void NavigateTo(string path)
    {
        if (Directory.Exists(path))
            SegmentClicked?.Invoke(path);
    }

    public void Update(string path)
    {
        _currentPath = path;
        _left.ClearAll();

        var root = Path.GetPathRoot(path) ?? "/";
        var rootLabel = SharpConsoleUI.Parsing.MarkupParser.Escape(root);
        _left.AddLeftText($"[underline cyan1]◈ {rootLabel}[/]", () => SegmentClicked?.Invoke(root));

        var parts = path.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        var accumulated = root;

        for (int i = 0; i < parts.Length; i++)
        {
            accumulated = Path.Combine(accumulated, parts[i]);
            var clickPath = accumulated;

            _left.AddLeftSeparator();

            if (i == parts.Length - 1)
                _left.AddLeftText($"[bold]{parts[i]}[/]");
            else
                _left.AddLeftText($"[underline]{parts[i]}[/]", () => SegmentClicked?.Invoke(clickPath));
        }
    }

    public void EnterEditMode()
    {
        if (_inEditMode) return;
        _inEditMode = true;

        _editInput.Input = _currentPath;
        SetEditVisibility(true);
        _editInput.RequestFocus(SharpConsoleUI.Controls.FocusReason.Keyboard);
    }

    public void ExitEditMode()
    {
        if (!_inEditMode) return;
        _inEditMode = false;
        SetEditVisibility(false);
        EditCancelled?.Invoke();
    }

    private void SetEditVisibility(bool editing)
    {
        // Columns 0 (button) and 1 (pipe separator) are always visible.
        _leftGrid.Columns[2].Visible = !editing; // breadcrumb
        _leftGrid.Columns[3].Visible = editing;  // input
        _editInput.Visible = editing;
        _left.Visible = !editing;
        _container.Invalidate();
    }
}
