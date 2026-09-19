# Idle Lineage Web (放置天堂 網頁版)

本專案為《放置天堂》（Idle Lineage）的 Godot Web 網頁版移植與動態串流架構工程。

---

## 核心設計理念與架構

源專案（`C:\Users\Mh\Desktop\LIN`）完整素材高達 **14.9 GB**。由於現代瀏覽器單次下載與記憶體容量限制（通常無法承受超過 100MB+ 的初始下載及 2GB 以上記憶體），直接將所有素材打包進 Web PCK 必然導致瀏覽器崩潰或白屏。

本專案採用大型 Web MMORPG 級別的**「四層階梯式動態串流架構」**：

1. **Web 微核心主體 (<35MB)**：
   - 僅包含 Godot WebAssembly 引擎、核心戰鬥與數值系統、UI 介面骨架、選角創角、基礎字型與圖示。
   - 玩家開啟網址 **3 秒內進入遊戲選角與登入介面**。
2. **地圖切塊動態串流 (On-Demand Map Streaming)**：
   - 258 張地圖（佔 12.4GB）不打入主包。
   - 玩家身處特定地圖時（如「說話之島村莊」），僅非同步加載該地圖可見視野內的 WebP 切塊（每次僅幾 MB）。
   - 離開地圖或切換視角時，自動卸載舊切片，記憶體恆定在 300MB 以內。
3. **精靈動畫深度濃縮 (WebP Atlas Streaming)**：
   - 將龐大的 PNG 圖集批次濃縮為 WebP 格式（節省 60%~75% 體積）。
   - 根據當前地圖的怪物出沒表，隨選非同步下載怪物的動作圖集。
4. **瀏覽器本地快取 (IndexedDB / CacheStorage)**：
   - 玩家造訪過的地圖與戰鬥素材會自動保存在瀏覽器本地快取中，二次進入時零流量消耗。

---

## 遊戲模式如何保證 100% 一模一樣？

- **數值與邏輯完全繼承**：怪物的 HP/MP、攻擊力防禦力、掉落表（`l1j-drops.json`）、技能傷害公式、裝備衝裝強化機制、NPC 拓撲對話、門與阻擋地形判定（`terrain-tiles.bin`）100% 保留。
- **存檔無損兼容**：支援瀏覽器 LocalStorage 本地自動保存，並完整支援原版的剪貼簿導出/導入代碼，隨時可在 Web 版與桌面客戶端互通存檔！

---

## 本地開發與調試

### 1. 編譯 C# 專案
```powershell
cd C:\Users\Mh\Desktop\lin-web
dotnet build IdleLineageWeb.csproj
```

### 2. 啟動支援 COOP/COEP 安全標頭的本地測試伺服器
```powershell
python tools/serve_web.py 8080
```
瀏覽器開啟：`http://localhost:8080`

### 3. 資產濃縮與發布管線
```powershell
python tools/compress_assets.py
```

---

## GitHub 自動部署 (GitHub Pages)

本專案已配置 GitHub Actions 工作流（`.github/workflows/deploy.yml`）。
當程式碼推送至 `main` 分支時，將自動執行 Web 構建並發布至 GitHub Pages：
`https://maplestorycandy.github.io/linweb/`
