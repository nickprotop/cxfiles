using System.Reflection;
using System.Runtime.InteropServices;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using CXFiles.Models;

namespace CXFiles.UI.Modals;

public class HelpModal : ModalBase<bool>
{
    private HelpModal(ConsoleWindowSystem ws, Window? parent) : base(ws, parent) { }

    public static Task<bool> ShowAsync(ConsoleWindowSystem ws, Window? parent = null)
        => new HelpModal(ws, parent).ShowAsync();

    protected override string GetTitle() => "Help & About";
    protected override int GetWidth() => 84;
    protected override int GetHeight() => 26;
    protected override bool GetDefaultResult() => false;

    protected override void BuildContent()
    {
        // Tab pages
        var shortcuts = Controls.ScrollablePanel().WithBackgroundColor(Color.Transparent).Build();
        shortcuts.ShowScrollbar = true;
        BuildShortcutsTab(shortcuts);

        var about = Controls.ScrollablePanel().WithBackgroundColor(Color.Transparent).Build();
        about.ShowScrollbar = true;
        BuildAboutTab(about);

        var tabs = Controls.TabControl()
            .WithHeaderStyle(TabHeaderStyle.Classic)
            .AddTab("Shortcuts", shortcuts)
            .AddTab("About", about)
            .Fill()
            .Build();
        tabs.HorizontalAlignment = HorizontalAlignment.Stretch;
        tabs.BackgroundColor = new Color(40, 50, 70);
        tabs.Margin = new Margin(1, 1, 1, 0);
        Modal!.AddControl(tabs);

        // Bottom strip: spacer line, then rule, then hint — all sticky-bottom,
        // added in visual top→bottom order.
        Modal.AddControl(Controls.Markup()
            .AddLine("")
            .StickyBottom()
            .Build());

        Modal.AddControl(Controls.RuleBuilder()
            .StickyBottom()
            .WithColor(Color.Grey27)
            .Build());

        Modal.AddControl(Controls.Markup()
            .AddLine("[grey70]Nikolaos Protopapas  ·  MIT License  ·  github.com/nickprotop/cxfiles[/]")
            .WithAlignment(HorizontalAlignment.Center)
            .StickyBottom()
            .WithMargin(1, 0, 1, 0)
            .Build());

        Modal.KeyPressed += (_, e) =>
        {
            if (e.KeyInfo.Key == ConsoleKey.Enter || e.KeyInfo.Key == ConsoleKey.F1)
            {
                CloseWithResult(true);
                e.Handled = true;
            }
        };
    }

    // ───────────────────────────── Shortcuts ─────────────────────────────

    private static readonly (string Section, (string Key, string Action)[] Items)[] Shortcuts = new[]
    {
        ("Navigation", new[]
        {
            ("Enter",       "Open file / folder"),
            ("Backspace",   "Navigate to parent"),
            ("↑ ↓",         "Move selection"),
            ("Home / End",  "First / last entry"),
            ("F3",          "Toggle detail panel"),
        }),
        ("Files", new[]
        {
            ("F2",          "Rename"),
            ("F4",          "Properties"),
            ("Delete",      "Delete (trash or permanent)"),
            ("Ctrl+C",      "Copy"),
            ("Ctrl+X",      "Cut"),
            ("Ctrl+V",      "Paste"),
            ("Ctrl+N",      "New file"),
            ("Ctrl+Shift+N","New folder"),
        }),
        ("Tabs", new[]
        {
            ("Ctrl+T",      "New tab"),
            ("Ctrl+W",      "Close tab"),
            ("Alt+← / →",   "Previous / next tab"),
            ("Ctrl+1…5",    "Jump to tab"),
        }),
        ("Search", new[]
        {
            ("Ctrl+F",      "Focus search bar"),
            ("Esc",         "Cancel search, restore listing"),
            ("./prefix",    "Force non-recursive (current dir only)"),
            ("[[R]] button", "Toggle recurse on/off"),
        }),
        ("View", new[]
        {
            ("Ctrl+H",      "Toggle hidden files"),
            ("F5",          "Refresh"),
            ("Ctrl+O",      "Options"),
        }),
        ("App", new[]
        {
            ("F1",          "This help"),
            ("Ctrl+P",      "Operations portal"),
            ("Ctrl+B",      "Clipboard portal"),
            ("Ctrl+Q",      "Quit"),
        }),
    };

