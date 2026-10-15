/*
 * @interface SimplifyIoC.Injectors.IInjectionBinder
 *
 * 在框架中实现依赖注入的 Binder。
 *
 * 这里的 key 始终是 Type（类或接口），而不是值；value 则视情况可以是 Type 或具体值。
 * GetInstance() 返回什么，取决于 key 当时的映射方式。
 *
 * 示例：
 *
 * //每次都返回一个新的 SimpleInterfaceImplementer 实例
 * Bind<ISimpleInterface>().To<SimpleInterfaceImplementer>();
 *
 * //返回 SimpleInterfaceImplementer 的单例
 * Bind<ISimpleInterface>().To<SimpleInterfaceImplementer>().ToSingleton();
 *
 * //返回绑定时提供的那个实例
 * Bind<ISimpleInterface>().ToValue(new SimpleInterfaceImplementer());
 *
 * //返回值 42
 * Bind<int>().ToValue(42);
 *
 * //返回按名字区分的 SimpleInterfaceImplementer 实例，只有标注了该名字的注入点才会拿到它；
 * //此后对该名字的任何请求返回的都是同一实例
 * Bind<ISimpleInterface>().To<SimpleInterfaceImplementer>().ToName(SomeEnum.MY_ENUM);
 *
 * //抛出异常：string 未实现 ISimpleInterface
 * Bind<ISimpleInterface>().To<string>();
 *
 * @see SimplifyIoC.Injectors.IInjectionBinding
 */

using System;
using System.Collections.Generic;
using SimplifyIoC.Framework;

namespace SimplifyIoC.Injectors
{
    public interface IInjectionBinder : IInstanceProvider
	{
		/// Get or set an Injector to use. By default, Injector instantiates it's own, but that can be overridden.
		Injector injector{ get; set;}

		/// Retrieve an Instance based on a key/name combo.
		/// ex. `injectionBinder.Get(typeof(ISomeInterface), SomeEnum.MY_ENUM);`
		object GetInstance(Type key, object name, bool ignoreException);

		/// Retrieve an Instance based on a key/name combo.
		/// ex. `injectionBinder.Get<ISomeInterface>(SomeEnum.MY_ENUM);`
		T GetInstance<T>(object name);

		/// Get a binding bound to the injection Type and promised to the target Type
		IInjectionBinding GetSupplier(Type injectionType, Type targetType);

		/// Reflect all the types in the list
		/// Return the number of types in the list, which should be equal to the list length
		int Reflect(List<Type> list);

		/// Reflect all the types currently registered with InjectionBinder
		/// Return the number of types reflected, which should be equal to the number
		/// of concrete classes you've mapped.
		int ReflectAll();

		/// <summary>
		/// Places individual Bindings into the bindings Dictionary as part of the resolving process
		/// </summary>
		/// Note that while some Bindings may store multiple keys, each key takes a unique position in the
		/// bindings Dictionary.
		/// 
		/// Conflicts in the course of fluent binding are expected, but GetBinding
		/// will throw an error if there are any unresolved conflicts.
		void ResolveBinding(IBinding binding, object key);

		/// Remove the supply binding for the specified injection Type T and target Type U.
		void Unsupply<T, U>();

		/// Remove the supply binding for the specified injection Type and target Type.
		void Unsupply(Type injectionType, Type targetType);

		IInjectionBinding Bind<T>();
		IInjectionBinding Bind(Type key);
		IBinding Bind(object key);
		IInjectionBinding GetBinding<T>();
		IInjectionBinding GetBinding<T>(object name);
		IInjectionBinding GetBinding(object key);
		IInjectionBinding GetBinding(object key, object name);
		void Unbind<T>();
		void Unbind<T>(object name);
		void Unbind (object key);
		void Unbind (object key, object name);
		void Unbind (IBinding binding);
	}
}