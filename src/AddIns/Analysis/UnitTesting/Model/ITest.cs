﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using ICSharpCode.NRefactory.Utils;
using ICSharpCode.SharpDevelop.Dom;

namespace ICSharpCode.UnitTesting
{
	/// <summary>
	/// Represents a unit test or a group of unit tests.
	/// </summary>
	public interface ITest
	{
		/// <summary>
		/// Gets the collection of nested tests.
		/// </summary>
		IModelCollection<ITest> NestedTests { get; }
		
		/// <summary>
		/// Gets whether this test allows expanding the list of nested tests.
		/// If possible, this property should return the same value as <c>NestedTests.Count &gt; 0</c>.
		/// However, when doing so is expensive (e.g. due to lazy initialization), this
		/// property may return true even if there are no nested tests.
		/// </summary>
		bool CanExpandNestedTests { get; }
		
		/// <summary>
		/// Gets the parent project that owns this test.
		/// </summary>
		ITestProject ParentProject { get; }
		
		/// <summary>
		/// Name to be displayed in the tests tree view.
		/// </summary>
		string DisplayName { get; }
		
		/// <summary>
		/// Raised when the <see cref="Name"/> property changes.
		/// </summary>
		event EventHandler DisplayNameChanged;
		
		/// <summary>
		/// Gets the result of the previous run of this test.
		/// </summary>
		TestResultType Result { get; }
		
		/// <summary>
		/// Raised when the <see cref="Result"/> property changes.
		/// </summary>
		event EventHandler<TestResultTypeChangedEventArgs> ResultChanged;
		
		/// <summary>
		/// Resets the test results for this test and all nested tests.
		/// </summary>
		void ResetTestResults();
		
		/// <summary>
		/// Retrieves the path to the specified test, if it is a descendant of this test.
		/// Returns null if the specified test is not a descendant.
		/// Returns an empty stack if this is the test we are searching for.
		/// The top-most element on the stack (=first when enumerating the stack) will be
		/// a direct child of this test.
		/// </summary>
		ImmutableStack<ITest> FindPathToDescendant(ITest test);
		
		System.Windows.Input.ICommand GoToDefinition { get; }
	}
}
