using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Aitty.Models;
using Aitty.Services;
using Aitty.Terminal;

namespace Aitty.Controls;

public partial class SSHTerminalControl : UserControl
{
    private SshService?        _ssh;
    private ConfigService?     _config;
    private KeyManagerService? _keys;

    private readonly TerminalBuffer  _termBuffer = new(120, 40);
    private readonly VT100Parser     _parser;
    private readonly DispatcherTimer _pollTimer  = new() { Interval = TimeSpan.FromMilliseconds(50) };

    public SSHTerminalControl()
    {
        InitializeComponent();

        _parser = new VT100Parser(_termBuffer);

        Term.Buffer  = _termBuffer;
        Term.KeyInput += OnTermKeyInput;

        _pollTimer.Tick += PollTimer_Tick;

        Loaded += (_, _) => ShowBanner();
    }

    public void Initialize(SshService ssh, ConfigService config, KeyManagerService keys)
    {
        _ssh    = ssh;
        _config = config;
        _keys   = keys;
    }

    // ─── Banner ──────────────────────────────────────────────────────────────

    private void ShowBanner()
    {
        _termBuffer.Clear();
        FeedLine("\x1b[36m   _____ _    _ _____ _   _ _    _          _   _   _____   _____ \x1b[0m");
        FeedLine("\x1b[36m  / ____| |  | |_   _| \\ | | |  | |   /\\  | \\ | | |  __ \\ / ____|\x1b[0m");
        FeedLine("\x1b[36m | (___ | |__| | | | |  \\| | |__| |  /  \\ |  \\| | | |  | | (___  \x1b[0m");
        FeedLine("\x1b[36m  \\___ \\|  __  | | | | . ` |  __  | / /\\ \\| . ` | | |  | |\\___ \\ \x1b[0m");
        FeedLine("\x1b[36m  ____) | |  | |_| |_| |\\  | |  | |/ ____ \\ |\\  | | |__| |____) |\x1b[0m");
        FeedLine("\x1b[36m |_____/|_|  |_|_____|_| \\_|_|  |_/_/    \\_\\_| \\_| |_____/|_____/ \x1b[0m");
        FeedLine("");
        FeedLine("\x1b[90m  SSH AI Terminal  |  Fill in the form above and click Connect.\x1b[0m");
        FeedLine("");
    }

    private void FeedLine(string text) => _parser.Feed(text + "\r\n");

    // ─── Connect / Disconnect ────────────────────────────────────────────────

    private async void ConnectBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_ssh == null) return;

        var connection = new SshConnection
        {
            Host     = HostBox.Text.Trim(),
            Username = UserBox.Text.Trim(),
            Password = PassBox.Password,
        };
        if (int.TryParse(PortBox.Text.Trim(), out int port)) connection.Port = port;

        if (string.IsNullOrEmpty(connection.Host) || string.IsNullOrEmpty(connection.Username)) return;

        SetStatus("Connecting…", "#ffcc00");
        ConnectBtn.IsEnabled = false;

        var ok = await _ssh.ConnectAsync(connection);

        if (ok)
        {
            SetStatus($"● {connection.Username}@{connection.Host}", "#00ff00");
            DisconnectBtn.IsEnabled = true;

            _termBuffer.Clear();
            FeedLine($"\x1b[32mConnected to {connection.Username}@{connection.Host}:{connection.Port}\x1b[0m");
            FeedLine("");

            Term.Focus();
            _pollTimer.Start();
        }
        else
        {
            var err = _ssh.State.Error ?? "Unknown error";
            SetStatus("Error", "#ff4444");
            FeedLine($"\x1b[31mConnection failed: {err}\x1b[0m");
            ConnectBtn.IsEnabled = true;
        }
    }

    private void DisconnectBtn_Click(object sender, RoutedEventArgs e) => Disconnect();

    private void Disconnect()
    {
        _pollTimer.Stop();
        _ssh?.Disconnect();

        SetStatus("Disconnected", "#555555");
        ConnectBtn.IsEnabled    = true;
        DisconnectBtn.IsEnabled = false;

        FeedLine("");
        FeedLine("\x1b[90m─── Session ended ───────────────────────────────\x1b[0m");
        FeedLine("");
        ShowBanner();
    }

    // ─── Shell I/O ───────────────────────────────────────────────────────────

    private void PollTimer_Tick(object? sender, EventArgs e)
    {
        if (_ssh == null || !_ssh.IsConnected)
        {
            Disconnect();
            return;
        }
        try
        {
            var data = _ssh.ReadFromShell();
            if (!string.IsNullOrEmpty(data))
                _parser.Feed(data);
        }
        catch
        {
            Disconnect();
        }
    }

    /// <summary>Called by TerminalControl when user presses a key.</summary>
    private void OnTermKeyInput(string vt)
    {
        if (_ssh is { IsConnected: true })
            _ssh.WriteToShell(vt);
    }

    // ─── Status ──────────────────────────────────────────────────────────────

    private void SetStatus(string text, string hexColor)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor));
        StatusText.Text       = text;
        StatusText.Foreground = brush;
        StatusDot.Fill        = brush;
    }
}
