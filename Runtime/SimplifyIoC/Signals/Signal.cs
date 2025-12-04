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
 * @class SimplifyIoC.Signals.Signal
 * 
 * This is actually a series of classes defining the Base concrete form for all Signals.
 * 
 * Signals are a type-safe approach to communication that essentially replace the
 * standard EventDispatcher model. Signals can be injected/mapped just like any other
 * object -- as Singletons, as instances, or as values. Signals can even be mapped
 * across Contexts to provide an effective and type-safe way of communicating
 * between the parts of your application.
 * 
 * Signals in Strange use the Action Class as the underlying mechanism for type safety.
 * Unity's C# implementation currently allows up to FOUR parameters in an Action, therefore
 * SIGNALS ARE LIMITED TO FOUR PARAMETERS. If you require more than four, consider
 * creating a value object to hold additional values.
 * 
 * Examples:

		//BASIC SIGNAL CREATION/DISPATCH
		//Create a new signal
		Signal signalWithNoParameters = new Signal();
		//Add a listener
		signalWithNoParameters.AddListener(callbackWithNoParameters);
		//This would throw a compile-time error
		signalWithNoParameters.AddListener(callbackWithOneParameter);
		//Dispatch
		signalWithNoParameters.Dispatch();
		//Remove the listener
		signalWithNoParameters.RemoveListener(callbackWithNoParameters);

		//SIGNAL WITH PARAMETERS
		//Create a new signal with two parameters
		Signal<int, string> signal = new Signal<int, string>();
		//Add a listener
		signal.AddListener(callbackWithParamsIntAndString);
		//Add a listener for the duration of precisely one Dispatch
		signal.AddOnce(anotherCallbackWithParamsIntAndString);
		//These all throw compile-time errors
		signal.AddListener(callbackWithParamsStringAndInt);
		signal.AddListener(callbackWithOneParameter);
		signal.AddListener(callbackWithNoParameters);
		//Dispatch
		signal.Dispatch(42, "zaphod");
		//Remove the first listener. The listener added by AddOnce has been automatically removed.
		signal.RemoveListener(callbackWithParamsIntAndString);
 * 
 * @see SimplifyIoC.Signals.IBaseSignal
 * @see SimplifyIoC.Signals.BaseSignal
 */

using System;
using System.Collections.Generic;
using System.Linq;

namespace SimplifyIoC.Signals
{
    public interface ISignal
    {
        Delegate listener { get; set; }
        void RemoveAllListeners();
    }
    /// Base concrete form for a Signal with no parameters
    public class Signal : BaseSignal, ISignal
    {
        private Action _listener;
        private Action _onceListener;

        public void AddListener(Action callback)
        {
            _listener = AddUnique(_listener, callback);
        }

        public void AddOnce(Action callback)
        {
            _onceListener = AddUnique(_onceListener, callback);
        }

        public void RemoveListener(Action callback)
        {
            if (_listener != null)
                _listener -= callback;
        }
        public override List<Type> GetTypes()
        {
            return new List<Type>();
        }
        public void Dispatch()
        {
            _listener?.Invoke();
            _onceListener?.Invoke();
            _onceListener = null;
            base.Dispatch(null);
        }

        private Action AddUnique(Action listeners, Action callback)
        {
            if (listeners == null || !listeners.GetInvocationList().Contains(callback))
            {
                listeners += callback;
            }
            return listeners;
        }

        public override void RemoveAllListeners()
        {
            _listener = null;
            _onceListener = null;
            base.RemoveAllListeners();
        }

        public Delegate listener
        {
            get
            {
                if (_listener == null) _listener = delegate { };
                return _listener;
            }
            set => _listener = (Action)value;
        }
    }

    /// Base concrete form for a Signal with one parameter
    public class Signal<T> : BaseSignal, ISignal
    {
        private Action<T> _listener;
        private Action<T> _onceListener;

        public void AddListener(Action<T> callback)
        {
            _listener = AddUnique(_listener, callback);
        }

        public void AddOnce(Action<T> callback)
        {
            _onceListener = AddUnique(_onceListener, callback);
        }

        public void RemoveListener(Action<T> callback)
        {
            if (_listener != null)
                _listener -= callback;
        }
        public override List<Type> GetTypes()
        {
            return new List<Type> { typeof(T) };
        }
        public void Dispatch(T type1)
        {
            _listener?.Invoke(type1);
            _onceListener?.Invoke(type1);
            _onceListener = null;
            base.Dispatch(new object[]{ type1 });
        }

        private Action<T> AddUnique(Action<T> listeners, Action<T> callback)
        {
            if (listeners == null || !listeners.GetInvocationList().Contains(callback))
            {
                listeners += callback;
            }
            return listeners;
        }

        public override void RemoveAllListeners()
        {
            _listener = null;
            _onceListener = null;
            base.RemoveAllListeners();
        }
        public Delegate listener
        {
            get
            {
                if (_listener == null) _listener = delegate { };
                return _listener;
            }
            set => _listener = (Action<T>)value;
        }
    }

