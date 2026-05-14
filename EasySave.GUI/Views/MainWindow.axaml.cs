using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using EasySave.GUI.ViewModels;
using Avalonia.Controls.Selection;

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
            try
            {
                await viewModel.InitializeAsync();
            }
            catch (Exception exception)
            {
                viewModel.ReportError(exception);
            }
        }
    }

    private async void SourceBrowse_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            await PickSourceAsync();
        }
        catch (Exception exception)
        {
            ViewModel?.ReportError(exception);
        }
    }

    private async void TargetBrowse_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            await PickFolderAsync(path => ViewModel?.SetTargetDirectory(path));
        }
        catch (Exception exception)
        {
            ViewModel?.ReportError(exception);
        }
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
    private async Task PickSourceAsync()
    {
        var picker = new SourceSelectionWindow(ViewModel?.SourceDirectory);
        var selectedSource = await picker.ShowDialog<string?>(this);
        if (!string.IsNullOrWhiteSpace(selectedSource))
        {
            ViewModel?.SetSourceDirectory(selectedSource);
        }
    }

    private void JobsListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox listBox || ViewModel is null)
        {
            return;
        }

        var selectedJobs = listBox.SelectedItems?
            .OfType<JobListRow>()
            .Select(row => row.Job)
            .ToList() ?? [];

        ViewModel.SetSelectedJobs(selectedJobs);
    }
}
