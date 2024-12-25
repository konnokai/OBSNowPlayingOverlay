using AutoUpdaterDotNET;
using Newtonsoft.Json;
using OBSNowPlayingOverlay.WebSocketBehavior;
using Spectre.Console;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using WebSocketSharp.Server;
using FontFamily = System.Windows.Media.FontFamily;

namespace OBSNowPlayingOverlay
{
    /// <summary>
    /// SettingWindow.xaml 的互動邏輯
    /// </summary>
    public partial class SettingWindow : Window
    {
        public static TwitchBotConfig TwitchBotConfig { get; set; } = new();

        private readonly Config _config = new();
        private readonly MainWindow _mainWindow = new();
        private readonly ObservableCollection<KeyValuePair<string, FontFamily>> _fontFamilies = new();

        private WebSocketServer? _wsServer;

        public SettingWindow()
        {
            InitializeComponent();

            try
            {
                AutoUpdater.RunUpdateAsAdmin = false;
                AutoUpdater.HttpUserAgent = "OBSNowPlayingOverlay";
                AutoUpdater.SetOwner(this);
                AutoUpdater.CheckForUpdateEvent += (e) =>
                {
                    if (e.Error != null)
                    {
                        AnsiConsole.WriteException(e.Error);
                    }
                    else if (e.IsUpdateAvailable)
                    {
                        AnsiConsole.MarkupLine("檢查更新: [green]發現更新![/]");
                        Dispatcher.Invoke(() => AutoUpdater.ShowUpdateForm(e));
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("檢查更新: [darkorange3]沒有需要更新[/]");
                    }
                };

                Task.Run(() => AutoUpdater.Start("https://raw.githubusercontent.com/konnokai/OBSNowPlayingOverlay/refs/heads/master/Docs/Update.xml"));
            }
            catch (Exception ex)
            {
                AnsiConsole.WriteException(ex);
            }

            if (File.Exists("Config.json"))
            {
                try
                {
                    _config = JsonConvert.DeserializeObject<Config>(File.ReadAllText("Config.json"))!;
                }
                catch (Exception ex)
                {
                    try
                    {
                        File.Delete("Config.json");
                    }
                    catch { }

                    AnsiConsole.MarkupLine("[red]設定檔載入失敗，將使用預設設定[/]");
                    AnsiConsole.WriteException(ex);
                }
            }

            if (File.Exists("TwitchBotConfig.json"))
            {
                try
                {
                    TwitchBotConfig = JsonConvert.DeserializeObject<TwitchBotConfig>(File.ReadAllText("TwitchBotConfig.json"))!;
                }
                catch (Exception ex)
                {
                    try
                    {
                        File.Delete("TwitchBotConfig.json");
                    }
                    catch { }

                    AnsiConsole.MarkupLine("[red]TwitchBotConfig 設定檔載入失敗，請重新登入 Twitch[/]");
                    AnsiConsole.WriteException(ex);
                }
            }

            try
            {
                _wsServer = new WebSocketServer(IPAddress.Loopback, 52998);
                _wsServer.AddWebSocketService<NowPlaying>("/");
                _wsServer.Start();
                AnsiConsole.MarkupLine("伺服器狀態: [green]已啟動![/]");
            }
            catch (System.Net.Sockets.SocketException ex) when (ex.SocketErrorCode == System.Net.Sockets.SocketError.AddressAlreadyInUse)
            {
                AnsiConsole.MarkupLine("伺服器狀態: [red]啟動失敗，請確認是否有其他應用程式使用 TCP 52998 Port[/]");
                AnsiConsole.WriteException(ex);
                return;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine("伺服器狀態: [red]啟動失敗，未知的錯誤，請向開發者詢問[/]");
                AnsiConsole.WriteException(ex);
                return;
            }

            _mainWindow.Show();

            chkb_LoadSystemFonts.Dispatcher.Invoke(() =>
            {
                chkb_LoadSystemFonts.IsChecked = _config.IsLoadSystemFonts;
            });

            chkb_UseCoverImageAsBackground.Dispatcher.Invoke(() =>
            {
                chkb_UseCoverImageAsBackground.IsChecked = _config.IsUseCoverImageAsBackground;
            });

            chkb_TopMost.Dispatcher.Invoke(() =>
            {
                chkb_TopMost.IsChecked = _config.IsTopmost;
            });

            ReloadFonts(_config.IsLoadSystemFonts);
            _mainWindow.SetUseCoverImageAsBackground(_config.IsUseCoverImageAsBackground);
            _mainWindow.SetTopmost(_config.IsTopmost);

            cb_FontChooser.Dispatcher.Invoke(() =>
            {
                cb_FontChooser.ItemsSource = _fontFamilies;
                cb_FontChooser.SelectedIndex = _config.SeletedFontIndex;
            });

            num_MainWindowWidth.Dispatcher.Invoke(() =>
            {
                num_MainWindowWidth.Value = _config.MainWindowWidth;
            });

            num_MarqueeSpeed.Dispatcher.Invoke(() =>
            {
                num_MarqueeSpeed.Value = _config.MarqueeSpeed;
            });

            MainWindow.IsUseBlackAsTitleColor = _config.OBSUseBlackAsTitleColor;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _mainWindow.Close();
            _wsServer?.Stop();
            _wsServer = null;

            if (TwitchBot.Bot.IsConnect != null && TwitchBot.Bot.IsConnect.Value)
            {
                TwitchBot.Bot.StopBot();
            }

            if (OBSWebSocket.Client.IsConnected)
            {
                OBSWebSocket.Client.Disconnect();
            }

            try
            {
                File.WriteAllText("Config.json", JsonConvert.SerializeObject(_config, Formatting.Indented));
            }
            catch (Exception)
            {
            }

            try
            {
                File.WriteAllText("TwitchBotConfig.json", JsonConvert.SerializeObject(TwitchBotConfig, Formatting.Indented));
            }
            catch (Exception)
            {
            }
        }

