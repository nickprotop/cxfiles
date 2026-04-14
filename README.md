# CXFiles

<div align="center">

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Linux%20|%20macOS%20|%20Windows-orange.svg)]()

</div>

**A cross-platform terminal file explorer built on [SharpConsoleUI](https://github.com/nickprotop/ConsoleEx).**

<div align="center">

### If you find CXFiles useful, please consider giving it a star!

It helps others discover the project and motivates continued development.

[![GitHub stars](https://img.shields.io/github/stars/nickprotop/cxfiles?style=for-the-badge&logo=github&color=yellow)](https://github.com/nickprotop/cxfiles/stargazers)

</div>

CXFiles brings a polished, Windows Explorer-style file manager to the terminal. Three-pane layout with folder tree, file list, and detail panel. Tabs, full file operations with background progress tracking, a rich tabbed Properties dialog with live disk-usage breakdown, trash support, context menus, and keyboard-driven navigation.

**Browse. Copy. Manage.**

![CXFiles Screenshot](.github/screenshot.png)

## Quick Start

**Option 1: One-line install** (Linux/macOS, no .NET required)
```bash
curl -fsSL https://raw.githubusercontent.com/nickprotop/cxfiles/master/install.sh | bash
cxfiles
```

**Windows** (PowerShell)
```powershell
irm https://raw.githubusercontent.com/nickprotop/cxfiles/master/install.ps1 | iex
```

**Option 2: Build from source** (requires .NET 10)
```bash
git clone https://github.com/nickprotop/cxfiles.git
cd cxfiles
./build-and-install.sh
cxfiles
```

## Features

| | |
|---|---|
| 🗂️ **Three-Pane Layout** | Folder tree, sortable file list with checkboxes, toggleable detail panel |
| 🧭 **Tabs** | Multiple open folders per window; `Ctrl+T` to open, `Ctrl+W` to close, `Alt+←/→` to cycle, `Ctrl+1..5` to jump |
| 📋 **File Operations** | Copy, cut, paste, delete, rename with background progress tracking |
| 🗑️ **Trash Support** | Cross-platform trash (XDG on Linux, ~/.Trash on macOS, Recycle Bin on Windows) with restore and empty |
| 📎 **Clipboard Portal** | View, manage, and remove individual clipboard items (`Ctrl+B`) |
| 🌳 **Folder Tree** | Lazy-loading tree with single-click navigation and two-way sync |
| 🔍 **Filter & Sort** | Fuzzy filtering (`/`), click-to-sort columns, column resize |
| 📊 **Operations Portal** | Live progress bars, per-file status, cancel buttons, dismiss completed (`Ctrl+P`) |
| 🖱️ **Context Menus** | Right-click on files or folders for contextual actions |
| 📁 **Breadcrumb Bar** | Clickable path segments with quick-access locations (Home, Desktop, Docs, Downloads, Trash) |
| 👁️ **Detail Panel** | Selection preview **and** a folder card when nothing is selected (toggle with `F3`) |
| 🖱️ **Null-Selection UX** | Click empty space to deselect; `F4` then shows properties for the current folder |
| 📐 **Properties Dialog** | Tabbed modal: General, Permissions (`rwxr-xr-x` + owner/group), Space, Checksums |
| 💽 **Space Tab** | Live drive usage + a **breakdown** of the folder's immediate children as sorted bar graphs — answers "where's my disk going?" without leaving the app |
| 🔐 **Checksums Tab** | On-demand MD5 / SHA256 with progress bar; cancel by closing the dialog |
| ✅ **Multi-Select** | Checkbox selection with bulk copy, cut, delete |
| 🔑 **Sudo Elevation** | Password dialog for privileged operations on Linux/macOS |
| ⚙️ **Options Dialog** | NavigationView-based settings with per-OS config storage |
| 👻 **Hidden Files** | Toggle visibility with `Ctrl+H` |
| 📡 **File Watcher** | Auto-refresh on external filesystem changes |
| 🎨 **Polished UI** | Gradient background, smooth-gradient bar graphs, alternating row tints, truncation fade, column separators, panel headers |

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| `Enter` | Open file/folder |
| `Backspace` | Navigate to parent |
| `Delete` | Delete selected (trash or permanent) |
| `F2` | Rename |
| `F3` | Toggle detail panel |
| `F4` | Properties (selection, or current folder if nothing selected) |
| `F5` | Refresh |
| `Ctrl+N` | New file |
| `Ctrl+Shift+N` | New folder |
| `Ctrl+T` | New tab |
| `Ctrl+W` | Close tab |
| `Alt+←` / `Alt+→` | Previous / next tab |
| `Ctrl+1`…`Ctrl+5` | Jump to tab |
| `Ctrl+C` | Copy |
| `Ctrl+X` | Cut |
| `Ctrl+V` | Paste |
| `Ctrl+B` | Clipboard portal |
| `Ctrl+P` | Operations portal |
| `Ctrl+H` | Toggle hidden files |
| `Ctrl+O` | Options |
| `/` | Filter file list |
| `Ctrl+Q` | Quit |

## The Properties dialog

Press `F4` on a file, folder, or drive (or click **Folder Props** in the toolbar when nothing is selected). You get a tabbed modal with four sections:

- **General** — name, type, location, size, modified / created / accessed, attributes, symlink target.
- **Permissions** — Unix mode formatted as `rwxr-xr-x` plus numeric (`0755`), owner and group resolved via `stat`. On Windows, `FileAttributes` flags.
- **Space** — a live view of where disk space is going:
    - Drive totals (total / used / free) with a smooth-gradient `Used` bar.
    - For the current folder or drive, a **breakdown** of its immediate children, each rendered as its own bar graph. Sizes accumulate live while the scan runs in the background; on completion, rows reorder descending by size and anything beyond the top 20 collapses into an `(others: N)` bucket.
    - Cross-filesystem children (`/proc`, `/sys`, foreign mounts) are detected via `DriveInfo`, skipped by the walker, and marked `(other fs)` in their row.
    - Permission-denied entries are counted live and surfaced as a yellow warning so you know the total is a lower bound.
    - The entire scan cancels instantly when the dialog closes.
- **Checksums** — on-demand MD5 / SHA256 for files, computed in 64 KB chunks with a throttled progress bar.

## Architecture

CXFiles uses Microsoft.Extensions.DependencyInjection with a clean service layer:

- **IFileSystemService** — File system abstraction (listing, copy, move, delete, watch)
- **ITrashService** — Cross-platform trash with XDG (Linux/macOS) and Windows Recycle Bin implementations
- **SudoService** — Privilege elevation via sudo on Linux/macOS
- **IConfigService** — Per-OS configuration (XDG on Linux, AppData on Windows, Library on macOS)
- **OperationManager** — Background operation tracking with throttled progress events
- **UI Components** — Modular panels (BreadcrumbBar, FileListPanel, FolderTreePanel, DetailPanel, StatusLine)
- **Modals** — Async `TaskCompletionSource`-based dialogs (DeleteConfirm, Rename, NewItem, Properties, Options, Sudo)
- **Portals** — Operations portal, clipboard portal, context menus (portal-based overlays)

## Requirements

- .NET 10 SDK (for building from source)
- A terminal with Unicode support (Kitty, WezTerm, Ghostty, Windows Terminal, iTerm2)

## License

[MIT](LICENSE)
