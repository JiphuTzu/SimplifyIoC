// CommandBinder 行为快照测试：并行、序列、Once、池化、信号载荷注入。
using System;
using NUnit.Framework;
using SimplifyIoC.Commands;
using SimplifyIoC.Framework;
using SimplifyIoC.Injectors;
using SimplifyIoC.Signals;

namespace SimplifyIoC.Tests
{
    public class CommandBinderTests
    {
        private InjectionBinder _binder;
        private CommandBinder _commandBinder;

        [SetUp]
        public void SetUp()
        {
            _binder = new InjectionBinder();
            _commandBinder = new CommandBinder();

            // Command 的 [Inject] 依赖
            _binder.Bind<IInjectionBinder>().ToValue(_binder);
            _binder.Bind<ICommandBinder>().ToValue(_commandBinder);
            // 池化路径需要 IInstanceProvider（Pool 的 [Inject] 属性）
            _binder.Bind<IInstanceProvider>().ToValue(_binder);
            _commandBinder.injectionBinder = _binder;

            TestCommand.ExecutionLog.Clear();
            TestCommand.ExecutionCount = 0;
            TestCommand.LastInstance = null;
            ValueCommand.Received.Clear();
            PooledPayloadCommand.Received.Clear();
            ReentrantValueCommand.Received.Clear();
            ReentrantValueCommand.reentered = false;
        }

        // ---- 基本派发 ----

        [Test]
        public void SignalDispatchExecutesBoundCommand()
        {
            _binder.Bind<TestSignal>().ToSingleton();
            _commandBinder.Bind<TestSignal>().To<TestCommand>();
            var signal = _binder.GetInstance<TestSignal>();

            signal.Dispatch();

            Assert.AreEqual(1, TestCommand.ExecutionCount);
        }

        [Test]
        public void SignalPayloadIsBoundIntoCommandInjection()
        {
            _binder.Bind<TestValueSignal>().ToSingleton();
            _commandBinder.Bind<TestValueSignal>().To<ValueCommand>();
            var signal = _binder.GetInstance<TestValueSignal>();

            signal.Dispatch(42);

            Assert.AreEqual(new[] { 42 }, ValueCommand.Received);
        }

        [Test]
        public void CommandReceivesCommandBinderAndInjectionBinderInjections()
        {
            _binder.Bind<TestSignal>().ToSingleton();
            _commandBinder.Bind<TestSignal>().To<TestCommand>();
            var signal = _binder.GetInstance<TestSignal>();

            signal.Dispatch();

            Assert.IsNotNull(TestCommand.LastInstance.commandBinder);
            Assert.IsNotNull(TestCommand.LastInstance.injectionBinder);
        }

        // ---- Once ----

        [Test]
        public void OnceBindingUnbindsAfterFirstDispatch()
        {
            _binder.Bind<TestSignal>().ToSingleton();
            _commandBinder.Bind<TestSignal>().To<TestCommand>().Once();
            var signal = _binder.GetInstance<TestSignal>();

            signal.Dispatch();
            signal.Dispatch();

            Assert.AreEqual(1, TestCommand.ExecutionCount);
            Assert.IsNull(_commandBinder.GetBinding(signal), "Once 后绑定应已移除");
        }

        // ---- 序列与并行 ----

        [Test]
        public void InSequenceRunsCommandsInBindingOrder()
        {
            _binder.Bind<TestSignal>().ToSingleton();
            _commandBinder.Bind<TestSignal>().To<SeqCommandA>().To<SeqCommandB>().InSequence();
            var signal = _binder.GetInstance<TestSignal>();

            signal.Dispatch();

            Assert.AreEqual(
                new[] { nameof(SeqCommandA), nameof(SeqCommandB) },
                TestCommand.ExecutionLog.ToArray());
        }

        [Test]
        public void ParallelBindingExecutesAllCommands()
        {
            _binder.Bind<TestSignal>().ToSingleton();
            _commandBinder.Bind<TestSignal>().To<SeqCommandA>().To<SeqCommandB>();
            var signal = _binder.GetInstance<TestSignal>();

            signal.Dispatch();

            Assert.AreEqual(2, TestCommand.ExecutionLog.Count);
            CollectionAssert.Contains(TestCommand.ExecutionLog, nameof(SeqCommandA));
            CollectionAssert.Contains(TestCommand.ExecutionLog, nameof(SeqCommandB));
        }

