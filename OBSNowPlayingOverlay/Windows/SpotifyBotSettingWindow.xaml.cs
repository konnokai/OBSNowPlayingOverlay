using OBSNowPlayingOverlay.Config;
using OBSNowPlayingOverlay.Spotify;
using Spectre.Console;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace OBSNowPlayingOverlay.Windows
{
    /// <summary>
    /// SpotifySettingWindow.xaml 的互動邏輯
    /// </summary>
    public partial class SpotifyBotSettingWindow : Window
    {
        private readonly SpotifyAPIConfig _spotifyAPIConfig;

        public SpotifyBotSettingWindow(SpotifyAPIConfig spotifyAPIConfig)
        {
            InitializeComponent();

            _spotifyAPIConfig = spotifyAPIConfig;

            txt_ClientId.Dispatcher.Invoke(() =>
            {
                txt_ClientId.Password = spotifyAPIConfig.ClientId;
            });
            txt_ClientSecret.Dispatcher.Invoke(() =>
            {
                txt_ClientSecret.Password = spotifyAPIConfig.ClientSecret;
            });

            if (!string.IsNullOrEmpty(spotifyAPIConfig.AuthorizationCodeToken?.AccessToken))
            {
                btn_StartOAuth.Dispatcher.Invoke(() =>
                {
                    btn_StartOAuth.IsEnabled = false;
                });
                btn_CheckAccessToken.Dispatcher.Invoke(() =>
                {
                    btn_CheckAccessToken.IsEnabled = true;
                });
            }

            txt_AccessToken.Dispatcher.Invoke(() =>
            {
                txt_AccessToken.Password = spotifyAPIConfig.AuthorizationCodeToken?.AccessToken;
            });
            txt_UserLogin.Dispatcher.Invoke(() =>
            {
                txt_UserLogin.Text = spotifyAPIConfig.UserLogin;
            });
            cb_AutoLoginBot.Dispatcher.Invoke(() =>
            {
                cb_AutoLoginBot.IsChecked = spotifyAPIConfig.AutoLogin;
            });

            if (Bot.IsConnect.HasValue && Bot.IsConnect.Value)
            {
                txt_ClientId.Dispatcher.Invoke(() =>
                {
                    txt_ClientId.IsEnabled = false;
                });
                txt_ClientSecret.Dispatcher.Invoke(() =>
                {
                    txt_ClientSecret.IsEnabled = false;
                });
                btn_CheckAccessToken.Dispatcher.Invoke(() =>
                {
                    btn_CheckAccessToken.IsEnabled = false;
                });
                btn_StopBot.Dispatcher.Invoke(() =>
                {
                    btn_StopBot.IsEnabled = true;
                });
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Hide();
            e.Cancel = true;
        }

        // https://stackoverflow.com/a/10238715/15800522
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            var processStartInfo = new ProcessStartInfo(e.Uri.AbsoluteUri)
            {
                UseShellExecute = true
            };
            Process.Start(processStartInfo);
            e.Handled = true;
        }

        private async void btn_StartOAuth_Click(object sender, RoutedEventArgs e)
        {
            string clientId = string.Empty, clientSecret = string.Empty;

            txt_ClientId.Dispatcher.Invoke(() =>
            {
                clientId = txt_ClientId.Password;
            });
            txt_ClientSecret.Dispatcher.Invoke(() =>
            {
                clientSecret = txt_ClientSecret.Password;
            });

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                MessageBox.Show("clientId 或 clientSecret 為空\r\n請確認 OAuth 相關資訊是否已填寫正確", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            EmbedIOAuthServer _server = new(new Uri("http://127.0.0.1:5543/callback"), 5543);
            await _server.Start();

            _server.AuthorizationCodeReceived += async (sender, response) =>
            {
                try
                {
                    await _server.Stop();

                    var config = SpotifyClientConfig.CreateDefault();
                    var tokenResponse = await new OAuthClient(config).RequestToken(new AuthorizationCodeTokenRequest(clientId, clientSecret, response.Code, new Uri("http://127.0.0.1:5543/callback")));

                    AnsiConsole.MarkupLine("[green]Authorization successful![/]");

                    SettingWindow.SpotifyAPIConfig.ClientId = clientId;
                    SettingWindow.SpotifyAPIConfig.ClientSecret = clientSecret;
                    SettingWindow.SpotifyAPIConfig.AuthorizationCodeToken = tokenResponse;

                    Bot.SetBotCred(clientId, clientSecret, tokenResponse);
                    var userName = await Bot.GetUserNameAsync();
                    SettingWindow.SpotifyAPIConfig.UserLogin = userName;

                    txt_AccessToken.Dispatcher.Invoke(() =>
                    {
                        txt_AccessToken.Password = tokenResponse.AccessToken;
                    });
                    txt_UserLogin.Dispatcher.Invoke(() =>
                    {
                        txt_UserLogin.Text = userName;
                    });

                    txt_ClientId.Dispatcher.Invoke(() =>
                    {
                        txt_ClientId.IsEnabled = false;
                    });
                    txt_ClientSecret.Dispatcher.Invoke(() =>
                    {
                        txt_ClientSecret.IsEnabled = false;
                    });
                    btn_CheckAccessToken.Dispatcher.Invoke(() =>
                    {
                        btn_CheckAccessToken.IsEnabled = false;
                    });
                    btn_StartBot.Dispatcher.Invoke(() =>
                    {
                        btn_StartBot.IsEnabled = true;
                    });
                }
                catch (Exception ex)
                {
                    AnsiConsole.WriteException(ex);
                }
            };

            _server.ErrorReceived += async (sender, error, state) =>
            {
                await _server.Stop();
                AnsiConsole.MarkupLineInterpolated($"[red]Aborting authorization, error received: {error}[/]");
            };

            var request = new LoginRequest(_server.BaseUri, clientId, LoginRequest.ResponseType.Code)
            {
                Scope = [Scopes.UserReadPrivate, Scopes.UserReadCurrentlyPlaying]
            };

            var oauthUrl = request.ToUri();

            // 顯示 OAuth 網址與提示
            AnsiConsole.MarkupLine("[yellow]開啟 Spotify OAuth 頁面，若不慎關閉頁面請複製以下連結至瀏覽器重新驗證:[/]");
            AnsiConsole.MarkupLineInterpolated($"[link]{oauthUrl}[/]");

            BrowserUtil.Open(oauthUrl);
        }

        private async void btn_CheckAccessToken_Click(object sender, RoutedEventArgs e)
        {
            string clientId = string.Empty, clientSecret = string.Empty;

            txt_ClientId.Dispatcher.Invoke(() =>
            {
                clientId = txt_ClientId.Password;
            });
            txt_ClientSecret.Dispatcher.Invoke(() =>
            {
                clientSecret = txt_ClientSecret.Password;
            });

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) || _spotifyAPIConfig.AuthorizationCodeToken == null)
            {
                MessageBox.Show("clientId, clientSecret 或 AuthorizationCodeToken 為空\r\n請確認 OAuth 相關資訊是否已填寫正確並已正常登入", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Bot.SetBotCred(clientId, clientSecret, _spotifyAPIConfig.AuthorizationCodeToken);

            var userName = await Bot.GetUserNameAsync();
            if (string.IsNullOrEmpty(userName))
            {
                txt_AccessToken.Dispatcher.Invoke(() =>
                {
                    txt_AccessToken.Password = "";
                });
                txt_UserLogin.Dispatcher.Invoke(() =>
                {
                    txt_UserLogin.Text = "";
                });

                SettingWindow.SpotifyAPIConfig.AuthorizationCodeToken = null;
                SettingWindow.SpotifyAPIConfig.UserLogin = "";

                MessageBox.Show("無法獲取 Spotify 用戶名，請確認 OAuth 相關資訊是否正確並重新登入", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            SettingWindow.SpotifyAPIConfig.UserLogin = userName;
            txt_UserLogin.Dispatcher.Invoke(() =>
            {
                txt_UserLogin.Text = userName;
            });

            btn_CheckAccessToken.Dispatcher.Invoke(() =>
            {
                btn_CheckAccessToken.IsEnabled = false;
            });
            btn_StartBot.Dispatcher.Invoke(() =>
            {
                btn_StartBot.IsEnabled = true;
            });
            btn_StopBot.Dispatcher.Invoke(() =>
            {
                btn_StopBot.IsEnabled = false;
            });
        }

        private void btn_StartBot_Click(object sender, RoutedEventArgs e)
        {
            Bot.StartBot();

            txt_ClientId.Dispatcher.Invoke(() =>
            {
                txt_ClientId.IsEnabled = false;
            });
            txt_ClientSecret.Dispatcher.Invoke(() =>
            {
                txt_ClientSecret.IsEnabled = false;
            });
            btn_StartBot.Dispatcher.Invoke(() =>
            {
                btn_StartBot.IsEnabled = false;
            });
            btn_StopBot.Dispatcher.Invoke(() =>
            {
                btn_StopBot.IsEnabled = true;
            });
        }

        private void btn_StopBot_Click(object sender, RoutedEventArgs e)
        {
            Bot.StopBot();

            btn_StartBot.Dispatcher.Invoke(() =>
            {
                btn_StartBot.IsEnabled = true;
            });
            btn_StopBot.Dispatcher.Invoke(() =>
            {
                btn_StopBot.IsEnabled = false;
            });
        }

        private void cb_AutoLoginBot_Checked(object sender, RoutedEventArgs e)
        {
            SettingWindow.SpotifyAPIConfig.AutoLogin = true;
        }

        private void cb_AutoLoginBot_Unchecked(object sender, RoutedEventArgs e)
        {
            SettingWindow.SpotifyAPIConfig.AutoLogin = false;
        }
    }
}
