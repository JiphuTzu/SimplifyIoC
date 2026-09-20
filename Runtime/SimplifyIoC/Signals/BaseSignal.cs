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

namespace SimplifyIoC.Signals
{
    public class BaseSignal
    {

        /// The delegate for repeating listeners
        private Action<BaseSignal, object[]> _baseListener;

        /// The delegate for one-off listeners
        private Action<BaseSignal, object[]> _onceBaseListener;

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
        /// <returns>5.1：订阅句柄，Dispose 即退订。旧写法忽略返回值照常编译。</returns>
        public SignalSubscription AddListener(Action<BaseSignal, object[]> callback)
        {
            _baseListener = (Action<BaseSignal, object[]>)CombineUnique(_baseListener, callback);
            return new SignalSubscription(this, callback, false);
        }

        /// <summary>
        /// Adds a listener which will be removed immediately after the Signal fires.
        /// </summary>
        /// <param name="callback">The method to be called when Dispatch fires.</param>
        /// <returns>5.1：订阅句柄，Dispose 可在派发前取消这条一次性订阅。</returns>
        public SignalSubscription AddOnce(Action<BaseSignal, object[]> callback)
        {
            _onceBaseListener = (Action<BaseSignal, object[]>)CombineUnique(_onceBaseListener, callback);
            return new SignalSubscription(this, callback, true);
        }

        /// <summary>
        /// Removes the listener.
        /// </summary>
        /// <param name="callback">The callback to be removed.</param>
        /// <remarks>
        /// 5.1：重复通道与一次性通道都查。改前 AddOnce 挂上的回调无法显式摘除
        /// （RemoveListener 只查 _baseListener），两条通道自此一致。
        /// </remarks>
        public void RemoveListener(Action<BaseSignal, object[]> callback)
        {
            RemoveCore(callback, false);
            RemoveCore(callback, true);
        }

        /// <summary>
        /// Removes all listeners currently attached to the Signal.
        /// </summary>
        public virtual void RemoveAllListeners()
        {
            _baseListener = null;
            _onceBaseListener = null;
        }

        #region 5.1：订阅句柄内部通道

        /// internal：回调是否仍在链上。子类先查自己的强类型通道，未命中再回落到此处。
        internal virtual bool ContainsCore(Delegate callback, bool once)
        {
            return ContainsImpl(once ? (Delegate)_onceBaseListener : _baseListener, callback);
        }

        /// internal：退订实现。null / 类型不匹配都原样返回，不抛异常。
        internal virtual void RemoveCore(Delegate callback, bool once)
        {
            if (once)
                _onceBaseListener = (Action<BaseSignal, object[]>)RemoveImpl(_onceBaseListener, callback);
            else
                _baseListener = (Action<BaseSignal, object[]>)RemoveImpl(_baseListener, callback);
        }

        internal bool ContainsSubscription(Delegate callback, bool once) => ContainsCore(callback, once);
        internal void RemoveSubscription(Delegate callback, bool once) => RemoveCore(callback, once);

        #endregion

        #region 委托链工具（5.1：收敛原先散落在 5 个 Signal 里的 AddUnique 实现）

        /// 去重判定：引用相等优先，否则展开比对。Delegate.Equals 比的是 Target+Method，
        /// 与旧实现的 GetInvocationList().Contains(callback) 语义等价。
        internal static bool ContainsImpl(Delegate source, Delegate callback)
        {
            if (source == null || callback == null) return false;
            if (ReferenceEquals(source, callback)) return true;
            if (source.GetType() != callback.GetType()) return false;
            var list = source.GetInvocationList();
            for (var i = 0; i < list.Length; i++)
            {
                if (list[i].Equals(callback)) return true;
            }
            return false;
        }

        /// 摘除：类型不同、或压根没挂在链上时原样返回（Delegate.Remove 的既有语义）。
        internal static Delegate RemoveImpl(Delegate source, Delegate callback)
        {
            if (source == null || callback == null) return source;
            if (source.GetType() != callback.GetType()) return source;
            return Delegate.Remove(source, callback);
        }

        /// 去重追加：保持旧 AddUnique 的"同一回调只挂一次"契约。
        internal static Delegate CombineUnique(Delegate source, Delegate callback)
        {
            if (callback == null) return source;
            if (ContainsImpl(source, callback)) return source;
            return Delegate.Combine(source, callback);
        }

        #endregion
    }
}