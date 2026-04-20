using System.Collections.Concurrent;
using SharpConsoleUI;
using SharpConsoleUI.Layout;
using CXFiles.Models;
using CXFiles.Services;
using CXFiles.UI.Components;

namespace CXFiles.App;

public partial class CXFilesApp
{
    // Compositor overlay state. Two independent slots:
    //   - Center: empty-state messages ("Type more…", "No matches"). Only set when
    //     there are no rows to compete with.
    //   - Corner: active-walker badge ("⏳ searching…"). Painted in the top-right
    //     of the file list area so it stays visible alongside streaming results.
    private string? _searchOverlayCenter;
    private string? _searchOverlayCorner;

    private void SetSearchCenter(string? message)
    {
        if (_searchOverlayCenter == message) return;
        _searchOverlayCenter = message;
        _mainWindow?.Invalidate(true);
    }

    private void SetSearchCorner(string? message)
    {
        if (_searchOverlayCorner == message) return;
        _searchOverlayCorner = message;
        _mainWindow?.Invalidate(true);
    }

    private void PaintSearchOverlay(CharacterBuffer buffer, LayoutRect dirtyRegion, LayoutRect clipRect)
    {
        if (_mainWindow == null) return;
        if (_searchOverlayCenter == null && _searchOverlayCorner == null) return;

        var node = _mainWindow.GetLayoutNode(ActiveTab.FileList.Control);
        if (node == null) return;

        var rect = node.AbsoluteBounds;
        if (rect.Width <= 0 || rect.Height <= 0) return;

        var dimFg = new SharpConsoleUI.Color(140, 150, 170);
        var dimBg = new SharpConsoleUI.Color(15, 20, 35);
        var badgeFg = new SharpConsoleUI.Color(180, 200, 240);
        var badgeBg = new SharpConsoleUI.Color(30, 40, 65);

        // Center: empty-state message.
        var center = _searchOverlayCenter;
        if (center != null)
        {
            int textW = SharpConsoleUI.Helpers.UnicodeWidth.GetStringWidth(center);
            int x = rect.X + Math.Max(0, (rect.Width - textW) / 2);
            int y = rect.Y + Math.Max(0, rect.Height / 2);
            buffer.WriteStringClipped(x, y, center, dimFg, dimBg, rect);
        }

        // Top-right corner: walker activity badge.
        var corner = _searchOverlayCorner;
        if (corner != null)
        {
            int textW = SharpConsoleUI.Helpers.UnicodeWidth.GetStringWidth(corner);
            int x = rect.X + Math.Max(0, rect.Width - textW - 2);
            int y = rect.Y;
            buffer.WriteStringClipped(x, y, corner, badgeFg, badgeBg, rect);
        }
    }

    private void WireSearchBar(TabState tab)
    {
        tab.SearchBar.UpClicked += NavigateUp;
        tab.SearchBar.QueryChanged += q => StartSearch(tab, q);
        tab.SearchBar.RecurseToggled += value =>
        {
            tab.Search.Recurse = value;
            // If a search is currently active, re-run with the new scope.
            if (!string.IsNullOrEmpty(tab.Search.Query))
                StartSearch(tab, tab.Search.Query);
        };
        tab.SearchBar.Cleared += () => CancelAndRestore(tab);
        tab.SearchBar.Submitted += () =>
        {
            // Move focus to the file list when Enter is pressed in the search bar.
            SharpConsoleUI.Extensions.WindowControlExtensions.RequestFocus(
                tab.FileList.Control,
                SharpConsoleUI.Controls.FocusReason.Keyboard);
        };
    }

