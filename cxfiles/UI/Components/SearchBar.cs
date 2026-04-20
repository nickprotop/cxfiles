using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Layout;

namespace CXFiles.UI.Components;

public sealed class SearchBar
{
    private readonly PromptControl _prompt;
    private readonly ButtonControl _upButton;
    private readonly SeparatorControl _separator;
    private readonly ButtonControl _recurseButton;
    private readonly ButtonControl _clearButton;
    private readonly HorizontalGridControl _grid;
    private bool _recurse = true;
    private bool _recurseOverridden;

    public IWindowControl Control => _grid;
    public bool HasFocus => _prompt.HasFocus;

    public event Action<string>? QueryChanged;
    public event Action? Submitted;
    public event Action? Cleared;
    public event Action<bool>? RecurseToggled;
    public event Action? UpClicked;

    public string Query
    {
        get => _prompt.Input;
        set => _prompt.Input = value;
    }

    public bool Recurse
    {
        get => _recurse;
        set
        {
            if (_recurse == value) return;
            _recurse = value;
            UpdateRecurseButton();
        }
    }

    public SearchBar()
    {
        _upButton = new ButtonControl
        {
            Text = "↑ Up [grey50]Bksp[/]",
            BackgroundColor = new Color(28, 36, 56),
            ForegroundColor = new Color(180, 200, 240),
            FocusedBackgroundColor = new Color(60, 80, 120),
            FocusedForegroundColor = new Color(220, 235, 255),
        };
        _upButton.Click += (_, _) => UpClicked?.Invoke();

        _separator = new SeparatorControl
        {
            ForegroundColor = Color.Grey27,
        };

        _recurseButton = new ButtonControl
        {
            Text = "[[R]]", // escaped: renders as [R] (Spectre markup eats single brackets)
            BackgroundColor = new Color(28, 36, 56),
            ForegroundColor = new Color(180, 200, 240),
            FocusedBackgroundColor = new Color(60, 80, 120),
            FocusedForegroundColor = new Color(220, 235, 255),
        };
        _recurseButton.Click += (_, _) =>
        {
            // Toggle and notify. The override flag (./) only loosens recurse;
            // user clicking the button always means "I'm choosing the toggle state".
            _recurse = !_recurse;
            _recurseOverridden = false;
            UpdateRecurseButton();
            RecurseToggled?.Invoke(_recurse);
        };

        _prompt = new PromptControl
        {
            UnfocusOnEnter = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            InputBackgroundColor = new Color(28, 36, 56),
            InputForegroundColor = new Color(220, 230, 245),
            InputFocusedBackgroundColor = new Color(40, 52, 80),
            InputFocusedForegroundColor = new Color(255, 255, 255),
        };
        UpdatePromptLabel();

        _clearButton = new ButtonControl
        {
            Text = "[[Esc]]", // escaped: renders as [Esc]
            BackgroundColor = new Color(28, 36, 56),
            ForegroundColor = new Color(180, 200, 240),
            FocusedBackgroundColor = new Color(60, 80, 120),
            FocusedForegroundColor = new Color(220, 235, 255),
            Visible = false,
        };
        _clearButton.Click += (_, _) => Cleared?.Invoke();

        _prompt.InputChanged += (_, text) =>
        {
            SetClearButtonVisible(!string.IsNullOrEmpty(text));
            QueryChanged?.Invoke(text);
        };
        _prompt.Entered += (_, _) => Submitted?.Invoke();

        UpdateRecurseButton();

        _grid = Controls.HorizontalGrid()
            .WithAlignment(HorizontalAlignment.Stretch)
            .Column(col => col.Width(12).Add(_upButton))
            .Column(col => col.Width(1).Add(_separator))
            .Column(col => col.Width(5).Add(_recurseButton))
            .Column(col => col.Flex(1.0).Add(_prompt))
            .Column(col => col.Width(7).Add(_clearButton))
            .Build();
        _grid.Margin = new Margin(1, 0, 0, 0);
        _grid.BackgroundColor = new Color(28, 36, 56);

        // Collapse the clear-button column initially — the prompt is empty,
        // so the prompt should fill the available width.
        SetClearButtonVisible(false);
    }

    public void Focus()
    {
        _prompt.RequestFocus(FocusReason.Keyboard);
    }

    public void Clear()
    {
        // Guard against re-entry: setting Input fires InputChanged even when
        // unchanged. Skip the assignment if already empty so a typing-driven
        // CancelAndRestore doesn't loop.
        if (!string.IsNullOrEmpty(_prompt.Input))
            _prompt.Input = string.Empty;
        _recurseOverridden = false;
        SetClearButtonVisible(false);
        UpdateRecurseButton();
    }

    private void SetClearButtonVisible(bool visible)
    {
        _clearButton.Visible = visible;
        // Also collapse the grid column so the prompt extends to the right edge
        // when there's no clear button to show.
        var cols = _grid.Columns;
        if (cols.Count >= 5)
            cols[4].Visible = visible;
    }

    public void SetRecurseOverridden(bool overridden)
    {
        if (_recurseOverridden == overridden) return;
        _recurseOverridden = overridden;
        UpdateRecurseButton();
    }

    private void UpdateRecurseButton()
    {
        // Visual states. Brackets must be escaped (Spectre markup parser treats
        // `[X]` as a tag; `[[X]]` is the escape that renders as literal `[X]`).
        //   [R]  recurse on
        //   [.]  recurse off (current dir only)
        //   [/]  ./ override forcing non-recursive
        if (_recurseOverridden)
        {
            _recurseButton.Text = "[[/]]";
            _recurseButton.IsEnabled = false; // dim — overridden by query
        }
        else
        {
            _recurseButton.Text = _recurse ? "[[R]]" : "[[.]]";
            _recurseButton.IsEnabled = true;
        }
    }

    private void UpdatePromptLabel()
    {
        // Search icon + Ctrl+F hint. The recurse state lives in the button to the left.
        _prompt.Prompt = "[grey50]🔍[/] [grey50]^F[/] ";
    }
}