    private void BuildShortcutsTab(ScrollablePanelControl panel)
    {
        var lines = new List<string>();
        foreach (var (section, items) in Shortcuts)
        {
            lines.Add($"[cyan1]▌[/] [bold cyan1]{section}[/]");
            lines.Add("");
            foreach (var (key, action) in items)
            {
                // Right-align key into a 16-char column, followed by the action.
                string keyChip = $"[grey50]❪[/] [white]{key}[/] [grey50]❫[/]";
                int rawLen = SharpConsoleUI.Parsing.MarkupParser.StripLength(keyChip);
                int pad = Math.Max(0, 18 - rawLen);
                lines.Add(new string(' ', pad) + keyChip + "  " + action);
            }
            lines.Add("");
        }
        panel.AddControl(Controls.Markup()
            .AddLines(lines.ToArray())
            .WithMargin(2, 1, 2, 0)
            .Build());
    }

    // ─────────────────────────────── About ───────────────────────────────

    private void BuildAboutTab(ScrollablePanelControl panel)
    {
        // Hero — small figlet, vivid color
        var figle = new FigleControl
        {
            Text = "cxfiles",
            Size = FigletSize.Small,
            Color = new Color(140, 200, 255),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        figle.Margin = new Margin(0, 1, 0, 0);
        panel.AddControl(figle);

        var tagline = Controls.Markup()
            .AddLine("[grey70]A polished terminal file explorer.[/]")
            .WithAlignment(HorizontalAlignment.Center)
            .Build();
        panel.AddControl(tagline);

        // Section: identity
        var env = EnvInfo.Gather();

        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("[bold]Identity[/]")
            .WithColor(new Color(80, 100, 140))
            .WithMargin(0, 1, 0, 0)
            .Build());

        panel.AddControl(Controls.Markup()
            .AddLine($"  [dim]Version    [/] {env.AppVersion}")
            .AddLine($"  [dim]Runtime    [/] {SharpConsoleUI.Parsing.MarkupParser.Escape(env.RuntimeDescription)}")
            .AddLine($"  [dim]UI         [/] SharpConsoleUI {env.SharpConsoleUIVersion}")
            .WithMargin(2, 0, 2, 0)
            .Build());

        // Section: system
        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("[bold]System[/]")
            .WithColor(new Color(80, 100, 140))
            .WithMargin(0, 1, 0, 0)
            .Build());

        panel.AddControl(Controls.Markup()
            .AddLine($"  [dim]OS         [/] {SharpConsoleUI.Parsing.MarkupParser.Escape(env.OSDescription)}")
            .AddLine($"  [dim]Arch       [/] {env.OSArchitecture}")
            .AddLine($"  [dim]Hostname   [/] {SharpConsoleUI.Parsing.MarkupParser.Escape(env.HostName)}")
            .AddLine($"  [dim]User       [/] {SharpConsoleUI.Parsing.MarkupParser.Escape(env.UserName)}")
            .AddLine($"  [dim]Home       [/] {SharpConsoleUI.Parsing.MarkupParser.Escape(env.HomePath)}")
            .WithMargin(2, 0, 2, 0)
            .Build());

