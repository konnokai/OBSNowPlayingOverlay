using Newtonsoft.Json;
using Spectre.Console;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Navigation;
using TwitchLib.Api;

namespace OBSNowPlayingOverlay
{
    /// <summary>
    /// TwitchBotWindow.xaml 的互動邏輯
    /// </summary>
    public partial class TwitchBotWindow : Window
    {
        private readonly TwitchBotConfig _twitchBotConfig;
        private const int OAuthPort = 9998;

        public TwitchBotWindow(TwitchBotConfig twitchBotConfig)
        {
            InitializeComponent();

            _twitchBotConfig = twitchBotConfig;

            if (!string.IsNullOrEmpty(_twitchBotConfig.AccessToken))
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
                txt_AccessToken.Password = _twitchBotConfig.AccessToken;
            });
            txt_ClientId.Dispatcher.Invoke(() =>
            {
                txt_ClientId.Password = _twitchBotConfig.ClientId;
            });
            txt_UserLogin.Dispatcher.Invoke(() =>
            {
                txt_UserLogin.Text = _twitchBotConfig.UserLogin;
            });
            cb_AutoLoginBot.Dispatcher.Invoke(() =>
            {
                cb_AutoLoginBot.IsChecked = _twitchBotConfig.AutoLogin;
            });

            if (TwitchBot.Bot.IsConnect.HasValue && TwitchBot.Bot.IsConnect.Value)
            {
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

        // 統一處理 HTML 回應，設定正確 Content-Type 與編碼
        private void SendHtmlResponse(HttpListenerContext context, string html)
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(html);
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();
        }

        // 等待帶有 access_token、error 或 state 參數的 OAuth callback
        private async Task<HttpListenerContext> AwaitOAuthCallbackAsync(HttpListener listener)
        {
            HttpListenerContext context;
            bool hasState;

            do
            {
                context = await listener.GetContextAsync();
                var query = context.Request.QueryString;
                hasState = !string.IsNullOrEmpty(query["state"]);

                if (!hasState)
                {
                    // 回傳 JS 將 fragment 轉為 query string 並重新導向
                    string js = @"<html><body><script>
                        if (window.location.hash.length > 1) {
                            var q = window.location.hash.substring(1);
                            window.location.href = window.location.pathname + '?' + q;
                        } else {
                            document.write('請從 Twitch OAuth 頁面登入');
                        }
                        </script></body></html>";
                    SendHtmlResponse(context, js);
                }
            }
            while (!hasState);

            return context;
        }

