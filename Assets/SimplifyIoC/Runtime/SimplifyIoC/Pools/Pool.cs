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
using System.Collections;
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

		public new T GetInstance()
		{
			return (T)base.GetInstance ();
		}
	}

	public class Pool : IPool, IPoolable
	{

		[Inject]
		public IInstanceProvider instanceProvider { get; set; }

		/// Stack of instances still in the Pool.
		protected Stack instancesAvailable = new Stack ();

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
			FailIf(value.GetType () != poolType, "Pool Type mismatch. Pools must consist of a common concrete type.\n\t\tPool type: " + poolType + "\n\t\tMismatch type: " + value.GetType ());
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
					var newInstance = instanceProvider.GetInstance(poolType,false);
					Add (newInstance);
				}
				return GetInstance ();
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

		#region IPoolable implementation

		public void Restore ()
		{
			Clean ();
			size = 0;
		}

		public void Retain()
		{
			retain = true;
		}

		public void Release()
		{
			retain = false;
		}


		public bool retain{ get; set; }

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
	}
}