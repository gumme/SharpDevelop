/*
 * Created by SharpDevelop.
 * User: trubra
 * Date: 2014-09-02
 * Time: 10:11
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;

namespace ICSharpCode.WpfDesign
{
	/// <summary>
	/// This Exception is used to mark an object for deletion in an Arrange run, when direct deletion 
	/// cannot be made since it will affect the AdornerPanel child collection in an iteration.
	/// </summary>
	public class ObjectDeletedException: Exception
	{
		public ObjectDeletedException()
		{
		}
	}
}
