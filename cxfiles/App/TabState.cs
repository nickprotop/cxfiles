using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using CXFiles.UI.Components;
using CXFiles.Services;

namespace CXFiles.App;

public class TabState
{
    public string Path { get; set; }
    public bool ViewingTrash { get; set; }
    public StatusBarControl Header { get; }
    public FileListPanel FileList { get; }
    public ScrollablePanelControl Container { get; }
    public IDisposable? FileWatcher { get; set; }

    private static readonly Color HeaderBg = new(40, 50, 70, 160);

    public TabState(IFileSystemService fs, bool showHidden, bool autoSelectFirstItem, string initialPath)
    {
        Path = initialPath;

        Header = Controls.StatusBar()
            .AddLeftText($"[grey70]{System.IO.Path.GetFileName(initialPath)}[/]")
            .WithMargin(1, 0, 0, 0)
            .Build();
        Header.BackgroundColor = HeaderBg;
        Header.HorizontalAlignment = HorizontalAlignment.Stretch;

        FileList = new FileListPanel(fs, showHidden, autoSelectFirstItem);

        Container = Controls.ScrollablePanel()
            .WithBackgroundColor(Color.Transparent)
            .Build();
        Container.ShowScrollbar = false;
        Container.VerticalAlignment = VerticalAlignment.Fill;
        Container.HorizontalAlignment = HorizontalAlignment.Stretch;
        Container.AddControl(Header);
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

    public void UpdateHeader()
    {
        Header.ClearAll();
        Header.AddLeftText($"[grey70]{SharpConsoleUI.Parsing.MarkupParser.Escape(TabTitle)}[/]");
    }

    public void Dispose()
    {
        FileWatcher?.Dispose();
        FileWatcher = null;
    }
}
