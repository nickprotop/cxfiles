using CXFiles.UI;

namespace CXFiles.Tests;

public class PathCompleterTests
{
    private string _tmp = null!;

    public PathCompleterTests()
    {
        _tmp = Path.Combine(Path.GetTempPath(), "cxfiles-pc-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(_tmp, "Apple"));
        Directory.CreateDirectory(Path.Combine(_tmp, "Apricot"));
        Directory.CreateDirectory(Path.Combine(_tmp, "Banana"));
        Directory.CreateDirectory(Path.Combine(_tmp, ".hidden"));
    }

    [Fact]
    public void SplitFragment_ReturnsParentAndFragment()
    {
        var (parent, fragment) = PathCompleter.SplitFragment(Path.Combine(_tmp, "Ap"));
        Assert.Equal(_tmp, parent);
        Assert.Equal("Ap", fragment);
    }

    [Fact]
    public void SplitFragment_TrailingSeparator_FragmentIsEmpty()
    {
        var (parent, fragment) = PathCompleter.SplitFragment(_tmp + Path.DirectorySeparatorChar);
        Assert.Equal(_tmp, parent);
        Assert.Equal("", fragment);
    }

    [Fact]
    public void Complete_Prefix_ReturnsMatchingDirectories()
    {
        var results = PathCompleter.Complete(_tmp, "Ap", includeHidden: false);
        Assert.Equal(new[] { "Apple", "Apricot" }, results);
    }

    [Fact]
    public void Complete_ExcludesHiddenByDefault()
    {
        var results = PathCompleter.Complete(_tmp, "", includeHidden: false);
        Assert.DoesNotContain(".hidden", results);
    }

    [Fact]
    public void Complete_IncludesHiddenWhenRequested()
    {
        var results = PathCompleter.Complete(_tmp, "", includeHidden: true);
        Assert.Contains(".hidden", results);
    }

    [Fact]
    public void LongestCommonPrefix_MultipleMatches()
    {
        Assert.Equal("Ap", PathCompleter.LongestCommonPrefix(new[] { "Apple", "Apricot" }));
    }

    [Fact]
    public void LongestCommonPrefix_Single_ReturnsFull()
    {
        Assert.Equal("Banana", PathCompleter.LongestCommonPrefix(new[] { "Banana" }));
    }

    [Fact]
    public void LongestCommonPrefix_None_ReturnsEmpty()
    {
        Assert.Equal("", PathCompleter.LongestCommonPrefix(Array.Empty<string>()));
    }

    [Fact]
    public void Resolve_ExpandsTilde()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var r = PathCompleter.Resolve("~/docs", baseDir: "/tmp");
        Assert.Equal(Path.GetFullPath(Path.Combine(home, "docs")), r);
    }

    [Fact]
    public void Resolve_Relative_UsesBaseDir()
    {
        var r = PathCompleter.Resolve("sub", baseDir: _tmp);
        Assert.Equal(Path.GetFullPath(Path.Combine(_tmp, "sub")), r);
    }

    [Fact]
    public void Resolve_Absolute_PassesThrough()
    {
        var r = PathCompleter.Resolve("/etc", baseDir: "/tmp");
        Assert.Equal(Path.GetFullPath("/etc"), r);
    }
}
