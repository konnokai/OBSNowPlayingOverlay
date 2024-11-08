﻿using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using Spectre.Console;
using System.Collections.Concurrent;
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
                catch (Exception ex)
                {
                    AnsiConsole.WriteException(ex);
                }
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
            if (latestTitle != nowPlayingJson.Title)
            {
                latestTitle = nowPlayingJson.Title;
                AnsiConsole.MarkupLineInterpolated($"歌曲切換: [green]{nowPlayingJson.Artists.FirstOrDefault() ?? "無"} - {nowPlayingJson.Title}[/]");
                AnsiConsole.MarkupLineInterpolated($"歌曲連結: [green]{nowPlayingJson.SongLink}[/]");

                rb_Title.Dispatcher.Invoke(() => { rb_Title.Content = nowPlayingJson.Title; });
                rb_Subtitle.Dispatcher.Invoke(() => { rb_Subtitle.Content = nowPlayingJson.Artists.FirstOrDefault() ?? "無"; });

                try
                {
                    AnsiConsole.MarkupLineInterpolated($"開始下載封面: [green]{nowPlayingJson.Cover}[/]");

                    using var imageStream = await _httpClient.GetStreamAsync(nowPlayingJson.Cover);
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
                        // 直接將圖片模糊化
                        image.Mutate(x => x.GaussianBlur(12));

                        bg.Dispatcher.Invoke(() =>
                        {
                            bg.Background = new ImageBrush()
                            {
                                ImageSource = GetBMP(image),
                                Stretch = Stretch.UniformToFill
                            };
                        });
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
                    }
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine("[red]封面圖下載失敗，可能是找不到圖片[/]");
                    AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
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
                }
            }

            if (double.IsNormal(nowPlayingJson.Progress) && double.IsNormal(nowPlayingJson.Duration))
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