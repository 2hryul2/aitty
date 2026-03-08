using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Aitty.Services;

namespace Aitty.Controls;

public partial class AITerminalControl : UserControl
{
    private ClaudeApiService? _claude;
    private ConfigService?    _config;

    private bool                    _isStreaming;
    private string                  _currentModel = "claude-sonnet-4-5-20251022";
    private CancellationTokenSource _cts          = new();

    public AITerminalControl()
    {
        InitializeComponent();
        ShowWelcome();
    }

    public void Initialize(ClaudeApiService claude, ConfigService config)
    {
        _claude = claude;
        _config = config;
        RefreshStatus();
    }

    // ─── Welcome / Help ──────────────────────────────────────────────────────

    private void ShowWelcome()
    {
        AppendWarningBanner();
        AppendLine("");
        AppendLine("Type help for commands.  Any other input → Claude.", "#555555");
        AppendLine("");
    }

    private void AppendWarningBanner()
    {
        AppendLine("╔════════════════════════════════════════════════════╗", "#ccaa00");
        AppendLine("║  ⚠ AI는 부정확한 정보를 제공할 수 있습니다.       ║", "#ccaa00");
        AppendLine("║  실행 전 반드시 내용을 검토하세요                  ║", "#998800");
        AppendLine("╚════════════════════════════════════════════════════╝", "#ccaa00");
    }

