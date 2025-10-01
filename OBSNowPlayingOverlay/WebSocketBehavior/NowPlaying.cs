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
                if (e.Data.StartsWith('{') && e.Data.EndsWith('}'))
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

                        if (Windows.MainWindow.LatestWebSocketGuid == guid)
                        {
                            // 主動偵測是否有其他正在播放的 client，若沒有正在播放的 client 則改為第一個偵測到的 client 並切換
                            var playingClient = _clientDict.Values.FirstOrDefault(c => c.IsPlaying) ?? _clientDict.Values.FirstOrDefault();
                            if (playingClient != null)
                            {
                                // 確實會變動，但因為沒有新的 ProcessNowPlayingData 事件，導致主介面不會有畫面上的更新，直到 WebSocket Client 重新推送資料
                                // 若要解決畫面沒更新的問題可能得 WebSocketClientInfo 內多加 LatestJsonData 之類的欄位，但這樣頻繁刷新資料有點費資源
                                Windows.MainWindow.LatestWebSocketGuid = playingClient.Guid;
                            }
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

            bool wasPlaying = client.IsPlaying;
            client.LastActiveTime = DateTime.Now;
            client.IsPlaying = nowPlaying.Status == "playing";

            // 主動切換：如果本 client 從播放變成暫停，且目前 guid 是 LatestWebSocketGuid
            if (wasPlaying && !client.IsPlaying && Windows.MainWindow.LatestWebSocketGuid == client.Guid)
            {
                // 主動偵測是否有其他正在播放的 client，若沒有正在播放的 client 則改為第一個偵測到的 client 並切換
                var playingClient = _clientDict.Values.FirstOrDefault(c => c.IsPlaying) ?? _clientDict.Values.FirstOrDefault();
                if (playingClient != null)
                {
                    Windows.MainWindow.LatestWebSocketGuid = playingClient.Guid;
                }
                // 若沒有其他 client 正在播放，則不變動 LatestWebSocketGuid
            }

            // If this client is the only one playing, set it as the latest
            // 當有兩個來源在播放時，若其中一個來源暫停，會導致條件成立
            // 且會短暫的讓 LatestWebSocketGuid 設定為被暫停的來源，之後才會從原本持續播放的來源繼續顯示狀態
            // 所以這邊多加個 IsPlaying 判定來確保真的是這個 client 正在播放，避免資料錯誤
            if (_clientDict.Count(c => c.Value.IsPlaying) == 1 && client.IsPlaying)
            {
                Windows.MainWindow.LatestWebSocketGuid = client.Guid;
            }

            try
            {
                Windows.MainWindow.MsgQueue.TryAdd(nowPlaying);
            }
            catch (Exception)
            {
                // 也許是已經被標記為已完成，忽略
            }
        }
    }
}
