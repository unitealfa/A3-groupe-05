using System.Text.Json;
using EasyLog;
using EasySave.Core.Configuration;
using EasySave.Core.Models;

namespace EasySave.Console;

public sealed class LanguageSelector
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly AppSettingsRepository settingsRepository;
    private Dictionary<string, string> translations = new(StringComparer.OrdinalIgnoreCase);

    public LanguageSelector(string settingsFilePath)
    {
        settingsRepository = new AppSettingsRepository(settingsFilePath);
    }

    public string CurrentLanguage { get; private set; } = "en";

    public LogFormat CurrentLogFormat { get; private set; } = LogFormat.Json;

    public async Task InitializeAsync()
    {
        var settings = await settingsRepository.LoadAsync();
        CurrentLanguage = settings.Language;
        CurrentLogFormat = settings.LogFormat;
        await LoadTranslationsAsync(CurrentLanguage);
    }

    public async Task SelectLanguageAsync()
    {
        System.Console.WriteLine($"1 - {Text("LanguageFrench")}");
        System.Console.WriteLine($"2 - {Text("LanguageEnglish")}");
        System.Console.Write("> ");
        var choice = System.Console.ReadLine();

        CurrentLanguage = choice == "1" ? "fr" : "en";
        await SaveSettingsAsync();
        await LoadTranslationsAsync(CurrentLanguage);
    }

    public async Task SelectLogFormatAsync()
    {
        System.Console.WriteLine($"1 - {Text("LogFormatJson")}");
        System.Console.WriteLine($"2 - {Text("LogFormatXml")}");
        System.Console.Write($"{Text("LogFormatPrompt")} ");
        var choice = System.Console.ReadLine();

        CurrentLogFormat = choice == "2" ? LogFormat.Xml : LogFormat.Json;
        await SaveSettingsAsync();
    }

    public string Text(string key)
    {
        return translations.TryGetValue(key, out var value) ? value : key;
    }

    private async Task LoadTranslationsAsync(string language)
    {
        var resourcePath = Path.Combine(AppContext.BaseDirectory, "Resources", $"{language}.json");
        if (!File.Exists(resourcePath))
        {
            translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        await using var stream = File.OpenRead(resourcePath);
        translations = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream, JsonOptions)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<AppSettings> LoadSettingsAsync()
    {
        return await settingsRepository.LoadAsync();
    }

    private async Task SaveSettingsAsync()
    {
        var settings = await settingsRepository.LoadAsync();
        settings.Language = CurrentLanguage;
        settings.LogFormatName = CurrentLogFormat.ToString().ToLowerInvariant();
        await settingsRepository.SaveAsync(settings);
    }

    private static LogFormat ParseLogFormat(string? value)
    {
        return string.Equals(value, "xml", StringComparison.OrdinalIgnoreCase)
            ? LogFormat.Xml
            : LogFormat.Json;
    }
}
