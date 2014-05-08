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
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Xml;
using ICSharpCode.Core;
using ICSharpCode.NRefactory.CSharp;

namespace CSharpBinding.FormattingStrategy
{
	/// <summary>
	/// Generic container for C# formatting options that can be chained together from general to specific and inherit
	/// options from parent.
	/// </summary>
	internal class CSharpFormattingOptionsContainer : INotifyPropertyChanged
	{
		CSharpFormattingOptionsContainer parent;
		CSharpFormattingOptions cachedOptions;
		
		readonly HashSet<string> activeOptions;
		
		public CSharpFormattingOptionsContainer(CSharpFormattingOptionsContainer parent = null)
			: this(parent, new HashSet<string>())
		{
		}
		
		private CSharpFormattingOptionsContainer(CSharpFormattingOptionsContainer parent, HashSet<string> activeOptions)
		{
			this.parent = parent;
			if (parent != null) {
				parent.PropertyChanged += HandlePropertyChanged;
			}
			this.activeOptions = activeOptions;
			Reset();
			cachedOptions = CreateCachedOptions();
		}
		
		public string DefaultText
		{
			get;
			set;
		}
		
		public CSharpFormattingOptionsContainer Parent
		{
			get {
				return parent;
			}
		}
		
		/// <summary>
		/// Resets all container's options to given <see cref="ICSharpCode.NRefactory.CSharp.CSharpFormattingOptions"/> instance.
		/// </summary>
		/// <param name="options">Option values to set in container. <c>null</c> (default) to use empty options.</param>
		public void Reset(CSharpFormattingOptions options = null)
		{
			activeOptions.Clear();
			cachedOptions = options ?? CreateCachedOptions();
			if ((options != null) || (parent == null)) {
				// Activate all options
				foreach (var property in typeof(CSharpFormattingOptions).GetProperties()) {
					activeOptions.Add(property.Name);
				}
			}
			OnPropertyChanged(null);
		}
		
		/// <summary>
		/// Creates a clone of current options container.
		/// </summary>
		/// <returns>Clone of options container.</returns>
		public CSharpFormattingOptionsContainer Clone()
		{
			var clone = new CSharpFormattingOptionsContainer(parent);
			clone.CloneFrom(this);
			return clone;
		}
		
		/// <summary>
		/// Clones all properties from another options container.
		/// </summary>
		/// <returns>Clone of options container.</returns>
		public void CloneFrom(CSharpFormattingOptionsContainer options)
		{
			activeOptions.Clear();
			foreach (var activeOption in options.activeOptions)
				activeOptions.Add(activeOption);
			cachedOptions = options.cachedOptions.Clone();
			OnPropertyChanged(null);
		}
		
		#region INotifyPropertyChanged implementation
		
		public event PropertyChangedEventHandler PropertyChanged;
		
		private void OnPropertyChanged(string propertyName)
		{
			if (PropertyChanged != null) {
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}
		
		#endregion
		
		private void HandlePropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if ((e.PropertyName == "Parent") || (e.PropertyName == null)) {
				// All properties might have changed -> update everything
				cachedOptions = CreateCachedOptions();
				OnPropertyChanged(e.PropertyName);
			} else {
				// Some other property has changed, check if we have our own value for it
				if (!activeOptions.Contains(e.PropertyName)) {
					// We rely on property value from some of the parents and have to update it from there
					PropertyInfo propertyInfo = typeof(CSharpFormattingOptions).GetProperty(e.PropertyName);
					if (propertyInfo != null) {
						var val = GetEffectiveOption(e.PropertyName);
						propertyInfo.SetValue(cachedOptions, val);
						OnPropertyChanged(e.PropertyName);
					}
				}
			}
		}
		
		/// <summary>
		/// Retrieves the value of a formatting option or null, if none is set.
		/// </summary>
		/// <param name="option">Name of option</param>
		/// <returns>True, if option with given type could be found in hierarchy. False otherwise.</returns>
		public object GetOption(string option)
		{
			// Run up the hierarchy until we find a defined value for property
			if (activeOptions.Contains(option)) {
				PropertyInfo propertyInfo = typeof(CSharpFormattingOptions).GetProperty(option);
				if (propertyInfo != null) {
					return propertyInfo.GetValue(cachedOptions);
				}
			}
			
			return null;
		}
		
