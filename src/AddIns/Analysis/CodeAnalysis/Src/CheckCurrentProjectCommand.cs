﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Project;
using ICSharpCode.SharpDevelop.Project.Commands;

namespace ICSharpCode.CodeAnalysis
{
	public class CheckCurrentProjectCommand : BuildProject
	{
		public override async void StartBuild()
		{
			var options = new BuildOptions(BuildTarget.Rebuild);
			options.TargetForDependencies = BuildTarget.Build;
			options.ProjectAdditionalProperties["RunCodeAnalysis"] = "true";
			CallbackMethod(await SD.BuildService.BuildAsync(this.ProjectToBuild, options));
		}
	}
}
