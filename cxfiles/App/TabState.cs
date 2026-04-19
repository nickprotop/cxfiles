using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using CXFiles.UI.Components;
using CXFiles.Services;

namespace CXFiles.App;

public sealed class SearchState
{
    public string Query { get; set; } = "";
    public bool Recurse { get; set; } = true;
    public CancellationTokenSource? Cts { get; set; }
    public FileListSnapshot? Restore { get; set; }
}

public class TabState
{
    public string Path { get; set; }
    public bool ViewingTrash { get; set; }
    public SearchBar SearchBar { get; }
    public FileListPanel FileList { get; }
    public ScrollablePanelControl Container { get; }
    public IDirectoryWatcher? FileWatcher { get; set; }
    public SearchState Search { get; } = new();

    public TabState(IFileSystemService fs, bool showHidden, bool autoSelectFirstItem, string initialPath)
    {
        Path = initialPath;

        SearchBar = new SearchBar();

        FileList = new FileListPanel(fs, showHidden, autoSelectFirstItem);

        var rule = Controls.RuleBuilder()
            .WithColor(Color.Grey27)
            .Build();

        Container = Controls.ScrollablePanel()
            .WithBackgroundColor(Color.Transparent)
            .Build();
        Container.ShowScrollbar = false;
        Container.VerticalAlignment = VerticalAlignment.Fill;
        Container.HorizontalAlignment = HorizontalAlignment.Stretch;
        Container.AddControl(SearchBar.Control);
        Container.AddControl(rule);
        Container.AddControl(FileList.Control);
    }

    public string TabTitle
    {
        get
        {
            if (ViewingTrash) return "Trash";
            var name = System.IO.Path.GetFileName(Path);
            return string.IsNullOrEmpty(name) ? Path : name;
        }
    }

    public void Dispose()
    {
        FileWatcher?.Dispose();
        FileWatcher = null;
        Search.Cts?.Cancel();
        Search.Cts?.Dispose();
        Search.Cts = null;
    }
}