        // ---- 池化 ----

        [Test]
        public void PooledCommandIsRecycledAcrossDispatches()
        {
            _binder.Bind<TestSignal>().ToSingleton();
            _commandBinder.Bind<TestSignal>().To<TestCommand>().Pooled();
            var signal = _binder.GetInstance<TestSignal>();

            signal.Dispatch();
            var first = TestCommand.LastInstance;

            signal.Dispatch();
            var second = TestCommand.LastInstance;

            Assert.AreEqual(2, TestCommand.ExecutionCount);
            Assert.AreSame(first, second, "池化路径应复用同一实例");
        }

        // ---- 池化关闭 ----

        [Test]
        public void UsePoolingFalseKeepsTempBindUnbindPath()
        {
            _commandBinder.usePooling = false;
            _binder.Bind<TestSignal>().ToSingleton();
            _commandBinder.Bind<TestSignal>().To<TestCommand>();
            var signal = _binder.GetInstance<TestSignal>();

            signal.Dispatch();

            Assert.AreEqual(1, TestCommand.ExecutionCount);
            // 非池化路径的临时 Command 绑定应已清理
            Assert.IsNull(_binder.GetBinding<Command>());
        }

        [Test]
        public void DispatchWithoutBindingIsSilentNoop()
        {
            _binder.Bind<TestSignal>().ToSingleton();
            var signal = _binder.GetInstance<TestSignal>();

            Assert.DoesNotThrow(() => signal.Dispatch());
            Assert.AreEqual(0, TestCommand.ExecutionCount);
        }

        // ---- 3.4.a 调用级作用域 ----
        // 这一组用例在改造前必然为红：载荷经全局容器临时 Bind，嵌套派发会与原绑定冲突。

        [Test]
        public void NestedDispatchKeepsEachCommandPayloadIndependent()
        {
            _binder.Bind<TestValueSignal>().ToSingleton();
            _commandBinder.Bind<TestValueSignal>().To<ReentrantValueCommand>();
            var signal = _binder.GetInstance<TestValueSignal>();

            signal.Dispatch(42);

            Assert.AreEqual(new[] { 42, 77 }, ReentrantValueCommand.Received.ToArray(),
                "嵌套派发时外层命令必须保持自己的载荷，不能被内层覆盖");
        }

        [Test]
        public void PayloadTakesPrecedenceOverGlobalBindingOfSameType()
        {
            _binder.Bind(typeof(int)).ToValue(7);
            _binder.Bind<TestValueSignal>().ToSingleton();
            _commandBinder.Bind<TestValueSignal>().To<ValueCommand>();
            var signal = _binder.GetInstance<TestValueSignal>();

            signal.Dispatch(42);

            Assert.AreEqual(new[] { 42 }, ValueCommand.Received.ToArray(),
                "调用级载荷应优先于全局同名绑定");
            Assert.AreEqual(7, _binder.GetInstance<int>(),
                "全局绑定不应被载荷破坏");
        }

        [Test]
        public void PooledCommandReceivesPayloadOfEachDispatch()
        {
            _binder.Bind<PayloadSignal>().ToSingleton();
            _commandBinder.Bind<PayloadSignal>().To<PooledPayloadCommand>().Pooled();
            var signal = _binder.GetInstance<PayloadSignal>();

            signal.Dispatch(new PayloadVO { value = 1 });
            signal.Dispatch(new PayloadVO { value = 2 });
            signal.Dispatch(new PayloadVO { value = 3 });

            Assert.AreEqual(3, PooledPayloadCommand.Received.Count, "三次派发都应执行");
            Assert.AreEqual(1, PooledPayloadCommand.Received[0].value, "首次创建实例时就要注入载荷");
            Assert.AreEqual(2, PooledPayloadCommand.Received[1].value);
            Assert.AreEqual(3, PooledPayloadCommand.Received[2].value, "回收复用的实例应重新注入本次载荷");
        }

        [Test]
        public void PayloadTypeNotDeclaredBySignalIsNotSilentlyInjected()
        {
            _binder.Bind<TestValueSignal>().ToSingleton();
            _commandBinder.Bind<TestValueSignal>().To<StringConsumerCommand>();
            var signal = _binder.GetInstance<TestValueSignal>();

            Assert.Throws<Exception>(() => signal.Dispatch(42),
                "载荷中没有 string，注入必须显式报错而不是静默注入 null");
        }
    }
}
