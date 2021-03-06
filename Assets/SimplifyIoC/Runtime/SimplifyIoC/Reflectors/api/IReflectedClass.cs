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

/**
 * @interface SimplifyIoC.Reflectors.IReflectedClass
 * 
 * Interface for representation of a class.
 * 
 * A reflection represents the already-reflected class, complete with the preferred
 * constructor, the constructor parameters, post-constructor(s) and settable
 * values.
 */

using System;
using System.Collections.Generic;
using System.Reflection;

namespace SimplifyIoC.Reflectors
{
    public interface IReflectedClass
	{
		/// Get/set the preferred constructor
		ConstructorInfo constructor{ get; set;}

		/// Get/set the preferred constructor's list of parameters
		Type[] constructorParameters{ get; set;}
		object[] constructorParameterNames { get; set; }

		/// Get/set any PostConstructors. This includes inherited PostConstructors.
		MethodInfo[] postConstructors{ get; set;}

		/// Get/set the list of setter injections. This includes inherited setters.
		ReflectedAttribute[] setters { get; set; }

		/// For testing. Allows a unit test to assert whether the binding was
		/// generated on the current call, or on a prior one.
		bool preGenerated{ get; set;}

		/// MethodInfo to Attribute
		/// Any attributed method is in this collection, including postconstructs
		KeyValuePair<MethodInfo, Attribute>[] attrMethods { get; set; }

		/// [Obsolete"Strange migration to conform to C# guidelines. Removing camelCased publics"]

		bool HasSetterFor(Type type);
	}

	public struct ReflectedAttribute
	{
		public Type type;
		public object name;
		public PropertyInfo propertyInfo;

		public ReflectedAttribute(Type type, PropertyInfo propertyInfo, object name )
		{
			this.type = type;
			this.propertyInfo = propertyInfo;
			this.name = name;
		}
	}
}