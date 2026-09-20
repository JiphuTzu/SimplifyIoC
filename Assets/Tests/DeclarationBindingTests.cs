// 4.1：声明式绑定解析（[Child] / [BindEvent] / [MainThread]）的回归测试。
// 钉住三条契约：
//  1) 解析发生在"注入完成之后"——Mediator 侧 [BindEvent(nameof(注入成员))] 可用；
//  2) 解析幂等——重复触发不得重复挂监听（UnityEvent.AddListener 本身不去重）；
//  3) 不会向 Context 注册的 View，由 Awake 兜底路径完成解析。
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SimplifyIoC.Contexts;
using SimplifyIoC.Injectors;
using SimplifyIoC.Mediations;
using SimplifyIoC.Utils;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace SimplifyIoC.Tests
{
    public class DeclarationBindingTests
    {
        private GameObject _bootstrapObject;
        private DeclarativeTestContext _context;
        private readonly List<GameObject> _objects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _bootstrapObject = new GameObject("DeclarationBindingTests.Bootstrap");
            _objects.Add(_bootstrapObject);
            _context = new DeclarativeTestContext(_bootstrapObject.AddComponent<Bootstrap>());
        }

        [TearDown]
        public void TearDown()
        {
            _context?.Dispose();
            foreach (var go in _objects)
            {
                if (go != null) Object.DestroyImmediate(go);
            }

            _objects.Clear();
            MainThreadProbe.Reset();
        }

        private IMediationBinder MediationBinder => _context.ExposedMediationBinder;

        // ---- [Child] ----

        [Test]
        public void ChildFieldsAreAutoMapped()
        {
            var view = CreateView<ChildMappingView>("ChildMappingView");

            MediationBinder.Trigger(MediationEvent.Awake, view);

            Assert.That(view.mappedChild, Is.Not.Null, "[Child(\"child\")] 应自动填上子对象");
            Assert.That(view.mappedChild.name, Is.EqualTo("child"));
            Assert.That(view.MappedList.Count, Is.EqualTo(2), "List<GameObject> 形态应收集到两个孙对象");
            Assert.That(view.MappedArray.Length, Is.EqualTo(2), "数组形态应收集到两个孙对象");
        }

        [Test]
        public void ChildMappingIsIdempotentOnReparse()
        {
            var view = CreateView<ChildMappingView>("ChildMappingView");

            MediationBinder.Trigger(MediationEvent.Awake, view);
            var firstChild = view.mappedChild;
            var firstList = view.MappedList;

            MediationBinder.Trigger(MediationEvent.Awake, view);   // 二次解析

            Assert.That(view.mappedChild, Is.SameAs(firstChild), "已赋值的 [Child] 字段不应被重建");
            Assert.That(view.MappedList, Is.SameAs(firstList), "List 形态已赋值后应被 HasValue 短路");
        }

        // ---- [BindEvent]（含幂等） ----

        [Test]
        public void BindEventIsAutoAttachedExactlyOnce()
        {
            var view = CreateView<EventMappingView>("EventMappingView");

            MediationBinder.Trigger(MediationEvent.Awake, view);
            //二次触发（含 Awake 兜底 + 注入后解析两条路径重叠的情形）不得重复挂载
            MediationBinder.Trigger(MediationEvent.Awake, view);

            view.holder.onClick.Invoke();

            Assert.That(view.clicks, Is.EqualTo(1),
                "[BindEvent] 应自动挂载；UnityEvent.AddListener 不去重，二次解析会让回调执行两次");
        }

        [Test]
        public void EnsureAttributesInitializedIsIdempotent()
        {
            var view = CreateView<EventMappingView>("EventMappingView");

            view.EnsureAttributesInitialized();
            view.EnsureAttributesInitialized();
            view.EnsureAttributesInitialized();

            view.holder.onClick.Invoke();

            Assert.That(view.clicks, Is.EqualTo(1));
        }

        // ---- Mediator 侧：解析时机必须在注入之后 ----

        [Test]
        public void MediatorBindEventCanReferenceInjectedView()
        {
            var view = CreateView<MediatorViewA>("MediatorViewA");

            MediationBinder.Trigger(MediationEvent.Awake, view);

            var mediator = view.GetComponent<InjectedViewMediator>();
            Assert.That(mediator, Is.Not.Null);
            Assert.That(mediator.view, Is.SameAs(view), "非泛型 Mediator 的 [Inject] view 应已注入");

            view.onDead.Invoke();

            Assert.That(mediator.deaths, Is.EqualTo(1),
                "[BindEvent(nameof(view))] 的 view 是 [Inject] 成员，解析必须晚于注入，否则取不到目标");
        }

        // ---- 4.2：Mediator<TView> 强类型视图 ----

        [Test]
        public void GenericMediatorReceivesTypedViewBeforeRegister()
        {
            var view = CreateView<MediatorViewB>("MediatorViewB");

            MediationBinder.Trigger(MediationEvent.Awake, view);

            var mediator = view.GetComponent<GenericViewMediator>();
            Assert.That(mediator, Is.Not.Null);
            Assert.That(mediator.view, Is.SameAs(view), "泛型 Mediator 的 view 应在 InitAttributes/OnRegister 之前赋值");
            Assert.That(mediator.viewAtRegister, Is.SameAs(view), "OnRegister 时 view 必须已就绪");

            view.onDead.Invoke();

            Assert.That(mediator.deaths, Is.EqualTo(1));
        }

        // ---- [MainThread] ----

        [Test]
        public void MainThreadMethodIsAutoRegistered()
        {
            var view = CreateView<MainThreadView>("MainThreadView");

            Assert.DoesNotThrow(() => MediationBinder.Trigger(MediationEvent.Awake, view));

            Assert.That(MainThreadProbe.RecordCount(), Is.EqualTo(1),
                "[MainThread] 标注的无参方法应被自动登记进 MainThreadRunner");
        }

        // ---- 不注册 Context 的 View 走 Awake 兜底 ----

        [Test]
        public void ViewOutsideContextStillParsesOnAwakeFallback()
        {
            var view = CreateView<FallbackView>("FallbackView");

            view.CallAwake();   // EditMode 下 Unity 不会自动调用 Awake，这里显式模拟

            Assert.That(view.mappedChild, Is.Not.Null,
                "!autoRegisterWithContext 的 View 拿不到注入后解析，应由 Awake 兜底完成");
            Assert.That(view.AwakeParseCount, Is.EqualTo(1), "兜底路径同样只解析一次");
        }

        private T CreateView<T>(string name) where T : View
        {
            //先把层级搭好再挂组件：Awake 兜底可能在 AddComponent 时立刻发生
            var root = new GameObject(name);
            _objects.Add(root);
            var child = new GameObject("child");
            child.transform.SetParent(root.transform);
            var gc1 = new GameObject("gc1");
            gc1.transform.SetParent(child.transform);
            var gc2 = new GameObject("gc2");
            gc2.transform.SetParent(child.transform);
            return root.AddComponent<T>();
        }

        // ---- 夹具 ----

        /// [BindEvent] 的 targetName 指向"事件宿主"，eventName 是宿主上的成员名
        private sealed class BindEventHolder
        {
            public UnityEvent onClick = new UnityEvent();
        }

        private sealed class DeclarativeTestContext : Context
        {
            public DeclarativeTestContext(Bootstrap view) : base(view) { }

            public IMediationBinder ExposedMediationBinder => mediationBinder;

            protected override void MapBindings()
            {
                mediationBinder.Bind<MediatorViewA>().To<InjectedViewMediator>();
                mediationBinder.Bind<MediatorViewB>().To<GenericViewMediator>();
            }
        }

        private abstract class ProbeView : View
        {
            protected ProbeView()
            {
                //不向 Context 自动注册：由测试显式 Trigger，避免依赖 Awake 在 EditMode 下的触发时机
                autoRegisterWithContext = false;
                requiresContext = false;
            }
        }

        private sealed class ChildMappingView : ProbeView
        {
            [Child("child")] public GameObject mappedChild;
            [Child("child")] private List<GameObject> _children = new List<GameObject>();
            [Child("child")] private GameObject[] _childArray = Array.Empty<GameObject>();

            public IList<GameObject> MappedList => _children;
            public GameObject[] MappedArray => _childArray;
        }

        private sealed class EventMappingView : ProbeView
        {
            public BindEventHolder holder = new BindEventHolder();
            public int clicks;

            [BindEvent("onClick", nameof(holder))]
            public void OnClick() => clicks++;
        }

        private sealed class MainThreadView : ProbeView
        {
            [MainThread] public void Tick() { }
        }

        private sealed class FallbackView : View
        {
            [Child("child")] public GameObject mappedChild;
            public int AwakeParseCount;

            public FallbackView()
            {
                autoRegisterWithContext = false;
                requiresContext = false;
            }

            public void CallAwake() => Awake();

            protected override void InitAttributes()
            {
                AwakeParseCount++;
                base.InitAttributes();
            }
        }

        private sealed class MediatorViewA : View
        {
            public UnityEvent onDead = new UnityEvent();

            public MediatorViewA()
            {
                autoRegisterWithContext = false;
                requiresContext = false;
            }
        }

        private sealed class MediatorViewB : View
        {
            public UnityEvent onDead = new UnityEvent();

            public MediatorViewB()
            {
                autoRegisterWithContext = false;
                requiresContext = false;
            }
        }

        /// 非泛型写法：[Inject] view + [BindEvent(nameof(view))]
        private sealed class InjectedViewMediator : Mediator
        {
            public int deaths;

            [Inject] public MediatorViewA view { get; set; }

            [BindEvent("onDead", nameof(view))]
            public void OnDead() => deaths++;
        }

        /// 4.2 写法：强类型视图，无需 [Inject]
        private sealed class GenericViewMediator : Mediator<MediatorViewB>
        {
            public int deaths;
            public MediatorViewB viewAtRegister;

            public override void OnRegister()
            {
                base.OnRegister();
                viewAtRegister = view;
            }

            [BindEvent("onDead", nameof(view))]
            public void OnDead() => deaths++;
        }

        /// 反射读取 RunInMainThreadExtension 的私有注册表，用于断言 [MainThread] 确实被登记。
        /// （框架有意不暴露该状态；EditMode 下 MainThreadRunner 的 Update 不会跑，只能看注册表。）
        private static class MainThreadProbe
        {
            private static readonly FieldInfo RunnerField =
                typeof(RunInMainThreadExtension).GetField("_runner", BindingFlags.Static | BindingFlags.NonPublic);

            private static readonly FieldInfo RecordsField;

            static MainThreadProbe()
            {
                var runnerType = typeof(RunInMainThreadExtension).GetNestedType("MainThreadRunner",
                    BindingFlags.NonPublic);
                RecordsField = runnerType?.GetField("_records", BindingFlags.Instance | BindingFlags.NonPublic);
            }

            public static int RecordCount()
            {
                var runner = RunnerField?.GetValue(null);
                if (runner == null) return 0;
                return (RecordsField?.GetValue(runner) as IList)?.Count ?? 0;
            }

            public static void Reset()
            {
                var runner = (Component)RunnerField?.GetValue(null);
                if (runner != null) Object.DestroyImmediate(runner.gameObject);
                RunnerField?.SetValue(null, null);
            }
        }
    }
}
