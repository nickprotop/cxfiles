using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using CXFiles.Services;

namespace CXFiles.UI.Modals;

public class OptionsModal : ModalBase<bool>
{
    private readonly IConfigService _config;

    private OptionsModal(ConsoleWindowSystem ws, IConfigService config, Window? parent)
        : base(ws, parent)
    {
        _config = config;
    }

    public static Task<bool> ShowAsync(ConsoleWindowSystem ws, IConfigService config, Window? parent = null)
        => new OptionsModal(ws, config, parent).ShowAsync();

    protected override string GetTitle() => "Options";
    protected override int GetWidth() => 60;
    protected override int GetHeight() => 20;
    protected override bool GetDefaultResult() => false;

    protected override void BuildContent()
    {
        var nav = Controls.NavigationView()
            .WithNavWidth(18)
            .WithPaneHeader("[bold cyan]Options[/]")
            .WithContentBorder(BorderStyle.Rounded)
            .WithContentBorderColor(new Color(60, 60, 80))
            .WithContentPadding(1, 0, 1, 0)
            .AddHeader("Settings", Color.Cyan, header => header
                .AddItem("General", subtitle: "Display and behavior",
                    content: panel => BuildGeneralPage(panel))
                .AddItem("Appearance", subtitle: "Colors and layout",
                    content: panel => BuildAppearancePage(panel)))
            .Fill()
            .Build();

        Modal!.AddControl(nav);

        Modal.AddControl(Controls.Markup()
            .AddLine("[dim]Esc: Close  Changes are saved automatically[/]")
            .StickyBottom()
            .WithMargin(1, 0, 1, 0)
            .Build());
    }

    private void BuildGeneralPage(ScrollablePanelControl panel)
    {
        panel.AddControl(Controls.Markup()
            .AddLine("[bold]General Settings[/]")
            .AddEmptyLine()
            .Build());

        var hiddenCheck = Controls.Checkbox("Show hidden files")
            .Checked(_config.Config.ShowHiddenFiles)
            .Build();
        hiddenCheck.CheckedChanged += (_, _) =>
        {
            _config.Config.ShowHiddenFiles = hiddenCheck.Checked;
            _config.Save();
        };
        panel.AddControl(hiddenCheck);

        var confirmCheck = Controls.Checkbox("Confirm before delete")
            .Checked(_config.Config.ConfirmDelete)
            .Build();
        confirmCheck.CheckedChanged += (_, _) =>
        {
            _config.Config.ConfirmDelete = confirmCheck.Checked;
            _config.Save();
        };
        panel.AddControl(confirmCheck);

        var detailCheck = Controls.Checkbox("Show detail panel on startup")
            .Checked(_config.Config.ShowDetailPanel)
            .Build();
        detailCheck.CheckedChanged += (_, _) =>
        {
            _config.Config.ShowDetailPanel = detailCheck.Checked;
            _config.Save();
        };
        panel.AddControl(detailCheck);
    }

    private void BuildAppearancePage(ScrollablePanelControl panel)
    {
        panel.AddControl(Controls.Markup()
            .AddLine("[bold]Appearance[/]")
            .AddEmptyLine()
            .AddLine("[dim]Default path on startup:[/]")
            .Build());

        var pathPrompt = Controls.Prompt("Path: ")
            .WithInput(_config.Config.DefaultPath)
            .WithInputWidth(30)
            .UnfocusOnEnter(false)
            .OnEntered((_, text) =>
            {
                if (Directory.Exists(text))
                {
                    _config.Config.DefaultPath = text;
                    _config.Save();
                }
            })
            .Build();
        panel.AddControl(pathPrompt);
    }
}
