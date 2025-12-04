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
 * @class SimplifyIoC.Injectors.Injector
 * 
 * Supplies injection for all mapped dependencies. 
 * 
 * Extension satisfies injection dependencies. Works in conjuntion with 
 * (and therefore relies on) the Reflector.
 * 
 * Dependencies may be Constructor injections (all parameters will be satisfied),
 * or setter injections.
 * 
 * Classes utilizing this injector must be marked with the following metatags:
 * <ul>
 *  <li>[Inject] - Use this metatag on any setter you wish to have supplied by injection.</li>
 *  <li>[Construct] - Use this metatag on the specific Constructor you wish to inject into when using Constructor injection. If you omit this tag, the Constructor with the shortest list of dependencies will be selected automatically.</li>
 *  <li>[PostConstruct] - Use this metatag on any method(s) you wish to fire directly after dependencies are supplied</li>
 * </ul>
 * 
 * The Injection system is quite loud and specific where dependencies are unmapped,
 * throwing Exceptions to warn you. This is exceptionally useful in ensuring that
 * your app is well structured.
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using SimplifyIoC.Reflectors;

namespace SimplifyIoC.Injectors
{
    public class Injector
    {
        private Dictionary<IInjectionBinding, int> _infinityLock;
        private const int _INFINITY_LIMIT = 10;

        public InjectorFactory factory { get; set; } = new InjectorFactory();
        public IInjectionBinder binder { get; set; }
        public ReflectionBinder reflector { get; set; }

        public object Instantiate(IInjectionBinding binding, bool tryInjectHere)
        {
            FailIf(binder == null, "Attempt to instantiate from Injector without a Binder", InjectionExceptionType.NO_BINDER);
            //FailIf(factory == null, "Attempt to inject into Injector without a Factory", InjectionExceptionType.NO_FACTORY);

            ArmorAgainstInfiniteLoops(binding);

            object retv = null;
            Type reflectionType = null;

            if (binding.value is Type type)
            {
                reflectionType = type;
            }
            else if (binding.value == null)
            {
                var tl = binding.key as object[];
                reflectionType = tl[0] as Type;
                if (reflectionType.IsPrimitive || reflectionType == typeof(Decimal) || reflectionType == typeof(string))
                {
                    retv = binding.value;
                }
            }
            else
            {
                retv = binding.value;
            }

            if (retv == null) //If we don't have an existing value, go ahead and create one.
            {

                var reflection = reflector.Get(reflectionType);

                var parameterTypes = reflection.constructorParameters;
                var parameterNames = reflection.constructorParameterNames;

                var aa = parameterTypes.Length;
                var args = new object[aa];
                for (var a = 0; a < aa; a++)
                {
                    args[a] = GetValueInjection(parameterTypes[a] as Type, parameterNames[a], reflectionType, null);
                }
                retv = factory.Get(binding, args);

                if (tryInjectHere)
                {
                    TryInject(binding, retv);
                }
            }
            _infinityLock = null; //Clear our infinity lock so the next time we instantiate we don't consider this a circular dependency

            return retv;
        }

        public object TryInject(IInjectionBinding binding, object target)
        {
            //If the InjectorFactory returns null, just return it. Otherwise inject the retv if it needs it
            //This could happen if Activator.CreateInstance returns null
            if (target == null) return null;
            if (binding.toInject)
            {
                target = Inject(target, false);
            }

            if (binding.type == InjectionBindingType.SINGLETON || binding.type == InjectionBindingType.VALUE)
            {
                //prevent double-injection
                binding.ToInject(false);
            }
            return target;
        }

        public object Inject(object target)
        {
            return Inject(target, true);
        }

        public object Inject(object target, bool attemptConstructorInjection)
        {
            FailIf(binder == null, "Attempt to inject into Injector without a Binder", InjectionExceptionType.NO_BINDER);
            FailIf(reflector == null, "Attempt to inject without a reflector", InjectionExceptionType.NO_REFLECTOR);
            FailIf(target == null, "Attempt to inject into null instance", InjectionExceptionType.NULL_TARGET);

            //Some things can't be injected into. Bail out.
            var t = target.GetType();
            if (t.IsPrimitive || t == typeof(Decimal) || t == typeof(string))
            {
                return target;
            }

            var reflection = reflector.Get(t);

            if (attemptConstructorInjection)
            {
                target = PerformConstructorInjection(target, reflection);
            }
            PerformSetterInjection(target, reflection);
            PostInject(target, reflection);
            return target;
        }

        public void Uninject(object target)
        {
            FailIf(binder == null, "Attempt to inject into Injector without a Binder", InjectionExceptionType.NO_BINDER);
            FailIf(reflector == null, "Attempt to inject without a reflector", InjectionExceptionType.NO_REFLECTOR);
            FailIf(target == null, "Attempt to inject into null instance", InjectionExceptionType.NULL_TARGET);

            var t = target.GetType();
            if (t.IsPrimitive || t == typeof(Decimal) || t == typeof(string))
            {
                return;
            }

            var reflection = reflector.Get(t);

            PerformUninjection(target, reflection);
        }

        private object PerformConstructorInjection(object target, ReflectedClass reflection)
        {
            FailIf(target == null, "Attempt to perform constructor injection into a null object", InjectionExceptionType.NULL_TARGET);
            FailIf(reflection == null, "Attempt to perform constructor injection without a reflection", InjectionExceptionType.NULL_REFLECTION);

