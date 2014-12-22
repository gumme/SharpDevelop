/*
 * Created by SharpDevelop.
 * User: trubra
 * Date: 2014-12-22
 * Time: 10:39
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Windows.Input;

namespace ICSharpCode.WpfDesign
{
	/// <summary>
	/// interface that can be implemented if a control is to be alerted of  KeyDown Event on DesignPanel
	/// </summary>
	public interface IKeyDown
	{
		/// <summary>
		/// Action to be performed on keydown on specific control
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void KeyDownAction(object sender, KeyEventArgs e);

		/// <summary>
		/// if that control wants the default DesignPanel action to be suppressed, let this return false
		/// </summary>
		bool InvokeDefaultAction { get; }
	}

	/// <summary>
	/// interface that can be implemented if a control is to be alerted of  KeyUp Event on DesignPanel
	/// </summary>
	public interface IKeyUp
	{
		/// <summary>
		/// Action to be performed on keyup on specific control
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void KeyUpAction(object sender, KeyEventArgs e);
	}
}
