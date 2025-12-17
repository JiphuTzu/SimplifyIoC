/*
 * Copyright 2013 ThirdMotion, Inc.
 *
 *	Licensed under the Apache License, Version 2.0 (the "License");
 *	you may not use this file except in compliance with the License.
 *	You may obtain a copy of the License at
 *
 *		http://www.apache.org/licenses/LICENSE-2.0
 *
 *		Unless required by applicable law or agreed to in writing, software
 *		distributed under the License is distributed on an "AS IS" BASIS,
 *		WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *		See the License for the specific language governing permissions and
 *		limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SimplifyIoC.Injectors;
using SimplifyIoC.Mediations;

namespace SimplifyIoC.ImplicitBinds
{
    public class ImplicitBinder : IImplicitBinder
	{
		[Inject]
		public IInjectionBinder injectionBinder { get; set; }

		[Inject]
		public IMediationBinder mediationBinder { get; set; }


		//Hold a copy of the assembly so we aren't retrieving this multiple times. 
		public Assembly assembly { get; set; }


		[PostConstruct]
		public void PostConstruct()
		{
			assembly = Assembly.GetExecutingAssembly();
		}

		/// <summary>
		/// Search through indicated namespaces and scan for all annotated classes.
		/// Automatically create bindings
		/// </summary>
		/// <param name="usingNamespaces">Array of namespaces. Compared using StartsWith. </param>

		public virtual void ScanForAnnotatedClasses(params string[] usingNamespaces)
		{
			if (assembly != null)
			{

				IEnumerable<Type> types = assembly.GetExportedTypes();

				var typesInNamespaces = new List<Type>();
				var namespacesLength = usingNamespaces.Length;
				for (var ns = 0; ns < namespacesLength; ns++)
				{
					typesInNamespaces.AddRange(types.Where(t => !string.IsNullOrEmpty(t.Namespace) && t.Namespace.StartsWith(usingNamespaces[ns])));
				}

				var implementsBindings = new List<ImplicitBindingVO>();
				var implementedByBindings = new List<ImplicitBindingVO>();

				foreach (var type in typesInNamespaces)
				{
					var implements = type.GetCustomAttributes(typeof (Implements), true);
					var implementedBy = type.GetCustomAttributes(typeof(ImplementedBy), true);
					var mediated = type.GetCustomAttributes(typeof(MediatedBy), true);
					var mediates = type.GetCustomAttributes(typeof(Mediates), true);

					#region Concrete and Interface Bindings

					//Interfaces first
					if (implementedBy.Any())
					{

						var implBy = (ImplementedBy)implementedBy.First();
						if (implBy.defaultType.GetInterfaces().Contains(type)) //Verify this DefaultType exists and implements the tagged interface
						{
							implementedByBindings.Add(new ImplicitBindingVO(type, implBy.defaultType, implBy.scope == InjectionBindingScope.CrossContext, null));
						}
						else
						{
							throw new Exception("Default Type: " + implBy.defaultType.Name + " does not implement annotated interface " + type.Name);
						}

					}

					if (implements.Any())
					{
						var interfaces = type.GetInterfaces();
						
						object name = null;
						var isCrossContext = false;
						var bindTypes = new List<Type>();

						foreach (Implements impl in implements)
						{
							//Confirm this type implements the type specified
							if (impl.defaultInterface != null)
							{
								//Verify this Type implements the passed interface
								if (interfaces.Contains(impl.defaultInterface) || type == impl.defaultInterface)
								{
									bindTypes.Add(impl.defaultInterface);
								}
								else
								{
									throw new Exception("Annotated type " + type.Name + " does not implement Default Interface " + impl.defaultInterface.Name);
								}
							}
							else //Concrete
							{
								if (impl.name is Type)
									Console.WriteLine("You have bound a type: " + type.Name + " as the name of this implements binding. Did you mean to use the (Type, InjectionBindingScope) signature instead of the (InjectionBindingScope, object) signature?");

								bindTypes.Add(type);
							}

							isCrossContext = isCrossContext || impl.scope == InjectionBindingScope.CrossContext;
							name = name ?? impl.name;
						}

						var thisBindingVo = new ImplicitBindingVO(bindTypes, type, isCrossContext, name);

						implementsBindings.Add(thisBindingVo);
					}

					#endregion
					
					//Handle mediations here. We have no need to re-iterate over them to prioritize anything. Not yet, at least.

					#region Mediations

					Type mediatorType = null;
					Type viewType = null;
					if (mediated.Any())
					{
						viewType = type;
						mediatorType = ((MediatedBy)mediated.First()).mediatorType;

						if (mediatorType == null)
							throw new Exception("Cannot implicitly bind view of type: " + type.Name + " due to null MediatorType");
					}
					else if (mediates.Any())
					{
						mediatorType = type;
						viewType = ((Mediates)mediates.First()).viewType;

						if (viewType == null)
							throw new Exception("Cannot implicitly bind Mediator of type: " + type.Name + " due to null ViewType");
					}

					if (mediationBinder != null && viewType != null && mediatorType != null) //Bind this mediator!
						mediationBinder.Bind(viewType).To(mediatorType);

					#endregion
				}

				//implementedBy/interfaces first, then implements to give them priority (they will overwrite)
				implementedByBindings.ForEach(Bind);
				//Next implements tags, which have priority over interfaces
				implementsBindings.ForEach(Bind);
			}
			else
			{
				throw new Exception("Assembly was not initialized yet for Implicit Bindings!");
			}
		}

		private void Bind(ImplicitBindingVO toBind)
		{
			//We do not check for the existence of a binding. Because implicit bindings are weak bindings, they are overridden automatically by other implicit bindings
			//Therefore, ImplementedBy will be overriden by an Implements to that interface.

			var binding = injectionBinder.Bind(toBind.bindTypes.First());
			binding.Weak();

			for (var i = 1; i < toBind.bindTypes.Count; i++)
			{
				var bindType = toBind.bindTypes.ElementAt(i);
				binding.Bind(bindType);
			}

			binding = toBind.toType != null ?
				binding.To(toBind.toType).ToName(toBind.name).ToSingleton() :
				binding.ToName(toBind.name).ToSingleton();

			if (toBind.isCrossContext) //Bind this to the cross context injector
				binding.CrossContext();

		}

		// ReSharper disable once InconsistentNaming
		private sealed class ImplicitBindingVO
		{
			public readonly List<Type> bindTypes = new List<Type>();
			public readonly Type toType;
			public readonly bool isCrossContext;
			public readonly object name;

			public ImplicitBindingVO(Type bindType, Type toType, bool isCrossContext, object name)
			{
				bindTypes.Add(bindType);
				this.toType = toType;
				this.isCrossContext = isCrossContext;
				this.name = name;
			}

			public ImplicitBindingVO(List<Type> bindTypes, Type toType, bool isCrossContext, object name)
			{
				this.bindTypes = bindTypes;
				this.toType = toType;
				this.isCrossContext = isCrossContext;
				this.name = name;
			}
		}
	}
}
