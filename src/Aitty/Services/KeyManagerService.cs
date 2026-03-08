using System.IO;

namespace Aitty.Services;

public class KeyManagerService
{
    private readonly string _sshDir;

    public KeyManagerService()
    {
        _sshDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
    }

    public Task<List<string>> FindKeysAsync()
    {
        var keys = new List<string>();
        var commonNames = new[] { "id_rsa", "id_ed25519", "id_ecdsa", "id_dsa" };

        foreach (var name in commonNames)
        {
            var keyPath = Path.Combine(_sshDir, name);
            if (File.Exists(keyPath))
                keys.Add(keyPath);
        }

        return Task.FromResult(keys);
    }

    public Task<bool> KeyExistsAsync(string keyPath)
    {
        return Task.FromResult(File.Exists(keyPath));
    }

    public async Task<Dictionary<string, Dictionary<string, string>>> ReadSshConfigAsync()
    {
        var configPath = Path.Combine(_sshDir, "config");
        if (!File.Exists(configPath))
            return new Dictionary<string, Dictionary<string, string>>();

        var content = await File.ReadAllTextAsync(configPath);
        return ParseSshConfig(content);
    }

    public async Task<bool> IsValidKeyFileAsync(string keyPath)
    {
        try
        {
            var content = await File.ReadAllTextAsync(keyPath);
            return content.Contains("BEGIN OPENSSH PRIVATE KEY")
                || content.Contains("BEGIN RSA PRIVATE KEY")
                || content.Contains("BEGIN EC PRIVATE KEY")
                || content.Contains("BEGIN PRIVATE KEY");
        }
        catch
        {
            return false;
        }
    }

    public string GetKeyDirectory() => _sshDir;

    private static Dictionary<string, Dictionary<string, string>> ParseSshConfig(string content)
    {
        var config = new Dictionary<string, Dictionary<string, string>>();
        var currentHost = string.Empty;

        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                continue;

            if (trimmed.StartsWith("Host ", StringComparison.OrdinalIgnoreCase))
            {
                currentHost = trimmed[5..].Trim();
                config[currentHost] = new Dictionary<string, string>();
            }
            else if (!string.IsNullOrEmpty(currentHost))
            {
                var parts = trimmed.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                    config[currentHost][parts[0].ToLowerInvariant()] = parts[1];
            }
        }

        return config;
    }
}
