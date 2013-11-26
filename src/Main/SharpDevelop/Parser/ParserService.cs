﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ICSharpCode.Core;
using ICSharpCode.NRefactory;
using ICSharpCode.NRefactory.Editor;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.NRefactory.TypeSystem.Implementation;
using ICSharpCode.SharpDevelop.Editor;
using ICSharpCode.SharpDevelop.Editor.Search;
using ICSharpCode.SharpDevelop.Gui;
using ICSharpCode.SharpDevelop.Project;
using ICSharpCode.SharpDevelop.Refactoring;

namespace ICSharpCode.SharpDevelop.Parser
{
	sealed class ParserService : IParserService
	{
		IList<ParserDescriptor> parserDescriptors;
		
		public ParserService()
		{
			parserDescriptors = AddInTree.BuildItems<ParserDescriptor>("/SharpDevelop/Parser", null, false);
			this.LoadSolutionProjectsThread = new LoadSolutionProjects();
		}
		
		public ILoadSolutionProjectsThread LoadSolutionProjectsThread { get; private set; }
		
		#region ParseInformationUpdated
		public event EventHandler<ParseInformationEventArgs> ParseInformationUpdated = delegate {};
		
		internal void RaiseParseInformationUpdated(ParseInformationEventArgs e)
		{
			// RaiseParseInformationUpdated is called inside a lock, but we don't want to raise the event inside that lock.
			// To ensure events are raised in the same order, we always invoke on the main thread.
			SD.MainThread.InvokeAsyncAndForget(delegate {
				if (!LoadSolutionProjectsThread.IsRunning) {
					string addition;
					if (e.OldUnresolvedFile == null) {
						addition = " (new)";
					} else if (e.NewUnresolvedFile == null) {
						addition = " (removed)";
					} else {
						addition = " (updated)";
					}
					LoggingService.Debug("ParseInformationUpdated " + e.FileName + addition);
				}
				ParseInformationUpdated(null, e);
			});
		}
		#endregion
		
		#region TaskListTokens
		IReadOnlyList<string> taskListTokens = LoadTaskListTokens();
		
		public IReadOnlyList<string> TaskListTokens {
			get { return taskListTokens; }
			set {
				SD.MainThread.VerifyAccess();
				if (!value.SequenceEqual(taskListTokens)) {
					taskListTokens = value.ToArray();
					SD.PropertyService.SetList("SharpDevelop.TaskListTokens", taskListTokens);
					// TODO: trigger reparse?
				}
			}
		}
		
		static IReadOnlyList<string> LoadTaskListTokens()
		{
			if (SD.PropertyService.Contains("SharpDevelop.TaskListTokens"))
				return SD.PropertyService.GetList<string>("SharpDevelop.TaskListTokens");
			else
				return new string[] { "HACK", "TODO", "UNDONE", "FIXME" };
		}
		#endregion
		
		#region Compilation
		public ICompilation GetCompilation(IProject project)
		{
			return GetCurrentSolutionSnapshot().GetCompilation(project);
		}
		
		public ICompilation GetCompilationForFile(FileName fileName)
		{
			IProject project = SD.ProjectService.FindProjectContainingFile(fileName);
			if (project != null)
				return GetCompilation(project);
			
			var entry = GetFileEntry(fileName, false);
			if (entry != null && entry.parser != null) {
				var unresolvedFile = entry.GetExistingUnresolvedFile(null, null);
				if (unresolvedFile != null) {
					ICompilation compilation = entry.parser.CreateCompilationForSingleFile(fileName, unresolvedFile);
					if (compilation != null)
						return compilation;
				}
			}
			return MinimalCorlib.Instance.CreateCompilation();
		}
		
		// Use a WeakReference for caching the solution snapshot - it can require
		// lots of memory and may not be invalidated soon enough if the user
		// is only browsing code.
		volatile WeakReference<SharpDevelopSolutionSnapshot> currentSolutionSnapshot;
		
		public ISolutionSnapshotWithProjectMapping GetCurrentSolutionSnapshot()
		{
			var weakRef = currentSolutionSnapshot;
			SharpDevelopSolutionSnapshot result;
			if (weakRef == null || !weakRef.TryGetTarget(out result)) {
				// create new snapshot if we don't have one cached
				var solution = ProjectService.OpenSolution;
				result = new SharpDevelopSolutionSnapshot(solution != null ? solution.Projects : null);
				currentSolutionSnapshot = new WeakReference<SharpDevelopSolutionSnapshot>(result);
			}
			return result;
		}
		
		public void InvalidateCurrentSolutionSnapshot()
		{
			currentSolutionSnapshot = null;
		}
		#endregion
		
		#region Entry management
		Dictionary<FileName, ParserServiceEntry> fileEntryDict = new Dictionary<FileName, ParserServiceEntry>();
		
