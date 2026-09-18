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
        //P0#5 修复：用递归深度决定锁的清空时机，避免内层 Instantiate 返回时清掉外层的计数
        private int _instantiationDepth;

        public InjectorFactory factory { get; set; } = new InjectorFactory();
        public IInjectionBinder binder { get; set; }
        public ReflectionBinder reflector { get; set; }

        public object Instantiate(IInjectionBinding binding, bool tryInjectHere)
        {
            FailIf(binder == null, "Attempt to instantiate from Injector without a Binder");
            FailIf(factory == null, "Attempt to inject into Injector without a Factory");

            _instantiationDepth++;
            try
            {
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
                    //P0#9 修复：基元类型分支原来只赋 null 又继续反射 int/string，
                    //现直接返回类型默认值（string 无默认构造，保持 null）并立即返回
                    if (reflectionType != null && (reflectionType.IsPrimitive || reflectionType == typeof(Decimal)))
                    {
                        return Activator.CreateInstance(reflectionType);
                    }
                    if (reflectionType == typeof(string))
                    {
                        return null;
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

                    //2.3.b：无参构造复用共享空数组，避免每次实例化的分配
                    var aa = parameterTypes.Length;
                    var args = aa == 0 ? Array.Empty<object>() : new object[aa];
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

                return retv;
            }
            finally
            {
                _instantiationDepth--;
                //仅当最外层 Instantiate 结束时才清空循环依赖计数，
                //递归内层返回时保留，环形依赖才能累计到阈值被检出
                if (_instantiationDepth == 0)
                {
                    _infinityLock = null;
                }
            }
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

            if (binding.type == InjectionBindingType.Singleton || binding.type == InjectionBindingType.Value)
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
            FailIf(binder == null, "Attempt to inject into Injector without a Binder");
            FailIf(reflector == null, "Attempt to inject without a reflector");
            FailIf(target == null, "Attempt to inject into null instance");

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
            FailIf(binder == null, "Attempt to inject into Injector without a Binder");
            FailIf(reflector == null, "Attempt to inject without a reflector");
            FailIf(target == null, "Attempt to inject into null instance");

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
            FailIf(target == null, "Attempt to perform constructor injection into a null object");
            FailIf(reflection == null, "Attempt to perform constructor injection without a reflection");

            var constructor = reflection.constructor;
            FailIf(constructor == null, "Attempt to construction inject a null constructor");

            var parameterTypes = reflection.constructorParameters;
            var parameterNames = reflection.constructorParameterNames;
            var aa = parameterTypes.Length;

            //2.3.b：无参构造提前返回，跳过参数数组分配
            //（原逻辑在 aa==0 时也会先 new object[0] 再走 length 检查）
            if (aa == 0)
            {
                return target;
            }

            //2.3.b 勘误说明：设计报告 §12 建议将 constructor.Invoke 换成 CreateDelegate，
            //但 Delegate.CreateDelegate 只接受 MethodInfo，ConstructorInfo 无法使用；
            //Expression.Compile（IL2CPP 解释执行）与 Reflection.Emit（不可用）均被排除，
            //new T() 泛型工厂在 IL2CPP 下也退化回 Activator。参数化构造的委托化
            //只能由 v2 源生成器（preGenerated 钩子）在编译期完成。
            var values = new object[aa];

            var i = 0;
            foreach (var type in parameterTypes)
            {
                values[i] = GetValueInjection(type, parameterNames[i], target, null);
                i++;
            }

            var constructedObj = constructor.Invoke(values);
            return (constructedObj == null) ? target : constructedObj;
        }

        private void PerformSetterInjection(object target, ReflectedClass reflection)
        {
            FailIf(target == null, "Attempt to inject into a null object");
            FailIf(reflection == null, "Attempt to inject without a reflection");

            foreach (var attr in reflection.setters)
            {
                var value = GetValueInjection(attr.type, attr.name, target, attr.propertyInfo);
                InjectValueIntoPoint(value, target, attr);
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

            FailIf(binding == null, "Attempt to Instantiate a null binding", t, name, target, propertyInfo);
            if (binding.type == InjectionBindingType.Value)
            {
                if (!binding.toInject)
                {
                    return binding.value;
                }
                var retv = Inject(binding.value, false);
                binding.ToInject(false);
                return retv;
            }
            if (binding.type == InjectionBindingType.Singleton)
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
        private void InjectValueIntoPoint(object value, object target, ReflectedAttribute point)
        {
            FailIf(target == null, "Attempt to inject into a null target");
            FailIf(point == null, "Attempt to inject into a null point");
            FailIf(value == null, "Attempt to inject null into a target object");

            //2.3.a：优先走缓存的 setter 委托；值类型等 AOT 不安全场景 setter 为 null，回落反射
            if (point.setter != null)
            {
                point.setter(target, value);
            }
            else
            {
                point.propertyInfo.SetValue(target, value, null);
            }
        }

        //After injection, call any methods labelled with the [PostConstruct] tag
        private void PostInject(object target, ReflectedClass reflection)
        {
            FailIf(target == null, "Attempt to PostConstruct a null target");
            FailIf(reflection == null, "Attempt to PostConstruct without a reflection");

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
            {
                //2.3.a：与注入路径同策略——委托优先，值类型回落反射
                if (attr.setter != null)
                {
                    attr.setter(target, null);
                }
                else
                {
                    attr.propertyInfo.SetValue(target, null, null);
                }
            }
        }

        private void FailIf(bool condition, string message)
        {
            FailIf(condition, message, null, null, null);
        }

        private void FailIf(bool condition, string message,  Type t, object name)
        {
            FailIf(condition, message, t, name, null);
        }

        private void FailIf(bool condition, string message, Type t, object name, object target, PropertyInfo propertyInfo)
        {
            if (condition)
            {
                if (propertyInfo != null)
                {
                    message += "\n\t\ttarget property: " + propertyInfo.Name;
                }
                FailIf(true, message, t, name, target);
            }
        }

        private void FailIf(bool condition, string message, Type t, object name, object target)
        {
            if (condition)
            {
                message += "\n\t\ttarget: " + target;
                message += "\n\t\ttype: " + t;
                message += "\n\t\tname: " + name;
                throw new Exception(message);
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
                throw new Exception("There appears to be a circular dependency. Terminating loop.");
            }
        }
    }
}