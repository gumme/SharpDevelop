﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using System.Xml;
using ICSharpCode.Core;
using ICSharpCode.NRefactory;
using ICSharpCode.SharpDevelop.Dom;
using ICSharpCode.SharpDevelop.Workbench;

namespace ICSharpCode.SharpDevelop.Project
{
	class Solution : SolutionFolder, ISolution
	{
		FileName fileName;
		DirectoryName directory;
		readonly IProjectChangeWatcher changeWatcher;
		readonly IFileService fileService;
		internal Version currVSVersion, minVSVersion;
		
		static readonly Version DefaultVSVersion = new Version(12, 0, 20827, 3);
		static readonly Version DefaultMinVSVersion = new Version(10, 0, 40219, 1);
		
		public Solution(FileName fileName, IProjectChangeWatcher changeWatcher, IFileService fileService)
		{
			this.changeWatcher = changeWatcher;
			this.fileService = fileService;
			this.ConfigurationNames = new SolutionConfigurationOrPlatformNameCollection(this, false);
			this.PlatformNames = new SolutionConfigurationOrPlatformNameCollection(this, true);
			this.projects = new SynchronizedModelCollection<IProject>(new ProjectModelCollection(this));
			this.FileName = fileName;
			base.Name = fileName.GetFileNameWithoutExtension();
			
			this.globalSections = new SynchronizedModelCollection<SolutionSection>(new NullSafeSimpleModelCollection<SolutionSection>());
			globalSections.CollectionChanged += OnSolutionSectionCollectionChanged;
			
			fileService.FileRenamed += FileServiceFileRenamed;
			fileService.FileRemoved += FileServiceFileRemoved;
		}
		
		public void Dispose()
		{
			fileService.FileRenamed -= FileServiceFileRenamed;
			fileService.FileRemoved -= FileServiceFileRemoved;
			
			changeWatcher.Dispose();
			foreach (var project in this.Projects) {
				project.Dispose();
			}
		}
		
		#region FileName
		public event EventHandler FileNameChanged = delegate { };
		
		public FileName FileName {
			get { return fileName; }
			private set {
				if (fileName != value) {
					this.fileName = value;
					this.directory = value.GetParentDirectory();
					UpdateMSBuildProperties();
					FileNameChanged(this, EventArgs.Empty);
				}
			}
		}
		
		public DirectoryName Directory {
			get { return directory; }
		}
		
		public override string Name {
			get {
				return base.Name;
			}
			set {
				throw new NotImplementedException();
			}
		}
		#endregion
		
		#region StartupProject
		public event EventHandler StartupProjectChanged = delegate { };
		
		IProject startupProject;
		
		public IProject StartupProject {
			get {
				if (startupProject == null) {
					startupProject = AutoDetectStartupProject();
				}
				return startupProject;
			}
			set {
				if (value == null || value.ParentSolution != this)
					throw new ArgumentException();
				if (startupProject != value) {
					startupProject = value;
					preferences.Set("StartupProject", value.IdGuid.ToString());
					StartupProjectChanged(this, EventArgs.Empty);
				}
			}
		}
		
		IProject AutoDetectStartupProject()
		{
			string startupProjectGuidText = preferences.Get("StartupProject", string.Empty);
			Guid startupProjectGuid;
			if (Guid.TryParse(startupProjectGuidText, out startupProjectGuid)) {
				var project = projects.FirstOrDefault(p => p.IdGuid == startupProjectGuid);
				if (project != null)
					return project;
			}
			return projects.FirstOrDefault(p => p.IsStartable);
		}
		#endregion
		
		#region Project list
		readonly IMutableModelCollection<IProject> projects; // = new SynchronizedModelCollection<IProject>(new ProjectModelCollection(this));
		
		sealed class ProjectModelCollection : SimpleModelCollection<IProject>
		{
			Solution solution;
			
			public ProjectModelCollection(Solution solution)
			{
				this.solution = solution;
			}
			
