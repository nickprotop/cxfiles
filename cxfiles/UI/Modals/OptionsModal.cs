using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;
using CXFiles.Services;

namespace CXFiles.UI.Modals;

public class OptionsModal : ModalBase<bool>
{
    private readonly IConfigService _config;

    // Working copy — only written to config on Save
    private bool _showHidden;
    private bool _confirmDelete;
    private bool _showDetail;
    private bool _syncTree;
    private bool _autoSelect;
    private string _editorCommand;
    private string _defaultPath;
    private int _maxTabs;
    private bool _allowSudo;

    private OptionsModal(ConsoleWindowSystem ws, IConfigService config, Window? parent)
        : base(ws, parent)
    {
        _config = config;

        // Copy current config into working state
        _showHidden = config.Config.ShowHiddenFiles;
        _confirmDelete = config.Config.ConfirmDelete;
        _showDetail = config.Config.ShowDetailPanel;
        _syncTree = config.Config.SyncTreeToTab;
        _autoSelect = config.Config.AutoSelectFirstItem;
        _editorCommand = config.Config.EditorCommand;
        _defaultPath = config.Config.DefaultPath;
        _maxTabs = config.Config.MaxTabs;
        _allowSudo = config.Config.AllowSudoElevation;
    }

    public static Task<bool> ShowAsync(ConsoleWindowSystem ws, IConfigService config, Window? parent = null)
        => new OptionsModal(ws, config, parent).ShowAsync();

    protected override string GetTitle() => "Options";
    protected override int GetWidth() => 80;
    protected override int GetHeight() => 28;
    protected override bool GetDefaultResult() => false; // Cancel/Escape = no changes

    protected override void CreateModal()
    {
        base.CreateModal();
        var gradient = ColorGradient.FromColors(new Color(20, 28, 48), new Color(10, 12, 22));
        Modal!.BackgroundGradient = new GradientBackground(gradient, GradientDirection.Vertical);
    }

    protected override void BuildContent()
    {
        var nav = Controls.NavigationView()
            .WithNavWidth(18)
            .WithExpandedThreshold(40)
            .WithPaneHeader("[bold cyan]Options[/]")
            .WithContentBorder(BorderStyle.Rounded)
            .WithContentBorderColor(new Color(60, 60, 80))
            .WithContentPadding(1, 0, 1, 0)
            .AddHeader("Settings", Color.Cyan, header => header
                .AddItem("General", subtitle: "Display and behavior",
                    content: panel => BuildGeneralPage(panel))
                .AddItem("Appearance", subtitle: "Paths and layout",
                    content: panel => BuildAppearancePage(panel))
                .AddItem("Security", subtitle: "Elevation and access",
                    content: panel => BuildSecurityPage(panel)))
            .Fill()
            .Build();

        Modal!.AddControl(nav);

        // Save / Cancel buttons
        var saveBtn = Controls.Button("  Save  ")
            .OnClick((_, _) => Save())
            .Build();
        var cancelBtn = Controls.Button(" Cancel ")
            .OnClick((_, _) => CloseWithResult(false))
            .Build();

        var buttons = HorizontalGridControl.ButtonRow(saveBtn, cancelBtn);
        buttons.StickyPosition = StickyPosition.Bottom;
        buttons.Margin = new Margin(1, 0, 1, 0);
        Modal.AddControl(buttons);
    }

    private void Save()
    {
        _config.Config.ShowHiddenFiles = _showHidden;
        _config.Config.ConfirmDelete = _confirmDelete;
        _config.Config.ShowDetailPanel = _showDetail;
        _config.Config.SyncTreeToTab = _syncTree;
        _config.Config.AutoSelectFirstItem = _autoSelect;
        _config.Config.EditorCommand = _editorCommand;
        _config.Config.DefaultPath = _defaultPath;
        _config.Config.MaxTabs = _maxTabs;
        _config.Config.AllowSudoElevation = _allowSudo;
        _config.Save();
        CloseWithResult(true);
    }

