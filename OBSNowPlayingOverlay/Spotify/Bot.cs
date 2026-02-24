using OBSNowPlayingOverlay.Spotify.Model;
using OBSNowPlayingOverlay.WebSocketBehavior;
using OBSNowPlayingOverlay.Windows;
using Spectre.Console;
using SpotifyAPI.Web;

namespace OBSNowPlayingOverlay.Spotify
{
    public static class Bot
    {
        public static bool? IsConnect { get { return client != null && isRunning; } }

        private static SpotifyClient? client;
        private static bool isRunning = false;
        private static string _guid = Guid.NewGuid().ToString();

        public static void SetBotCred(string clientId, string clientSecret, AuthorizationCodeTokenResponse authorizationCodeToken)
        {
            var authenticator = new AuthorizationCodeAuthenticator(clientId, clientSecret, authorizationCodeToken);
            authenticator.TokenRefreshed += (sender, tokenResponse) =>
            {
                SettingWindow.SpotifyAPIConfig.AuthorizationCodeToken = tokenResponse;
                AnsiConsole.MarkupLine("[green]Spotify 金鑰已刷新[/]");
            };
            var config = SpotifyClientConfig.CreateDefault()
                .WithAuthenticator(authenticator);
            client = new SpotifyClient(config);
        }

        public static async Task<string> GetUserNameAsync()
        {
            if (client == null)
                return string.Empty;

            try
            {
                var profile = await client.UserProfile.Current();
                return profile.DisplayName;
            }
            catch (APIException ex)
            {
                AnsiConsole.WriteException(ex);
                return string.Empty;
            }
        }

        public static async Task<CurrentlyPlayingTrack?> GetCurrentlyPlayingTrackAsync()
        {
            if (client == null)
                return null;

            try
            {
                var _ = await client.Player.GetCurrentlyPlaying(new PlayerCurrentlyPlayingRequest()); // 假請求，主要是觸發 API 回傳下面的 LastResponse.Body
                if (client.LastResponse == null) // 邏輯上不會
                {
                    return null;
                }

                if (client.LastResponse.StatusCode == System.Net.HttpStatusCode.NoContent) // 用戶一段時間沒播放音訊會回傳 204 NoContent
                {
                    return null;
                }

                var currentlyPlaying = Newtonsoft.Json.JsonConvert.DeserializeObject<CurrentlyPlayingTrack>(client.LastResponse.Body!.ToString()!);
                return currentlyPlaying;
            }
            catch (APIException ex)
            {
                AnsiConsole.WriteException(ex);
                return null;
            }
        }

        public static void StartBot()
        {
            if (isRunning)
                return;

            NowPlaying.AddWebSocketClient(_guid);
            isRunning = true;

            Task.Run(async () =>
            {
                do
                {
                    var track = await GetCurrentlyPlayingTrackAsync();
                    if (track != null)
                    {
                        NowPlaying.ProcessNowPlayingData(new NowPlayingJson()
                        {
                            Title = track.Item.Name,
                            Artists = [.. track.Item.Artists.Select((x) => x.Name)],
                            Cover = track.Item.Album.Images.First().Url,
                            IsLive = false,
                            Platform = "spotify-api",
                            Guid = _guid,
                            Status = track.IsPlaying ? "playing" : "paused",
                            Progress = track.ProgressMs,
                            Duration = track.Item.DurationMs,
                            SongLink = track.Item.Album.ExternalUrls.Spotify,
                        });
                    }
                    await Task.Delay(500);
                } while (isRunning);
            });
        }

        public static void StopBot()
        {
            NowPlaying.RemoveWebSocketClient(_guid);
            isRunning = false;
        }
    }
}
