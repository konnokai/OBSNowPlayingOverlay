using Spectre.Console;
using System.Windows;

namespace OBSNowPlayingOverlay
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            Console.Title = "正在播放 - 紀錄視窗";
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.CancelKeyPress += (sender, e) => { e.Cancel = true; };
            Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
        }

        private void Current_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            AnsiConsole.WriteException(e.Exception);
            e.Handled = true;
        }
    }
}
