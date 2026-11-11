/*
 * @class SimplifyIoC.Framework.Binding
 *
 * 一个 Binding 至少维护两个、可选维护三个 SemiBinding：
 *
 * - key：客户端为解锁一个值而提供的 Type 或值；
 * - value：与 key 绑定、凭 key 释放的一个或多个对象；
 * - name：可选的区分符，允许客户端区分同一 Type 的多个绑定。
 *
 * 解析器（resolver，类型为 Binder.BindingResolver）是传入的回调，用于解开实例化链条。
 *
 * 本框架以 Pool 作为 SemiBinding 的另一种形态，Pool 可回收一组实例；
 * Binding 实现 IPool，作为 Pool SemiBinding 的门面。
 *
 * @see SimplifyIoC.Framework.IBinding
 * @see SimplifyIoC.Pools.IPool
 * @see SimplifyIoC.Framework.Binder
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
        protected Binder.BindingResolver _resolver;

        private readonly ISemiBinding _key;
        protected readonly ISemiBinding _value;
        private readonly ISemiBinding _name;

        public Binding(Binder.BindingResolver resolver)
        {
            this._resolver = resolver;

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

        public bool isWeak {get; private set;}

        // 所有逻辑集中在下面几个 Core 方法中，public 外观与派生类的强类型外观只做调用与转换。
        // 派生类要增加语义（互斥守卫、校验等）只需 override 对应的 Core。

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
            _resolver?.Invoke(this);
            return this;
        }

        /// 登记 name 并通知 resolver。入口：ToName&lt;T&gt;() / ToName(object)
        protected virtual IBinding ToNameCore(object o)
        {
            var toName = o ?? Binder.NULL_BINDING;
            _name.Add(toName);
            _resolver?.Invoke(this);
            return this;
        }

        /// 名字匹配。入口：Named&lt;T&gt;() / Named(object)。不匹配时返回 null。
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
            isWeak = true;
            return this;
        }
        #endregion
    }
}