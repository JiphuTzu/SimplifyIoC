/*
 * @interface SimplifyIoC.Pools.IPool
 *
 * 存储并复用实例的机制。
 *
 * 与框架其他部分不同，Pool 本身并不是 Binder：它保存应用其他部分创建的
 * 实例以供使用与复用。框架在 CommandBinder 中用 Pool 回收命令实例。
 * 你也可以自己使用 Pool：映射并注入一个 Pool 来复用你需要的实例。
 *
 * 注入 Pool 的基本做法——先在 InjectionBinder 中映射：

		injectionBinder.Bind<IPool<MyClass>>().ToSingleton();

 * 然后注入：

		[Inject]
		public IPool<MyClass> myPool { get; set; }

 *
 * 使用 Pool 的两点注意：
 * 1. 运行时不支持在泛型里使用接口或抽象类——不能映射并注入 IPool<IMyInterface>
 *    或 IPool<MyAbstractClass>。这与日常注入习惯（injectionBinder.Bind<ISomeInterface>）
 *    略有不同，原因出在属性赋值环节而非绑定本身。为免踩坑，映射或设置任何
 *    非具体 Pool 类型时框架会直接抛异常。
 *
 * 2. 使用 Pool 的前提是：实例完成工作后会被清理并归还给 Pool。
 *    用 IPool.ReturnInstance() 把对象标记为可复用；
 *    清理逻辑详见 @see SimplifyIoC.Pools.IPoolable。
 */

using System;
using SimplifyIoC.Framework;

namespace SimplifyIoC.Pools
{
	public enum PoolInflationType
	{
		/// When a dynamic pool inflates, add one to the pool.
		Increment,

		/// When a dynamic pool inflates, double the size of the pool
		Double
	}
	public enum PoolOverflowBehavior
	{
		/// Requesting more than the fixed size will throw an exception.
		Exception,

		/// Requesting more than the fixed size will throw a warning.
		Warning,

		/// Requesting more than the fixed size will return null and not throw an error.
		Ignore
	}
    public interface IPool<T> : IPool
	{
		new T GetInstance ();
	}
	public interface IPool : IManagedList
	{
		/// A class that provides instances to the pool when it needs them.
		/// This can be the InjectionBinder, or any class you write that satisfies the IInstanceProvider
		/// interface.
		IInstanceProvider instanceProvider { get; set; }

		/// The object Type of the first object added to the pool.
		/// Pool objects must be of the same concrete type. This property enforces that requirement. 
		Type poolType { get; set; }

		/// <summary>
		/// Gets an instance from the pool if one is available.
		/// </summary>
		/// <returns>The instance.</returns>
		object GetInstance();

		/// <summary>
		/// Returns an instance to the pool.
		/// </summary>
		/// If the instance being released implements IPoolable, the Release() method will be called.
		/// <param name="value">The instance to be return to the pool.</param>
		void ReturnInstance (object value);

		/// <summary>
		/// Remove all instance references from the Pool.
		/// </summary>
		void Clean ();

		/// <summary>
		/// Returns the count of non-committed instances
		/// </summary>
		int available { get; }

		/// <summary>
		/// Gets or sets the size of the pool.
		/// </summary>
		/// <value>The pool size. '0' is a special value indicating infinite size. Infinite pools expand as necessary to accomodate requirement.</value>
		int size { get; set; }

		/// <summary>
		/// Returns the total number of instances currently managed by this pool.
		/// </summary>
		int instanceCount { get; }

		/// <summary>
		/// Gets or sets the overflow behavior of this pool.
		/// </summary>
		/// <value>A PoolOverflowBehavior value.</value>
		PoolOverflowBehavior overflowBehavior{ get; set; }

		/// <summary>
		/// Gets or sets the type of inflation for infinite-sized pools.
		/// </summary>
		/// By default, a pool doubles its InstanceCount.
		/// <value>A PoolInflationType value.</value>
		PoolInflationType inflationType{ get; set; }
	}
}