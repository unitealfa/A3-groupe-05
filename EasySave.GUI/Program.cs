using Avalonia;
using System;
using System.IO;

namespace EasySave.GUI;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception exception)
        {
            WriteStartupFailure(exception);
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    private static void WriteStartupFailure(Exception exception)
    {
        try
        {
            var errorPath = Path.Combine(Path.GetTempPath(), "EasySave-startup-error.txt");
            File.WriteAllText(errorPath, $"EasySave startup error:{Environment.NewLine}{exception}");
        }
        catch
        {
            // If even the temp folder is unavailable, there is no reliable place left to report.
        }
    }
}
