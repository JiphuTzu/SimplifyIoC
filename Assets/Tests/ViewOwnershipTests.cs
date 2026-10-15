// 5.6：View 的 Bootstrap 归属回归测试。
//
// 改前 View 的生命周期事件每次都重新沿 Transform 向上查一遍 Bootstrap。三条问题：
//   1. 归属不确定：Mediate 由 A 做，事件却可能通知到 B（中途 reparent 就会发生）；
//   2. 每次事件都要走 100 层上限的向上查找，纯属浪费；
//   3. 查不到时落到一条"全局兜底"（Context.firstContext —— 谁先构造谁当兜底）。

// 现在：归属在注册成功那一刻写下（Bootstrap.AddView），之后一律发回原 Bootstrap（问题 1、2 已消除）。
// 2026-10-15：问题 3 的那条全局兜底**恢复了**（对外名 Bootstrap.root，见兜底用例）——
// 它是"层级里确实找不到家"时的最后退路，因此兜底只决定注册到谁，不改变上面这条归属规则。
using System.Collections.Generic;
using NUnit.Framework;
using SimplifyIoC.Bootstraps;
using SimplifyIoC.Injectors;
using SimplifyIoC.Mediations;
using SimplifyIoC.Signals;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SimplifyIoC.Tests
{
    public class ViewOwnershipTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            //锚点是进程级静态槽（Bootstrap.root）：先归零，让"第一个构造的链根持有锚点"可预期
            Bootstrap.root = null;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _objects.Clear();

            //锚点静态槽不得留给下一个用例
            Bootstrap.root = null;
        }

        [Test]
        public void ViewRemembersTheBootstrapThatRegisteredIt()
        {
            var recorder = CreateBootstrap("Owner");
            var view = CreateView(recorder.transform);

            view.CallAwake();

            Assert.That(view.registeredWithBootstrap, Is.True);
            Assert.That(view.owningBootstrap, Is.SameAs(recorder), "归属应在注册那一刻记下");
            Assert.That(recorder.AddCalls, Is.EqualTo(1));
        }

        [Test]
        public void LifecycleEventsGoBackToTheOwningBootstrapEvenAfterReparenting()
        {
            var owner = CreateBootstrap("Owner");
            var stranger = CreateBootstrap("Stranger");

            var view = CreateView(owner.transform);
            view.CallAwake();
            Assert.That(view.owningBootstrap, Is.SameAs(owner));

            //搬到一个完全无关、且自带 Bootstrap 的 GameObject 之下
            view.transform.SetParent(stranger.transform);

            view.CallOnEnable();
            view.CallOnDisable();

            Assert.That(owner.EnableCalls, Is.EqualTo(1), "事件必须回到当初做 Mediate 的那个 Bootstrap");
            Assert.That(owner.DisableCalls, Is.EqualTo(1));
            Assert.That(stranger.EnableCalls, Is.EqualTo(0),
                "改前这里会变成 1：每次事件重新向上查找，会把通知错投给新的父 Bootstrap");
            Assert.That(stranger.AddCalls, Is.EqualTo(0));
        }

        [Test]
        public void OnDestroyNotifiesOwningBootstrapAndReleasesOwnership()
        {
            var recorder = CreateBootstrap("Owner");
            var view = CreateView(recorder.transform);
            view.CallAwake();

            view.CallOnDestroy();

            Assert.That(recorder.RemoveCalls, Is.EqualTo(1));
            Assert.That(view.owningBootstrap, Is.Null, "销毁后不得继续持有 Bootstrap 引用");
            Assert.That(view.registeredWithBootstrap, Is.False);

            //销毁之后再触发事件是安全的（没有人可通知）
            Assert.DoesNotThrow(() => view.CallOnEnable());
        }

        [Test]
        public void ViewFallbackToAnchorWhenNoBootstrapIsFound()
        {
            //2026-10-15 恢复的 last-ditch 兜底（5.6 之前叫 Context.firstContext）：
            //层级里找不到任何 Bootstrap 的 View 注册到框架级锚点，而不是在 Start 抛异常。
            var anchor = CreateBootstrap("Unrelated");
            var orphanRoot = NewObject("Orphan");
            var view = orphanRoot.AddComponent<OwnershipView>();

            Assert.DoesNotThrow(() => view.CallAwake(), "Awake 阶段不动声色：Start 还有一次机会");
            Assert.DoesNotThrow(() => view.CallStart(), "Start 阶段回落到锚点");
            Assert.That(view.owningBootstrap, Is.SameAs(anchor), "兜底注册同样写下归属");
            Assert.That(view.registeredWithBootstrap, Is.True);
            Assert.That(anchor.AddCalls, Is.EqualTo(1));
        }

        [Test]
        public void GetReturnsTheSignalInstanceFromTheOwningContainer()
        {
            var recorder = CreateBootstrap("Owner");
            var view = CreateView(recorder.transform);
            view.CallAwake();

            var signal = view.ExposedGet<TestSignal>();

            Assert.That(signal, Is.Not.Null);
            Assert.That(signal, Is.SameAs(recorder.ExposedInjectionBinder.GetInstance<TestSignal>()),
                "Get<T>() 应等价于 injectionBinder.GetInstance<T>()");
        }

        [Test]
        public void GetOnAnOrphanViewReturnsNullInsteadOfThrowing()
        {
            var orphanRoot = NewObject("Orphan");
            var view = orphanRoot.AddComponent<OwnershipView>();
            view.requiresBootstrap = false;
            view.CallAwake();

            Assert.That(view.owningBootstrap, Is.Null);
            Assert.That(view.ExposedGet<TestSignal>(), Is.Null,
                "没有容器的孤儿 View 不做容器往返，返回 null 而不是抛异常");
        }

        // ---- 辅助 ----

        private GameObject NewObject(string name)
        {
            var go = new GameObject("ViewOwnershipTests." + name);
            _objects.Add(go);
            return go;
        }

        private RecordingBootstrap CreateBootstrap(string name)
        {
            var go = NewObject(name);
            //2.0.0：锚点与容器同体——把 Bootstrap 组件挂上去、Init 一次，View 的层级查找就能找到它。
            var bootstrap = go.AddComponent<RecordingBootstrap>();
            bootstrap.Init();
            return bootstrap;
        }

        private OwnershipView CreateView(Transform parent)
        {
            var go = NewObject("View");
            go.transform.SetParent(parent);
            return go.AddComponent<OwnershipView>();
        }

        private sealed class RecordingBootstrap : Bootstrap
        {
            public int AddCalls { get; private set; }
            public int RemoveCalls { get; private set; }
            public int EnableCalls { get; private set; }
            public int DisableCalls { get; private set; }

            public ICrossContextInjectionBinder ExposedInjectionBinder => injectionBinder;

            public override void AddView(View view)
            {
                AddCalls++;
                base.AddView(view);
            }

            public override void RemoveView(View view)
            {
                RemoveCalls++;
                base.RemoveView(view);
            }

            public override void EnableView(View view)
            {
                EnableCalls++;
                base.EnableView(view);
            }

            public override void DisableView(View view)
            {
                DisableCalls++;
                base.DisableView(view);
            }

            protected override void MapBindings()
            {
                injectionBinder.Bind<TestSignal>().ToSingleton();
            }
        }

        /// <summary>把生命周期回调显式暴露出来——EditMode 下引擎不会派发 MonoBehaviour 消息。</summary>
        private sealed class OwnershipView : View
        {
            public void CallAwake() => Awake();
            public void CallStart() => Start();
            public void CallOnEnable() => OnEnable();
            public void CallOnDisable() => OnDisable();
            public void CallOnDestroy() => OnDestroy();

            public T ExposedGet<T>() where T : BaseSignal => GetSignal<T>();
        }
    }
}
