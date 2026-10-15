/*
 * @class SimplifyIoC.Injectors.Injector
 *
 * 为所有映射好的依赖提供注入。
 *
 * 注入依赖可以来自构造注入（所有参数都会被满足），也可以来自属性注入。
 * 工作原理依赖反射器（Reflector）对类的解析结果。
 *
 * 使用注入的类可以标注以下特性：
 * - [Inject]：标注任何希望由注入提供的属性；
 * - [Construct]：使用构造注入时，标注希望被注入的构造函数；
 *   省略时自动选择参数最少的构造函数；
 * - [PostConstruct]：标注希望在依赖注入完成后立即执行的方法。
 *
 * 当依赖未映射时，注入系统会抛出异常大声告警——
 * 这对保证应用结构正确非常有用。
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using SimplifyIoC.Framework;
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

        /// <summary>
        /// 3.4：新增可选的调用级作用域。scope 里的参数优先于全局绑定被解析，
        /// 传 null 时行为与改造前完全一致（既有调用点无需改动）。
        /// </summary>
        public object Instantiate(IInjectionBinding binding, bool tryInjectHere, InjectionScope scope = null)
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
                        args[a] = GetValueInjection(parameterTypes[a] as Type, parameterNames[a], reflectionType, null, scope);
                    }
                    retv = factory.Get(binding, args);

                    if (tryInjectHere)
                    {
                        TryInject(binding, retv, scope);
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

        public object TryInject(IInjectionBinding binding, object target, InjectionScope scope = null)
        {
            //If the InjectorFactory returns null, just return it. Otherwise inject the retv if it needs it
            //This could happen if Activator.CreateInstance returns null
            if (target == null) return null;
            if (binding.toInject)
            {
                target = Inject(target, false, scope);
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

        public object Inject(object target, bool attemptConstructorInjection, InjectionScope scope = null)
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
                target = PerformConstructorInjection(target, reflection, scope);
            }
            PerformSetterInjection(target, reflection, scope);
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

        private object PerformConstructorInjection(object target, ReflectedClass reflection, InjectionScope scope)
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
                values[i] = GetValueInjection(type, parameterNames[i], target, null, scope);
                i++;
            }

            var constructedObj = constructor.Invoke(values);
            return (constructedObj == null) ? target : constructedObj;
        }

        private void PerformSetterInjection(object target, ReflectedClass reflection, InjectionScope scope)
        {
            FailIf(target == null, "Attempt to inject into a null object");
            FailIf(reflection == null, "Attempt to inject without a reflection");

            foreach (var attr in reflection.setters)
            {
                var value = GetValueInjection(attr.type, attr.name, target, attr.propertyInfo, scope);
                InjectValueIntoPoint(value, target, attr);
            }
        }

        private object GetValueInjection(Type t, object name, object target, PropertyInfo propertyInfo, InjectionScope scope)
        {
            //3.4.a：调用级作用域优先——信号载荷这类"仅本次调用有效"的参数由 Scope 显式传递，
            //不再临时 Bind 进全局容器。作用域内不含 null 值（构建阶段已排除），故命中即返回。
            if (scope != null && scope.TryGet(t, out var scopedValue))
            {
                return scopedValue;
            }

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
                var retv = Inject(binding.value, false, scope);
                binding.ToInject(false);
                return retv;
            }
            if (binding.type == InjectionBindingType.Singleton)
            {
                if (binding.value is Type || binding.value == null)
                {
                    Instantiate(binding, true, scope);
                }
                return binding.value;
            }
            //3.4.c 连带修复：作用域必须继续下钻——依赖对象的构造/注入与本次调用同属一个作用域。
            //旧实现把参数临时绑进全局容器，嵌套构造链全程可见；若这里丢掉 scope，
            //「mediator/command → 依赖服务 → view/载荷」这类链路就会查不到绑定而失败。
            return Instantiate(binding, true, scope);
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
                //2.3.c：委托优先（反射缓存 Action<object>）；AOT 不安全槽位为 null，回落 method.Invoke
                var actions = reflection.postConstructorActions;
                for (var i = 0; i < postConstructors.Length; i++)
                {
                    var action = (actions != null && i < actions.Length) ? actions[i] : null;
                    if (action != null)
                    {
                        action(target);
                    }
                    else
                    {
                        postConstructors[i].Invoke(target, null);
                    }
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