			protected override void OnCollectionChanged(IReadOnlyCollection<IProject> removedItems, IReadOnlyCollection<IProject> addedItems)
			{
				foreach (var project in addedItems) {
					project.ProjectSections.CollectionChanged += solution.OnSolutionSectionCollectionChanged;
					project.ConfigurationMapping.Changed += solution.OnProjectConfigurationMappingChanged;
					solution.OnSolutionSectionCollectionChanged(EmptyList<SolutionSection>.Instance, project.ProjectSections);
				}
				foreach (var project in removedItems) {
					project.ProjectSections.CollectionChanged -= solution.OnSolutionSectionCollectionChanged;
					project.ConfigurationMapping.Changed -= solution.OnProjectConfigurationMappingChanged;
					solution.OnSolutionSectionCollectionChanged(project.ProjectSections, EmptyList<SolutionSection>.Instance);
				}
				// If the startup project was removed, reset that property
				bool startupProjectWasRemoved = removedItems.Contains(solution.startupProject);
				if (startupProjectWasRemoved)
					solution.startupProject = null; // this will force auto-detection on the next property access
				base.OnCollectionChanged(removedItems, addedItems);
				// After the event is raised; dispose any removed projects.
				// Note that this method is only called at the end of a batch update.
				// When moving a project from one folder to another, a batch update
				// must be used to prevent the project from being disposed.
				foreach (var project in removedItems)
					project.Dispose();
				if (startupProjectWasRemoved || (solution.startupProject == null && addedItems.Contains(solution.AutoDetectStartupProject())))
					solution.StartupProjectChanged(this, EventArgs.Empty);
			}
		}
		
		public IModelCollection<IProject> Projects {
			get { return projects; }
		}
		
		internal IDisposable ReportBatch()
		{
			return projects.BatchUpdate();
		}
		
		internal void ReportRemovedItem(ISolutionItem oldItem)
		{
			if (oldItem is ISolutionFolder) {
				// recurse into removed folders
				foreach (var childItem in ((ISolutionFolder)oldItem).Items) {
					ReportRemovedItem(childItem);
				}
			} else if (oldItem is IProject) {
				projects.Remove((IProject)oldItem);
			}
		}
		
		internal void ReportAddedItem(ISolutionItem newItem)
		{
			if (newItem is ISolutionFolder) {
				// recurse into added folders
				foreach (var childItem in ((ISolutionFolder)newItem).Items) {
					ReportAddedItem(childItem);
				}
			} else if (newItem is IProject) {
				projects.Add((IProject)newItem);
			}
		}
		#endregion
		
		public IEnumerable<ISolutionItem> AllItems {
			get {
				return this.Items.Flatten(i => i is ISolutionFolder ? ((ISolutionFolder)i).Items : null);
			}
		}
		
		readonly IMutableModelCollection<SolutionSection> globalSections;
		
		public IMutableModelCollection<SolutionSection> GlobalSections {
			get { return globalSections; }
		}
		
		void OnProjectConfigurationMappingChanged(object sender, EventArgs e)
		{
			this.IsDirty = true;
		}
		
		void OnSolutionSectionCollectionChanged(IReadOnlyCollection<SolutionSection> oldItems, IReadOnlyCollection<SolutionSection> newItems)
		{
			this.IsDirty = true;
			foreach (var section in oldItems) {
				section.Changed -= OnSolutionSectionChanged;
			}
			foreach (var section in newItems) {
				section.Changed += OnSolutionSectionChanged;
			}
		}
		
		void OnSolutionSectionChanged(object sender, EventArgs e)
		{
			this.IsDirty = true;
		}
		
		public ISolutionItem GetItemByGuid(Guid guid)
		{
			// Maybe we should maintain a dictionary to make these lookups faster?
			// But I don't think lookups by GUID are commonly used...
			return this.AllItems.FirstOrDefault(i => i.IdGuid == guid);
		}
		
		#region Preferences
		Properties preferences = new Properties();
		
		public Properties Preferences {
			get { return preferences; }
		}
		
		string GetPreferencesKey()
		{
			return "solution:" + fileName.ToString().ToUpperInvariant();
		}
		
