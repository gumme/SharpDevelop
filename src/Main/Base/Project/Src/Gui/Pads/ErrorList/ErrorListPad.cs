﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.Core;
using ICSharpCode.Core.Presentation;
using ICSharpCode.SharpDevelop.Project;
using ICSharpCode.SharpDevelop.Workbench;

namespace ICSharpCode.SharpDevelop.Gui
{
	public class ErrorListPad : AbstractPadContent
	{
		public const string DefaultContextMenuAddInTreeEntry = "/SharpDevelop/Pads/ErrorList/TaskContextMenu";
		
		static ErrorListPad instance;
		public static ErrorListPad Instance {
			get {
				return instance;
			}
		}
		
		ToolBar toolBar;
		DockPanel contentPanel = new DockPanel();
		ListView errorView = new ListView();
		readonly ObservableCollection<SDTask> errors;
		
		Properties properties;
		
		public bool ShowErrors {
			get {
				return properties.Get<bool>("ShowErrors", true);
			}
			set {
				properties.Set<bool>("ShowErrors", value);
				InternalShowResults();
			}
		}
		
		public bool ShowMessages {
			get {
				return properties.Get<bool>("ShowMessages", true);
			}
			set {
				properties.Set<bool>("ShowMessages", value);
				InternalShowResults();
			}
		}
		
		public bool ShowWarnings {
			get {
				return properties.Get<bool>("ShowWarnings", true);
			}
			set {
				properties.Set<bool>("ShowWarnings", value);
				InternalShowResults();
			}
		}
		
		public static bool ShowAfterBuild {
			get {
				return Project.BuildOptions.ShowErrorListAfterBuild;
			}
			set {
				Project.BuildOptions.ShowErrorListAfterBuild = value;
			}
		}
		
		public override object Control {
			get {
				return contentPanel;
			}
		}
		
		public ErrorListPad()
		{
			instance = this;
			properties = PropertyService.NestedProperties("ErrorListPad");
			
			TaskService.Cleared += TaskServiceCleared;
			TaskService.Added   += TaskServiceAdded;
			TaskService.Removed += TaskServiceRemoved;
			TaskService.InUpdateChanged += delegate {
				if (!TaskService.InUpdate)
					InternalShowResults();
			};
			
			SD.BuildService.BuildFinished += ProjectServiceEndBuild;
			SD.ProjectService.SolutionOpened += OnSolutionOpen;
			SD.ProjectService.SolutionClosed += OnSolutionClosed;
			errors = new ObservableCollection<SDTask>(TaskService.Tasks.Where(t => t.TaskType != TaskType.Comment));
			
			toolBar = ToolBarService.CreateToolBar(contentPanel, this, "/SharpDevelop/Pads/ErrorList/Toolbar");
			
			contentPanel.Children.Add(toolBar);
			toolBar.SetValue(DockPanel.DockProperty, Dock.Top);
			contentPanel.Children.Add(errorView);
			errorView.ItemsSource = errors;
			errorView.MouseDoubleClick += ErrorViewMouseDoubleClick;
			errorView.Style = (Style)new TaskViewResources()["TaskListView"];
			errorView.ContextMenu = MenuService.CreateContextMenu(this, DefaultContextMenuAddInTreeEntry);
			
			errorView.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, ExecuteCopy, CanExecuteCopy));
			errorView.CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll, ExecuteSelectAll, CanExecuteSelectAll));
			
			errors.CollectionChanged += delegate { MenuService.UpdateText(toolBar.Items); };
			
			InternalShowResults();
		}
		
		void ErrorViewMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			SDTask task = errorView.SelectedItem as SDTask;
			var item = errorView.ItemContainerGenerator.ContainerFromItem(task) as ListViewItem;
			UIElement element = e.MouseDevice.DirectlyOver as UIElement;
			if (task != null && task.FileName != null && element != null && item != null
			    && element.IsDescendantOf(item)) {
				SD.FileService.JumpToFilePosition(task.FileName, task.Line, task.Column);
			}
		}
		
		void OnSolutionOpen(object sender, SolutionEventArgs e)
		{
			errors.Clear();
		}
		
		void OnSolutionClosed(object sender, EventArgs e)
		{
			errors.Clear();
		}
		
		void ProjectServiceEndBuild(object sender, EventArgs e)
		{
			if (TaskService.TaskCount > 0 && ShowAfterBuild) {
				SD.Workbench.GetPad(typeof(ErrorListPad)).BringPadToFront();
			}
		}
		
		public BuildResults BuildResults = null;
		
		void AddTask(SDTask task)
		{
			switch (task.TaskType) {
				case TaskType.Warning:
					if (!ShowWarnings) {
						return;
					}
					break;
				case TaskType.Error:
					if (!ShowErrors) {
						return;
					}
					break;
				case TaskType.Message:
					if (!ShowMessages) {
						return;
					}
					break;
				default:
					return;
			}
			
			errors.Add(task);
		}
		
		
		void TaskServiceCleared(object sender, EventArgs e)
		{
			if (TaskService.InUpdate)
				return;
			errors.Clear();
		}
		
		void TaskServiceAdded(object sender, TaskEventArgs e)
		{
			if (TaskService.InUpdate)
				return;
			AddTask(e.Task);
		}
		
		void TaskServiceRemoved(object sender, TaskEventArgs e)
		{
			if (TaskService.InUpdate)
				return;
			errors.Remove(e.Task);
		}
		
		void InternalShowResults()
		{
			errors.Clear();
			
			foreach (SDTask task in TaskService.Tasks) {
				AddTask(task);
			}
		}
		
		void CanExecuteCopy(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = errorView.SelectedItem != null;
		}
		
		void ExecuteCopy(object sender, ExecutedRoutedEventArgs e)
		{
			TaskViewResources.CopySelectionToClipboard(errorView);
		}
		
		void CanExecuteSelectAll(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = true;
		}
		
		void ExecuteSelectAll(object sender, ExecutedRoutedEventArgs e)
		{
			errorView.SelectAll();
		}
	}
}