    /// Base concrete form for a Signal with two parameters
    public class Signal<T, U> : BaseSignal, ISignal
    {
        private Action<T, U> _listener;
        private Action<T, U> _onceListener;

        public void AddListener(Action<T, U> callback)
        {
            _listener = AddUnique(_listener, callback);
        }

        public void AddOnce(Action<T, U> callback)
        {
            _onceListener = AddUnique(_onceListener, callback);
        }

        public void RemoveListener(Action<T, U> callback)
        {
            if (_listener != null)
                _listener -= callback;
        }
        public override List<Type> GetTypes()
        {
            return new List<Type>
            {
                typeof(T),
                typeof(U)
            };
        }
        public virtual void Dispatch(T type1, U type2)
        {
            _listener?.Invoke(type1, type2);
            _onceListener?.Invoke(type1, type2);
            _onceListener = null;
            base.Dispatch(new object[] { type1, type2 });
        }
        private Action<T, U> AddUnique(Action<T, U> listeners, Action<T, U> callback)
        {
            if (listeners == null || !listeners.GetInvocationList().Contains(callback))
            {
                listeners += callback;
            }
            return listeners;
        }

        public override void RemoveAllListeners()
        {
            _listener = null;
            _onceListener = null;
            base.RemoveAllListeners();
        }
        public Delegate listener
        {
            get
            {
                if (_listener == null) _listener = delegate { };
                return _listener;
            }
            set => _listener = (Action<T, U>)value;
        }
    }

    /// Base concrete form for a Signal with three parameters
    public class Signal<T, U, V> : BaseSignal, ISignal
    {
        private Action<T, U, V> _listener;
        private Action<T, U, V> _onceListener;

        public void AddListener(Action<T, U, V> callback)
        {
            _listener = AddUnique(_listener, callback);
        }

        public void AddOnce(Action<T, U, V> callback)
        {
            _onceListener = AddUnique(_onceListener, callback);
        }

        public void RemoveListener(Action<T, U, V> callback)
        {
            if (_listener != null)
                _listener -= callback;
        }
        public override List<Type> GetTypes()
        {
            return new List<Type>
            {
                typeof(T),
                typeof(U),
                typeof(V)
            };
        }
        public virtual void Dispatch(T type1, U type2, V type3)
        {
            _listener?.Invoke(type1, type2, type3);
            _onceListener?.Invoke(type1, type2, type3);
            _onceListener = null;
            base.Dispatch(new object[] { type1, type2, type3 });
        }
        private Action<T, U, V> AddUnique(Action<T, U, V> listeners, Action<T, U, V> callback)
        {
            if (listeners == null || !listeners.GetInvocationList().Contains(callback))
            {
                listeners += callback;
            }
            return listeners;
        }
        public override void RemoveAllListeners()
        {
            _listener = null;
            _onceListener = null;
            base.RemoveAllListeners();
        }
        public Delegate listener
        {
            get
            {
                if (_listener == null) _listener = delegate { };
                return _listener;
            }
            set => _listener = (Action<T, U, V>)value;
        }
    }

    /// Base concrete form for a Signal with four parameters
    public class Signal<T, U, V, W> : BaseSignal, ISignal
    {
        private Action<T, U, V, W> _listener;
        private Action<T, U, V, W> _onceListener;

        public void AddListener(Action<T, U, V, W> callback)
        {
            _listener = AddUnique(_listener, callback);
        }

        public void AddOnce(Action<T, U, V, W> callback)
        {
            _onceListener = AddUnique(_onceListener, callback);
        }

        public void RemoveListener(Action<T, U, V, W> callback)
        {
            if (_listener != null)
                _listener -= callback;
        }
        public override List<Type> GetTypes()
        {
            return new List<Type>
            {
                typeof(T),
                typeof(U),
                typeof(V),
                typeof(W)
            };
        }
        public void Dispatch(T type1, U type2, V type3, W type4)
        {
            _listener?.Invoke(type1, type2, type3, type4);
            _onceListener?.Invoke(type1, type2, type3, type4);
            _onceListener = null;
            base.Dispatch(new object[] { type1, type2, type3, type4 });
        }

        private Action<T, U, V, W> AddUnique(Action<T, U, V, W> listeners, Action<T, U, V, W> callback)
        {
            if (listeners == null || !listeners.GetInvocationList().Contains(callback))
            {
                listeners += callback;
            }
            return listeners;
        }
        public override void RemoveAllListeners()
        {
            _listener = null;
            _onceListener = null;
            base.RemoveAllListeners();
        }
        public Delegate listener
        {
            get
            {
                if (_listener == null ) _listener = delegate { };
                return _listener;
            }
            set => _listener = (Action<T, U, V, W>)value;
        }
    }
}