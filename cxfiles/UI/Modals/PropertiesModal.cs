using System.Security.Cryptography;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using CXFiles.Models;
using CXFiles.Services;

namespace CXFiles.UI.Modals;

public class PropertiesModal : ModalBase<bool>
{
    private readonly FileEntry _entry;
    private readonly IFileSystemService _fs;
    private readonly CancellationTokenSource _cts = new();

    private BarGraphControl? _driveBar;
    private BarGraphControl? _folderBar;
    private MarkupControl? _spaceStatus;
    private MarkupControl? _permWarning;
    private long _driveTotal;

    // Breakdown: key → single-line bar. Key "" = direct files of the scan root.
    private readonly Dictionary<string, BarGraphControl> _childRows = new();
    private readonly Dictionary<string, ForeignChildInfo> _foreignChildren = new(StringComparer.Ordinal);
    private ScrollablePanelControl? _spacePanel;
    private bool _breakdownSorted;
    private const int BreakdownMaxRows = 20;

    // Metadata captured at breakdown-build time for a foreign-mount subdir so we can
    // show the remote drive's used% immediately (no recursion), disclose its stats,
    // and offer an on-demand scan without re-querying DriveInfo on every progress tick.
    // ResolvedPath is the symlink-followed target; Scan uses it so the inner modal's
    // mount detection treats the remote filesystem as its own (not "foreign to itself").
    private sealed record ForeignChildInfo(
        string Key,
        string SubPath,
        string ResolvedPath,
        string MountRoot,
        string Format,
        long Total,
        long Free);

    private MarkupControl? _md5Result;
    private MarkupControl? _sha256Result;
    private ProgressBarControl? _md5Progress;
    private ProgressBarControl? _sha256Progress;

    private PropertiesModal(ConsoleWindowSystem ws, IFileSystemService fs, FileEntry entry, Window? parent)
        : base(ws, parent)
    {
        _fs = fs;
        _entry = entry;
    }

    public static Task<bool> ShowAsync(ConsoleWindowSystem ws, IFileSystemService fs, FileEntry entry, Window? parent = null)
        => new PropertiesModal(ws, fs, entry, parent).ShowAsync();

    protected override string GetTitle() => "Properties";
    protected override int GetWidth() => 70;
    protected override int GetHeight() => 22;
    protected override bool GetDefaultResult() => false;

    protected override void BuildContent()
    {
        // Header row with icon + name
        var header = Controls.Markup()
            .AddLine($"[bold cyan1]{_entry.Icon}[/] [bold]{SharpConsoleUI.Parsing.MarkupParser.Escape(_entry.Name)}[/]  [dim]{_entry.TypeDescription}[/]")
            .WithMargin(1, 0, 1, 0)
            .Build();
        Modal!.AddControl(header);

        // Tab control
        var general = Controls.ScrollablePanel().WithBackgroundColor(Color.Transparent).Build();
        general.ShowScrollbar = false;
        BuildGeneralTab(general);

        var permissions = Controls.ScrollablePanel().WithBackgroundColor(Color.Transparent).Build();
        permissions.ShowScrollbar = false;
        BuildPermissionsTab(permissions);

        var space = Controls.ScrollablePanel().WithBackgroundColor(Color.Transparent).Build();
        space.ShowScrollbar = true;
        _spacePanel = space;
        BuildSpaceTab(space);

        var checksums = Controls.ScrollablePanel().WithBackgroundColor(Color.Transparent).Build();
        checksums.ShowScrollbar = false;
        BuildChecksumsTab(checksums);

        var tabBuilder = Controls.TabControl()
            .WithHeaderStyle(TabHeaderStyle.Classic)
            .AddTab("General", general)
            .AddTab("Permissions", permissions)
            .AddTab("Space", space)
            .AddTab("Checksums", checksums);

        if (!_entry.IsDirectory && IsMediaFile(_entry.Extension))
        {
            var media = Controls.ScrollablePanel().WithBackgroundColor(Color.Transparent).Build();
            media.ShowScrollbar = true;
            BuildMediaTab(media);
            tabBuilder = tabBuilder.AddTab("Media", media);
        }

        var tabs = tabBuilder
            .Fill()
            .Build();
        tabs.HorizontalAlignment = HorizontalAlignment.Stretch;
        tabs.BackgroundColor = new Color(40, 50, 70);
        Modal.AddControl(tabs);

        // Footer
        Modal.AddControl(Controls.Markup()
            .AddLine("[dim]Esc/Enter: Close   ←/→: Switch tab[/]")
            .StickyBottom()
            .WithMargin(1, 0, 1, 0)
            .Build());

        Modal.KeyPressed += (_, e) =>
        {
            if (e.KeyInfo.Key == ConsoleKey.Enter)
            {
                _cts.Cancel();
                CloseWithResult(true);
                e.Handled = true;
            }
        };
        Modal.OnClosed += (_, _) => _cts.Cancel();

        // Kick off background scan for directories and drive roots
        if (_entry.IsDirectory)
        {
            StartDirectoryScan(_entry.FullPath);
        }
    }