		internal void LoadPreferences()
		{
			try {
				preferences = SD.PropertyService.LoadExtraProperties(GetPreferencesKey());
			} catch (IOException) {
			} catch (XmlException) {
				// ignore errors about inaccessible or malformed files
			}
			// Load active configuration from preferences
			CreateDefaultConfigurationsIfMissing();
			this.ActiveConfiguration = ConfigurationAndPlatform.FromKey(preferences.Get("ActiveConfiguration", "Debug|Any CPU"));
			ValidateConfiguration();
			// We can't set the startup project property yet; LoadPreferences() is called before
			// the projects are loaded into the solution.
			// This is necessary so that the projects can be loaded in the correct configuration
			// - we avoid an expensive configuration switch during solution load.
		}
		
		public event EventHandler PreferencesSaving = delegate { };
		
		public void SavePreferences()
		{
			preferences.Set("ActiveConfiguration.Configuration", activeConfiguration.Configuration);
			preferences.Set("ActiveConfiguration.Platform", activeConfiguration.Platform);
			PreferencesSaving(this, EventArgs.Empty);
			
			try {
				SD.PropertyService.SaveExtraProperties(GetPreferencesKey(), preferences);
			} catch (IOException) {
				// ignore errors writing to extra properties
			}
		}
		#endregion
		
		#region Save
		public void Save()
		{
			try {
				changeWatcher.Disable();
				using (var solutionWriter = new SolutionWriter(fileName)) {
					var version = ComputeSolutionVersion();
					solutionWriter.WriteFormatHeader(version);
					solutionWriter.WriteSolutionVersionProperties(version, currVSVersion ?? DefaultVSVersion, minVSVersion ?? DefaultMinVSVersion);
					solutionWriter.WriteSolutionItems(this);
					solutionWriter.WriteGlobalSections(this);
				}
				changeWatcher.Enable();
			} catch (IOException ex) {
				MessageService.ShowErrorFormatted("${res:SharpDevelop.Solution.CannotSave.IOException}", fileName, ex.Message);
			} catch (UnauthorizedAccessException ex) {
				FileAttributes attributes = File.GetAttributes(fileName);
				if ((FileAttributes.ReadOnly & attributes) == FileAttributes.ReadOnly) {
					MessageService.ShowErrorFormatted("${res:SharpDevelop.Solution.CannotSave.ReadOnly}", fileName);
				}
				else
				{
					MessageService.ShowErrorFormatted
						("${res:SharpDevelop.Solution.CannotSave.UnauthorizedAccessException}", fileName, ex.Message);
				}
			}
		}
		
		SolutionFormatVersion ComputeSolutionVersion()
		{
			SolutionFormatVersion version = SolutionFormatVersion.VS2005;
			foreach (var project in this.Projects) {
				if (project.MinimumSolutionVersion > version)
					version = project.MinimumSolutionVersion;
			}
			
			if ((minVSVersion != null || currVSVersion != null) && version < SolutionFormatVersion.VS2012)
				version = SolutionFormatVersion.VS2012;
			return version;
		}
		#endregion
		
		#region MSBuildProjectCollection
		readonly Microsoft.Build.Evaluation.ProjectCollection msBuildProjectCollection = new Microsoft.Build.Evaluation.ProjectCollection();
		
		public Microsoft.Build.Evaluation.ProjectCollection MSBuildProjectCollection {
			get { return msBuildProjectCollection; }
		}
		
		void UpdateMSBuildProperties()
		{
			var dict = new Dictionary<string, string>();
			MSBuildInternals.AddMSBuildSolutionProperties(this, dict);
			foreach (var pair in dict) {
				msBuildProjectCollection.SetGlobalProperty(pair.Key, pair.Value);
			}
		}
		#endregion
		
		#region Handle FileService.FileRenamed / FileRemoved
		void FileServiceFileRenamed(object sender, FileRenameEventArgs e)
		{
			string oldName = e.SourceFile;
			string newName = e.TargetFile;
			foreach (ISolutionFileItem fileItem in this.AllItems.OfType<ISolutionFileItem>()) {
				if (FileUtility.IsBaseDirectory(oldName, fileItem.FileName)) {
					string newFullName = FileUtility.RenameBaseDirectory(fileItem.FileName, oldName, newName);
					fileItem.FileName = FileName.Create(newFullName);
				}
			}
			
			foreach (IProject project in this.Projects) {
				if (FileUtility.IsBaseDirectory(project.Directory, oldName)) {
					foreach (ProjectItem item in project.Items) {
						if (FileUtility.IsBaseDirectory(oldName, item.FileName)) {
							SD.GetRequiredService<IProjectServiceRaiseEvents>().RaiseProjectItemRemoved(new ProjectItemEventArgs(project, item));
							item.FileName = FileName.Create(FileUtility.RenameBaseDirectory(item.FileName, oldName, newName));
							SD.GetRequiredService<IProjectServiceRaiseEvents>().RaiseProjectItemAdded(new ProjectItemEventArgs(project, item));
						}
					}
				}
			}
		}
		