    private void BuildGeneralPage(ScrollablePanelControl panel)
    {
        panel.AddControl(Controls.Markup()
            .AddLine("[bold]General Settings[/]")
            .AddEmptyLine()
            .Build());

        var hiddenCheck = Controls.Checkbox("Show hidden files")
            .Checked(_showHidden)
            .Build();
        hiddenCheck.CheckedChanged += (_, _) => _showHidden = hiddenCheck.Checked;
        panel.AddControl(hiddenCheck);

        var confirmCheck = Controls.Checkbox("Confirm before delete")
            .Checked(_confirmDelete)
            .Build();
        confirmCheck.CheckedChanged += (_, _) => _confirmDelete = confirmCheck.Checked;
        panel.AddControl(confirmCheck);

        var detailCheck = Controls.Checkbox("Show detail panel on startup")
            .Checked(_showDetail)
            .Build();
        detailCheck.CheckedChanged += (_, _) => _showDetail = detailCheck.Checked;
        panel.AddControl(detailCheck);

        panel.AddControl(Controls.Markup()
            .AddEmptyLine()
            .AddLine("[bold]Tabs[/]")
            .AddEmptyLine()
            .Build());

        var syncTreeCheck = Controls.Checkbox("Sync folder tree to active tab")
            .Checked(_syncTree)
            .Build();
        syncTreeCheck.CheckedChanged += (_, _) => _syncTree = syncTreeCheck.Checked;
        panel.AddControl(syncTreeCheck);

        var autoSelectCheck = Controls.Checkbox("Auto-select first item on folder enter")
            .Checked(_autoSelect)
            .Build();
        autoSelectCheck.CheckedChanged += (_, _) => _autoSelect = autoSelectCheck.Checked;
        panel.AddControl(autoSelectCheck);

        panel.AddControl(Controls.Markup()
            .AddEmptyLine()
            .AddLine("[dim]Maximum tabs (1-10):[/]")
            .Build());

        var maxTabsPrompt = Controls.Prompt("Max tabs: ")
            .WithInput(_maxTabs.ToString())
            .WithInputWidth(5)
            .UnfocusOnEnter(false)
            .OnEntered((_, text) =>
            {
                if (int.TryParse(text, out var val) && val >= 1 && val <= 10)
                    _maxTabs = val;
            })
            .Build();
        panel.AddControl(maxTabsPrompt);

        panel.AddControl(Controls.Markup()
            .AddEmptyLine()
            .AddLine("[bold]External Tools[/]")
            .AddEmptyLine()
            .AddLine("[dim]Editor command (empty = $EDITOR or nano):[/]")
            .Build());

        var editorPrompt = Controls.Prompt("Editor: ")
            .WithInput(_editorCommand)
            .WithInputWidth(30)
            .UnfocusOnEnter(false)
            .OnEntered((_, text) => _editorCommand = text)
            .Build();
        panel.AddControl(editorPrompt);
    }

    private void BuildAppearancePage(ScrollablePanelControl panel)
    {
        panel.AddControl(Controls.Markup()
            .AddLine("[bold]Appearance[/]")
            .AddEmptyLine()
            .AddLine("[dim]Default path on startup:[/]")
            .Build());

        var pathPrompt = Controls.Prompt("Path: ")
            .WithInput(_defaultPath)
            .WithInputWidth(30)
            .UnfocusOnEnter(false)
            .OnEntered((_, text) =>
            {
                if (Directory.Exists(text))
                    _defaultPath = text;
            })
            .Build();
        panel.AddControl(pathPrompt);
    }

    private void BuildSecurityPage(ScrollablePanelControl panel)
    {
        panel.AddControl(Controls.Markup()
            .AddLine("[bold]Security[/]")
            .AddEmptyLine()
            .Build());

        var sudoCheck = Controls.Checkbox("Allow sudo elevation for file operations")
            .Checked(_allowSudo)
            .Build();
        sudoCheck.CheckedChanged += (_, _) => _allowSudo = sudoCheck.Checked;
        panel.AddControl(sudoCheck);

        panel.AddControl(Controls.Markup()
            .AddEmptyLine()
            .AddLine("[dim]When enabled, operations that fail due to insufficient")
            .AddLine("permissions will offer to retry with elevated privileges")
            .AddLine("(sudo). A password dialog will appear each time.[/]")
            .AddEmptyLine()
            .AddLine("[dim]When disabled, permission errors are shown as")
            .AddLine("notifications without offering elevation.[/]")
            .Build());

        if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows))
        {
            // Only show on Linux/macOS
        }
        else
        {
            panel.AddControl(Controls.Markup()
                .AddEmptyLine()
                .AddLine("[yellow]Sudo elevation is not available on Windows.[/]")
                .Build());
        }
    }
}
