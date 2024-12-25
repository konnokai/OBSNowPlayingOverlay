using OBSWebsocketDotNet.Communication;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace OBSNowPlayingOverlay
{
    /// <summary>
    /// OBSWebSocketWindow.xaml 的互動邏輯
    /// </summary>
    public partial class OBSWebSocketWindow : Window
    {
        private readonly Config _config;

        public OBSWebSocketWindow(Config config)
        {
            _config = config;

            InitializeComponent();

            OBSWebSocket.Client.OBSWebsocket.Connected += Client_Connected;
            OBSWebSocket.Client.OBSWebsocket.Disconnected += Client_Disconnected;

            txt_OBSWebSocketIP.Dispatcher.Invoke(() => txt_OBSWebSocketIP.Text = _config.OBSWebSocketIP);
            txt_OBSWebSocketPort.Dispatcher.Invoke(() => txt_OBSWebSocketPort.Text = _config.OBSWebSocketPort);
            pb_OBSWebSocketPassword.Dispatcher.Invoke(() => pb_OBSWebSocketPassword.Password = _config.OBSWebSocketPassword);

            cb_UseBlackAsTitleColor.Dispatcher.Invoke(() => cb_UseBlackAsTitleColor.IsChecked = _config.OBSUseBlackAsTitleColor);

            CheckAndEnableUserControl();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            OBSWebSocket.Client.OBSWebsocket.Connected -= Client_Connected;
            OBSWebSocket.Client.OBSWebsocket.Disconnected -= Client_Disconnected;

            txt_OBSWebSocketIP.Dispatcher.Invoke(() => _config.OBSWebSocketIP = txt_OBSWebSocketIP.Text);
            txt_OBSWebSocketPort.Dispatcher.Invoke(() => _config.OBSWebSocketPort = txt_OBSWebSocketPort.Text);
            pb_OBSWebSocketPassword.Dispatcher.Invoke(() => _config.OBSWebSocketPassword = pb_OBSWebSocketPassword.Password);

            cb_UseBlackAsTitleColor.Dispatcher.Invoke(() => _config.OBSUseBlackAsTitleColor = cb_UseBlackAsTitleColor.IsChecked ?? false);
        }

        private void Client_Connected(object? sender, EventArgs e)
        {
            CheckAndEnableUserControl();
        }

        private void Client_Disconnected(object? sender, ObsDisconnectionInfo e)
        {
            CheckAndEnableUserControl();
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

        private void btn_ConnectOBSWebSocket_Click(object sender, RoutedEventArgs e)
        {
            if (!OBSWebSocket.Client.IsConnected)
            {
                btn_ConnectOBSWebSocket.Dispatcher.Invoke(() => btn_ConnectOBSWebSocket.IsEnabled = false);
                OBSWebSocket.Client.Connect($"{txt_OBSWebSocketIP.Text}:{txt_OBSWebSocketPort.Text}", pb_OBSWebSocketPassword.Password);
            }
        }

        private void btn_DisconnectOBSWebSocket_Click(object sender, RoutedEventArgs e)
        {
            if (OBSWebSocket.Client.IsConnected)
            {
                btn_DisconnectOBSWebSocket.Dispatcher.Invoke(() => btn_DisconnectOBSWebSocket.IsEnabled = false);
                OBSWebSocket.Client.Disconnect();
            }
        }

        private void CheckAndEnableUserControl()
        {
            if (OBSWebSocket.Client.IsConnected)
            {
                txt_OBSWebSocketIP.Dispatcher.Invoke(() => txt_OBSWebSocketIP.IsEnabled = false);
                txt_OBSWebSocketPort.Dispatcher.Invoke(() => txt_OBSWebSocketPort.IsEnabled = false);
                pb_OBSWebSocketPassword.Dispatcher.Invoke(() => pb_OBSWebSocketPassword.IsEnabled = false);

                btn_ConnectOBSWebSocket.Dispatcher.Invoke(() => btn_ConnectOBSWebSocket.IsEnabled = false);
                btn_DisconnectOBSWebSocket.Dispatcher.Invoke(() => btn_DisconnectOBSWebSocket.IsEnabled = true);
            }
            else
            {
                txt_OBSWebSocketIP.Dispatcher.Invoke(() => txt_OBSWebSocketIP.IsEnabled = true);
                txt_OBSWebSocketPort.Dispatcher.Invoke(() => txt_OBSWebSocketPort.IsEnabled = true);
                pb_OBSWebSocketPassword.Dispatcher.Invoke(() => pb_OBSWebSocketPassword.IsEnabled = true);

                btn_ConnectOBSWebSocket.Dispatcher.Invoke(() => btn_ConnectOBSWebSocket.IsEnabled = true);
                btn_DisconnectOBSWebSocket.Dispatcher.Invoke(() => btn_DisconnectOBSWebSocket.IsEnabled = false);
            }
        }

        private void cb_UseBlackAsTitleColor_Checked(object sender, RoutedEventArgs e)
        {
            MainWindow.IsUseBlackAsTitleColor = true;
        }

        private void cb_UseBlackAsTitleColor_Unchecked(object sender, RoutedEventArgs e)
        {
            MainWindow.IsUseBlackAsTitleColor = false;
        }
    }
}
