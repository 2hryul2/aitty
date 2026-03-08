using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Web.WebView2.Core;
using Aitty.Models;
using Aitty.Services;

namespace Aitty.Ipc;

public class IpcHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly WebView2 _webView;
    private readonly SshService _sshService;
    private readonly ConfigService _configService;
    private readonly KeyManagerService _keyManagerService;
    private readonly ClaudeApiService _claudeApiService;
    private CancellationTokenSource? _streamingCts;

    public IpcHandler(
        WebView2 webView,
        SshService sshService,
        ConfigService configService,
        KeyManagerService keyManagerService,
        ClaudeApiService claudeApiService)
    {
        _webView = webView;
        _sshService = sshService;
        _configService = configService;
        _keyManagerService = keyManagerService;
        _claudeApiService = claudeApiService;
    }

    public void Register()
    {
        _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        IpcResponse response;

        try
        {
            var json = e.WebMessageAsJson;
            var msg = JsonSerializer.Deserialize<IpcMessage>(json, JsonOptions);
            if (msg is null)
            {
                return;
            }

            var result = await HandleMessage(msg);
            response = new IpcResponse
            {
                Id = msg.Id,
                Type = $"{msg.Type}:result",
                Payload = result
            };
        }
        catch (Exception ex)
        {
            // Try to extract id from raw json for error response
            var id = TryExtractId(e.WebMessageAsJson);
            response = new IpcResponse
            {
                Id = id,
                Type = "error",
                Error = ex.Message
            };
        }

        var responseJson = JsonSerializer.Serialize(response, JsonOptions);
        _webView.CoreWebView2.PostWebMessageAsJson(responseJson);
    }

    private async Task<object?> HandleMessage(IpcMessage msg)
    {
        return msg.Type switch
        {
            // SSH
            "ssh:connect" => await HandleSshConnect(msg.Payload),
            "ssh:disconnect" => HandleSshDisconnect(),
            "ssh:exec" => await HandleSshExec(msg.Payload),
            "ssh:test" => await HandleSshTest(),
            "ssh:state" => HandleSshState(),
            "ssh:shell:write" => HandleSshShellWrite(msg.Payload),
            "ssh:shell:read" => HandleSshShellRead(),

            // Config
            "config:load" => await HandleConfigLoad(),
            "config:save" => await HandleConfigSave(msg.Payload),
            "config:connections:add" => await HandleConfigAddConnection(msg.Payload),
            "config:connections:remove" => await HandleConfigRemoveConnection(msg.Payload),

            // Keys
            "keys:list" => await HandleKeysList(),
            "keys:validate" => await HandleKeysValidate(msg.Payload),
            "keys:ssh-config" => await HandleKeysSshConfig(),

            // AI
            "ai:send" => await HandleAiSend(msg),
            "ai:stream" => await HandleAiStream(msg),
            "ai:stream:cancel" => HandleAiStreamCancel(),
            "ai:configure" => HandleAiConfigure(msg.Payload),
            "ai:set-key" => HandleAiSetKey(msg.Payload),
            "ai:set-model" => HandleAiSetModel(msg.Payload),
            "ai:set-system" => HandleAiSetSystem(msg.Payload),
            "ai:state" => HandleAiState(),
            "ai:history" => HandleAiHistory(),
            "ai:clear" => HandleAiClear(),

            // App
            "app:version" => GetAppVersion(),

            _ => throw new NotSupportedException($"Unknown IPC type: {msg.Type}")
        };
    }

    // --- SSH handlers ---

    private async Task<object> HandleSshConnect(object? payload)
    {
        var conn = DeserializePayload<SshConnection>(payload);
        var success = await _sshService.ConnectAsync(conn);
        return new { success };
    }

    private object HandleSshDisconnect()
    {
        _sshService.Disconnect();
        return new { success = true };
    }

    private async Task<object> HandleSshExec(object? payload)
    {
        var data = DeserializePayload<CommandPayload>(payload);
        var output = await _sshService.ExecuteAsync(data.Command);
        return new { output };
    }

    private async Task<object> HandleSshTest()
    {
        var ok = await _sshService.TestAsync();
        return new { success = ok };
    }

    private object HandleSshState()
    {
        var state = _sshService.State;
        return new
        {
            isConnected = _sshService.IsConnected,
            isConnecting = state.IsConnecting,
            error = state.Error,
            host = state.Connection?.Host,
            connectionTime = state.ConnectionTime?.ToString("o")
        };
    }

    private object HandleSshShellWrite(object? payload)
    {
        var data = DeserializePayload<ShellWritePayload>(payload);
        _sshService.WriteToShell(data.Data);
        return new { success = true };
    }

    private object HandleSshShellRead()
    {
        var data = _sshService.ReadFromShell();
        return new { data };
    }

    // --- Config handlers ---

    private async Task<object> HandleConfigLoad()
    {
        return await _configService.LoadAsync();
    }

    private async Task<object> HandleConfigSave(object? payload)
    {
        var config = DeserializePayload<AppConfig>(payload);
        await _configService.SaveAsync(config);
        return new { success = true };
    }

    private async Task<object> HandleConfigAddConnection(object? payload)
    {
        var conn = DeserializePayload<SshConnection>(payload);
        await _configService.AddConnectionAsync(conn);
        return new { success = true };
    }

    private async Task<object> HandleConfigRemoveConnection(object? payload)
    {
        var data = DeserializePayload<HostPayload>(payload);
        await _configService.RemoveConnectionAsync(data.Host);
        return new { success = true };
    }

    // --- Key handlers ---

    private async Task<object> HandleKeysList()
    {
        var keys = await _keyManagerService.FindKeysAsync();
        return new { keys, directory = _keyManagerService.GetKeyDirectory() };
    }

    private async Task<object> HandleKeysValidate(object? payload)
    {
        var data = DeserializePayload<KeyPathPayload>(payload);
        var valid = await _keyManagerService.IsValidKeyFileAsync(data.Path);
        return new { valid };
    }

    private async Task<object> HandleKeysSshConfig()
    {
        var config = await _keyManagerService.ReadSshConfigAsync();
        return config;
    }

    // --- AI handlers ---

    private async Task<object> HandleAiSend(IpcMessage msg)
    {
        var data = DeserializePayload<AiChatRequest>(msg.Payload);
        var response = await _claudeApiService.SendMessageAsync(data.Message);
        return new
        {
            content = response.Content,
            model = response.Model,
            inputTokens = response.InputTokens,
            outputTokens = response.OutputTokens
        };
    }

    private async Task<object> HandleAiStream(IpcMessage msg)
    {
        var data = DeserializePayload<AiChatRequest>(msg.Payload);
        _streamingCts = new CancellationTokenSource();

        var fullContent = await _claudeApiService.SendStreamingAsync(
            data.Message,
            chunk =>
            {
                var chunkResponse = new IpcResponse
                {
                    Id = msg.Id,
                    Type = "ai:stream:chunk",
                    Payload = new { chunk }
                };
                var chunkJson = JsonSerializer.Serialize(chunkResponse, JsonOptions);
                _webView.Dispatcher.Invoke(() =>
                    _webView.CoreWebView2.PostWebMessageAsJson(chunkJson));
            },
            _streamingCts.Token);

        return new { content = fullContent, done = true };
    }

    private object HandleAiStreamCancel()
    {
        _streamingCts?.Cancel();
        return new { success = true };
    }

    private object HandleAiConfigure(object? payload)
    {
        var config = DeserializePayload<AiConfig>(payload);
        _claudeApiService.Configure(config);
        return new { success = true };
    }

    private object HandleAiSetKey(object? payload)
    {
        var data = DeserializePayload<ApiKeyPayload>(payload);
        _claudeApiService.SetApiKey(data.ApiKey);
        return new { success = true };
    }

    private object HandleAiSetModel(object? payload)
    {
        var data = DeserializePayload<ModelPayload>(payload);
        _claudeApiService.SetModel(data.Model);
        return new { success = true, model = data.Model };
    }

    private object HandleAiSetSystem(object? payload)
    {
        var data = DeserializePayload<SystemPromptPayload>(payload);
        _claudeApiService.SetSystemPrompt(data.SystemPrompt);
        return new { success = true };
    }

    private object HandleAiState()
    {
        return new
        {
            isConfigured = _claudeApiService.IsConfigured,
            model = _claudeApiService.CurrentModel,
            historyCount = _claudeApiService.History.Count
        };
    }

    private object HandleAiHistory()
    {
        return new
        {
            messages = _claudeApiService.History.Select(m => new { m.Role, m.Content }).ToList()
        };
    }

    private object HandleAiClear()
    {
        _claudeApiService.ClearHistory();
        return new { success = true };
    }

    // --- App handlers ---

    private static object GetAppVersion()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        return new { version = version?.ToString() ?? "0.1.0" };
    }

    // --- Helpers ---

    private static T DeserializePayload<T>(object? payload) where T : class
    {
        if (payload is JsonElement element)
        {
            return JsonSerializer.Deserialize<T>(element.GetRawText(), JsonOptions)
                ?? throw new ArgumentException($"Failed to deserialize payload to {typeof(T).Name}");
        }

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new ArgumentException($"Failed to deserialize payload to {typeof(T).Name}");
    }

    private static string TryExtractId(string json)
    {
        try
        {
            var node = JsonNode.Parse(json);
            return node?["id"]?.GetValue<string>() ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }
}

// Payload DTOs
internal class CommandPayload { public string Command { get; set; } = string.Empty; }
internal class ShellWritePayload { public string Data { get; set; } = string.Empty; }
internal class HostPayload { public string Host { get; set; } = string.Empty; }
internal class KeyPathPayload { public string Path { get; set; } = string.Empty; }
internal class ApiKeyPayload { public string ApiKey { get; set; } = string.Empty; }
internal class ModelPayload { public string Model { get; set; } = string.Empty; }
internal class SystemPromptPayload { public string? SystemPrompt { get; set; } }
