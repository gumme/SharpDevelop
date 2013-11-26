﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

using ICSharpCode.Core;
using ICSharpCode.SharpDevelop.Project;
using ICSharpCode.UnitTesting;

namespace ICSharpCode.MachineSpecifications
{
	public class MSpecApplication
	{
		public MSpecApplication(IEnumerable<ITest> tests)
		{
			InitializeFrom(tests);
		}
		
		public ProcessStartInfo GetProcessStartInfo()
		{
			return new ProcessStartInfo {
				FileName = ExecutableFileName,
				Arguments = GetArguments()
			};
		}
	
		public string Results { get;set; }
		
		void InitializeFrom(IEnumerable<ITest> tests)
		{
			this.tests = tests;
			ITest test = tests.FirstOrDefault();
			if (test != null)
				project = test.ParentProject.Project;
		}
		
		IEnumerable<ITest> tests;
		IProject project;

		string GetArguments()
		{
			var builder = new StringBuilder();

			builder.Append("--xml \"");
			builder.Append(FileUtility.GetAbsolutePath(Environment.CurrentDirectory, Results));
			builder.Append("\" ");

			string filterFileName = CreateFilterFile();
			if (filterFileName != null) {
				builder.Append("-f \"");
				builder.Append(FileUtility.GetAbsolutePath(Environment.CurrentDirectory, filterFileName));
				builder.Append("\" ");
			}

			builder.Append("\"");
			builder.Append(project.OutputAssemblyFullPath);
			builder.Append("\"");

			return builder.ToString();
		}

		string CreateFilterFile()
		{
			var classFilterBuilder = new ClassFilterBuilder();
			IList<string> filter = classFilterBuilder.BuildFilterFor(tests);
			
			string path = null;
			if (filter.Count > 0) {
				path = Path.GetTempFileName();
				using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
				using (var writer = new StreamWriter(stream)) {
					foreach (string testClassName in filter) {
						writer.WriteLine(testClassName);
					}
				}
			}
			return path;
		}

		string ExecutableFileName {
			get {
				string assemblyDirectory = Path.GetDirectoryName(new Uri(typeof(MSpecApplication).Assembly.CodeBase).LocalPath);
				string runnerDirectory = Path.Combine(assemblyDirectory, @"Tools\Machine.Specifications");

				string executableName = "mspec";
				if (TargetPlatformIs32Bit(project))
					executableName += "-x86";
				if (UsesClr4(project))
					executableName += "-clr4";

				executableName += ".exe";
				return Path.Combine(runnerDirectory, executableName);
			}
		}

		bool UsesClr4(IProject project)
		{
			MSBuildBasedProject msbuildProject = project as MSBuildBasedProject;
			if (msbuildProject != null) {
				string targetFrameworkVersion = msbuildProject.GetEvaluatedProperty("TargetFrameworkVersion");
				return String.Equals(targetFrameworkVersion, "v4.0", StringComparison.OrdinalIgnoreCase);
			}
			return false;
		}

		bool TargetPlatformIs32Bit(IProject project)
		{
			MSBuildBasedProject msbuildProject = project as MSBuildBasedProject;
			if (msbuildProject != null) {
				string platformTarget = msbuildProject.GetEvaluatedProperty("PlatformTarget");
				return String.Compare(platformTarget, "x86", true) == 0;
			}
			return false;
		}

		string WorkingDirectory {
			get {
				return Path.GetDirectoryName(project.OutputAssemblyFullPath);
			}
		}
	}
}
