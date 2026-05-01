using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using EasySave.GUI.ViewModels;

namespace EasySave.GUI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }

    private async void SourceBrowse_Click(object? sender, RoutedEventArgs e)
    {
        await PickFolderAsync(path => ViewModel?.SetSourceDirectory(path));
    }

    private async void TargetBrowse_Click(object? sender, RoutedEventArgs e)
    {
        await PickFolderAsync(path => ViewModel?.SetTargetDirectory(path));
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private async Task PickFolderAsync(Action<string> onPicked)
    {
        if (!StorageProvider.CanOpen)
        {
            return;
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false
        });

        var folder = folders.FirstOrDefault();
        var localPath = folder?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(localPath))
        {
            onPicked(localPath);
        }
    }
}
