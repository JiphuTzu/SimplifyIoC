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
 * @class SimplifyIoC.Framework.Binding
 * 
 * A binding maintains at least two — and optionally three — SemiBindings:
 * 
 * <ul>
 * <li>key - The Type or value that a client provides in order to unlock a value.</li>
 * <li>value - One or more things tied to and released by the offering of a key</li>
 * <li>name - An optional discriminator, allowing a client to differentiate between multiple keys of the same Type</li>
 * </ul>
 * 
 * <p>Resolver</p>
 * The resolver method (type Binder.BindingResolver) is a callback passed in to resolve
 * instantiation chains.
 *
 * Strange v0.7 adds Pools as an alternative form of SemiBinding. Pools can recycle groups of instances.
 * Binding implements IPool to act as a facade on any Pool SemiBinding.
 * 
 * @see SimplifyIoC.Framework.IBinding;
 * @see SimplifyIoC.Pools.IPool;
 * @see SimplifyIoC.Framework.Binder;
 */

namespace SimplifyIoC.Framework
{
    public enum BindingConstraintType
    {
        /// Constrains a SemiBinding to carry no more than one item in its Value
        One,
        /// Constrains a SemiBinding to carry a list of items in its Value
        Many,
        /// Instructs the Binding to apply a Pool instead of a SemiBinding
        Pool,
    }
    public class Binding : IBinding
    {
        protected Binder.BindingResolver resolver;

        private readonly ISemiBinding _key;
        protected readonly ISemiBinding _value;
        private readonly ISemiBinding _name;

        public Binding(Binder.BindingResolver resolver)
        {
            this.resolver = resolver;

            _key = new SemiBinding();
            _value = new SemiBinding();
            _name = new SemiBinding();

            keyConstraint = BindingConstraintType.One;
            nameConstraint = BindingConstraintType.One;
            valueConstraint = BindingConstraintType.Many;
        }

        public Binding() : this(null) { }

        #region IBinding implementation
        public object key => _key.value;

        public object value => _value.value;

        public object name => _name.value ?? Binder.NULL_BINDING;

        public BindingConstraintType keyConstraint
        {
            get => _key.constraint;
            set => _key.constraint = value;
        }

        public BindingConstraintType valueConstraint
        {
            get => _value.constraint;
            set => _value.constraint = value;
        }

        public BindingConstraintType nameConstraint
        {
            get => _name.constraint;
            set => _name.constraint = value;
        }

        private bool _isWeak = false;
        public bool isWeak => _isWeak;

        // ------------------------------------------------------------------
        // 5.2：单一收口（single choke point）
        //
        // 背景：本机实测 netstandard2.1（Unity 2022.3 的脚本目标是同一套引用程序集）
        // 下 Roslyn 报 CS8830「目标运行时不支持替代中的协变返回类型」——
        // 派生类无法把返回类型由 IBinding 收窄为 ICommandBinding 的同时 override。
        // 因此 <T> / <object> 两组外观只能继续用 new 隐藏来维持链式类型，改 override 是死路。
        //
        // 既然 new 消不掉，就要让它无害。做法是：**所有逻辑只存在于本区域这几个 Core 方法里**，
        // 上面的 public 外观与派生类的强类型外观都不包含任何逻辑，只做调用与转换。
        // 派生类要加语义（互斥守卫、校验、日志…）只需 override 对应的 Core，
        // 于是"每新增一个入口签名都要记得补一遍"这类遗漏（4.3 的 ToHandler 守卫就是这么被绕过的）
        // 在结构上不可能再发生。
        // ------------------------------------------------------------------

        /// 登记 key。入口：Bind&lt;T&gt;() / Bind(object)
        protected virtual IBinding BindCore(object o)
        {
            _key.Add(o);
            return this;
        }

        /// 登记 value 并通知 resolver。入口：To&lt;T&gt;() / To(object) / SetValue(...)
        protected virtual IBinding ToCore(object o)
        {
            _value.Add(o);
            resolver?.Invoke(this);
            return this;
        }

        /// 登记 name 并通知 resolver。入口：ToName&lt;T&gt;() / ToName(object)
        protected virtual IBinding ToNameCore(object o)
        {
            var toName = o ?? Binder.NULL_BINDING;
            _name.Add(toName);
            resolver?.Invoke(this);
            return this;
        }

        /// 名字匹配。入口：Named&lt;T&gt;() / Named(object)。注意：不匹配时返回 null（既有语义，勿改）
        protected virtual IBinding NamedCore(object o)
        {
            return _name.value == o ? this : null;
        }

        public virtual IBinding Bind<T>()
        {
            return Bind(typeof(T));
        }

        public virtual IBinding Bind(object o)
        {
            return BindCore(o);
        }

        public virtual IBinding To<T>()
        {
            return To(typeof(T));
        }

        public virtual IBinding To(object o)
        {
            return ToCore(o);
        }

        public virtual IBinding ToName<T>()
        {
            return ToName(typeof(T));
        }

        public virtual IBinding ToName(object o)
        {
            return ToNameCore(o);
        }

        public virtual IBinding Named<T>()
        {
            return Named(typeof(T));
        }

        public virtual IBinding Named(object o)
        {
            return NamedCore(o);
        }

        public virtual void RemoveKey(object o)
        {
            _key.Remove(o);
        }

        public virtual void RemoveValue(object o)
        {
            _value.Remove(o);
        }

        public virtual void RemoveName(object o)
        {
            _name.Remove(o);
        }

        public virtual IBinding Weak()
        {
            _isWeak = true;
            return this;
        }
        #endregion
    }
}