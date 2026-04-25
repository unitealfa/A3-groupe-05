using EasySave.Core.Configuration;

namespace EasySave.Tests;

public sealed class AppPathsTests : IDisposable
{
    private readonly string previousOverride;

    public AppPathsTests()
    {
        previousOverride = Environment.GetEnvironmentVariable("EASYSAVE_APPDATA") ?? string.Empty;
    }

    [Fact]
    public void AppPathsUsePortableLocalApplicationData()
    {
        var root = Path.Combine(Path.GetTempPath(), $"easysave-tests-{Guid.NewGuid():N}");
        Environment.SetEnvironmentVariable("EASYSAVE_APPDATA", root);

        AppPaths.EnsureDirectories();

        Assert.StartsWith(root, AppPaths.BaseDirectory);
        Assert.EndsWith(Path.Combine("ProSoft", "EasySave", "config", "jobs.json"), AppPaths.JobsFilePath);
        Assert.True(Directory.Exists(AppPaths.ConfigDirectory));
        Assert.True(Directory.Exists(AppPaths.LogsDirectory));
        Assert.True(Directory.Exists(AppPaths.StateDirectory));
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("EASYSAVE_APPDATA", string.IsNullOrEmpty(previousOverride) ? null : previousOverride);
    }
}
