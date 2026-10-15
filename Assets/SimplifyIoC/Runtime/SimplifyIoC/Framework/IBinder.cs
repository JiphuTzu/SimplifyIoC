/*
 * @interface SimplifyIoC.Framework.IBinder
 *
 * 用于绑定的集合接口。
 *
 * Binder 是一种集合类（类似 ArrayList 与 Dictionary），
 * 其用途是把那些并不天然相关、但需要在运行时建立关联的东西连接起来。
 * Binder 是本框架的核心概念，其他所有功能都建立在它之上，
 * 更多功能也可以便捷地由它派生。
 *
 * 可以把每个 Binder 看作“原因与结果”或“动作与反应”的集合：
 * key 动作发生时触发 value 动作。例如一个事件可以作为 key，触发某个类的实例化。
 *
 * @see SimplifyIoC.Framework.IBinding
 */

using System.Collections.Generic;

namespace SimplifyIoC.Framework
{
    public interface IBinder
	{
		/// Bind a Binding Key to a class or interface generic
		IBinding Bind<T>();

		/// Bind a Binding Key to a value
		IBinding Bind(object value);

		/// Retrieve a binding based on the provided Type
		IBinding GetBinding<T> ();

		/// Retrieve a binding based on the provided object
		IBinding GetBinding(object key);
		
		/// Retrieve a binding based on the provided Key (generic)/Name combo
		IBinding GetBinding<T>(object name);

		/// Retrieve a binding based on the provided Key/Name combo
		IBinding GetBinding(object key, object name);

		/// Generate an unpopulated IBinding in whatever concrete form the Binder dictates
		IBinding GetRawBinding();

		/// Remove a binding based on the provided Key (generic)
		void Unbind<T>();

		/// Remove a binding based on the provided Key (generic) / Name combo
		void Unbind<T>(object name);

		/// Remove a binding based on the provided Key
		void Unbind (object key);

		/// Remove a binding based on the provided Key / Name combo
		void Unbind (object key, object name);

		/// Remove the provided binding from the Binder
		void Unbind (IBinding binding);

		/// Remove a select value from the given binding
		void RemoveValue (IBinding binding, object value);

		/// Remove a select key from the given binding
		void RemoveKey (IBinding binding, object value);

		/// Remove a select name from the given binding
		void RemoveName (IBinding binding, object value);

		/// The Binder is being removed
		/// Override this method to clean up remaining bindings
		void OnRemove();

		/// <summary>
		/// Places individual Bindings into the bindings Dictionary as part of the resolving process
		/// </summary>
		/// Note that while some Bindings may store multiple keys, each key takes a unique position in the
		/// bindings Dictionary.
		/// 
		/// Conflicts in the course of fluent binding are expected, but GetBinding
		/// will throw an error if there are any unresolved conflicts.
		void ResolveBinding(IBinding binding, object key);
	}
}