        private async void btn_StartOAuth_Click(object sender, RoutedEventArgs e)
        {
            btn_StartOAuth.Dispatcher.Invoke(() =>
            {
                btn_StartOAuth.IsEnabled = false;
            });

            // 產生隨機 state 參數
            string state = Guid.NewGuid().ToString("N");
            string redirectUri = $"http://localhost:{OAuthPort}/";
            string oauthUrl = $"https://id.twitch.tv/oauth2/authorize" +
                $"?response_type=token" +
                $"&client_id={_twitchBotConfig.ClientId}" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                $"&scope=chat:read+chat:edit" +
                $"&force_verify=true" +
                $"&state={state}";

            // 啟動 TcpListener 等待 Twitch redirect
            var listener = new HttpListener();
            listener.Prefixes.Add(redirectUri);
            listener.Start();

            // 顯示 OAuth 網址與提示
            AnsiConsole.MarkupLine("[yellow]開啟 Twitch OAuth 頁面，若不慎關閉頁面請複製以下連結至瀏覽器重新驗證:[/]");
            AnsiConsole.MarkupLineInterpolated($"[link]{oauthUrl}[/]");

            // 用預設瀏覽器開啟授權頁
            Process.Start(new ProcessStartInfo(oauthUrl) { UseShellExecute = true });

            try
            {
                string accessToken = string.Empty;
                string receivedState = string.Empty;
                string error = string.Empty;
                string errorDescription = string.Empty;

                // 只處理帶有 state 的請求
                var context = await AwaitOAuthCallbackAsync(listener);

                // 直接從 QueryString 取得參數
                var query = context.Request.QueryString;
                accessToken = query["access_token"] ?? string.Empty;
                receivedState = query["state"] ?? string.Empty;
                error = query["error"] ?? string.Empty;
                errorDescription = query["error_description"] ?? string.Empty;
                if (!string.IsNullOrEmpty(errorDescription))
                {
                    errorDescription = System.Web.HttpUtility.UrlDecode(errorDescription);
                }

                string responseHtml = string.Empty;
                if (!string.IsNullOrEmpty(error))
                {
                    AnsiConsole.MarkupLine($"[red]Twitch OAuth 失敗: {error} - {errorDescription}[/]");

                    btn_StartOAuth.Dispatcher.Invoke(() =>
                    {
                        btn_StartOAuth.IsEnabled = true;
                    });

                    responseHtml = $"<html><body><h2>授權失敗</h2><p>錯誤: {error}</p><p>{errorDescription}</p></body></html>";
                }
                else if (!string.IsNullOrEmpty(accessToken) && receivedState == state)
                {
                    AnsiConsole.MarkupLine("[green]Twitch 登入成功![/]");

                    SettingWindow.TwitchBotConfig.AccessToken = accessToken;

                    txt_AccessToken.Dispatcher.Invoke(() =>
                    {
                        txt_AccessToken.Password = accessToken;
                    });
                    btn_CheckAccessToken.Dispatcher.Invoke(() =>
                    {
                        btn_CheckAccessToken.IsEnabled = true;
                    });

                    try
                    {
                        File.WriteAllText("TwitchBotConfig.json", JsonConvert.SerializeObject(SettingWindow.TwitchBotConfig, Formatting.Indented));
                    }
                    catch (Exception) { }

                    responseHtml = "<html><body><h2>授權成功，請關閉此頁面</h2>";
                }
                else if (!string.IsNullOrEmpty(accessToken) && receivedState != state)
                {
                    AnsiConsole.MarkupLine("[red]Twitch 登入失敗: state 驗證失敗，請重新登入[/]");

                    btn_StartOAuth.Dispatcher.Invoke(() =>
                    {
                        btn_StartOAuth.IsEnabled = true;
                    });

                    responseHtml = "<html><body><h2>授權失敗，state 驗證失敗，請重試</h2></body></html>";
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]Twitch 登入失敗: 找不到 AccessToken，請重新登入[/]");

                    btn_StartOAuth.Dispatcher.Invoke(() =>
                    {
                        btn_StartOAuth.IsEnabled = true;
                    });

                    responseHtml = "<html><body><h2>授權失敗，請重試</h2></body></html>";
                }

                SendHtmlResponse(context, responseHtml);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]OAuth 流程發生錯誤: {ex.Message}[/]");
                btn_StartOAuth.Dispatcher.Invoke(() =>
                {
                    btn_StartOAuth.IsEnabled = true;
                });
            }
            finally
            {
                listener.Stop();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Hide();
            e.Cancel = true;
        }

        private async void btn_CheckAccessToken_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SettingWindow.TwitchBotConfig.AccessToken) || string.IsNullOrEmpty(SettingWindow.TwitchBotConfig.ClientId))
                return;

            btn_CheckAccessToken.Dispatcher.Invoke(() =>
            {
                btn_CheckAccessToken.IsEnabled = false;
            });

            var twitchAPI = new TwitchAPI()
            {
                Helix =
                {
                    Settings =
                    {
                        AccessToken = SettingWindow.TwitchBotConfig.AccessToken,
                        ClientId = SettingWindow.TwitchBotConfig.ClientId
                    }
                }
            };

            try
            {
                var accessTokenResponse = await twitchAPI.Auth.ValidateAccessTokenAsync();
                if (accessTokenResponse == null)
                {
                    SettingWindow.TwitchBotConfig.AccessToken = "";

                    btn_StartOAuth.Dispatcher.Invoke(() =>
                    {
                        btn_StartOAuth.IsEnabled = true;
                    });

                    btn_CheckAccessToken.Dispatcher.Invoke(() =>
                    {
                        btn_CheckAccessToken.IsEnabled = false;
                    });

                    txt_AccessToken.Dispatcher.Invoke(() =>
                    {
                        txt_AccessToken.Password = "";
                    });

                    MessageBox.Show("Twitch AccessToken 驗證失敗，請重新登入", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    AnsiConsole.MarkupLineInterpolated($"[green]Twitch AccessToken 驗證成功，過期時間: {DateTime.Now.AddSeconds(accessTokenResponse.ExpiresIn)}[/]");

                    btn_CheckAccessToken.Dispatcher.Invoke(() =>
                    {
                        btn_CheckAccessToken.IsEnabled = true;
                    });

                    txt_UserLogin.Dispatcher.Invoke(() =>
                    {
                        txt_UserLogin.Text = accessTokenResponse.Login;
                    });

                    SettingWindow.TwitchBotConfig.UserLogin = accessTokenResponse.Login;

                    btn_StartBot.Dispatcher.Invoke(() =>
                    {
                        btn_StartBot.IsEnabled = true;
                    });

                    TwitchBot.Bot.SetBotCred(SettingWindow.TwitchBotConfig.AccessToken, accessTokenResponse.Login);
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]Twitch AccessToken 驗證失敗: {ex.Message}[/]");
                btn_CheckAccessToken.Dispatcher.Invoke(() =>
                {
                    btn_CheckAccessToken.IsEnabled = true;
                });
            }

            try
            {
                File.WriteAllText("TwitchBotConfig.json", JsonConvert.SerializeObject(SettingWindow.TwitchBotConfig, Formatting.Indented));
            }
            catch (Exception) { }
        }

        private void btn_StartBot_Click(object sender, RoutedEventArgs e)
        {
            TwitchBot.Bot.StartBot();

            btn_StartBot.Dispatcher.Invoke(() =>
            {
                btn_StartBot.IsEnabled = false;
            });
            btn_StopBot.Dispatcher.Invoke(() =>
            {
                btn_StopBot.IsEnabled = true;
            });
            btn_CheckAccessToken.Dispatcher.Invoke(() =>
            {
                btn_CheckAccessToken.IsEnabled = false;
            });
        }

        private void btn_StopBot_Click(object sender, RoutedEventArgs e)
        {
            TwitchBot.Bot.StopBot();

            btn_StartBot.Dispatcher.Invoke(() =>
            {
                btn_StartBot.IsEnabled = false;
            });
            btn_StopBot.Dispatcher.Invoke(() =>
            {
                btn_StopBot.IsEnabled = false;
            });
            btn_CheckAccessToken.Dispatcher.Invoke(() =>
            {
                btn_CheckAccessToken.IsEnabled = true;
            });
        }

        private void cb_AutoLoginBot_Checked(object sender, RoutedEventArgs e)
        {
            SettingWindow.TwitchBotConfig.AutoLogin = true;
        }

        private void cb_AutoLoginBot_Unchecked(object sender, RoutedEventArgs e)
        {
            SettingWindow.TwitchBotConfig.AutoLogin = false;
        }
    }
}
