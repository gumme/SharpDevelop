﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.Collections.Generic;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.PackageManagement;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Dom;
using ICSharpCode.SharpDevelop.Project;

namespace ICSharpCode.PackageManagement.Design
{
	public class FakePackageManagementProjectService : IPackageManagementProjectService
	{
		public bool IsRefreshProjectBrowserCalled;
		
		public IProject CurrentProject { get; set; }
		public ISolution OpenSolution { get; set; }
		
		public event EventHandler<SolutionEventArgs> SolutionClosed;
		public event EventHandler<SolutionEventArgs> SolutionOpened;
		
		public void RefreshProjectBrowser()
		{
			IsRefreshProjectBrowserCalled = true;
		}
		
		public void FireSolutionClosedEvent(ISolution solution)
		{
			if (SolutionClosed != null) {
				SolutionClosed(this, new SolutionEventArgs(solution));
			}
		}
		
		public void FireSolutionOpenedEvent(ISolution solution)
		{
			if (SolutionOpened != null) {
				SolutionOpened(this, new SolutionEventArgs(solution));
			}
		}
		
		public readonly IMutableModelCollection<IModelCollection<IProject>> ProjectCollections = new NullSafeSimpleModelCollection<IModelCollection<IProject>>();
		IModelCollection<IProject> allProjects;
		
		public IModelCollection<IProject> AllProjects {
			get { 
				if (allProjects == null)
					allProjects = ProjectCollections.SelectMany(c => c);
				return allProjects; 
			}
		}
		
		public void AddProject(IProject project)
		{
			ProjectCollections.Add(new ImmutableModelCollection<IProject>(new[] { project }));
		}
		
		public void AddProjectItem(IProject project, ProjectItem item)
		{
			ProjectService.AddProjectItem(project, item);
		}
		
		public void RemoveProjectItem(IProject project, ProjectItem item)
		{
			ProjectService.RemoveProjectItem(project, item);
		}
		
		public void Save(IProject project)
		{
			project.Save();
		}
		
		public ISolution SavedSolution;
		
		public void Save(ISolution solution)
		{
			SavedSolution = solution;
		}
		
//		public IProjectContent GetProjectContent(IProject project)
//		{
//			return new DefaultProjectContent();
//		}
		
		public IProjectBrowserUpdater ProjectBrowserUpdater;
		
		public IProjectBrowserUpdater CreateProjectBrowserUpdater()
		{
			return ProjectBrowserUpdater;
		}
		
		Dictionary<string, string> defaultCustomTools = new Dictionary<string, string>();
		
		public void AddDefaultCustomToolForFileName(string fileName, string customTool)
		{
			defaultCustomTools.Add(fileName, customTool);
		}
		
		public string GetDefaultCustomToolForFileName(FileProjectItem projectItem)
		{
			if (defaultCustomTools.ContainsKey(projectItem.FileName)) {
				return defaultCustomTools[projectItem.FileName];
			}
			return String.Empty;
		}
		
		public FakeProjectBuilder FakeProjectBuilder = new FakeProjectBuilder();
		
		public IProjectBuilder ProjectBuilder {
			get { return FakeProjectBuilder; }
		}
	}
}