    // ─── Input Handling ──────────────────────────────────────────────────────

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !_isStreaming)
        {
            e.Handled = true;
            SubmitInput();
        }
        else if (e.Key == Key.Escape && _isStreaming)
        {
            CancelStream();
        }
    }

    private void SendBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_isStreaming) CancelStream();
        else SubmitInput();
    }

    private void SubmitInput()
    {
        var text = InputBox.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;
        InputBox.Clear();

        if (!HandleBuiltinCommand(text))
            _ = SendMessageAsync(text);
    }

    // ─── Builtin Commands ────────────────────────────────────────────────────

    private bool HandleBuiltinCommand(string input)
    {
        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var cmd   = parts[0].ToLowerInvariant();

        switch (cmd)
        {
            case "help":
                ShowHelp();
                return true;

            case "clear":
                ChatOutput.Document.Blocks.Clear();
                return true;

            case "reset":
                _claude?.ClearHistory();
                AppendLine("Conversation history cleared.", "#00aa00");
                return true;

            case "status":
                ShowStatus();
                return true;

            case "model" when parts.Length >= 2:
                if (parts[1] == "list") { ListModels(); return true; }
                if (parts[1] == "use" && parts.Length >= 3) { SetModel(parts[2]); return true; }
                return false;

            case "config" when parts.Length >= 4 && parts[1] == "set":
                HandleConfigSet(parts[2], string.Join(' ', parts[3..]));
                return true;
        }
        return false;
    }

    private void ShowHelp()
    {
        AppendLine("");
        AppendLine("─── Commands ────────────────────────────────────────", "#444");
        AppendLine("  config set api-key <KEY>    Set Claude API key",     "#ffcc00");
        AppendLine("  config set model <MODEL>    Set AI model",           "#ffcc00");
        AppendLine("  config set system <PROMPT>  Set system prompt",      "#ffcc00");
        AppendLine("  model list                  List available models",  "#ffcc00");
        AppendLine("  model use <MODEL>           Switch model",           "#ffcc00");
        AppendLine("  status                      Show API status",        "#ffcc00");
        AppendLine("  reset                       Clear conversation",     "#ffcc00");
        AppendLine("  clear                       Clear screen",           "#ffcc00");
        AppendLine("  help                        This help",              "#ffcc00");
        AppendLine("─────────────────────────────────────────────────────", "#444");
        AppendLine("");
    }

    private void ShowStatus()
    {
        var configured = _claude?.IsConfigured ?? false;
        AppendLine("");
        AppendLine($"  API Key : {(configured ? "✓ Configured" : "✗ Not set")}", configured ? "#00aa00" : "#ff4444");
        AppendLine($"  Model   : {_currentModel}", "#7c9ef0");
        AppendLine("");
    }

    private void ListModels()
    {
        var models = new[] {
            ("claude-opus-4-5-20251022",   "Claude Opus 4.5"),
            ("claude-sonnet-4-5-20251022", "Claude Sonnet 4.5"),
            ("claude-haiku-4-5-20251022",  "Claude Haiku 4.5"),
        };
        AppendLine("");
        AppendLine("Available Models:", "#7c9ef0");
        foreach (var (id, name) in models)
        {
            var marker = id == _currentModel ? " ← current" : "";
            AppendLine($"  {id}  ({name}){marker}", id == _currentModel ? "#00aa00" : "#aaaaaa");
        }
        AppendLine("");
    }

    private void SetModel(string model)
    {
        _currentModel = model;
        _claude?.SetModel(model);
        ModelLabel.Text = model.Split('-')[1];   // e.g. "sonnet"
        AppendLine($"Model set to: {model}", "#00aa00");
    }

    private void HandleConfigSet(string key, string value)
    {
        switch (key.ToLowerInvariant())
        {
            case "api-key":
                _claude?.SetApiKey(value);
                AppendLine("API key configured.", "#00aa00");
                RefreshStatus();
                break;
            case "model":
                SetModel(value);
                break;
            case "system":
                _claude?.SetSystemPrompt(value);
                AppendLine("System prompt updated.", "#00aa00");
                break;
            default:
                AppendLine($"Unknown config key: {key}", "#ff4444");
                break;
        }
    }

    // ─── Claude Streaming ────────────────────────────────────────────────────

    private async Task SendMessageAsync(string message)
    {
        if (_claude == null)
        {
            AppendLine("Claude API not initialized.", "#ff4444");
            return;
        }

        if (!_claude.IsConfigured)
        {
            AppendLine("API key not set.  Run: config set api-key <YOUR_KEY>", "#ff4444");
            return;
        }

        _isStreaming = true;
        SendBtn.Content = "Cancel";
        StreamingIndicator.Visibility = Visibility.Visible;

        // User turn
        AppendLine("");
        AppendLine($"you: {message}", "#7c9ef0");
        AppendLine("");

        // Assistant prefix
        var assistantPara = new Paragraph { Margin = new Thickness(0) };
        assistantPara.Inlines.Add(new Run("ai: ") { Foreground = new SolidColorBrush(Color.FromRgb(0, 0xCC, 0x88)) });
        var contentRun = new Run { Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)) };
        assistantPara.Inlines.Add(contentRun);
        ChatOutput.Document.Blocks.Add(assistantPara);
        ChatScroll.ScrollToBottom();

        _cts = new CancellationTokenSource();

        try
        {
            await _claude.SendStreamingAsync(message, chunk =>
            {
                Dispatcher.Invoke(() =>
                {
                    contentRun.Text += chunk;
                    ChatScroll.ScrollToBottom();
                });
            }, _cts.Token);

            AppendLine("");
        }
        catch (OperationCanceledException)
        {
            contentRun.Text += " [cancelled]";
            AppendLine("");
        }
        catch (Exception ex)
        {
            AppendLine($"Error: {ex.Message}", "#ff4444");
        }
        finally
        {
            _isStreaming = false;
            SendBtn.Content = "Send";
            StreamingIndicator.Visibility = Visibility.Collapsed;
        }
    }

    private void CancelStream()
    {
        _cts.Cancel();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private void AppendLine(string text, string hexColor = "#e0e0e0")
    {
        var para = new Paragraph { Margin = new Thickness(0) };
        var run  = new Run(text);
        if (!string.IsNullOrEmpty(hexColor))
        {
            try { run.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor)); }
            catch { /* ignore bad color strings */ }
        }
        para.Inlines.Add(run);
        ChatOutput.Document.Blocks.Add(para);
        ChatScroll.ScrollToBottom();
    }

    private void RefreshStatus()
    {
        var ok = _claude?.IsConfigured ?? false;
        ApiStatusDot.Fill     = new SolidColorBrush(ok ? Color.FromRgb(0, 0xFF, 0) : Color.FromRgb(0x55, 0x55, 0x55));
        ApiStatusText.Text    = ok ? "Connected" : "No API Key";
        ApiStatusText.Foreground = ApiStatusDot.Fill;
    }
}
