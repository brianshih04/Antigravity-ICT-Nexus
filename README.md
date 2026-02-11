# Antigravity ICT Nexus (NinjaScript Edition)

> 基於 NinjaTrader 8 的高效能 ICT 概念自動化指標，採用 SharpDX (Direct2D) 渲染引擎。

---

## ✨ 功能總覽

| 功能 | 說明 | 視覺化 |
|------|------|--------|
| **Swing High/Low** | 根據 SwingStrength 偵測價格擺動點 | `PH` / `PL` 文字標記 |
| **BOS (Break of Structure)** | 偵測結構突破 | 藍色水平線段 + `BOS` 標籤 |
| **FVG (Fair Value Gap)** | 三根 K 線形成的價格缺口 (含緩解追蹤) | 綠色 (多) / 紅色 (空) 半透明色塊 |
| **Kill Zones** | 倫敦開盤 (02-05) / 紐約開盤 (07-10) 時段 | 灰色背景帶 |
| **Order Blocks** | 造成 BOS 的起源 K 線 (含緩解追蹤) | 青色 (多) / 洋紅色 (空) 色塊 |

---

## 🏗️ 技術架構

```
NinjaTrader.NinjaScript.Indicators.AntigravityICTNexus
├── OnStateChange()    → 生命週期管理 & 預設參數
├── OnBarUpdate()      → 邏輯計算 (Swing / FVG / BOS / OB)
├── OnRender()         → Direct2D 高效能繪圖
└── Custom Classes     → ICT_FVG / SwingPoint / ICT_StructureBreak / ICT_OrderBlock
```

- **繪圖引擎**：SharpDX (Direct2D) — 避免 `Draw.Rectangle` 的效能瓶頸
- **計算模式**：`Calculate.OnBarClose` — 避免 Repaint 問題
- **資源管理**：所有 D2D Brush 於 `OnRender` 內建立/釋放，`TextFormat` 與 `Factory` 於 `State.Terminated` 釋放

---

## 📦 安裝方式

1. 開啟 NinjaTrader 8 → **NinjaScript Editor**
2. 新增指標 (New Indicator)，命名為 `AntigravityICTNexus`
3. 將 `AntigravityICTNexus.cs` 的內容貼上並覆蓋預設程式碼
4. 按 **F5** 編譯
5. 在任意圖表加入 **Antigravity ICT Nexus** 指標

---

## ⚙️ 參數設定

### 1. Market Structure
| 參數 | 預設值 | 說明 |
|------|--------|------|
| Swing Strength | 5 | 擺動點偵測的左右 K 線數量 |

### 2. FVG Settings
| 參數 | 預設值 | 說明 |
|------|--------|------|
| Show FVG | True | 顯示合理價值缺口 |
| Bullish FVG Color | LimeGreen | 多方 FVG 顏色 |
| Bearish FVG Color | Red | 空方 FVG 顏色 |
| FVG Opacity | 30 | 透明度 (0-100) |

### 3. Time Zones
| 參數 | 預設值 | 說明 |
|------|--------|------|
| Show Kill Zones | True | 顯示時段標記 |
| Kill Zone Color | Silver | 時段背景色 |
| Kill Zone Opacity | 10 | 透明度 (0-100) |

### 4. Order Blocks
| 參數 | 預設值 | 說明 |
|------|--------|------|
| Show Order Blocks | True | 顯示訂單塊 |
| Bullish OB Color | DarkCyan | 多方 OB 顏色 |
| Bearish OB Color | DarkMagenta | 空方 OB 顏色 |
| OB Opacity | 50 | 透明度 (0-100) |

---

## 🔧 邏輯說明

### Swing Points
檢查某根 K 線是否高於 (或低於) 其左右各 `SwingStrength` 根 K 線：
```csharp
for (int i = 1; i <= SwingStrength; i++)
{
    if (High[checkIndex] <= High[checkIndex + i] || High[checkIndex] <= High[checkIndex - i])
        isSwingHigh = false;
}
```

### BOS (Break of Structure)
當 `Close[0]` 突破前一個 Swing High/Low，且前一根 K 線的收盤價尚未突破該水位時，確認為 BOS。

### FVG Detection
- **Bullish FVG**: `Low[0] > High[2]` (當根低點 > 前兩根高點)
- **Bearish FVG**: `High[0] < Low[2]` (當根高點 < 前兩根低點)

### Order Block
當 BOS 發生時，回溯搜尋波段極值 K 線作為 OB 的起源點：
- **Bullish OB**: BOS 前方最低點的 K 線
- **Bearish OB**: BOS 前方最高點的 K 線

---

## 📋 系統需求

- NinjaTrader 8
- .NET Framework 4.8+
- SharpDX (NT8 內建)

---

## 📄 License

MIT License

---

## 🤝 Contributing

歡迎提交 Pull Request 或 Issue。若要擴充功能 (例如多時框 MTF)，請參考 `State.Configure` 中的 `AddDataSeries` 預留結構。
