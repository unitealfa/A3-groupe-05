using System.Text.Json;
using EasyLog;

namespace EasySave.Console;

public sealed class LanguageSelector
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string settingsFilePath;
    private Dictionary<string, string> translations = new(StringComparer.OrdinalIgnoreCase);

    public LanguageSelector(string settingsFilePath)
    {
        this.settingsFilePath = settingsFilePath;
    }

    public string CurrentLanguage { get; private set; } = "en";

    public LogFormat CurrentLogFormat { get; private set; } = LogFormat.Json;

    public async Task InitializeAsync()
    {
        var settings = await LoadSettingsAsync();
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
        if (!File.Exists(settingsFilePath))
        {
            return new AppSettings();
        }

        await using var stream = File.OpenRead(settingsFilePath);
        var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions);
        return new AppSettings
        {
            Language = settings?.Language is "fr" or "en" ? settings.Language : "en",
            LogFormatName = ParseLogFormatName(settings?.LogFormatName)
        };
    }

    private async Task SaveSettingsAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settingsFilePath)!);
        await using var stream = File.Create(settingsFilePath);
        await JsonSerializer.SerializeAsync(
            stream,
            new AppSettings
            {
                Language = CurrentLanguage,
                LogFormatName = CurrentLogFormat.ToString().ToLowerInvariant()
            },
            JsonOptions);
    }

    private static string ParseLogFormatName(string? value)
    {
        return string.Equals(value, "xml", StringComparison.OrdinalIgnoreCase) ? "xml" : "json";
    }

    private static LogFormat ParseLogFormat(string? value)
    {
        return string.Equals(value, "xml", StringComparison.OrdinalIgnoreCase)
            ? LogFormat.Xml
            : LogFormat.Json;
    }

    private sealed class AppSettings
    {
        public string Language { get; set; } = "en";

        public string LogFormatName { get; set; } = "json";

        public LogFormat LogFormat => ParseLogFormat(LogFormatName);
    }
}
