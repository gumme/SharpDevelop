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
using System.Collections;
using System.Collections.Generic;
using System.Windows.Controls;

using ICSharpCode.Core;
using ICSharpCode.Core.Presentation;

namespace ICSharpCode.SharpDevelop.Editor.Search
{
	public class LastSearchResultsBuilder : IMenuItemBuilder
	{
		public IEnumerable<object> BuildItems(Codon codon, object owner)
		{
			List<object> items = new List<object>();
			foreach (ISearchResult searchResult in SearchResultsPad.Instance.LastSearches) {
				MenuItem menuItem = new MenuItem();
				menuItem.Header = searchResult.Text;
				// copy in local variable so that lambda refers to correct loop iteration
				ISearchResult searchResultCopy = searchResult;
				menuItem.Click += (sender, e) => SearchResultsPad.Instance.ShowSearchResults(searchResultCopy);
				items.Add(menuItem);
			}
			return items;
		}
	}
	
	public class ClearSearchResultsList : AbstractMenuCommand
	{
		public override void Run()
		{
			SearchResultsPad.Instance.ClearLastSearchesList();
		}
	}
}
