using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using EasySave.GUI.ViewModels;
using EasySave.GUI.Views;

namespace EasySave.GUI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Dispatcher.UIThread.UnhandledException += (_, args) =>
        {
            args.Handled = true;
            ReportGlobalException(args.Exception);
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            args.SetObserved();
            Dispatcher.UIThread.Post(() => ReportGlobalException(args.Exception));
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                WriteStartupFailure(exception);
            }
        };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            try
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(),
                };
            }
            catch (Exception exception)
            {
                desktop.MainWindow = CreateStartupErrorWindow(exception);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ReportGlobalException(Exception exception)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow.DataContext: MainWindowViewModel viewModel })
        {
            viewModel.ReportError(exception);
        }
    }

    private static Window CreateStartupErrorWindow(Exception exception)
    {
        return new Window
        {
            Title = "EasySave - Erreur",
            Width = 560,
            Height = 220,
            Content = new Border
            {
                Padding = new Thickness(18),
                Child = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "EasySave n'a pas pu démarrer correctement.",
                            FontWeight = Avalonia.Media.FontWeight.Bold,
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap
                        },
                        new TextBlock
                        {
                            Text = $"Erreur inattendue : {exception.Message}",
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap
                        }
                    }
                }
            }
        };
    }

    private static void WriteStartupFailure(Exception exception)
    {
        try
        {
            var errorPath = Path.Combine(Path.GetTempPath(), "EasySave-startup-error.txt");
            File.WriteAllText(errorPath, $"EasySave fatal error:{Environment.NewLine}{exception}");
        }
        catch
        {
        }
    }
}
