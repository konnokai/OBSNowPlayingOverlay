using Spectre.Console;
using System.Diagnostics;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;

namespace OBSNowPlayingOverlay.TwitchBot
{
    public static class Bot
    {
        public static bool? IsConnect { get { return client?.IsConnected; } }

        private static TwitchClient? client = null;
        private static DateTime latestNPCommandExecuteTime = DateTime.MinValue;

        private static readonly string[] _musicCommandArray = ["music", "playing", "np", "nowplaying", "正在播放", "音樂"];

        public static void SetBotCred(string accessToken, string userLogin)
        {
            var credentials = new ConnectionCredentials(userLogin, accessToken);
            var clientOptions = new ClientOptions
            {
                MessagesAllowedInPeriod = 750,
                ThrottlingPeriod = TimeSpan.FromSeconds(30)
            };

            var customClient = new WebSocketClient(clientOptions);
            client = new TwitchClient(customClient);
            client.Initialize(credentials, userLogin, autoReListenOnExceptions: !Debugger.IsAttached);

            client.OnJoinedChannel += client_OnJoinedChannel;
            client.OnConnected += client_OnConnected;
            client.OnChatCommandReceived += client_OnChatCommandReceived;
        }

        public static void StartBot()
        {
            Console.WriteLine("Twitch Bot 連線中...");
            client?.Connect();
        }

        public static void StopBot()
        {
            if (client != null)
            {
                Console.WriteLine("Twitch Bot 離線中...");
                client.Disconnect();

                client.OnJoinedChannel -= client_OnJoinedChannel;
                client.OnConnected -= client_OnConnected;
                client.OnChatCommandReceived -= client_OnChatCommandReceived;
            }

            client = null;

            Console.WriteLine("Twitch Bot 已離線");
        }

        private static void client_OnChatCommandReceived(object? sender, OnChatCommandReceivedArgs e)
        {
            // 每 30 秒觸發一次指令
            if (_musicCommandArray.Contains(e.Command.CommandText.Trim()) && DateTime.Now.Subtract(latestNPCommandExecuteTime).TotalSeconds >= 30)
            {
                AnsiConsole.MarkupLineInterpolated($"Twitch Bot 指令觸發: [green]{e.Command.ChatMessage.Username} - {e.Command.CommandText}[/]");

                if (string.IsNullOrEmpty(MainWindow.NowPlayingTitle))
                {
                    client?.SendMessage(e.Command.ChatMessage.Channel, "目前沒有播放的音樂");
                }
                else
                {
                    client?.SendMessage(e.Command.ChatMessage.Channel, $"正在播放: {MainWindow.NowPlayingTitle}");
                    client?.SendMessage(e.Command.ChatMessage.Channel, $"網址: {MainWindow.NowPlayingUrl}");
                }

                latestNPCommandExecuteTime = DateTime.Now;
            }
        }

        private static void client_OnConnected(object? sender, OnConnectedArgs e)
        {
            Console.WriteLine($"Twitch Bot 已連線到 IRC");
        }

        private static void client_OnJoinedChannel(object? sender, OnJoinedChannelArgs e)
        {
            AnsiConsole.MarkupLineInterpolated($"Twitch Bot 已連線到頻道: [green]{e.Channel}[/]");
        }
    }
}
