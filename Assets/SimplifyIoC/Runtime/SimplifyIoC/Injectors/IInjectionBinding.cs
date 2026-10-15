/*
 * @interface SimplifyIoC.Injectors.IInjectionBinding
 *
 * 注入系统使用的绑定形式。
 *
 * InjectionBinding 支持三种核心映射类型：
 * - Default（默认）：对 Binder key 的每次 GetInstance() 都返回新实例；
 * - ToSingleton：每次 GetInstance() 返回同一个实例；
 * - ToValue：每次 GetInstance() 返回绑定时提供的实例。
 *
 * 支持命名注入：
 *
 *         Bind<IMyInterface>().To<MyInterfaceImplementer>().ToName(SomeEnum.VALUE);
 *
 * 只有显式标注 SomeEnum.VALUE 的注入点才会拿到该实例，且每次拿到的是同一个。
 *
 * 还可以映射多个 key，实现多态绑定——让两个或更多接口指向同一个类或值：
 *
 *         Bind<IFirstInterface>().Bind<ISecondInterface>().To<PolymorphicClass>();
 *
 * 注意：一个 InjectionBinding 可以绑定多个 key，但只能绑定一个值
 * （注入系统需要知道要创建哪个具体类）。
 *
 * @see SimplifyIoC.Injectors.IInjectionBinder
 */

using System;
using SimplifyIoC.Framework;

namespace SimplifyIoC.Injectors
{
    public enum InjectionBindingType
    {
        /// The binding provides a new instance every time
        Default,

        /// The binding always provides the same instance
        Singleton,

        /// The binding always provides the same instance based on a provided value
        Value,
    }
    public interface IInjectionBinding : IBinding
    {
        /// Map the Binding to a Singleton so that every `GetInstance()` on the Binder Key returns the same imstance.
        IInjectionBinding ToSingleton();

        /// Map the Binding to a stated instance so that every `GetInstance()` on the Binder Key returns the provided imstance. Sets type to Value
        IInjectionBinding ToValue(object o);

        /// Map the Binding to a stated instance so that every `GetInstance()` on the Binder Key returns the provided imstance. Does not set type.
        IInjectionBinding SetValue(object o);

        /// Map the binding and give access to all bootstraps in hierarchy
        IInjectionBinding CrossContext();

        /// Get the list of promised supply types
        object[] GetSupply();

        /// <summary>
        /// 3.3：本绑定是否承诺供给 <paramref name="targetType"/>。
        /// 供给关系的唯一数据源，InjectionBinder.GetSupplier 直接查这里。
        /// </summary>
        bool SuppliesTo(Type targetType);

        /// Promise this Binding to any instance of Type <T>
        IInjectionBinding SupplyTo<T>();

        /// Promise this Binding to any instance of Type type
        IInjectionBinding SupplyTo(Type type);

        /// Remove the promise to supply this binding to Type <T>
        IInjectionBinding Unsupply<T>();

        /// Remove the promise to supply this binding to Type type
        IInjectionBinding Unsupply(Type type);

        bool isCrossContext { get; }

        /// Boolean setter to optionally override injection. If false, the instance will not be injected after instantiation.
        IInjectionBinding ToInject(bool inject);

        /// Get the parameter that specifies whether this Binding allows an instance to be injected
        bool toInject { get; }

        /// Get and set the InjectionBindingType
        /// @see InjectionBindingType
        InjectionBindingType type { get; set; }

        /// Bind is the same as Key, but permits Binder syntax: `Bind<T>().Bind<T>()`
        new IInjectionBinding Bind<T>();

        /// Bind is the same as Key, but permits Binder syntax: `Bind<T>().Bind<T>()`
        new IInjectionBinding Bind(object key);

        new IInjectionBinding To<T>();
        new IInjectionBinding To(object o);
        new IInjectionBinding ToName<T>();
        new IInjectionBinding ToName(object o);
        new IInjectionBinding Named<T>();
        new IInjectionBinding Named(object o);


        new object key { get; }
        new object name { get; }
        new object value { get; }
        new BindingConstraintType keyConstraint { get; set; }
        new BindingConstraintType valueConstraint { get; set; }
    }
}