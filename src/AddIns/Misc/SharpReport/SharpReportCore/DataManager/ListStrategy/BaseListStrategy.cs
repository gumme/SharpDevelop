//------------------------------------------------------------------------------
// <autogenerated>
//     This code was generated by a tool.
//     Runtime Version: 1.1.4322.2032
//
//     Changes to this file may cause incorrect behavior and will be lost if 
//     the code is regenerated.
// </autogenerated>
//------------------------------------------------------------------------------

using System;
using System.Collections;
using System.ComponentModel;

/// <summary>
/// BaseClass for all Datahandling Strategies
/// </summary>
/// <remarks>
/// 	created by - Forstmeier Peter
/// 	created on - 13.11.2005 15:26:02
/// </remarks>

namespace SharpReportCore {	
	public abstract class BaseListStrategy :IDataViewStrategy,IEnumerator {
		private bool isSorted;
		private bool isFiltered;
		private bool isGrouped;
		
		//Index to plain Datat
		private SharpIndexCollection indexList;
		private ReportSettings reportSettings;

		
		private ListChangedEventArgs resetList = new ListChangedEventArgs(ListChangedType.Reset,-1,-1);
		
		public event EventHandler <ListChangedEventArgs> ListChanged;
		public event EventHandler <GroupChangedEventArgs> GroupChanged;
		
		#region Constructor
		
		protected BaseListStrategy(ReportSettings reportSettings) {
			if (reportSettings == null) {
				throw new ArgumentNullException("reportSettings");
			}
			this.reportSettings = reportSettings;
			this.indexList = new SharpIndexCollection("IndexList");
		}
		
		#endregion
		
		#region Event's
		
		protected void NotifyResetList(){
			if (this.ListChanged != null) {
				this.ListChanged (this,this.resetList);
			}
		}
		
		protected void NotifyGroupChanging (object source,GroupSeperator groupSeperator) {
			
			if (this.GroupChanged != null) {
				this.GroupChanged (source,new GroupChangedEventArgs(groupSeperator));
			}
		}
			
		
		#endregion
		

		public SharpIndexCollection IndexList {
			get {
				return indexList;
			}
		}
		
		
		public ReportSettings ReportSettings {
			get {
				return reportSettings;
			}
		}
		#region Building Groups
		
		private static void WriteToIndexFile (SharpIndexCollection destination,BaseComparer comparer) {
			SortComparer sc = comparer as SortComparer;
//			if (sc != null) {
//				System.Console.WriteLine("\t {0} - <{1}>",comparer.ListIndex, comparer.ObjectArray[0].ToString());
//			} else {
//				System.Console.WriteLine("Wrong comparer");
//			}
			destination.Add(comparer);
		}
		
		
		private static GroupSeperator BuildGroupSeperator (BaseComparer newGroup,int groupLevel) {
			
			GroupSeperator seperator = new GroupSeperator (newGroup.ColumnCollection,
			                                               newGroup.ListIndex,
			                                               newGroup.ObjectArray,
			                                               groupLevel);
			
//			System.Console.WriteLine("Add Parent <{0}>",seperator.ObjectArray[0].ToString());
			return seperator;
		}
		
		
		protected void MakeGroupedIndexList (SharpIndexCollection sourceList) {
			if (sourceList == null) {
				throw new ArgumentNullException("sourceList");
			}
			int level = 0;
			this.indexList.Clear();
			
			
			SortComparer compareComparer = null;
			GroupSeperator parent = null;
//			System.Console.WriteLine("MakeGroupedIndexList with {0} rows",sourceList.Count);
			for (int i = 0;i < sourceList.Count ;i++ ) {
				SortComparer currentComparer = (SortComparer)sourceList[i];
				/*
				System.Console.WriteLine("\t\t\t performing nr {0}",i);
				if (i == 68) {
					System.Console.WriteLine("last");
				}
				*/
				if (compareComparer != null) {
					string str1,str2;
					str1 = currentComparer.ObjectArray[0].ToString();
					str2 = compareComparer.ObjectArray[0].ToString();
					int compareVal = str1.CompareTo(str2);
					
					if (compareVal != 0) {
						BaseListStrategy.WriteToIndexFile(parent.GetChildren,compareComparer);
						parent = BaseListStrategy.BuildGroupSeperator (currentComparer,level);
						this.indexList.Add(parent);
						BaseListStrategy.WriteToIndexFile(parent.GetChildren,currentComparer);
					} else {
						BaseListStrategy.WriteToIndexFile(parent.GetChildren,compareComparer);
						
					}
				}
				else {
					parent = BaseListStrategy.BuildGroupSeperator (currentComparer,level);
				this.indexList.Add(parent);
				}		
				compareComparer = (SortComparer)sourceList[i];
			}
			BaseListStrategy.WriteToIndexFile(parent.GetChildren,compareComparer);
		}
		
