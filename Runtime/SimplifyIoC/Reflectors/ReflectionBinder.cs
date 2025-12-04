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
 * @class SimplifyIoC.Reflectors.ReflectionBinder
 * 
 * Uses System.Reflection to create `ReflectedClass` instances.
 * 
 * Reflection is a slow process. This binder isolates the calls to System.Reflector 
 * and caches the result, meaning that Reflection is performed only once per class.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SimplifyIoC.Framework;

namespace SimplifyIoC.Reflectors
{
    public class ReflectionBinder : SimplifyIoC.Framework.Binder, IReflectionBinder
    {
        public ReflectionBinder(){}

        public IReflectedClass Get<T>()
        {
            return Get(typeof(T));
        }

        public IReflectedClass Get(Type type)
        {
            var binding = GetBinding(type);
            IReflectedClass retv;
            if (binding == null)
            {
                binding = GetRawBinding();
                IReflectedClass reflected = new ReflectedClass();
                MapPreferredConstructor(reflected, binding, type);
                MapSetters(reflected, binding, type); //map setters before mapping methods
                MapMethods(reflected, binding, type);
                binding.Bind(type).To(reflected);
                retv = binding.value as IReflectedClass;
                retv.preGenerated = false;
            }
            else
            {
                retv = binding.value as IReflectedClass;
                retv.preGenerated = true;
            }
            return retv;
        }

        public override IBinding GetRawBinding()
        {
            var binding = base.GetRawBinding();
            binding.valueConstraint = BindingConstraintType.One;
            return binding;
        }

        private void MapPreferredConstructor(IReflectedClass reflected, IBinding binding, Type type)
        {
            var constructor = FindPreferredConstructor(type);
            if (constructor == null)
            {
                throw new ReflectionException("The reflector requires concrete classes.\nType " + type + " has no constructor. Is it an interface?", ReflectionExceptionType.CANNOT_REFLECT_INTERFACE);
            }
            var parameters = constructor.GetParameters();


            var paramList = new Type[parameters.Length];
            var names = new object[parameters.Length];
            var i = 0;
            foreach (var param in parameters)
            {
                var paramType = param.ParameterType;
                paramList[i] = paramType;

                var attributes = param.GetCustomAttributes(typeof(Name), false);
                if (attributes.Length > 0)
                {
                    names[i] = ((Name)attributes[0]).name;
                }
                i++;
            }
            reflected.constructor = constructor;
            reflected.constructorParameters = paramList;
            reflected.constructorParameterNames = names;
        }

        //Look for a constructor in the order:
        //1. Only one (just return it, since it's our only option)
        //2. Tagged with [Construct] tag
        //3. The constructor with the fewest parameters
        private ConstructorInfo FindPreferredConstructor(Type type)
        {
            var constructors = type.GetConstructors(BindingFlags.FlattenHierarchy |
                                                    BindingFlags.Public |
                                                    BindingFlags.Instance |
                                                    BindingFlags.InvokeMethod);
            if (constructors.Length == 1)
            {
                return constructors[0];
            }
            int len;
            var shortestLen = int.MaxValue;
            ConstructorInfo shortestConstructor = null;
            foreach (var constructor in constructors)
            {
                var taggedConstructors = constructor.GetCustomAttributes(typeof(Construct), true);
                if (taggedConstructors.Length > 0)
                {
                    return constructor;
                }
                len = constructor.GetParameters().Length;
                if (len < shortestLen)
                {
                    shortestLen = len;
                    shortestConstructor = constructor;
                }
            }
            return shortestConstructor;
        }

        private void MapMethods(IReflectedClass reflected, IBinding binding, Type type)
        {
            var methods = type.GetMethods(BindingFlags.FlattenHierarchy |
                                          BindingFlags.Public |
                                          BindingFlags.NonPublic |
                                          BindingFlags.Instance |
                                          BindingFlags.InvokeMethod);
            var methodList = new ArrayList();
            var attrMethods = new List<KeyValuePair<MethodInfo, Attribute>>();
            foreach (var method in methods)
            {
                var tagged = method.GetCustomAttributes(typeof(PostConstruct), true);
                if (tagged.Length > 0)
                {
                    methodList.Add(method);
                    attrMethods.Add(new KeyValuePair<MethodInfo, Attribute>(method, (Attribute)tagged[0]));
                }
                var listensToAttr = method.GetCustomAttributes(typeof(ListensTo), true);
                if (listensToAttr.Length > 0)
                {

                    for (var i = 0; i < listensToAttr.Length; i++)
                    {
                        attrMethods.Add(new KeyValuePair<MethodInfo, Attribute>(method, (ListensTo)listensToAttr[i]));
                    }
                }
            }

            methodList.Sort(new PriorityComparer());
            reflected.postConstructors = (MethodInfo[])methodList.ToArray(typeof(MethodInfo));
            reflected.attrMethods = attrMethods.ToArray();
        }

        private void MapSetters(IReflectedClass reflected, IBinding binding, Type type)
        {
            var privateMembers = type.FindMembers(MemberTypes.Property,
                                                    BindingFlags.FlattenHierarchy |
                                                    BindingFlags.SetProperty |
                                                    BindingFlags.NonPublic |
                                                    BindingFlags.Instance,
                                                    null, null);
            foreach (var member in privateMembers)
            {
                var injections = member.GetCustomAttributes(typeof(Inject), true);
                if (injections.Length > 0)
                {
                    throw new ReflectionException("The class " + type.Name + " has a non-public Injection setter " + member.Name + ". Make the setter public to allow injection.", ReflectionExceptionType.CANNOT_INJECT_INTO_NONPUBLIC_SETTER);
                }
            }

            var members = type.FindMembers(MemberTypes.Property,
                                                          BindingFlags.FlattenHierarchy |
                                                          BindingFlags.SetProperty |
                                                          BindingFlags.Public |
                                                          BindingFlags.Instance,
                                                          null, null);

            //propertyinfo.name to reflectedattribute
            //This is to test for 'hidden' or overridden injections.
            var namedAttributes = new Dictionary<string, ReflectedAttribute>();

            foreach (var member in members)
            {
                var injections = member.GetCustomAttributes(typeof(Inject), true);
                if (injections.Length > 0)
                {
                    var attr = injections[0] as Inject;
                    var point = member as PropertyInfo;
                    var baseType = member.DeclaringType.BaseType;
                    var hasInheritedProperty = baseType != null ? baseType.GetProperties().Any(p => p.Name == point.Name) : false;
                    var toAddOrOverride = true; //add or override by default

                    //if we have an overriding value, we need to know whether to override or leave it out.
                    //We leave out the base if it's hidden
                    //And we add if its overriding.
                    if (namedAttributes.ContainsKey(point.Name))
                        toAddOrOverride = hasInheritedProperty; //if this attribute has been 'hidden' by a new or override keyword, we should not add this.

                    if (toAddOrOverride)
                        namedAttributes[point.Name] = new ReflectedAttribute(point.PropertyType, point, attr.name);
                }
            }
            reflected.setters = namedAttributes.Values.ToArray();
        }
    }

    class PriorityComparer : IComparer
    {
        int IComparer.Compare(Object x, Object y)
        {

            var pX = GetPriority(x as MethodInfo);
            var pY = GetPriority(y as MethodInfo);

            return (pX < pY) ? -1 : (pX == pY) ? 0 : 1;
        }

        private int GetPriority(MethodInfo methodInfo)
        {
            var attr = methodInfo.GetCustomAttributes(true)[0] as PostConstruct;
            var priority = attr.priority;
            return priority;
        }
    }
}