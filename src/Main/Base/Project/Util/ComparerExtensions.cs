﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.Collections.Generic;
using ICSharpCode.NRefactory.Utils;

namespace ICSharpCode.SharpDevelop
{
	/// <summary>
	/// Extension methods for building comparers.
	/// </summary>
	public static class ComparerExtensions
	{
		public static IComparer<T> Descending<T>(this IComparer<T> comparer)
		{
			if (comparer == null)
				throw new ArgumentNullException("comparer");
			return new DescendingComparer<T>(comparer);
		}
		
		public static IComparer<T> Then<T>(this IComparer<T> primaryComparer, IComparer<T> secondaryComparer)
		{
			if (primaryComparer == null)
				throw new ArgumentNullException("primaryComparer");
			if (secondaryComparer == null)
				throw new ArgumentNullException("secondaryComparer");
			return new CompositeComparer<T>(primaryComparer, secondaryComparer);
		}
		
		public static IComparer<TElement> ThenBy<TElement, TKey>(this IComparer<TElement> primaryComparer, Func<TElement, TKey> keySelector)
		{
			return primaryComparer.Then(KeyComparer.Create(keySelector));
		}
		
		public static IComparer<TElement> ThenBy<TElement, TKey>(this IComparer<TElement> primaryComparer, Func<TElement, TKey> keySelector, IComparer<TKey> keyComparer)
		{
			return primaryComparer.Then(KeyComparer.Create(keySelector, keyComparer));
		}
		
		public static IComparer<TElement> ThenByDescending<TElement, TKey>(this IComparer<TElement> primaryComparer, Func<TElement, TKey> keySelector)
		{
			return primaryComparer.Then(KeyComparer.Create(keySelector).Descending());
		}
		
		public static IComparer<TElement> ThenByDescending<TElement, TKey>(this IComparer<TElement> primaryComparer, Func<TElement, TKey> keySelector, IComparer<TKey> keyComparer)
		{
			return primaryComparer.Then(KeyComparer.Create(keySelector, keyComparer).Descending());
		}
	}
	
	sealed class DescendingComparer<T> : IComparer<T>
	{
		readonly IComparer<T> comparer;
		
		public DescendingComparer(IComparer<T> comparer)
		{
			this.comparer = comparer;
		}
		
		public int Compare(T x, T y)
		{
			return comparer.Compare(y, x);
		}
	}
	
	sealed class CompositeComparer<T> : IComparer<T>
	{
		readonly IComparer<T> primary;
		readonly IComparer<T> secondary;
		
		public CompositeComparer(IComparer<T> primary, IComparer<T> secondary)
		{
			this.primary = primary;
			this.secondary = secondary;
		}
		
		public int Compare(T x, T y)
		{
			int r = primary.Compare(x, y);
			if (r == 0)
				r = secondary.Compare(x, y);
			return r;
		}
	}
}
