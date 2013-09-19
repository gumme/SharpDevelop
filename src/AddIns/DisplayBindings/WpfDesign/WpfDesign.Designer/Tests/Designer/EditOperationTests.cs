﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

using ICSharpCode.WpfDesign.Designer.Xaml;
using ICSharpCode.WpfDesign.XamlDom;
using NUnit.Framework;

namespace ICSharpCode.WpfDesign.Tests.Designer
{
	[TestFixture]
	public class EditOperationTests : ModelTestHelper
	{
		protected override XamlLoadSettings CreateXamlLoadSettings()
		{
			var settings = base.CreateXamlLoadSettings();
			
			settings.TypeFinder.RegisterAssembly(typeof(NamespaceTests).Assembly);
			
			return settings;
		}
		
		Mutex mutex;
		
		[TestFixtureSetUp]
		public void FixtureSetUp()
		{
			// Avoid test failure on build server when unit tests for several branches are run concurrently.
			bool createdNew;
			mutex = new Mutex(true, "ClipboardUnitTest", out createdNew);
			if (!createdNew) {
				if (!mutex.WaitOne(10000)) {
					throw new Exception("Could not acquire mutex");
				}
			}
		}
		
		[TestFixtureTearDown]
		public void FixtureTearDown()
		{
			mutex.ReleaseMutex();
			mutex.Dispose();
		}
		
		[Test]
		public void Cut()
		{
			var grid = CreateGridContextWithDesignSurface("<Button/><Button/>");
			var xamlContext = grid.Context as XamlDesignContext;
			if (xamlContext != null) {
				xamlContext.XamlEditAction.Cut(grid.ContentProperty.CollectionElements);
				var cutXaml = Clipboard.GetText(TextDataFormat.Xaml);
				Assert.AreEqual("<Buttonxmlns=\"" + XamlConstants.PresentationNamespace + "\"/>" + xamlContext.XamlEditAction.Delimeter + "<Buttonxmlns=\"" + XamlConstants.PresentationNamespace + "\"/>" + xamlContext.XamlEditAction.Delimeter, cutXaml.Replace(" ", ""));
				Assert.AreEqual(0, grid.ContentProperty.CollectionElements.Count);
			} else {
				Assert.Fail();
			}
		}

		[Test]
		public void Copy()
		{
			var grid = CreateGridContextWithDesignSurface("<Button/><Button/>");
			var xamlContext = grid.Context as XamlDesignContext;
			if (xamlContext != null) {
				xamlContext.XamlEditAction.Copy(grid.ContentProperty.CollectionElements);
				var cutXaml = Clipboard.GetText(TextDataFormat.Xaml);
				Assert.AreEqual("<Buttonxmlns=\"" + XamlConstants.PresentationNamespace + "\"/>" + xamlContext.XamlEditAction.Delimeter + "<Buttonxmlns=\"" + XamlConstants.PresentationNamespace + "\"/>" + xamlContext.XamlEditAction.Delimeter, cutXaml.Replace(" ", ""));
				Assert.AreEqual(2, grid.ContentProperty.CollectionElements.Count);
			} else {
				Assert.Fail();
			}
		}

		private string _name;

		private DesignItem IntializePasteOperationsTest()
		{
			var grid = CreateGridContextWithDesignSurface("<Button Name=\"TestElement\"/><Grid><Button/></Grid><Window/><Image/>");
			Assert.IsNotNull(grid);
			var xamlConxtext = grid.Context as XamlDesignContext;
			if (xamlConxtext != null) {
				_name = grid.ContentProperty.CollectionElements[0].Name;
				xamlConxtext.XamlEditAction.Cut(new[] {grid.ContentProperty.CollectionElements[0]});
			} else
				Assert.Fail();
			return grid;
		}

		private DesignItem IntializePasteOperationsCannotAddTest()
		{
			var grid = CreateGridContextWithDesignSurface("<Button Name=\"TestElement\"/><Grid><Image/></Grid><Window/><ListBox/>");
			Assert.IsNotNull(grid);
			var xamlConxtext = grid.Context as XamlDesignContext;
			if (xamlConxtext != null) {
				_name = grid.ContentProperty.CollectionElements[0].Name;
				xamlConxtext.XamlEditAction.Cut(new[] {grid.ContentProperty.CollectionElements[0]});
			} else
				Assert.Fail();
			return grid;
		}
		
