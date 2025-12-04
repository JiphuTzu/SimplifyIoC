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
 * @class SimplifyIoC.Signals.BaseSignal
 * 
 * The base class for all Signals.
 * 
 * @see SimplifyIoC.Signals.IBaseSignal
 * @see SimplifyIoC.Signals.Signal
 */

using System;
using System.Collections.Generic;
using System.Linq;

namespace SimplifyIoC.Signals
{
    public class BaseSignal
    {

        /// The delegate for repeating listeners
        private event Action<BaseSignal, object[]> _baseListener = null;

        /// The delegate for one-off listeners
        private event Action<BaseSignal, object[]> _onceBaseListener = null;

        /// <summary>
        /// Sends a Dispatch to all listeners with the provided arguments
        /// </summary>
        /// <param name="args">A list of values which must be implemented by listening methods.</param>
        public void Dispatch(object[] args)
        {
            _baseListener?.Invoke(this, args);
            _onceBaseListener?.Invoke(this, args);
            _onceBaseListener = null;
        }

        public virtual List<Type> GetTypes() { return new List<Type>(); }

        /// <summary>
        /// Adds a listener.
        /// </summary>
        /// <param name="callback">The method to be called when Dispatch fires.</param>
        public void AddListener(Action<BaseSignal, object[]> callback)
        {
            _baseListener = AddUnique(_baseListener, callback);
        }

        /// <summary>
        /// Adds a listener which will be removed immediately after the Signal fires.
        /// </summary>
        /// <param name="callback">The method to be called when Dispatch fires.</param>
        public void AddOnce(Action<BaseSignal, object[]> callback)
        {
            _onceBaseListener = AddUnique(_onceBaseListener, callback);
        }

        private Action<T, U> AddUnique<T, U>(Action<T, U> listeners, Action<T, U> callback)
        {
            if (listeners == null || !listeners.GetInvocationList().Contains(callback))
            {
                listeners += callback;
            }
            return listeners;
        }

        /// <summary>
        /// Removes the listener.
        /// </summary>
        /// <param name="callback">The callback to be removed.</param>
        public void RemoveListener(Action<BaseSignal, object[]> callback)
        {
            if (_baseListener != null)
                _baseListener -= callback;
        }

        /// <summary>
        /// Removes all listeners currently attached to the Signal.
        /// </summary>
        public virtual void RemoveAllListeners()
        {
            _baseListener = null;
            _onceBaseListener = null;
        }
    }
}