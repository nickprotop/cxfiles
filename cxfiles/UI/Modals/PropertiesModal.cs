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
    private ScrollablePanelControl? _spacePanel;
    private bool _breakdownSorted;
    private const int BreakdownMaxRows = 20;

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

        var tabs = Controls.TabControl()
            .WithHeaderStyle(TabHeaderStyle.Classic)
            .AddTab("General", general)
            .AddTab("Permissions", permissions)
            .AddTab("Space", space)
            .AddTab("Checksums", checksums)
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
        List<(string key, string display, bool foreign)> rows = new();

        var scanDrive = GetDriveForPath(_entry.FullPath);
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
                if (scanMount != null)
                {
                    var childDrive = GetDriveForPath(sub);
                    if (childDrive != null)
                    {
                        var childMount = Path.TrimEndingDirectorySeparator(childDrive.RootDirectory.FullName);
                        foreign = !string.Equals(childMount, scanMount, StringComparison.Ordinal);
                    }
                }
                rows.Add((name, name, foreign));
            }
        }
        catch { }

        rows.Sort((a, b) => string.Compare(a.display, b.display, StringComparison.OrdinalIgnoreCase));

        bool hasDirectFiles = false;
        try { hasDirectFiles = Directory.EnumerateFiles(_entry.FullPath).Any(); }
        catch { }
        if (hasDirectFiles) rows.Add(("", "(files)", false));

        if (rows.Count == 0) return;

        panel.AddControl(Controls.RuleBuilder()
            .WithTitle("[bold]Breakdown[/]  [dim](top level)[/]")
            .WithColor(new Color(80, 100, 140))
            .WithMargin(0, 1, 0, 0)
            .Build());

        foreach (var (key, display, foreign) in rows)
        {
            var bar = Controls.BarGraph()
                .WithLabel(FormatBreakdownLabel(display, foreign ? -1 : 0))
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
            _spaceStatus.SetContent(new List<string>
            {
                $"  {prefix}  [dim]{FileEntry.FormatSize(p.BytesSoFar)}  {p.FilesScanned:N0} files[/]"
            });
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
                if (!p.PerChildBytes.TryGetValue(key, out var bytes)) bytes = 0;
                double share = bytes * 100.0 / total;
                bar.Value = Math.Min(share, 100);

                string display = key.Length == 0 ? "(files)" : key;
                bool foreign = bar.Label.Contains("other fs");
                bar.Label = FormatBreakdownLabel(display, foreign ? -1 : bytes);
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
