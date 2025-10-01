using OBSNowPlayingOverlay.WebSocketBehavior;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Communication;
using OBSWebsocketDotNet.Types.Events;
using Spectre.Console;
using System.IO;

namespace OBSNowPlayingOverlay.OBSWebSocket
{
    public static class Client
    {
        public static bool IsConnected => isConnected;
        public static OBSWebsocket OBSWebsocket => _obsWebsocket;

        private static bool isConnected = false;
        private static readonly OBSWebsocket _obsWebsocket = new();
        private static readonly List<string> _mediaSourceList = new();
        private static Timer _timer;

        static Client()
        {
            _obsWebsocket.Connected += _obsWebsocket_Connected;
            _obsWebsocket.Disconnected += _obsWebsocket_Disconnected;
            _obsWebsocket.InputCreated += _obsWebsocket_InputCreated;
            _obsWebsocket.InputRemoved += _obsWebsocket_InputRemoved;
            _obsWebsocket.InputNameChanged += _obsWebsocket_InputNameChanged;
            _obsWebsocket.CurrentProgramSceneChanged += _obsWebsocket_CurrentProgramSceneChanged;

            _timer = new Timer(_timer_Elapsed, null, TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(500));
        }

        private static void _obsWebsocket_InputRemoved(object? sender, InputRemovedEventArgs e)
        {
            AnsiConsole.MarkupLineInterpolated($"OBS 已移除媒體來源: [green]{e.InputName}[/]");
            if (_mediaSourceList.Contains(e.InputName))
            {
                _mediaSourceList.Remove(e.InputName);
            }
        }

        public static void Connect(string ip, string password)
        {
            if (!ip.StartsWith("ws://"))
            {
                ip = $"ws://{ip}";
            }

            if (!isConnected)
            {
                try
                {
                    _obsWebsocket.ConnectAsync(ip, password);
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine("[red]OBS WebSocket 連線失敗，可能是 IP 格式錯誤[/]");
                    AnsiConsole.WriteException(ex);
                }
            }
        }

        public static void Disconnect()
        {
            if (isConnected)
            {
                _obsWebsocket.Disconnect();
            }
        }

        private static void _timer_Elapsed(object? state)
        {
            if (isConnected)
            {
                try
                {
                    foreach (var item in _mediaSourceList)
                    {
                        try
                        {
                            var status = _obsWebsocket.GetMediaInputStatus(item);
                            if (status.Cursor == null || status.Duration == null)
                                continue;

                            var setting = _obsWebsocket.GetInputSettings(item);

                            // 使用本機檔案時 WebSocket 並不會設定 is_local_file 屬性，只有使用串流檔案時才會設定
                            //if (!setting.Settings.TryGetValue("is_local_file", out var isLocalFile) || !(bool)isLocalFile)
                            //    continue;

                            if (!setting.Settings.TryGetValue("local_file", out var filePath))
                                continue;

                            NowPlaying.ProcessNowPlayingData(new NowPlayingJson()
                            {
                                Title = Path.GetFileNameWithoutExtension(filePath!.ToString()),
                                Artists = [""],
                                Cover = "",
                                IsLive = false,
                                Platform = "obs",
                                Guid = item.GetHashCode().ToString(),
                                Status = status.State == OBSWebsocketDotNet.Types.MediaState.OBS_MEDIA_STATE_PLAYING ? "playing" : "paused",
                                Progress = status.Cursor.Value,
                                Duration = status.Duration.Value,
                                SongLink = "無",
                            });
                        }
                        catch (Exception ex)
                        {
                            AnsiConsole.MarkupLineInterpolated($"[red]取得 OBS 播放狀態失敗: {item}[/]");
                            AnsiConsole.WriteException(ex);
                        }
                    }
                }
                catch (InvalidOperationException)
                {
                    // OBS WebSocket 連線中斷時會清除清單，忽略
                }
            }
        }

        private static void _obsWebsocket_Connected(object? sender, EventArgs e)
        {
            AnsiConsole.MarkupLine("[green]OBS WebSocket 連線成功![/]");

            _mediaSourceList.Clear();
            foreach (var item in _obsWebsocket.GetInputList("ffmpeg_source"))
            {
                AnsiConsole.MarkupLineInterpolated($"新增媒體來源: [green]{item.InputName} ({item.InputName.GetHashCode()})[/]");
                _mediaSourceList.Add(item.InputName);

                NowPlaying.AddWebSocketClient(item.InputName.GetHashCode().ToString());
            }

            isConnected = true;
        }

        private static void _obsWebsocket_Disconnected(object? sender, ObsDisconnectionInfo e)
        {
            AnsiConsole.MarkupLine("[yellow]OBS WebSocket 連線中斷[/]");

            _mediaSourceList.Clear();

            isConnected = false;
        }

        private static void _obsWebsocket_CurrentProgramSceneChanged(object? sender, ProgramSceneChangedEventArgs e)
        {
            // Todo: 更換場景後需要讓舊場景的 Client IsPlaying = false
            // 否則需要等 Timer 3 秒鐘自動設定屬性後才會顯示新場景的媒體播放狀態
            AnsiConsole.MarkupLineInterpolated($"OBS 場景更換，清除最後使用的 Guid: [green]{e.SceneName}[/]");
            Windows.MainWindow.LatestWebSocketGuid = string.Empty;
        }

        private static void _obsWebsocket_InputCreated(object? sender, InputCreatedEventArgs e)
        {
            if (e.InputKind == "ffmpeg_source")
            {
                AnsiConsole.MarkupLineInterpolated($"新增媒體來源: [green]{e.InputName} ({e.InputName.GetHashCode()})[/]");
                _mediaSourceList.Add(e.InputName);

                NowPlaying.AddWebSocketClient(e.InputName.GetHashCode().ToString());
            }
        }

        private static void _obsWebsocket_InputNameChanged(object? sender, InputNameChangedEventArgs e)
        {
            if (_mediaSourceList.Contains(e.OldInputName))
            {
                AnsiConsole.MarkupLineInterpolated($"{e.OldInputName} 的名稱已更改為: [green]{e.InputName} ({e.InputName.GetHashCode()})[/]");

                _mediaSourceList.Remove(e.OldInputName);
                _mediaSourceList.Add(e.InputName);

                NowPlaying.RemoveWebSocketClient(e.OldInputName.GetHashCode().ToString());
                NowPlaying.AddWebSocketClient(e.InputName.GetHashCode().ToString());
            }
            else
            {
                AnsiConsole.MarkupLineInterpolated($"[purple]{e.OldInputName} 的名稱已更改但不在清單內，忽略[/]");
            }
        }
    }
}
