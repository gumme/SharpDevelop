// Copyright (c) 2014 AlphaSierraPapa for the SharpDevelop Team
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
using NUnit.Framework;
using System.Windows.Markup;
using ICSharpCode.WpfDesign.XamlDom;
using System.IO;

namespace ICSharpCode.WpfDesign.Tests.XamlDom
{
	[TestFixture]
	public class MarkupExtensionTests : TestHelper
	{
		[Test]
		public void TestBinding1()
		{
			TestMarkupExtension("Title=\"{Binding}\"");
		}

		[Test]
		public void TestBinding2()
		{
			TestMarkupExtension("Title=\"{Binding Some}\"");
		}

		[Test]
		public void TestBinding3()
		{
			TestMarkupExtension("Title=\"{ Binding  Some , ElementName = Some , Mode = TwoWay }\"");
		}

		[Test]
		public void TestType()
		{
			TestMarkupExtension("Content=\"{x:Type Button}\"");
		}		

		[Test]
		public void TestMyExtensionOnDependencyProperty()
		{
			TestMarkupExtension("Content=\"{t:MyExtension 1, 2}\"");
		}
		
		[Test]
		public void TestMyExtensionOnNormalProperty()
		{
			TestMarkupExtension("Owner=\"{t:MyExtension 1, 2}\"");
		}
		
		[Test]
		[Ignore("Fails because of XamlObjectServiceProvider, TargetObject getter must not call ValueOnInstance on parent property but keep a reference for parent collection in some way.")]
		public void TestMyExtensionOnImplicitList()
		{
						TestLoading(@"
<ExampleClassContainer
  xmlns=""" + XamlTypeFinderTests.XamlDomTestsNamespace + @"""
  xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
    <MyExtension/>
</ExampleClassContainer>
			");
		}
		
		[Test]
		[Ignore("Fails because of XamlObjectServiceProvider, TargetObject getter must not call ValueOnInstance on parent property but keep a reference for parent collection in some way.")]
		public void TestMyExtensionOnExplicitList()
		{
						TestLoading(@"
<ExampleClassContainer
  xmlns=""" + XamlTypeFinderTests.XamlDomTestsNamespace + @"""
  xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
   <ExampleClassContainer.OtherList>
      <ExampleClassList>
         <MyExtension/>
      </ExampleClassList>
   </ExampleClassContainer.OtherList>
</ExampleClassContainer>
			");
		}

		[Test]
		public void TestStatic()
		{
			TestMarkupExtension("Background=\"{x:Static SystemColors.ControlBrush}\"");
		}

		[Test]
		[Ignore]
		public void TestDynamicResource()
		{
			TestMarkupExtension("Background=\"{DynamicResource {x:Static SystemColors.ControlBrushKey}}\"");
		}

		[Test]
		public void TestBindingRelativeSourceSelf()
		{
			TestMarkupExtension("Content=\"{Binding Some, RelativeSource={RelativeSource Self}}\"");
		}

		[Test]
		//[ExpectedException] 
		// Must differ from official XamlReader result, because Static dereference
		// To catch this we should use XamlDocument.Save() instead of XamlWriter.Save(instance)
		public void TestStaticWithMyStaticClass()
		{
			TestMarkupExtension("Content=\"{x:Static t:MyStaticClass.StaticString}\"");
		}

//        [Test]
//        public void Test10()
//        {
//            var s =			
//@"<Window
//	xmlns='http://schemas.microsoft.com/netfx/2007/xaml/presentation'
//	xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
//	Content='{Binding}'";
//            var doc = XamlParser.Parse(new StringReader(s));
//            var binding = doc.RootElement.FindOrCreateProperty("Content").PropertyValue as XamlObject;
//            binding.FindOrCreateProperty("ElementName").PropertyValue = doc.CreatePropertyValue("name1", null);
//            Assert.AreEqual(binding.XmlAttribute.Value, "{Binding ElementName=name1}");

//        }

		static void TestMarkupExtension(string s)
		{
			TestLoading(@"<Window
	xmlns=""http://schemas.microsoft.com/netfx/2007/xaml/presentation""
	xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
	xmlns:t=""" + XamlTypeFinderTests.XamlDomTestsNamespace + @"""
	" + s + @"/>");
		}
	}

	public static class MyStaticClass
	{
		public static string StaticString = "a";
	}
	
	public class MyExtension : MarkupExtension
	{
		public MyExtension()
		{
		}
		
		public MyExtension(object p1, object p2)
		{
		}
		
		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			var provideValueTarget = (IProvideValueTarget)serviceProvider.GetService(typeof(IProvideValueTarget));
			if (provideValueTarget == null) {
				throw new InvalidOperationException("IProvideValueTarget not available.");
			}
			
			string targetPropertyDescription = provideValueTarget.GetTargetPropertyDescription();
			TestHelperLog.Log(GetType().Name + ".ProvideValue target property: " + targetPropertyDescription);
			
			var targetObject = provideValueTarget.TargetObject;
			
			string targetObjectDescription = targetObject != null ? targetObject.GetType().Name + " (" + targetObject.GetType().AssemblyQualifiedName + ")" : "{null}";
			TestHelperLog.Log(GetType().Name + ".ProvideValue target object: " + targetObjectDescription);
			
			// To support MarkupExtension tests on lists.
			if(targetObject is System.Collections.Generic.List<ExampleClass>) {
				return new ExampleClass();
			}
			
			return null;
		}
	}
	
	public static class ProvideValueTargetExtensions
	{
		public static string GetTargetPropertyDescription(this IProvideValueTarget provideValueTarget)
		{
			var targetProperty = provideValueTarget.TargetProperty;
			
			var propertyInfo = targetProperty as System.Reflection.PropertyInfo;
			if (propertyInfo != null) {
				TestHelperLog.Log("GetTargetPropertyDescription found PropertyInfo.");
				return propertyInfo.Name + " (" + propertyInfo.PropertyType.AssemblyQualifiedName + ")";
			} else {
				var dependencyProperty = targetProperty as System.Windows.DependencyProperty;
				if (dependencyProperty != null) {
					TestHelperLog.Log("GetTargetPropertyDescription found DependencyProperty, returning type.");
					return dependencyProperty.Name + " (" + dependencyProperty.PropertyType.AssemblyQualifiedName + ")";
				}
			}
			
			TestHelperLog.Log(String.Format("GetTargetPropertyDescription found {0}; returning unknown.", targetProperty != null ? targetProperty.GetType().AssemblyQualifiedName : "{null}"));
			
			return "<unknown>";
		}
	}
}