    private void StartSearch(TabState tab, string rawQuery)
    {
        // 1. Cancel any prior walker for this tab.
        var prior = tab.Search.Cts;
        prior?.Cancel();

        // 2. Parse ./ prefix → non-recursive override.
        string effectiveQuery;
        bool effectiveRecurse;
        if (rawQuery.StartsWith("./", StringComparison.Ordinal))
        {
            effectiveQuery = rawQuery.Substring(2);
            effectiveRecurse = false;
            tab.SearchBar.SetRecurseOverridden(true);
        }
        else
        {
            effectiveQuery = rawQuery;
            effectiveRecurse = tab.Search.Recurse;
            tab.SearchBar.SetRecurseOverridden(false);
        }

        tab.Search.Query = rawQuery;

        // 3. Empty raw query → exit search entirely.
        // NOTE: check rawQuery, not effectiveQuery. Typing just "./" parses to
        // an empty effectiveQuery but the user is mid-input — don't wipe their bar.
        if (string.IsNullOrEmpty(rawQuery))
        {
            CancelAndRestore(tab);
            return;
        }

        // Minimum query length — avoids flooding on single-letter queries.
        const int MinQueryLength = 2;
        if (effectiveQuery.Length < MinQueryLength)
        {
            _statusLine.ClearSearchProgress();
            // Snapshot once + clear results so the file list isn't
            // misleadingly populated with stale matches from a prior longer query.
            // The "type more" hint goes in the search bar label, not as a fake row.
            if (tab.Search.Restore == null)
                tab.Search.Restore = tab.FileList.CaptureSnapshot();
            SearchResultsDataSource emptyDs;
            if (tab.FileList.Control.DataSource is SearchResultsDataSource existingDs)
            {
                emptyDs = existingDs;
                emptyDs.Clear();
            }
            else
            {
                emptyDs = new SearchResultsDataSource();
                tab.FileList.EnterSearchMode(emptyDs);
            }
            SetSearchCenter("Type more to search…");
            SetSearchCorner(null);
            return;
        }

        // 4. Snapshot (once per search session) and enter search mode.
        SearchResultsDataSource results;
        if (tab.Search.Restore == null)
        {
            tab.Search.Restore = tab.FileList.CaptureSnapshot();
            results = new SearchResultsDataSource();
            tab.FileList.EnterSearchMode(results);
        }
        else if (tab.FileList.Control.DataSource is SearchResultsDataSource existing)
        {
            results = existing;
        }
        else
        {
            results = new SearchResultsDataSource();
            tab.FileList.EnterSearchMode(results);
        }

        // Centered "Type more"/"No matches" goes away — we're searching now.
        // Corner badge stays up while the walker runs (visible alongside results).
        SetSearchCenter(null);
        SetSearchCorner("⏳ searching…");

        // 5. Fresh cancellation token + walker/hydrator tasks.
        var cts = new CancellationTokenSource();
        tab.Search.Cts = cts;
        var ct = cts.Token;
        var hydratorQueue = new ConcurrentQueue<SearchRow>();
        var capturedResults = results;
        bool isFirstFlush = true;
        var batch = new List<SearchHit>();
        var flushLock = new object();

        bool limitHit = false;
        IProgress<SearchProgress> progress = new Progress<SearchProgress>(p =>
        {
            if (ct.IsCancellationRequested) return;
            _statusLine.SetSearchProgress(p.DirsScanned, p.MatchesFound);
            if (p.LimitReached)
            {
                limitHit = true;
                SetSearchCorner("⚠ scan limit hit · refine query");
            }
        });

        void FlushBatch()
        {
            List<SearchHit>? toFlush = null;
            lock (flushLock)
            {
                if (batch.Count > 0)
                {
                    toFlush = batch.ToList();
                    batch.Clear();
                }
            }
            if (toFlush == null || toFlush.Count == 0) return;

            _ws.EnqueueOnUIThread(() =>
            {
                if (ct.IsCancellationRequested) return;
                if (isFirstFlush)
                {
                    capturedResults.Clear();
                    isFirstFlush = false;
                    // Results are taking over the file list — center overlay
                    // would now obscure them. Corner badge stays.
                    SetSearchCenter(null);
                }
                var newRows = capturedResults.AppendHits(toFlush);
                foreach (var row in newRows)
                    hydratorQueue.Enqueue(row);
            });
        }

        // Walker task.
        _ = Task.Run(async () =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            long lastFlushMs = 0;
            try
            {
                await foreach (var hit in _fs.SearchAsync(
                    tab.Path, effectiveQuery, effectiveRecurse,
                    _config.Config.ShowHiddenFiles, progress, ct))
                {
                    lock (flushLock) batch.Add(hit);

                    var nowMs = sw.ElapsedMilliseconds;
                    int count;
                    lock (flushLock) count = batch.Count;
                    if (count >= 50 || nowMs - lastFlushMs >= 33)
                    {
                        lastFlushMs = nowMs;
                        FlushBatch();
                    }
                }
                FlushBatch();
            }
            catch (OperationCanceledException) { }
            catch { }
            finally
            {
                _ws.EnqueueOnUIThread(() =>
                {
                    if (ct.IsCancellationRequested) return;
                    _statusLine.ClearSearchProgress();
                    // Walker is done — drop the corner badge UNLESS the limit was hit
                    // (in which case the warning stays visible until the user acts).
                    if (!limitHit)
                        SetSearchCorner(null);
                    // If nothing was ever flushed, clear stale prior results
                    // and show the centered "No matches" message over the empty list.
                    if (isFirstFlush)
                    {
                        capturedResults.Clear();
                        SetSearchCenter(limitHit ? "Scan limit reached — no matches yet" : "No matches");
                    }
                });
            }
        }, ct);

