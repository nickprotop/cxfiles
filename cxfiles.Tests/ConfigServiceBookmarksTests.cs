using CXFiles.Models;
using CXFiles.Services;

namespace CXFiles.Tests;

public class ConfigServiceBookmarksTests
{
    private static ConfigService NewService()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "cxfiles-test-" + Guid.NewGuid());
        Directory.CreateDirectory(tmp);
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", tmp);
        return new ConfigService();
    }

    [Fact]
    public void Add_AppendsBookmark_FiresEvent()
    {
        var svc = NewService();
        bool fired = false;
        svc.BookmarksChanged += (_, _) => fired = true;

        svc.AddBookmark("/home/me/work", "Work");

        Assert.Contains(svc.Config.Bookmarks, b => b.Path == "/home/me/work" && b.Name == "Work");
        Assert.True(fired);
    }

    [Fact]
    public void Add_NullName_DefaultsToLeafName()
    {
        var svc = NewService();
        svc.AddBookmark("/home/me/work", null);
        Assert.Equal("work", svc.Config.Bookmarks.Single().Name);
    }

    [Fact]
    public void Add_DuplicatePath_IsNoOp()
    {
        var svc = NewService();
        svc.AddBookmark("/p", "P");
        int fireCount = 0;
        svc.BookmarksChanged += (_, _) => fireCount++;

        svc.AddBookmark("/p", "Other");

        Assert.Single(svc.Config.Bookmarks);
        Assert.Equal(0, fireCount);
    }

    [Fact]
    public void Remove_RemovesAndFires()
    {
        var svc = NewService();
        svc.AddBookmark("/p", "P");
        bool fired = false;
        svc.BookmarksChanged += (_, _) => fired = true;

        svc.RemoveBookmark("/p");

        Assert.Empty(svc.Config.Bookmarks);
        Assert.True(fired);
    }

    [Fact]
    public void Remove_Missing_IsSilentNoOp()
    {
        var svc = NewService();
        bool fired = false;
        svc.BookmarksChanged += (_, _) => fired = true;

        svc.RemoveBookmark("/does-not-exist");

        Assert.Empty(svc.Config.Bookmarks);
        Assert.False(fired);
    }

    [Fact]
    public void Rename_UpdatesName_Fires()
    {
        var svc = NewService();
        svc.AddBookmark("/p", "P");
        bool fired = false;
        svc.BookmarksChanged += (_, _) => fired = true;

        svc.RenameBookmark("/p", "Project");

        Assert.Equal("Project", svc.Config.Bookmarks.Single().Name);
        Assert.True(fired);
    }

    [Fact]
    public void Bookmarks_PersistAcrossInstances()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "cxfiles-persist-" + Guid.NewGuid());
        Directory.CreateDirectory(tmp);
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", tmp);

        var a = new ConfigService();
        a.AddBookmark("/x", "X");

        var b = new ConfigService();
        Assert.Single(b.Config.Bookmarks);
        Assert.Equal("X", b.Config.Bookmarks[0].Name);
    }
}
