using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;

namespace EasySave.GUI.Views;

public partial class SourceSelectionWindow : Window
{
    private readonly ObservableCollection<SourceSelectionEntry> entries = [];
    private string currentDirectory;

    public SourceSelectionWindow()
        : this(null)
    {
    }

    public SourceSelectionWindow(string? initialSelection)
    {
        InitializeComponent();

        currentDirectory = ResolveInitialDirectory(initialSelection);
        EntriesListBox.ItemsSource = entries;

        LoadDirectory(currentDirectory);
        RefreshSelectionState();
    }

    private void LoadDirectory(string directoryPath)
    {
        try
        {
            var directory = new DirectoryInfo(directoryPath);
            if (!directory.Exists)
            {
                return;
            }

            currentDirectory = directory.FullName;
            CurrentPathTextBox.Text = currentDirectory;
            entries.Clear();

            foreach (var childDirectory in directory.EnumerateDirectories().OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
            {
                entries.Add(SourceSelectionEntry.ForDirectory(childDirectory));
            }

            foreach (var file in directory.EnumerateFiles().OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
            {
                entries.Add(SourceSelectionEntry.ForFile(file));
            }

            EntriesListBox.SelectedItems?.Clear();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SelectionHintTextBlock.Text = $"Unable to open folder: {directoryPath}";
        }

        RefreshSelectionState();
    }

    private void RefreshSelectionState()
    {
        UpButton.IsEnabled = Directory.GetParent(currentDirectory) is not null;

        var selectedEntries = GetSelectedEntries();
        var hasSingleDirectory = selectedEntries.Count == 1 && selectedEntries[0].IsDirectory;
        var hasOnlyFiles = selectedEntries.Count > 0 && selectedEntries.All(item => !item.IsDirectory);

        OpenButton.IsEnabled = hasSingleDirectory;
        OkButton.IsEnabled = hasSingleDirectory || hasOnlyFiles;

        SelectionHintTextBlock.Text = selectedEntries.Count switch
        {
            0 => "Select one folder, or one or more files, then click OK.",
            _ when hasSingleDirectory => "OK will save the selected folder.",
            _ when hasOnlyFiles => $"OK will save {selectedEntries.Count} file(s).",
            _ => "Choose either one folder or only files in the same validation."
        };
    }

    private List<SourceSelectionEntry> GetSelectedEntries()
    {
        if (EntriesListBox.SelectedItems is null)
        {
            return [];
        }

        return EntriesListBox.SelectedItems
            .OfType<SourceSelectionEntry>()
            .ToList();
    }

    private void EntriesListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        RefreshSelectionState();
    }

    private void EntriesListBox_DoubleTapped(object? sender, TappedEventArgs e)
    {
        var selectedEntries = GetSelectedEntries();
        if (selectedEntries.Count != 1)
        {
            return;
        }

        if (selectedEntries[0].IsDirectory)
        {
            LoadDirectory(selectedEntries[0].FullPath);
            return;
        }

        ConfirmSelection();
    }

    private void UpButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var parent = Directory.GetParent(currentDirectory);
        if (parent is not null)
        {
            LoadDirectory(parent.FullName);
        }
    }

    private void UseCurrentFolderButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(currentDirectory);
    }

    private void OpenButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var selectedEntries = GetSelectedEntries();
        if (selectedEntries.Count == 1 && selectedEntries[0].IsDirectory)
        {
            LoadDirectory(selectedEntries[0].FullPath);
        }
    }

    private void OkButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ConfirmSelection();
    }

    private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(null);
    }

    private void ConfirmSelection()
    {
        var selectedEntries = GetSelectedEntries();
        if (selectedEntries.Count == 1 && selectedEntries[0].IsDirectory)
        {
            Close(selectedEntries[0].FullPath);
            return;
        }

        if (selectedEntries.Count > 0 && selectedEntries.All(item => !item.IsDirectory))
        {
            var joinedPaths = string.Join(";", selectedEntries.Select(item => item.FullPath));
            Close(joinedPaths);
        }
    }

    private static string ResolveInitialDirectory(string? initialSelection)
    {
        var firstEntry = initialSelection?
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(firstEntry))
        {
            if (Directory.Exists(firstEntry))
            {
                return Path.GetFullPath(firstEntry);
            }

            if (File.Exists(firstEntry))
            {
                var parent = Path.GetDirectoryName(firstEntry);
                if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent))
                {
                    return Path.GetFullPath(parent);
                }
            }
        }

        var myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrWhiteSpace(myDocuments) && Directory.Exists(myDocuments))
        {
            return myDocuments;
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile) && Directory.Exists(userProfile))
        {
            return userProfile;
        }

        return AppContext.BaseDirectory;
    }

}

public sealed record SourceSelectionEntry(string DisplayName, string FullPath, bool IsDirectory, string ParentLabel)
{
    public string EntryTypeLabel => IsDirectory ? "[Folder]" : "[File]";

    public static SourceSelectionEntry ForDirectory(DirectoryInfo directory)
    {
        return new SourceSelectionEntry(directory.Name, directory.FullName, true, directory.Parent?.Name ?? directory.Root.FullName);
    }

    public static SourceSelectionEntry ForFile(FileInfo file)
    {
        return new SourceSelectionEntry(file.Name, file.FullName, false, file.Directory?.Name ?? string.Empty);
    }
}
