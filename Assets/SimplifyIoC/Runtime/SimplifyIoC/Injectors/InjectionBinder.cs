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
 * @class SimplifyIoC.Injectors.InjectionBinder
 * 
 * The Binder for creating Injection mappings.
 * 
 * @see SimplifyIoC.Injectors.IInjectionBinder
 * @see SimplifyIoC.Injectors.IInjectionBinding
 */

using System;
using System.Collections.Generic;
using SimplifyIoC.Reflectors;
using SimplifyIoC.Framework;

namespace SimplifyIoC.Injectors
{
    public class InjectionBinder : Binder, IInjectionBinder
    {
        private Injector _injector;
        //3.3：原 suppliers 反向索引已删除——供给关系改由 InjectionBinding 自身持有
        //（唯一数据源），本类不再维护第二份注册表。

        public InjectionBinder()
        {
            injector = new Injector();
            injector.binder = this;
            injector.reflector = new ReflectionBinder();
        }

        public object GetInstance(Type key, bool ignoreException)
        {
            return GetInstance(key, null, ignoreException, null);
        }

        /// <summary>
        /// 3.4：带调用级作用域的实例获取（IInstanceProvider 的 scope 重载）。
        /// 池化对象的首次构造会走到这里。
        /// </summary>
        public object GetInstance(Type key, bool ignoreException, InjectionScope scope)
        {
            return GetInstance(key, null, ignoreException, scope);
        }

        public virtual object GetInstance(Type key, object name, bool ignoreException)
        {
            return GetInstance(key, name, ignoreException, null);
        }

        public virtual object GetInstance(Type key, object name, bool ignoreException, InjectionScope scope)
        {
            var binding = GetBinding(key, name);
            if (binding == null)
            {
                if (ignoreException) return null;
                throw new Exception("InjectionBinder has no binding for:\n\tkey: " + key + "\nname: " + name);
            }
            var instance = GetInjectorForBinding(binding).Instantiate(binding, false, scope);
            injector.TryInject(binding, instance, scope);

            return instance;
        }

        protected virtual Injector GetInjectorForBinding(IInjectionBinding binding)
        {
            return injector;
        }

        public T GetInstance<T>()
        {
            var instance = GetInstance(typeof(T),false);
            var retv = (T)instance;
            return retv;
        }

        public T GetInstance<T>(object name)
        {
            var instance = GetInstance(typeof(T), name,false);
            var retv = (T)instance;
            return retv;
        }

        public override IBinding GetRawBinding()
        {
            return new InjectionBinding(Resolver);
        }

        public Injector injector
        {
            get
            {
                return _injector;
            }
            set
            {
                if (_injector != null)
                {
                    _injector.binder = null;
                }
                _injector = value;
                _injector.binder = this;
            }
        }

        public new IInjectionBinding Bind<T>()
        {
            return base.Bind<T>() as IInjectionBinding;
        }

        public IInjectionBinding Bind(Type key)
        {
            return base.Bind(key) as IInjectionBinding;
        }

        #region 5.3 强类型入口

        // 这一组方法全是语法糖：每一条都严格等价于某个既有链式写法，运行时语义（包括 resolver
        // 触发顺序在内）不变——新增的价值只在编译期。
        //
        // 关于名字的一个刻意选择：Enum 名字按装箱枚举值存储，不做 ToString() 归一化。
        // 框架自身的 `[Inject(ContextKeys.Bootstrap)]`、`ToName(SomeEnum.MY_ENUM)` 走的都是这条语义，
        // 若在这里转成字符串，枚举与字符串写法会落进不同的名字槽位，既有代码会静默查不到绑定。
        // 代价是"同一个语义既写成枚举又写成字符串"仍会错——属输入不规范，不是本层能拦住的。

        /// <summary>以字符串给 Type 键的绑定命名，等价于 Bind&lt;T&gt;().ToName(name)。</summary>
        public IInjectionBinding Bind<T>(string name)
        {
            return Bind<T>().ToName(name);
        }