    private void BuildGeneralTab(ScrollablePanelControl panel)
    {
        var lines = new List<string>
        {
            $"[bold]General[/]",
            "",
            $"[dim]Name       :[/] {SharpConsoleUI.Parsing.MarkupParser.Escape(_entry.Name)}",
            $"[dim]Type       :[/] {_entry.TypeDescription}",
            $"[dim]Location   :[/] {SharpConsoleUI.Parsing.MarkupParser.Escape(Path.GetDirectoryName(_entry.FullPath) ?? "")}",
        };

        if (!string.IsNullOrEmpty(_entry.Extension))
            lines.Add($"[dim]Extension  :[/] {_entry.Extension}");

        if (!_entry.IsDirectory)
            lines.Add($"[dim]Size       :[/] {FileEntry.FormatSize(_entry.Size)} [grey]({_entry.Size:N0} bytes)[/]");

        lines.Add($"[dim]Modified   :[/] {_entry.Modified:yyyy-MM-dd HH:mm:ss}");
        lines.Add($"[dim]Created    :[/] {_entry.Created:yyyy-MM-dd HH:mm:ss}");
        lines.Add($"[dim]Accessed   :[/] {_entry.Accessed:yyyy-MM-dd HH:mm:ss}");
        lines.Add("");

        var attrs = new List<string>();
        if (_entry.IsHidden) attrs.Add("Hidden");
        if (_entry.IsReadOnly) attrs.Add("Read-only");
        if (_entry.IsSymlink) attrs.Add("Symlink");
        lines.Add($"[dim]Attributes :[/] {(attrs.Count > 0 ? string.Join(", ", attrs) : "None")}");

        if (_entry.IsSymlink)
        {
            try
            {
                var target = File.ResolveLinkTarget(_entry.FullPath, returnFinalTarget: false);
                if (target != null)
                    lines.Add($"[dim]Target     :[/] {SharpConsoleUI.Parsing.MarkupParser.Escape(target.FullName)}");
            }
            catch { }
        }

        panel.AddControl(Controls.Markup().AddLines(lines.ToArray()).WithMargin(1, 0, 1, 0).Build());
    }

    private void BuildPermissionsTab(ScrollablePanelControl panel)
    {
        var lines = new List<string> { "[bold]Permissions[/]", "" };

        if (OperatingSystem.IsWindows())
        {
            try
            {
                var attrs = File.GetAttributes(_entry.FullPath);
                lines.Add($"[dim]Attributes :[/] {attrs}");
            }
            catch (Exception ex)
            {
                lines.Add($"[red]Unable to read attributes: {SharpConsoleUI.Parsing.MarkupParser.Escape(ex.Message)}[/]");
            }
        }
        else
        {
            try
            {
                var mode = File.GetUnixFileMode(_entry.FullPath);
                lines.Add($"[dim]Mode       :[/] [bold]{FormatUnixMode(mode)}[/]  [grey]{FormatNumericMode(mode)}[/]");
            }
            catch (Exception ex)
            {
                lines.Add($"[red]Unable to read mode: {SharpConsoleUI.Parsing.MarkupParser.Escape(ex.Message)}[/]");
            }

            TryGetOwnerGroup(_entry.FullPath, out var owner, out var group);
            lines.Add($"[dim]Owner      :[/] {SharpConsoleUI.Parsing.MarkupParser.Escape(owner)}");
            lines.Add($"[dim]Group      :[/] {SharpConsoleUI.Parsing.MarkupParser.Escape(group)}");
        }

        panel.AddControl(Controls.Markup().AddLines(lines.ToArray()).WithMargin(1, 0, 1, 0).Build());
    }

    private void BuildSpaceTab(ScrollablePanelControl panel)
    {
        var drive = GetDriveForPath(_entry.FullPath);
        if (drive == null)
        {
            panel.AddControl(Controls.Markup()
                .AddLine("[yellow]Drive information unavailable.[/]")
                .WithMargin(1, 1, 1, 0).Build());
            return;
        }

        long total = 0, free = 0, used = 0;
        string driveRoot = drive.Name, driveLabel = drive.Name, driveType = "";
        try
        {
            total = drive.TotalSize;
            free = drive.AvailableFreeSpace;
            used = total - free;
            driveType = drive.DriveType.ToString();
            driveRoot = drive.RootDirectory.FullName;
            driveLabel = string.IsNullOrEmpty(drive.VolumeLabel) ? driveRoot : $"{driveRoot}  [grey]{SharpConsoleUI.Parsing.MarkupParser.Escape(drive.VolumeLabel)}[/]";
        }
        catch { }
        _driveTotal = total;

        bool isDriveRoot = string.Equals(
            Path.TrimEndingDirectorySeparator(_entry.FullPath),
            Path.TrimEndingDirectorySeparator(driveRoot),
            StringComparison.Ordinal);

        // ── Drive section ──
        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("[bold]Drive[/]")
            .WithColor(new Color(80, 100, 140))
            .Build());

