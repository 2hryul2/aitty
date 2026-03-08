namespace Aitty.Models;

public class SshConnection
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string? PrivateKey { get; set; }
    public string? Password { get; set; }
    public string? Passphrase { get; set; }
}

public class SshConnectionState
{
    public bool IsConnected { get; set; }
    public bool IsConnecting { get; set; }
    public string? Error { get; set; }
    public SshConnection? Connection { get; set; }
    public DateTime? ConnectionTime { get; set; }
}
