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

		//5.1：订阅句柄
		//AddListener / AddOnce 返回 SignalSubscription，Dispose 即退订。
		//忘记 RemoveListener 曾经只能靠"记住回调引用"补救，现在有句柄可管：
		SignalSubscription subscription = signalWithNoParameters.AddListener(callbackWithNoParameters);
		...
		subscription.Dispose();                       //退订，幂等，可重复调用
		using (var once = signal.AddListener(cb)) { } //或用 using 限定作用域

		Mediator / View / Command 另有集中回收：subscriptions.Listen(signal, cb)，
		组件销毁时组内订阅自动退订，详见 SignalSubscriptionGroup。
 * 
 * @see SimplifyIoC.Signals.IBaseSignal
 * @see SimplifyIoC.Signals.BaseSignal
 * @see SimplifyIoC.Signals.SignalSubscription
 */

using System;
using System.Collections.Generic;

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

        public SignalSubscription AddListener(Action callback)
        {
            _listener = (Action)CombineUnique(_listener, callback);
            return new SignalSubscription(this, callback, false);
        }

        public SignalSubscription AddOnce(Action callback)
        {
            _onceListener = (Action)CombineUnique(_onceListener, callback);
            return new SignalSubscription(this, callback, true);
        }

        public void RemoveListener(Action callback)
        {
            RemoveCore(callback, false);
            RemoveCore(callback, true);
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

        /// 5.1：强类型通道先判，未命中再落到 BaseSignal 的无参通道。
        internal override bool ContainsCore(Delegate callback, bool once)
        {
            return ContainsImpl(once ? (Delegate)_onceListener : _listener, callback)
                   || base.ContainsCore(callback, once);
        }

        internal override void RemoveCore(Delegate callback, bool once)
        {
            if (once)
                _onceListener = (Action)RemoveImpl(_onceListener, callback);
            else
                _listener = (Action)RemoveImpl(_listener, callback);
            base.RemoveCore(callback, once);
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

        public SignalSubscription AddListener(Action<T> callback)
        {
            _listener = (Action<T>)CombineUnique(_listener, callback);
            return new SignalSubscription(this, callback, false);
        }

        public SignalSubscription AddOnce(Action<T> callback)
        {
            _onceListener = (Action<T>)CombineUnique(_onceListener, callback);
            return new SignalSubscription(this, callback, true);
        }

        public void RemoveListener(Action<T> callback)
        {
            RemoveCore(callback, false);
            RemoveCore(callback, true);
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

        internal override bool ContainsCore(Delegate callback, bool once)
        {
            return ContainsImpl(once ? (Delegate)_onceListener : _listener, callback)
                   || base.ContainsCore(callback, once);
        }

        internal override void RemoveCore(Delegate callback, bool once)
        {
            if (once)
                _onceListener = (Action<T>)RemoveImpl(_onceListener, callback);
            else
                _listener = (Action<T>)RemoveImpl(_listener, callback);
            base.RemoveCore(callback, once);
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

        public SignalSubscription AddListener(Action<T, U> callback)
        {
            _listener = (Action<T, U>)CombineUnique(_listener, callback);
            return new SignalSubscription(this, callback, false);
        }

        public SignalSubscription AddOnce(Action<T, U> callback)
        {
            _onceListener = (Action<T, U>)CombineUnique(_onceListener, callback);
            return new SignalSubscription(this, callback, true);
        }

        public void RemoveListener(Action<T, U> callback)
        {
            RemoveCore(callback, false);
            RemoveCore(callback, true);
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

        internal override bool ContainsCore(Delegate callback, bool once)
        {
            return ContainsImpl(once ? (Delegate)_onceListener : _listener, callback)
                   || base.ContainsCore(callback, once);
        }

        internal override void RemoveCore(Delegate callback, bool once)
        {
            if (once)
                _onceListener = (Action<T, U>)RemoveImpl(_onceListener, callback);
            else
                _listener = (Action<T, U>)RemoveImpl(_listener, callback);
            base.RemoveCore(callback, once);
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

        public SignalSubscription AddListener(Action<T, U, V> callback)
        {
            _listener = (Action<T, U, V>)CombineUnique(_listener, callback);
            return new SignalSubscription(this, callback, false);
        }

        public SignalSubscription AddOnce(Action<T, U, V> callback)
        {
            _onceListener = (Action<T, U, V>)CombineUnique(_onceListener, callback);
            return new SignalSubscription(this, callback, true);
        }

        public void RemoveListener(Action<T, U, V> callback)
        {
            RemoveCore(callback, false);
            RemoveCore(callback, true);
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

        internal override bool ContainsCore(Delegate callback, bool once)
        {
            return ContainsImpl(once ? (Delegate)_onceListener : _listener, callback)
                   || base.ContainsCore(callback, once);
        }

        internal override void RemoveCore(Delegate callback, bool once)
        {
            if (once)
                _onceListener = (Action<T, U, V>)RemoveImpl(_onceListener, callback);
            else
                _listener = (Action<T, U, V>)RemoveImpl(_listener, callback);
            base.RemoveCore(callback, once);
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

        public SignalSubscription AddListener(Action<T, U, V, W> callback)
        {
            _listener = (Action<T, U, V, W>)CombineUnique(_listener, callback);
            return new SignalSubscription(this, callback, false);
        }

        public SignalSubscription AddOnce(Action<T, U, V, W> callback)
        {
            _onceListener = (Action<T, U, V, W>)CombineUnique(_onceListener, callback);
            return new SignalSubscription(this, callback, true);
        }

        public void RemoveListener(Action<T, U, V, W> callback)
        {
            RemoveCore(callback, false);
            RemoveCore(callback, true);
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

        internal override bool ContainsCore(Delegate callback, bool once)
        {
            return ContainsImpl(once ? (Delegate)_onceListener : _listener, callback)
                   || base.ContainsCore(callback, once);
        }

        internal override void RemoveCore(Delegate callback, bool once)
        {
            if (once)
                _onceListener = (Action<T, U, V, W>)RemoveImpl(_onceListener, callback);
            else
                _listener = (Action<T, U, V, W>)RemoveImpl(_listener, callback);
            base.RemoveCore(callback, once);
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
