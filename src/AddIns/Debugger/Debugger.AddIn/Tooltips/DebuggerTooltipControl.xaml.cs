﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

using Debugger.AddIn.TreeModel;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Editor;

namespace Debugger.AddIn.Tooltips
{
	public partial class DebuggerTooltipControl : Popup, ITooltip
	{
		static Point ChildPopupOffset = new Point(16, 15);
		
		public DebuggerTooltipControl ChildTooltip { get; private set; }
		public IEnumerable<TreeNode> TreeNodes { get; set; }
		
		public DebuggerTooltipControl(IEnumerable<TreeNode> treeNodes)
		{
			InitializeComponent();
			
			this.TreeNodes = treeNodes;
			this.dataGrid.ItemsSource = treeNodes;
			
			// Only the leaf of the tooltip has this set to false
			// Therefore it will automatically close if something else gets focus
			this.StaysOpen = false;
			this.Placement = PlacementMode.Absolute;
		}
		
		public DebuggerTooltipControl(params TreeNode[] treeNodes)
			: this((IEnumerable<TreeNode>)treeNodes)
		{
		}
		
		private void Expand_Click(object sender, RoutedEventArgs e)
		{
			var clickedButton = (ToggleButton)e.OriginalSource;
			var clickedNode = (TreeNode)clickedButton.DataContext;
			
			if (clickedButton.IsChecked == true && clickedNode.GetChildren != null) {
				Point popupPos = clickedButton.PointToScreen(ChildPopupOffset).TransformFromDevice(clickedButton);
				this.ChildTooltip = new DebuggerTooltipControl(clickedNode.GetChildren().ToList()) {
					// We can not use placement target otherwise we would get too deep logical tree
					Placement = PlacementMode.Absolute,
					HorizontalOffset = popupPos.X,
					VerticalOffset = popupPos.Y,
				};
				
				// The child is now tracking the focus
				this.StaysOpen = true;
				this.ChildTooltip.StaysOpen = false;
				
				this.ChildTooltip.Closed += delegate {
					// The null will have the effect of ignoring the next click
					clickedButton.IsChecked = clickedButton.IsMouseOver ? (bool?)null : false;
					// Either keep closing or make us the new leaf
					if (this.IsMouseOver) {
						this.StaysOpen = false;
					} else {
						this.IsOpen = false;
					}
				};
				this.ChildTooltip.IsOpen = true;
			}
		}
		
		bool ITooltip.CloseWhenMouseMovesAway {
			get {
				return this.ChildTooltip == null;
			}
		}

		protected override void OnClosed(EventArgs e)
		{
			// Closing the popup does not normally cause LostFocus on the textbox so we have to update it manually
			TextBox textBox = FocusManager.GetFocusedElement(this) as TextBox;
			if (textBox != null) {
				BindingExpression be = textBox.GetBindingExpression(TextBox.TextProperty);
				be.UpdateSource();
			}

			base.OnClosed(e);

			if (this.ChildTooltip != null) {
				this.ChildTooltip.IsOpen = false;
			}
		}
		
		void CopyMenuItemClick(object sender, RoutedEventArgs e)
		{
			ValueNode node = ((MenuItem)sender).DataContext as ValueNode;
			if (node != null) {
				Clipboard.SetText(node.FullText);
			}
		}
		
		/*
		void AnimateCloseControl(bool show)
		{
			DoubleAnimation animation = new DoubleAnimation();
			animation.From = show ? 0 : 1;
			animation.To = show ? 1 : 0;
			animation.BeginTime = new TimeSpan(0, 0, show ? 0 : 1);
			animation.Duration = new Duration(TimeSpan.FromMilliseconds(500));
			animation.SetValue(Storyboard.TargetProperty, this.PinCloseControl);
			animation.SetValue(Storyboard.TargetPropertyProperty, new PropertyPath(Rectangle.OpacityProperty));
			
			Storyboard board = new Storyboard();
			board.Children.Add(animation);
			
			board.Begin(this);
		}
		*/
	}
}