// 4.3：Bind<TSignal>().ToHandler(...) 短路通路的回归测试。
// 钉住的行为：
//  1) 信号派发直达"容器解析出的服务 + 用户委托"，不创建 Command、不进命令链；
//  2) 服务生命周期沿用绑定语义（单例共享 / transient 每次新建）；
//  3) 载荷个数或类型不符时在派发期抛异常，绝不静默传 null；
//  4) handler 内再次派发同一信号不互相破坏（与 3.4 的作用域结论一致）；
//  5) Once() 复用命令路径语义；InSequence()/Pooled()/To<T>()/ToName() 混用在绑定期即被拒绝。
using System;
using System.Collections.Generic;
using NUnit.Framework;
using SimplifyIoC.Commands;
using SimplifyIoC.Framework;
using SimplifyIoC.Injectors;
using SimplifyIoC.Signals;

namespace SimplifyIoC.Tests
{
    public class CommandHandlerTests
    {
        private InjectionBinder _binder;
        private CommandBinder _commandBinder;

        [SetUp]
        public void SetUp()
        {
            _binder = new InjectionBinder();
            _commandBinder = new CommandBinder();

            _binder.Bind<IInjectionBinder>().ToValue(_binder);
            _binder.Bind<ICommandBinder>().ToValue(_commandBinder);
            _binder.Bind<IInstanceProvider>().ToValue(_binder);
            _commandBinder.injectionBinder = _binder;

            HandlerService.Instances = 0;
            TestCommand.ExecutionLog.Clear();
            TestCommand.ExecutionCount = 0;
            TestCommand.LastInstance = null;
            ValueCommand.Received.Clear();
        }

        // ---- 基本调用 ----

        [Test]
        public void HandlerReceivesSignalPayloadAndCreatesNoCommand()
        {
            _binder.Bind<IHandlerService>().To<HandlerService>().ToSingleton();
            _binder.Bind<TestValueSignal>().ToSingleton();
            var received = new List<int>();
            _commandBinder.Bind<TestValueSignal>().ToHandler<IHandlerService, int>((svc, v) => received.Add(v));

            _binder.GetInstance<TestValueSignal>().Dispatch(42);

            Assert.That(received, Is.EqualTo(new[] { 42 }));
            Assert.That(TestCommand.ExecutionCount, Is.EqualTo(0), "短路通路不得创建 Command");
            Assert.That(ValueCommand.Received, Is.Empty);
        }

        [Test]
        public void ServiceIsResolvedThroughTheContainerAndFollowsItsLifetime()
        {
            _binder.Bind<IHandlerService>().To<HandlerService>().ToSingleton();
            _binder.Bind<TestSignal>().ToSingleton();
            var services = new List<IHandlerService>();
            _commandBinder.Bind<TestSignal>().ToHandler<IHandlerService>(svc => services.Add(svc));

            var signal = _binder.GetInstance<TestSignal>();
            signal.Dispatch();
            signal.Dispatch();

            Assert.That(services.Count, Is.EqualTo(2));
            Assert.That(services[0], Is.SameAs(services[1]), "单例绑定：多次派发应是同一实例");
        }

        [Test]
        public void TransientServiceProducesNewInstancePerDispatch()
        {
            _binder.Bind<IHandlerService>().To<HandlerService>();
            _binder.Bind<TestSignal>().ToSingleton();
            var services = new List<IHandlerService>();
            _commandBinder.Bind<TestSignal>().ToHandler<IHandlerService>(svc => services.Add(svc));

            var signal = _binder.GetInstance<TestSignal>();
            signal.Dispatch();
            signal.Dispatch();

            Assert.That(services[0], Is.Not.SameAs(services[1]), "transient 绑定：每次派发应是新实例");
            Assert.That(HandlerService.Instances, Is.EqualTo(2));
        }

        // ---- 载荷校验：不静默传 null ----

        [Test]
        public void PayloadTypeMismatchThrows()
        {
            _binder.Bind<IHandlerService>().To<HandlerService>().ToSingleton();
            _binder.Bind<TestValueSignal>().ToSingleton();
            _commandBinder.Bind<TestValueSignal>().ToHandler<IHandlerService, string>((svc, s) => { });

            var signal = _binder.GetInstance<TestValueSignal>();

            Assert.Throws<Exception>(() => signal.Dispatch(42),
                "载荷类型不符必须显式报错，而不是把 null 递给 handler");
        }

        [Test]
        public void PayloadCountMismatchThrows()
        {
            _binder.Bind<IHandlerService>().To<HandlerService>().ToSingleton();
            _binder.Bind<TestValueSignal>().ToSingleton();
            //信号的载荷会被忽略：handler 声明"零载荷"
            _commandBinder.Bind<TestValueSignal>().ToHandler<IHandlerService>(svc => { });

            var signal = _binder.GetInstance<TestValueSignal>();

            Assert.Throws<Exception>(() => signal.Dispatch(42),
                "载荷个数与 handler 形参不匹配必须报错");
        }