		void FileServiceFileRemoved(object sender, FileEventArgs e)
		{
			string fileName = e.FileName;
			
			foreach (ISolutionFileItem fileItem in this.AllItems.OfType<ISolutionFileItem>().ToArray()) {
				if (FileUtility.IsBaseDirectory(fileName, fileItem.FileName)) {
					fileItem.ParentFolder.Items.Remove(fileItem);
				}
			}
			
			foreach (IProject project in this.Projects) {
				if (FileUtility.IsBaseDirectory(project.Directory, fileName)) {
					foreach (ProjectItem item in project.Items.ToArray()) {
						if (FileUtility.IsBaseDirectory(fileName, item.FileName)) {
							project.Items.Remove(item);
						}
					}
				}
			}
		}
		#endregion
		
		public bool IsReadOnly {
			get {
				try {
					FileAttributes attributes = File.GetAttributes(fileName);
					return ((FileAttributes.ReadOnly & attributes) == FileAttributes.ReadOnly);
				} catch (FileNotFoundException) {
					return false;
				} catch (DirectoryNotFoundException) {
					return true;
				}
			}
		}
		
		#region IConfigurable implementation
		ConfigurationAndPlatform activeConfiguration = new ConfigurationAndPlatform("Debug", "AnyCPU");
		
		public ConfigurationAndPlatform ActiveConfiguration {
			get { return activeConfiguration; }
			set {
				if (value.Configuration == null || value.Platform == null)
					throw new ArgumentNullException();
				
				if (activeConfiguration != value) {
					activeConfiguration = value;
					
					// Apply changed configuration to all projects:
					foreach (var project in this.Projects) {
						project.ActiveConfiguration = project.ConfigurationMapping.GetProjectConfiguration(value);
					}
					
					ActiveConfigurationChanged(this, EventArgs.Empty);
				}
			}
		}
		
		public event EventHandler ActiveConfigurationChanged = delegate { };
		
		public IConfigurationOrPlatformNameCollection ConfigurationNames { get; private set; }
		public IConfigurationOrPlatformNameCollection PlatformNames { get; private set; }
		
		void CreateDefaultConfigurationsIfMissing()
		{
			if (this.ConfigurationNames.Count == 0) {
				this.ConfigurationNames.Add("Debug");
				this.ConfigurationNames.Add("Release");
			}
			if (this.PlatformNames.Count == 0) {
				this.PlatformNames.Add("Any CPU");
			}
		}
		
		void ValidateConfiguration()
		{
			string config = this.ActiveConfiguration.Configuration;
			string platform = this.ActiveConfiguration.Platform;
			if (!this.ConfigurationNames.Contains(config))
				config = this.ConfigurationNames.First();
			if (!this.PlatformNames.Contains(platform))
				platform = this.PlatformNames.First();
			this.ActiveConfiguration = new ConfigurationAndPlatform(config, platform);
		}
		#endregion
		
		#region ICanBeDirty implementation
		public event EventHandler IsDirtyChanged;
		
		bool isDirty;
		
		public bool IsDirty {
			get { return isDirty; }
			set {
				if (isDirty != value) {
					isDirty = value;
					
					if (IsDirtyChanged != null) {
						IsDirtyChanged(this, EventArgs.Empty);
					}
				}
			}
		}
		#endregion
		
		public override string ToString()
		{
			return "[Solution " + fileName + " with " + projects.Count + " projects]";
		}
		
		public Version VSVersion {
			get { return currVSVersion; }
		}
		
		public Version MinVSVersion {
			get { return minVSVersion; }
		}
	}
}
