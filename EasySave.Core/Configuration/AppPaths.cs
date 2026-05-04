namespace EasySave.Core.Configuration;

public static class AppPaths
{
    public static string BaseDirectory => Path.GetFullPath(AppContext.BaseDirectory);

    public static string ConfigDirectory => Path.Combine(BaseDirectory, "config");

    public static string LogsDirectory => Path.Combine(BaseDirectory, "logs");

    public static string StateDirectory => Path.Combine(BaseDirectory, "state");

    public static string JobsFilePath => Path.Combine(ConfigDirectory, "jobs.json");

    public static string SettingsFilePath => Path.Combine(ConfigDirectory, "settings.json");

    public static string StateFilePath => Path.Combine(StateDirectory, "state.json");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(ConfigDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(StateDirectory);
    }
}
