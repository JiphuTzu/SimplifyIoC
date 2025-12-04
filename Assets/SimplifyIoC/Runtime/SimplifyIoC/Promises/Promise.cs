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
* @class SimplifyIoC.Promises.Promise
*
* @see SimplifyIoC.Promises.IPromise
*/

using System;
using System.Linq;

namespace SimplifyIoC.Promises
{
    public class Promise : BasePromise, IPromise
    {
        private Action _listener;

        /// <summary>
        /// Trigger completion callbacks to all listeners.
        /// </summary>
        public void Dispatch()
        {
            if (Fulfill())
                CallListener();
            Finally();
        }

        /// <summary>
        /// Handle a callback when the Promise completes successfully.
        /// </summary>
        /// <param name="action">The callback (no arguments).</param>
        public IPromise Then(Action action)
        {
            if (fulfilled)
            {
                action();
                Finally();
            }
            else if (pending)
            {
                _listener = AddUnique(_listener, action);
            }

            return this;
        }

        public void RemoveListener(Action action)
        {
            if (_listener != null)
                _listener -= action;
        }
        public override void RemoveAllListeners()
        {
            base.RemoveAllListeners();
            _listener = null;
        }

        public override int ListenerCount()
        {
            return _listener == null ? 0 : _listener.GetInvocationList().Length;
        }

        private void CallListener()
        {
            _listener?.Invoke();
        }
    }

    public class Promise<T> : BasePromise, IPromise<T>
    {
        private T _t;

        private Action<T> _listener;

        /// <summary>
        /// Trigger completion callbacks to all listeners
        /// </summary>
        /// <param name="t">First param.</param>
        public void Dispatch(T t)
        {
            if (!Fulfill()) return;
            _t = t;
            CallListener();
            Finally();
        }

        /// <summary>
        /// Handle a callback when the Promise completes successfully.
        /// </summary>
        /// <param name="action">The callback (one argument).</param>
        public IPromise<T> Then(Action<T> action)
        {
            if (fulfilled)
            {
                action(_t);
                Finally();
            }
            else if (pending)
            {
                _listener = AddUnique(_listener, action);
            }
            return this;
        }

        public void RemoveListener(Action<T> action)
        {
            if (_listener != null)
                _listener -= action;
        }
        public override void RemoveAllListeners()
        {
            base.RemoveAllListeners();
            _listener = null;
        }
        public override int ListenerCount()
        {
            return _listener == null ? 0 : _listener.GetInvocationList().Length;
        }
        private void CallListener()
        {
            _listener?.Invoke(_t);
        }

    }

    public class Promise<T, TU> : BasePromise, IPromise<T, TU>
    {
        private T _t;
        private TU _u;

        private Action<T, TU> _listener;

        /// <summary>
        /// Trigger completion callbacks to all listeners
        /// </summary>
        /// <param name="t">First param.</param>
        /// <param name="u">Second param.</param>
        public void Dispatch(T t, TU u)
        {
            if (!Fulfill()) return;
            _t = t;
            _u = u;
            CallListener();
            Finally();
        }

        /// <summary>
        /// Handle a callback when the Promise completes successfully.
        /// </summary>
        /// <param name="action">The callback (two arguments).</param>
        public IPromise<T, TU> Then(Action<T, TU> action)
        {
            if (fulfilled)
            {
                action(_t, _u);
                Finally();
            }
            else if (pending)
            {
                _listener = AddUnique(_listener, action);
            }
            return this;
        }

        public void RemoveListener(Action<T, TU> action)
        {
            if (_listener != null)
                _listener -= action;
        }
        public override void RemoveAllListeners()
        {
            base.RemoveAllListeners();
            _listener = null;
        }
        public override int ListenerCount()
        {
            return _listener == null ? 0 : _listener.GetInvocationList().Length;
        }
        private void CallListener()
        {
            _listener?.Invoke(_t, _u);
        }

