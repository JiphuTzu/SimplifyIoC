// 4.7：没有 Mediator 时 View 依然可用。
//
// 背景：4.1 把 View 的声明式解析时机挪到了"注入完成之后"，触发点在 MediationBinder。
// 若"触发点只存在于 Mediator 机制里"，那么不使用 Mediator 的项目就永远解析不到
// [Child] / [BindEvent] / [BindMethod]。本文件钉住解耦后的两条契约：
//
//  1) View 没有绑定任何 Mediator（Context 有 mediationBinder）—— 走 Trigger 的
//     "无绑定"分支，注入与声明式解析照常完成，生命周期事件不抛；
//  2) Context 完全没有 mediationBinder（不使用 Mediator 机制）—— 走 Context 的
//     兜底路径（InjectViewWithoutMediation），View 同样被注入并完成解析。
using System.Collections.Generic;
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
    public class ViewWithoutMediatorTests
    {
        private Context _previousFirstContext;
        private GameObject _bootstrapObject;
        private PlainTestContext _context;
        private readonly List<GameObject> _objects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _previousFirstContext = Context.firstContext;
            Context.firstContext = null;
            _bootstrapObject = new GameObject("ViewWithoutMediatorTests.Bootstrap");
            _objects.Add(_bootstrapObject);
            _context = new PlainTestContext(_bootstrapObject.AddComponent<Bootstrap>());
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
            Context.firstContext = _previousFirstContext;
            MainThreadProbe.Reset();
        }

        // ---- 1) 没有 Mediator 绑定 ----

        [Test]
        public void ViewWithoutMediatorIsInjectedAndParsed()
        {
            var view = CreateAutoView();

            _context.AddView(view);

            Assert.That(view.service, Is.Not.Null,
                "没有 Mediator 的 View 也必须被注入——注入是 Context 的能力，不是 Mediator 的");
            Assert.That(view.mappedChild, Is.Not.Null, "[Child] 必须生效");
            Assert.That(view.mappedChild.name, Is.EqualTo("child"));

            view.holder.onClick.Invoke();
            Assert.That(view.clicks, Is.EqualTo(1), "[BindEvent] 必须生效");

            view.InvokeBind("hit", 7);
            Assert.That(view.hits, Is.EqualTo(new[] { 7 }), "[BindMethod] 必须生效");
        }

        [Test]
        public void LifecycleEventsAreSafeWithoutMediator()
        {
            var view = CreateAutoView();
            _context.AddView(view);

            //没有 mediator 时这些事件无事可做，但绝不能抛
            Assert.DoesNotThrow(() => _context.EnableView(view));
            Assert.DoesNotThrow(() => _context.DisableView(view));
            Assert.DoesNotThrow(() => _context.RemoveView(view));
        }

        [Test]
        public void ChildViewWithoutMediatorIsAlsoHandled()
        {
            var root = CreateAutoView();

            //子视图与父视图同属"没有 Mediator"的情形，应各自完成注入与解析
            _context.AddView(root);

            var sub = root.GetComponentInChildren<AutoSubView>();
            Assert.That(sub, Is.Not.Null);
            Assert.That(sub.service, Is.Not.Null, "子视图也必须被注入");
            Assert.That(sub.mappedChild, Is.Not.Null, "子视图的 [Child] 也必须生效");
            Assert.That(sub.mappedChild.name, Is.EqualTo("subchild"));
        }

        // ---- 2) 整个 Context 没有 Mediator 机制 ----

        [Test]
        public void ContextWithoutMediationBinderStillDrivesViews()
        {
            var noMediatorObject = new GameObject("ViewWithoutMediatorTests.NoMediatorBootstrap");
            _objects.Add(noMediatorObject);
            var context = new NoMediatorContext(noMediatorObject.AddComponent<Bootstrap>());
            try
            {
                Assert.That(context.ExposedMediationBinder, Is.Null,
                    "本 Context 刻意不绑定 IMediationBinder，且不得在装配期抛异常");

                var view = CreateAutoView();
                context.AddView(view);

                Assert.That(view.service, Is.Not.Null, "没有 mediationBinder 时 View 仍须被注入");
                Assert.That(view.mappedChild, Is.Not.Null, "没有 mediationBinder 时 [Child] 仍须生效");

                view.holder.onClick.Invoke();
                Assert.That(view.clicks, Is.EqualTo(1), "没有 mediationBinder 时 [BindEvent] 仍须生效");

                //生命周期事件同样不得抛
                Assert.DoesNotThrow(() => context.EnableView(view));
                Assert.DoesNotThrow(() => context.DisableView(view));
                Assert.DoesNotThrow(() => context.RemoveView(view));
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void EarlyViewIsHandledWhenContextHasNoMediationBinder()
        {
            //早到的 View：Context 尚未就绪（mediationBinder 还没实例化）时先缓存，
            //PostBindings 阶段即使没有 mediationBinder 也要把它补上，而不是抛异常。
            var noMediatorObject = new GameObject("ViewWithoutMediatorTests.EarlyBootstrap");
            _objects.Add(noMediatorObject);

            var stagedView = CreateAutoView();
            var stagedContext = new StagedNoMediatorContext(noMediatorObject.AddComponent<Bootstrap>());
            try
            {
                //手动构造的 Context 会立刻 Start，这里直接验证 PostBindings 的兜底：
                //把 View 交给尚未就绪的缓存路径，再由 MediateViewCache 补处理。
                stagedContext.CacheViewForTest(stagedView);
                stagedContext.MediateViewCacheForTest();

                Assert.That(stagedView.service, Is.Not.Null);
                Assert.That(stagedView.mappedChild, Is.Not.Null);
            }
            finally
            {
                stagedContext.Dispose();
            }
        }

        // ---- 3) 完全没有 Context（既没有 Mediator 也没有容器）----

        [Test]
        public void OrphanViewWithoutContextParsesOnStartNotAwake()
        {
            //requiresContext=false 的 View：允许在 Context 之外存在。
            //它拿不到注入，但声明式绑定不该因此失效——由 Start 兜底解析。
            Context.firstContext = null;

            var go = new GameObject("OrphanView");
            _objects.Add(go);
            var child = new GameObject("child");
            child.transform.SetParent(go.transform);
            var view = go.AddComponent<OrphanView>();

            view.CallAwake();
            Assert.That(view.mappedChild, Is.Null,
                "Awake 不得抢先解析：解析要等到确定拿不到注入之后（4.1 的时机契约）");

            view.CallStart();
            Assert.That(view.mappedChild, Is.Not.Null,
                "没有 Context / Mediator 的 View，Start 兜底也要把声明式绑定解析出来");

            view.InvokeBind("hit", 5);
            Assert.That(view.hits, Is.EqualTo(new[] { 5 }));
        }

        // ---- 辅助 ----

        /// <summary>默认配置的 View（autoRegister / requiresContext 均为 true）+ 子视图层级</summary>
        private AutoView CreateAutoView()
        {
            var root = new GameObject("AutoView");
            _objects.Add(root);

            var child = new GameObject("child");
            child.transform.SetParent(root.transform);

            var sub = new GameObject("sub");
            sub.transform.SetParent(root.transform);
            sub.AddComponent<AutoSubView>();

            var subChild = new GameObject("subchild");
            subChild.transform.SetParent(sub.transform);

            return root.AddComponent<AutoView>();
        }

        private sealed class PlainTestContext : Context
        {
            public PlainTestContext(Bootstrap view) : base(view) { }

            public IMediationBinder ExposedMediationBinder => mediationBinder;

            protected override void MapBindings()
            {
                //刻意不绑定任何 View→Mediator
                injectionBinder.Bind<ProbeService>().ToSingleton();
            }
        }

        private class NoMediatorContext : Context
        {
            public NoMediatorContext(Bootstrap view) : base(view) { }

            public IMediationBinder ExposedMediationBinder => mediationBinder;

            protected override void AddCoreComponents()
            {
                base.AddCoreComponents();
                //不使用 Mediator 机制：摘掉 IMediationBinder 绑定。
                //4.7 之前，这会让 InstantiateCoreComponents 抛"no binding"；
                //现在 mediationBinder 允许为 null，由 View 的兜底路径接管。
                injectionBinder.Unbind<IMediationBinder>();
            }

            protected override void MapBindings()
            {
                injectionBinder.Bind<ProbeService>().ToSingleton();
            }
        }

        private sealed class StagedNoMediatorContext : NoMediatorContext
        {
            public StagedNoMediatorContext(Bootstrap view) : base(view) { }

            public void CacheViewForTest(View view) => CacheView(view);

            public void MediateViewCacheForTest() => MediateViewCache();
        }
        private sealed class ProbeService { }

        private sealed class EventHolder
        {
            public UnityEvent onClick = new UnityEvent();
        }

        private sealed class AutoView : View
        {
            public EventHolder holder = new EventHolder();
            public int clicks;
            public readonly List<int> hits = new List<int>();

            [Inject] public ProbeService service { get; set; }
            [Child("child")] public GameObject mappedChild;

            [BindEvent("onClick", nameof(holder))]
            public void OnClick() => clicks++;

            [BindMethod("hit")]
            private void OnHit(int n) => hits.Add(n);
        }

        private sealed class AutoSubView : View
        {
            [Inject] public ProbeService service { get; set; }
            [Child("subchild")] public GameObject mappedChild;
        }

        /// 允许存在于 Context 之外的 View：requiresContext=false，但仍会在 Start 兜底解析
        private sealed class OrphanView : View
        {
            public readonly List<int> hits = new List<int>();

            [Child("child")] public GameObject mappedChild;

            public OrphanView()
            {
                requiresContext = false;
            }

            public void CallAwake() => Awake();

            public void CallStart() => Start();

            [BindMethod("hit")]
            private void OnHit(int n) => hits.Add(n);
        }

        /// 反射读取 MainThreadRunner 的私有注册表，仅用于 TearDown 清理常驻对象。
        private static class MainThreadProbe
        {
            private static readonly System.Reflection.FieldInfo RunnerField =
                typeof(RunInMainThreadExtension).GetField("_runner",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

            public static void Reset()
            {
                var runner = (Component)RunnerField?.GetValue(null);
                if (runner != null) Object.DestroyImmediate(runner.gameObject);
                RunnerField?.SetValue(null, null);
            }
        }
    }
}
