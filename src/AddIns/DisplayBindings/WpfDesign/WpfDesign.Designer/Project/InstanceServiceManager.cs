using System;
using System.Collections.Generic;
using ICSharpCode.WpfDesign.Designer.OutlineView;
using ICSharpCode.WpfDesign.Designer.Services;

namespace ICSharpCode.WpfDesign.Designer
{
	/// <summary>
	/// class that allows override default services even if they need to reinstanciated each time
	/// just register by using any of the two AddInstanceService methods below
	/// </summary>
	public class InstanceServiceManager
	{
		class ActivatorClass
		{
			private Type DesiredType { get; set; }
			private Type T { get; set; }
			private object[] O { get; set; }


			public ActivatorClass(Type desiredtype, Type t, object[] o)
			{
				DesiredType = desiredtype;
				T = t;
				O = o;
			}

			public object GetInstance()
			{
				if (O == null)
					return Activator.CreateInstance(T);
				else
				{
					return Activator.CreateInstance(T, O);
				}
			}
		}
		#region Singleton instance

		private static InstanceServiceManager _instance = null;

		public static InstanceServiceManager Instance
		{
			get
			{
				if (_instance == null)
				{
					_instance = new InstanceServiceManager();
				}

				return _instance;
			}
		}


		private InstanceServiceManager()
		{
			RegisteredInstanceServices = new Dictionary<Type, object>();
			RegisteredServices = new Dictionary<Type, ActivatorClass>();
		}

		#endregion
		Dictionary<Type, object> RegisteredInstanceServices;
		Dictionary<Type, ActivatorClass> RegisteredServices;




		/// <summary>
		/// register parameterless constructor ie
		/// to register type CustomSelectionService to key ISelectionService
		/// AddInstanceService(typeof(ISelectionSErvice), typeof(CustomSelectionService))
		/// </summary>
		/// <param name="interfaceKey"></param>
		/// <param name="instance"></param>
		public static void AddInstanceService(Type interfaceKey, Type instance)
		{
			Instance.AddInstanceService(interfaceKey, instance, new object[] { });
		}

		/// <summary>
		/// register constructor that takes parameters ie
		/// to register type CustomSelectionService to key ISelectionService with string parameter CustomString as parameter
		/// AddInstanceService(typeof(ISelectionSErvice), typeof(CustomSelectionService), new object[]{CustomParameter})
		/// </summary>
		/// <param name="interfaceKey"></param>
		/// <param name="instance"></param>
		/// <param name="args"></param>
		public void AddInstanceService(Type interfaceKey, Type instance, object[] args)
		{
			if (interfaceKey == null)
				throw new ArgumentNullException("interfaceKey");
			if (instance == null)
				throw new ArgumentNullException("instance");

			if (RegisteredInstanceServices.ContainsKey(interfaceKey))
				RegisteredInstanceServices.Remove(interfaceKey);

			if (RegisteredServices.ContainsKey(interfaceKey))
				RegisteredServices.Remove(interfaceKey);
			RegisteredServices.Add(interfaceKey, new ActivatorClass(interfaceKey, instance, args));
		}


		/// <summary>
		/// Gets the service object of the specified type.
		/// Returns null when the service is not available.
		/// </summary>
		public static T GetService<T>(DesignContext context) where T : class
		{
			object instance = null;

			if (!Instance.RegisteredInstanceServices.TryGetValue(typeof(T), out instance))
			{
				ActivatorClass a;
				if (Instance.RegisteredServices.TryGetValue(typeof(T), out a))
					instance = a.GetInstance();
			}
			if(instance==null)
				instance = Instance.GetDefaultInstance(typeof(T), context);
			return instance!=null?instance as T:null;
		}



		public object GetDefaultInstance(Type key, DesignContext context)
		{
			if(key == typeof(ISelectionService)) return new DefaultSelectionService();
			if(key == typeof(IToolService)) return new DefaultToolService(context);
			if(key == typeof(UndoService)) return new UndoService();
			if(key == typeof(IErrorService)) return new DefaultErrorService(context);
			if(key == typeof(IOutlineNodeNameService)) return new OutlineNodeNameService();
			if(key == typeof(ViewService)) return new DefaultViewService(context);
			if(key == typeof(OptionService)) return new OptionService();
			if(key == typeof(XamlErrorService)) return new XamlErrorService();

			return null;
			
		}
	}
}
