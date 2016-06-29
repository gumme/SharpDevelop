// Copyright (c) 2014 AlphaSierraPapa for the SharpDevelop Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Controls;
using ICSharpCode.WpfDesign.Extensions;

namespace ICSharpCode.WpfDesign.Designer.Extensions
{
	static class TransparentBrushHelper
	{
		public static readonly Brush TransparentBrush;

		static TransparentBrushHelper()
		{
			// Must freeze the brush before using it in the CustomInstanceFactory derived classes in this file,
			// otherwise me might get a memory leak. Not sure why the leak occurs or why freezing the brush helps, but the memory leak
			// got tracked here with a Memory Profiler and the leak was no longer present after the call to Freeze was added here.
			// Also, now when the brush is frozen we can safely share the brush.
			TransparentBrush = new SolidColorBrush(Colors.Transparent);
			TransparentBrush.Freeze();
		}
	}

	/// <summary>
	/// Instance factory used to create Panel instances.
	/// Sets the panels Brush to a transparent brush, and modifies the panel's type descriptor so that
	/// the property value is reported as null when the transparent brush is used, and
	/// setting the Brush to null actually restores the transparent brush.
	/// </summary>
	[ExtensionFor(typeof(Panel))]
	public sealed class PanelInstanceFactory : CustomInstanceFactory
	{
		/// <summary>
		/// Creates an instance of the specified type, passing the specified arguments to its constructor.
		/// </summary>
		public override object CreateInstance(Type type, params object[] arguments)
		{
			object instance = base.CreateInstance(type, arguments);
			Panel panel = instance as Panel;
			if (panel != null) {
				if (panel.Background == null) {
					panel.Background = TransparentBrushHelper.TransparentBrush;
				}
				TypeDescriptionProvider provider = new DummyValueInsteadOfNullTypeDescriptionProvider(
					TypeDescriptor.GetProvider(panel), "Background", TransparentBrushHelper.TransparentBrush);
				TypeDescriptor.AddProvider(provider, panel);
			}
			return instance;
		}
	}
	
	[ExtensionFor(typeof(HeaderedContentControl))]
	public sealed class HeaderedContentControlInstanceFactory : CustomInstanceFactory
	{
		/// <summary>
		/// Creates an instance of the specified type, passing the specified arguments to its constructor.
		/// </summary>
		public override object CreateInstance(Type type, params object[] arguments)
		{
			object instance = base.CreateInstance(type, arguments);
			Control control = instance as Control;
			if (control != null) {
				if (control.Background == null) {
					control.Background = TransparentBrushHelper.TransparentBrush;
				}
				TypeDescriptionProvider provider = new DummyValueInsteadOfNullTypeDescriptionProvider(
					TypeDescriptor.GetProvider(control), "Background", TransparentBrushHelper.TransparentBrush);
				TypeDescriptor.AddProvider(provider, control);
			}
			return instance;
		}
	}
	
	[ExtensionFor(typeof(ItemsControl))]
	public sealed class TransparentControlsInstanceFactory : CustomInstanceFactory
	{
		/// <summary>
		/// Creates an instance of the specified type, passing the specified arguments to its constructor.
		/// </summary>
		public override object CreateInstance(Type type, params object[] arguments)
		{
			object instance = base.CreateInstance(type, arguments);
			Control control = instance as Control;
			if (control != null && (
				type == typeof(ItemsControl))) {
				if (control.Background == null) {
					control.Background = TransparentBrushHelper.TransparentBrush;
				}
				
				TypeDescriptionProvider provider = new DummyValueInsteadOfNullTypeDescriptionProvider(
					TypeDescriptor.GetProvider(control), "Background", TransparentBrushHelper.TransparentBrush);
				TypeDescriptor.AddProvider(provider, control);
			}
			return instance;
		}
	}
	
	[ExtensionFor(typeof(Border))]
	public sealed class BorderInstanceFactory : CustomInstanceFactory
	{
		/// <summary>
		/// Creates an instance of the specified type, passing the specified arguments to its constructor.
		/// </summary>
		public override object CreateInstance(Type type, params object[] arguments)
		{
			object instance = base.CreateInstance(type, arguments);
			Border panel = instance as Border;
			if (panel != null)
			{
				if (panel.Background == null)
				{
					panel.Background = TransparentBrushHelper.TransparentBrush;
				}
				TypeDescriptionProvider provider = new DummyValueInsteadOfNullTypeDescriptionProvider(
					TypeDescriptor.GetProvider(panel), "Background", TransparentBrushHelper.TransparentBrush);
				TypeDescriptor.AddProvider(provider, panel);
			}
			return instance;
		}
	}
}
