using ImageMagick;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using Spectre.Console;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using Image = SixLabors.ImageSharp.Image;
using ImageBrush = System.Windows.Media.ImageBrush;
using Rectangle = SixLabors.ImageSharp.Rectangle;

namespace OBSNowPlayingOverlay
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static BlockingCollection<NowPlayingJson> MsgQueue { get; } = new();
        public static string LatestWebSocketGuid { get; set; } = "";
        public static string NowPlayingTitle { get; private set; } = "";
        public static string NowPlayingUrl { get; private set; } = "";
        public static bool IsUseBlackAsTitleColor { get; set; } = false;

        private readonly HttpClient _httpClient;
        private string latestTitle = "";
        private bool isUseCoverImageAsBackground = false;

        public MainWindow()
        {
            InitializeComponent();

            _httpClient = new(new HttpClientHandler()
            {
                AllowAutoRedirect = false
            });

            Task.Run(async () =>
            {
                try
                {
                    while (!MsgQueue.IsCompleted)
                    {
                        NowPlayingJson data;

                        try
                        {
                            data = MsgQueue.Take();
                        }
                        catch (InvalidOperationException)
                        {
                            break;
                        }

                        await UpdateNowPlayingDataAsync(data);
                    }
                }
                catch (Exception) { }
            });
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            MsgQueue.CompleteAdding();
        }

        private void grid_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        public async Task UpdateNowPlayingDataAsync(NowPlayingJson nowPlayingJson)
        {
            // 如果 LatestWebSocketGuid 為空，則代表有發送端已關閉，以最新接收到的發送端來更新狀態
            if (LatestWebSocketGuid == "")
            {
                AnsiConsole.MarkupLineInterpolated($"最後的 Guid 為空，改由此 Guid 繼續更新狀態: [purple4_1]{nowPlayingJson.Guid}[/]");
                LatestWebSocketGuid = nowPlayingJson.Guid;
            }

            // 檢測發送端的 Guid 是否為最新的，避免重複更新播放狀態
            if (LatestWebSocketGuid != nowPlayingJson.Guid)
                return;

            // 檢測標題是否有變更
            if (latestTitle != nowPlayingJson.Title)
            {
                latestTitle = nowPlayingJson.Title;
                NowPlayingTitle = nowPlayingJson.Title;
                NowPlayingUrl = nowPlayingJson.SongLink;
                var artists = nowPlayingJson.Artists != null ? string.Join(", ", nowPlayingJson.Artists) : "無";
                double grayLevel = 0d;

                AnsiConsole.MarkupLineInterpolated($"歌曲切換: [green]{artists} - {nowPlayingJson.Title}[/]");
                AnsiConsole.MarkupLineInterpolated($"歌曲連結: [green]{nowPlayingJson.SongLink}[/]");

                rb_Title.Dispatcher.Invoke(() => { rb_Title.Content = nowPlayingJson.Title; });
                rb_Subtitle.Dispatcher.Invoke(() => { rb_Subtitle.Content = artists; });

                if (!string.IsNullOrEmpty(nowPlayingJson.Cover) && nowPlayingJson.Platform != "obs")
                {
                    try
                    {
                        AnsiConsole.MarkupLineInterpolated($"下載封面: [green]{nowPlayingJson.Cover}[/]");

                        // 因 YouTube 開始將圖片轉換成 avif，故先使用 MagickImage 讀取圖片並轉換成 jpeg
                        // https://github.com/dlemstra/Magick.NET/blob/main/docs/ConvertImage.md
                        using var magickImage = new MagickImage(await _httpClient.GetStreamAsync(nowPlayingJson.Cover));
                        magickImage.Format = MagickFormat.Jpeg;

                        using var imageStream = new MemoryStream(magickImage.ToByteArray());
                        using var image = await Image.LoadAsync<Rgba32>(imageStream);
                        using var coverImage = image.Clone();

                        // 若圖片非正方形才進行裁切
                        if (coverImage.Width != coverImage.Height)
                        {
                            // 將圖片從正中間裁切
                            // 若圖片的寬比較大，就由寬來裁切
                            if (coverImage.Width > coverImage.Height)
                            {
                                int x = coverImage.Width / 2 - coverImage.Height / 2;
                                coverImage.Mutate(i => i
                                    .Crop(new Rectangle(x, 0, coverImage.Height, coverImage.Height)));
                            }
                            else
                            {
                                int y = coverImage.Height / 2 - coverImage.Width / 2;
                                coverImage.Mutate(i => i
                                    .Crop(new Rectangle(0, y, coverImage.Width, coverImage.Width)));
                            }
                        }

                        // 設定封面圖片
                        img_Cover.Dispatcher.Invoke(() =>
                        {
                            img_Cover.Source = GetBMP(coverImage);
                        });

                        if (isUseCoverImageAsBackground)
                        {
                            // 將圖片模糊化
                            image.Mutate(x => x.GaussianBlur(12));

                            bg.Dispatcher.Invoke(() =>
                            {
                                bg.Background = new ImageBrush()
                                {
                                    ImageSource = GetBMP(image),
                                    Stretch = Stretch.UniformToFill
                                };
                            });

                            // 將圖片縮放，以提高計算效率，並灰階化
                            image.Mutate(x => x.Resize(128, 0).Grayscale());

                            // 計算縮放後圖片的平均亮度
                            double totalBrightness = 0;
                            int width = image.Width;
                            int height = image.Height;

                            for (int y = 0; y < height; y++)
                            {
                                for (int x = 0; x < width; x++)
                                {
                                    // 使用 indexer 獲取像素
                                    Rgba32 pixel = image[x, y];
                                    double brightness = (pixel.R + pixel.G + pixel.B) / 3.0;
                                    totalBrightness += brightness;
                                }
                            }

                            grayLevel = Math.Round((totalBrightness / (width * height)), 2);
                        }
                        else
                        {
                            // 取得圖片的主要顏色
                            // https://gist.github.com/JimBobSquarePants/12e0ef5d904d03110febea196cf1d6ee
                            image.Mutate(x => x
                                // Scale the image down preserving the aspect ratio. This will speed up quantization.
                                // We use nearest neighbor as it will be the fastest approach.
                                .Resize(new ResizeOptions() { Sampler = KnownResamplers.NearestNeighbor, Size = new SixLabors.ImageSharp.Size(128, 0) })
                                // Reduce the color palette to 1 color without dithering.
                                .Quantize(new OctreeQuantizer(new QuantizerOptions { MaxColors = 1 })));

                            // 設定背景顏色
                            var color = image[0, 0];
                            bg.Dispatcher.Invoke(() =>
                            {
                                bg.Background = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
                            });

                            grayLevel = Math.Round((color.R + color.G + color.B) / 3d, 2);
                        }

                        AnsiConsole.MarkupLineInterpolated($"封面灰階等級: [green]{grayLevel}[/]");

                        // 取得圖片是否為亮色系
                        var isLightBackground = grayLevel >= 128;

                        // SolidColorBrush 不可在與 DispatcherObject 不同的執行緒上建立，所以只能用這種很醜的方式來寫
                        // https://stackoverflow.com/a/8010725/15800522
                        rb_Title.Dispatcher.Invoke(() => { rb_Title.Foreground = new SolidColorBrush(isLightBackground ? Color.FromRgb(0, 0, 0) : Color.FromRgb(255, 255, 255)); });
                        rb_Subtitle.Dispatcher.Invoke(() => { rb_Subtitle.Foreground = new SolidColorBrush(isLightBackground ? Color.FromRgb(0, 0, 0) : Color.FromRgb(255, 255, 255)); });

                        // 根據灰階等級來設定字的顏色，但效果有點不好，暫時作罷
                        //var colorLevel = (byte)(255d - grayLevel);
                        //rb_Title.Dispatcher.Invoke(() => { rb_Title.Foreground = new SolidColorBrush(Color.FromRgb(colorLevel, colorLevel, colorLevel)); });
                        //rb_Subtitle.Dispatcher.Invoke(() => { rb_Subtitle.Foreground = new SolidColorBrush(Color.FromRgb(colorLevel, colorLevel, colorLevel)); });
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine("[red]封面圖下載失敗，可能是找不到圖片或圖片解析錯誤[/]");
                        AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
                    }
                }
                else
                {
                    // 設定封面圖片為空
                    img_Cover.Dispatcher.Invoke(() =>
                    {
                        img_Cover.Source = null;
                    });

                    // 設定背景顏色為透明
                    bg.Dispatcher.Invoke(() =>
                    {
                        bg.Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
                    });

                    // 設定標題字體顏色為黑色
                    rb_Title.Dispatcher.Invoke(() => { rb_Title.Foreground = IsUseBlackAsTitleColor ? new SolidColorBrush(Color.FromRgb(0, 0, 0)) : new SolidColorBrush(Color.FromRgb(255, 255, 255)); });
                }
            }

            Color progressColor = Color.FromRgb(255, 0, 51);
            if (!string.IsNullOrEmpty(nowPlayingJson.Platform))
            {
                switch (nowPlayingJson.Platform)
                {
                    case "youtube":
                    case "youtube_music":
                        progressColor = Color.FromRgb(255, 0, 51);
                        break;
                    case "soundcloud":
                        progressColor = Color.FromRgb(255, 85, 0);
                        break;
                    case "spotify":
                        progressColor = Color.FromRgb(30, 215, 96);
                        break;
                    case "bilibili":
                        progressColor = Color.FromRgb(0, 174, 236);
                        break;
                    case "obs":
                        progressColor = Color.FromRgb(42, 130, 218);
                        break;
                }
            }

            if (nowPlayingJson.IsLive) // 正在直播就直接把進度條切滿
            {
                pb_Process.Dispatcher.Invoke(() =>
                {
                    pb_Process.Foreground = new SolidColorBrush(progressColor);
                    pb_Process.Value = 100;
                });
            }
            else if (double.IsNormal(nowPlayingJson.Progress) && double.IsNormal(nowPlayingJson.Duration)) // 正常影片
            {
                pb_Process.Dispatcher.Invoke(() =>
                {
                    pb_Process.Foreground = new SolidColorBrush(progressColor);
                    pb_Process.Value = (nowPlayingJson.Progress / nowPlayingJson.Duration) * 100;
                });
            }

            if (!string.IsNullOrEmpty(nowPlayingJson.Status))
            {
                grid_Pause.Dispatcher.Invoke(() =>
                {
                    grid_Pause.Visibility = nowPlayingJson.Status == "playing" ? Visibility.Hidden : Visibility.Visible;
                });
            }
        }

        internal void SetFont(FontFamily fontFamily)
        {
            rb_Title.Dispatcher.Invoke(() => rb_Title.FontFamily = fontFamily);
            rb_Subtitle.Dispatcher.Invoke(() => rb_Subtitle.FontFamily = fontFamily);
        }

        internal void SetWindowWidth(int width)
        {
            if (width < 400 || width > 1000)
                return;

            Dispatcher.Invoke(() => { Width = width; });
        }

        internal void SetMarqueeSpeed(int speed)
        {
            if (speed < 25 || speed > 200)
                return;

            rb_Title.Dispatcher.Invoke(() =>
            {
                rb_Title.Speed = speed;
                rb_Title.OnApplyTemplate(); // 需要執行這類方法來觸發更新
            });
            rb_Subtitle.Dispatcher.Invoke(() =>
            {
                rb_Subtitle.Speed = speed;
                rb_Subtitle.OnApplyTemplate();
            });
        }

        internal void SetUseCoverImageAsBackground(bool useCoverImage)
        {
            isUseCoverImageAsBackground = useCoverImage;
            latestTitle = ""; // 清除最後保存的標題來觸發狀態更新
        }

        internal void SetTopmost(bool isTopmost)
        {
            Dispatcher.Invoke(() =>
            {
                Topmost = isTopmost;
            });
        }

        // https://github.com/SixLabors/ImageSharp/issues/531#issuecomment-2275170928
        private WriteableBitmap GetBMP(Image<Rgba32> _imgState)
        {
            var bmp = new WriteableBitmap(_imgState.Width, _imgState.Height, _imgState.Metadata.HorizontalResolution, _imgState.Metadata.VerticalResolution, PixelFormats.Bgra32, null);

            bmp.Lock();
            try
            {
                _imgState.ProcessPixelRows(accessor =>
                {
                    var backBuffer = bmp.BackBuffer;

                    for (var y = 0; y < _imgState.Height; y++)
                    {
                        Span<Rgba32> pixelRow = accessor.GetRowSpan(y);

                        for (var x = 0; x < _imgState.Width; x++)
                        {
                            var backBufferPos = backBuffer + (y * _imgState.Width + x) * 4;
                            var rgba = pixelRow[x];
                            var color = rgba.A << 24 | rgba.R << 16 | rgba.G << 8 | rgba.B;

                            System.Runtime.InteropServices.Marshal.WriteInt32(backBufferPos, color);
                        }
                    }
                });

                bmp.AddDirtyRect(new Int32Rect(0, 0, _imgState.Width, _imgState.Height));
            }
            finally
            {
                bmp.Unlock();
            }

            return bmp;
        }
    }
}