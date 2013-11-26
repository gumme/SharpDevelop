﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.Collections.Generic;
using ICSharpCode.Core;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Dom;
using ICSharpCode.SharpDevelop.Project;
using Rhino.Mocks;

namespace PackageManagement.Tests.Helpers
{
	public static class ProjectHelper
	{
		public static ISolution CreateSolution()
		{
			SD.InitializeForUnitTests();
			ISolution solution = MockRepository.GenerateStrictMock<ISolution>();
			solution.Stub(s => s.MSBuildProjectCollection).Return(new Microsoft.Build.Evaluation.ProjectCollection());
			solution.Stub(s => s.Projects).Return(new NullSafeSimpleModelCollection<IProject>());
			solution.Stub(s => s.ActiveConfiguration).Return(new ConfigurationAndPlatform("Debug", "Any CPU"));
			//solution.Stub(s => s.FileName).Return(FileName.Create(@"d:\projects\Test\TestSolution.sln"));
			return solution;
		}
		
		public static TestableProject CreateTestProject()
		{
			return CreateTestProject("TestProject");
		}
		
		public static TestableProject CreateTestProject(string name)
		{
			ISolution solution = CreateSolution();
			
			return CreateTestProject(solution, name);
		}
		
		public static TestableProject CreateTestProject(
			ISolution parentSolution,
			string name,
			string fileName = null)
		{
			var createInfo = new ProjectCreateInformation(parentSolution, new FileName(fileName ?? (@"d:\projects\Test\TestProject\" + name + ".csproj")));
			
			var project = new TestableProject(createInfo);
			((ICollection<IProject>)parentSolution.Projects).Add(project);
			return project;
		}
		
		public static TestableProject CreateTestWebApplicationProject()
		{
			TestableProject project = CreateTestProject();
			AddWebApplicationProjectType(project);
			return project;
		}
		
		public static TestableProject CreateTestWebSiteProject()
		{
			TestableProject project = CreateTestProject();
			AddWebSiteProjectType(project);
			return project;
		}
		
		public static void AddWebApplicationProjectType(MSBuildBasedProject project)
		{
			AddProjectType(project, ProjectTypeGuids.WebApplication);
		}
		
		public static void AddWebSiteProjectType(TestableProject project)
		{
			AddProjectType(project, ProjectTypeGuids.WebSite);
		}
		
		public static void AddProjectType(MSBuildBasedProject project, Guid guid)
		{
			project.AddProjectType(guid);
		}
		
		public static void AddReference(MSBuildBasedProject project, string referenceName)
		{
			var referenceProjectItem = new ReferenceProjectItem(project, referenceName);
			ProjectService.AddProjectItem(project, referenceProjectItem);
		}
		
		public static void AddFile(MSBuildBasedProject project, string fileName)
		{
			var fileProjectItem = new FileProjectItem(project, ItemType.Compile);
			fileProjectItem.FileName = FileName.Create(fileName);
			ProjectService.AddProjectItem(project, fileProjectItem);
		}
		
		public static ReferenceProjectItem GetReference(MSBuildBasedProject project, string referenceName)
		{
			foreach (ReferenceProjectItem referenceProjectItem in project.GetItemsOfType(ItemType.Reference)) {
				if (referenceProjectItem.Include == referenceName) {
					return referenceProjectItem;
				}
			}
			return null;
		}
		
		public static FileProjectItem GetFile(MSBuildBasedProject project, string fileName)
		{
			return project.FindFile(FileName.Create(fileName));
		}
		
		public static FileProjectItem GetFileFromInclude(TestableProject project, string include)
		{
			foreach (ProjectItem projectItem in project.Items) {
				FileProjectItem fileItem = projectItem as FileProjectItem;
				if (fileItem != null) {
					if (fileItem.Include == include) {
						return fileItem;
					}
				}
			}
			return null;
		}
	}
}
