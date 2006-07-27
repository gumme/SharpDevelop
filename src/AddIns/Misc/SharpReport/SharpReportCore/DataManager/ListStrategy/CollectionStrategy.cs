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
using System.Globalization;

/// <summary>
/// This Class handles all List's with IList
/// Access to Data is allway#s done by using the 'IndexList'
/// </summary>

namespace SharpReportCore {
	public class CollectionStrategy : BaseListStrategy {
		// Holds the plain Data
		
		private Type	itemType;
		private object firstItem;
		private object current;
		
		private PropertyDescriptorCollection listProperties;
		
		private SharpDataCollection<object> baseList;


		public CollectionStrategy(IList list,string dataMember,ReportSettings reportSettings):base(reportSettings) {

			if (list.Count > 0) {
				firstItem = list[0];
				itemType =  firstItem.GetType();
				
				this.baseList = new SharpDataCollection <object>(itemType);
				this.baseList.AddRange(list);
			}
			
			this.listProperties = this.baseList.GetItemProperties(null);
		}


		private void BuildGroup(){

			try {
				SharpIndexCollection groupedArray = new SharpIndexCollection();
				
				if (base.ReportSettings.GroupColumnsCollection != null) {
					if (base.ReportSettings.GroupColumnsCollection.Count > 0) {
						this.BuildSortIndex (groupedArray,base.ReportSettings.GroupColumnsCollection);
					}
				}

				base.MakeGroupedIndexList (groupedArray);


				foreach (BaseComparer bc in this.IndexList) {
					GroupSeperator gs = bc as GroupSeperator;
					
					if (gs != null) {
						System.Console.WriteLine("Group Header <{0}> with <{1}> Childs ",gs.ObjectArray[0].ToString(),gs.GetChildren.Count);
						if (gs.HasChildren) {
							foreach (SortComparer sc in gs.GetChildren) {
								
								System.Console.WriteLine("\t {0}   {1}",sc.ListIndex,sc.ObjectArray[0].ToString());										}
						}
					} else {
						SortComparer sc = bc as SortComparer;
						
						if (sc != null) {
							System.Console.WriteLine("\t Child {0}",sc.ObjectArray[0].ToString());
						}
					}
					
				}
			} catch (Exception e) {
				System.Console.WriteLine("BuildGroup {0}",e.Message);
				throw;
			}
		}
		
