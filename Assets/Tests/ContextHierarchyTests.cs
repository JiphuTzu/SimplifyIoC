// 3.5：Context 父子成链释放（Composite）回归测试。
// 覆盖：
//   1. 第二个 Context 登记为第一个（链根）的子，并共享同一个跨域根 binder；
//   2. 父 Dispose 级联释放子 Context（子的注册表/命令绑定器一并清空）；
//   3. 子 Dispose 从父的链上摘除，且不影响父自身的可用性；
//   4. 链根 Dispose 清空共享的跨域根 binder（CrossContextBridge 单例宿主），
//      否则整条链的跨域绑定与单例会随它一起泄漏；
//   5. 链内跨域单例共享仍然成立（回归护栏）。
using System.Collections.Generic;
using NUnit.Framework;
using SimplifyIoC.Commands;
using SimplifyIoC.Contexts;
using SimplifyIoC.Injectors;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SimplifyIoC.Tests
{
    public class ContextHierarchyTests
    {
        private Context _previousFirstContext;
        private GameObject _parentObject;
        private GameObject _childObject;
        private RootHierarchyContext _parent;
        private ChildHierarchyContext _child;

        [SetUp]
        public void SetUp()
        {
            _previousFirstContext = Context.firstContext;
            Context.firstContext = null;
            TestCommand.ExecutionCount = 0;

            // 第一个 Context 自动成为链根
            _parentObject = new GameObject("ContextHierarchyTests.Parent");
            _parent = new RootHierarchyContext(_parentObject.AddComponent<Bootstrap>());

            // 第二个 Context 因 firstContext 已存在而登记为其子
            _childObject = new GameObject("ContextHierarchyTests.Child");
            _child = new ChildHierarchyContext(_childObject.AddComponent<Bootstrap>());
        }

        [TearDown]
        public void TearDown()
        {
            _child?.Dispose();
            _parent?.Dispose();
            if (_childObject != null) Object.DestroyImmediate(_childObject);
            if (_parentObject != null) Object.DestroyImmediate(_parentObject);
            Context.firstContext = _previousFirstContext;
        }

        [Test]
        public void SecondContextIsRegisteredAsChildOfFirst()
        {
            Assert.That(Context.firstContext, Is.SameAs(_parent));
            Assert.That(_parent.ExposedChildren, Does.Contain(_child));
            Assert.That(_child.ExposedChildren, Is.Empty);

            // 子与父共享同一个跨域根 binder
            Assert.That(_parent.ExposedInjectionBinder.crossContextBinder, Is.Not.Null);
            Assert.That(_child.ExposedInjectionBinder.crossContextBinder,
                Is.SameAs(_parent.ExposedInjectionBinder.crossContextBinder));
        }

        [Test]
        public void ParentDisposeCascadesToChild()
        {
            var service = _child.ExposedInjectionBinder.GetInstance<ITestService>();
            Assert.That(service, Is.Not.Null);

            _parent.Dispose();

            // 子被级联销毁：跨域查询断开、注册表清空、命令绑定器置空
            Assert.That(_child.ExposedInjectionBinder.GetBinding<ITestService>(), Is.Null);
            Assert.That(_child.ExposedCommandBinder, Is.Null);
            Assert.That(_parent.ExposedChildren, Is.Empty);
            Assert.That(Context.firstContext, Is.Null);
        }

        [Test]
        public void ChildDisposeDetachesFromParentButKeepsParentUsable()
        {
            _child.Dispose();

            // 子已从父的链上摘除
            Assert.That(_parent.ExposedChildren, Is.Empty);

            // 父自身不受影响：跨域绑定仍在、信号链路仍可执行
            Assert.That(_parent.ExposedInjectionBinder.GetBinding<ITestService>(), Is.Not.Null);
            _parent.ExposedInjectionBinder.GetInstance<TestSignal>().Dispatch();
            Assert.That(TestCommand.ExecutionCount, Is.GreaterThan(0));
        }

        [Test]
        public void RootDisposeClearsSharedCrossContextBinder()
        {
            var shared = _parent.ExposedInjectionBinder.crossContextBinder;
            Assert.That(shared, Is.Not.Null);
            // CrossContextBridge 单例挂在共享根 binder 上（跨域绑定被 ResolveBinding 转交）
            Assert.That(shared.GetBinding<CrossContextBridge>(), Is.Not.Null);
            // 子的跨域查询也是回落到这个共享 binder
            Assert.That(_child.ExposedInjectionBinder.GetBinding<ITestService>(), Is.Not.Null);

            _parent.Dispose();

            // 共享根 binder 的注册表被清空，且两端都不再持有它
            Assert.That(shared.GetBinding<CrossContextBridge>(), Is.Null);
            Assert.That(_parent.ExposedInjectionBinder.crossContextBinder, Is.Null);
            Assert.That(_child.ExposedInjectionBinder.crossContextBinder, Is.Null);
        }

        [Test]
        public void CrossContextSingletonIsSharedWithinHierarchy()
        {
            var fromParent = _parent.ExposedInjectionBinder.GetInstance<ITestService>();
            var fromChild = _child.ExposedInjectionBinder.GetInstance<ITestService>();

            Assert.That(fromParent, Is.Not.Null);
            Assert.That(fromChild, Is.SameAs(fromParent));
        }

        /// 链根：登记一个跨域单例（会落到共享根 binder 上）
        private sealed class RootHierarchyContext : HierarchyTestContext
        {
            public RootHierarchyContext(Bootstrap view) : base(view) { }

            protected override void MapBindings()
            {
                injectionBinder.Bind<ITestService>().To<TestServiceImpl>().ToSingleton().CrossContext();
                BindLocal();
            }
        }

        /// 子 Context：只做本地绑定——不重复登记跨域类型，
        /// 否则共享根 binder 上会出现同名非 weak 冲突，把根 binder 打进 conflicted 状态。
        private sealed class ChildHierarchyContext : HierarchyTestContext
        {
            public ChildHierarchyContext(Bootstrap view) : base(view) { }

            protected override void MapBindings()
            {
                BindLocal();
            }
        }

        private abstract class HierarchyTestContext : Context
        {
            protected HierarchyTestContext(Bootstrap view) : base(view) { }

            public IReadOnlyList<Context> ExposedChildren => _children;
            public ICrossContextInjectionBinder ExposedInjectionBinder => injectionBinder;
            public ICommandBinder ExposedCommandBinder => commandBinder;

            protected void BindLocal()
            {
                injectionBinder.Bind<TestSignal>().ToSingleton();
                commandBinder.Bind<TestSignal>().To<TestCommand>();
            }
        }
    }
}