        // Section: storage
        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("[bold]Storage[/]")
            .WithColor(new Color(80, 100, 140))
            .WithMargin(0, 1, 0, 0)
            .Build());

        panel.AddControl(Controls.Markup()
            .AddLine($"  [dim]Filesystem [/] {SharpConsoleUI.Parsing.MarkupParser.Escape(env.HomeFsType ?? "—")}")
            .AddLine($"  [dim]Volumes    [/] {env.MountedVolumes}")
            .AddLine($"  [dim]Home volume[/] {FormatBytes(env.HomeFreeBytes)} free of {FormatBytes(env.HomeTotalBytes)}")
            .WithMargin(2, 0, 2, 0)
            .Build());

        if (env.HomeTotalBytes > 0)
        {
            double usedPct = (env.HomeTotalBytes - env.HomeFreeBytes) * 100.0 / env.HomeTotalBytes;
            var bar = Controls.BarGraph()
                .WithLabel("Used")
                .WithLabelWidth(11)
                .WithMaxValue(100)
                .WithValue(usedPct)
                .WithValueFormat("F1")
                .WithBarWidth(40)
                .WithSmoothGradient("green→yellow→red")
                .WithMargin(4, 0, 2, 0)
                .Build();
            panel.AddControl(bar);
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "—";
        return FileEntry.FormatSize(bytes).Replace("B", " B").Replace("K", " KB").Replace("M", " MB").Replace("G", " GB");
    }

    // ─────────────────── Environment info gathering ──────────────────────

    private sealed record EnvInfoData(
        string AppVersion,
        string RuntimeDescription,
        string SharpConsoleUIVersion,
        string OSDescription,
        Architecture OSArchitecture,
        string HostName,
        string UserName,
        string HomePath,
        string? HomeFsType,
        int MountedVolumes,
        long HomeTotalBytes,
        long HomeFreeBytes);

    private static class EnvInfo
    {
        public static EnvInfoData Gather()
        {
            string appVersion = SafeAssemblyVersion(Assembly.GetEntryAssembly());
            string sharpVersion = SafeAssemblyVersion(typeof(ConsoleWindowSystem).Assembly);
            string runtime = $"{RuntimeInformation.FrameworkDescription}";
            string os = RuntimeInformation.OSDescription.Trim();
            var arch = RuntimeInformation.OSArchitecture;
            string host = SafeGet(() => Environment.MachineName, "—");
            string user = SafeGet(() => Environment.UserName, "—");
            string home = SafeGet(() => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "—");

            string? fsType = null;
            long total = 0, free = 0;
            int mounted = 0;
            try
            {
                var drives = DriveInfo.GetDrives();
                foreach (var d in drives) { if (d.IsReady) mounted++; }
                if (!string.IsNullOrEmpty(home))
                {
                    var homeDrive = FindBestDriveForPath(drives, home);
                    if (homeDrive != null && homeDrive.IsReady)
                    {
                        try { fsType = homeDrive.DriveFormat; } catch { }
                        try { total = homeDrive.TotalSize; } catch { }
                        try { free = homeDrive.AvailableFreeSpace; } catch { }
                    }
                }
            }
            catch { }

            return new EnvInfoData(
                AppVersion: appVersion,
                RuntimeDescription: runtime,
                SharpConsoleUIVersion: sharpVersion,
                OSDescription: os,
                OSArchitecture: arch,
                HostName: host,
                UserName: user,
                HomePath: home,
                HomeFsType: fsType,
                MountedVolumes: mounted,
                HomeTotalBytes: total,
                HomeFreeBytes: free);
        }

        private static DriveInfo? FindBestDriveForPath(DriveInfo[] drives, string path)
        {
            DriveInfo? best = null;
            int bestLen = -1;
            foreach (var d in drives)
            {
                if (!d.IsReady) continue;
                string root;
                try { root = d.RootDirectory.FullName; } catch { continue; }
                if (path.StartsWith(root, StringComparison.Ordinal) && root.Length > bestLen)
                {
                    best = d;
                    bestLen = root.Length;
                }
            }
            return best;
        }

        private static string SafeAssemblyVersion(Assembly? asm)
        {
            if (asm == null) return "—";
            var v = asm.GetName().Version;
            return v == null ? "—" : v.ToString(3);
        }

        private static string SafeGet(Func<string> f, string fallback)
        {
            try { return f() ?? fallback; } catch { return fallback; }
        }
    }
}