		[Test]
		public void PasteWhenContentControlSelectedAndCannotAdd()
		{
			var grid = IntializePasteOperationsCannotAddTest();
			var xamlContext = grid.Context as XamlDesignContext;
			Assert.IsNotNull(xamlContext);

			var selection = grid.Services.Selection;
			var innerGrid = grid.ContentProperty.CollectionElements[0];
			selection.SetSelectedComponents(innerGrid.ContentProperty.CollectionElements);
			xamlContext.XamlEditAction.Paste();
			Assert.AreEqual(_name, innerGrid.ContentProperty.CollectionElements[1].Name);			
			Assert.AreEqual(innerGrid.ContentProperty.CollectionElements[1], selection.PrimarySelection);
		}

		[Test]
		public void PasteWhenContentControlSelectedAndCanAdd()
		{
			var grid = IntializePasteOperationsTest();
			var xamlContext = grid.Context as XamlDesignContext;
			Assert.IsNotNull(xamlContext);

			var selection = grid.Services.Selection;
			var window = grid.ContentProperty.CollectionElements[1];
			selection.SetSelectedComponents(new[] {window});
			xamlContext.XamlEditAction.Paste();
			Assert.AreEqual(_name, window.ContentProperty.Value.Name);
			Assert.AreEqual(window.ContentProperty.Value, selection.PrimarySelection);
		}

		[Test]
		public void PasteWhenIAddChildSelectedAndCanAdd()
		{
			var grid = IntializePasteOperationsTest();
			var xamlContext = grid.Context as XamlDesignContext;
			Assert.IsNotNull(xamlContext);

			var selection = grid.Services.Selection;
			selection.SetSelectedComponents(new[] {grid});
			xamlContext.XamlEditAction.Paste();
			Assert.AreEqual(_name, grid.ContentProperty.CollectionElements[3].Name);
			Assert.AreEqual(grid.ContentProperty.CollectionElements[3], selection.PrimarySelection);
		}

		[Test]
		public void PasteWhenIAddChildSelectedAndCannotAdd()
		{
			var grid = IntializePasteOperationsTest();
			var xamlContext = grid.Context as XamlDesignContext;
			Assert.IsNotNull(xamlContext);

			var selection = grid.Services.Selection;
			selection.SetSelectedComponents(new[] {grid.ContentProperty.CollectionElements[2]});
			xamlContext.XamlEditAction.Paste();
			Assert.AreEqual(_name, grid.ContentProperty.CollectionElements[3].Name);
			Assert.AreEqual(grid.ContentProperty.CollectionElements[3], selection.PrimarySelection);
		}
		
		[Test]
		public void PasteCustomControlUsingMixedTypes()
		{
			DesignItem grid = CreateGridContextWithDesignSurface("<Button/>");
			DesignItem myButton = grid.Services.Component.RegisterComponentForDesigner(new ICSharpCode.WpfDesign.Tests.Controls.CustomButton());
			grid.Properties["Children"].CollectionElements.Add(myButton);
			
			DesignItem extensionItem = grid.Services.Component.RegisterComponentForDesigner(new MyExtension());
			extensionItem.Properties["MyProperty1"].SetValue(new Button());
			myButton.Properties[ICSharpCode.WpfDesign.Tests.Controls.CustomButton.TagProperty].SetValue(extensionItem);
			
			var xamlContext = grid.Context as XamlDesignContext;
			Assert.IsNotNull(xamlContext);
			xamlContext.XamlEditAction.Copy(new[] {myButton});
			
			grid = CreateGridContextWithDesignSurface("<Button/>");
			xamlContext = grid.Context as XamlDesignContext;
			Assert.IsNotNull(xamlContext);
			var selection = grid.Services.Selection;
			selection.SetSelectedComponents(new[] {grid});
			xamlContext.XamlEditAction.Paste();

			string expectedXaml = "<Button />\n" +
								  "<sdtcontrols:CustomButton>\n" +
								  "  <sdtcontrols:CustomButton.Tag>\n" +
								  "    <Controls0:MyExtension>\n" +
								  "      <Controls0:MyExtension.MyProperty1>\n" +
								  "        <Button />\n" +
								  "      </Controls0:MyExtension.MyProperty1>\n" +
								  "    </Controls0:MyExtension>\n" +
								  "  </sdtcontrols:CustomButton.Tag>\n" +
								  "</sdtcontrols:CustomButton>\n";
			
			AssertGridDesignerOutput(expectedXaml, grid.Context,
			                         "xmlns:Controls0=\"clr-namespace:ICSharpCode.WpfDesign.Tests.Designer;assembly=ICSharpCode.WpfDesign.Tests\"",
			                         "xmlns:sdtcontrols=\"http://sharpdevelop.net/WpfDesign/Tests/Controls\"");
		}

