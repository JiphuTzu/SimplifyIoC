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
 * @class SimplifyIoC.Signals.SignalSubscriptionGroup
 *
 * 5.1：随组件生命周期自动退订的订阅组。
 *
 * Mediator / View / Command 各自持有一个（惰性创建），由框架在摘除/销毁时 Dispose：
 *
 *      //Mediator 里
 *      public override void OnRegister()
 *      {
 *          subscriptions.Listen(view.OnDead, OnDead);            //带载荷：subscriptions.Listen(sig<int>, cb)
 *          subscriptions.Track(scoreChanged.AddOnce(OnFirstScore)); //AddOnce 用 Track
 *      }
 *      //不用再写 OnRemove { view.OnDead.RemoveListener(OnDead); }
 *
 * 与 SignalSubscription 的分工：句柄管"一条订阅"，组管"一批订阅的生命周期"。
 * 组的 Dispose 只清空引用并逐条 Dispose，不持有任何静态状态——不存在跨场景残留。
 *
 * @see SimplifyIoC.Signals.SignalSubscription
 */

using System;
using System.Collections.Generic;

namespace SimplifyIoC.Signals
{
    public sealed class SignalSubscriptionGroup : IDisposable
    {
        private List<SignalSubscription> _items;

        /// 组内已登记订阅数（未登记过为 0，不触发列表分配）
        public int Count => _items != null ? _items.Count : 0;

        /// <summary>
        /// 登记一条订阅。返回同一句柄以便链式使用；重复 Dispose 由句柄自身保证幂等。
        /// </summary>
        public SignalSubscription Track(SignalSubscription subscription)
        {
            if (_items == null) _items = new List<SignalSubscription>(4);
            _items.Add(subscription);
            return subscription;
        }

        public bool Contains(SignalSubscription subscription)
        {
            return _items != null && _items.Contains(subscription);
        }

        /// <summary>
        /// 提前摘除单条订阅（并退订）。返回是否确实摘掉了一条。
        /// </summary>
        public bool Untrack(SignalSubscription subscription)
        {
            if (_items == null) return false;
            if (!_items.Remove(subscription)) return false;
            subscription.Dispose();
            return true;
        }

        /// <summary>
        /// 退订组内全部订阅并清空。幂等。
        /// </summary>
        public void Dispose()
        {
            if (_items == null) return;
            for (var i = 0; i < _items.Count; i++)
            {
                _items[i].Dispose();
            }
            _items.Clear();
        }
    }

    /// <summary>
    /// 5.1：组上的语法糖。把"订阅"两步合成一句，使集中回收成为默认写法而非额外纪律。
    /// 只有 AddListener 形态；AddOnce 请写 subscriptions.Track(signal.AddOnce(cb))，
    /// 免得 Listen / ListenOnce 两排重载把 API 面翻一倍。
    /// </summary>
    public static class SignalSubscriptionGroupExtensions
    {
        public static SignalSubscription Listen(this SignalSubscriptionGroup group, Signal signal, Action callback)
        {
            return group.Track(signal.AddListener(callback));
        }

        public static SignalSubscription Listen<T>(this SignalSubscriptionGroup group, Signal<T> signal, Action<T> callback)
        {
            return group.Track(signal.AddListener(callback));
        }

        public static SignalSubscription Listen<T, U>(this SignalSubscriptionGroup group, Signal<T, U> signal, Action<T, U> callback)
        {
            return group.Track(signal.AddListener(callback));
        }

        public static SignalSubscription Listen<T, U, V>(this SignalSubscriptionGroup group, Signal<T, U, V> signal, Action<T, U, V> callback)
        {
            return group.Track(signal.AddListener(callback));
        }

        public static SignalSubscription Listen<T, U, V, W>(this SignalSubscriptionGroup group, Signal<T, U, V, W> signal, Action<T, U, V, W> callback)
        {
            return group.Track(signal.AddListener(callback));
        }

        public static SignalSubscription Listen(this SignalSubscriptionGroup group, BaseSignal signal, Action<BaseSignal, object[]> callback)
        {
            return group.Track(signal.AddListener(callback));
        }
    }
}
