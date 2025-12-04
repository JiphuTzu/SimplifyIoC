/*
 * Copyright 2015 StrangeIoC
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
 * @class SimplifyIoC.Promises.BasePromise
 *
 * @see SimplifyIoC.Promises.IPromise
 */

using System;
using System.Linq;

namespace SimplifyIoC.Promises
{
    public abstract class BasePromise : IBasePromise
	{
		private Action<float> _onProgress;
		private Action<Exception> _onFail;
		private Action _onFinally;
		private Exception _exception;

		public PromiseState state { get; private set; }

		public enum PromiseState
		{
			Fulfilled,
			Failed,
			Pending,
		}

		protected BasePromise()
		{
			state = PromiseState.Pending;
		}

		public void ReportFail(Exception ex)
		{
			_exception = ex;
			state = PromiseState.Failed;
			if (_onFail != null)
				_onFail(ex);
			Finally();
		}

		public void ReportProgress(float progress)
		{
			if (_onProgress != null)
				_onProgress(progress);
		}

		/// <summary>
		/// Returns false if the Promise has yet to be resolved. If resolved,
		/// sets the state to Fulfilled and returns true.
		/// </summary>
		protected bool Fulfill()
		{
			if (resolved) return false;

			state = PromiseState.Fulfilled;
			return true;
		}

		public IBasePromise Progress(Action<float> listener)
		{
			_onProgress = AddUnique(_onProgress, listener);
			return this;
		}

		public IBasePromise Fail(Action<Exception> listener)
		{
			if (failed)
			{
				listener(_exception);
				Finally();
			}
			else
				_onFail = AddUnique(_onFail, listener);
			return this;
		}

		public IBasePromise Finally(Action listener)
		{
			if (resolved)
				listener();
			else
				_onFinally = AddUnique(_onFinally, listener);

			return this;
		}

		/// <summary>
		/// Trigger Finally callbacks
		/// </summary>
		protected void Finally()
		{
			if (_onFinally != null)
				_onFinally();
			RemoveAllListeners();
		}

		public void RemoveProgressListeners() { _onProgress = null; }
		public void RemoveFailListeners() { _onFail = null; }
		public virtual void RemoveAllListeners()
		{
			_onProgress = null;
			_onFail = null;
			_onFinally = null;
		}

		/// <summary>
		/// Adds a listener to a callback queue.
		/// </summary>
		/// <returns>The complete list of associated listeners.</returns>
		/// <param name="listeners">Any existing callback queue.</param>
		/// <param name="callback">A callback to add to the queue.</param>
		protected Action AddUnique(Action listeners, Action callback)
		{
			if (listeners == null || !listeners.GetInvocationList().Contains(callback))
			{
				listeners += callback;
			}
			return listeners;
		}

		/// <summary>
		/// Adds a listener to a callback queue, specifying the Action parameter Type of the listener.
		/// </summary>
		/// <returns>The complete list of associated listeners.</returns>
		/// <param name="listeners">Any existing callback queue.</param>
		/// <param name="callback">A callback to add to the queue.</param>
		protected Action<T> AddUnique<T>(Action<T> listeners, Action<T> callback)
		{
			if (listeners == null || !listeners.GetInvocationList().Contains(callback))
			{
				listeners += callback;
			}
			return listeners;
		}

		protected bool pending => state == PromiseState.Pending;
		protected bool resolved => state != PromiseState.Pending;
		protected bool fulfilled => state == PromiseState.Fulfilled;
		protected bool failed => state == PromiseState.Failed;

		public abstract int ListenerCount();
	}
}