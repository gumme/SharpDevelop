﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.ComponentModel.Design.Serialization;
using ICSharpCode.Core;
using ICSharpCode.SharpDevelop.Gui;

namespace ICSharpCode.FormsDesigner.Gui.OptionPanels
{
	/// <summary>
	/// Interaction logic for LocalizationOptionPanelXAML.xaml
	/// </summary>
	public partial class LocalizationModelOptionsPanel : OptionPanel
	{
		public const string DefaultLocalizationModelPropertyName = "FormsDesigner.DesignerOptions.DefaultLocalizationModel";
		public const string KeepLocalizationModelPropertyName = "FormsDesigner.DesignerOptions.KeepLocalizationModel";
		
		const CodeDomLocalizationModel DefaultLocalizationModelDefaultValue = CodeDomLocalizationModel.PropertyReflection;
		const bool KeepLocalizationModelDefaultValue = true;
		
		public LocalizationModelOptionsPanel()
		{
			InitializeComponent();
			this.reflectionRadioButton.IsChecked = (DefaultLocalizationModel == CodeDomLocalizationModel.PropertyReflection);
			this.assignmentRadioButton.IsChecked = !this.reflectionRadioButton.IsChecked;
			this.keepModelCheckBox.IsChecked = KeepLocalizationModel;
		}
		
		
		
		public static CodeDomLocalizationModel DefaultLocalizationModel {
			get { return GetPropertySafe(DefaultLocalizationModelPropertyName, DefaultLocalizationModelDefaultValue); }
			set { PropertyService.Set(DefaultLocalizationModelPropertyName, value); }
		}
		
		
		public static bool KeepLocalizationModel {
			get { return GetPropertySafe(KeepLocalizationModelPropertyName, KeepLocalizationModelDefaultValue); }
			set { PropertyService.Set(KeepLocalizationModelPropertyName, value); }
		}
		
		
		static T GetPropertySafe<T>(string name, T defaultValue)
		{
			// This wrapper is no longer necessary in SD5;
			// if the actual property service isn't available (in unit tests), a dummy property service is used
			return PropertyService.Get<T>(name, defaultValue);
		}
		
		public override bool SaveOptions()
		{
			if (this.reflectionRadioButton.IsChecked == true) {
				DefaultLocalizationModel = CodeDomLocalizationModel.PropertyReflection;
			} else if (this.assignmentRadioButton.IsChecked == true) {
				DefaultLocalizationModel = CodeDomLocalizationModel.PropertyAssignment;
			} else {
				MessageService.ShowError("One localization model must be selected!");
				return false;
			}
			
			KeepLocalizationModel = (this.keepModelCheckBox.IsChecked == true);
			
			return true;
			
		}
	}
}