        // Hydrator task — drains the queue and coalesces UI refreshes to ~15 Hz.
        _ = Task.Run(async () =>
        {
            var tickSw = System.Diagnostics.Stopwatch.StartNew();
            long lastNotifyMs = 0;
            const long notifyIntervalMs = 66; // ~15 Hz
            bool pendingNotify = false;

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    if (!hydratorQueue.TryDequeue(out var row))
                    {
                        if (pendingNotify)
                        {
                            pendingNotify = false;
                            _ws.EnqueueOnUIThread(() =>
                            {
                                if (ct.IsCancellationRequested) return;
                                capturedResults.NotifyChanged();
                            });
                        }
                        await Task.Delay(50, ct);
                        continue;
                    }
                    var full = await _fs.HydrateAsync(row.Hit.FullPath, ct);
                    if (full == null) continue;
                    _ws.EnqueueOnUIThread(() =>
                    {
                        if (ct.IsCancellationRequested) return;
                        capturedResults.SetHydrated(row, full);
                    });
                    pendingNotify = true;

                    var now = tickSw.ElapsedMilliseconds;
                    if (now - lastNotifyMs >= notifyIntervalMs)
                    {
                        lastNotifyMs = now;
                        pendingNotify = false;
                        _ws.EnqueueOnUIThread(() =>
                        {
                            if (ct.IsCancellationRequested) return;
                            capturedResults.NotifyChanged();
                        });
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch { }
        }, ct);
    }

    private void CancelAndRestore(TabState tab)
    {
        tab.Search.Cts?.Cancel();
        tab.Search.Cts?.Dispose();
        tab.Search.Cts = null;

        _statusLine.ClearSearchProgress();
        SetSearchCenter(null);
        SetSearchCorner(null);

        if (tab.Search.Restore == null)
        {
            // Nothing to restore; just clear bar state.
            tab.SearchBar.Clear();
            tab.Search.Query = "";
            return;
        }

        tab.FileList.ExitSearchMode();
        tab.FileList.RestoreSnapshot(tab.Search.Restore);
        tab.Search.Restore = null;
        tab.Search.Query = "";
        tab.SearchBar.Clear();
        // PromptControl unfocuses itself on Escape — re-focus so the next
        // keystroke starts a fresh search instead of going to the file list.
        tab.SearchBar.Focus();
    }
}
