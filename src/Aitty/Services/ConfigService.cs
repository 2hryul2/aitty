using System.IO;
using System.Text.Json;
using Aitty.Models;

namespace Aitty.Services;

public class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _configDir;
    private readonly string _configPath;

    public ConfigService()
    {
        _configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ssh-ai-terminal");
        _configPath = Path.Combine(_configDir, "config.json");
    }

    public async Task<AppConfig> LoadAsync()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                var defaultConfig = CreateDefault();
                await SaveAsync(defaultConfig);
                return defaultConfig;
            }

            var json = await File.ReadAllTextAsync(_configPath);
            return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? CreateDefault();
        }
        catch
        {
            return CreateDefault();
        }
    }

    public async Task SaveAsync(AppConfig config)
    {
        Directory.CreateDirectory(_configDir);
        var json = JsonSerializer.Serialize(config, JsonOptions);
        await File.WriteAllTextAsync(_configPath, json);
    }

    public async Task AddConnectionAsync(SshConnection connection)
    {
        var config = await LoadAsync();
        config.SshConnections.Add(connection);
        await SaveAsync(config);
    }

    public async Task RemoveConnectionAsync(string host)
    {
        var config = await LoadAsync();
        config.SshConnections.RemoveAll(c => c.Host == host);
        await SaveAsync(config);
    }

    public async Task UpdateConnectionAsync(string host, SshConnection updated)
    {
        var config = await LoadAsync();
        var index = config.SshConnections.FindIndex(c => c.Host == host);
        if (index >= 0)
        {
            config.SshConnections[index] = updated;
            await SaveAsync(config);
        }
    }

    private static AppConfig CreateDefault() => new()
    {
        Theme = "dark",
        FontSize = 12,
        FontFamily = "Consolas, \"Courier New\"",
        SshConnections = new List<SshConnection>()
    };
}
