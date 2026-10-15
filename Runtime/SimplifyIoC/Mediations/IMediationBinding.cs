/*
 * @interface SimplifyIoC.Mediations.IMediationBinding
 *
 * MediationBinding 的接口。
 *
 * 在标准 Binding 之上补充了语义更明确的 View/Mediator 绑定方法。
 */

using System;
using SimplifyIoC.Framework;

namespace SimplifyIoC.Mediations
{
    public interface IMediationBinding : IBinding
    {
        /// Porcelain for To<T> providing a little extra clarity and security.
        IMediationBinding ToMediator<T>();

        /// Provide an Interface or base Class adapter for the View.
        /// When the binding specifies ToAbstraction<T>, the Mediator will be expected to inject <T>
        /// instead of the concrete View class.
        IMediationBinding ToAbstraction<T>();

        IMediationBinding ToAbstraction(Type t);

        /// Retrieve the abstracted value set by ToAbstraction<T>
        object abstraction { get; }

        new IMediationBinding Bind<T>();
        new IMediationBinding Bind(object key);
        new IMediationBinding To<T>();
        new IMediationBinding To(object o);
    }
}