# CXFiles Roadmap

A short list of features in the queue. Everything here passes the focus
filter: it makes browsing, copying, or managing files **better, faster,
or more delightful**. We are not turning cxfiles into an editor, a
multiplexer, a sync tool, or a plugin platform.

## In progress

### Favorites + Go-to-path + Extended previews (bundled)

Three user-facing quality-of-life features shipping together as one
tested commit. Spec:
`docs/superpowers/specs/2026-04-18-favorites-gotopath-previews-design.md`.

**Favorites** (folders). `Ctrl+D` adds the current folder. Bookmarks
render as a "Favorites" section at the top of the folder tree and
as a `⭐ ▾` dropdown button on the breadcrumb right-bar. `Del` on a
bookmark node removes it; right-click for Rename / Remove; context
menu entry "Add to Favorites" on any folder (tree, file list, or
empty-area folder menu). Persisted via `IConfigService`. Missing
targets render dim with a status-line warning on click; never
auto-removed.

**Go-to-path.** `Ctrl+L` (or the `❯ Go to` button at the start of
the breadcrumb) replaces the breadcrumb segments with a text input.
Filesystem tab-completion via a portal dropdown: Tab / Ctrl+Space
open it, arrow keys navigate, Enter picks a highlighted candidate
(URL-bar style — Enter without prior arrow-navigation commits the
typed path). Debounced auto-open (~200 ms) on typing when the
portal is closed. Handles `~`, absolute, and relative paths. Esc
closes the portal, then exits edit mode.

**Extended previews.** `.pdf` (text extract via PdfPig) and
`.md` / `.markdown` / `.mdown` / `.mkd` (rendered via Markdig)
now have dedicated branches in the DetailPanel, ahead of the
plain-text fallback.