		[Test]
		public void PasteCustomControlUsingStaticResource()
		{
			DesignItem grid = CreateGridContextWithDesignSurface("<Button/>");

			DesignItemProperty resProp = grid.Properties.GetProperty("Resources");
			Assert.IsTrue(resProp.IsCollection);
			DesignItem exampleClassItem = grid.Services.Component.RegisterComponentForDesigner(new ExampleClass());
			exampleClassItem.Key = "res1";
			resProp.CollectionElements.Add(exampleClassItem);

			DesignItem myButton = grid.Services.Component.RegisterComponentForDesigner(new ICSharpCode.WpfDesign.Tests.Controls.CustomButton());
			grid.Properties["Children"].CollectionElements.Add(myButton);
			
			myButton.Properties[TextBox.TagProperty].SetValue(new StaticResourceExtension());
			myButton.Properties[TextBox.TagProperty].Value.Properties["ResourceKey"].SetValue("res1");
			
			// Verify xaml document to be copied
			string expectedXaml = "<Grid.Resources>\n" +
								  "  <Controls0:ExampleClass x:Key=\"res1\" />\n" +
								  "</Grid.Resources>\n" +
								  "<Button />\n" +
								  "<sdtcontrols:CustomButton Tag=\"{StaticResource ResourceKey=res1}\" />\n";
			
			AssertGridDesignerOutput(expectedXaml, grid.Context,
			                         "xmlns:Controls0=\"clr-namespace:ICSharpCode.WpfDesign.Tests.Designer;assembly=ICSharpCode.WpfDesign.Tests\"",
			                         "xmlns:sdtcontrols=\"http://sharpdevelop.net/WpfDesign/Tests/Controls\"");
			
			var xamlContext = grid.Context as XamlDesignContext;
			Assert.IsNotNull(xamlContext);
			xamlContext.XamlEditAction.Copy(new[] {myButton});
			
			grid = CreateGridContextWithDesignSurface("<Button/>");

			resProp = grid.Properties.GetProperty("Resources");
			Assert.IsTrue(resProp.IsCollection);
			exampleClassItem = grid.Services.Component.RegisterComponentForDesigner(new ExampleClass());
			exampleClassItem.Key = "res1";
			resProp.CollectionElements.Add(exampleClassItem);
			
			xamlContext = grid.Context as XamlDesignContext;
			Assert.IsNotNull(xamlContext);
			var selection = grid.Services.Selection;
			selection.SetSelectedComponents(new[] {grid});
			xamlContext.XamlEditAction.Paste();
			
			AssertGridDesignerOutput(expectedXaml, grid.Context,
			                         "xmlns:Controls0=\"clr-namespace:ICSharpCode.WpfDesign.Tests.Designer;assembly=ICSharpCode.WpfDesign.Tests\"",
			                         "xmlns:sdtcontrols=\"http://sharpdevelop.net/WpfDesign/Tests/Controls\"");
		}
	}
	
	public class MyExtension : MarkupExtension
	{
		public MyExtension()
		{
		}

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			return null;
		}
		
		public object MyProperty1 { get; set; }
	}
}
