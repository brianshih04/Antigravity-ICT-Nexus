#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
#endregion

//This namespace holds Indicators in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Indicators
{
	[Gui.Design.DisplayName("Antigravity ICT Nexus")]
	[Description("Automated ICT Concepts: Market Structure, FVG, and Order Blocks")]
	[Gui.Category("Antigravity")]
	public class AntigravityICTNexus : Indicator
	{
		#region Variables
		private List<ICT_FVG> fvgList;
		private List<SwingPoint> swingPoints;
		private List<ICT_StructureBreak> structureBreaks;
		private List<ICT_OrderBlock> orderBlocks;
		
		// Resources for Direct2D
		private SharpDX.DirectWrite.TextFormat textFormat;
		private SharpDX.DirectWrite.Factory dwFactory;
		
		private int lastHighBar = -1;
		private int lastLowBar = -1;
		#endregion

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Automated ICT Concepts: Market Structure, FVG, and Order Blocks";
				Name										= "AntigravityICTNexus";
				Calculate									= Calculate.OnBarClose;
				IsOverlay									= true;
				DisplayInDataBox							= true;
				DrawOnPricePanel							= true;
				DrawHorizontalGridLines						= true;
				DrawVerticalGridLines						= true;
				PaintPriceMarkers							= true;
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				//Disable this property for performance gains in Strategy Analyzer optimizations
				IsSuspendedWhileInactive					= true;
				
				// Default Property Values
				SwingStrength = 5;
				ShowFVG = true;
				FvgColorBullish = System.Windows.Media.Brushes.LimeGreen;
				FvgColorBearish = System.Windows.Media.Brushes.Red;
				FvgOpacity = 30; // 0-100
				
				ShowKillZones = true;
				KillZoneColor = System.Windows.Media.Brushes.Silver;
				KillZoneOpacity = 10;
				
				ShowOrderBlocks = true;
				ObColorBullish = System.Windows.Media.Brushes.DarkCyan;
				ObColorBearish = System.Windows.Media.Brushes.DarkMagenta;
				ObOpacity = 50;
			}
			else if (State == State.Configure)
			{
				// AddDataSeries logic for MTF would go here
			}
			else if (State == State.DataLoaded)
			{
				fvgList = new List<ICT_FVG>();
				swingPoints = new List<SwingPoint>();
				structureBreaks = new List<ICT_StructureBreak>();
				orderBlocks = new List<ICT_OrderBlock>();
			}
			else if (State == State.Terminated)
			{
				if (textFormat != null)
				{
					textFormat.Dispose();
					textFormat = null;
				}
				if (dwFactory != null)
				{
					dwFactory.Dispose();
					dwFactory = null;
				}
			}
		}

		protected override void OnBarUpdate()
		{
			if (CurrentBar < SwingStrength * 2 + 1) return;

			// --- Market Structure (Swing Points) ---
			// We check for a swing point at 'SwingStrength' bars ago
			int checkIndex = SwingStrength; 
			bool isSwingHigh = true;
			bool isSwingLow = true;

			for (int i = 1; i <= SwingStrength; i++)
			{
				// Check for Swing High
				if (High[checkIndex] <= High[checkIndex + i] || High[checkIndex] <= High[checkIndex - i])
					isSwingHigh = false;

				// Check for Swing Low
				if (Low[checkIndex] >= Low[checkIndex + i] || Low[checkIndex] >= Low[checkIndex - i])
					isSwingLow = false;
			}

			if (isSwingHigh)
			{
				int swingBarIndex = CurrentBar - checkIndex;
				// Avoid duplicate adds if we are recalculating historical bars (though with OnBarClose it's cleaner)
				if (swingPoints.Count == 0 || swingPoints.Last().BarIndex != swingBarIndex || !swingPoints.Last().IsHigh)
				{
					swingPoints.Add(new SwingPoint { Price = High[checkIndex], BarIndex = swingBarIndex, IsHigh = true });
					lastHighBar = swingBarIndex;
				}
			}

			if (isSwingLow)
			{
				int swingBarIndex = CurrentBar - checkIndex;
				if (swingPoints.Count == 0 || swingPoints.Last().BarIndex != swingBarIndex || swingPoints.Last().IsHigh)
				{
					swingPoints.Add(new SwingPoint { Price = Low[checkIndex], BarIndex = swingBarIndex, IsHigh = false });
					lastLowBar = swingBarIndex;
				}
			}

			// --- FVG Detection ---
			if (ShowFVG && CurrentBar > 2)
			{
				// Bullish FVG: Low[0] > High[2]
				if (Low[0] > High[2])
				{
					var fvg = new ICT_FVG 
					{ 
						Top = Low[0], 
						Bottom = High[2], 
						StartBarIndex = CurrentBar - 2, 
						IsBullish = true,
						IsMitigated = false
					};
					fvgList.Add(fvg);
				}
				// Bearish FVG: High[0] < Low[2]
				else if (High[0] < Low[2])
				{
					var fvg = new ICT_FVG 
					{ 
						Top = Low[2], 
						Bottom = High[0], 
						StartBarIndex = CurrentBar - 2, 
						IsBullish = false,
						IsMitigated = false
					};
					fvgList.Add(fvg);
				}
			}
			
			// --- FVG Mitigation Check ---
			// Simple check: if price enters the FVG box, mark as mitigated
			// Optimization: Only check unmitigated FVGs and maybe Limit how far back we check?
			// For now, check all unmitigated.
			foreach (var fvg in fvgList)
			{
				if (fvg.IsMitigated) continue;
				
				// If we are past the FVG formation
				if (CurrentBar > fvg.StartBarIndex + 2) 
				{
					// Check if current bar interacts with FVG
					if (High[0] >= fvg.Bottom && Low[0] <= fvg.Top)
					{
						fvg.IsMitigated = true;
					}
				}
			}
			
			// --- BOS / MSS Detection ---
			// Check if we broke the last Swing High
			if (lastHighBar != -1 && High[0] > High[CurrentBar - lastHighBar] && Close[0] > High[CurrentBar - lastHighBar])
			{
				// We have a potential BOS Bullish
				// Logic: If previous move was bearish (making lower lows), this might be MSS. 
				// For simplicity in Phase 1, we just mark it as BOS.
				
				// To avoid repetitive BOS on every bar above high, we track if we already marked this level?
				// Simple approach: Only trigger BOS if the *previous* bar closed below the level.
				double level = High[CurrentBar - lastHighBar];
				if (Close[1] <= level)
				{
					// New Break
					var bos = new ICT_StructureBreak
					{
						StartBarIndex = lastHighBar,
						EndBarIndex = CurrentBar,
						PriceLevel = level,
						IsBullish = true,
						Label = "BOS" // Or MSS based on trend logic
					};
					structureBreaks.Add(bos);
				}
			}

			// Check if we broke the last Swing Low
			if (lastLowBar != -1 && Low[0] < Low[CurrentBar - lastLowBar] && Close[0] < Low[CurrentBar - lastLowBar])
			{
				double level = Low[CurrentBar - lastLowBar];
				if (Close[1] >= level)
				{
					var bos = new ICT_StructureBreak
					{
						StartBarIndex = lastLowBar,
						EndBarIndex = CurrentBar,
						PriceLevel = level,
						IsBullish = false,
						Label = "BOS"
					};
					structureBreaks.Add(bos);
				}
			}
			
			// --- Order Block Detection ---
			// Triggered when BOS occurs.
			// If Bullish BOS: The last DOWN candle before the move started.
			// Simplified Logic: Look back from BOS point (StartBarIndex) for the lowest candle or the specific down candle.
			
			// We check specifically when a Bullish BOS is confirmed (Close > High).
			// We can reuse the bos variable context if we refactor, but here we'll do a standalone check or hook into BOS block above.
			// For robustness, let's do a standalone check for OB creation based on valid Swing Lows that *led* to a higher high?
			// OR: Use the classic ICT definition: "Last down candle before the displacement that broke structure".
			
			// Let's hook into the BOS detection block above (conceptually). 
			// Since we just added BOS logic without refactoring, I will add a separate block that detects
			// "If we just made a BOS, find the OB".
			
			if (ShowOrderBlocks)
			{
				if (structureBreaks.Count > 0)
				{
					var lastBos = structureBreaks.Last();
					if (lastBos.EndBarIndex == CurrentBar) // Only process on creation
					{
						if (lastBos.IsBullish)
						{
							// Find the last DOWN candle between previous structure and this BOS.
							// Start searching backwards from the BOS start point (Swing High) down to the lowest point?
							// Actually, the move started from a Low.
							// So we need the Lowest Low between the previous High and current BOS High.
							
							int searchEnd = lastBos.StartBarIndex; // The High that was broken
							int searchStart = CurrentBar; // Where we are now
							
							// Find the lowest low in the recent range (the "origin" of the move)
							int lowestBar = -1;
							double minPrice = double.MaxValue;
							
							// We need to look further back than StartBarIndex actually. 
							// The move that broke the structure started from a Low AFTER the previous High? No.
							// Correct Logic: 
							// 1. Structure: High A -> Low B -> Break High A.
							// 2. The OB is the down candle at Low B.
							
							// So we need to find the lowest point since 'StartBarIndex' (which is the index of the Old High).
							for (int i = 0; i < CurrentBar - lastBos.StartBarIndex; i++)
							{
								if (Low[i] < minPrice)
								{
									minPrice = Low[i];
									lowestBar = CurrentBar - i;
								}
							}
							
							if (lowestBar != -1)
							{
								// The OB is the candle AT lowestBar. 
								// ICT specific: If it's a down candle. If lowest is not down, look adjacent?
								// Simplified: Use the candle at the Swing Low.
								var ob = new ICT_OrderBlock
								{
									Top = High[CurrentBar - lowestBar],
									Bottom = Low[CurrentBar - lowestBar],
									StartBarIndex = lowestBar,
									IsBullish = true,
									IsMitigated = false
								};
								orderBlocks.Add(ob);
							}
						}
						else // Bearish BOS
						{
							// Structure: Low A -> High B -> Break Low A.
							// OB is at High B.
							int highestBar = -1;
							double maxPrice = double.MinValue;
							
							for (int i = 0; i < CurrentBar - lastBos.StartBarIndex; i++)
							{
								if (High[i] > maxPrice)
								{
									maxPrice = High[i];
									highestBar = CurrentBar - i;
								}
							}
							
							if (highestBar != -1)
							{
								var ob = new ICT_OrderBlock
								{
									Top = High[CurrentBar - highestBar],
									Bottom = Low[CurrentBar - highestBar],
									StartBarIndex = highestBar,
									IsBullish = false,
									IsMitigated = false
								};
								orderBlocks.Add(ob);
							}
						}
					}
				}
				
				// Mitigation Check for OBs
				foreach (var ob in orderBlocks)
				{
					if (ob.IsMitigated) continue;
					if (CurrentBar > ob.StartBarIndex + 5) // Give it some space
					{
						// If price touches DB
						if (ob.IsBullish && Low[0] <= ob.Top) ob.IsMitigated = true;
						if (!ob.IsBullish && High[0] >= ob.Bottom) ob.IsMitigated = true;
					}
				}
			}
		}

		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			if (chartControl == null || chartScale == null || RenderTarget == null)
				return;

			// Initialize DirectWrite resources if needed
			if (textFormat == null)
			{
				if (dwFactory == null)
					dwFactory = new SharpDX.DirectWrite.Factory();
				
				textFormat = new TextFormat(dwFactory, "Arial", 12)
				{
					TextAlignment = TextAlignment.Center,
					ParagraphAlignment = ParagraphAlignment.Center
				};
			}

			// Create Brushes
			var dxBullishBrush = ToDxBrush(RenderTarget, FvgColorBullish, FvgOpacity);
			var dxBearishBrush = ToDxBrush(RenderTarget, FvgColorBearish, FvgOpacity);
			var dxTextBrush = new SolidColorBrush(RenderTarget, SharpDX.Color.White); // Default text color
			var dxLineBrush = new SolidColorBrush(RenderTarget, SharpDX.Color.DodgerBlue); // BOS Line Color
			var dxObBullishBrush = ToDxBrush(RenderTarget, ObColorBullish, ObOpacity);
			var dxObBearishBrush = ToDxBrush(RenderTarget, ObColorBearish, ObOpacity);

			try
			{
				RenderTarget.AntialiasMode = AntialiasMode.PerPrimitive;

				// --- Draw Kill Zones ---
				if (ShowKillZones)
				{
					// For simplicity in this iteration, we don't store KillZones in a list in OnBarUpdate (though we could).
					// Instead we can iterate visible bars and checking time.
					// However, iterating every bar in OnRender is expensive.
					// Better approach: Calculate Kill Zone start/end indices in OnBarUpdate or just calc for visible range here.
					
					// Let's iterate visible bars to find time ranges.
					// London Open: 02:00 - 05:00 EST (roughly 07:00 - 10:00 UTC, depends on DST)
					// NY Open: 07:00 - 10:00 EST
					// NOTE: This requires Session Iterator or Time check.
					// For this simplified version, we'll check Time[0].TimeOfDay
					
					// Define EST offsets (Simplified, assumes user chart is in local time or exchange time)
					// Ideally we should use distinct TimeZoneInfo processing.
					
					// This is a placeholder for the logic to draw vertical bands.
					// Since we don't have a reliable "TimeZone" property from user yet, 
					// I will skip the complex TZ conversion and just highlight 02:00-05:00 and 07:00-10:00 of the CHART time.
					
					using (var dxZoneBrush = ToDxBrush(RenderTarget, KillZoneColor, KillZoneOpacity))
					{
						int startBar = -1;
						// Iterate ONLY visible bars for performance
						for (int i = ChartBars.FromIndex; i <= ChartBars.ToIndex; i++)
						{
							DateTime t = Bars.GetTime(i);
							int hour = t.Hour;
							
							// London: 2-5, NY: 7-10 (Simplified: assumes Chart Time ≈ EST)
							bool isZone = (hour >= 2 && hour < 5) || (hour >= 7 && hour < 10);
							
							if (isZone)
							{
								if (startBar == -1) startBar = i;
							}
							else
							{
								if (startBar != -1)
								{
									float x1 = (float)chartControl.GetXByBarIndex(ChartBars, startBar);
									float x2 = (float)chartControl.GetXByBarIndex(ChartBars, i - 1);
									var rect = new SharpDX.RectangleF(x1, 0, x2 - x1, (float)chartControl.ActualHeight);
									RenderTarget.FillRectangle(rect, dxZoneBrush);
									startBar = -1;
								}
							}
						}
						// Close open zone at end of chart
						if (startBar != -1)
						{
							float x1 = (float)chartControl.GetXByBarIndex(ChartBars, startBar);
							float x2 = (float)chartControl.GetXByBarIndex(ChartBars, ChartBars.ToIndex);
							var rect = new SharpDX.RectangleF(x1, 0, x2 - x1, (float)chartControl.ActualHeight);
							RenderTarget.FillRectangle(rect, dxZoneBrush);
						}
					}
				}

				// --- Draw Order Blocks ---
				if (ShowOrderBlocks)
				{
					lock (orderBlocks)
					{
						foreach (var ob in orderBlocks)
						{
							if (ob.IsMitigated) continue;

							float startX = (float)chartControl.GetXByBarIndex(ChartBars, ob.StartBarIndex);
							float endX = (float)chartControl.GetXByBarIndex(ChartBars, CurrentBar);
							
							float topY = (float)chartScale.GetYByValue(ob.Top);
							float bottomY = (float)chartScale.GetYByValue(ob.Bottom);

							var rect = new SharpDX.RectangleF(
								Math.Min(startX, endX), 
								Math.Min(topY, bottomY), 
								Math.Abs(endX - startX), 
								Math.Abs(bottomY - topY));

							if (ob.IsBullish)
								RenderTarget.FillRectangle(rect, dxObBullishBrush);
							else
								RenderTarget.FillRectangle(rect, dxObBearishBrush);
						}
					}
				}
				
				// --- Draw FVG ---
				if (ShowFVG)
				{
					lock (fvgList)
					{
						foreach (var fvg in fvgList)
						{
							if (fvg.IsMitigated) continue;

							float startX = (float)chartControl.GetXByBarIndex(ChartBars, fvg.StartBarIndex);
							float endX = (float)chartControl.GetXByBarIndex(ChartBars, CurrentBar);
							
							float topY = (float)chartScale.GetYByValue(fvg.Top);
							float bottomY = (float)chartScale.GetYByValue(fvg.Bottom);

							var rect = new SharpDX.RectangleF(
								Math.Min(startX, endX), 
								Math.Min(topY, bottomY), 
								Math.Abs(endX - startX), 
								Math.Abs(bottomY - topY));

							if (fvg.IsBullish)
								RenderTarget.FillRectangle(rect, dxBullishBrush);
							else
								RenderTarget.FillRectangle(rect, dxBearishBrush);
						}
					}
				}

				// --- Draw Structure Breaks (BOS/MSS) ---
				lock (structureBreaks)
				{
					foreach (var sb in structureBreaks)
					{
						if (sb.EndBarIndex < ChartBars.FromIndex || sb.StartBarIndex > ChartBars.ToIndex) continue;

						float x1 = (float)chartControl.GetXByBarIndex(ChartBars, sb.StartBarIndex);
						float x2 = (float)chartControl.GetXByBarIndex(ChartBars, sb.EndBarIndex);
						float y = (float)chartScale.GetYByValue(sb.PriceLevel);

						RenderTarget.DrawLine(new Vector2(x1, y), new Vector2(x2, y), dxLineBrush, 2.0f);
						
						// Draw Label
						var textRect = new SharpDX.RectangleF(x2, y - 10, 40, 20);
						RenderTarget.DrawText(sb.Label, textFormat, textRect, dxTextBrush);
					}
				}

				// --- Draw Swing Points ---
				lock (swingPoints)
				{
					foreach (var sp in swingPoints)
					{
						if (sp.BarIndex < ChartBars.FromIndex || sp.BarIndex > ChartBars.ToIndex)
							continue;

						float x = (float)chartControl.GetXByBarIndex(ChartBars, sp.BarIndex);
						float y = (float)chartScale.GetYByValue(sp.Price);
						
						string text = sp.IsHigh ? "PH" : "PL";
						float yOffset = sp.IsHigh ? -15 : 15;
						
						var textRect = new SharpDX.RectangleF(x - 20, y + yOffset - 10, 40, 20);
						RenderTarget.DrawText(text, textFormat, textRect, dxTextBrush);
					}
				}
			}
			finally
			{
				if (dxBullishBrush != null) dxBullishBrush.Dispose();
				if (dxBearishBrush != null) dxBearishBrush.Dispose();
				if (dxTextBrush != null) dxTextBrush.Dispose();
				if (dxLineBrush != null) dxLineBrush.Dispose();
				if (dxObBullishBrush != null) dxObBullishBrush.Dispose();
				if (dxObBearishBrush != null) dxObBearishBrush.Dispose();
			}
		}

		// Helper to convert Media Brush to Direct2D Brush
		private SolidColorBrush ToDxBrush(RenderTarget renderTarget, System.Windows.Media.Brush mediaBrush, int opacity)
		{
			System.Windows.Media.Color solidColor = System.Windows.Media.Colors.Gray;
			if (mediaBrush is System.Windows.Media.SolidColorBrush scb)
				solidColor = scb.Color;
			
			float a = (opacity / 100f);
			float r = solidColor.R / 255f;
			float g = solidColor.G / 255f;
			float b = solidColor.B / 255f;
			
			return new SolidColorBrush(renderTarget, new SharpDX.Color4(r, g, b, a));
		}

		#region Properties
		[Range(1, 100)]
		[NinjaScriptProperty]
		[Display(Name = "Swing Strength", Description = "Number of bars to left/right for Swing High/Low", Order = 1, GroupName = "1. Market Structure")]
		public int SwingStrength { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Show FVG", Description = "Show Fair Value Gaps", Order = 1, GroupName = "2. FVG Settings")]
		public bool ShowFVG { get; set; }
		
		[XmlIgnore]
		[Display(Name = "Bullish FVG Color", Description = "Color for Bullish FVG", Order = 2, GroupName = "2. FVG Settings")]
		public System.Windows.Media.Brush FvgColorBullish { get; set; }
		
		[Browsable(false)]
		public string FvgColorBullishSerialize
		{
			get { return Serialize.BrushToString(FvgColorBullish); }
			set { FvgColorBullish = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "Bearish FVG Color", Description = "Color for Bearish FVG", Order = 3, GroupName = "2. FVG Settings")]
		public System.Windows.Media.Brush FvgColorBearish { get; set; }
		
		[Browsable(false)]
		public string FvgColorBearishSerialize
		{
			get { return Serialize.BrushToString(FvgColorBearish); }
			set { FvgColorBearish = Serialize.StringToBrush(value); }
		}
		
		[Range(0, 100)]
		[NinjaScriptProperty]
		[Display(Name = "FVG Opacity", Description = "Opacity for FVG rectangles (0-100)", Order = 4, GroupName = "2. FVG Settings")]
		public int FvgOpacity { get; set; }
		
		[NinjaScriptProperty]
		[Display(Name = "Show Kill Zones", Description = "Show London/NY Open Kill Zones", Order = 1, GroupName = "3. Time Zones")]
		public bool ShowKillZones { get; set; }
		
		[XmlIgnore]
		[Display(Name = "Kill Zone Color", Description = "Background Color for Kill Zones", Order = 2, GroupName = "3. Time Zones")]
		public System.Windows.Media.Brush KillZoneColor { get; set; }
		
		[Browsable(false)]
		public string KillZoneColorSerialize
		{
			get { return Serialize.BrushToString(KillZoneColor); }
			set { KillZoneColor = Serialize.StringToBrush(value); }
		}
		
		[Range(0, 100)]
		[NinjaScriptProperty]
		[Display(Name = "Kill Zone Opacity", Description = "Opacity for Kill Zones (0-100)", Order = 3, GroupName = "3. Time Zones")]
		public int KillZoneOpacity { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Show Order Blocks", Description = "Show Order Blocks (OB)", Order = 1, GroupName = "4. Order Blocks")]
		public bool ShowOrderBlocks { get; set; }
		
		[XmlIgnore]
		[Display(Name = "Bullish OB Color", Description = "Color for Bullish OB", Order = 2, GroupName = "4. Order Blocks")]
		public System.Windows.Media.Brush ObColorBullish { get; set; }
		
		[Browsable(false)]
		public string ObColorBullishSerialize
		{
			get { return Serialize.BrushToString(ObColorBullish); }
			set { ObColorBullish = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "Bearish OB Color", Description = "Color for Bearish OB", Order = 3, GroupName = "4. Order Blocks")]
		public System.Windows.Media.Brush ObColorBearish { get; set; }
		
		[Browsable(false)]
		public string ObColorBearishSerialize
		{
			get { return Serialize.BrushToString(ObColorBearish); }
			set { ObColorBearish = Serialize.StringToBrush(value); }
		}
		
		[Range(0, 100)]
		[NinjaScriptProperty]
		[Display(Name = "OB Opacity", Description = "Opacity for OB rectangles (0-100)", Order = 4, GroupName = "4. Order Blocks")]
		public int ObOpacity { get; set; }
		#endregion
	}
	
	#region Custom Classes
	public class ICT_FVG
	{
		public double Top { get; set; }
		public double Bottom { get; set; }
		public int StartBarIndex { get; set; }
		public bool IsBullish { get; set; }
		public bool IsMitigated { get; set; }
	}
	
	public class SwingPoint
	{
		public double Price { get; set; }
		public int BarIndex { get; set; }
		public bool IsHigh { get; set; } // true = High, false = Low
	}

	public class ICT_StructureBreak
	{
		public int StartBarIndex { get; set; }
		public int EndBarIndex { get; set; }
		public double PriceLevel { get; set; }
		public bool IsBullish { get; set; }
		public string Label { get; set; }
	}

	public class ICT_OrderBlock
	{
		public double Top { get; set; }
		public double Bottom { get; set; }
		public int StartBarIndex { get; set; }
		public bool IsBullish { get; set; }
		public bool IsMitigated { get; set; }
	}
    #endregion
}
