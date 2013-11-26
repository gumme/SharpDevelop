﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using ICSharpCode.NRefactory.CSharp.Refactoring;

namespace CSharpBinding.Refactoring
{
	public class SDRedundantUsingIssue : RedundantUsingDirectiveIssue
	{
		public SDRedundantUsingIssue()
		{
			base.NamespacesToKeep.Add("System");
			base.NamespacesToKeep.Add("System.Linq");
		}
	}
}