		ParserServiceEntry GetFileEntry(FileName fileName, bool createIfMissing)
		{
			if (fileName == null)
				throw new ArgumentNullException("fileName");
			ParserServiceEntry entry;
			lock (fileEntryDict) {
				if (!fileEntryDict.TryGetValue(fileName, out entry)) {
					if (!createIfMissing)
						return null;
					entry = new ParserServiceEntry(this, fileName);
					fileEntryDict.Add(fileName, entry);
				}
			}
			return entry;
		}
		
		public void ClearParseInformation(FileName fileName)
		{
			ParserServiceEntry entry = GetFileEntry(fileName, false);
			if (entry != null) {
				entry.ExpireCache();
			}
		}
		
		internal void RemoveEntry(ParserServiceEntry entry)
		{
			Debug.Assert(Monitor.IsEntered(entry));
			lock (fileEntryDict) {
				ParserServiceEntry entryAtKey;
				if (fileEntryDict.TryGetValue(entry.fileName, out entryAtKey)) {
					if (entry == entryAtKey)
						fileEntryDict.Remove(entry.fileName);
				}
			}
		}
		
		const int cachedEntryCount = 5;
		List<ParserServiceEntry> cacheExpiryQueue = new List<ParserServiceEntry>();
		
		internal void RegisterForCacheExpiry(ParserServiceEntry entry)
		{
			// This method should not be called within any locks
			Debug.Assert(!Monitor.IsEntered(entry));
			ParserServiceEntry expiredItem = null;
			lock (cacheExpiryQueue) {
				cacheExpiryQueue.Remove(entry); // remove entry from queue if it's already enqueued
				if (cacheExpiryQueue.Count >= cachedEntryCount) {
					// dequeue item at front
					expiredItem = cacheExpiryQueue[0];
					cacheExpiryQueue.RemoveAt(0);
				}
				cacheExpiryQueue.Add(entry); // add entry to back
			}
			if (expiredItem != null)
				expiredItem.ExpireCache();
		}
		
		public void AddOwnerProject(FileName fileName, IProject project, bool startAsyncParse, bool isLinkedFile)
		{
			if (project == null)
				throw new ArgumentNullException("project");
			var entry = GetFileEntry(fileName, true);
			entry.AddOwnerProject(project, isLinkedFile);
			if (startAsyncParse)
				entry.ParseFileAsync(null, project, CancellationToken.None).FireAndForget();
		}
		
		public void RemoveOwnerProject(FileName fileName, IProject project)
		{
			if (project == null)
				throw new ArgumentNullException("project");
			var entry = GetFileEntry(fileName, false);
			if (entry != null)
				entry.RemoveOwnerProject(project);
		}
		#endregion
		
		#region Forward Parse() calls to entry
		public IUnresolvedFile GetExistingUnresolvedFile(FileName fileName, ITextSourceVersion version, IProject parentProject)
		{
			var entry = GetFileEntry(fileName, false);
			if (entry != null)
				return entry.GetExistingUnresolvedFile(version, parentProject);
			else
				return null;
		}
		
		public ParseInformation GetCachedParseInformation(FileName fileName, ITextSourceVersion version, IProject parentProject)
		{
			var entry = GetFileEntry(fileName, false);
			if (entry != null)
				return entry.GetCachedParseInformation(version, parentProject);
			else
				return null;
		}
		
		public ParseInformation Parse(FileName fileName, ITextSource fileContent, IProject parentProject, CancellationToken cancellationToken)
		{
			return GetFileEntry(fileName, true).Parse(fileContent, parentProject, cancellationToken);
		}
		
		public IUnresolvedFile ParseFile(FileName fileName, ITextSource fileContent, IProject parentProject, CancellationToken cancellationToken)
		{
			return GetFileEntry(fileName, true).ParseFile(fileContent, parentProject, cancellationToken);
		}
		
		public Task<ParseInformation> ParseAsync(FileName fileName, ITextSource fileContent, IProject parentProject, CancellationToken cancellationToken)
		{
			return GetFileEntry(fileName, true).ParseAsync(fileContent, parentProject, cancellationToken);
		}
		
		public Task<IUnresolvedFile> ParseFileAsync(FileName fileName, ITextSource fileContent, IProject parentProject, CancellationToken cancellationToken)
		{
			return GetFileEntry(fileName, true).ParseFileAsync(fileContent, parentProject, cancellationToken);
		}
		#endregion
		
		#region Resolve
		public ResolveResult Resolve(ITextEditor editor, TextLocation location, ICompilation compilation, CancellationToken cancellationToken)
		{
			if (editor == null)
				throw new ArgumentNullException("editor");
			return Resolve(editor.FileName, location, editor.Document, compilation, cancellationToken);
		}
		