        /// <summary>以枚举给 Type 键的绑定命名，等价于 Bind&lt;T&gt;().ToName(name)（名字按装箱枚举值存储）。</summary>
        public IInjectionBinding Bind<T>(Enum name)
        {
            return Bind<T>().ToName(name);
        }

        /// <summary>
        /// 键值成对入口：Bind&lt;IFoo, Impl&gt;() 等价于 Bind&lt;IFoo&gt;().To&lt;Impl&gt;()。
        /// <c>where TValue : TKey</c> 是关键——把"值类型没实现键类型"从运行期异常提前到编译期报错。
        /// </summary>
        public IInjectionBinding Bind<TKey, TValue>() where TValue : TKey
        {
            return Bind<TKey>().To<TValue>();
        }

        /// <summary>键值成对 + 字符串命名，等价于 Bind&lt;TKey&gt;(name).To&lt;TValue&gt;()。</summary>
        public IInjectionBinding Bind<TKey, TValue>(string name) where TValue : TKey
        {
            return Bind<TKey>(name).To<TValue>();
        }

        /// <summary>键值成对 + 枚举命名，等价于 Bind&lt;TKey&gt;(name).To&lt;TValue&gt;()。</summary>
        public IInjectionBinding Bind<TKey, TValue>(Enum name) where TValue : TKey
        {
            return Bind<TKey>(name).To<TValue>();
        }

        #endregion

        public new virtual IInjectionBinding GetBinding<T>()
        {
            return base.GetBinding<T>() as IInjectionBinding;
        }

        public new virtual IInjectionBinding GetBinding<T>(object name)
        {
            return base.GetBinding<T>(name) as IInjectionBinding;
        }

        public new virtual IInjectionBinding GetBinding(object key)
        {
            return base.GetBinding(key) as IInjectionBinding;
        }

        public new virtual IInjectionBinding GetBinding(object key, object name)
        {
            return base.GetBinding(key, name) as IInjectionBinding;
        }

        public int ReflectAll()
        {
            var list = new List<Type>();
            foreach (var pair in bindings)
            {
                var dict = pair.Value;
                foreach (var bPair in dict)
                {
                    var binding = bPair.Value;
                    var t = (binding.value is Type) ? (Type)binding.value : binding.value.GetType();
                    if (list.IndexOf(t) == -1)
                    {
                        list.Add(t);
                    }
                }
            }
            return Reflect(list);
        }

        public int Reflect(List<Type> list)
        {
            var count = 0;
            foreach (var t in list)
            {
                //Reflector won't permit primitive types, so screen them
                if (t.IsPrimitive || t == typeof(Decimal) || t == typeof(string))
                {
                    continue;
                }
                count++;
                injector.reflector.Get(t);
            }
            return count;
        }

        /// <summary>
        /// 3.3：改为查绑定自身的供给集合（原为 suppliers 反向索引的直接读取）。
        /// 行为对齐：仍不区分 name——原 suppliers 注册表在登记时就丢掉了 name 维度，
        /// 这里同样取该 key 下第一个承诺供给 targetType 的绑定。
        /// </summary>
        public virtual IInjectionBinding GetSupplier(Type injectionType, Type targetType)
        {
            if (injectionType == null || targetType == null) return null;
            return FindSupplied(bindings.TryGetValue(injectionType, out var dict) ? dict : null, targetType);
        }

        private static IInjectionBinding FindSupplied(Dictionary<object, IBinding> dict, Type targetType)
        {
            if (dict == null) return null;
            foreach (var pair in dict)
            {
                if (pair.Value is IInjectionBinding injectionBinding && injectionBinding.SuppliesTo(targetType))
                {
                    return injectionBinding;
                }
            }
            return null;
        }

        /// <summary>
        /// 3.3：供给关系随绑定走，Unsupply 只需改绑定自身；
        /// 绑定被 Unbind 后 GetSupplier 自然查不到（不再需要手工同步第二份注册表）。
        /// </summary>
        public void Unsupply(Type injectionType, Type targetType)
        {
            var binding = GetSupplier(injectionType, targetType);
            binding?.Unsupply(targetType);
        }

        public void Unsupply<T, U>()
        {
            Unsupply(typeof(T), typeof(U));
        }
    }
}
