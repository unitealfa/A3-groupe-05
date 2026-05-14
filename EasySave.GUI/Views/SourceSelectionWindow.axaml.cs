using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Input;
using EasySave.Core.Configuration;

namespace EasySave.GUI.Views;

public partial class SourceSelectionWindow : Window
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly ObservableCollection<SourceSelectionEntry> entries = [];
    private readonly Dictionary<string, string> texts;
    private string currentDirectory;

    public SourceSelectionWindow()
        : this(null)
    {
    }

    public SourceSelectionWindow(string? initialSelection)
    {
        InitializeComponent();
        texts = LoadTranslations();
        ApplyTranslations();

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
                entries.Add(SourceSelectionEntry.ForDirectory(childDirectory, Translate("SourceSelectionFolderType")));
            }

            foreach (var file in directory.EnumerateFiles().OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
            {
                entries.Add(SourceSelectionEntry.ForFile(file, Translate("SourceSelectionFileType")));
            }

            EntriesListBox.SelectedItems?.Clear();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            SelectionHintTextBlock.Text = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                Translate("SourceSelectionOpenFolderError"),
                directoryPath);
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
            0 => Translate("SourceSelectionHintNone"),
            _ when hasSingleDirectory => Translate("SourceSelectionHintFolder"),
            _ when hasOnlyFiles => string.Format(CultureInfo.InvariantCulture, Translate("SourceSelectionHintFiles"), selectedEntries.Count),
            _ => Translate("SourceSelectionHintMixed")
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

    private string Translate(string key)
    {
        return texts.TryGetValue(key, out var value) ? value : key;
    }

    private void ApplyTranslations()
    {
        Title = Translate("SourceSelectionWindowTitle");
        WindowTitleTextBlock.Text = Translate("SourceSelectionWindowTitle");
        WindowSubtitleTextBlock.Text = Translate("SourceSelectionWindowSubtitle");
        UpButton.Content = Translate("SourceSelectionUpButton");
        UseCurrentFolderButton.Content = Translate("SourceSelectionUseCurrentFolderButton");
        TypeHeaderTextBlock.Text = Translate("SourceSelectionTypeHeader");
        NameHeaderTextBlock.Text = Translate("SourceSelectionNameHeader");
        LocationHeaderTextBlock.Text = Translate("SourceSelectionLocationHeader");
        OpenButton.Content = Translate("SourceSelectionOpenFolderButton");
        OkButton.Content = Translate("SourceSelectionOkButton");
        CancelButton.Content = Translate("SourceSelectionCancelButton");
    }

    private static Dictionary<string, string> LoadTranslations()
    {
        try
        {
            var settingsRepository = new AppSettingsRepository(AppPaths.SettingsFilePath);
            var language = settingsRepository.LoadAsync().GetAwaiter().GetResult().Language;
            var resourcePath = Path.Combine(AppContext.BaseDirectory, "Resources", $"{language}.json");
            if (!File.Exists(resourcePath))
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var content = File.ReadAllText(resourcePath);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(content, JsonOptions)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

}

public sealed record SourceSelectionEntry(string DisplayName, string FullPath, bool IsDirectory, string ParentLabel, string EntryTypeLabel)
{
    public static SourceSelectionEntry ForDirectory(DirectoryInfo directory, string folderLabel)
    {
        return new SourceSelectionEntry(directory.Name, directory.FullName, true, directory.Parent?.Name ?? directory.Root.FullName, folderLabel);
    }

    public static SourceSelectionEntry ForFile(FileInfo file, string fileLabel)
    {
        return new SourceSelectionEntry(file.Name, file.FullName, false, file.Directory?.Name ?? string.Empty, fileLabel);
    }
}
