# KK_RandomCardPicker

Koikatsu 的 CharaStudio 插件。在 [BrowserFolders](https://github.com/ManlyMarco/Illusion_BrowserFolders) 的資料夾視窗加一顆 **Random** 按鈕，從目前選取的資料夾隨機挑一張卡：

- **角色卡**：隨機選中一張，可繼續按 Random 換卡，滿意後自己按 Load / Change 套用。
- **場景卡**：隨機選中一張並跳出 Load / Import / Delete 面板，不喜歡可繼續按 Random。

## 場景時長

- 讀取 Timeline 插件存在場景卡裡的動畫長度(不需載入場景)。
- 載入面板的預覽圖，以及清單裡每張縮圖的右下角，都顯示該場景的時長，沒有 Timeline 的顯示 `No timeline`。
- F1 設定 `Scene > Random minimum duration (seconds)`：Random 只挑時長不小於此值的場景，0 為不限。
- F1 設定 `Scene > List minimum duration (seconds)`：載入清單只列出時長不小於此值的場景，0 為不限，重新整理清單後生效（重開清單或按 Refresh thumbnails）。
- Random 按鈕上顯示「符合條件張數/範圍內總張數」，背景掃描中會帶 `...`。
- 掃描結果存在 `BepInEx/cache/KK_RandomCardPicker.durations.tsv`，第一次掃描整個資料夾需要一些時間，之後只會讀新增或修改過的卡。

## 最愛

- 資料夾樹最上方（與 scene / female / male 同層）多一個 **Favorite** 節點，點進去就是最愛清單，Random 也能在裡面抽。
- 卡片預覽圖右上角的星星：點一下把目前選中的卡片複製到 `UserData/Favorite/<scene|female|male>`（原檔不動），再點一次取消；實心星星表示已在最愛。
- 在 Favorite 資料夾裡點星星會刪除那份副本，星星變灰但卡片留在清單，再點一次可還原，下次重新載入清單才會消失。

## 歷史

- 資料夾樹的 **History** 節點在 Favorite 下方，列出最近使用的場景或角色卡。
- 場景按 Load / Import、角色按 Load / Change 時，會把該卡複製到 `UserData/History/<scene|female|male>` 並把修改時間設為現在，清單依日期排序時最新的在最上面（原檔不動）。重複使用同一張只會移到最前面。
- F1 設定 `History > Maximum cards per type`：每種類型最多保留幾張，超過就刪最舊的，預設 50，0 為不限。
- 角色清單進入 History 時會自動改成依時間排序（最新在上），場景清單本來就是。
- 在 History 裡也可以點星星加入最愛，Random 也能在裡面抽。
- 新紀錄要重新整理清單（重開清單或按 Refresh thumbnails）才會出現。

## 最常使用

- 資料夾樹的 **Most Used** 節點在 History 下方，依使用次數由多到少列出卡片，次數相同的最近用過的在前。
- 使用次數的算法與 History 相同（Load / Import / Change 各算一次），以檔名為單位，存在 `BepInEx/cache/KK_RandomCardPicker.usage.tsv`。不同資料夾裡同檔名的卡會合併計算。
- 卡片複製到 `UserData/MostUsed/<scene|female|male>`，用修改時間表示名次，清單依日期排序時次數多的在最上面，角色清單進入時會自動切成依時間排序。
- F1 設定 `History > Maximum most used cards per type`：每種類型最多保留幾張，預設 50，0 為不限。

## 需求

- Koikatsu + BepInEx 5
- BrowserFolders（`marco.FolderBrowser`，已測試 4.0.1 的方法簽名）

## 編譯

編譯時需要遊戲目錄來引用 DLL，把 `src/GameDir.props.example` 複製成 `src/GameDir.props` 並改成自己的遊戲路徑（不進版控）。也可以設定環境變數 `KK_GAME_DIR`，或用 `-p:GameDir=` 指定：

```
dotnet build src -c Release -p:GameDir="D:\Koikatsu"
```

輸出 `src/bin/Release/net35/KK_RandomCardPicker.dll`，放進遊戲的 `BepInEx/plugins`。
