# KK_RandomCardPicker

Koikatsu 的 CharaStudio 插件。在 [BrowserFolders](https://github.com/ManlyMarco/Illusion_BrowserFolders) 的資料夾視窗加一顆 **Random** 按鈕，從目前選取的資料夾隨機挑一張卡：

- **角色卡**：隨機選中一張，可繼續按 Random 換卡，滿意後自己按 Load / Change 套用。
- **場景卡**：隨機選中一張並跳出 Load / Import / Delete 面板，不喜歡可繼續按 Random。

## 場景時長

- 讀取 Timeline 插件存在場景卡裡的動畫長度(不需載入場景)。
- 載入面板的預覽圖，以及清單裡每張縮圖的右下角，都顯示該場景的時長，沒有 Timeline 的顯示 `No timeline`。
- F1 設定 `Scene > Random minimum duration (seconds)`：Random 只挑時長不小於此值的場景，0 為不限。
- F1 設定 `Scene > List minimum duration (seconds)`：載入清單只列出時長不小於此值的場景，0 為不限，重新整理清單後生效（重開清單或按 Refresh thumbnails）。
- Random 按鈕旁顯示「符合條件張數/範圍內總張數」，背景掃描中會帶 `...`。
- 掃描結果存在 `BepInEx/cache/KK_RandomCardPicker.durations.tsv`，第一次掃描整個資料夾需要一些時間，之後只會讀新增或修改過的卡。

## 需求

- Koikatsu + BepInEx 5
- BrowserFolders（`marco.FolderBrowser`，已測試 4.0.1 的方法簽名）

## 編譯

編譯時需要遊戲目錄來引用 DLL，請設定環境變數 `KK_GAME_DIR`，或用 `-p:GameDir=` 指定：

```
dotnet build src -c Release -p:GameDir="D:\Koikatsu"
```

輸出 `src/bin/Release/net35/KK_RandomCardPicker.dll`，放進遊戲的 `BepInEx/plugins`。
