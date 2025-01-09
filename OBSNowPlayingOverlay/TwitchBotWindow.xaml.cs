using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Spectre.Console;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
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

        public TwitchBotWindow(TwitchBotConfig twitchBotConfig)
        {
            InitializeComponent();

            webView.NavigationStarting += WebView_NavigationStarting;

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
                btn_StopBot.Dispatcher.Invoke(() =>
                {
                    btn_StopBot.IsEnabled = true;
                });
            }
        }

        // https://stackoverflow.com/a/10238715/15800522
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            // for .NET Core you need to add UseShellExecute = true
            // see https://learn.microsoft.com/dotnet/api/system.diagnostics.processstartinfo.useshellexecute#property-value
            var processStartInfo = new ProcessStartInfo(e.Uri.AbsoluteUri)
            {
                UseShellExecute = true
            };
            Process.Start(processStartInfo);
            e.Handled = true;
        }

        private void btn_StartOAuth_Click(object sender, RoutedEventArgs e)
        {
            if (webView != null)
            {
                webView.Visibility = Visibility.Visible;

                webView.Source = new Uri($"https://id.twitch.tv/oauth2/authorize" +
                    $"?response_type=token" +
                    $"&client_id={_twitchBotConfig.ClientId}" +
                    $"&redirect_uri=http://localhost" +
                    $"&scope=chat:read+chat:edit");

                btn_StartOAuth.Dispatcher.Invoke(() =>
                {
                    btn_StartOAuth.IsEnabled = false;
                });
            }
        }

        // https://github.com/joacand/VattenMedia/blob/main/VattenMedia/Views/OAuthWindowView.xaml.cs
        private void WebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            string url = e.Uri.ToString();
            Navigating(url);
        }

        private void Navigating(string url)
        {
            if (!url.StartsWith("http://localhost/"))
                return;

            var regex = new Regex(@"access_token=(?'AccessToken'[\w]*)&");
            var match = regex.Match(url);
            if (match.Success)
            {
                AnsiConsole.MarkupLine("[green]Twitch 登入成功![/]");

                string accessToken = match.Groups["AccessToken"].ToString();
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
                catch (Exception)
                {
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Twitch 登入失敗: 找不到 AccessToken，請重新登入[/]");
                webView.CoreWebView2.CookieManager.DeleteAllCookies();
                btn_StartOAuth.Dispatcher.Invoke(() =>
                {
                    btn_StartOAuth.IsEnabled = true;
                });
            }

            webView.Source = new Uri("about:blank");
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

            var accessTokenResponse = await twitchAPI.Auth.ValidateAccessTokenAsync();
            if (accessTokenResponse == null)
            {
                AnsiConsole.MarkupLine("[red]Twitch AccessToken 驗證失敗，請重新登入[/]");
                SettingWindow.TwitchBotConfig.AccessToken = "";

                await webView.EnsureCoreWebView2Async()
                    .ContinueWith((obj) =>
                    {
                        Task.Run(() =>
                        {
                            webView.CoreWebView2.CookieManager.DeleteAllCookies();
                        });
                    });

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

                return;
            }

            AnsiConsole.MarkupLineInterpolated($"[green]Twitch AccessToken 驗證成功，過期時間: {DateTime.Now.AddSeconds(accessTokenResponse.ExpiresIn)}[/]");

            txt_UserLogin.Dispatcher.Invoke(() =>
            {
                txt_UserLogin.Text = accessTokenResponse.Login;
            });

            SettingWindow.TwitchBotConfig.UserLogin = accessTokenResponse.Login;

            btn_StartBot.Dispatcher.Invoke(() =>
            {
                btn_StartBot.IsEnabled = true;
            });

            try
            {
                File.WriteAllText("TwitchBotConfig.json", JsonConvert.SerializeObject(SettingWindow.TwitchBotConfig, Formatting.Indented));
            }
            catch (Exception)
            {
            }

            TwitchBot.Bot.SetBotCred(SettingWindow.TwitchBotConfig.AccessToken, accessTokenResponse.Login);
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
