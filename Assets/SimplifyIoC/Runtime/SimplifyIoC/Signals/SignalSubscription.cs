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
 * @class SimplifyIoC.Signals.SignalSubscription
 *
 * 5.1：Signal 的订阅句柄。
 *
 * 背景：改前 AddListener / AddOnce 返回 void，退订只能在 OnRemove / OnDestroy 里
 * "照着回调的签名再写一遍 RemoveListener"，漏一行就是泄漏（长期单例 Signal 上尤其致命）。
 * 句柄把"我订阅过什么"这件事变成一个可传递、可 Dispose 的值：
 *
 *      var subscription = scoreChanged.AddListener(OnScoreChanged);
 *      ...
 *      subscription.Dispose();                    //退订，幂等
 *
 * 结构体而非类：一次订阅一次句柄的场景不希望再有堆分配（Signal 是每帧派发的热点路径相邻工序）。
 * default(SignalSubscription) 是惯用的"空句柄"——IsValid == false，Dispose 为 no-op，
 * 因此不必在字段初始化或空引用判断上花力气。
 *
 * 注意：只记录 (Signal, 回调, 通道)，不持有 Signal 的强引用以外的任何东西，
 * 也不会阻止 Signal 被 GC（方向是反的：Signal 持有回调，回调持有你）。
 *
 * @see SimplifyIoC.Signals.SignalSubscriptionGroup
 */

using System;

namespace SimplifyIoC.Signals
{
    public readonly struct SignalSubscription : IDisposable, IEquatable<SignalSubscription>
    {
        private readonly BaseSignal _signal;
        private readonly Delegate _callback;
        private readonly bool _once;

        internal SignalSubscription(BaseSignal signal, Delegate callback, bool once)
        {
            _signal = signal;
            _callback = callback;
            _once = once;
        }

        /// <summary>
        /// 句柄是否指向一条真实订阅。default(SignalSubscription) 为 false。
        /// </summary>
        public bool IsValid => _signal != null && _callback != null;

        /// <summary>
        /// 回调当前是否仍挂在 Signal 上。AddOnce 的句柄在派发之后自然转为 false。
        /// </summary>
        public bool IsActive => IsValid && _signal.ContainsSubscription(_callback, _once);

        /// <summary>
        /// 退订。幂等；重复调用、或对已自然结束的一次性订阅调用均无副作用。
        /// </summary>
        public void Dispose()
        {
            if (!IsValid) return;
            _signal.RemoveSubscription(_callback, _once);
        }

        public bool Equals(SignalSubscription other)
        {
            return ReferenceEquals(_signal, other._signal)
                   && Equals(_callback, other._callback)
                   && _once == other._once;
        }

        public override bool Equals(object obj) => obj is SignalSubscription other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = _signal != null ? _signal.GetHashCode() : 0;
                hash = (hash * 397) ^ (_callback != null ? _callback.GetHashCode() : 0);
                hash = (hash * 397) ^ (_once ? 1 : 0);
                return hash;
            }
        }

        public override string ToString()
        {
            return IsValid
                ? $"SignalSubscription({_signal.GetType().Name}, once={_once})"
                : "SignalSubscription(default)";
        }
    }
}
