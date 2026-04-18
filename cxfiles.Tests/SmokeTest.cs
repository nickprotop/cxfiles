// cxfiles.Tests/SmokeTest.cs
namespace CXFiles.Tests;

public class SmokeTest
{
    [Fact]
    public void TestProjectCompilesAndReferencesMainProject()
    {
        var type = typeof(CXFiles.Services.CXFilesConfig);
        Assert.NotNull(type);
    }
}
