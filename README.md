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

CXFiles brings a polished, Windows Explorer-style file manager to the terminal. Three-pane layout with folder tree, file list, and detail panel. Full file operations with background progress tracking, context menus, and keyboard-driven navigation.

**Browse. Copy. Manage.**

## Quick Start

**Build from source** (requires .NET 10)
```bash
git clone https://github.com/nickprotop/cxfiles.git
cd cxfiles
dotnet run --project cxfiles
```

## Features

| | |
|---|---|
| 🗂️ **Three-Pane Layout** | Folder tree, sortable file list with checkboxes, toggleable detail panel |
| 📋 **File Operations** | Copy, cut, paste, delete, rename with background progress tracking |
| 🌳 **Folder Tree** | Lazy-loading tree with single-click navigation and two-way sync |
| 🔍 **Filter & Sort** | Fuzzy filtering (`/`), click-to-sort columns, column resize |
| 📊 **Operations Portal** | Live progress bars, per-file status, cancel buttons, dismiss completed |
| 🖱️ **Context Menus** | Right-click on files or folders for contextual actions |
| 📁 **Breadcrumb Bar** | Clickable path segments with item count |
| 👁️ **Detail Panel** | File properties, permissions, text preview (toggle with F3) |
| ✅ **Multi-Select** | Checkbox selection with bulk copy, cut, delete |
| ⚙️ **Options Dialog** | NavigationView-based settings with per-OS config storage |
| 👻 **Hidden Files** | Toggle visibility with Ctrl+H |
| 📡 **File Watcher** | Auto-refresh on external filesystem changes |
| 🎨 **Polished UI** | Gradient background, alternating row tints, truncation fade, cxpost-style toolbar |

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| Enter | Open file/folder |
| Backspace | Navigate to parent |
| Delete | Delete selected |
| F2 | Rename |
| F3 | Toggle detail panel |
| F4 | Properties |
| F5 | Refresh |
| Ctrl+N | New file |
| Ctrl+Shift+N | New folder |
| Ctrl+C | Copy |
| Ctrl+X | Cut |
| Ctrl+V | Paste |
| Ctrl+H | Toggle hidden files |
| Ctrl+O | Options |
| / | Filter file list |
| Ctrl+Q | Quit |

## Architecture

CXFiles uses Microsoft.Extensions.DependencyInjection with a clean service layer:

- **IFileSystemService** — File system abstraction (listing, copy, move, delete, watch)
- **IConfigService** — Per-OS configuration (XDG on Linux, AppData on Windows, Library on macOS)
- **OperationManager** — Background operation tracking with throttled progress events
- **UI Components** — Modular panels (BreadcrumbBar, FileListPanel, FolderTreePanel, DetailPanel, StatusLine)
- **Modals** — Async `TaskCompletionSource`-based dialogs (Confirm, Rename, NewItem, Properties, Options)

## Requirements

- .NET 10 SDK
- A terminal with Unicode support (Kitty, WezTerm, Ghostty, Windows Terminal, iTerm2)

## License

[MIT](LICENSE)
