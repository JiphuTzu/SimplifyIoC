/*
 * @interface SimplifyIoC.Mediations.MediationBinding
 *
 * Binding 的 Mediation 子类。
 *
 * 相对标准 Binding 的扩展：
 *
 * ToMediator —— To<T> 的便捷形式，语义更明确、更安全。
 *
 * ToAbstraction<T> —— 为 View 提供接口或基类适配：绑定指定 ToAbstraction<T> 时，
 * Mediator 将注入 <T> 而不是具体的 View 类。
 */

using System;
using SimplifyIoC.Framework;

namespace SimplifyIoC.Mediations
{
    public class MediationBinding : Binding, IMediationBinding
    {
        private readonly ISemiBinding _abstraction= new SemiBinding();


        public MediationBinding(Binder.BindingResolver resolver) : base(resolver)
        {
            _abstraction.constraint = BindingConstraintType.One;
        }

        IMediationBinding IMediationBinding.ToMediator<T>()
        {
            return base.To(typeof(T)) as IMediationBinding;
        }

        IMediationBinding IMediationBinding.ToAbstraction<T>()
        {
            return ((IMediationBinding)this).ToAbstraction(typeof(T));
        }

        IMediationBinding IMediationBinding.ToAbstraction(Type t)
        {
            var abstractionType = t;
            if (key != null)
            {
                var keyType = key as Type;
                if (abstractionType.IsAssignableFrom(keyType) == false)
                    throw new Exception("The View " + key + " has been bound to the abstraction " + t + " which the View neither extends nor implements. ");
            }
            _abstraction.Add(abstractionType);
            return this;
        }

        public object abstraction => _abstraction.value ?? Binder.NULL_BINDING;

        public new IMediationBinding Bind<T>()
        {
            return base.Bind<T>() as IMediationBinding;
        }

        public new IMediationBinding Bind(object key)
        {
            return base.Bind(key) as IMediationBinding;
        }

        public new IMediationBinding To<T>()
        {
            return base.To<T>() as IMediationBinding;
        }

        public new IMediationBinding To(object o)
        {
            return base.To(o) as IMediationBinding;
        }
    }
}