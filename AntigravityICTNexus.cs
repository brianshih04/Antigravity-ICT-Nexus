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
		
		// Resources for Direct2D
		private SharpDX.Direct2D1.Brush bullishFvgBrush;
		private SharpDX.Direct2D1.Brush bearishFvgBrush;
		private SharpDX.Direct2D1.Brush textBrush;
		private SharpDX.DirectWrite.TextFormat textFormat;
		
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
			}
			else if (State == State.Configure)
			{
				// AddDataSeries logic for MTF would go here
			}
			else if (State == State.DataLoaded)
			{
				fvgList = new List<ICT_FVG>();
				swingPoints = new List<SwingPoint>();
			}
			else if (State == State.Terminated)
			{
				if (textFormat != null)
				{
					textFormat.Dispose();
					textFormat = null;
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
		}

		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			if (chartControl == null || chartScale == null || RenderTarget == null)
				return;

			// Initialize DirectWrite resources if needed
			if (textFormat == null)
			{
				textFormat = new TextFormat(new SharpDX.DirectWrite.Factory(), "Arial", 12)
				{
					TextAlignment = TextAlignment.Center,
					ParagraphAlignment = ParagraphAlignment.Center
				};
			}

			// Create Brushes
			// Note: converting Media.Brush to SharpDX.Direct2D1.Brush
			// We recreate them here to ensure they are valid for the current RenderTarget
			// In a production refined version, we would cache these and handle RenderTarget changes.
			var dxBullishBrush = ToDxBrush(RenderTarget, FvgColorBullish, FvgOpacity);
			var dxBearishBrush = ToDxBrush(RenderTarget, FvgColorBearish, FvgOpacity);
			var dxTextBrush = new SolidColorBrush(RenderTarget, SharpDX.Color.White); // Default text color

			try
			{
				RenderTarget.AntialiasMode = AntialiasMode.PerPrimitive;

				// --- Draw FVGs ---
				if (ShowFVG)
				{
					lock (fvgList)
					{
						foreach (var fvg in fvgList)
						{
							// Optimization: Skip if completely off-screen (implied by checking coordinates?)
							// Actually, we should check if the FVG is within visible range or extends into it.
							// For simplicity in this version, we iterate all and let Clipped boundaries handle it,
							// but for performance with thousands of FVGs, we should filter by ChartBars.FromIndex/ToIndex.
							
							if (fvg.IsMitigated) continue; // Only draw unmitigated for now, or change logic to draw faded

							float startX = (float)chartControl.GetXByBarIndex(ChartBars, fvg.StartBarIndex);
							float endX = (float)chartControl.GetXByBarIndex(ChartBars, CurrentBar); // Extend to current
							// Or extend to infinity if we want
							
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

				// --- Draw Swing Points ---
				lock (swingPoints)
				{
					foreach (var sp in swingPoints)
					{
						// Check visibility
						if (sp.BarIndex < chartBars.FromIndex || sp.BarIndex > chartBars.ToIndex)
							continue;

						float x = (float)chartControl.GetXByBarIndex(ChartBars, sp.BarIndex);
						float y = (float)chartScale.GetYByValue(sp.Price);
						
						string text = sp.IsHigh ? "PH" : "PL"; // Pivot High / Pivot Low
						
						// Draw slightly above/below price
						float yOffset = sp.IsHigh ? -15 : 15;
						
						var textRect = new SharpDX.RectangleF(x - 20, y + yOffset - 10, 40, 20);
						
						RenderTarget.DrawText(text, textFormat, textRect, dxTextBrush);
					}
				}
			}
			finally
			{
				// Dispose temporary brushes
				if (dxBullishBrush != null) dxBullishBrush.Dispose();
				if (dxBearishBrush != null) dxBearishBrush.Dispose();
				if (dxTextBrush != null) dxTextBrush.Dispose();
			}
		}

		// Helper to convert Media Brush to Direct2D Brush
		private SolidColorBrush ToDxBrush(RenderTarget renderTarget, System.Windows.Media.Brush mediaBrush, int opacity)
		{
			System.Windows.Media.Color solidColor = System.Windows.Media.Colors.Gray; // Default fallback
			
			if (mediaBrush is System.Windows.Media.SolidColorBrush scb)
			{
				solidColor = scb.Color;
			}
			
			// Convert 0-255 to 0.0-1.0
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
    #endregion
}