        panel.AddControl(Controls.Markup()
            .AddLine($"  {driveLabel}  [grey]{driveType}[/]")
            .AddLine($"  [dim]Total[/] {FileEntry.FormatSize(total)}   [dim]Used[/] {FileEntry.FormatSize(used)}   [dim]Free[/] {FileEntry.FormatSize(free)}")
            .WithMargin(1, 0, 1, 0)
            .Build());

        double usedPct = total > 0 ? used * 100.0 / total : 0;
        _driveBar = Controls.BarGraph()
            .WithLabel("Used")
            .WithLabelWidth(14)
            .WithMaxValue(100)
            .WithValue(usedPct)
            .WithValueFormat("F1")
            .WithBarWidth(32)
            .WithSmoothGradient("green→yellow→red")
            .WithMargin(1, 0, 1, 0)
            .Build();
        panel.AddControl(_driveBar);

        if (!_entry.IsDirectory && !isDriveRoot)
        {
            // ── Selection section (file) ──
            panel.AddControl(Controls.RuleBuilder()
                .WithTitle("[bold]This file[/]")
                .WithColor(new Color(80, 100, 140))
                .WithMargin(0, 1, 0, 0)
                .Build());

            panel.AddControl(Controls.Markup()
                .AddLine($"  [dim]Path[/] {SharpConsoleUI.Parsing.MarkupParser.Escape(_entry.FullPath)}")
                .WithMargin(1, 0, 1, 0)
                .Build());

            double filePct = total > 0 ? _entry.Size * 100.0 / total : 0;
            string filePctText = filePct < 0.01 ? "< 0.01" : filePct.ToString("F2");
            panel.AddControl(Controls.Markup()
                .AddLine($"  [dim]Size[/] {FileEntry.FormatSize(_entry.Size)}   [dim]Share of drive[/] {filePctText} %")
                .WithMargin(1, 0, 1, 0)
                .Build());

            _folderBar = Controls.BarGraph()
                .WithLabel("Share")
                .WithLabelWidth(14)
                .WithMaxValue(100)
                .WithValue(Math.Max(filePct, 0))
                .WithValueFormat("F2")
                .WithBarWidth(32)
                .WithSmoothGradient("green→yellow→red")
                .WithMargin(1, 0, 1, 0)
                .Build();
            panel.AddControl(_folderBar);
            return;
        }

        // Directory (or drive root): "This folder" section + live Breakdown.
        panel.AddControl(Controls.RuleBuilder()
            .WithTitle(isDriveRoot ? "[bold]This drive[/]" : "[bold]This folder[/]")
            .WithColor(new Color(80, 100, 140))
            .WithMargin(0, 1, 0, 0)
            .Build());

        panel.AddControl(Controls.Markup()
            .AddLine($"  [dim]Path[/] {SharpConsoleUI.Parsing.MarkupParser.Escape(_entry.FullPath)}")
            .WithMargin(1, 0, 1, 0)
            .Build());

        if (!isDriveRoot)
        {
            _folderBar = Controls.BarGraph()
                .WithLabel("Share")
                .WithLabelWidth(14)
                .WithMaxValue(100)
                .WithValue(0)
                .WithValueFormat("F2")
                .WithBarWidth(32)
                .WithSmoothGradient("green→yellow→red")
                .WithMargin(1, 0, 1, 0)
                .Build();
            panel.AddControl(_folderBar);
        }

        _spaceStatus = Controls.Markup()
            .AddLine("[dim]  scanning…[/]")
            .WithMargin(1, 0, 1, 0)
            .Build();
        panel.AddControl(_spaceStatus);

        _permWarning = Controls.Markup()
            .AddLine("")
            .WithMargin(1, 0, 1, 0)
            .Build();
        _permWarning.Visible = false;
        panel.AddControl(_permWarning);

