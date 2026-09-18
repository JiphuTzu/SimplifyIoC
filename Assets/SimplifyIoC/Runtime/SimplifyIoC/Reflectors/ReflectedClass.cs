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
using System;
using System.Reflection;

namespace SimplifyIoC.Reflectors
{
    /// <summary>
    /// 2.3.a：由 struct 改为 class（避免携带委托字段后频繁复制），
    /// 并为每个 setter 缓存强类型委托，注入路径不再走 PropertyInfo.SetValue。
    /// </summary>
    public class ReflectedAttribute
    {
        public Type type;
        public object name;
        public PropertyInfo propertyInfo;

        /// 缓存的 setter 委托；涉及值类型时为 null（回落反射，保证 IL2CPP/AOT 安全）
        public Action<object, object> setter;

        public ReflectedAttribute(Type type, PropertyInfo propertyInfo, object name)
        {
            this.type = type;
            this.propertyInfo = propertyInfo;
            this.name = name;
            setter = BuildSetterDelegate(propertyInfo);
        }

        /// <summary>
        /// 为属性 setter 构建 Action&lt;object,object&gt; 委托。
        /// 实现：泛型辅助方法 + Delegate.CreateDelegate 生成开放实例委托，再包一层 object 适配。
        /// AOT 约束：IL2CPP 的泛型共享仅覆盖引用类型，涉及值类型（含 Nullable）时返回 null，
        /// 由调用方回落 PropertyInfo.SetValue；不使用 Expression.Compile / Reflection.Emit。
        /// </summary>
        private static Action<object, object> BuildSetterDelegate(PropertyInfo property)
        {
            if (property == null) return null;
            var setMethod = property.GetSetMethod(true);
            if (setMethod == null || !setMethod.IsPublic) return null;

            var declaringType = property.DeclaringType;
            if (declaringType == null || declaringType.IsValueType || property.PropertyType.IsValueType)
            {
                return null;
            }

            var helper = typeof(ReflectedAttribute).GetMethod(nameof(BuildSetter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var closed = helper.MakeGenericMethod(declaringType, property.PropertyType);
            return (Action<object, object>)closed.Invoke(null, new object[] { setMethod });
        }

        private static Action<object, object> BuildSetter<TTarget, TValue>(MethodInfo setMethod) where TTarget : class
        {
            var setter = (Action<TTarget, TValue>)Delegate.CreateDelegate(typeof(Action<TTarget, TValue>), setMethod);
            return (target, value) => setter((TTarget)target, (TValue)value);
        }
    }
    public class ReflectedClass
    {
        public ConstructorInfo constructor { get; set; }
        public Type[] constructorParameters { get; set; }
        public object[] constructorParameterNames { get; set; }
        public MethodInfo[] postConstructors { get; set; }

        /// <summary>
        /// 2.3.c：与 postConstructors 一一对应的缓存调用委托（Action&lt;object&gt;）。
        /// AOT 不安全场景（目标为值类型、或方法签名非无参 void）对应槽位为 null，
        /// 由调用方回落 method.Invoke；不使用 Expression.Compile / Reflection.Emit。
        /// </summary>
        public Action<object>[] postConstructorActions { get; set; }
        public ReflectedAttribute[] setters { get; set; }
        public bool preGenerated { get; set; }
        public KeyValuePair<MethodInfo, Attribute>[] attrMethods { get; set; }

        /// <summary>
        /// 2.3.c：为 PostConstruct 无参方法构建 Action&lt;object&gt; 开放实例委托。
        /// 实现：泛型辅助方法 + Delegate.CreateDelegate（MethodInfo 兼容，与构造函数不同）。
        /// 委托的泛型实参取 method.DeclaringType，目标为派生类实例时由闭包内引用转换承接。
        /// </summary>
        internal static Action<object> BuildPostConstructorDelegate(MethodInfo method)
        {
            if (method == null) return null;

            var declaringType = method.DeclaringType;
            if (declaringType == null || declaringType.IsValueType) return null;
            //私有方法同样可委托绑定（CreateDelegate 不做可见性检查），
            //与原反射路径保持一致——MapMethods 扫描 NonPublic 方法
            if (method.ReturnType != typeof(void)) return null;
            if (method.GetParameters().Length != 0) return null;

            var helper = typeof(ReflectedClass).GetMethod(nameof(BuildPostConstructor),
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var closed = helper.MakeGenericMethod(declaringType);
            return (Action<object>)closed.Invoke(null, new object[] { method });
        }

        private static Action<object> BuildPostConstructor<TTarget>(MethodInfo method) where TTarget : class
        {
            var action = (Action<TTarget>)Delegate.CreateDelegate(typeof(Action<TTarget>), method);
            return target => action((TTarget)target);
        }
    }
}