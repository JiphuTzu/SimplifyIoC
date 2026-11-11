/*
 * @class SimplifyIoC.Pools.Pool
 *
 * IPool 的默认实现。
 *
 * @see SimplifyIoC.Pools.IPool
 */

using System;
using System.Collections.Generic;
using SimplifyIoC.Framework;
using UnityEngine;

namespace SimplifyIoC.Pools
{
    public class Pool<T> : Pool, IPool<T>
	{
		public Pool()
		{
			poolType = typeof(T);
		}

		/// 想把返回值从 object 收窄成 T 并同时 override 需要"协变返回"，
		/// 而目标运行时（netstandard2.1 / Unity 2022.3）不支持，Roslyn 报 CS8830。
		/// 因此这里用 new 隐藏，<see cref="Pool"/> 是唯一的真实实现，Pool&lt;T&gt; 只当门面。
		public new T GetInstance()
		{
			return (T)base.GetInstance ();
		}
	}

	/// <summary>
	/// 池只承担"容器"职责，不实现 <see cref="IPoolable"/>；元素的清理契约由
	/// <see cref="IPoolable"/> 独占，与容器无关。
	/// </summary>
	public class Pool : IPool
	{

		[Inject]
		public IInstanceProvider instanceProvider { get; set; }

		/// Stack of instances still in the Pool.
		protected readonly Stack<object> _instancesAvailable = new();

		/// A HashSet of the objects checked out of the Pool.
		protected HashSet<object> _instancesInUse = new();

		public Pool ()
		{
			size = 0;
			constraint = BindingConstraintType.Pool;
			uniqueValues = true;
			
			overflowBehavior = PoolOverflowBehavior.Exception;
			inflationType = PoolInflationType.Double;
		}

		#region IManagedList implementation

		public virtual IManagedList Add (object value)
		{
			//消息拼接惰性求值，仅在失败时构造字符串，避免 Add 高频路径的开销。
			FailIf(value.GetType () != poolType, () => "Pool Type mismatch. Pools must consist of a common concrete type.\n\t\tPool type: " + poolType + "\n\t\tMismatch type: " + value.GetType ());
			instanceCount++;
			_instancesAvailable.Push(value);
			return this;
		}

		public virtual IManagedList Add (object[] list)
		{
			foreach (var item in list)
				Add (item);

			return this;
		}

		public virtual IManagedList Remove (object value)
		{
			instanceCount--;
			RemoveInstance (value);
			return this;
		}

		public virtual IManagedList Remove (object[] list)
		{
			foreach (var item in list)
				Remove (item);

			return this;
		}

		public virtual object value 
		{
			get 
			{
				return GetInstance ();
			}
		}
		#endregion

		#region ISemiBinding region
		public virtual bool uniqueValues { get; set;}
		public virtual Enum constraint { get; set; }

		#endregion

		#region IPool implementation

		/// The object Type of the first object added to the pool.
		/// Pool objects must be of the same concrete type. This property enforces that requirement. 
		public Type poolType { get; set; }

		public int instanceCount{get;private set;}

		public virtual object GetInstance ()
		{
			return GetInstance (null);
		}

		/// <summary>
		/// 带调用级作用域的取用。
		/// 池按需惰性创建实例，创建动作发生在本方法内，所以作用域必须一并传下去——
		/// 否则"信号载荷 → 池化 Command"的首次注入会在构造阶段因查不到绑定而报错。
		/// 作用域为 null 时与无参重载完全等价。
		/// </summary>
		public virtual object GetInstance (InjectionScope scope)
		{
			// Is an instance available?
			if (_instancesAvailable.Count > 0)
			{
				var retv = _instancesAvailable.Pop ();
				_instancesInUse.Add (retv);
				return retv;
			}

			var instancesToCreate = 0;

			//New fixed-size pool. Populate.
			if (size > 0)
			{
				if (instanceCount == 0)
				{
					//New pool. Add instances.
					instancesToCreate = size;
				}
				else
				{
					//Illegal overflow. Report and return null
					FailIf (overflowBehavior == PoolOverflowBehavior.Exception,
						"A pool has overflowed its limit.\n\t\tPool type: " + poolType);

					if (overflowBehavior == PoolOverflowBehavior.Warning)
					{
						Debug.Log("WARNING: A pool has overflowed its limit.\n\t\tPool type: " + poolType);
					}
					return null;
				}
			}
			else
			{
				//Zero-sized pools will expand.
				if (instanceCount == 0 || inflationType == PoolInflationType.Increment)
				{
					instancesToCreate = 1;
				}
				else
				{
					instancesToCreate = instanceCount;
				}
			}

			if (instancesToCreate > 0)
			{
				FailIf (instanceProvider == null, "A Pool of type: " + poolType + " has no instance provider.");

				for (var a = 0; a < instancesToCreate; a++)
				{
					var newInstance = instanceProvider.GetInstance(poolType,false, scope);
					Add (newInstance);
				}
				return GetInstance (scope);
			}

			//If not, return null
			return null;
		}

		public virtual void ReturnInstance (object value)
		{
			if (_instancesInUse.Contains (value))
			{
				if (value is IPoolable)
				{
					(value as IPoolable).Restore ();
				}
				_instancesInUse.Remove (value);
				_instancesAvailable.Push (value);
			}
		}

		public virtual void Clean()
		{
			_instancesAvailable.Clear ();
			_instancesInUse = new HashSet<object> ();
			instanceCount = 0;
		}

		public virtual int available
		{
			get
			{
				return _instancesAvailable.Count;
			}
		}

		public virtual int size { get; set; }

		public virtual PoolOverflowBehavior overflowBehavior { get; set; }

		public virtual PoolInflationType inflationType { get; set; }

		#endregion

		/// <summary>
		/// Permanently removes an instance from the Pool
		/// </summary>
		/// In the event that the removed Instance is in use, it is removed from instancesInUse.
		/// Otherwise, it is presumed inactive, and the next available object is popped from
		/// instancesAvailable.
		/// <param name="value">An instance to remove permanently from the Pool.</param>
		protected virtual void RemoveInstance(object value)
		{
			FailIf (value.GetType() != poolType, "Attempt to remove a instance from a pool that is of the wrong Type:\n\t\tPool type: " + poolType + "\n\t\tInstance type: " + value.GetType());
			if (_instancesInUse.Contains(value))
			{
				_instancesInUse.Remove (value);
			}
			else
			{
				_instancesAvailable.Pop ();
			}
		}

		private void FailIf(bool condition, string message)
		{
			if (condition)
			{
				throw new Exception(message);
			}
		}

		/// 接受消息工厂，仅失败时才构造字符串
		private void FailIf(bool condition, Func<string> messageFactory)
		{
			if (condition)
			{
				throw new Exception(messageFactory());
			}
		}
	}
}