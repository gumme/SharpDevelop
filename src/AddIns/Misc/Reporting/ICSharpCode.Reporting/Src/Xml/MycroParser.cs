﻿/*
 * Created by SharpDevelop.
 * User: Peter Forstmeier
 * Date: 19.03.2013
 * Time: 20:14
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.Reflection;

namespace ICSharpCode.Reporting.Xml
{
	/// <summary>
	/// Description of MycroParser.
	/// </summary>
	public interface IMycroXaml
	{
		void Initialize(object parent);
		object ReturnedObject
		{
			get;
		}
	}

	/// <summary>
	/// See http://www.codeproject.com/dotnet/MycroXaml.asp
	/// </summary>
	internal abstract class MycroParser
	{
		public object Load(XmlElement element)
		{
			return ProcessNode(element, null);
		}

		protected abstract Type GetTypeByName(string ns, string name);

	    private object ProcessNode(XmlNode node, object parent)
		{
			object ret=null;
			if (node is XmlElement)
			{
				// instantiate the class
				string ns=node.Prefix;
				string cname=node.LocalName;
				Console.WriteLine ("ProcessNode(XmlNode node, object parent)  {0}",cname);
				Type t=GetTypeByName(ns, cname);
				if (t == null) {
					Console.WriteLine("\t Not found {0}",t.FullName);
//					t = GetTypeByName (ns,"ErrorItem");
				}
				
				Trace.Assert(t != null, "Type "+cname+" could not be determined.");
//				Debug.WriteLine("Looking for " + cname + " and got " + t.FullName);
				Console.WriteLine("Looking for " + cname + " and got " + t.FullName);
				try
				{
					ret=Activator.CreateInstance(t);
				}
				catch(Exception e)
				{
					Trace.Fail("Type "+cname+" could not be instantiated:\r\n"+e.Message);
				}

				// support the ISupportInitialize interface
				if (ret is ISupportInitialize) {
					((ISupportInitialize)ret).BeginInit();
				}

				// If the instance implements the IMicroXaml interface, then it may need
				// access to the parser.
				if (ret is IMycroXaml) {
					((IMycroXaml)ret).Initialize(parent);
				}

				// implements the class-property-class model
				ProcessAttributes(node, ret, t);
				ProcessChildProperties(node, ret);

				// support the ISupportInitialize interface
				if (ret is ISupportInitialize) {
					((ISupportInitialize)ret).EndInit();
				}

				// If the instance implements the IMicroXaml interface, then it has the option
				// to return an object that replaces the instance created by the parser.
				if (ret is IMycroXaml) {
					ret=((IMycroXaml)ret).ReturnedObject;
				}
				
			}
			return ret;
		}

		protected void ProcessChildProperties(XmlNode node, object parent)
		{
			var t=parent.GetType();

			// children of a class must always be properties
			foreach(XmlNode child in node.ChildNodes)
			{
			    if (!(child is XmlElement)) continue;
			    string pname=child.LocalName;
			    var pi=t.GetProperty(pname);

			    if (pi==null)
			    {
			        // Special case--we're going to assume that the child is a class instance
			        // not associated with the parent object
//						Trace.Fail("Unsupported property: "+pname);
			        System.Console.WriteLine("Unsupported property: "+pname);
			        continue;
			    }

			    // a property can only have one child node unless it's a collection
			    foreach(XmlNode grandChild in child.ChildNodes)
			    {
			        if (grandChild is XmlText) {
			            SetPropertyToString(parent, pi, child.InnerText);
			            break;
			        }
			        else if (grandChild is XmlElement)
			        {
			            var propObject=pi.GetValue(parent, null);
			            var obj=ProcessNode(grandChild, propObject);

			            // A null return is valid in cases where a class implementing the IMicroXaml interface
			            // might want to take care of managing the instance it creates itself.  See DataBinding
			            if (obj != null)
			            {

			                // support for ICollection objects
			                if (propObject is ICollection)
			                {
			                    MethodInfo mi=t.GetMethod("Add", new Type[] {obj.GetType()});
			                    if (mi != null)
			                    {
			                        try
			                        {
			                            mi.Invoke(obj, new object[] {obj});
			                        }
			                        catch(Exception e)
			                        {
			                            Trace.Fail("Adding to collection failed:\r\n"+e.Message);
			                        }
			                    }
			                    else if (propObject is IList)
			                    {
			                        try
			                        {
			                            ((IList)propObject).Add(obj);
			                        }
			                        catch(Exception e)
			                        {
			                            Trace.Fail("List/Collection add failed:\r\n"+e.Message);
			                        }
			                    }
			                }
			                else if (!pi.CanWrite) {
			                    Trace.Fail("Unsupported read-only property: "+pname);
			                }
			                else
			                {
			                    // direct assignment if not a collection
			                    try
			                    {
			                        pi.SetValue(parent, obj, null);
			                    }
			                    catch(Exception e)
			                    {
			                        Trace.Fail("Property setter for "+pname+" failed:\r\n"+e.Message);
			                    }
			                }
			            }
			        }
			    }
			}
		}

	    private void ProcessAttributes(XmlNode node, object ret, Type type)
		{
			// process attributes
			foreach(XmlAttribute attr in node.Attributes)
			{
				string pname=attr.Name;
				string pvalue=attr.Value;

				// it's either a property or an event
				PropertyInfo pi=type.GetProperty(pname);

				if (pi != null)
				{
					// it's a property!
					SetPropertyToString(ret, pi, pvalue);
				}
				else
				{
					// who knows what it is???
					Trace.Fail("Failed acquiring property information for "+pname);
				}
			}
		}

	    static void SetPropertyToString(object obj, PropertyInfo pi, string value)
		{
			Console.WriteLine("MP - SetPropertyToString {0} - {1}",pi.Name,value.ToString());
			// it's string, so use a type converter.
			TypeConverter tc=TypeDescriptor.GetConverter(pi.PropertyType);
			try
			{
				if (tc.CanConvertFrom(typeof(string)))
				{
					object val=tc.ConvertFromInvariantString(value);
					pi.SetValue(obj, val, null);
				} else if (pi.PropertyType == typeof(Type)) {
					pi.SetValue(obj, Type.GetType(value), null);
				}
			}
			catch(Exception e)
			{
				String s = String.Format("Property setter for {0} failed {1}\r\n",pi.Name,
				                         e.Message);
				System.Console.WriteLine("MycroParser : {0}",s);
			}
		}
		
	}
}