        private Action<T, TU> AddUnique(Action<T, TU> listeners, Action<T, TU> callback)
        {
            if (listeners == null || !listeners.GetInvocationList().Contains(callback))
            {
                listeners += callback;
            }
            return listeners;
        }
    }

    public class Promise<T, TU, TV> : BasePromise, IPromise<T, TU, TV>
    {
        private T _t;
        private TU _u;
        private TV _v;

        private Action<T, TU, TV> _listener;

        /// <summary>
        /// Trigger completion callbacks to all listeners
        /// </summary>
        /// <param name="t">First param.</param>
        /// <param name="u">Second param.</param>
        /// <param name="v">Third param.</param>
        public void Dispatch(T t, TU u, TV v)
        {
            if (!Fulfill()) return;
            _t = t;
            _u = u;
            _v = v;
            CallListener();
            Finally();
        }

        /// <summary>
        /// Handle a callback when the Promise completes successfully.
        /// </summary>
        /// <param name="action">The callback (three arguments).</param>
        public IPromise<T, TU, TV> Then(Action<T, TU, TV> action)
        {
            if (fulfilled)
            {
                action(_t, _u, _v);
                Finally();
            }
            else if (pending)
            {
                _listener = AddUnique(_listener, action);
            }
            return this;
        }

        public void RemoveListener(Action<T, TU, TV> action)
        {
            if (_listener != null)
                _listener -= action;
        }
        public override void RemoveAllListeners()
        {
            base.RemoveAllListeners();
            _listener = null;
        }
        public override int ListenerCount()
        {
            return _listener == null ? 0 : _listener.GetInvocationList().Length;
        }
        private void CallListener()
        {
            _listener?.Invoke(_t, _u, _v);
        }

        private Action<T, TU, TV> AddUnique(Action<T, TU, TV> listeners, Action<T, TU, TV> callback)
        {
            if (listeners == null || !listeners.GetInvocationList().Contains(callback))
            {
                listeners += callback;
            }
            return listeners;
        }
    }

    public class Promise<T, TU, TV, TW> : BasePromise, IPromise<T, TU, TV, TW>
    {
        private T _t;
        private TU _u;
        private TV _v;
        private TW _w;

        private Action<T, TU, TV, TW> _listener;

        /// <summary>
        /// Trigger completion callbacks to all listeners
        /// </summary>
        /// <param name="t">First param.</param>
        /// <param name="u">Second param.</param>
        /// <param name="v">Third param.</param>
        /// <param name="w">Fourth param.</param>
        public void Dispatch(T t, TU u, TV v, TW w)
        {
            if (!Fulfill()) return;
            _t = t;
            _u = u;
            _v = v;
            _w = w;
            CallListener();
            Finally();
        }

        /// <summary>
        /// Handle a callback when the Promise completes successfully.
        /// </summary>
        /// <param name="action">The callback (four arguments).</param>
        public IPromise<T, TU, TV, TW> Then(Action<T, TU, TV, TW> action)
        {
            if (fulfilled)
            {
                action(_t, _u, _v, _w);
                Finally();
            }
            else if (pending)
            {
                _listener = AddUnique(_listener, action);
            }
            return this;
        }

        public void RemoveListener(Action<T, TU, TV, TW> action)
        {
            if (_listener != null)
                _listener -= action;
        }
        public override void RemoveAllListeners()
        {
            base.RemoveAllListeners();
            _listener = null;
        }
        public override int ListenerCount()
        {
            return _listener == null ? 0 : _listener.GetInvocationList().Length;
        }
        private void CallListener()
        {
            _listener?.Invoke(_t, _u, _v, _w);
        }

        private Action<T, TU, TV, TW> AddUnique(Action<T, TU, TV, TW> listeners, Action<T, TU, TV, TW> callback)
        {
            if (listeners == null || !listeners.GetInvocationList().Contains(callback))
            {
                listeners += callback;
            }
            return listeners;
        }
    }
}