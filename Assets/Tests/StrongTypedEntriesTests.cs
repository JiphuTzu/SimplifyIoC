// 5.3 强类型入口测试。
// 每一步新 API 都与其等价的既有链式写法对照：新增入口只做一件事——
// 把原本只能在运行期发现的问题提前到编译期，运行时语义不允许有任何漂移。
using NUnit.Framework;
using SimplifyIoC.Commands;
using SimplifyIoC.Framework;
using SimplifyIoC.Injectors;
using SimplifyIoC.Mediations;
using SimplifyIoC.Signals;

namespace SimplifyIoC.Tests
{
    /// 5.3 用作绑定键/绑定名的枚举夹具
    public enum StrongTypedKeys
    {
        Primary,
        Secondary
    }

    /// 按名字注入的消费者：用来确认 Bind<T>(Enum/string) 与 ToName(...) 落在同一个名字槽位
    public class NamedServiceConsumer
    {
        [Inject(StrongTypedKeys.Primary)]
        public ITestService primary { get; set; }

        [Inject("secondary")]
        public ITestService secondary { get; set; }
    }

    /// 5.3 成对入口（视图/中介者）的类型夹具，只做绑定 registrations，不实例化
    public class PairEntryView : View { }

    public class PairEntryMediator : Mediator { }

    /// 5.3 成对入口（信号/命令）用的第二个信号
    public class PairEntrySecondSignal : Signal { }

    public class StrongTypedEntriesTests
    {
        private InjectionBinder _binder;

        [SetUp]
        public void SetUp()
        {
            _binder = new InjectionBinder();
            TestServiceImpl.InstanceCount = 0;
            TestCommand.ExecutionCount = 0;
            TestCommand.LastInstance = null;
        }

        // ---- Binder：枚举键 ----

        [Test]
        public void EnumKeyRoundTripsThroughBindGetAndUnbind()
        {
            var binder = new Binder();

            var binding = binder.Bind(StrongTypedKeys.Primary).To("value");

            Assert.AreSame(binding, binder.GetBinding(StrongTypedKeys.Primary));
            binder.Unbind(StrongTypedKeys.Primary);
            Assert.IsNull(binder.GetBinding(StrongTypedKeys.Primary));
        }

        [Test]
        public void EnumKeySharesSlotWithTheObjectOverload()
        {
            var binder = new Binder();

            var binding = binder.Bind(StrongTypedKeys.Primary).To("value");

            //与历史写法 Bind((object)SomeEnum.X) 完全一致：枚举按装箱值作键
            Assert.AreSame(binding, binder.GetBinding((object)StrongTypedKeys.Primary));
            //刻意不做 ToString() 归一化：枚举与其字符串形式是两条独立的键/名
            Assert.IsNull(binder.GetBinding("Primary"));
        }

        // ---- InjectionBinder：命名入口 ----

        [Test]
        public void EnumNamedBindingResolvesThroughGetInstance()
        {
            _binder.Bind<ITestService>(StrongTypedKeys.Primary).To<TestServiceImpl>().ToSingleton();

            var instance = _binder.GetInstance<ITestService>(StrongTypedKeys.Primary);

            Assert.IsNotNull(instance);
            Assert.AreSame(instance, _binder.GetInstance<ITestService>(StrongTypedKeys.Primary));
            //未命名槽位不受影响：名字是在同一个 Type 键下的第二层
            Assert.IsNull(_binder.GetBinding<ITestService>());
        }

        [Test]
        public void StringNamedBindingResolvesThroughGetInstance()
        {
            _binder.Bind<ITestService>("secondary").To<TestServiceImpl>().ToSingleton();

            var instance = _binder.GetInstance<ITestService>("secondary");

            Assert.IsNotNull(instance);
            Assert.AreSame(instance, _binder.GetInstance<ITestService>("secondary"));
        }

