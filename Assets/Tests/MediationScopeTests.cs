// 3.4.c：Mediation 往返改走调用级作用域的回归测试。
// 钉住的行为：ApplyMediationToView 不再把 view 临时 Bind 进全局容器。
//  1) 容器里已有同 key 绑定时，mediation 不得触发 Binder 冲突、更不得删改用户的绑定；
//  2) 注入期间的重入（mediator 注入点里再创建/再 mediation 一个 View）必须各自拿到自己的 view；
//  3) 正常路径下容器不残留任何 view 绑定。
// 前两条在 3.4.c 之前必然为红：旧实现 Bind(typeToInject).ToValue(view) 与既有同名绑定冲突，
// RegisterNameConflict 之后任何 GetBinding 都抛"conflicted state"异常。
using System;
using System.Collections.Generic;
using NUnit.Framework;
using SimplifyIoC.Contexts;
using SimplifyIoC.Injectors;
using SimplifyIoC.Mediations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SimplifyIoC.Tests
{
    public class MediationScopeTests
    {
        private Context _previousFirstContext;
        private GameObject _bootstrapObject;
        private MediationScopeTestContext _context;
        private readonly List<GameObject> _objects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _previousFirstContext = Context.firstContext;
            Context.firstContext = null;
            MediationScopeProbeMediator.onViewInjected = null;
            MediationScopeProbeMediator.InjectedViews.Clear();
            _bootstrapObject = new GameObject("MediationScopeTests.Bootstrap");
            _objects.Add(_bootstrapObject);
            _context = new MediationScopeTestContext(_bootstrapObject.AddComponent<Bootstrap>());
        }

        [TearDown]
        public void TearDown()
        {
            MediationScopeProbeMediator.onViewInjected = null;
            _context?.Dispose();
            foreach (var go in _objects)
            {
                if (go != null) Object.DestroyImmediate(go);
            }

            _objects.Clear();
            Context.firstContext = _previousFirstContext;
        }

        private IMediationBinder MediationBinder => _context.ExposedMediationBinder;
        private IInjectionBinder InjectionBinder => _context.ExposedInjectionBinder;

        [Test]
        public void MediatorReceivesItsOwnViewAndLeavesNoResidue()
        {
            var view = CreateView();

            MediationBinder.Trigger(MediationEvent.Awake, view);

            var mediator = view.GetComponent<MediationScopeProbeMediator>();
            Assert.That(mediator, Is.Not.Null);
            Assert.That(mediator.view, Is.SameAs(view), "mediator 必须拿到本 View 实例");
            Assert.That(InjectionBinder.GetBinding<MediationScopeProbeView>(), Is.Null,
                "mediation 不应在全局容器里留下 view 绑定");
        }

        [Test]
        public void MediationKeepsExistingBindingForSameKey()
        {
            var kept = CreateView();
            InjectionBinder.Bind<MediationScopeProbeView>().ToValue(kept).ToInject(false);

            var view = CreateView();

            //旧实现：Bind(viewType).ToValue(view) 与上面这条既有绑定同名 → RegisterNameConflict
            //→ Binder 进入 conflicted 状态 → 紧接着的 Inject 解析任何注入点都抛异常
            Assert.DoesNotThrow(() => MediationBinder.Trigger(MediationEvent.Awake, view));

            var binding = InjectionBinder.GetBinding<MediationScopeProbeView>();
            Assert.That(binding, Is.Not.Null, "旧实现收尾的 Unbind 会把用户的绑定整个删掉");
            Assert.That(binding.value, Is.SameAs(kept), "mediation 不应改写用户已有绑定的值");

            //同时，调用级作用域优先于全局绑定——mediator 拿到的是本次的 view 而不是 kept
            Assert.That(view.GetComponent<MediationScopeProbeMediator>().view, Is.SameAs(view));
        }

        [Test]
        public void NestedMediationDuringInjectionIsSupported()
        {
            var nestedView = CreateView();
            MediationScopeProbeMediator.onViewInjected = _ =>
            {
                //外层 mediator 尚未注入完成时又 mediation 一个同类型 View：
                //旧实现此刻外层临时绑定仍在容器里，内层 Bind 同一 key 直接冲突
                MediationBinder.Trigger(MediationEvent.Awake, nestedView);
            };

            var outerView = CreateView();
            Assert.DoesNotThrow(() => MediationBinder.Trigger(MediationEvent.Awake, outerView));

            Assert.That(outerView.GetComponent<MediationScopeProbeMediator>().view, Is.SameAs(outerView));
            Assert.That(nestedView.GetComponent<MediationScopeProbeMediator>().view, Is.SameAs(nestedView));
            Assert.That(MediationScopeProbeMediator.InjectedViews.Count, Is.EqualTo(2),
                "内外两个 mediator 的 view 注入都不应被吞掉");
            Assert.That(InjectionBinder.GetBinding<MediationScopeProbeView>(), Is.Null);
        }

        [Test]
        public void ScopedValueIsVisibleToNestedDependencyConstruction()
        {
            var view = CreateView();

            MediationBinder.Trigger(MediationEvent.Awake, view);

            var mediator = view.GetComponent<MediationScopeProbeMediator>();
            Assert.That(mediator.consumer, Is.Not.Null);
            //旧实现是把 view 临时绑进全局容器，所以 mediator 的依赖（甚至依赖的依赖）在构造期也能拿到 view。
            //3.4.c 改走 Scope 后必须沿构造链下钻，否则这条链路会退化成"no binding"。
            Assert.That(mediator.consumer.view, Is.SameAs(view),
                "view 必须对 mediator 的依赖构造链可见");
        }

        private MediationScopeProbeView CreateView()
        {
            var go = new GameObject("MediationScopeTests.View");
            _objects.Add(go);
            return go.AddComponent<MediationScopeProbeView>();
        }

        private sealed class MediationScopeTestContext : Context
        {
            public MediationScopeTestContext(Bootstrap view) : base(view) { }

            public IMediationBinder ExposedMediationBinder => mediationBinder;
            public IInjectionBinder ExposedInjectionBinder => injectionBinder;

            protected override void MapBindings()
            {
                //transient：每次 mediation 都会为 mediator 造一个新的 consumer
                injectionBinder.Bind<ScopeViewConsumer>().To<ScopeViewConsumer>();
                mediationBinder.Bind<MediationScopeProbeView>().To<MediationScopeProbeMediator>();
            }
        }

        /// 不自动向 Context 注册；测试里显式调用 Trigger，避免依赖 Awake 在 EditMode 下的触发时机
        private sealed class MediationScopeProbeView : View
        {
            public MediationScopeProbeView()
            {
                autoRegisterWithContext = false;
                requiresContext = false;
            }
        }

        /// mediator 的依赖：本身也需要拿到同一个 view
        private sealed class ScopeViewConsumer
        {
            [Inject] public MediationScopeProbeView view { get; set; }
        }

        /// 注入点 setter 带钩子：用于在外层注入尚未结束时触发一次嵌套 mediation
        private sealed class MediationScopeProbeMediator : Mediator
        {
            public static readonly List<MediationScopeProbeView> InjectedViews = new List<MediationScopeProbeView>();
            public static Action<MediationScopeProbeView> onViewInjected;

            [Inject] public ScopeViewConsumer consumer { get; set; }

            private MediationScopeProbeView _view;

            [Inject]
            public MediationScopeProbeView view
            {
                get => _view;
                set
                {
                    _view = value;
                    InjectedViews.Add(value);
                    //先摘钩子再回调，嵌套调用不会再触发，避免无限递归
                    var hook = onViewInjected;
                    onViewInjected = null;
                    hook?.Invoke(value);
                }
            }
        }
    }
}
