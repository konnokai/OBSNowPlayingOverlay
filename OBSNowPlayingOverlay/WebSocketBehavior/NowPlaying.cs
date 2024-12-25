using Newtonsoft.Json;
using Spectre.Console;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using WebSocketSharp;

namespace OBSNowPlayingOverlay.WebSocketBehavior
{
    public class NowPlaying : WebSocketSharp.Server.WebSocketBehavior
    {
        private readonly static ConcurrentDictionary<string, WebSocketClientInfo> _clientDict = new();
        private readonly Regex _wsMsgRegex = new(@"(?<Type>\S+) - (?<SiteName>\S+) \((?<Guid>[^)]+)\)");
        private readonly Timer _clientActivityTimer;

        public NowPlaying()
        {
            _clientActivityTimer = new Timer(CheckClientActivity, null, 0, 3000);
        }

        private static void CheckClientActivity(object? state)
        {
            var inactiveClients = _clientDict.Where(c => (DateTime.Now - c.Value.LastActiveTime).TotalSeconds > 3).ToList();

            foreach (var client in inactiveClients)
            {
                client.Value.IsPlaying = false;
            }
        }

        internal static void AddWebSocketClient(string guid)
        {
            if (!_clientDict.ContainsKey(guid))
            {
                _clientDict.TryAdd(guid, new WebSocketClientInfo(guid));
            }
        }

        internal static void RemoveWebSocketClient(string guid)
        {
            _clientDict.TryRemove(guid, out _);
        }

        protected override void OnError(ErrorEventArgs e)
        {
            base.OnError(e);

            AnsiConsole.WriteException(e.Exception);
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            try
            {
                base.OnMessage(e);

                if (e.IsPing)
                    return;

                if (string.IsNullOrEmpty(e.Data))
                    return;

                // Update the logic to handle JSON messages.
                if (e.Data.StartsWith("{") && e.Data.EndsWith("}"))
                {
                    NowPlayingJson nowPlaying = JsonConvert.DeserializeObject<NowPlayingJson>(e.Data)!;

                    ProcessNowPlayingData(nowPlaying);
                }
                else if (_wsMsgRegex.IsMatch(e.Data))
                {
                    var match = _wsMsgRegex.Match(e.Data);
                    var guid = match.Groups["Guid"].ToString();

                    if (match.Groups["Type"].ToString() == "connected")
                    {
                        AnsiConsole.MarkupLineInterpolated($"連線狀態變更: [springgreen4]新連線[/] | [yellow4_1]{match.Groups["SiteName"]}[/] | [purple4_1]{guid}[/]");

                        AddWebSocketClient(guid);
                    }
                    else if (match.Groups["Type"].ToString() == "closed")
                    {
                        AnsiConsole.MarkupLineInterpolated($"連線狀態變更: [orangered1]已關閉[/] | [yellow4_1]{match.Groups["SiteName"]}[/] | [purple4_1]{guid}[/]");

                        _clientDict.TryRemove(guid, out _);

                        if (MainWindow.LatestWebSocketGuid == guid)
                        {
                            MainWindow.LatestWebSocketGuid = string.Empty;
                        }
                    }
                }
                else
                {
                    AnsiConsole.MarkupLineInterpolated($"接收到未處理的資料: [dodgerblue2]{e.Data}[/]");
                }
            }
            catch (OperationCanceledException) { }
            catch (InvalidOperationException) { }
            catch (JsonSerializationException jsonEx)
            {
                AnsiConsole.MarkupLineInterpolated($"Json 解析失敗: [olive]{e.Data}[/]");
                AnsiConsole.WriteException(jsonEx, ExceptionFormats.Default);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLineInterpolated($"[olive]{e.Data}[/]");
                AnsiConsole.WriteException(ex, ExceptionFormats.Default);
            }
        }

        internal static void ProcessNowPlayingData(NowPlayingJson nowPlaying)
        {
            // Find the client in the list or create a new one
            if (!_clientDict.TryGetValue(nowPlaying.Guid, out var client))
            {
                client = new WebSocketClientInfo(nowPlaying.Guid);
                _clientDict.TryAdd(nowPlaying.Guid, client);
            }

            client.LastActiveTime = DateTime.Now;
            client.IsPlaying = nowPlaying.Status == "playing";

            // If this client is the only one playing, set it as the latest
            // 當有兩個來源在播放時，若其中一個來源暫停，會導致條件成立
            // 且會短暫的讓 LatestWebSocketGuid 設定為被暫停的來源，之後才會從原本持續播放的來源繼續顯示狀態
            // 所以這邊多加個 IsPlaying 判定來確保真的是這個 client 正在播放，避免資料錯誤
            if (_clientDict.Count(c => c.Value.IsPlaying) == 1 && client.IsPlaying)
            {
                MainWindow.LatestWebSocketGuid = client.Guid;
            }

            try
            {
                MainWindow.MsgQueue.TryAdd(nowPlaying);
            }
            catch (Exception)
            {
                // 也許是已經被標記為已完成，忽略
            }
        }
    }
}
