namespace OBSNowPlayingOverlay.WebSocketBehavior
{
    public class WebSocketClientInfo
    {
        public string Guid { get; set; }
        public string Platform { get; set; } = string.Empty;
        public DateTime LastActiveTime { get; set; }
        public DateTime CreatedTime { get; set; }
        public bool IsPlaying { get; set; }

        public WebSocketClientInfo(string guid)
        {
            Guid = guid;
            LastActiveTime = DateTime.Now;
            CreatedTime = DateTime.Now;
            IsPlaying = false;
        }

        /// <summary>
        /// 計算優先級，spotify-api 有絕對優先權，其他情況依照創建時間 FILO 方式排序
        /// 數字越小優先級越高
        /// </summary>
        public long GetPriority()
        {
            // Spotify API 有絕對優先權，設為最小值
            if (Platform == "spotify-api")
            {
                return long.MinValue;
            }

            // 其他情況以 FILO 方式排序，較晚加入的優先級越高（數字越小）
            // 使用負數的 FileTime，較晚的時間轉換後的負數會越小
            return -CreatedTime.ToFileTime();
        }
    }
}
