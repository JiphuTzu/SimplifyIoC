// 3.5：Context 父子成链释放（Composite）回归测试。
// 覆盖：
//   1. 挂在已有 Bootstrap 层级下的 Context 自动成为其子，并共享同一个跨域根 binder；
//      没有 Transform 关系时可用显式 parent 构造或 AddContext 组合（见最后一个用例）；
//   2. 父 Dispose 级联释放子 Context（子的注册表/命令绑定器一并清空）；
//   3. 子 Dispose 从父的链上摘除，且不影响父自身的可用性；
//   4. 链根 Dispose 清空共享的跨域根 binder（CrossContextBridge 单例宿主），
//      否则整条链的跨域绑定与单例会随它一起泄漏；
//   5. 链内跨域单例共享仍然成立（回归护栏）；
//   6. 无关的两个 Context 互不隶属（5.6：静态 firstContext 已摘除）。
//
// 5.6：上述层级归属依赖"祖先 Bootstrap 持有自己的 Context"。这条链接改由框架
// 在 Context 构造期回写（Bootstrap.AttachContext），不再要求用户派生 Bootstrap 手工赋值。
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
        private GameObject _parentObject;
        private GameObject _childObject;
        private RootHierarchyContext _parent;
        private ChildHierarchyContext _child;

        [SetUp]
        public void SetUp()
        {
            TestCommand.ExecutionCount = 0;

            // 第一个 Context 挂在场景根上 → 没有父 Bootstrap → 自任链根
            _parentObject = new GameObject("ContextHierarchyTests.Parent");
            _parent = new RootHierarchyContext(_parentObject.AddComponent<Bootstrap>());

            // 第二个 Context 挂在第一个的 GameObject 之下 → 自动成为其子（5.6 的归属规则）。
            // 该查找要求"祖先 Bootstrap 持有自己的 Context"——由框架在 Context 构造期回写，
            // 这里用的裸 Bootstrap 不需要任何用户代码配合。
            _childObject = new GameObject("ContextHierarchyTests.Child");
            _childObject.transform.SetParent(_parentObject.transform);
            _child = new ChildHierarchyContext(_childObject.AddComponent<Bootstrap>());
        }

        [TearDown]
        public void TearDown()
        {
            _child?.Dispose();
            _parent?.Dispose();
            if (_childObject != null) Object.DestroyImmediate(_childObject);
            if (_parentObject != null) Object.DestroyImmediate(_parentObject);
        }

        [Test]
        public void ContextNestedUnderAnotherBootstrapIsItsChild()
        {
            //5.6：层级归属的依据是"祖先 Bootstrap 持有自己的 Context"。
            //这条反向链接由框架在 Context 构造期回写（Bootstrap.AttachContext）——
            //不依赖用户派生 Bootstrap 手工赋值，也不再有"随便挑一个 Context"的余地。
            Assert.That(_parent.ExposedBootstrap.context, Is.SameAs(_parent));
            Assert.That(_child.ExposedBootstrap.context, Is.SameAs(_child));

            Assert.That(_parent.isRoot, Is.True, "挂在场景根上的 Context 自任链根");
            Assert.That(_child.isRoot, Is.False);
            Assert.That(_child.parentContext, Is.SameAs(_parent));
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

        [Test]
        public void TwoUnrelatedContextsAreIndependentChainRoots()
        {
            //5.6：没有 Transform 关系、也没有显式指定 parent 的两个 Context 互不隶属，
            //各自持有自己的跨域根 binder——跨域单例不再被隐式共享。
            //改前：后构造的那个会被挂到静态 firstContext 下面，两个毫无关系的 Context 被强行耦合。
            var otherObject = new GameObject("ContextHierarchyTests.OtherRoot");
            try
            {
                var other = new RootHierarchyContext(otherObject.AddComponent<Bootstrap>());

                Assert.That(other.isRoot, Is.True);
                Assert.That(other.parentContext, Is.Null);
                Assert.That(_parent.ExposedChildren, Has.No.Member(other));
                Assert.That(other.ExposedInjectionBinder.crossContextBinder,
                    Is.Not.SameAs(_parent.ExposedInjectionBinder.crossContextBinder));

                other.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(otherObject);
            }
        }

        [Test]
        public void ExplicitParentConstructorComposesContextsWithoutTransformRelation()
        {
            //5.6：跨场景 / 跨 Prefab 组合的替代通道——两个 GameObject 没有任何层级关系，
            //但要共享跨域绑定时，用显式 parent 构造（或事后 AddContext）。
            var otherObject = new GameObject("ContextHierarchyTests.ExplicitlyParented");
            try
            {
                var other = new ParentedHierarchyContext(
                    otherObject.AddComponent<Bootstrap>(), _parent);

                Assert.That(other.parentContext, Is.SameAs(_parent));
                Assert.That(other.isRoot, Is.False);
                Assert.That(_parent.ExposedChildren, Does.Contain(other));
                Assert.That(other.ExposedInjectionBinder.crossContextBinder,
                    Is.SameAs(_parent.ExposedInjectionBinder.crossContextBinder));

                //级联释放同样成立
                _parent.Dispose();
                Assert.That(other.ExposedCommandBinder, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(otherObject);
            }
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

        /// 显式挂载到指定父 Context 的子（两侧 GameObject 无层级关系）
        private sealed class ParentedHierarchyContext : HierarchyTestContext
        {
            public ParentedHierarchyContext(Bootstrap view, Context parent) : base(view, parent) { }

            protected override void MapBindings()
            {
                BindLocal();
            }
        }

        private abstract class HierarchyTestContext : Context
        {
            protected HierarchyTestContext(Bootstrap view) : base(view) { }

            protected HierarchyTestContext(Bootstrap view, Context parent) : base(view, parent) { }

            public IReadOnlyList<Context> ExposedChildren => _children;
            public Bootstrap ExposedBootstrap => bootstrap;
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
