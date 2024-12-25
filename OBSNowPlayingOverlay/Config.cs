namespace OBSNowPlayingOverlay
{
    public class Config
    {
        public bool IsLoadSystemFonts { get; set; } = false;
        public bool IsUseCoverImageAsBackground { get; set; } = false;
        public bool IsTopmost { get; set; } = false;
        public int SeletedFontIndex { get; set; } = 1;
        public int MainWindowWidth { get; set; } = 400;
        public int MarqueeSpeed { get; set; } = 50;
        public string OBSWebSocketIP { get; set; } = "ws://127.0.0.1";
        public string OBSWebSocketPort { get; set; } = "4455";
        public string OBSWebSocketPassword { get; set; } = string.Empty;
        public bool OBSUseBlackAsTitleColor { get; set; } = false;
    }
}
