using EasySave.Core.Configuration;

namespace EasySave.Tests;

public sealed class AppPathsTests
{
    [Fact]
    public void AppPathsUseProjectRootDirectories()
    {
        AppPaths.EnsureDirectories();

        Assert.Equal(Path.GetFullPath(AppContext.BaseDirectory), AppPaths.BaseDirectory);
        Assert.Equal(Path.Combine(Path.GetFullPath(AppContext.BaseDirectory), "config", "jobs.json"), AppPaths.JobsFilePath);
        Assert.True(Directory.Exists(AppPaths.ConfigDirectory));
        Assert.True(Directory.Exists(AppPaths.LogsDirectory));
        Assert.True(Directory.Exists(AppPaths.StateDirectory));
    }
}
