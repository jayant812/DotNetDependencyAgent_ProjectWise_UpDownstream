using System.IO;
using System.Text.Json;

namespace DotNetDependencyAgent;

public sealed class AppSettings
{
    public GeminiSettings Gemini { get; set; } = new();

    public static AppSettings Load()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(configPath))
            throw new FileNotFoundException("Configuration file 'appsettings.json' was not found.", configPath);

        var json = File.ReadAllText(configPath);
        var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new AppSettings();

        // Environment variable can override the file value for safer deployments.
        var envKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (!string.IsNullOrWhiteSpace(envKey))
            settings.Gemini.ApiKey = envKey;

        var envModel = Environment.GetEnvironmentVariable("GEMINI_MODEL");
        if (!string.IsNullOrWhiteSpace(envModel))
            settings.Gemini.Model = envModel;

        return settings;
    }
}

public sealed class GeminiSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-3.5-flash";
}
