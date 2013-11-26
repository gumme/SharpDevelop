﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.NRefactory.Xml;
using ICSharpCode.SharpDevelop.Editor;

namespace ICSharpCode.XamlBinding
{
	public class XamlNamespace
	{
		public string XmlNamespace { get; private set; }
		
		public IList<ITypeDefinition> GetContents(ICompilation compilation)
		{
			List<INamespace> namespaces = ResolveNamespaces(compilation).ToList();
			var contents = new List<ITypeDefinition>();
			foreach (var ns in namespaces) {
				contents.AddRange(ns.Types);
			}
			return contents;
		}
		
		public XamlNamespace(string xmlNamespace)
		{
			this.XmlNamespace = xmlNamespace;
		}
		
		IEnumerable<INamespace> ResolveNamespaces(ICompilation compilation)
		{
			IType xmlnsDefinition = compilation.FindType(typeof(System.Windows.Markup.XmlnsDefinitionAttribute));
			if (XmlNamespace.StartsWith("clr-namespace:", StringComparison.Ordinal)) {
				string name = XmlNamespace.Substring("clr-namespace:".Length);
				IAssembly asm = compilation.MainAssembly;
				int asmIndex = name.IndexOf(";assembly=", StringComparison.Ordinal);
				if (asmIndex >= 0) {
					string asmName = name.Substring(asmIndex + ";assembly=".Length);
					asm = compilation.ReferencedAssemblies.FirstOrDefault(a => a.AssemblyName == asmName) ?? compilation.MainAssembly;
					name = name.Substring(0, asmIndex);
				}
				string[] parts = name.Split('.');
				var @namespace = FindNamespace(asm, parts);
				if (@namespace != null) yield return @namespace;
			} else {
				foreach (IAssembly asm in compilation.Assemblies) {
					foreach (IAttribute attr in asm.AssemblyAttributes) {
						if (xmlnsDefinition.Equals(attr.AttributeType) && attr.PositionalArguments.Count == 2) {
							string xmlns = attr.PositionalArguments[0].ConstantValue as string;
							if (xmlns != XmlNamespace) continue;
							string ns = attr.PositionalArguments[1].ConstantValue as string;
							if (ns == null) continue;
							var @namespace = FindNamespace(asm, ns.Split('.'));
							if (@namespace != null) yield return @namespace;
						}
					}
				}
			}
		}
		
		static INamespace FindNamespace(IAssembly asm, string[] parts)
		{
			INamespace ns = asm.RootNamespace;
			for (int i = 0; i < parts.Length; i++) {
				INamespace tmp = ns.ChildNamespaces.FirstOrDefault(n => n.Name == parts[i]);
				if (tmp == null)
					return null;
				ns = tmp;
			}
			return ns;
		}
	}
	
	public class XamlContext
	{
		public AXmlElement ActiveElement { get; set; }
		public AXmlElement ParentElement { get; set; }
		public ReadOnlyCollection<AXmlElement> Ancestors { get; set; }
		public AXmlAttribute Attribute { get; set; }
		public AttributeValue AttributeValue { get; set; }
		public string RawAttributeValue { get; set; }
		public int ValueStartOffset { get; set; }
		public XamlContextDescription Description { get; set; }
		public Dictionary<string, XamlNamespace> XmlnsDefinitions { get; set; }
		public XamlFullParseInformation ParseInformation { get; set; }
		public bool InRoot { get; set; }
		public ReadOnlyCollection<string> IgnoredXmlns { get; set; }
		public string XamlNamespacePrefix { get; set; }
		
		public XamlContext() {}
		
		public bool InAttributeValueOrMarkupExtension {
			get { return Description == XamlContextDescription.InMarkupExtension ||
					Description == XamlContextDescription.InAttributeValue; }
		}
		
		public bool InCommentOrCData {
			get { return Description == XamlContextDescription.InComment ||
					Description == XamlContextDescription.InCData; }
		}
	}
	
	public class XamlCompletionContext : XamlContext
	{
		public XamlCompletionContext() { }
		
		public XamlCompletionContext(XamlContext context)
		{
			this.ActiveElement = context.ActiveElement;
			this.Ancestors = context.Ancestors;
			this.Attribute = context.Attribute;
			this.AttributeValue = context.AttributeValue;
			this.Description = context.Description;
			this.ParentElement = context.ParentElement;
			this.ParseInformation = context.ParseInformation;
			this.RawAttributeValue = context.RawAttributeValue;
			this.ValueStartOffset = context.ValueStartOffset;
			this.XmlnsDefinitions = context.XmlnsDefinitions;
			this.InRoot = context.InRoot;
			this.IgnoredXmlns = context.IgnoredXmlns;
			this.XamlNamespacePrefix = context.XamlNamespacePrefix;
		}
		
		public char PressedKey { get; set; }
		public bool Forced { get; set; }
		public ITextEditor Editor { get; set; }
	}
	
	public enum XamlContextDescription
	{
		/// <summary>
		/// Outside any tag
		/// </summary>
		None,
		/// <summary>
		/// After '&lt;'
		/// </summary>
		AtTag,
		/// <summary>
		/// Inside '&lt;TagName &gt;'
		/// </summary>
		InTag,
		/// <summary>
		/// Inside '="Value"'
		/// </summary>
		InAttributeValue,
		/// <summary>
		/// Inside '="{}"'
		/// </summary>
		InMarkupExtension,
		/// <summary>
		/// Inside '&lt;!-- --&gt;'
		/// </summary>
		InComment,
		/// <summary>
		/// Inside '&lt;![CDATA[]]&gt;'
		/// </summary>
		InCData
	}
}