Also: the breadcrumb's leading chip now shows the actual filesystem
root (`◈ /` on Linux, `◈ C:\` on Windows) instead of the static
"cxfiles" label, so the label matches the click action.

**Out of scope** (deferred to Next up): back/forward history, PDF
first-page thumbnails, markdown tables, syntax-highlighted code
blocks, content search inside PDFs.

## Next up

### 1. Back/forward history per tab

Originally bundled with bookmarks; split off so favorites could ship
independently. Each `TabState` keeps two stacks of visited paths.
`Alt+Backspace` goes back, `Alt+Shift+Backspace` goes forward. Two
clickable arrows on the breadcrumb bar mirror the keybinds. Search
activation does not push history; clearing search does not pop.

**Files to touch:** `App/TabState.cs`, `App/CXFilesApp.cs`,
`App/CXFilesApp.Keyboard.cs`, `UI/Components/BreadcrumbBar.cs`.

**Estimate:** ~0.5 day.

---

### 2. Open in editor / Open terminal here

Two integrations every file manager needs.

**Open in editor.** Configurable command in Options (default `$EDITOR`
on Linux/macOS, `notepad` on Windows). Context menu entry plus a
keybind (`Ctrl+E`?). Spawns the editor in a detached process pointed
at the selected file or current folder.

**Open terminal here.** Two flavors:

- **Embedded** — open a `TerminalControl` (SharpConsoleUI already
  ships one with `LinuxPtyBackend` and `WindowsPtyBackend`) in a modal
  or a portal, `cd`'d to the current folder. Linux-tested upstream;
  Windows pty backend exists but is not yet validated in this app.
  This is the polished path — it keeps the user inside cxfiles.
- **External** — fall back to spawning the system terminal
  (`x-terminal-emulator` / `wt.exe` / `Terminal.app`) at the current
  folder for the case where the embedded path is unavailable or the
  user prefers their real shell.

User picks which path is default in Options. Embedded is preferred
where it works.

**Files to touch:** `Services/` (new `LauncherService` for spawning
external commands), `App/CXFilesApp.cs`, `App/CXFilesApp.Keyboard.cs`,
`UI/ContextMenuBuilder.cs`, `UI/Modals/OptionsModal.cs`,
new `UI/Modals/TerminalModal.cs` for the embedded variant.

**Estimate:** ~1 day for external, +1 day for embedded.

**Risks:** the embedded `TerminalControl` is largely unproven on
Windows. If validation fails, ship external-only and gate embedded
behind a config flag.

---

### 3. Image preview in the detail panel

The differentiator. SharpConsoleUI has an `ImageControl`; this is
about wiring it into the detail panel for image file selections and
making sure the visual integration is polished.

**What it does.** When the selected entry is an image
(`.png .jpg .jpeg .gif .webp .bmp`), the detail panel renders a
thumbnail using `ImageControl` instead of the text preview. Below the
thumbnail: dimensions, format, color depth, file size. Falls back to
text-only metadata on terminals that don't support the chosen image
protocol.

**Open questions to settle during implementation:**

- Which protocol(s) does `ImageControl` actually support — sixel,
  kitty graphics, iTerm inline, or something else? Detection logic
  needs to live somewhere (probably already does in SharpConsoleUI).
- How does it handle aspect ratio in a fixed-cell grid?
- Does it block on decode for large images, or stream?
- What's the fallback story when the protocol isn't supported?

**Files to touch:** `UI/Components/DetailPanel.cs` mainly. New
`ShowImage(FileEntry)` branch parallel to `ShowEntry` / `ShowLoading`.
Maybe a `Models/FileEntry.cs` extension to classify image extensions.

**Estimate:** 2–4 days, with real risk that the result depends on the
user's terminal.

---

### 4. Archive browsing & extract (read-only)

Browse `.zip`, `.tar`, `.tar.gz`, `.tgz` like a folder. No editing in
v1 — extract-here is the only write operation.

**What it does.** Activating an archive in the file list opens a new
tab whose `FileList` is backed by an `ArchiveDataSource` (a sibling
to `FileDataSource` and `SearchResultsDataSource`, like we did for
search). Path bar shows `archive.zip › subfolder/`. Selecting an
entry in the archive shows its metadata in the detail panel.
`Extract here` and `Extract to…` actions in the context menu and
toolbar handle write.

**.NET BCL coverage:** `System.IO.Compression.ZipFile` /
`ZipArchive` for zip, `System.Formats.Tar.TarReader` for tar,
`System.IO.Compression.GZipStream` for gzip. No external dependencies.

**Out of scope for v1:** creating archives, modifying archives,
encrypted archives, rar/7z (those need third-party libraries — defer
until users explicitly ask).

**Files to touch:** new `Services/ArchiveService.cs`, new
`UI/Components/ArchiveDataSource.cs`, `App/TabState.cs` (mode flag),
`App/CXFilesApp.cs` (`FileActivated` handler dispatches archives),
context menu actions.

**Estimate:** ~2 days for read-only browse + extract.

---

## Deferred (worth doing eventually)

- **Bulk rename with patterns** — power-user modal with regex /
  template / numbering. No mainstream TUI competitor has this.
- **Saved searches** — small, only valuable once a few users want it.
- **Content search (grep mode)** — was deferred from the search spec;
  complements name search.
- **Folder compare / sync** — big scope, niche audience.
- **Permissions editor UI** — chmod/chown dialog. Niche, complex.
- **Archive write** — create / modify archives. v2 of feature 4.

## Explicitly out of scope

- Multiple top-level windows (tabs cover multi-context).
- Theme / accent customization (cosmetic, low ROI, infinite bikeshed).
- Plugin system / scripting (resist until users ask explicitly).
- Cloud / sync integration.
- Built-in editor (`Open in editor` is the right answer).

## Principles

- **Polish over breadth.** A small set of features that look and feel
  great beats a long list that almost works.
- **Cross-platform parity.** Anything we ship should work on Linux,
  macOS, and Windows or be explicitly gated with a clear fallback.
- **Reuse SharpConsoleUI building blocks.** We own that library —
  every new file-manager feature should ask "is there already a
  control for this?" first. ImageControl, TerminalControl,
  BarGraphControl, TabControl, ColorGradient, PostBufferPaint
  compositor — all already there.
- **Ship in slices.** A feature is done when it's wired, polished,
  and the README + Help modal know about it.
