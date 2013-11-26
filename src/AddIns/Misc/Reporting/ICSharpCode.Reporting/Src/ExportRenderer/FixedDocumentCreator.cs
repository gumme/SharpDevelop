﻿/*
 * Created by SharpDevelop.
 * User: Peter Forstmeier
 * Date: 05.05.2013
 * Time: 19:42
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

using ICSharpCode.Reporting.Items;
using ICSharpCode.Reporting.PageBuilder.ExportColumns;
using Brush = System.Windows.Media.Brush;
using FontFamily = System.Windows.Media.FontFamily;
using Image = System.Windows.Controls.Image;
using Pen = System.Windows.Media.Pen;
using Size = System.Windows.Size;

namespace ICSharpCode.Reporting.ExportRenderer
{
	/// <summary>
	/// Description of FixedDocumentCreator.
	/// </summary>
	internal class FixedDocumentCreator
	{
		BrushConverter brushConverter ;
		ReportSettings reportSettings;
		
		public FixedDocumentCreator(ReportSettings reportSettings)
		{
			if (reportSettings == null)
				throw new ArgumentNullException("reportSettings");
			this.reportSettings = reportSettings;
			Console.WriteLine("FixedDocumentCreator()");
			brushConverter = new BrushConverter();
		}
		
		
		public  UIElement CreateContainer(ExportContainer container)
		{
//			http://tech.pro/tutorial/736/wpf-tutorial-creating-a-custom-panel-control
			
			var canvas = CreateCanvas(container);
//			canvas.Measure(new Size(reportSettings.PageSize.Width,reportSettings.PageSize.Height));
			
			var size = new Size(container.DesiredSize.Width,container.DesiredSize.Height);
			
			canvas.Measure(size);
			
//			canvas.Arrange(new Rect(new System.Windows.Point(),new Size(reportSettings.PageSize.Width,reportSettings.PageSize.Height) ));
			
			canvas.Arrange(new Rect(new System.Windows.Point(),size ));

			canvas.UpdateLayout();
			
			return canvas;
			
		}
		
		public TextBlock CreateTextBlock(ExportText exportText)
		{
			var textBlock = new TextBlock();
			textBlock.Text = exportText.Text;
			textBlock.Foreground = ConvertBrush(exportText.ForeColor);
			SetFont(textBlock,exportText);
			textBlock.TextWrapping = TextWrapping.WrapWithOverflow;
			
//			string [] inlines = exportText.Text.Split(System.Environment.NewLine.ToCharArray());
			//string [] inlines = "jmb,.n,knn-.n.-n.n-.n.n.-";
//			for (int i = 0; i < inlines.Length; i++) {
//				if (inlines[i].Length > 0) {
//					textBlock.Inlines.Add(new Run(inlines[i]));
			////					textBlock.Inlines.Add(new LineBreak());
//				}
//			}
//			var li = textBlock.Inlines.LastInline;
//			textBlock.Inlines.Remove(li);
//			SetDimension(textBlock,exportText.StyleDecorator);
//		    textBlock.Background = ConvertBrush(exportText.StyleDecorator.BackColor);
//		    SetContendAlignment(textBlock,exportText.StyleDecorator);
			SetPosition(textBlock,exportText);
			SetDimension(textBlock,exportText);
			textBlock.Background = ConvertBrush(exportText.BackColor);
			return textBlock;
		}
		
		
		Canvas CreateCanvas(ExportContainer container)
		{
			var canvas = new Canvas();
			SetDimension(canvas, container);
			SetPosition(canvas,container);
			canvas.Background = ConvertBrush(System.Drawing.Color.Red);
			return canvas;
		}
		
		
		static void SetDimension (FrameworkElement element,ExportColumn exportColumn)
		{
			element.Width = exportColumn.DesiredSize.Width;
			element.Height = exportColumn.DesiredSize.Height;
		}
		
		
		static void SetPosition (FrameworkElement element,ExportColumn exportColumn) {
			FixedPage.SetLeft(element,exportColumn.Location.X );
			FixedPage.SetTop(element,exportColumn.Location.Y);
		}
		
		void SetFont(TextBlock textBlock,ExportText exportText)
		{
			textBlock.FontFamily = new FontFamily(exportText.Font.FontFamily.Name);
		
			textBlock.FontSize = exportText.Font.Size * 96/72;
			
			if (exportText.Font.Bold) {
				textBlock.FontWeight = FontWeights.Bold;
			}
			if (exportText.Font.Underline) {
				CreateUnderline(textBlock,exportText);
			}
			
			if (exportText.Font.Italic) {
				textBlock.FontStyle = System.Windows.FontStyles.Italic ;
			}
			if (exportText.Font.Strikeout) {
				CreateStrikeout(textBlock,exportText);
			}
		}
		
		
		void CreateStrikeout (TextBlock textBlock,ExportColumn exportColumn )
		{
			var strikeOut = new TextDecoration();
			strikeOut.Location = TextDecorationLocation.Strikethrough;

			Pen p = CreateWpfPen(exportColumn);
			strikeOut.Pen = p ;
			strikeOut.PenThicknessUnit = TextDecorationUnit.FontRecommended;
			textBlock.TextDecorations.Add(strikeOut);
		}
		
		
		void CreateUnderline(TextBlock textBlock,ExportColumn exportColumn)
		{
			var underLine = new TextDecoration();
			Pen p = CreateWpfPen(exportColumn);
			underLine.Pen = p ;
			underLine.PenThicknessUnit = TextDecorationUnit.FontRecommended;
			textBlock.TextDecorations.Add(underLine);
		}
		
		
		Pen CreateWpfPen(ExportColumn exportColumn)
		{
			var myPen = new Pen();
			myPen.Brush = ConvertBrush(exportColumn.ForeColor);
			myPen.Thickness = 1.5;
			return myPen;
		}
		
		
		Brush ConvertBrush(System.Drawing.Color color)
		{
			if (brushConverter.IsValid(color.Name)){
				return brushConverter.ConvertFromString(color.Name) as SolidColorBrush;
			} else{
				return brushConverter.ConvertFromString("Black") as SolidColorBrush;
			}
		}
	}
}