		public ResolveResult Resolve(FileName fileName, TextLocation location, ITextSource fileContent, ICompilation compilation, CancellationToken cancellationToken)
		{
			var entry = GetFileEntry(fileName, true);
			if (entry.parser == null)
				return ErrorResolveResult.UnknownError;
			IProject project = compilation != null ? compilation.GetProject() : null;
			var parseInfo = entry.Parse(fileContent, project, cancellationToken);
			if (parseInfo == null)
				return ErrorResolveResult.UnknownError;
			if (compilation == null)
				compilation = GetCompilationForFile(fileName);
			ResolveResult rr = entry.parser.Resolve(parseInfo, location, compilation, cancellationToken);
			LoggingService.Debug("Resolved " + location + " to " + rr);
			return rr;
		}
		
		public ResolveResult ResolveSnippet(FileName fileName, TextLocation fileLocation, ITextSource fileContent, string codeSnippet, ICompilation compilation, CancellationToken cancellationToken)
		{
			var entry = GetFileEntry(fileName, true);
			if (entry.parser == null)
				return ErrorResolveResult.UnknownError;
			IProject project = compilation != null ? compilation.GetProject() : null;
			var parseInfo = entry.Parse(fileContent, project, cancellationToken);
			if (parseInfo == null)
				return ErrorResolveResult.UnknownError;
			if (compilation == null)
				compilation = GetCompilationForFile(fileName);
			ResolveResult rr = entry.parser.ResolveSnippet(parseInfo, fileLocation, codeSnippet, compilation, cancellationToken);
			LoggingService.Debug("Resolved " + fileLocation + " to " + rr);
			return rr;
		}
		
		public Task<ResolveResult> ResolveAsync(FileName fileName, TextLocation location, ITextSource fileContent, ICompilation compilation, CancellationToken cancellationToken)
		{
			var entry = GetFileEntry(fileName, true);
			if (entry.parser == null)
				return Task.FromResult<ResolveResult>(ErrorResolveResult.UnknownError);
			IProject project = compilation != null ? compilation.GetProject() : null;
			return entry.ParseAsync(fileContent, project, cancellationToken).ContinueWith(
				delegate (Task<ParseInformation> parseInfoTask) {
					var parseInfo = parseInfoTask.Result;
					if (parseInfo == null)
						return ErrorResolveResult.UnknownError;
					if (compilation == null)
						compilation = GetCompilationForFile(fileName);
					ResolveResult rr = entry.parser.Resolve(parseInfo, location, compilation, cancellationToken);
					LoggingService.Debug("Resolved " + location + " to " + rr);
					return rr;
				}, cancellationToken);
		}
		
		public async Task FindLocalReferencesAsync(FileName fileName, IVariable variable, Action<SearchResultMatch> callback, ITextSource fileContent, ICompilation compilation, CancellationToken cancellationToken)
		{
			var entry = GetFileEntry(fileName, true);
			if (entry.parser == null)
				return;
			if (fileContent == null)
				fileContent = entry.parser.GetFileContent(fileName);
			if (compilation == null)
				compilation = GetCompilationForFile(fileName);
			var parseInfo = await entry.ParseAsync(fileContent, compilation.GetProject(), cancellationToken).ConfigureAwait(false);
			await Task.Run(
				() => entry.parser.FindLocalReferences(parseInfo, fileContent, variable, compilation, callback, cancellationToken)
			);
		}
		#endregion
		
		#region HasParser / CreateParser
		public bool HasParser(FileName fileName)
		{
			if (fileName == null)
				throw new ArgumentNullException("fileName");
			if (parserDescriptors == null)
				return false;
			foreach (ParserDescriptor descriptor in parserDescriptors) {
				if (descriptor.CanParse(fileName)) {
					return true;
				}
			}
			return false;
		}
		
		/// <summary>
		/// Creates a new IParser instance that can parse the specified file.
		/// This method is thread-safe.
		/// </summary>
		internal IParser CreateParser(FileName fileName)
		{
			if (fileName == null)
				throw new ArgumentNullException("fileName");
			if (parserDescriptors == null)
				return null;
			foreach (ParserDescriptor descriptor in parserDescriptors) {
				if (descriptor.CanParse(fileName)) {
					IParser p = descriptor.CreateParser();
					if (p != null) {
						p.TaskListTokens = TaskListTokens;
						return p;
					}
				}
			}
			return null;
		}
		#endregion
		
		internal void StartParserThread()
		{
			// TODO
		}
		
		internal void StopParserThread()
		{
			// TODO
		}
		
		public void RegisterUnresolvedFile(FileName fileName, IProject project, IUnresolvedFile unresolvedFile)
		{
			GetFileEntry(fileName, true).RegisterUnresolvedFile(project, unresolvedFile);
		}
	}
}