		/// <summary>
		/// Retrieves the value of a formatting option by looking at current and (if nothing set here) parent
		/// containers.
		/// </summary>
		/// <param name="option">Name of option</param>
		/// <returns>True, if option with given type could be found in hierarchy. False otherwise.</returns>
		public object GetEffectiveOption(string option)
		{
			// Run up the hierarchy until we find a defined value for property
			CSharpFormattingOptionsContainer container = this;
			do
			{
				object val = null;
				if (container.activeOptions.Contains(option)) {
					PropertyInfo propertyInfo = typeof(CSharpFormattingOptions).GetProperty(option);
					if (propertyInfo != null) {
						val = propertyInfo.GetValue(container.cachedOptions);
					}
				}
				if (val != null) {
					return val;
				}
				container = container.parent;
			} while (container != null);
			
			return null;
		}
		
		/// <summary>
		/// Sets an option.
		/// </summary>
		/// <param name="option">Option name.</param>
		/// <param name="value">Option value, <c>null</c> to reset.</param>
		public void SetOption(string option, object value)
		{
			if (value != null) {
				// Save value in option values and cached options
				activeOptions.Add(option);
				PropertyInfo propertyInfo = typeof(CSharpFormattingOptions).GetProperty(option);
				if ((propertyInfo != null) && (propertyInfo.PropertyType == value.GetType())) {
					propertyInfo.SetValue(cachedOptions, value);
				}
			} else {
				// Reset this option
				activeOptions.Remove(option);
				// Update formatting options object from parents
				PropertyInfo propertyInfo = typeof(CSharpFormattingOptions).GetProperty(option);
				if (propertyInfo != null) {
					propertyInfo.SetValue(cachedOptions, GetEffectiveOption(option));
				}
			}
			OnPropertyChanged(option);
		}
		
		/// <summary>
		/// Retrieves the type of a given option.
		/// </summary>
		/// <param name="option">Option name</param>
		/// <returns>Option's type.</returns>
		public Type GetOptionType(string option)
		{
			PropertyInfo propertyInfo = typeof(CSharpFormattingOptions).GetProperty(option);
			if (propertyInfo != null) {
				return propertyInfo.PropertyType;
			}
			
			return null;
		}
		
		/// <summary>
		/// Retrieves a <see cref="ICSharpCode.NRefactory.CSharp.CSharpFormattingOptions"/> instance from current
		/// container, resolving all options throughout container hierarchy.
		/// </summary>
		/// <returns>Filled <see cref="ICSharpCode.NRefactory.CSharp.CSharpFormattingOptions"/> instance.</returns>
		public CSharpFormattingOptions GetEffectiveOptions()
		{
			// Use copy of cached options instance
			return cachedOptions.Clone();
		}
		
		/// <summary>
		/// Creates a <see cref="ICSharpCode.NRefactory.CSharp.CSharpFormattingOptions"/> instance from current
		/// container, resolving all options throughout container hierarchy.
		/// </summary>
		/// <returns>Created and filled <see cref="ICSharpCode.NRefactory.CSharp.CSharpFormattingOptions"/> instance.</returns>
		private CSharpFormattingOptions CreateCachedOptions()
		{
			var outputOptions = FormattingOptionsFactory.CreateSharpDevelop();
			
			// Look at all container options and try to set identically named properties of CSharpFormattingOptions
			foreach (PropertyInfo propertyInfo in typeof(CSharpFormattingOptions).GetProperties()) {
				object val = GetEffectiveOption(propertyInfo.Name);
				if ((val != null) && (val.GetType() == propertyInfo.PropertyType)) {
					propertyInfo.SetValue(outputOptions, val);
				}
			}
			
			return outputOptions;
		}
		
		public void Load(Properties parentProperties)
		{
			if (parentProperties == null)
				throw new ArgumentNullException("parentProperties");
			
			Properties formatProperties = parentProperties.NestedProperties("CSharpFormatting");
			if (formatProperties != null) {
				foreach (var key in formatProperties.Keys) {
					try {
						object val = formatProperties.Get(key, (object) null);
						SetOption(key, val);
					} catch (Exception) {
						// Silently ignore loading error, then this property will be "as parent" automatically
					}
				}
			}
		}
		
		public void Save(Properties parentProperties)
		{
			if (parentProperties == null)
				throw new ArgumentNullException("parentProperties");
			
			// Create properties container from container settings
			Properties formatProperties = new Properties();
			foreach (var activeOption in activeOptions) {
				object val = GetOption(activeOption);
				if (val != null) {
					formatProperties.Set(activeOption, val);
				}
			}
			
			parentProperties.SetNestedProperties("CSharpFormatting", formatProperties);
		}
	}
}
