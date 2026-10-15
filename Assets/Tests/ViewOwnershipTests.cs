// 5.6：View 的 Context 归属回归测试。
//
// 改前 View 的生命周期事件每次都重新沿 Transform 向上查一遍 Context。三条问题：
//   1. 归属不确定：Mediate 由 A 做，事件却可能通知到 B（中途 reparent 就会发生）；
//   2. 每次事件都要走 100 层上限的向上查找，纯属浪费；
//   3. 查不到时落到一条"全局兜底"（Context.firstContext —— 谁先构造谁当兜底）。

// 现在：归属在注册成功那一刻写下（Context.AddView），之后一律发回原 Context（问题 1、2 已消除）。
// 2026-10-15：问题 3 的那条全局兜底**恢复了**（对外名 Context.rootContext，见兜底用例）——
// 它是"层级里确实找不到家"时的最后退路，因此兜底只决定注册到谁，不改变上面这条归属规则。
using System.Collections.Generic;
using NUnit.Framework;
using SimplifyIoC.Contexts;
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
            //锚点是进程级静态槽（Context.rootContext）：先归零，让"第一个构造的链根持有锚点"可预期
            Context.rootContext = null;
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
            Context.rootContext = null;
        }

        [Test]
        public void ViewRemembersTheContextThatRegisteredIt()
        {
            var recorder = CreateContext("Owner");
            var view = CreateView(recorder.transform);

            view.CallAwake();

            Assert.That(view.registeredWithContext, Is.True);
            Assert.That(view.owningContext, Is.SameAs(recorder), "归属应在注册那一刻记下");
            Assert.That(recorder.AddCalls, Is.EqualTo(1));
        }

        [Test]
        public void LifecycleEventsGoBackToTheOwningContextEvenAfterReparenting()
        {
            var owner = CreateContext("Owner");
            var stranger = CreateContext("Stranger");

            var view = CreateView(owner.transform);
            view.CallAwake();
            Assert.That(view.owningContext, Is.SameAs(owner));

            //搬到一个完全无关、且自带 Context 的 GameObject 之下
            view.transform.SetParent(stranger.transform);

            view.CallOnEnable();
            view.CallOnDisable();

            Assert.That(owner.EnableCalls, Is.EqualTo(1), "事件必须回到当初做 Mediate 的那个 Context");
            Assert.That(owner.DisableCalls, Is.EqualTo(1));
            Assert.That(stranger.EnableCalls, Is.EqualTo(0),
                "改前这里会变成 1：每次事件重新向上查找，会把通知错投给新的父 Context");
            Assert.That(stranger.AddCalls, Is.EqualTo(0));
        }

        [Test]
        public void OnDestroyNotifiesOwningContextAndReleasesOwnership()
        {
            var recorder = CreateContext("Owner");
            var view = CreateView(recorder.transform);
            view.CallAwake();

            view.CallOnDestroy();

            Assert.That(recorder.RemoveCalls, Is.EqualTo(1));
            Assert.That(view.owningContext, Is.Null, "销毁后不得继续持有 Context 引用");
            Assert.That(view.registeredWithContext, Is.False);

            //销毁之后再触发事件是安全的（没有人可通知）
            Assert.DoesNotThrow(() => view.CallOnEnable());
        }

        [Test]
        public void ViewFallbackToAnchorWhenNoBootstrapIsFound()
        {
            //2026-10-15 恢复的 last-ditch 兜底（5.6 之前叫 Context.firstContext）：
            //层级里找不到任何 Bootstrap 的 View 注册到框架级锚点，而不是在 Start 抛异常。
            var anchor = CreateContext("Unrelated");
            var orphanRoot = NewObject("Orphan");
            var view = orphanRoot.AddComponent<OwnershipView>();

            Assert.DoesNotThrow(() => view.CallAwake(), "Awake 阶段不动声色：Start 还有一次机会");
            Assert.DoesNotThrow(() => view.CallStart(), "Start 阶段回落到锚点");
            Assert.That(view.owningContext, Is.SameAs(anchor), "兜底注册同样写下归属");
            Assert.That(view.registeredWithContext, Is.True);
            Assert.That(anchor.AddCalls, Is.EqualTo(1));
        }

        [Test]
        public void GetReturnsTheSignalInstanceFromTheOwningContainer()
        {
            var recorder = CreateContext("Owner");
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
            view.requiresContext = false;
            view.CallAwake();

            Assert.That(view.owningContext, Is.Null);
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

        private RecordingContext CreateContext(string name)
        {
            var go = NewObject(name);
            //5.6：Bootstrap → Context 的归属由框架在 Context 构造期回写（见 Bootstrap.AttachContext），
            //用裸 Bootstrap 就够——View 的层级查找能找到它。
            //（改前这里需要一个派生 Bootstrap 手工把 Context 挂上去，否则查找必然落空。）
            return new RecordingContext(go.AddComponent<Bootstrap>());
        }

        private OwnershipView CreateView(Transform parent)
        {
            var go = NewObject("View");
            go.transform.SetParent(parent);
            return go.AddComponent<OwnershipView>();
        }

        private sealed class RecordingContext : Context
        {
            public RecordingContext(Bootstrap view) : base(view) { }

            public int AddCalls { get; private set; }
            public int RemoveCalls { get; private set; }
            public int EnableCalls { get; private set; }
            public int DisableCalls { get; private set; }

            public Transform transform => bootstrap.transform;
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
