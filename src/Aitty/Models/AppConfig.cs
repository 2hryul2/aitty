namespace Aitty.Models;

public class AppConfig
{
    public string Theme { get; set; } = "dark";
    public int FontSize { get; set; } = 12;
    public string FontFamily { get; set; } = "Consolas, \"Courier New\"";
    public List<SshConnection> SshConnections { get; set; } = new();
    public string? LastConnection { get; set; }
}
