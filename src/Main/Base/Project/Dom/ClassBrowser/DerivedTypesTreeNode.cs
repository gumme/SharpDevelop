﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.Linq;
using ICSharpCode.Core;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.NRefactory.TypeSystem.Implementation;
using ICSharpCode.TreeView;
using ICSharpCode.SharpDevelop.Parser;
using ICSharpCode.SharpDevelop.Project;
using ICSharpCode.SharpDevelop.Refactoring;

namespace ICSharpCode.SharpDevelop.Dom.ClassBrowser
{
	/// <summary>
	/// Represents the "Derived types" sub-node of type nodes in ClassBrowser tree.
	/// </summary>
	public class DerivedTypesTreeNode : ModelCollectionTreeNode
	{
		ITypeDefinitionModel definition;
		string text;
		bool childrenLoaded;
		SimpleModelCollection<ITypeDefinitionModel> derivedTypes;
		
		public DerivedTypesTreeNode(ITypeDefinitionModel definition)
		{
			if (definition == null)
				throw new ArgumentNullException("definition");
			this.definition = definition;
			this.definition.Updated += (sender, e) => UpdateDerivedTypes();
			this.text = SD.ResourceService.GetString("MainWindow.Windows.ClassBrowser.DerivedTypes");
			derivedTypes = new SimpleModelCollection<ITypeDefinitionModel>();
			childrenLoaded = false;
		}

		protected override IModelCollection<object> ModelChildren {
			get {
				if (!childrenLoaded) {
					UpdateDerivedTypes();
					childrenLoaded = true;
				}
				return derivedTypes;
			}
		}
		
		public override SharpTreeNode FindChildNodeRecursively(Func<SharpTreeNode, bool> predicate)
		{
			// Don't search children of this node, because they are repeating type nodes from elsewhere
			return null;
		}
		
		public override bool CanFindChildNodeRecursively {
			get { return false; }
		}
		
		void UpdateDerivedTypes()
		{
			derivedTypes.Clear();
			ITypeDefinition currentTypeDef = definition.Resolve();
			if (currentTypeDef != null) {
				foreach (var derivedType in FindReferenceService.FindDerivedTypes(currentTypeDef, true)) {
					ITypeDefinitionModel derivedTypeModel = GetTypeDefinitionModel(currentTypeDef, derivedType);
					if (derivedTypeModel != null)
						derivedTypes.Add(derivedTypeModel);
				}
			}
		}
		
		ITypeDefinitionModel GetTypeDefinitionModel(ITypeDefinition mainTypeDefinition, ITypeDefinition derivedTypeDefinition)
		{
			ITypeDefinitionModel resolveTypeDefModel = null;
			var assemblyFileName = mainTypeDefinition.ParentAssembly.GetRuntimeAssemblyLocation();
			IAssemblyModel assemblyModel = null;
			
			try {
				// Try to get AssemblyModel from project list
				IProjectService projectService = SD.GetRequiredService<IProjectService>();
				if (projectService.CurrentSolution != null) {
					var projectOfAssembly = projectService.CurrentSolution.Projects.FirstOrDefault(p => p.AssemblyModel.Location == assemblyFileName);
					if (projectOfAssembly != null) {
						// We automatically have an AssemblyModel from project
						assemblyModel = projectOfAssembly.AssemblyModel;
					}
				}
				
				if (assemblyModel == null) {
					// Nothing in projects, load from assembly file
					var assemblyParserService = SD.GetService<IAssemblyParserService>();
					if (assemblyParserService != null) {
						if (assemblyFileName != null) {
							assemblyModel = assemblyParserService.GetAssemblyModel(assemblyFileName);
						}
					}
				}
				
				if (assemblyModel != null) {
					// Look in found AssemblyModel
					resolveTypeDefModel = assemblyModel.TopLevelTypeDefinitions[derivedTypeDefinition.FullTypeName];
					if (resolveTypeDefModel != null) {
						return resolveTypeDefModel;
					}
				}
			} catch (Exception) {
				// TODO Can't load the type, what to do?
			}
			
			return resolveTypeDefModel;
		}
		
		protected override System.Collections.Generic.IComparer<ICSharpCode.TreeView.SharpTreeNode> NodeComparer {
			get {
				return NodeTextComparer;
			}
		}
		
		public override object Text {
			get {
				return text;
			}
		}
		
		public override object Icon {
			get {
				return SD.ResourceService.GetImageSource("Icons.16x16.OpenFolderBitmap");
			}
		}
	}
}