        // ── Breakdown section ──
        BuildBreakdownSection(panel);
    }

    private void BuildBreakdownSection(ScrollablePanelControl panel)
    {
        List<(string key, string display, bool foreign, string subPath)> rows = new();

        // Resolve the scan root before mount-matching so that when the user opens
        // Properties on a symlink (e.g. ~/Downloads → /mnt/nick/Downloads) we compare
        // against the real underlying mount, not the link's apparent parent.
        var scanResolved = FileSystemService.ResolveFullTarget(_entry.FullPath);
        var scanDrive = GetDriveForPath(scanResolved);
        string? scanMount = scanDrive != null
            ? Path.TrimEndingDirectorySeparator(scanDrive.RootDirectory.FullName)
            : null;

        try
        {
            foreach (var sub in Directory.EnumerateDirectories(_entry.FullPath))
            {
                var name = Path.GetFileName(sub);
                if (string.IsNullOrEmpty(name)) name = sub;

                bool foreign = false;
                var subResolved = FileSystemService.ResolveFullTarget(sub);
                if (scanMount != null)
                {
                    var childDrive = GetDriveForPath(subResolved);
                    if (childDrive != null)
                    {
                        var childMount = Path.TrimEndingDirectorySeparator(childDrive.RootDirectory.FullName);
                        foreign = !string.Equals(childMount, scanMount, StringComparison.Ordinal);
                        if (foreign)
                        {
                            string fmt = "";
                            long total = 0, free = 0;
                            try { fmt = childDrive.DriveFormat ?? ""; } catch { }
                            try { total = childDrive.TotalSize; } catch { }
                            try { free = childDrive.AvailableFreeSpace; } catch { }
                            _foreignChildren[name] = new ForeignChildInfo(name, sub, subResolved, childMount, fmt, total, free);
                        }
                    }
                }
                rows.Add((name, name, foreign, sub));
            }
        }
        catch { }

        rows.Sort((a, b) => string.Compare(a.display, b.display, StringComparison.OrdinalIgnoreCase));

        bool hasDirectFiles = false;
        try { hasDirectFiles = Directory.EnumerateFiles(_entry.FullPath).Any(); }
        catch { }
        if (hasDirectFiles) rows.Add(("", "(files)", false, ""));

        if (rows.Count == 0) return;

        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("[bold]Breakdown[/]  [dim](top level)[/]")
            .WithColor(new Color(80, 100, 140))
            .WithMargin(0, 1, 0, 0)
            .Build());

        foreach (var (key, display, foreign, subPath) in rows)
        {
            if (foreign && _foreignChildren.TryGetValue(key, out var info))
            {
                BuildForeignRow(panel, key, display, info);
                continue;
            }

            var bar = Controls.BarGraph()
                .WithLabel(FormatBreakdownLabel(display, 0))
                .WithLabelWidth(26)
                .WithMaxValue(100)
                .WithValue(0)
                .WithValueFormat("0.0' %'")
                .WithBarWidth(20)
                .WithSmoothGradient("green→yellow→red")
                .WithMargin(1, 0, 1, 0)
                .Build();
            panel.AddControl(bar);
            _childRows[key] = bar;
        }
    }

    // Renders a foreign-mount subdir as a live card: the bar tracks that mount's
    // own used% (from DriveInfo, no recursion), with a secondary line showing
    // used/total and the mount root, plus a Scan button that opens a fresh
    // Properties modal rooted at the subdir to scan the remote fs on demand.
    private void BuildForeignRow(ScrollablePanelControl panel, string key, string display, ForeignChildInfo info)
    {
        double usedPct = info.Total > 0 ? (info.Total - info.Free) * 100.0 / info.Total : 0;
        long used = info.Total - info.Free;

        var bar = Controls.BarGraph()
            .WithLabel(FormatForeignLabel(display, info.Format))
            .WithLabelWidth(26)
            .WithMaxValue(100)
            .WithValue(Math.Clamp(usedPct, 0, 100))
            .WithValueFormat("0.0' %'")
            .WithBarWidth(20)
            .WithSmoothGradient("green→yellow→red")
            .WithMargin(1, 0, 1, 0)
            .Build();
        panel.AddControl(bar);
        _childRows[key] = bar;

        string mountRoot = SharpConsoleUI.Parsing.MarkupParser.Escape(info.MountRoot);
        string sizes = info.Total > 0
            ? $"{FileEntry.FormatSize(used)} / {FileEntry.FormatSize(info.Total)} used"
            : "size unknown";
        panel.AddControl(Controls.Markup()
            .AddLine($"    [dim]↳ other filesystem · {sizes} · {mountRoot}[/]")
            .WithMargin(1, 0, 1, 0)
            .Build());

        var btn = Controls.Button($"Scan {display}").WithMargin(1, 0, 1, 0).Build();
        btn.Click += (_, _) => _ = OpenForeignScan(info.ResolvedPath);
        panel.AddControl(btn);
    }

    private async Task OpenForeignScan(string resolvedPath)
    {
        // Pass the resolved (symlink-followed) path so the inner modal's mount
        // detection treats the remote fs as its own, scans normally, and doesn't
        // flag every subdir as "foreign to itself".
        FileEntry? subEntry = null;
        try { subEntry = _fs.GetFileInfo(resolvedPath); } catch { }
        if (subEntry == null) return;
        await PropertiesModal.ShowAsync(WindowSystem, _fs, subEntry, Modal);
    }

    private static string FormatForeignLabel(string name, string format)
    {
        const int nameWidth = 16;
        string shown = name.Length <= nameWidth ? name : name.Substring(0, nameWidth - 1) + "…";
        string tag = string.IsNullOrEmpty(format) ? "fs" : format.ToLowerInvariant();
        return $"{shown.PadRight(nameWidth)} {tag,8}";
    }

    /// <summary>
    /// Builds the fixed-width left label: "name   1.2 GB" (or "(other fs)").
    /// bytes = -1 → foreign mount; bytes = 0 → not scanned yet.
    /// </summary>
    private static string FormatBreakdownLabel(string name, long bytes)
    {
        const int nameWidth = 16;
        string shown = name.Length <= nameWidth ? name : name.Substring(0, nameWidth - 1) + "…";
        string sizeStr = bytes < 0
            ? "other fs"
            : bytes == 0 ? "—" : FileEntry.FormatSize(bytes);
        return $"{shown.PadRight(nameWidth)} {sizeStr,8}";
    }

    private void BuildChecksumsTab(ScrollablePanelControl panel)
    {
        panel.AddControl(Controls.Markup().AddLine("[bold]Checksums[/]").WithMargin(1, 0, 1, 0).Build());

        if (_entry.IsDirectory)
        {
            panel.AddControl(Controls.Markup()
                .AddLine("[dim]Checksums are not available for directories.[/]")
                .WithMargin(1, 1, 1, 0).Build());
            return;
        }

        panel.AddControl(Controls.Markup().AddLine("").WithMargin(1, 0, 1, 0).Build());

        _md5Progress = Controls.ProgressBar()
            .WithMaxValue(_entry.Size > 0 ? _entry.Size : 1)
            .WithValue(0)
            .WithBarWidth(28)
            .ShowPercentage(true)
            .WithMargin(1, 0, 1, 0)
            .Build();
        _md5Progress.Visible = false;

        _md5Result = Controls.Markup()
            .AddLine("[dim]not computed[/]")
            .WithMargin(1, 0, 1, 0)
            .Build();

        var md5Btn = Controls.Button("Compute MD5").WithMargin(1, 0, 1, 0).Build();
        md5Btn.Click += (_, _) => _ = ComputeHashAsync(HashKind.Md5);
        panel.AddControl(md5Btn);
        panel.AddControl(_md5Progress);
        panel.AddControl(_md5Result);

        _sha256Progress = Controls.ProgressBar()
            .WithMaxValue(_entry.Size > 0 ? _entry.Size : 1)
            .WithValue(0)
            .WithBarWidth(28)
            .ShowPercentage(true)
            .WithMargin(1, 0, 1, 0)
            .Build();
        _sha256Progress.Visible = false;

        _sha256Result = Controls.Markup()
            .AddLine("[dim]not computed[/]")
            .WithMargin(1, 0, 1, 0)
            .Build();

        var shaBtn = Controls.Button("Compute SHA256").WithMargin(1, 1, 1, 0).Build();
        shaBtn.Click += (_, _) => _ = ComputeHashAsync(HashKind.Sha256);
        panel.AddControl(shaBtn);
        panel.AddControl(_sha256Progress);
        panel.AddControl(_sha256Result);
    }

    private void StartDirectoryScan(string path)
    {
        var progress = new Progress<DirectorySizeProgress>(OnScanProgress);
        Task.Run(() =>
        {
            try
            {
                _fs.EnumerateDirectorySize(path, progress, _cts.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                // Surface on UI thread
                if (_spaceStatus != null)
                {
                    try { _spaceStatus.SetContent(new List<string> { "[red]scan failed[/]" }); } catch { }
                }
            }
        });
    }

    private readonly System.Diagnostics.Stopwatch _scanClock = System.Diagnostics.Stopwatch.StartNew();

    private void OnScanProgress(DirectorySizeProgress p)
    {
        double driveSharePct = _driveTotal > 0 ? p.BytesSoFar * 100.0 / _driveTotal : 0;
        if (_folderBar != null)
        {
            _folderBar.Value = Math.Min(driveSharePct, 100);
        }
        if (_spaceStatus != null)
        {
            var prefix = p.IsFinal
                ? $"[green]done[/] [dim]in {_scanClock.Elapsed.TotalSeconds:F1}s[/]"
                : "[dim]scanning…[/]";
            var line = $"  {prefix}  [dim]{FileEntry.FormatSize(p.BytesSoFar)}  {p.FilesScanned:N0} files[/]";
            var lines = new List<string> { line };
            if (p.IsFinal && _foreignChildren.Count > 0)
            {
                var names = string.Join(", ",
                    _foreignChildren.Values
                        .Select(f => SharpConsoleUI.Parsing.MarkupParser.Escape(f.Key))
                        .OrderBy(s => s, StringComparer.OrdinalIgnoreCase));
                var word = _foreignChildren.Count == 1 ? "subdir" : "subdirs";
                lines.Add($"  [yellow]↪ {_foreignChildren.Count} {word} on other filesystems excluded:[/] [dim]{names}[/]");
            }
            _spaceStatus.SetContent(lines);
        }
        if (_permWarning != null && p.InaccessibleEntries > 0)
        {
            _permWarning.Visible = true;
            var word = p.InaccessibleEntries == 1 ? "entry" : "entries";
            _permWarning.SetContent(new List<string>
            {
                $"  [yellow]⚠ {p.InaccessibleEntries:N0} {word} skipped (permission denied)[/]"
            });
        }

        if (p.PerChildBytes != null && _childRows.Count > 0)
        {
            long total = Math.Max(p.BytesSoFar, 1);
            foreach (var (key, bar) in _childRows)
            {
                // Foreign rows render the mount's own used% (set at build time);
                // scan progress leaves them untouched so the card keeps telling
                // the truth about that remote drive instead of "0 % of scanned".
                if (_foreignChildren.ContainsKey(key)) continue;

                if (!p.PerChildBytes.TryGetValue(key, out var bytes)) bytes = 0;
                double share = bytes * 100.0 / total;
                bar.Value = Math.Min(share, 100);

                string display = key.Length == 0 ? "(files)" : key;
                bar.Label = FormatBreakdownLabel(display, bytes);
            }

            if (p.IsFinal && !_breakdownSorted)
            {
                _breakdownSorted = true;
                ReflowBreakdown(p.PerChildBytes, total);
            }
        }
    }

    private void ReflowBreakdown(IReadOnlyDictionary<string, long> finalBytes, long total)
    {
        if (_spacePanel == null || _childRows.Count == 0) return;

        // Snapshot the current rows sorted by bytes desc. Keep "(files)" out of Others collapsing.
        var sorted = _childRows
            .Select(kv => new { Key = kv.Key, Bar = kv.Value, Bytes = finalBytes.TryGetValue(kv.Key, out var b) ? b : 0L })
            .OrderByDescending(r => r.Bytes)
            .ToList();

        // Remove all breakdown bars from the panel so we can re-add in the new order.
        foreach (var r in sorted)
            _spacePanel.RemoveControl(r.Bar);

        // Determine visible set and aggregate Others.
        var visible = sorted.Take(BreakdownMaxRows).ToList();
        var collapsed = sorted.Skip(BreakdownMaxRows).ToList();

        foreach (var r in visible)
            _spacePanel.AddControl(r.Bar);

        if (collapsed.Count > 0)
        {
            long othersBytes = collapsed.Sum(r => r.Bytes);
            double othersShare = total > 0 ? othersBytes * 100.0 / total : 0;

            var othersBar = Controls.BarGraph()
                .WithLabel(FormatBreakdownLabel($"(others: {collapsed.Count})", othersBytes))
                .WithLabelWidth(26)
                .WithMaxValue(100)
                .WithValue(Math.Min(othersShare, 100))
                .WithValueFormat("0.0' %'")
                .WithBarWidth(20)
                .WithSmoothGradient("green→yellow→red")
                .WithMargin(1, 0, 1, 0)
                .Build();
            _spacePanel.AddControl(othersBar);
        }
    }

    private enum HashKind { Md5, Sha256 }

    private async Task ComputeHashAsync(HashKind kind)
    {
        var progress = kind == HashKind.Md5 ? _md5Progress : _sha256Progress;
        var result = kind == HashKind.Md5 ? _md5Result : _sha256Result;
        if (progress == null || result == null) return;

        progress.Visible = true;
        progress.Value = 0;
        result.SetContent(new List<string> { "[dim]computing…[/]" });

        try
        {
            var hex = await Task.Run(() => HashFile(_entry.FullPath, kind, progress, _cts.Token), _cts.Token);
            result.SetContent(new List<string> { $"[green]{hex}[/]" });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            result.SetContent(new List<string> { $"[red]{SharpConsoleUI.Parsing.MarkupParser.Escape(ex.Message)}[/]" });
        }
        finally
        {
            if (progress != null) progress.Visible = false;
        }
    }

    private static string HashFile(string path, HashKind kind, ProgressBarControl progress, CancellationToken ct)
    {
        using var algo = kind == HashKind.Md5 ? (HashAlgorithm)MD5.Create() : SHA256.Create();
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan);
        var buffer = new byte[65536];
        long total = 0;
        int read;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        long lastReport = 0;
        while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            algo.TransformBlock(buffer, 0, read, null, 0);
            total += read;
            if (sw.ElapsedMilliseconds - lastReport >= 100)
            {
                lastReport = sw.ElapsedMilliseconds;
                progress.Value = total;
            }
        }
        algo.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        progress.Value = total;
        return Convert.ToHexString(algo.Hash!).ToLowerInvariant();
    }

    private void BuildMediaTab(ScrollablePanelControl panel)
    {
        var lines = new List<string>();

        try
        {
            using var file = TagLib.File.Create(_entry.FullPath);
            var props = file.Properties;
            var tag = file.Tag;

            // Technical properties
            lines.Add("[bold]Technical[/]");
            lines.Add("");

            if (props.Duration.TotalSeconds > 0)
            {
                var d = props.Duration;
                lines.Add(d.TotalHours >= 1
                    ? $"[dim]Duration    :[/] {(int)d.TotalHours}:{d.Minutes:D2}:{d.Seconds:D2}"
                    : $"[dim]Duration    :[/] {d.Minutes}:{d.Seconds:D2}");
            }

            if (props.VideoWidth > 0 && props.VideoHeight > 0)
                lines.Add($"[dim]Resolution  :[/] {props.VideoWidth} x {props.VideoHeight}");

            if (props.PhotoWidth > 0 && props.PhotoHeight > 0)
                lines.Add($"[dim]Dimensions  :[/] {props.PhotoWidth} x {props.PhotoHeight}");

            if (props.AudioBitrate > 0)
                lines.Add($"[dim]Bitrate     :[/] {props.AudioBitrate} kbps");
            if (props.AudioSampleRate > 0)
                lines.Add($"[dim]Sample Rate :[/] {props.AudioSampleRate} Hz");
            if (props.AudioChannels > 0)
                lines.Add($"[dim]Channels    :[/] {(props.AudioChannels == 1 ? "Mono" : props.AudioChannels == 2 ? "Stereo" : $"{props.AudioChannels}")}");
            if (props.BitsPerSample > 0)
                lines.Add($"[dim]Bit Depth   :[/] {props.BitsPerSample} bit");
            if (props.PhotoQuality > 0)
                lines.Add($"[dim]Quality     :[/] {props.PhotoQuality}");

            foreach (var codec in props.Codecs)
            {
                if (codec is TagLib.IVideoCodec vc)
                    lines.Add($"[dim]Video Codec :[/] {Esc(vc.Description)}");
                else if (codec is TagLib.IAudioCodec ac)
                    lines.Add($"[dim]Audio Codec :[/] {Esc(ac.Description)}");
            }

            // Tags
            bool hasTag = !string.IsNullOrWhiteSpace(tag.Title)
                || !string.IsNullOrWhiteSpace(tag.JoinedPerformers)
                || !string.IsNullOrWhiteSpace(tag.Album)
                || tag.Year > 0;

            if (hasTag)
            {
                lines.Add("");
                lines.Add("[bold]Tags[/]");
                lines.Add("");

                if (!string.IsNullOrWhiteSpace(tag.Title))
                    lines.Add($"[dim]Title       :[/] {Esc(tag.Title)}");
                if (!string.IsNullOrWhiteSpace(tag.JoinedPerformers))
                    lines.Add($"[dim]Artist      :[/] {Esc(tag.JoinedPerformers)}");
                if (!string.IsNullOrWhiteSpace(tag.JoinedAlbumArtists))
                    lines.Add($"[dim]Album Artist:[/] {Esc(tag.JoinedAlbumArtists)}");
                if (!string.IsNullOrWhiteSpace(tag.Album))
                    lines.Add($"[dim]Album       :[/] {Esc(tag.Album)}");
                if (tag.Year > 0)
                    lines.Add($"[dim]Year        :[/] {tag.Year}");
                if (tag.Track > 0)
                    lines.Add(tag.TrackCount > 0
                        ? $"[dim]Track       :[/] {tag.Track} / {tag.TrackCount}"
                        : $"[dim]Track       :[/] {tag.Track}");
                if (tag.Disc > 0)
                    lines.Add(tag.DiscCount > 0
                        ? $"[dim]Disc        :[/] {tag.Disc} / {tag.DiscCount}"
                        : $"[dim]Disc        :[/] {tag.Disc}");
                if (!string.IsNullOrWhiteSpace(tag.JoinedGenres))
                    lines.Add($"[dim]Genre       :[/] {Esc(tag.JoinedGenres)}");
                if (!string.IsNullOrWhiteSpace(tag.JoinedComposers))
                    lines.Add($"[dim]Composer    :[/] {Esc(tag.JoinedComposers)}");
                if (!string.IsNullOrWhiteSpace(tag.Conductor))
                    lines.Add($"[dim]Conductor   :[/] {Esc(tag.Conductor)}");
                if (tag.BeatsPerMinute > 0)
                    lines.Add($"[dim]BPM         :[/] {tag.BeatsPerMinute}");
                if (!string.IsNullOrWhiteSpace(tag.Copyright))
                    lines.Add($"[dim]Copyright   :[/] {Esc(tag.Copyright)}");
                if (!string.IsNullOrWhiteSpace(tag.Comment))
                    lines.Add($"[dim]Comment     :[/] {Esc(tag.Comment)}");
            }

            // EXIF (images)
            if (file.TagTypes.HasFlag(TagLib.TagTypes.XMP) ||
                file.TagTypes.HasFlag(TagLib.TagTypes.TiffIFD))
            {
                var imageTag = file.Tag as TagLib.Image.CombinedImageTag;
                if (imageTag != null)
                {
                    lines.Add("");
                    lines.Add("[bold]EXIF[/]");
                    lines.Add("");

                    if (!string.IsNullOrWhiteSpace(imageTag.Make))
                        lines.Add($"[dim]Camera Make :[/] {Esc(imageTag.Make)}");
                    if (!string.IsNullOrWhiteSpace(imageTag.Model))
                        lines.Add($"[dim]Camera Model:[/] {Esc(imageTag.Model)}");
                    if (imageTag.FocalLength.HasValue)
                        lines.Add($"[dim]Focal Length:[/] {imageTag.FocalLength:F1} mm");
                    if (imageTag.FNumber.HasValue)
                        lines.Add($"[dim]Aperture    :[/] f/{imageTag.FNumber:F1}");
                    if (imageTag.ExposureTime.HasValue)
                    {
                        var et = imageTag.ExposureTime.Value;
                        lines.Add(et >= 1
                            ? $"[dim]Exposure    :[/] {et:F1}s"
                            : $"[dim]Exposure    :[/] 1/{1.0 / et:F0}s");
                    }
                    if (imageTag.ISOSpeedRatings.HasValue)
                        lines.Add($"[dim]ISO         :[/] {imageTag.ISOSpeedRatings}");
                    if (imageTag.DateTime.HasValue)
                        lines.Add($"[dim]Date Taken  :[/] {imageTag.DateTime:yyyy-MM-dd HH:mm:ss}");
                    if (!string.IsNullOrWhiteSpace(imageTag.Software))
                        lines.Add($"[dim]Software    :[/] {Esc(imageTag.Software)}");
                    if (imageTag.Latitude.HasValue && imageTag.Longitude.HasValue)
                        lines.Add($"[dim]GPS         :[/] {imageTag.Latitude:F6}, {imageTag.Longitude:F6}");
                    if (imageTag.Orientation != TagLib.Image.ImageOrientation.None)
                        lines.Add($"[dim]Orientation :[/] {imageTag.Orientation}");
                    if (!string.IsNullOrWhiteSpace(imageTag.Creator))
                        lines.Add($"[dim]Creator     :[/] {Esc(imageTag.Creator)}");
                }
            }
        }
        catch (Exception ex)
        {
            lines.Add($"[red]Unable to read metadata: {Esc(ex.Message)}[/]");
        }

        if (lines.Count == 0)
            lines.Add("[dim]No media metadata available.[/]");

        panel.AddControl(Controls.Markup().AddLines(lines.ToArray()).WithMargin(1, 0, 1, 0).Build());
    }

    private static string Esc(string text) => SharpConsoleUI.Parsing.MarkupParser.Escape(text);

    private static bool IsMediaFile(string? ext) => ext?.ToLowerInvariant() switch
    {
        ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or
        ".tga" or ".tiff" or ".tif" or ".pbm" or
        ".mp3" or ".flac" or ".ogg" or ".opus" or ".wav" or ".aac" or ".wma" or
        ".m4a" or ".aiff" or ".ape" or
        ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" or ".webm" or ".flv" or
        ".m4v" or ".3gp" => true,
        _ => false
    };

    // --- Helpers ---

    private static string FormatUnixMode(UnixFileMode mode)
    {
        char R(UnixFileMode bit) => (mode & bit) != 0 ? 'r' : '-';
        char W(UnixFileMode bit) => (mode & bit) != 0 ? 'w' : '-';
        char X(UnixFileMode bit) => (mode & bit) != 0 ? 'x' : '-';
        return $"{R(UnixFileMode.UserRead)}{W(UnixFileMode.UserWrite)}{X(UnixFileMode.UserExecute)}" +
               $"{R(UnixFileMode.GroupRead)}{W(UnixFileMode.GroupWrite)}{X(UnixFileMode.GroupExecute)}" +
               $"{R(UnixFileMode.OtherRead)}{W(UnixFileMode.OtherWrite)}{X(UnixFileMode.OtherExecute)}";
    }

    private static string FormatNumericMode(UnixFileMode mode)
    {
        int n = 0;
        if ((mode & UnixFileMode.UserRead) != 0) n |= 0400;
        if ((mode & UnixFileMode.UserWrite) != 0) n |= 0200;
        if ((mode & UnixFileMode.UserExecute) != 0) n |= 0100;
        if ((mode & UnixFileMode.GroupRead) != 0) n |= 0040;
        if ((mode & UnixFileMode.GroupWrite) != 0) n |= 0020;
        if ((mode & UnixFileMode.GroupExecute) != 0) n |= 0010;
        if ((mode & UnixFileMode.OtherRead) != 0) n |= 0004;
        if ((mode & UnixFileMode.OtherWrite) != 0) n |= 0002;
        if ((mode & UnixFileMode.OtherExecute) != 0) n |= 0001;
        return $"0{Convert.ToString(n, 8).PadLeft(3, '0')}";
    }

    private static void TryGetOwnerGroup(string path, out string owner, out string group)
    {
        owner = "unavailable";
        group = "unavailable";
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "stat",
                ArgumentList = { "-c", "%U:%G", path },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc == null) return;
            if (!proc.WaitForExit(500)) { try { proc.Kill(); } catch { } return; }
            var line = proc.StandardOutput.ReadToEnd().Trim();
            var parts = line.Split(':');
            if (parts.Length == 2)
            {
                owner = parts[0];
                group = parts[1];
            }
        }
        catch { }
    }

    private static DriveInfo? GetDriveForPath(string path)
    {
        try
        {
            // Prefer longest-prefix mount match for Linux-style mounts.
            DriveInfo? best = null;
            int bestLen = -1;
            foreach (var d in DriveInfo.GetDrives())
            {
                if (!d.IsReady) continue;
                var root = d.RootDirectory.FullName;
                if (path.StartsWith(root, StringComparison.Ordinal) && root.Length > bestLen)
                {
                    best = d;
                    bestLen = root.Length;
                }
            }
            return best ?? new DriveInfo(Path.GetPathRoot(path) ?? "/");
        }
        catch { return null; }
    }
}
