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

/*
 * @class SimplifyIoC.Reflectors.ReflectedClass
 * 
 * A reflection of a class.
 * 
 * A reflection represents the already-reflected class, complete with the preferred
 * constructor, the constructor parameters, post-constructor(s) and settable
 * values.
 */

using System.Collections.Generic;
using System.Linq;
using System;
using System.Reflection;

namespace SimplifyIoC.Reflectors
{
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
    public class ReflectedClass
    {
        public ConstructorInfo constructor { get; set; }
        public Type[] constructorParameters { get; set; }
        public object[] constructorParameterNames { get; set; }
        public MethodInfo[] postConstructors { get; set; }
        public ReflectedAttribute[] setters { get; set; }
        public object[] setterNames { get; set; }
        public bool preGenerated { get; set; }
        public KeyValuePair<MethodInfo, Attribute>[] attrMethods { get; set; }

        public bool HasSetterFor(Type type)
        {
            return setters.Any(attr => attr.type == type);
        }
    }
}