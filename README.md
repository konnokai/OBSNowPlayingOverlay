# OBSNowPlayingOverlay - 正在播放

![MainWindows](Docs/MainWindow.gif)

![MainWindows2](Docs/MainWindow2.png)

一個可以顯示播放狀態的小工具

起因是因為我推 ([998rrr](https://www.twitch.tv/998rrr)) 的 NowPlaying 軟體出現問題，正好拿來練手寫個工具看看

# 特色

- 顯示播放狀態
- 緩慢新增的可自訂化介面
- 自動更新 (v1.0.1 新增)
- 自動監測現在是否為直播影片 (v1.0.2 新增)
- 自動根據背景顏色深淺來切換字體顏色避免看不清楚的問題 (v1.0.6 新增)
- Twitch Bot 指令支援 (v1.1.0 新增)
- ~~還有些我抓不到的 Bug~~

# 支援平台

- YouTube (包含 YouTube Music)
- SoundCloud
- Spotify
- bilibili (v1.0.5 新增，需搭配瀏覽器插件 v1.0.2.1)
- OBS 媒體來源 (v1.1.4 新增，需搭配 [obs-websocket](https://github.com/obsproject/obs-websocket)) (通常情況下已被新版 OBS 包含在內，除非你的 OBS 版本低於 28) (由 [澪Rei](https://x.com/Reirei_Neon) 提議)

# 如何使用

0. 確保你的系統位元是 x64 (現在的電腦應該都會是 x64) ([如何查看系統位元](https://support.lenovo.com/tw/zh/solutions/ht117173))
1. 安裝 [瀏覽器插件](https://chromewebstore.google.com/detail/obs-%E6%AD%A3%E5%9C%A8%E6%92%AD%E6%94%BE/bbaajjiddghleiifnnhagkgjfihnkphe) (剛安裝完插件的話需要重整網頁或是重開瀏覽器來讓插件載入)
2. 安裝 [.NET 6 Desktop Runtime](https://dotnet.microsoft.com/zh-tw/download/dotnet/thank-you/runtime-desktop-6.0.36-windows-x64-installer)
3. [點我下載](https://github.com/konnokai/OBSNowPlayingOverlay/releases/latest/download/OBSNowPlayingOverlay.zip) 最新版的 `OBSNowPlayingOverlay.zip` 壓縮包並解壓縮
4. 確保瀏覽器插件已安裝以及重整網頁，並打開 `OBSNowPlayingOverlay.exe`
5. 設定想要的字型以及視窗寬度
6. 打開 OBS，新增 `視窗擷取` 來源，並按照下方圖片設定

![OBSProperty](Docs/OBSProperty.png)

(擷取方式一定要改成 `Windows 10`，視窗匹配優先度一定要是 `視窗標題必須相符`，不然重開程式都要重新設定一次屬性)

7. 開始播放任一支援的平台音樂，若正常的話即會出現正在播放的音樂狀態

OBS 的畫面應該會長這樣

![OBSDone](Docs/OBSDone.png)

# 如何新增字型

![HowToAddFont](Docs/HowToAddFont.png)

有兩種方式

1. 直接把字型安裝到系統內，之後到設定視窗勾選 `載入系統安裝字型`
2. 將 ttf 或 otf 字型檔案丟到程式的 `Fonts` 資料夾，然後重開程式讓字型載入即可

弄完之後記得要選擇想用的字型

# 如何關閉程式

![CloseProgram](Docs/CloseProgram.png)

對著設定視窗點關閉，或是到工具列對兩個圖形視窗關閉都行

直接關小黑窗也能關閉，但怕資源釋放有問題，盡量避免用此方式來關

# 已知問題

- 關閉程式時有可能會遇到 InvalidOperationException，但因程式已關閉故無法正常拋出例外，導致整個程式出現卡死的死循環，這種情況下只能透過工作管理員強制關閉，目前尚未發現該如何避免此狀況

# 出現問題該怎麼處理

<details>
<summary>程式不開或是打開來馬上閃退</summary>
  
- 記得裝 [.NET 6 Desktop Runtime](https://dotnet.microsoft.com/zh-tw/download/dotnet/thank-you/runtime-desktop-6.0.36-windows-x64-installer)

</details>
<details>
<summary>程式打開了但播放影片沒有效果</summary>
  
1. 先去安裝 [瀏覽器插件](https://chromewebstore.google.com/detail/obs-%E6%AD%A3%E5%9C%A8%E6%92%AD%E6%94%BE/bbaajjiddghleiifnnhagkgjfihnkphe)，或是去看看擴充插件有沒有被關閉
2. 把瀏覽器關掉重開
3. 把程式打開來
4. 找個影片播放
5. 應該要能正常執行

</details>
<details>
<summary>OBS 新增視窗來源但擷取出來的是黑畫面</summary>
  
- 擷取方式一定要改成 `Windows 10`，這算是 WPF 自身的問題，沒有其他解法

</details>
<details>
<summary>我有其他問題但我找不到解法</summary>
  
- 先問 Google 或你身邊懂電腦的人，都沒辦法再來問我或是發 Issus

</details>

# 關於 & 參考專案

- [Now Playing - OBS](https://gitlab.com/tizhproger/now-playing-obs)
- [obs-websocket-dotnet](https://github.com/konnokai/obs-websocket-dotnet) (自編譯版本，原專案已停止更新)
- [Vinyl icons](https://www.flaticon.com/free-icons/vinyl) created by Those Icons - Flaticon
- [Lp icons](https://www.flaticon.com/free-icons/lp) created by Alfredo Hernandez - Flaticon
- [Pause icons](https://www.flaticon.com/free-icons/pause) created by Debi Alpa Nugraha - Flaticon
- [cjkfonts 全瀨體](https://cjkfonts.io/blog/cjkfonts_allseto)
- [貓啃什錦黑 繁體中文版](https://github.com/Skr-ZERO/MaokenAssortedSans-TC)
- [辰宇落雁體](https://github.com/Chenyu-otf/chenyuluoyan_thin)