		private PropertyDescriptor[] BuildSortProperties (ColumnCollection col){
			PropertyDescriptor[] sortProperties = new PropertyDescriptor[col.Count];
			PropertyDescriptorCollection c = this.baseList.GetItemProperties(null);
			
			for (int criteriaIndex = 0; criteriaIndex < col.Count; criteriaIndex++){
				PropertyDescriptor descriptor = c.Find (col[criteriaIndex].ColumnName,true);
		
				if (descriptor == null){
					throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture,
					                                                  "Die Liste enth�lt keine Spalte [{0}].",
					                                                  col[criteriaIndex].ColumnName));
				}
				sortProperties[criteriaIndex] = descriptor;
			}
			return sortProperties;
		}
		
		#region Index Building
		private  void BuildSortIndex(SharpIndexCollection arrayList,ColumnCollection col) {
			PropertyDescriptor[] sortProperties = BuildSortProperties (col);
			for (int rowIndex = 0; rowIndex < this.baseList.Count; rowIndex++){
				object rowItem = this.baseList[rowIndex];
				object[] values = new object[col.Count];
				
				// Hier bereits Wertabruf um dies nicht w�hrend des Sortierens tun zu m�ssen.
				for (int criteriaIndex = 0; criteriaIndex < sortProperties.Length; criteriaIndex++){
					object value = sortProperties[criteriaIndex].GetValue(rowItem);
					// Hier auf Vertr�glichkeit testen um Vergleiche bei Sortierung zu vereinfachen.
					// Muss IComparable und gleicher Typ sein.
					
					if (value != null && value != DBNull.Value)
					{
						if (!(value is IComparable)){
							throw new InvalidOperationException("ReportDataSource:BuildSortArray - > This type doesn't support IComparable." + value.ToString());
						}
						
						values[criteriaIndex] = value;
					}
				}
				arrayList.Add(new SortComparer(col, rowIndex, values));
			}
			arrayList.Sort();
		}
		
		
		
		
		// if we have no sorting, we build the indexlist as well, so we don't need to
		//check each time we reasd data if we have to go directly or by IndexList
		private  void BuildPlainIndex(SharpIndexCollection arrayList,ColumnCollection col) {
			
			PropertyDescriptor[] sortProperties = new PropertyDescriptor[1];
			PropertyDescriptorCollection c = this.baseList.GetItemProperties(null);
			PropertyDescriptor descriptor = c[0];
			sortProperties[0] = descriptor;
			
			for (int rowIndex = 0; rowIndex < this.baseList.Count; rowIndex++){
				object[] values = new object[1];
				
				// We insert only the RowNr as a dummy value
				values[0] = rowIndex;
				arrayList.Add(new BaseComparer(col, rowIndex, values));
			}
			
		}
	
	
		#endregion
		
		#region SharpReportCore.IDataViewStrategy interface implementation
		
		public override ColumnCollection AvailableFields {
			get {
				ColumnCollection c = base.AvailableFields;
				foreach (PropertyDescriptor p in baseList.GetItemProperties(null)){
					c.Add (new AbstractColumn(p.Name,p.ComponentType));
					}
				return c;
			}
		}
		
		public override object Current {
			get {
				return this.baseList[((BaseComparer)base.IndexList[base.CurrentRow]).ListIndex];
			}
			
		}
		
		public override int Count {
			get {
				return this.baseList.Count;
			}
		}
		
		public override  int CurrentRow {
			get {
				return base.IndexList.CurrentPosition;
			}
			
			set {
				base.CurrentRow = value;
				current = this.baseList[((BaseComparer)base.IndexList[value]).ListIndex];
			}
		}

		
		public override void Sort() {
			base.Sort();
			if ((base.ReportSettings.SortColumnCollection != null)) {
				if (base.ReportSettings.SortColumnCollection.Count > 0) {
					this.BuildSortIndex (base.IndexList,
					                     base.ReportSettings.SortColumnCollection);
					
					
					base.IsSorted = true;
//					BaseListStrategy.CheckSortArray (base.IndexList,"TableStrategy - CheckSortArray");
				} else {
					this.BuildPlainIndex(base.IndexList,
					                     base.ReportSettings.SortColumnCollection);
					base.IsSorted = false;
				}
			}
		}
		
		
		public override void Reset() {
			this.CurrentRow = 0;
			base.Reset();
		}
		
		
		
		
		public override void Bind() {
			base.Bind();
			if (base.ReportSettings.GroupColumnsCollection.Count > 0) {
				this.Group ();
				Reset();
				return;
			}
			
			if (base.ReportSettings.SortColumnCollection != null) {
				this.Sort ();
			}
			Reset();
		}
		
		public override void Fill(IItemRenderer item) {
			try {
				base.Fill(item);
				if (current != null) {
					BaseDataItem baseDataItem = item as BaseDataItem;
					PropertyDescriptor p = this.listProperties.Find (baseDataItem.ColumnName,true);
					
					if (baseDataItem != null) {
						baseDataItem.DbValue = "";
						baseDataItem.DbValue = p.GetValue(this.Current).ToString();
					}
				}
			} catch (System.NullReferenceException) {

			}
			
		}
		
		protected override void Group() {
			if (base.ReportSettings.GroupColumnsCollection.Count == 0) {
				return;
			}
			this.BuildGroup();
			base.Group();
		}
		
		
		#endregion
		
		#region IDisposable
		
		public override void Dispose(){
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}
		
		~CollectionStrategy(){
			Dispose(false);
		}
		
		protected override void Dispose(bool disposing){
			try {
				if (disposing) {
					if (this.baseList != null) {
						this.baseList.Clear();
						this.baseList = null;
					}
				}
			} finally {
				base.Dispose(disposing);
				// Release unmanaged resources.
				// Set large fields to null.
				// Call Dispose on your base class.
			}
		}
		#endregion
	}
}