        [Test]
        public void NamedBindingsAreVisibleToNamedInjection()
        {
            //两种写法都进消费者，确认新入口与 ToName(...) 落在同一个名字槽位：
            //绑定的是枚举，注入侧写 [Inject(StrongTypedKeys.Primary)] 必须命中。
            _binder.Bind<ITestService>(StrongTypedKeys.Primary).To<TestServiceImpl>().ToSingleton();
            _binder.Bind<ITestService>("secondary").To<TestServiceImpl>().ToSingleton();
            _binder.Bind<NamedServiceConsumer>().ToSingleton();

            var consumer = _binder.GetInstance<NamedServiceConsumer>();

            Assert.IsNotNull(consumer.primary, "枚举名绑定应能被同名的 [Inject] 命中");
            Assert.IsNotNull(consumer.secondary, "字符串名绑定应能被同名的 [Inject] 命中");
            Assert.AreNotSame(consumer.primary, consumer.secondary, "两个名字槽位应是各自的单例");
        }

        // ---- InjectionBinder：键值成对入口 ----

        [Test]
        public void KeyValuePairEntryMatchesTheChainedForm()
        {
            var pairBinder = new InjectionBinder();
            var chainedBinder = new InjectionBinder();

            //顺序有意为之：单例在首次 GetInstance 时由 InjectorFactory.SingletonOf 把绑定值
            //从 Type 换成实例本身（SetValue），所以"绑定结构"的对照必须排在实例化之前。
            var pairBinding = pairBinder.Bind<ITestService, TestServiceImpl>().ToSingleton();
            var chainedBinding = chainedBinder.Bind<ITestService>().To<TestServiceImpl>().ToSingleton();

            //成对入口等价于链式写法：同一键、同一值
            Assert.AreEqual(chainedBinding.key, pairBinding.key);
            Assert.AreEqual(FirstValue(chainedBinding), FirstValue(pairBinding));

            Assert.IsInstanceOf<TestServiceImpl>(pairBinder.GetInstance<ITestService>());
            Assert.AreSame(
                pairBinder.GetInstance<ITestService>(),
                pairBinder.GetInstance<ITestService>(),
                "ToSingleton 同样生效：两次取用同一实例");
        }

        [Test]
        public void KeyValuePairEntryAcceptsNames()
        {
            _binder.Bind<ITestService, TestServiceImpl>(StrongTypedKeys.Primary).ToSingleton();
            _binder.Bind<ITestService, TestServiceImpl>("secondary").ToSingleton();

            Assert.IsNotNull(_binder.GetInstance<ITestService>(StrongTypedKeys.Primary));
            Assert.IsNotNull(_binder.GetInstance<ITestService>("secondary"));
            Assert.AreNotSame(
                _binder.GetInstance<ITestService>(StrongTypedKeys.Primary),
                _binder.GetInstance<ITestService>("secondary"));
        }

        // ---- MediationBinder / CommandBinder：成对入口 ----

        [Test]
        public void MediationPairEntryMatchesToMediatorForm()
        {
            var mediationBinder = new MediationBinder();

            var binding = mediationBinder.Bind<PairEntryView, PairEntryMediator>();

            Assert.IsNotNull(binding);
            Assert.AreEqual(typeof(PairEntryView), binding.key);
            Assert.AreEqual(typeof(PairEntryMediator), FirstValue(binding));
        }

        [Test]
        public void CommandPairEntryExecutesTheBoundCommand()
        {
            var injectionBinder = new InjectionBinder();
            var commandBinder = new CommandBinder();
            injectionBinder.Bind<IInjectionBinder>().ToValue(injectionBinder);
            injectionBinder.Bind<ICommandBinder>().ToValue(commandBinder);
            injectionBinder.Bind<IInstanceProvider>().ToValue(injectionBinder);
            commandBinder.injectionBinder = injectionBinder;
            injectionBinder.Bind<TestSignal>().ToSingleton();

            commandBinder.Bind<TestSignal, TestCommand>();
            injectionBinder.GetInstance<TestSignal>().Dispatch();

            Assert.AreEqual(1, TestCommand.ExecutionCount);
        }

        private static object FirstValue(IBinding binding)
        {
            return binding.value is object[] values ? values[0] : binding.value;
        }
    }
}
