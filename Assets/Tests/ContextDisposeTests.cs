// 3.1：Context.Dispose 契约回归测试。
// 覆盖：注册表清空、信号监听移除、幂等、成链释放与链根判定、
//       Dispose 后使用抛 ObjectDisposedException。
using System;
using NUnit.Framework;
using SimplifyIoC.Commands;
using SimplifyIoC.Contexts;
using SimplifyIoC.Injectors;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SimplifyIoC.Tests
{
    public class ContextDisposeTests
    {
        private GameObject _bootstrapObject;
        private DisposeTestContext _context;

        [SetUp]
        public void SetUp()
        {
            TestCommand.ExecutionCount = 0;
            _bootstrapObject = new GameObject("ContextDisposeTests.Bootstrap");
            _context = new DisposeTestContext(_bootstrapObject.AddComponent<Bootstrap>());
        }

        [TearDown]
        public void TearDown()
        {
            _context?.Dispose();
            if (_bootstrapObject != null) Object.DestroyImmediate(_bootstrapObject);
        }

        [Test]
        public void DisposeClearsBindingRegistries()
        {
            var service = _context.ExposedInjectionBinder.GetInstance<ITestService>();
            Assert.That(service, Is.Not.Null);

            _context.Dispose();

            Assert.That(_context.ExposedInjectionBinder.GetBinding<ITestService>(), Is.Null);
            Assert.That(_context.ExposedCommandBinder, Is.Null);
            //注册表已空 → 按既有行为抛"no binding"异常
            Assert.Throws<Exception>(() => _context.ExposedInjectionBinder.GetInstance<ITestService>());
        }

        [Test]
        public void DisposeIsIdempotent()
        {
            _context.Dispose();
            Assert.DoesNotThrow(() => _context.Dispose());
            Assert.That(_context.ExposedInjectionBinder.GetBinding<ITestService>(), Is.Null);
        }

        [Test]
        public void ContextUnderARootlessBootstrapIsItsOwnChainRoot()
        {
            //5.6：链根不再由"谁先构造"这种进程级全局状态决定——
            //只看它有没有父 Bootstrap。本 Context 挂在场景根上，因此自任链根，
            // Dispose 时无需与任何其它 Context 协商，也不留下任何跨测试用例的状态。
            Assert.That(_context.isRoot, Is.True);
            Assert.That(_context.parentContext, Is.Null);

            _context.Dispose();

            Assert.That(_context.ExposedInjectionBinder.crossContextBinder, Is.Null,
                "链根 Dispose 要断开并清空共享跨域根 binder");
        }

        [Test]
        public void SignalListenerRemovedAfterDispose()
        {
            var signal = _context.ExposedInjectionBinder.GetInstance<TestSignal>();
            signal.Dispatch();
            var countAfterDispatch = TestCommand.ExecutionCount;
            Assert.That(countAfterDispatch, Is.GreaterThan(0));

            _context.Dispose();

            //信号实例仍在，但其命令监听已被 OnRemove 移除
            signal.Dispatch();
            Assert.That(TestCommand.ExecutionCount, Is.EqualTo(countAfterDispatch));
        }

        [Test]
        public void StartAfterDisposeThrowsObjectDisposed()
        {
            var manualObject = new GameObject("ContextDisposeTests.ManualBootstrap");
            try
            {
                var manualContext = new ManualStartContext(
                    manualObject.AddComponent<Bootstrap>(), ContextStartupFlags.ManualMapping);
                manualContext.Dispose();
                Assert.Throws<ObjectDisposedException>(() => manualContext.Start());
            }
            finally
            {
                Object.DestroyImmediate(manualObject);
            }
        }

        private sealed class DisposeTestContext : Context
        {
            public DisposeTestContext(Bootstrap view) : base(view) { }

            public ICrossContextInjectionBinder ExposedInjectionBinder => injectionBinder;
            public ICommandBinder ExposedCommandBinder => commandBinder;

            protected override void MapBindings()
            {
                injectionBinder.Bind<ITestService>().To<TestServiceImpl>().ToSingleton();
                injectionBinder.Bind<TestSignal>().ToSingleton();
                commandBinder.Bind<TestSignal>().To<TestCommand>();
            }
        }

        private sealed class ManualStartContext : Context
        {
            public ManualStartContext(Bootstrap view, ContextStartupFlags flags) : base(view, flags) { }
        }
    }
}
