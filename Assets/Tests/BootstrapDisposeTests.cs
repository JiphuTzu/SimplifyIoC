// 3.1：Bootstrap.Dispose 契约回归测试。
// 覆盖：注册表清空、信号监听移除、幂等、成链释放与链根判定、
//       Dispose 后使用抛 ObjectDisposedException。
using System;
using NUnit.Framework;
using SimplifyIoC.Commands;
using SimplifyIoC.Bootstraps;
using SimplifyIoC.Injectors;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SimplifyIoC.Tests
{
    public class BootstrapDisposeTests
    {
        private GameObject _bootstrapObject;
        private DisposeTestBootstrap _bootstrap;

        [SetUp]
        public void SetUp()
        {
            //锚点是进程级静态槽：先归零，本类用例里 _bootstrap 才会是可预期的链根
            Bootstrap.root = null;
            TestCommand.ExecutionCount = 0;
            _bootstrapObject = new GameObject("BootstrapDisposeTests.Bootstrap");
            _bootstrap = _bootstrapObject.AddComponent<DisposeTestBootstrap>();
            //EditMode 不派发 MonoBehaviour 生命周期（Awake），显式建容器
            _bootstrap.Init();
        }

        [TearDown]
        public void TearDown()
        {
            _bootstrap?.Dispose();
            if (_bootstrapObject != null) Object.DestroyImmediate(_bootstrapObject);
            Bootstrap.root = null;
        }

        [Test]
        public void DisposeClearsBindingRegistries()
        {
            var service = _bootstrap.ExposedInjectionBinder.GetInstance<ITestService>();
            Assert.That(service, Is.Not.Null);

            _bootstrap.Dispose();

            Assert.That(_bootstrap.ExposedInjectionBinder.GetBinding<ITestService>(), Is.Null);
            Assert.That(_bootstrap.ExposedCommandBinder, Is.Null);
            //注册表已空 → 按既有行为抛"no binding"异常
            Assert.Throws<Exception>(() => _bootstrap.ExposedInjectionBinder.GetInstance<ITestService>());
        }

        [Test]
        public void DisposeIsIdempotent()
        {
            _bootstrap.Dispose();
            Assert.DoesNotThrow(() => _bootstrap.Dispose());
            Assert.That(_bootstrap.ExposedInjectionBinder.GetBinding<ITestService>(), Is.Null);
        }

        [Test]
        public void BootstrapOnItsOwnGameObjectIsItsOwnChainRoot()
        {
            //本 Bootstrap 挂在场景根上，显式 parent 与 Transform 层级都找不到父；
            //又因为 SetUp 已清空锚点，它就是第一个构造的链根 → 自任链根并接任锚点。
            //（"谁先构造谁当根"这条 2026-10-15 随 firstContext 一起恢复，但它只作用于兜底：
            //  显式 parent 与 Transform 层级仍然优先。）
            Assert.That(_bootstrap.isRoot, Is.True);
            Assert.That(_bootstrap.parentBootstrap, Is.Null);

            _bootstrap.Dispose();

            Assert.That(_bootstrap.ExposedInjectionBinder.crossContextBinder, Is.Null,
                "链根 Dispose 要断开并清空共享跨域根 binder");
        }

        [Test]
        public void SignalListenerRemovedAfterDispose()
        {
            var signal = _bootstrap.ExposedInjectionBinder.GetInstance<TestSignal>();
            signal.Dispatch();
            var countAfterDispatch = TestCommand.ExecutionCount;
            Assert.That(countAfterDispatch, Is.GreaterThan(0));

            _bootstrap.Dispose();

            //信号实例仍在，但其命令监听已被 OnRemove 移除
            signal.Dispatch();
            Assert.That(TestCommand.ExecutionCount, Is.EqualTo(countAfterDispatch));
        }

        [Test]
        public void InitAfterDisposeThrowsObjectDisposed()
        {
            var manualObject = new GameObject("BootstrapDisposeTests.ManualBootstrap");
            try
            {
                var manualBootstrap = manualObject.AddComponent<BareTestBootstrap>();
                manualBootstrap.Init();
                manualBootstrap.Dispose();
                Assert.Throws<ObjectDisposedException>(() => manualBootstrap.Init());
            }
            finally
            {
                Object.DestroyImmediate(manualObject);
            }
        }

        private sealed class DisposeTestBootstrap : Bootstrap
        {
            public ICrossContextInjectionBinder ExposedInjectionBinder => injectionBinder;
            public ICommandBinder ExposedCommandBinder => commandBinder;

            protected override void MapBindings()
            {
                injectionBinder.Bind<ITestService>().To<TestServiceImpl>().ToSingleton();
                injectionBinder.Bind<TestSignal>().ToSingleton();
                commandBinder.Bind<TestSignal>().To<TestCommand>();
            }
        }

        private sealed class BareTestBootstrap : Bootstrap { }
    }
}