        [Test]
        public void WeakHandlerReceivesRawPayloadArray()
        {
            _binder.Bind<IHandlerService>().To<HandlerService>().ToSingleton();
            _binder.Bind<TestValueSignal>().ToSingleton();
            object[] captured = null;
            _commandBinder.Bind<TestValueSignal>().ToHandler<IHandlerService>((svc, payload) => captured = payload);

            _binder.GetInstance<TestValueSignal>().Dispatch(9);

            Assert.That(captured, Is.Not.Null);
            Assert.That(captured.Length, Is.EqualTo(1));
            Assert.That(captured[0], Is.EqualTo(9));
        }

        // ---- 重入 ----

        [Test]
        public void HandlerCanDispatchTheSameSignalReentrantly()
        {
            _binder.Bind<IHandlerService>().To<HandlerService>().ToSingleton();
            _binder.Bind<TestValueSignal>().ToSingleton();
            var seen = new List<int>();
            _commandBinder.Bind<TestValueSignal>().ToHandler<IHandlerService, int>((svc, v) =>
            {
                seen.Add(v);
                if (v == 1)
                    _binder.GetInstance<TestValueSignal>().Dispatch(2);
            });

            _binder.GetInstance<TestValueSignal>().Dispatch(1);

            Assert.That(seen, Is.EqualTo(new[] { 1, 2 }), "handler 内再次派发同一信号应各自持有自己的载荷");
        }

        // ---- Once ----

        [Test]
        public void OnceHandlerRunsOnceAndBindingIsRemoved()
        {
            _binder.Bind<IHandlerService>().To<HandlerService>().ToSingleton();
            _binder.Bind<TestValueSignal>().ToSingleton();
            var seen = new List<int>();
            _commandBinder.Bind<TestValueSignal>().ToHandler<IHandlerService, int>((svc, v) => seen.Add(v)).Once();

            var signal = _binder.GetInstance<TestValueSignal>();
            signal.Dispatch(1);
            signal.Dispatch(2);

            Assert.That(seen, Is.EqualTo(new[] { 1 }));
            Assert.That(_commandBinder.GetBinding(signal), Is.Null, "Once 后绑定应被移除");
        }

        // ---- 互斥组合：绑定期早失败 ----

        [Test]
        public void InSequenceAfterToHandlerThrows()
        {
            _binder.Bind<TestSignal>().ToSingleton();

            Assert.Throws<Exception>(() =>
                _commandBinder.Bind<TestSignal>().ToHandler<IHandlerService>(svc => { }).InSequence());
        }

        [Test]
        public void ToHandlerAfterSequenceThrows()
        {
            _binder.Bind<TestSignal>().ToSingleton();
            var binding = _commandBinder.Bind<TestSignal>().To<TestCommand>().InSequence();

            Assert.Throws<Exception>(() => binding.ToHandler<IHandlerService>(svc => { }));
        }

        [Test]
        public void ToCommandAfterToHandlerThrows()
        {
            _binder.Bind<TestSignal>().ToSingleton();

            Assert.Throws<Exception>(() =>
                _commandBinder.Bind<TestSignal>().ToHandler<IHandlerService>(svc => { }).To<TestCommand>());
        }

        [Test]
        public void ToNameAndHandlerAreMutuallyExclusive()
        {
            _binder.Bind<TestSignal>().ToSingleton();

            //handler 之后：ToName 是 new 出来的接口方法，若只在 To(object) 上守卫会被绕过
            Assert.Throws<Exception>(() =>
                _commandBinder.Bind<TestSignal>().ToHandler<IHandlerService>(svc => { }).ToName("named"));

            //ToName 之后：SetHandler 必须同时拒绝已被命名（存在第二来源声明）的绑定
            Assert.Throws<Exception>(() =>
                _commandBinder.Bind<TestSignal>().ToName("named").ToHandler<IHandlerService>(svc => { }));
        }

        // ---- 回归：命令路径不受影响 ----

        [Test]
        public void CommandPathStillWorksAlongsideHandlerPath()
        {
            _binder.Bind<TestSignal>().ToSingleton();
            _commandBinder.Bind<TestSignal>().To<TestCommand>();

            _binder.GetInstance<TestSignal>().Dispatch();

            Assert.That(TestCommand.ExecutionCount, Is.EqualTo(1));
        }

        // ---- 夹具 ----

        private interface IHandlerService
        {
        }

        private sealed class HandlerService : IHandlerService
        {
            public static int Instances;

            public HandlerService() => Instances++;
        }
    }
}