            var constructor = reflection.constructor;
            FailIf(constructor == null, "Attempt to construction inject a null constructor", InjectionExceptionType.NULL_CONSTRUCTOR);

            var parameterTypes = reflection.constructorParameters;
            var parameterNames = reflection.constructorParameterNames;
            var values = new object[parameterTypes.Length];

            var i = 0;
            foreach (var type in parameterTypes)
            {
                values[i] = GetValueInjection(type, parameterNames[i], target, null);
                i++;
            }
            if (values.Length == 0)
            {
                return target;
            }

            var constructedObj = constructor.Invoke(values);
            return (constructedObj == null) ? target : constructedObj;
        }

        private void PerformSetterInjection(object target, ReflectedClass reflection)
        {
            FailIf(target == null, "Attempt to inject into a null object", InjectionExceptionType.NULL_TARGET);
            FailIf(reflection == null, "Attempt to inject without a reflection", InjectionExceptionType.NULL_REFLECTION);

            foreach (var attr in reflection.setters)
            {
                var value = GetValueInjection(attr.type, attr.name, target, attr.propertyInfo);
                InjectValueIntoPoint(value, target, attr.propertyInfo);
            }
        }

        private object GetValueInjection(Type t, object name, object target, PropertyInfo propertyInfo)
        {
            IInjectionBinding suppliedBinding = null;
            if (target != null)
            {
                suppliedBinding = binder.GetSupplier(t, target is Type ? target as Type : target.GetType());
            }

            var binding = suppliedBinding ?? binder.GetBinding(t, name);

            FailIf(binding == null, "Attempt to Instantiate a null binding", InjectionExceptionType.NULL_BINDING, t, name, target, propertyInfo);
            if (binding.type == InjectionBindingType.VALUE)
            {
                if (!binding.toInject)
                {
                    return binding.value;
                }
                var retv = Inject(binding.value, false);
                binding.ToInject(false);
                return retv;
            }
            if (binding.type == InjectionBindingType.SINGLETON)
            {
                if (binding.value is Type || binding.value == null)
                {
                    Instantiate(binding, true);
                }
                return binding.value;
            }
            return Instantiate(binding, true);
        }

        //Inject the value into the target at the specified injection point
        private void InjectValueIntoPoint(object value, object target, PropertyInfo point)
        {
            FailIf(target == null, "Attempt to inject into a null target", InjectionExceptionType.NULL_TARGET);
            FailIf(point == null, "Attempt to inject into a null point", InjectionExceptionType.NULL_INJECTION_POINT);
            FailIf(value == null, "Attempt to inject null into a target object", InjectionExceptionType.NULL_VALUE_INJECTION);

            point.SetValue(target, value, null);
        }

        //After injection, call any methods labelled with the [PostConstruct] tag
        private void PostInject(object target, ReflectedClass reflection)
        {
            FailIf(target == null, "Attempt to PostConstruct a null target", InjectionExceptionType.NULL_TARGET);
            FailIf(reflection == null, "Attempt to PostConstruct without a reflection", InjectionExceptionType.NULL_REFLECTION);

            var postConstructors = reflection.postConstructors;
            if (postConstructors != null)
            {
                foreach (var method in postConstructors)
                {
                    method.Invoke(target, null);
                }
            }
        }

        //Note that uninjection can only clean publicly settable points
        private void PerformUninjection(object target, ReflectedClass reflection)
        {
            foreach (var attr in reflection.setters)
                attr.propertyInfo.SetValue(target, null, null);
        }

        private void FailIf(bool condition, string message, InjectionExceptionType type)
        {
            FailIf(condition, message, type, null, null, null);
        }

        private void FailIf(bool condition, string message, InjectionExceptionType type, Type t, object name)
        {
            FailIf(condition, message, type, t, name, null);
        }

        private void FailIf(bool condition, string message, InjectionExceptionType type, Type t, object name, object target, PropertyInfo propertyInfo)
        {
            if (condition)
            {
                if (propertyInfo != null)
                {
                    message += "\n\t\ttarget property: " + propertyInfo.Name;
                }
                FailIf(true, message, type, t, name, target);
            }
        }

        private void FailIf(bool condition, string message, InjectionExceptionType type, Type t, object name, object target)
        {
            if (condition)
            {
                message += "\n\t\ttarget: " + target;
                message += "\n\t\ttype: " + t;
                message += "\n\t\tname: " + name;
                throw new InjectionException(message, type);
            }
        }

        private void ArmorAgainstInfiniteLoops(IInjectionBinding binding)
        {
            if (binding == null)
            {
                return;
            }
            if (_infinityLock == null)
            {
                _infinityLock = new Dictionary<IInjectionBinding, int>();
            }
            if (_infinityLock.ContainsKey(binding) == false)
            {
                _infinityLock.Add(binding, 0);
            }
            _infinityLock[binding] += 1;
            if (_infinityLock[binding] > _INFINITY_LIMIT)
            {
                throw new InjectionException("There appears to be a circular dependency. Terminating loop.", InjectionExceptionType.CIRCULAR_DEPENDENCY);
            }
        }
    }
}