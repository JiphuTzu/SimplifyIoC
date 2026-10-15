// 3.5：Bootstrap 父子成链释放（Composite）回归测试。
// 覆盖：
//   1. 挂在已有 Bootstrap 层级下的 Bootstrap 自动成为其子，并共享同一个跨域根 binder；
//      没有 Transform 关系时可用 Init(parent) 显式认父或 AddBootstrap 组合（见最后一个用例）；
//   2. 父 Dispose 级联释放子 Bootstrap（子的注册表/命令绑定器一并清空）；
//   3. 子 Dispose 从父的链上摘除，且不影响父自身的可用性；
//   4. 链根 Dispose 清空共享的跨域根 binder（CrossContextBridge 单例宿主），
//      否则整条链的跨域绑定与单例会随它一起泄漏；
//   5. 链内跨域单例共享仍然成立（回归护栏）；
//   6. 无关的两个 Bootstrap 仍会成链——没有显式 parent 也没 Transform 关系时，
//      后构造的那个挂到框架级锚点（Bootstrap.root）之下，见最后一个用例。

// 5.6：上述层级归属依赖"祖先 Bootstrap 已经就绪"。改前这条链接靠框架回写 Bootstrap.bootstrap 维持；
// 2026-10-15 合并（2.0.0）后锚点与容器同体，这条链接不可能再失效。
using System.Collections.Generic;
using NUnit.Framework;
using SimplifyIoC.Commands;
using SimplifyIoC.Bootstraps;
using SimplifyIoC.Framework;
using SimplifyIoC.Injectors;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SimplifyIoC.Tests
{
    public class BootstrapHierarchyTests
    {
        private GameObject _parentObject;
        private GameObject _childObject;
        private RootHierarchyBootstrap _parent;
        private ChildHierarchyBootstrap _child;

        [SetUp]
        public void SetUp()
        {
            //锚点是进程级静态槽：先归零，下面第一个构造的 _parent 才会成为本类用例的锚点
            Bootstrap.root = null;
            TestCommand.ExecutionCount = 0;

            // 第一个 Bootstrap 挂在场景根上 → 没有祖先 Bootstrap → 自任链根
            _parentObject = new GameObject("BootstrapHierarchyTests.Parent");
            _parent = _parentObject.AddComponent<RootHierarchyBootstrap>();
            _parent.Init();

            // 第二个 Bootstrap 挂在第一个的 GameObject 之下 → 自动成为其子（5.6 的归属规则）。
            // 该查找要求"祖先 Bootstrap 已经就绪"，所以父必须先 Init（EditMode 下由测试显式驱动）。
            _childObject = new GameObject("BootstrapHierarchyTests.Child");
            _childObject.transform.SetParent(_parentObject.transform);
            _child = _childObject.AddComponent<ChildHierarchyBootstrap>();
            _child.Init();
        }

        [TearDown]
        public void TearDown()
        {
            _child?.Dispose();
            _parent?.Dispose();
            if (_childObject != null) Object.DestroyImmediate(_childObject);
            if (_parentObject != null) Object.DestroyImmediate(_parentObject);
            Bootstrap.root = null;
        }

        [Test]
        public void BootstrapNestedUnderAnotherBootstrapIsItsChild()
        {
            //5.6：层级归属的依据是"祖先 Bootstrap 已经就绪"——2.0.0 合并后锚点与容器同体，
            //不存在"忘了把 Bootstrap 挂到 Bootstrap 上"这种失效，也没有"随便挑一个 Bootstrap"的余地。
            Assert.That(_parent.isRoot, Is.True, "挂在场景根上的 Bootstrap 自任链根");
            Assert.That(_child.isRoot, Is.False);
            Assert.That(_child.parentBootstrap, Is.SameAs(_parent));
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
        public void UnrelatedBootstrapAutomaticallyAttachesToTheAnchor()
        {
            //2026-10-15 恢复：没有 Transform 关系、也没有显式 parent 的 Bootstrap 会挂到
            //框架级锚点下（本用例里锚点就是 SetUp 中第一个构造的 _parent），共享同一个跨域根 binder。
            //这里用 ChildHierarchyBootstrap（只做本地绑定）——先前的独立根写法会重复登记跨域类型，
            //挂到共享根 binder 之后就会撞名冲突（见下方 ChildHierarchyBootstrap 的说明）。
            var otherObject = new GameObject("BootstrapHierarchyTests.OtherRoot");
            try
            {
                var other = otherObject.AddComponent<ChildHierarchyBootstrap>();
                other.Init();

                Assert.That(other.isRoot, Is.False);
                Assert.That(other.parentBootstrap, Is.SameAs(_parent));
                Assert.That(_parent.ExposedChildren, Does.Contain(other));
                Assert.That(other.ExposedInjectionBinder.crossContextBinder,
                    Is.SameAs(_parent.ExposedInjectionBinder.crossContextBinder));

                other.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(otherObject);
            }
        }

        [Test]
        public void ExplicitParentInitComposesBootstrapsWithoutTransformRelation()
        {
            //5.6：跨场景 / 跨 Prefab 组合的替代通道——两个 GameObject 没有任何层级关系，
            //但要共享跨域绑定时，用 Init(parent) 显式认父（或事后 AddBootstrap）。
            var otherObject = new GameObject("BootstrapHierarchyTests.ExplicitlyParented");
            try
            {
                var other = otherObject.AddComponent<ParentedHierarchyBootstrap>();
                other.Init(_parent);

                Assert.That(other.parentBootstrap, Is.SameAs(_parent));
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
        private sealed class RootHierarchyBootstrap : HierarchyTestBootstrap
        {
            protected override void MapBindings()
            {
                injectionBinder.Bind<ITestService>().To<TestServiceImpl>().ToSingleton().CrossContext();
                BindLocal();
            }
        }

        /// 子 Bootstrap：只做本地绑定——不重复登记跨域类型，
        /// 否则共享根 binder 上会出现同名非 weak 冲突，把根 binder 打进 conflicted 状态。
        private sealed class ChildHierarchyBootstrap : HierarchyTestBootstrap
        {
            protected override void MapBindings()
            {
                BindLocal();
            }
        }

        /// 显式挂载到指定父 Bootstrap 的子（两侧 GameObject 无层级关系）
        private sealed class ParentedHierarchyBootstrap : HierarchyTestBootstrap
        {
            protected override void MapBindings()
            {
                BindLocal();
            }
        }

        private abstract class HierarchyTestBootstrap : Bootstrap
        {
            public IReadOnlyList<Bootstrap> ExposedChildren => _children;
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
