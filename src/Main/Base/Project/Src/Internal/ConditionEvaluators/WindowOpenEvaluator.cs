﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using ICSharpCode.Core;
using ICSharpCode.SharpDevelop.Gui;

namespace ICSharpCode.SharpDevelop
{
	/// <summary>
	/// Tests if a window of a specified type or implementing an interface is open.
	/// The window does not need to be the active window.
	/// </summary>
	/// <attribute name="openwindow">
	/// The fully qualified name of the type the window should be or the
	/// interface name it should implement.
	/// "*" to test if any window is open.
	/// </attribute>
	/// <example title="Test if a text editor is opened">
	/// &lt;Condition name="WindowOpen" openwindow="ICSharpCode.SharpDevelop.Editor.ITextEditor"&gt;
	/// </example>
	/// <example title="Test if any window is open">
	/// &lt;Condition name="WindowOpen" openwindow="*"&gt;
	/// </example>
	public class WindowOpenConditionEvaluator : IConditionEvaluator
	{
		public bool IsValid(object caller, Condition condition)
		{
			if (SD.Workbench == null) {
				return false;
			}
			
			string openWindow = condition.Properties["openwindow"];
			
			Type openWindowType = Type.GetType(openWindow, false);
			if (openWindowType == null) {
				//SD.Log.WarnFormatted("WindowOpenCondition: cannot find Type {0}", openWindow);
				return false;
			}
			
			if (SD.GetActiveViewContentService(openWindowType) != null)
				return true;
			
			if (openWindow == "*") {
				return SD.Workbench.ActiveWorkbenchWindow != null;
			}
			
			foreach (IViewContent view in SD.Workbench.ViewContentCollection) {
				Type currentType = view.GetType();
				if (currentType.ToString() == openWindow) {
					return true;
				}
				foreach (Type i in currentType.GetInterfaces()) {
					if (i.ToString() == openWindow) {
						return true;
					}
				}
			}
			return false;
		}
	}
}
