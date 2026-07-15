using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using mixer.Models;

namespace mixer.Services
{
    /// <summary>
    /// Talks to the Node.js bridge server over WebSocket (ws://localhost:8765).
    ///
    /// Lessons applied:
    ///  - PropertyNameCaseInsensitive = true is mandatory: the server sends lowercase
    ///    keys (type, data, inputs, isMuted, ...) while our C# DTOs use PascalCase.
    ///    Without this the UI silently stays empty.
    ///  - Debounce outgoing slider commands with a CancellationTokenSource per key so we
    ///    only send the final value after the user stops dragging, instead of flooding
    ///    the server (and freezing the UI).
    ///  - Auto-reconnect with a simple retry loop, since the Node server or Wave Link
    ///    itself might not be up yet when the app starts.
    /// </summary>
    public class WaveLinkService : IDisposable
    {
        private const string ServerUri = "ws://localhost:8765/";
        private const int DebounceMilliseconds = 10;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private ClientWebSocket? _socket;
        private CancellationTokenSource? _receiveLoopCts;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _debounceTokens = new();

        public event Action<StateData>? StateReceived;
        public event Action<bool>? ConnectionChanged; // true = connected
        public event Action<string>? StatusReceived;  // status messages from server
        public event Action<LevelMeterData>? LevelMetersReceived;
        public event Action<FocusedAppData>? FocusedAppReceived;

        public bool IsConnected => _socket?.State == WebSocketState.Open;

        public async Task ConnectAsync()
        {
            while (true)
            {
                try
                {
                    _socket = new ClientWebSocket();
                    Logger.Info("Connecting to mixer bridge server...");
                    await _socket.ConnectAsync(new Uri(ServerUri), CancellationToken.None);
                    Logger.Info("Connected to mixer bridge server.");
                    ConnectionChanged?.Invoke(true);

                    _receiveLoopCts = new CancellationTokenSource();
                    await ReceiveLoopAsync(_receiveLoopCts.Token);
                }
                catch (Exception ex)
                {
                    Logger.Error("WaveLinkService connection error", ex);
                }
                finally
                {
                    ConnectionChanged?.Invoke(false);
                    _socket?.Dispose();
                    _socket = null;
                }

                // Retry after a short delay.
                await Task.Delay(3000);
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            var buffer = new byte[8192];
            var messageBuilder = new StringBuilder();

            while (_socket is { State: WebSocketState.Open } && !token.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                messageBuilder.Clear();

                do
                {
                    result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Logger.Warn("Server closed the WebSocket connection.");
                        return;
                    }

                    messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
                while (!result.EndOfMessage);

                HandleIncomingMessage(messageBuilder.ToString());
            }
        }

        private void HandleIncomingMessage(string json)
        {
            try
            {
                var msg = JsonSerializer.Deserialize<ServerMessage>(json, JsonOptions);
                if (msg == null) return;

                if (string.IsNullOrWhiteSpace(msg.Type))
                {
                    Logger.Warn("Received message with null/empty type.");
                    return;
                }

                switch (msg.Type)
                {
                    case "state":
                        if (msg.Data == null)
                        {
                            Logger.Warn("Received state message with null Data.");
                            return;
                        }
                        if (msg.Data.Inputs == null) msg.Data.Inputs = new();
                        if (msg.Data.Mixes == null) msg.Data.Mixes = new();
                        if (msg.Data.Cells == null) msg.Data.Cells = new();
                        StateReceived?.Invoke(msg.Data);
                        break;

                    case "error":
                        Logger.Warn($"Server reported error: {json}");
                        break;

                    case "status":
                        if (msg is { Data: not null })
                        {
                            // Status message: extract status string via raw JSON
                            using var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.TryGetProperty("status", out var statusProp))
                            {
                                StatusReceived?.Invoke(statusProp.GetString() ?? "");
                            }
                        }
                        break;

                    case "levelMeters":
                        try
                        {
                            var meters = JsonSerializer.Deserialize<LevelMeterData>(json, JsonOptions);
                            if (meters != null)
                                LevelMetersReceived?.Invoke(meters);
                        }
                        catch { }
                        break;

                    case "focusedApp":
                        try
                        {
                            var app = JsonSerializer.Deserialize<FocusedAppData>(json, JsonOptions);
                            if (app != null)
                                FocusedAppReceived?.Invoke(app);
                        }
                        catch { }
                        break;

                    default:
                        Logger.Debug($"Unhandled message type: {msg.Type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to parse incoming server message", ex);
            }
        }

        // -----------------------------------------------------------------
        // Outgoing commands (debounced per logical key so rapid slider drags
        // don't flood the server).
        // -----------------------------------------------------------------

        public void SetInputVolumeDebounced(string inputId, double volume)
        {
            DebouncedSend($"vol|{inputId}", () => new
            {
                type = "setInputVolume",
                inputId,
                volume
            });
        }

        public void SetInputMixVolumeDebounced(string inputId, string mixId, double volume)
        {
            DebouncedSend($"send|{inputId}|{mixId}", () => new
            {
                type = "setInputMixVolume",
                inputId,
                mixId,
                volume
            });
        }

        public void SetInputMute(string inputId, bool isMuted)
        {
            // Mute toggles are not debounced -- they're discrete, immediate actions.
            _ = SendAsync(new
            {
                type = "setInputMute",
                inputId,
                isMuted
            });
        }

        /// <summary>كتم/إلغاء كتم إرسال قناة معينة إلى مزيج معين (خلية).</summary>
        public void SetInputMixMute(string inputId, string mixId, bool isMuted)
        {
            _ = SendAsync(new
            {
                type = "setInputMixMute",
                inputId,
                mixId,
                isMuted
            });
        }

        /// <summary>كتم/إلغاء كتم مزيج بالكامل.</summary>
        public void SetMixMute(string mixId, bool isMuted)
        {
            _ = SendAsync(new
            {
                type = "setMixMute",
                mixId,
                isMuted
            });
        }

        public void AddChannelToMix(string inputId, string mixId)
        {
            _ = SendAsync(new
            {
                type = "addChannelToMix",
                inputId,
                mixId
            });
        }

        private void DebouncedSend(string key, Func<object> buildPayload)
        {
            // Cancel any pending send for this same key.
            if (_debounceTokens.TryRemove(key, out var existing))
            {
                existing.Cancel();
                existing.Dispose();
            }

            var cts = new CancellationTokenSource();
            _debounceTokens[key] = cts;
            var token = cts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(DebounceMilliseconds, token);
                    if (!token.IsCancellationRequested)
                    {
                        await SendAsync(buildPayload());
                    }
                }
                catch (TaskCanceledException)
                {
                    // Expected when a newer value supersedes this one.
                }
            }, token);
        }

        private async Task SendAsync(object payload)
        {
            if (_socket is not { State: WebSocketState.Open })
            {
                Logger.Warn("Attempted to send while WebSocket is not open.");
                return;
            }

            try
            {
                var json = JsonSerializer.Serialize(payload);
                var bytes = Encoding.UTF8.GetBytes(json);
                await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to send command to server", ex);
            }
        }

        public void Dispose()
        {
            _receiveLoopCts?.Cancel();
            _socket?.Dispose();

            foreach (var cts in _debounceTokens.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }
            _debounceTokens.Clear();
        }
    }
}