namespace EasySave.Core.Configuration;

public static class AppPaths
{
    private const string OverrideEnvironmentVariable = "EASYSAVE_APPDATA";

    public static string BaseDirectory => Path.Combine(GetLocalApplicationData(), "ProSoft", "EasySave");

    public static string ConfigDirectory => Path.Combine(BaseDirectory, "config");

    public static string LogsDirectory => Path.Combine(Directory.GetCurrentDirectory(), "logs");

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

    private static string GetLocalApplicationData()
    {
        var overridePath = Environment.GetEnvironmentVariable(OverrideEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return overridePath;
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    }
}
