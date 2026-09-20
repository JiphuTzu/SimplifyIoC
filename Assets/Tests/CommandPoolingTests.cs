// 5.5 Command 池化统一测试。
// 主题只有一条：命令实例的生命周期只剩一种走法——按命令类型取池、执行、归还。
// 所有用例都写成"能在改造前/后同时表达同一件事"的形式，避免退化成实现快照。
using System;
using System.Collections.Generic;
using NUnit.Framework;
using SimplifyIoC.Commands;
using SimplifyIoC.Framework;
using SimplifyIoC.Injectors;
using SimplifyIoC.Pools;
using SimplifyIoC.Signals;

namespace SimplifyIoC.Tests
{
    /// 持有进程内依赖的命令：用来确认回收实例在每次派发前都被重新注入
    public class RecyclingProbeCommand : Command
    {
        public static int instances;
        public static readonly List<bool> SawServiceOnExecute = new();

        [Inject]
        public ITestService service { get; set; }

        public RecyclingProbeCommand()
        {
            instances++;
        }

        public override void Execute()
        {
            SawServiceOnExecute.Add(service != null);
        }
    }

    /// Retain 的异步命令：不 Release 就不应该回池
    public class RetainingCommand : Command
    {
        public static RetainingCommand last;

        public override void Execute()
        {
            last = this;
            Retain();
        }
    }

    /// 5.5 第二个信号（用于验证同一命令类型被两个绑定共享时池不会被误回收）
    public class PoolingSecondSignal : Signal { }

    public class CommandPoolingTests
    {
        private InjectionBinder _binder;
        private ProbeCommandBinder _commandBinder;

        [SetUp]
        public void SetUp()
        {
            _binder = new InjectionBinder();
            _commandBinder = new ProbeCommandBinder();

            _binder.Bind<IInjectionBinder>().ToValue(_binder);
            _binder.Bind<ICommandBinder>().ToValue(_commandBinder);
            _binder.Bind<IInstanceProvider>().ToValue(_binder);
            _commandBinder.injectionBinder = _binder;

            TestCommand.ExecutionCount = 0;
            TestCommand.LastInstance = null;
            TestCommand.LastCommandBinder = null;
            TestCommand.LastInjectionBinder = null;
            RecyclingProbeCommand.instances = 0;
            RecyclingProbeCommand.SawServiceOnExecute.Clear();
            RetainingCommand.last = null;
        }

        // ---- 统一池化 ----

        [Test]
        public void CommandsAreRecycledWithoutPooledDeclaration()
        {
            _binder.Bind<TestSignal>().ToSingleton();
            _commandBinder.Bind<TestSignal>().To<TestCommand>();
            var signal = _binder.GetInstance<TestSignal>();

            signal.Dispatch();
            var first = TestCommand.LastInstance;
            signal.Dispatch();
            var second = TestCommand.LastInstance;

            Assert.AreEqual(2, TestCommand.ExecutionCount);
            Assert.AreSame(first, second, "统一池化后，未声明 Pooled() 的命令同样复用实例");
        }

        [Test]
        public void CommandPoolLeavesNoTraceInTheInjectionBinder()
        {
            _binder.Bind<TestSignal>().ToSingleton();
            _commandBinder.Bind<TestSignal>().To<TestCommand>();

            _binder.GetInstance<TestSignal>().Dispatch();

            //旧实现会为池补一条 Bind<TestCommand>().To<TestCommand>()：
            //它与用户自己的同名绑定相遇就把容器打进 conflicted 状态。现在不该有任何痕迹。
            Assert.IsNull(_binder.GetBinding<TestCommand>());
        }

        [Test]
        public void RecycledCommandIsReinjectedBeforeEveryExecute()
        {
            _binder.Bind<ITestService>().To<TestServiceImpl>().ToSingleton();
            _binder.Bind<TestSignal>().ToSingleton();
            _commandBinder.Bind<TestSignal>().To<RecyclingProbeCommand>();
            var signal = _binder.GetInstance<TestSignal>();

            signal.Dispatch();
            signal.Dispatch();

            Assert.AreEqual(1, RecyclingProbeCommand.instances, "两次派发应复用同一实例");
            Assert.AreEqual(new[] { true, true }, RecyclingProbeCommand.SawServiceOnExecute.ToArray(),
                "回收实例在每次 Execute 前都必须重新拿到依赖（Restore 会把上一次的注入清掉）");
        }

        /// <summary>
        /// 5.5 的行为变更：旧实现里未声明 Pooled() 的命令执行完就没人管，注入值原样留着；
        /// 统一池化后每条命令执行完都会归还并 Restore（Uninject），注入槽位随即清空。
        /// 好处是池不再长期持有对 Context/单例的引用；代价是命令引用在 Dispatch 返回后不可再用。
        /// 这条用例把该语义钉住——用户代码若在 Execute 之外持有 Command，看到的应是空槽位而非脏数据。
        /// </summary>
        [Test]
        public void DispatchReturnLeavesTheCommandUninjected()
        {
            _binder.Bind<TestSignal>().ToSingleton();
            _commandBinder.Bind<TestSignal>().To<TestCommand>();
            var signal = _binder.GetInstance<TestSignal>();

            signal.Dispatch();

            var executed = TestCommand.LastInstance;
            Assert.IsNotNull(executed);
            Assert.IsTrue(executed.isClean, "执行完即归还并被 Restore");
            Assert.IsNull(executed.commandBinder);
            Assert.IsNull(executed.injectionBinder);
        }

