﻿// Copyright (c) 2014 AlphaSierraPapa for the SharpDevelop Team
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
using System.Collections;
using System.ComponentModel.Design;
using System.ComponentModel.Design.Serialization;
using System.Data;
using System.Drawing.Design;
using System.Drawing.Printing;
using System.IO;
using System.Text;
using System.Windows.Forms;

using ICSharpCode.Core;
using ICSharpCode.Reports.Core;
using ICSharpCode.Reports.Core.Exporter;
using ICSharpCode.Reports.Core.Exporter.ExportRenderer;

using ICSharpCode.Reports.Core.Globals;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Gui;
using ICSharpCode.SharpDevelop.WinForms;
using ICSharpCode.SharpDevelop.Workbench;
using ICSharpCode.Reports.Addin.Commands;
using ICSharpCode.Reports.Addin.Designer;
using ICSharpCode.Reports.Addin.SecondaryViews;

namespace ICSharpCode.Reports.Addin
{
	/// <summary>
	/// Description of the view content
	/// </summary>
	public class ReportDesignerView : AbstractViewContent, IHasPropertyContainer,
	IClipboardHandler,IUndoHandler, IToolsHost,IPrintable
	{
		readonly IDesignerGenerator generator;
		bool IsFormsDesignerVisible;
		bool tabOrderMode;
		bool hasUnmergedChanges;
		bool unloading;
		string reportFileContent;
		Panel panel;
		ReportDesignerLoader loader;
		DesignSurface designSurface;
		DefaultServiceContainer defaultServiceContainer;
		ReportDesignerUndoEngine undoEngine;
		
		XmlView xmlView;
		ReportPreview reportPreview;
		ReportViewerSecondaryView reportViewer;
//		TestWPFReportPreview testView;
		
		#region Constructor
		
		/// <summary>
		/// Creates a new ReportDesignerView object
		/// </summary>
		
		public ReportDesignerView(OpenedFile openedFile, IDesignerGenerator generator):base (openedFile)
		{
			if (openedFile == null) {
				throw new ArgumentNullException("openedFile");
			}
			if (generator == null) {
				throw new ArgumentNullException("generator");
			}
			Console.WriteLine("ReportDesignerView");
		
			this.generator = generator;
			this.generator.Attach(this);
			
			base.TabPageText = ResourceService.GetString("SharpReport.Design");
			ReportingSideTabProvider.AddViewContent(this);
		}
		
		/// <summary>
		/// This constructor allows running in unit test mode with a mock file. Get it from Matt Ward
		/// </summary>
		public ReportDesignerView(IViewContent primaryViewContent, OpenedFile mockFile)
//			: this(primaryViewContent)
		{
//			this.sourceCodeStorage.AddFile(mockFile, Encoding.UTF8);
//			this.sourceCodeStorage.DesignerCodeFile = mockFile;
		}
		
		
		private void SetupSecondaryView ()
		{
			LoggingService.Info("Form Designer: SetupSecondaryView ");
			xmlView = new XmlView(generator,this);
			SecondaryViewContents.Add(xmlView);
			
			reportPreview = new ReportPreview(loader,this);
			SecondaryViewContents.Add(reportPreview);
			
			reportViewer = new ReportViewerSecondaryView(loader,this);
			SecondaryViewContents.Add(reportViewer);
			
			var wpfViewer = new WPFReportPreview(loader,this);
			SecondaryViewContents.Add(wpfViewer);
			
//			testView = new TestWPFReportPreview(loader,this);
//			SecondaryViewContents.Add(testView);
			
		}
		
		#endregion
		
		#region setup designer
		
		private void LoadDesigner(Stream stream)
		{
			LoggingService.Info("Form Designer: LoadDesigner");
			CreatePanel();
			defaultServiceContainer = new DefaultServiceContainer();
			
			defaultServiceContainer.AddService(typeof(System.Windows.Forms.Design.IUIService),
			                                   new UIService());

			defaultServiceContainer.AddService(typeof(IToolboxService),new ToolboxService());
			defaultServiceContainer.AddService(typeof(IHelpService), new HelpService());
			
			this.designSurface = CreateDesignSurface(defaultServiceContainer);
			designSurface.Loading += this.DesignerLoading;
			designSurface.Loaded += this.DesignerLoaded;
			designSurface.Flushed += this.DesignerFlushed;
			designSurface.Unloading += this.DesingerUnloading;
			
			AmbientProperties ambientProperties = new AmbientProperties();
			defaultServiceContainer.AddService(typeof(AmbientProperties), ambientProperties);
			
			defaultServiceContainer.AddService(typeof(ITypeResolutionService), new TypeResolutionService());
			
			defaultServiceContainer.AddService(typeof(ITypeDiscoveryService),
			                                   new TypeDiscoveryService());

			defaultServiceContainer.AddService(typeof(System.ComponentModel.Design.IMenuCommandService),
			                                   new MenuCommandService(panel,this.designSurface ));
			
			defaultServiceContainer.AddService(typeof(MemberRelationshipService),
			                                   new DefaultMemberRelationshipService());
			
			//need this to resolve the filename and manipulate
			//ReportSettings in ReportDefinitionDeserializer.LoadObjectFromXmlDocument
			//if the filename in ReportSettings is different from load location
			
			defaultServiceContainer.AddService(typeof(OpenedFile),base.PrimaryFile);
			
			DesignerOptionService dos = new System.Windows.Forms.Design.WindowsFormsDesignerOptionService();
			dos.Options.Properties.Find( "UseSmartTags", true ).SetValue( dos, true );
			dos.Options.Properties.Find( "ShowGrid", true ).SetValue( dos, false );
			dos.Options.Properties.Find( "UseSnapLines", true ).SetValue( dos, true );
			defaultServiceContainer.AddService( typeof( DesignerOptionService ), dos );
			this.loader = new ReportDesignerLoader(generator,stream);
			this.designSurface.BeginLoad(this.loader);
			if (!designSurface.IsLoaded) {
				throw new FormsDesignerLoadException(FormatLoadErrors(designSurface));
			}
			defaultServiceContainer.AddService(typeof(System.ComponentModel.Design.Serialization.INameCreationService),
			                                   new NameCreationService());
			
			ISelectionService selectionService = (ISelectionService)this.designSurface.GetService(typeof(ISelectionService));
			selectionService.SelectionChanged  += SelectionChangedHandler;
			
			undoEngine = new ReportDesignerUndoEngine(Host);
			
			IComponentChangeService componentChangeService = (IComponentChangeService)this.designSurface.GetService(typeof(IComponentChangeService));
			
			componentChangeService.ComponentChanged += OnComponentChanged;
			componentChangeService.ComponentAdded   += OnComponentListChanged;
			componentChangeService.ComponentRemoved += OnComponentListChanged;
			componentChangeService.ComponentRename  += OnComponentListChanged;
			
			this.Host.TransactionClosed += TransactionClose;
			
			UpdatePropertyPad();
			hasUnmergedChanges = false;
			
			LoggingService.Info("Form Designer: END INITIALIZE");
		}
		
		
		void DesignerLoading(object sender, EventArgs e)
		{
			LoggingService.Debug("Forms designer: DesignerLoader loading...");
			this.unloading = false;
		}
		
		
		void DesignerLoaded(object sender, LoadedEventArgs e)
		{
			// This method is called when the designer has loaded.
			LoggingService.Debug("Report designer: DesignerLoader loaded, HasSucceeded=" + e.HasSucceeded.ToString());
			this.unloading = false;
			
			if (e.HasSucceeded) {

				SetupDesignSurface();
				this.IsFormsDesignerVisible = true;
				generator.MergeFormChanges(null);
				StartReportExplorer ();

				LoggingService.Debug("FormsDesigner loaded, setting ActiveDesignSurface to " + this.designSurface.ToString());
				designSurfaceManager.ActiveDesignSurface = this.designSurface;
				this.UpdatePropertyPad();
			}
		}
		
		
		
		private void CreatePanel ()
		{
			this.panel = new Panel();
			this.panel.Dock = DockStyle.Fill;
			this.panel.BackColor = System.Drawing.Color.LightBlue;
		}
		
		
		private void SetupDesignSurface()
		{
			Control c = null;
			c = this.designSurface.View as Control;
			c.Parent = this.panel;
			c.Dock = DockStyle.Fill;
		}
			
		private void DesingerUnloading(object sender, EventArgs e)
		{
			LoggingService.Debug("Forms designer: DesignerLoader unloading...");
			this.unloading = true;
		}
		
		
		private void DesignerFlushed(object sender, EventArgs e)
		{
			System.Diagnostics.Trace.WriteLine("View:DesignerFlushed");
			this.hasUnmergedChanges = false;
		}
		
		
		private static string FormatLoadErrors(DesignSurface designSurface)
		{
			var sb = new StringBuilder();
			foreach(Exception le in designSurface.LoadErrors) {
				sb.AppendLine(le.ToString());
				sb.AppendLine();
			}
			return sb.ToString();
		}
		#endregion

		private void MergeFormChanges()
		{
			LoggingService.Info("MergeFormChanges()");
			designSurface.Flush();
			generator.MergeFormChanges(null);
			LoggingService.Info("Finished merging form changes");
			hasUnmergedChanges = false;
		}
		
		
		public string ReportFileContent {
			get {
				if (this.IsDirty) {
					this.MergeFormChanges();
				}
				return this.reportFileContent; }
			set { this.reportFileContent = value; }
		}
		
		#region ReportExplorer
		
		private void StartReportExplorer ()
		{
			LoggingService.Info("StartReportExplorer ()");
			ReportExplorerPad explorerPad = CheckReportExplorer();
			SD.Workbench.GetPad(typeof(ReportExplorerPad)).BringPadToFront();
			explorerPad.AddContent(this.loader.ReportModel);
			explorerPad.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler(ReportExplorer_PropertyChanged);
		}
		
		
		private void ReportExplorer_PropertyChanged (object sender,System.ComponentModel.PropertyChangedEventArgs e)
		{
			LoggingService.Info("ReportExplorer_PropertyChanged");
			this.MakeDirty();
			ReportExplorerPad explorerPad = CheckReportExplorer();
			IComponentChangeService change = Host.GetService(typeof(IComponentChangeService)) as IComponentChangeService;
			change.OnComponentChanged(explorerPad, null, null, null);
		}
		
		
		private static ReportExplorerPad CheckReportExplorer ()
		{
			ReportExplorerPad p = ReportExplorerPad.Instance;
			if (p == null) {
				SD.Workbench.GetPad(typeof(ReportExplorerPad)).CreatePad();
			}
			return ReportExplorerPad.Instance;
		}
		
		#endregion
		
		#region SelectionService
		
		private void SelectionChangedHandler(object sender, EventArgs args)
		{
			var selectionService = (ISelectionService)sender;
			var item = selectionService.PrimarySelection as AbstractItem;
			if (item != null) {
				if (String.IsNullOrEmpty(item.Site.Name)) {
					item.Site.Name = item.Name;
				}
			}
			UpdatePropertyPadSelection((ISelectionService)sender);
		}
		
		#endregion
		
		#region Transaction
		
		bool shouldUpdateSelectableObjects;
		
		void TransactionClose(object sender, DesignerTransactionCloseEventArgs e)
		{
			if (shouldUpdateSelectableObjects) {
				SD.MainThread.InvokeAsync(UpdatePropertyPad).FireAndForget();
				shouldUpdateSelectableObjects = false;
			}
		}
		
		
		#endregion
		
		#region ComponentChangeService
		
		private void OnComponentChanged (object sender, ComponentChangedEventArgs e)
		{
			var item = e.Component as BaseImageItem;
			
			if (item != null) {
				item.ReportFileName = this.loader.ReportModel.ReportSettings.FileName;
			}
			
			bool loading = this.loader != null && this.loader.Loading;
			LoggingService.Debug("ReportDesignerView: ComponentChanged: " + (e.Component == null ? "<null>" : e.Component.ToString()) + ", Member=" + (e.Member == null ? "<null>" : e.Member.Name) + ", OldValue=" + (e.OldValue == null ? "<null>" : e.OldValue.ToString()) + ", NewValue=" + (e.NewValue == null ? "<null>" : e.NewValue.ToString()) + "; Loading=" + loading + "; Unloading=" + this.unloading);
			if (!loading && !unloading) {
				this.MakeDirty();
			}
			MergeFormChanges();
		}
		
		
		void OnComponentListChanged(object sender, EventArgs e)
		{
			bool loading = this.loader != null && this.loader.Loading;
			LoggingService.Debug("ReportDesigner: Component added/removed/renamed, Loading=" + loading + ", Unloading=" + this.unloading);
			if (!loading && !unloading) {
				shouldUpdateSelectableObjects = true;
				this.MakeDirty();
			}
		}
		
		private void MakeDirty()
		{
			hasUnmergedChanges = true;
			this.PrimaryFile.MakeDirty();
		}
		
		#endregion
	
		#region HasPropertyContainer implementation
		
		private void UpdatePropertyPad()
		{
			Console.WriteLine("UpdatePropertyPad()");
			if (IsFormsDesignerVisible && Host != null) {
				PropertyContainer.Host = Host;
				PropertyContainer.SelectableObjects = Host.Container.Components;
				ISelectionService selectionService = (ISelectionService)this.designSurface.GetService(typeof(ISelectionService));
				if (selectionService != null) {
					UpdatePropertyPadSelection(selectionService);
				}
			}
		}
		
		
		private void UpdatePropertyPadSelection(ISelectionService selectionService)
		{
			ICollection selection = selectionService.GetSelectedComponents();
			object[] selArray = new object[selection.Count];
			selection.CopyTo(selArray, 0);
			PropertyContainer.SelectedObjects = selArray;
		}
		
		
		#endregion
		
		#region IHasPropertyContainer impementation
		
		PropertyContainer propertyContainer;
		
		public PropertyContainer PropertyContainer {
			get {
				if (propertyContainer == null) {
					propertyContainer = new PropertyContainer();
				}
				return propertyContainer;
			}
		}
		
		#endregion
		
		#region IUnDohandler
		
		public bool EnableUndo {
			get {
				if (undoEngine != null) {
					return undoEngine.EnableUndo;
				}
				return false;
			}
		}
		
		
		public bool EnableRedo {
			get {
				if (undoEngine != null) {
					return undoEngine.EnableRedo;
				}
				return false;
			}
		}
		
		
		public virtual void Undo()
		{
			if (undoEngine != null) {
				undoEngine.Undo();
			}
		}
		
		
		public virtual void Redo()
		{
			if (undoEngine != null) {
				undoEngine.Redo();
			}
		}
		
		#endregion
		
		#region IClipboardHandler implementation
		
		private bool IsMenuCommandEnabled(CommandID commandID)
		{
			if (designSurface == null) {
				return false;
			}
			
			IMenuCommandService menuCommandService = (IMenuCommandService)designSurface.GetService(typeof(IMenuCommandService));
			if (menuCommandService == null) {
				return false;
			}
			
			System.ComponentModel.Design.MenuCommand menuCommand = menuCommandService.FindCommand(commandID);
			if (menuCommand == null) {
				return false;
			}
			return menuCommand.Enabled;
		}
		
		
		public bool EnableCut {
			get {
				return IsMenuCommandEnabled(StandardCommands.Cut);
			}
		}
		
		
		public bool EnableCopy {
			get {
				return IsMenuCommandEnabled(StandardCommands.Copy);
			}
		}
		
		
		const string ComponentClipboardFormat = "CF_DESIGNERCOMPONENTS";
		public bool EnablePaste {
			get {
				return IsMenuCommandEnabled(StandardCommands.Paste);
			}
		}
		
		
		public bool EnableDelete {
			get {
				return IsMenuCommandEnabled(StandardCommands.Delete);
			}
		}
		
		public bool EnableSelectAll {
			get {
				return designSurface != null;
			}
		}
		
		
		public void Cut()
		{
			IMenuCommandService menuCommandService = (IMenuCommandService)designSurface.GetService(typeof(IMenuCommandService));
			menuCommandService.GlobalInvoke(StandardCommands.Cut);
		}
		
		
		public void Copy()
		{
			IMenuCommandService menuCommandService = (IMenuCommandService)designSurface.GetService(typeof(IMenuCommandService));
			menuCommandService.GlobalInvoke(StandardCommands.Copy);
		}
		
		public void Paste()
		{
			IMenuCommandService menuCommandService = (IMenuCommandService)designSurface.GetService(typeof(IMenuCommandService));
			menuCommandService.GlobalInvoke(StandardCommands.Paste);
		}
		
		public void Delete()
		{
			IMenuCommandService menuCommandService = (IMenuCommandService)designSurface.GetService(typeof(IMenuCommandService));
			menuCommandService.GlobalInvoke(StandardCommands.Delete);
		}
		
		public void SelectAll()
		{
			throw new NotImplementedException();
		}
		
		#endregion
		
		#region IToolsHost
		
		object IToolsHost.ToolsContent {
			get {
				return ReportingSideTabProvider.ReportingSideBar;
			}
		}
		
		#endregion
		
		#region IPrintable
		
		
		public PrintDocument PrintDocument
		{
			get {
				Console.WriteLine("");
				Console.WriteLine("----ReportdesignerView:PrintDocument------");
				Console.WriteLine("");
				ReportModel model = loader.CreateRenderableModel();
				IReportCreator reportCreator = null;
				var  paramCmd = new CollectParametersCommand(model.ReportSettings);
				paramCmd.Run();
				switch (model.DataModel) {
						case GlobalEnums.PushPullModel.FormSheet :
						{
							reportCreator = FormPageBuilder.CreateInstance(model);
							break;
						}
						case GlobalEnums.PushPullModel.PullData:
						{
							IDataManager dataManager = DataManagerFactory.CreateDataManager(model,(ReportParameters)null);
							reportCreator = DataPageBuilder.CreateInstance(model,dataManager);
							break;
						}
						case GlobalEnums.PushPullModel.PushData:{
							var cmd = new ICSharpCode.Reports.Addin.Commands.DataSetFromXsdCommand();
							cmd.Run();
							DataSet ds = cmd.DataSet;
							IDataManager dataManager = DataManagerFactory.CreateDataManager(model,ds.Tables[0]);
							reportCreator = DataPageBuilder.CreateInstance(model,dataManager);
							break;
						}
					default:
						throw new InvalidReportModelException();
				}
				
				reportCreator.BuildExportList();
				PrintRenderer printer = PrintRenderer.CreateInstance(reportCreator.Pages);
				printer.Start();
				printer.RenderOutput();
				printer.End();
				return printer.PrintDocument;
			}
		}
	
		#endregion
		
		#region IDesignerHost implementation
		
		public IDesignerHost Host {
			get {
				return this.designSurface.GetService(typeof(IDesignerHost)) as IDesignerHost;
			}
		}
		
		#endregion

		#region Commands
		
		public void ShowSourceCode()
		{
			WorkbenchWindow.SwitchView(1);
		}
		
		public void TogglePageMargin ()
		{
			IDesignerHost designerHost = (IDesignerHost)this.designSurface.GetService(typeof(IDesignerHost));
			RootReportModel rootReportModel = (RootReportModel)designerHost.RootComponent;
			rootReportModel.Toggle();

		}
		#endregion
		
		#region Tab Order Handling
		
		public virtual bool IsTabOrderMode {
			get {
				return tabOrderMode;
			}
		}
		
		public virtual void ShowTabOrder()
		{
			if (!IsTabOrderMode) {
				var menuCommandService = (IMenuCommandService)designSurface.GetService(typeof(IMenuCommandService));
				menuCommandService.GlobalInvoke(StandardCommands.TabOrder);
				tabOrderMode = true;
			}
		}
		
		public virtual void HideTabOrder()
		{
			if (IsTabOrderMode) {
				var menuCommandService = (IMenuCommandService)designSurface.GetService(typeof(IMenuCommandService));
				menuCommandService.GlobalInvoke(StandardCommands.TabOrder);
				tabOrderMode = false;
			}
		}
		#endregion
		
		#region Design surface manager (static)
		
		static readonly DesignSurfaceManager designSurfaceManager = new DesignSurfaceManager();
		
		public static DesignSurface CreateDesignSurface(IServiceProvider serviceProvider)
		{
			return designSurfaceManager.CreateDesignSurface(serviceProvider);
		}
		
		#endregion
		
		#region overrides
		
		public override object Control {
			get {
				return panel;
			}
		}
		
		
		public override void Load(OpenedFile file, Stream stream)
		{
			LoggingService.Debug("ReportDesigner: Load from: " + file.FileName);
			base.Load(file, stream);
			this.LoadDesigner(stream);
			this.SetupSecondaryView();
		}
		
		
		public override void Save(OpenedFile file,Stream stream)
		{
			LoggingService.Debug("ReportDesigner: Save to: " + file.FileName);
			
			if (hasUnmergedChanges) {
				this.MergeFormChanges();
			}
			using(StreamWriter writer = new StreamWriter(stream)) {
				writer.Write(this.ReportFileContent);
			}
		}
		
		
		/// <summary>
		/// Cleans up all used resources
		/// </summary>
		public sealed override void Dispose()
		{
			try {
				
				IComponentChangeService componentChangeService = (IComponentChangeService)this.designSurface.GetService(typeof(IComponentChangeService));
				if (componentChangeService != null) {
					componentChangeService.ComponentChanged -= OnComponentChanged;
					componentChangeService.ComponentAdded   -= OnComponentListChanged;
					componentChangeService.ComponentRemoved -= OnComponentListChanged;
					componentChangeService.ComponentRename  -= OnComponentListChanged;
				}
				
				ISelectionService selectionService = (ISelectionService)this.designSurface.GetService(typeof(ISelectionService));
				if (selectionService != null) {
					selectionService.SelectionChanged  -= SelectionChangedHandler;
				}
				

				if (this.loader != null) {
					this.loader.Dispose();
				}
				
				
				if (this.defaultServiceContainer != null) {
					this.defaultServiceContainer.Dispose();
				}
				
				if (this.undoEngine != null) {
					this.undoEngine.Dispose();
				}
				if (this.xmlView != null) {
					this.xmlView.Dispose();
				}
				if (this.reportPreview != null) {
					this.reportPreview.Dispose();
				}
				if (this.reportViewer != null) {
					this.reportViewer.Dispose();
				}
				if (this.panel != null) {
					this.panel.Dispose();
				}
				
				if (this.Host != null) {
					this.Host.TransactionClosed -= TransactionClose;
				}
				
			} finally {
				base.Dispose();
				
			}
		}
		
		#endregion
	}
	
}