		#endregion
		
		protected static void CheckSortArray (SharpIndexCollection arr,string text){

			System.Console.WriteLine("{0}",text);
			
			if (arr != null) {
				int row = 0;
				foreach (BaseComparer bc in arr) {
					GroupSeperator sep = bc as GroupSeperator;
					if (sep != null) {

						
					} else {
						object [] oarr = bc.ObjectArray;
//						tabs = "\t";
						for (int i = 0;i < oarr.Length ;i++ ) {
							string str = oarr[i].ToString();
							System.Console.WriteLine("\t\t row: {0} {1}",row,str);
						}
						row ++;
					}
					
				}
			}
			System.Console.WriteLine("-----End of <CheckSortArray>-----------");
			
		}
		public  virtual void Reset() {
			this.indexList.CurrentPosition = -1;
			this.NotifyResetList();
		}
		
		public virtual object Current {
			get {
				
				throw new NotImplementedException();
			}
		}
		
		public virtual bool MoveNext(){
			return(++this.indexList.CurrentPosition<this.indexList.Count);
		}
		
		
		
		#region SharpReportCore.IDataViewStrategy interface implementation
		
		public virtual ColumnCollection AvailableFields {
			get {
				return new ColumnCollection();
			}
		}
		
		public virtual int Count {
			get {
				return 0;
			}
		}
		
		public virtual int CurrentRow {
			get {
				return this.indexList.CurrentPosition;
			}
			set {
				if ((value > -1)|| (value > this.indexList.Count)){
					this.indexList.CurrentPosition = value;
				}
			}
		}
		
		public bool HasMoreData {
			get {
				return true;
			}
		}
		
		public virtual bool IsSorted {
			get {
				return this.isSorted;
			}
			set {
				this.isSorted = value;
			}
		}
		
		public bool IsFiltered {
			get {
				return this.isFiltered;
			} set {
				this.isFiltered = value;
			}
		}
		
		public bool IsGrouped {
			get {
				return this.isGrouped;
			}
		}
		
		protected virtual void Group() {
			if (this.indexList != null) {
				this.isGrouped = true;
				this.isSorted = true;
			} else {
				throw new SharpReportException ("BaseListStrategy:Group Sorry, no IndexList");
			}
		
		}
		
		
	
		
		public virtual void Sort() {
			this.indexList.Clear();
		}
		
	
		
		public virtual void Bind() {
			
		}
		
		public  virtual void Fill(IItemRenderer item) {
		
		}
		
		public  bool HasChilds {
			get {
				if (this.IsGrouped == true) {
					GroupSeperator gs = (GroupSeperator)this.indexList[this.CurrentRow] as GroupSeperator;
					if (gs != null) {
						return (gs.GetChildren.Count > 0);
					} else {
						return false;
					}
				} else {
					return false;
				}
			}
		}
		
		public SharpIndexCollection ChildRows {
			get {
				if (this.IsGrouped == true) {
					GroupSeperator gs = (GroupSeperator)this.indexList[this.CurrentRow] as GroupSeperator;
					if (gs != null) {
						return (gs.GetChildren);
					} else {
						return null;
					}
				} else {
					return null;
				}
			}
		}
		
		public virtual void Dispose(){
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}
		
		~BaseListStrategy(){
			Dispose(false);
		}
		
		protected virtual void Dispose(bool disposing){
			if (disposing) {
				// Free other state (managed objects).
				if (this.indexList != null) {
					this.indexList.Clear();
					this.indexList = null;
				}
			}
			
			// Release unmanaged resources.
			// Set large fields to null.
			// Call Dispose on your base class.
		}
		#endregion
		
	}
}