        // ---- Retain / Release ----

        [Test]
        public void RetainedCommandStaysOutOfThePoolUntilReleased()
        {
            _binder.Bind<TestSignal>().ToSingleton();
            _commandBinder.Bind<TestSignal>().To<RetainingCommand>();

            _binder.GetInstance<TestSignal>().Dispatch();

            var command = RetainingCommand.last;
            Assert.IsNotNull(command);
            Assert.IsTrue(_commandBinder.HasPool(typeof(RetainingCommand)), "派发过就该有池");
            Assert.AreEqual(0, _commandBinder.Available(typeof(RetainingCommand)), "Retain 期间不应归还");

            command.Release();

            Assert.IsTrue(command.isClean, "归还时 Restore() 应已执行");
            Assert.AreEqual(1, _commandBinder.Available(typeof(RetainingCommand)));
        }

        // ---- 孤儿池回收 ----

        [Test]
        public void OnceBindingDropsItsCommandPool()
        {
            _binder.Bind<TestSignal>().ToSingleton();
            _commandBinder.Bind<TestSignal>().To<TestCommand>().Once();
            var signal = _binder.GetInstance<TestSignal>();

            signal.Dispatch();

            Assert.IsNull(_commandBinder.GetBinding(signal), "Once 后绑定应移除");
            Assert.IsFalse(_commandBinder.HasPool(typeof(TestCommand)),
                "绑定已消失，命令实例不该继续被 Context 持有");
        }

        [Test]
        public void SharedCommandTypeKeepsItsPoolWhileAnotherBindingRemains()
        {
            _binder.Bind<TestSignal>().ToSingleton();
            _binder.Bind<PoolingSecondSignal>().ToSingleton();
            _commandBinder.Bind<TestSignal>().To<TestCommand>();
            _commandBinder.Bind<PoolingSecondSignal>().To<TestCommand>();
            _binder.GetInstance<TestSignal>().Dispatch();
            _binder.GetInstance<PoolingSecondSignal>().Dispatch();

            _commandBinder.Unbind(_binder.GetInstance<TestSignal>());

            Assert.IsTrue(_commandBinder.HasPool(typeof(TestCommand)),
                "另一个绑定仍在引用同一命令类型，池不能回收");
        }

        // ---- 销毁 ----

        [Test]
        public void OnRemoveReleasesPoolInstancesAndTrackers()
        {
            _binder.Bind<TestSignal>().ToSingleton();
            _commandBinder.Bind<TestSignal>().To<TestCommand>();
            _binder.GetInstance<TestSignal>().Dispatch();

            Assert.AreEqual(1, _commandBinder.Available(typeof(TestCommand)));
            var dispatched = TestCommand.LastInstance;

            _commandBinder.OnRemove();

            //归还时刻（而非销毁时刻）执行的 Restore 已经把它置为 clean；
            //OnRemove 负责的是把池连同其中的引用一起释放掉。
            Assert.IsTrue(dispatched.isClean);
            Assert.AreEqual(0, _commandBinder.PoolCount);
            Assert.AreEqual(0, _commandBinder.ActiveCommands);
        }

        // ---- Pool 自身 ----

        [Test]
        public void PoolIsNotIPoolableAnymore()
        {
            var pool = new Pool<PooledThing>();

            Assert.IsFalse(pool is IPoolable,
                "池不再实现 IPoolable：否则被归还时 Restore() 会把它自己清空");
        }

        // ---- 废弃开关 ----

        [Test]
        public void UsePoolingIsRetainedButInert()
        {
#pragma warning disable 618 // usePooling 已 Obsolete，这里正是要确认它不再改变行为
            _commandBinder.usePooling = false;
#pragma warning restore 618

            _binder.Bind<TestSignal>().ToSingleton();
            _commandBinder.Bind<TestSignal>().To<TestCommand>();
            var signal = _binder.GetInstance<TestSignal>();

            signal.Dispatch();
            var first = TestCommand.LastInstance;
            signal.Dispatch();

            Assert.AreEqual(2, TestCommand.ExecutionCount);
            Assert.AreSame(first, TestCommand.LastInstance, "开关已无作用，命令依旧走池");
        }

        /// 只为测试开一扇窗：pools / activeCommands 都是 protected。
        private sealed class ProbeCommandBinder : CommandBinder
        {
            public int PoolCount => pools.Count;

            public bool HasPool(Type commandType)
            {
                return pools.ContainsKey(commandType);
            }

            public int Available(Type commandType)
            {
                return pools.TryGetValue(commandType, out var pool) ? pool.available : -1;
            }

            public int ActiveCommands => activeCommands.Count;
        }
    }
}
