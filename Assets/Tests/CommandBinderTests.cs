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
    }
}
