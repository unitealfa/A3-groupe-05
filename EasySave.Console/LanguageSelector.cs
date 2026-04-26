using System.Text.Json;

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

    public async Task InitializeAsync()
    {
        CurrentLanguage = await LoadSavedLanguageAsync();
        await LoadTranslationsAsync(CurrentLanguage);
    }

    public async Task SelectLanguageAsync()
    {
        System.Console.WriteLine($"1 - {Text("LanguageFrench")}");
        System.Console.WriteLine($"2 - {Text("LanguageEnglish")}");
        System.Console.Write("> ");
        var choice = System.Console.ReadLine();

        CurrentLanguage = choice == "1" ? "fr" : "en";
        await SaveLanguageAsync(CurrentLanguage);
        await LoadTranslationsAsync(CurrentLanguage);
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

    private async Task<string> LoadSavedLanguageAsync()
    {
        if (!File.Exists(settingsFilePath))
        {
            return "en";
        }

        await using var stream = File.OpenRead(settingsFilePath);
        var settings = await JsonSerializer.DeserializeAsync<LanguageSettings>(stream, JsonOptions);
        return settings?.Language is "fr" or "en" ? settings.Language : "en";
    }

    private async Task SaveLanguageAsync(string language)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settingsFilePath)!);
        await using var stream = File.Create(settingsFilePath);
        await JsonSerializer.SerializeAsync(stream, new LanguageSettings { Language = language }, JsonOptions);
    }

    private sealed class LanguageSettings
    {
        public string Language { get; set; } = "en";
    }
}
