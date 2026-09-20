// 5.1：Signal 订阅句柄（SignalSubscription）+ 集中回收（SignalSubscriptionGroup）的回归测试。
// 钉住的行为：
//  1) AddListener / AddOnce 返回的句柄 Dispose 即退订，且幂等、空句柄惰性；
//  2) AddOnce 挂上的回调此前无法用 RemoveListener 显式摘除，现在两条通道一致；
//  3) Group 管一批订阅的生命周期（Dispose / Untrack）；
//  4) View.OnDestroy 与 Mediator 摘除（DestroyMediator）两条回收链各自自动生效。
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SimplifyIoC.Contexts;
using SimplifyIoC.Injectors;
using SimplifyIoC.Mediations;
using SimplifyIoC.Signals;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SimplifyIoC.Tests
{
    public class SignalSubscriptionTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();

        private int _counter;
        private readonly List<int> _payloads = new List<int>();

        [SetUp]
        public void SetUp()
        {
            _counter = 0;
            _payloads.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _objects.Clear();
        }

        private void Handler() => _counter++;
        private void PayloadHandler(int v) => _payloads.Add(v);

        private T NewComponent<T>() where T : Component
        {
            var go = new GameObject("SignalSubscriptionTests." + typeof(T).Name);
            _objects.Add(go);
            return go.AddComponent<T>();
        }

        // ---- 句柄基本契约 ----

        [Test]
        public void SubscriptionDisposeUnsubscribes()
        {
            var signal = new Signal();
            var subscription = signal.AddListener(Handler);

            signal.Dispatch();
            subscription.Dispose();
            signal.Dispatch();

            Assert.That(_counter, Is.EqualTo(1), "Dispose 之后不得再收到派发");
        }

        [Test]
        public void DisposeIsIdempotent()
        {
            var signal = new Signal();
            var subscription = signal.AddListener(Handler);

            subscription.Dispose();
            Assert.DoesNotThrow(() => subscription.Dispose());
            Assert.That(subscription.IsActive, Is.False);
        }

        [Test]
        public void DefaultSubscriptionIsInert()
        {
            var subscription = default(SignalSubscription);

            Assert.That(subscription.IsValid, Is.False);
            Assert.That(subscription.IsActive, Is.False);
            Assert.DoesNotThrow(() => subscription.Dispose(), "空句柄 Dispose 必须是 no-op");
        }

        [Test]
        public void IsActiveTracksSubscriptionState()
        {
            var signal = new Signal();
            var subscription = signal.AddListener(Handler);
            Assert.That(subscription.IsActive, Is.True);

            subscription.Dispose();
            Assert.That(subscription.IsActive, Is.False);

            var other = signal.AddListener(Handler);
            signal.RemoveAllListeners();
            Assert.That(other.IsActive, Is.False, "被 RemoveAllListeners 清空后同样为 false");
        }

        [Test]
        public void DeduplicationKeepsSingleEntry()
        {
            //旧实现的 AddUnique 契约必须保持一致：同一回调挂两次只执行一次
            var signal = new Signal();
            signal.AddListener(Handler);
            signal.AddListener(Handler);

            signal.Dispatch();

            Assert.That(_counter, Is.EqualTo(1));
        }

        [Test]
        public void TypedSignalSubscriptionUnsubscribes()
        {
            var signal = new Signal<int>();
            var subscription = signal.AddListener(PayloadHandler);

            signal.Dispatch(1);
            subscription.Dispose();
            signal.Dispatch(2);

            Assert.That(_payloads, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void TwoParameterSignalSubscriptionUnsubscribes()
        {
            var signal = new Signal<int, int>();
            var seen = new List<int>();
            var subscription = signal.AddListener((a, b) => seen.Add(a + b));

            signal.Dispatch(1, 2);
            subscription.Dispose();
            signal.Dispatch(3, 4);

            Assert.That(seen, Is.EqualTo(new[] { 3 }));
        }

        [Test]
        public void BaseChannelSubscriptionUnsubscribes()
        {
            var signal = new Signal();
            var received = new List<object[]>();
            var subscription = signal.AddListener((BaseSignal s, object[] args) => received.Add(args));

            signal.Dispatch();
            subscription.Dispose();
            signal.Dispatch();

            Assert.That(received.Count, Is.EqualTo(1), "BaseSignal 通道（弱类型回调）同样受句柄管理");
        }

        // ---- AddOnce 通道 ----

        [Test]
        public void OnceSubscriptionCanBeCancelledBeforeDispatch()
        {
            var signal = new Signal();
            var subscription = signal.AddOnce(Handler);

            subscription.Dispose();
            signal.Dispatch();

            Assert.That(_counter, Is.Zero, "AddOnce 的句柄应能在派发前取消订阅");
        }

        [Test]
        public void OnceSubscriptionBecomesInactiveAfterDispatch()
        {
            var signal = new Signal();
            var subscription = signal.AddOnce(Handler);
            Assert.That(subscription.IsActive, Is.True);

            signal.Dispatch();

            Assert.That(_counter, Is.EqualTo(1));
            Assert.That(subscription.IsActive, Is.False, "一次性订阅派发后自然失效");
        }

        [Test]
        public void RemoveListenerNowAlsoRemovesOnceListener()
        {
            //改前 RemoveListener 只查重复通道：AddOnce 挂上的回调显式 RemoveListener 摘不掉，只能等一次派发。
            var signal = new Signal();
            signal.AddOnce(Handler);

            signal.RemoveListener(Handler);
            signal.Dispatch();

            Assert.That(_counter, Is.Zero, "AddOnce 挂的回调也应能被 RemoveListener 显式摘除");
        }

        // ---- 组 ----

        [Test]
        public void GroupDisposesAllSubscriptions()
        {
            var a = new Signal();
            var b = new Signal<int>();
            var group = new SignalSubscriptionGroup();

            group.Listen(a, Handler);
            group.Listen(b, PayloadHandler);
            Assert.That(group.Count, Is.EqualTo(2));

            group.Dispose();
            a.Dispatch();
            b.Dispatch(1);

            Assert.That(_counter, Is.Zero);
            Assert.That(_payloads, Is.Empty);
            Assert.That(group.Count, Is.Zero, "Dispose 后组应清空（且可重复 Dispose）");
        }

        [Test]
        public void GroupUntrackRemovesAndDisposesOneEntry()
        {
            var signal = new Signal();
            var group = new SignalSubscriptionGroup();

            var subscription = group.Listen(signal, Handler);
            Assert.That(group.Untrack(subscription), Is.True);
            Assert.That(group.Untrack(subscription), Is.False);
            Assert.That(group.Contains(subscription), Is.False);

            signal.Dispatch();
            Assert.That(_counter, Is.Zero, "Untrack 应顺带退订");
        }

        [Test]
        public void DisposingAnEmptyGroupIsSafe()
        {
            Assert.DoesNotThrow(() => new SignalSubscriptionGroup().Dispose());
        }

        // ---- View 回收链 ----

        [Test]
        public void ViewOnDestroyDisposesItsSubscriptionGroup()
        {
            var signal = new Signal();
            var view = NewComponent<SubscriptionProbeView>();
            view.Subscribe(signal);

            signal.Dispatch();
            Assert.That(view.hits, Is.EqualTo(1), "订阅在 View 存活期间生效");

            //编辑模式不派发生命周期消息（工程既有结论），显式派发一次 OnDestroy
            InvokeLifecycle(view, "OnDestroy");

            signal.Dispatch();
            Assert.That(view.hits, Is.EqualTo(1), "View 销毁应自动退订组内订阅");
        }

        // ---- Mediator 回收链 ----

        [Test]
        public void RemovingMediatorDisposesItsSubscriptionGroup()
        {
            var bootstrapObject = NewGameObject("Bootstrap");
            var signal = new Signal();
            var context = new SubscriptionTestContext(bootstrapObject.AddComponent<Bootstrap>());
            //注意时序：Context 的构造函数内部就会跑 MapBindings，实例字段此刻还没赋值
            //（把 signal 传进构造参数的那一版正是死在 ToValue(null) 上），故值绑定放在构造之后。
            context.ExposedInjectionBinder.Bind<Signal>().ToValue(signal).ToInject(false);

            var view = NewComponent<SubscriptionProbeView>();
            context.ExposedMediationBinder.Trigger(MediationEvent.Awake, view);

            var mediator = view.GetComponent<SubscriptionProbeMediator>();
            Assert.That(mediator, Is.Not.Null, "mediation 应挂上 probe mediator");

            signal.Dispatch();
            Assert.That(mediator.hits, Is.EqualTo(1), "mediator 的组内订阅在注册后生效");

            context.ExposedMediationBinder.Trigger(MediationEvent.Destroyed, view);

            signal.Dispatch();
            Assert.That(mediator.hits, Is.EqualTo(1),
                "DestroyMediator 应在 OnRemove 之后统一退订组内订阅");

            context.Dispose();
        }

        // ---- 辅助 ----

        private GameObject NewGameObject(string name)
        {
            var go = new GameObject("SignalSubscriptionTests." + name);
            _objects.Add(go);
            return go;
        }

        private static void InvokeLifecycle(object target, string message)
        {
            var method = target.GetType().GetMethod(message, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"未找到生命周期方法 {message}");
            method.Invoke(target, null);
        }

        // ---- 夹具 ----

        private sealed class SubscriptionTestContext : Context
        {
            public SubscriptionTestContext(Bootstrap view) : base(view) { }

            public IMediationBinder ExposedMediationBinder => mediationBinder;
            public IInjectionBinder ExposedInjectionBinder => injectionBinder;

            protected override void MapBindings()
            {
                //signal 的值绑定由测试在构造后补上：MapBindings 跑在基类构造函数里，
                //晚于它的实例字段赋值都还没发生。
                mediationBinder.Bind<SubscriptionProbeView>().To<SubscriptionProbeMediator>();
            }
        }

        private sealed class SubscriptionProbeView : View
        {
            public SubscriptionProbeView()
            {
                autoRegisterWithContext = false;
                requiresContext = false;
            }

            public int hits { get; private set; }

            /// 走 View 自己的订阅组：与 Mediator 无关，对应 4.7 起"无 Mediator 也能活"的路径
            public void Subscribe(Signal signal)
            {
                subscriptions.Listen(signal, () => hits++);
            }
        }

        private sealed class SubscriptionProbeMediator : Mediator
        {
            [Inject] public Signal signal { get; set; }

            public int hits { get; private set; }

            public override void OnRegister()
            {
                subscriptions.Listen(signal, () => hits++);
            }
        }
    }
}
