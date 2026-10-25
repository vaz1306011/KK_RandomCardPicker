# KK_RandomCardPicker

Koikatsu 的 CharaStudio 插件。在 [BrowserFolders](https://github.com/ManlyMarco/Illusion_BrowserFolders) 的資料夾視窗加一顆 **Random** 按鈕，從目前選取的資料夾隨機挑一張卡：

- **角色卡**：隨機選中一張，可繼續按 Random 換卡，滿意後自己按 Load / Change 套用。
- **場景卡**：隨機選中一張並跳出 Load / Import / Delete 面板，不喜歡可繼續按 Random。

## 需求

- Koikatsu + BepInEx 5
- BrowserFolders（`marco.FolderBrowser`，已測試 4.0.1 的方法簽名）

## 編譯

編譯時需要遊戲目錄來引用 DLL，請設定環境變數 `KK_GAME_DIR`，或用 `-p:GameDir=` 指定：

```
dotnet build src -c Release -p:GameDir="D:\Koikatsu"
```

輸出 `src/bin/Release/net35/KK_RandomCardPicker.dll`，放進遊戲的 `BepInEx/plugins`。
