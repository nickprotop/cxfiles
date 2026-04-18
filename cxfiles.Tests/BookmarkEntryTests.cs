// cxfiles.Tests/BookmarkEntryTests.cs
using CXFiles.Models;

namespace CXFiles.Tests;

public class BookmarkEntryTests
{
    [Fact]
    public void Entry_StoresNameAndPath()
    {
        var e = new BookmarkEntry("Projects", "/home/me/projects");
        Assert.Equal("Projects", e.Name);
        Assert.Equal("/home/me/projects", e.Path);
    }

    [Fact]
    public void Entry_IsValueEqual()
    {
        var a = new BookmarkEntry("P", "/p");
        var b = new BookmarkEntry("P", "/p");
        Assert.Equal(a, b);
    }
}
