﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the BSD license (for details please see \src\AddIns\Debugger\Debugger.AddIn\license.txt)

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Xml.Serialization;
using Debugger;
using ICSharpCode.Core;

namespace ICSharpCode.SharpDevelop.Services
{
	public enum ShowIntegersAs { Decimal, Hexadecimal, Both };
	
	public class DebuggingOptions: Options
	{
		public static DebuggingOptions Instance {
			get { return new DebuggingOptions(); }
		}
		
		public IPropertyService PS {
			get { return SD.GetService<IPropertyService>(); }
		}
		
		public override bool EnableJustMyCode {
			get { return PS.Get<bool>("Debugger.EnableJustMyCode", true); }
			set { PS.Set<bool>("Debugger.EnableJustMyCode", value); }
		}
		
		public override bool EnableEditAndContinue {
			get { return PS.Get<bool>("Debugger.EnableEditAndContinue", true); }
			set { PS.Set<bool>("Debugger.EnableEditAndContinue", value); }
		}
		
		public override bool SuppressJITOptimization {
			get { return PS.Get<bool>("Debugger.SuppressJITOptimization", true); }
			set { PS.Set<bool>("Debugger.SuppressJITOptimization", value); }
		}
		
		public override bool SuppressNGENOptimization {
			get { return PS.Get<bool>("Debugger.SuppressNGENOptimization", true); }
			set { PS.Set<bool>("Debugger.SuppressNGENOptimization", value); }
		}
		
		public override bool StepOverDebuggerAttributes {
			get { return PS.Get<bool>("Debugger.StepOverDebuggerAttributes", true); }
			set { PS.Set<bool>("Debugger.StepOverDebuggerAttributes", value); }
		}
		
		public override bool StepOverAllProperties {
			get { return PS.Get<bool>("Debugger.StepOverAllProperties", false); }
			set { PS.Set<bool>("Debugger.StepOverAllProperties", value); }
		}
		
		public override bool StepOverFieldAccessProperties {
			get { return PS.Get<bool>("Debugger.StepOverFieldAccessProperties", true); }
			set { PS.Set<bool>("Debugger.StepOverFieldAccessProperties", value); }
		}
		
		public override IEnumerable<string> SymbolsSearchPaths {
			get { return PS.GetList<string>("Debugger.SymbolsSearchPaths"); }
			set { PS.SetList<string>("Debugger.SymbolsSearchPaths", value); }
		}
		
		public override bool PauseOnHandledExceptions {
			get { return PS.Get<bool>("Debugger.PauseOnHandledExceptions", false); }
			set { PS.Set<bool>("Debugger.PauseOnHandledExceptions", value); }
		}
		
		public bool AskForArguments {
			get { return PS.Get<bool>("Debugger.AskForArguments", false); }
			set { PS.Set<bool>("Debugger.AskForArguments", value); }
		}
		
		public bool BreakAtBeginning {
			get { return PS.Get<bool>("Debugger.BreakAtBeginning", false); }
			set { PS.Set<bool>("Debugger.BreakAtBeginning", value); }
		}
		
		public ShowIntegersAs ShowIntegersAs {
			get { return PS.Get<ShowIntegersAs>("Debugger.ShowIntegersAs", ShowIntegersAs.Decimal); }
			set { PS.Set<ShowIntegersAs>("Debugger.ShowIntegersAs", value); }
		}
		
		public bool ShowArgumentNames {
			get { return PS.Get<bool>("Debugger.ShowArgumentNames", false); }
			set { PS.Set<bool>("Debugger.ShowArgumentNames", value); }
		}
		
		public bool ShowArgumentValues {
			get { return PS.Get<bool>("Debugger.ShowArgumentValues", false); }
			set { PS.Set<bool>("Debugger.ShowArgumentValues", value); }
		}
		
		public bool ShowExternalMethods {
			get { return PS.Get<bool>("Debugger.ShowExternalMethods", false); }
			set { PS.Set<bool>("Debugger.ShowExternalMethods", value); }
		}
		
		public bool ShowLineNumbers {
			get { return PS.Get<bool>("Debugger.ShowLineNumbers", false); }
			set { PS.Set<bool>("Debugger.ShowLineNumbers", value); }
		}
		
		public bool ShowModuleNames {
			get { return PS.Get<bool>("Debugger.ShowModuleNames", false); }
			set { PS.Set<bool>("Debugger.ShowModuleNames", value); }
		}
		
		public FormWindowState DebuggerEventWindowState {
			get { return PS.Get<FormWindowState>("Debugger.DebuggerEventWindowState", FormWindowState.Normal); }
			set { PS.Set<FormWindowState>("Debugger.DebuggerEventWindowState", value); }
		}
		
		public FormWindowState DebuggeeExceptionWindowState {
			get { return PS.Get<FormWindowState>("Debugger.DebuggeeExceptionWindowState", FormWindowState.Normal); }
			set { PS.Set<FormWindowState>("Debugger.DebuggeeExceptionWindowState", value); }
		}
	}
}
