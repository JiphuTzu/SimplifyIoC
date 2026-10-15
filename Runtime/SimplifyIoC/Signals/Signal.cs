/*
 * @class SimplifyIoC.Signals.Signal
 *
 * 这里定义的是所有 Signal 的基础具体形态（一系列泛型类）。
 *
 * Signal 是类型安全的通信方式，本质上取代了传统 EventDispatcher 模型。
 * Signal 可以像其他对象一样被注入/映射——作为单例、实例或值，
 * 还可以跨 Context 映射，为应用各部分之间提供类型安全的通信。
 *
 * Signal 用 Action 作为类型安全的底层机制，参数最多四个；
 * 需要更多参数时，考虑用一个值对象把多个值装在一起。
 *
 * 示例：

		//基础信号的创建与派发
		//创建信号
		Signal signalWithNoParameters = new Signal();
		//添加监听
		signalWithNoParameters.AddListener(callbackWithNoParameters);
		//下面这行会引发编译错误
		signalWithNoParameters.AddListener(callbackWithOneParameter);
		//派发
		signalWithNoParameters.Dispatch();
		//移除监听
		signalWithNoParameters.RemoveListener(callbackWithNoParameters);

		//带参数的信号
		//创建带两个参数的信号
		Signal<int, string> signal = new Signal<int, string>();
		//添加监听
		signal.AddListener(callbackWithParamsIntAndString);
		//添加只存活一次派发的监听
		signal.AddOnce(anotherCallbackWithParamsIntAndString);
		//以下都会引发编译错误
		signal.AddListener(callbackWithParamsStringAndInt);
		signal.AddListener(callbackWithOneParameter);
		signal.AddListener(callbackWithNoParameters);
		//派发
		signal.Dispatch(42, "zaphod");
		//移除第一个监听；AddOnce 添加的监听已被自动移除
		signal.RemoveListener(callbackWithParamsIntAndString);

		//5.1：订阅句柄
		//AddListener / AddOnce 返回 SignalSubscription，Dispose 即退订。
		//忘记 RemoveListener 曾经只能靠“记住回调引用”补救，现在有句柄可管：
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