        private void cb_FontChooser_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            FontFamily? fontFamily = null;
            cb_FontChooser.Dispatcher.Invoke(() =>
            {
                if (cb_FontChooser.SelectedItem == null)
                    return;

                _config.SeletedFontIndex = cb_FontChooser.SelectedIndex;
                fontFamily = ((KeyValuePair<string, FontFamily>)cb_FontChooser.SelectedItem).Value;
            });

            if (fontFamily == null)
                return;

            _mainWindow.SetFont(fontFamily);
        }

        private void chkb_LoadSystemFonts_Click(object sender, RoutedEventArgs e)
        {
            bool isLoadSystemFonts = chkb_LoadSystemFonts.IsChecked.HasValue && chkb_LoadSystemFonts.IsChecked.Value;
            _config.IsLoadSystemFonts = isLoadSystemFonts;

            ReloadFonts(isLoadSystemFonts);
        }

        private void ReloadFonts(bool isLoadSystemFonts = false)
        {
            cb_FontChooser.Dispatcher.Invoke(() =>
            {
                cb_FontChooser.SelectedIndex = -1;
            });

            _fontFamilies.Clear();

            if (Directory.Exists("Fonts"))
            {
                foreach (var item in Directory.EnumerateFiles("Fonts", "*.*", SearchOption.TopDirectoryOnly).Where((x) => x.EndsWith(".ttf") | x.EndsWith(".otf")))
                {
                    try
                    {
                        // https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.fontfamily
                        var fontFamilies = Fonts.GetFontFamilies($"file:///{AppContext.BaseDirectory.Replace("\\", "/")}{item.Replace("\\", "/")}");
                        var fontName = fontFamilies.First().FamilyNames.First().Value;
                        if (fontFamilies.First().FamilyNames.Any((x) => x.Key == XmlLanguage.GetLanguage(CultureInfo.CurrentUICulture.Name)))
                        {
                            fontName = fontFamilies.First().FamilyNames[XmlLanguage.GetLanguage(CultureInfo.CurrentUICulture.Name)];
                        }

                        _fontFamilies.Add(new KeyValuePair<string, FontFamily>(fontName, fontFamilies.First()));

                        AnsiConsole.MarkupLineInterpolated($"載入自訂字型: [green]{fontName}[/]");
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLineInterpolated($"[red]字型載入失敗: {Path.GetFileName(item)}[/]");
                        AnsiConsole.WriteException(ex);
                    }
                }
            }

            if (isLoadSystemFonts)
            {
                foreach (var item in Fonts.SystemFontFamilies)
                {
                    _fontFamilies.Add(new KeyValuePair<string, FontFamily>(item.FamilyNames.First().Value, item));
                }

                AnsiConsole.MarkupLineInterpolated($"載入系統字型: [green]{Fonts.SystemFontFamilies.Count}[/] 個");
            }
        }

        private void num_MainWindowWidth_ValueChanged(object sender, HandyControl.Data.FunctionEventArgs<double> e)
        {
            if (int.TryParse(e.Info.ToString(), out int width))
            {
                _config.MainWindowWidth = width;

                _mainWindow.SetWindowWidth(width);
            }
        }

        private void num_MarqueeSpeed_ValueChanged(object sender, HandyControl.Data.FunctionEventArgs<double> e)
        {
            if (int.TryParse(e.Info.ToString(), out int speed))
            {
                _config.MarqueeSpeed = speed;

                _mainWindow.SetMarqueeSpeed(speed);
            }
        }

        private void chkb_UseCoverImageAsBackground_Click(object sender, RoutedEventArgs e)
        {
            bool isUseCoverImageAsBackground = chkb_UseCoverImageAsBackground.IsChecked.HasValue && chkb_UseCoverImageAsBackground.IsChecked.Value;
            _config.IsUseCoverImageAsBackground = isUseCoverImageAsBackground;

            _mainWindow.SetUseCoverImageAsBackground(isUseCoverImageAsBackground);
        }

        private void chkb_TopMost_Click(object sender, RoutedEventArgs e)
        {
            bool isTopmost = chkb_TopMost.IsChecked.HasValue && chkb_TopMost.IsChecked.Value;
            _config.IsTopmost = isTopmost;

            _mainWindow.SetTopmost(isTopmost);
        }

        private void btn_TwitchBotSetting_Click(object sender, RoutedEventArgs e)
        {
            // 先關閉主視窗的置頂，避免 TwitchBotWindow 被遮蔽
            _mainWindow.SetTopmost(false);

            var twitchBotWindow = new TwitchBotWindow(TwitchBotConfig);
            twitchBotWindow.ShowDialog();

            _mainWindow.SetTopmost(_config.IsTopmost);
        }

        private void btn_OBSWebSocketSetting_Click(object sender, RoutedEventArgs e)
        {
            // 先關閉主視窗的置頂，避免 OBSWebSocketWindow 被遮蔽
            _mainWindow.SetTopmost(false);

            var webSocketWindow = new OBSWebSocketWindow(_config);
            webSocketWindow.ShowDialog();

            _mainWindow.SetTopmost(_config.IsTopmost);
        }
    }
}
