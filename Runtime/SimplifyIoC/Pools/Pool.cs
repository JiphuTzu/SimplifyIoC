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
* @class SimplifyIoC.Pools.Pool
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

		/// 5.2 勘误：下面的 `new GetInstance()` 与三套 Binder/Binding 的 new 是同一类问题——
		/// 想把返回值从 object 收窄成 T 并同时 override，需要"协变返回"，
		/// 而本项目的目标运行时（netstandard2.1 / Unity 2022.3）不支持，Roslyn 报 CS8830。
		/// 因此这里保留隐藏，并把 <see cref="Pool"/> 做成唯一的真实实现，Pool&lt;T&gt; 只当门面。
		public new T GetInstance()
		{
			return (T)base.GetInstance ();
		}
	}

	/// <summary>
	/// 5.5（design §6）：拆开"容器"与"元素"两种身份——池不再实现 <see cref="IPoolable"/>。
	/// 旧实现里 Pool : IPool, IPoolable，且 Restore() 直接 Clean() 清空自己：
	/// 把池放进另一个池（"you can employ Pools yourself" 就是这么用的）后，
	/// 归还时的 `if (value is IPoolable) value.Restore()` 会把整个池抹成空——静态 retrieving 丢失。
	/// 元素的清理契约由 <see cref="IPoolable"/> 独占，与容器无关。
	/// </summary>
	public class Pool : IPool
	{

		[Inject]
		public IInstanceProvider instanceProvider { get; set; }

		/// Stack of instances still in the Pool.
		protected Stack<object> instancesAvailable = new Stack<object> ();

		/// A HashSet of the objects checked out of the Pool.
		protected HashSet<object> instancesInUse = new HashSet<object> ();

		public Pool ()
		{
			size = 0;
			constraint = BindingConstraintType.Pool;
			uniqueValues = true;
			
			overflowBehavior = PoolOverflowBehavior.EXCEPTION;
			inflationType = PoolInflationType.DOUBLE;
		}

		#region IManagedList implementation

		public virtual IManagedList Add (object value)
		{
			//2.2 优化：消息拼接改为惰性求值，Add 高频路径不再白构造字符串
			FailIf(value.GetType () != poolType, () => "Pool Type mismatch. Pools must consist of a common concrete type.\n\t\tPool type: " + poolType + "\n\t\tMismatch type: " + value.GetType ());
			instanceCount++;
			instancesAvailable.Push(value);
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
		/// 3.4.a：带调用级作用域的取用。
		/// 池按需惰性创建实例，创建动作发生在本方法内，所以作用域必须一并传下去——
		/// 否则"信号载荷 → 池化 Command"的首次注入会在构造阶段因查不到绑定而报错。
		/// 作用域为 null 时与无参重载完全等价。
		/// </summary>
		public virtual object GetInstance (InjectionScope scope)
		{
			// Is an instance available?
			if (instancesAvailable.Count > 0)
			{
				var retv = instancesAvailable.Pop ();
				instancesInUse.Add (retv);
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
					FailIf (overflowBehavior == PoolOverflowBehavior.EXCEPTION,
						"A pool has overflowed its limit.\n\t\tPool type: " + poolType);

					if (overflowBehavior == PoolOverflowBehavior.WARNING)
					{
						Debug.Log("WARNING: A pool has overflowed its limit.\n\t\tPool type: " + poolType);
					}
					return null;
				}
			}
			else
			{
				//Zero-sized pools will expand.
				if (instanceCount == 0 || inflationType == PoolInflationType.INCREMENT)
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
			if (instancesInUse.Contains (value))
			{
				if (value is IPoolable)
				{
					(value as IPoolable).Restore ();
				}
				instancesInUse.Remove (value);
				instancesAvailable.Push (value);
			}
		}

		public virtual void Clean()
		{
			instancesAvailable.Clear ();
			instancesInUse = new HashSet<object> ();
			instanceCount = 0;
		}

		public virtual int available
		{
			get
			{
				return instancesAvailable.Count;
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
			if (instancesInUse.Contains(value))
			{
				instancesInUse.Remove (value);
			}
			else
			{
				instancesAvailable.Pop ();
			}
		}

		private void FailIf(bool condition, string message)
		{
			if (condition)
			{
				throw new Exception(message);
			}
		}

		/// 2.2 优化：接受消息工厂，仅失败时才构造字符串
		private void FailIf(bool condition, Func<string> messageFactory)
		{
			if (condition)
			{
				throw new Exception(messageFactory());
			}
		}
	}
}