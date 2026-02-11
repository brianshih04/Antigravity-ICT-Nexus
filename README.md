# Antigravity ICT Nexus (NinjaScript 版) 實作導覽

## 概述
本文件說明 **Antigravity ICT Nexus** 指標在 NinjaTrader 8 上的實作細節。此指標旨在自動化識別 ICT 概念 (市場結構、FVG)，並採用高效能的 Direct2D 繪圖引擎。

## 實作細節

### 1. 檔案結構
- **檔案名稱**: `AntigravityICTNexus.cs`
- **路徑**: `C:\Users\AV00021\.gemini\antigravity\scratch\AntigravityICTNexus.cs`
- **命名空間**: `NinjaTrader.NinjaScript.Indicators`

### 2. 核心功能
- **擺動點 (Swing Points)**: 根據使用者設定的 `SwingStrength` (擺動強度) 偵測價格的高低點 (Swing High/Low)。
- **合理價值缺口 (FVG)**:
    - 識別 **看漲 FVG** (當根低點 > 前兩根高點)。
    - 識別 **看跌 FVG** (當根高點 < 前兩根低點)。
    - 追蹤 **緩解狀態 (Mitigation)**：當價格回補缺口時標記為已緩解。
    - 使用 **Direct2D** 矩形繪製未緩解的 FVG。
- **Direct2D 高效能繪圖**:
    - 使用 `OnRender` 與 `SharpDX` 進行優化渲染。
    - 繞過標準的 `Draw.Rectangle` 方法，以應對大量與高頻率的圖形更新。
    - 支援自定義顏色與透明度 (Opacity)。

## 邏輯說明

### 市場結構 (Swing Points)
指標會回溯檢查 `SwingStrength` 數量的 K 線。如果某根 K 線的高點高於其左邊與右邊各 `SwingStrength` 根 K 線的高點，則確認為 Swing High。
```csharp
if (High[checkIndex] <= High[checkIndex + i] || High[checkIndex] <= High[checkIndex - i])
    isSwingHigh = false;
```

### FVG 偵測
FVG 在 K 線收盤時 (3 根 K 線的型態) 進行確認。
- **看漲 (Bullish)**: `Low[0] > High[2]`
- **看跌 (Bearish)**: `High[0] < Low[2]`
- **緩解 (Mitigation)**: 如果未來的價格進入 FVG 區域，該 FVG 將被標記為已緩解 (IsMitigated = true)，目前設定為不再繪製以保持圖面整潔。

## 安裝與使用說明
1. **複製程式碼**: 複製 `AntigravityICTNexus.cs` 的完整內容。
2. **NinjaTrader 8**:
    - 開啟 NinjaScript Editor。
    - 建立一個新的指標 (New Indicator)，命名為 `AntigravityICTNexus`。
    - 將程式碼貼上並覆蓋預設內容。
    - 編譯 (按 F5)。
3. **套用到圖表**:
    - 在任何圖表上加入 "Antigravity ICT Nexus" 指標。
    - 可調整參數：`Swing Strength` (預設: 5) 與 `FVG Opacity` (透明度)。

## 未來擴充規劃 (多時框 MTF)
目前的架構已預留多時框 (MTF) 的擴充空間。若要啟用 MTF：
1. 在 `State.Configure` 中取消註解並加入 `AddDataSeries`。
2. 在 `OnBarUpdate` 中加入 `BarsInProgress` 的檢查邏輯。
