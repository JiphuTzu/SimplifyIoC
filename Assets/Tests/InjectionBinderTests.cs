// InjectionBinder / Injector 行为快照测试。
using System;
using NUnit.Framework;
using SimplifyIoC.Injectors;

namespace SimplifyIoC.Tests
{
    public class InjectionBinderTests
    {
        private InjectionBinder _binder;

        [SetUp]
        public void SetUp()
        {
            _binder = new InjectionBinder();
            TestServiceImpl.InstanceCount = 0;
        }

        // ---- 生命周期类型 ----

        [Test]
        public void DefaultBindingCreatesNewInstanceEachTime()
        {
            _binder.Bind<ITestService>().To<TestServiceImpl>();

            var a = _binder.GetInstance<ITestService>();
            var b = _binder.GetInstance<ITestService>();

            Assert.AreNotSame(a, b);
            Assert.AreEqual(2, TestServiceImpl.InstanceCount);
        }

        [Test]
        public void ToSingletonReturnsSameInstance()
        {
            _binder.Bind<ITestService>().To<TestServiceImpl>().ToSingleton();

            var a = _binder.GetInstance<ITestService>();
            var b = _binder.GetInstance<ITestService>();

            Assert.AreSame(a, b);
            Assert.AreEqual(1, TestServiceImpl.InstanceCount);
        }

        [Test]
        public void ToValueReturnsTheExactValueInstance()
        {
            var value = new TestServiceImpl();
            _binder.Bind<ITestService>().ToValue(value);

            Assert.AreSame(value, _binder.GetInstance<ITestService>());
            Assert.AreSame(value, _binder.GetInstance<ITestService>());
        }

        [Test]
        public void NamedBindingResolvesOnlyViaThatName()
        {
            _binder.Bind<ITestService>().To<TestServiceImpl>().ToSingleton().ToName("a");

            var a = _binder.GetInstance<ITestService>("a");

            Assert.IsNotNull(a);
            Assert.AreEqual(1, TestServiceImpl.InstanceCount);
        }

        // ---- 错误路径 ----

        [Test]
        public void GetInstanceForUnboundTypeThrows()
        {
            Assert.Throws<Exception>(() => _binder.GetInstance<ITestService>());
        }

        [Test]
        public void GetInstanceWithIgnoreExceptionReturnsNull()
        {
            Assert.IsNull(_binder.GetInstance(typeof(ITestService), true));
        }

        [Test]
        public void SetValueWithIncompatibleTypeThrows()
        {
            Assert.Throws<Exception>(() =>
                _binder.Bind<ITestService>().ToValue("不是 ITestService 的值"));
        }

        [Test]
        public void GetInstanceOfInterfaceWithoutToThrows()
        {
            // 只 Bind 了 key 没有 To：工厂无法实例化接口
            _binder.Bind<ITestService>();
            Assert.Throws<Exception>(() => _binder.GetInstance<ITestService>());
        }

        // ---- 注入行为 ----

        [Test]
        public void SetterInjectionSatisfiesInjectProperty()
        {
            _binder.Bind<ITestService>().To<TestServiceImpl>().ToSingleton();
            _binder.Bind<ServiceConsumer>().ToSingleton();

            var consumer = _binder.GetInstance<ServiceConsumer>();

            Assert.IsNotNull(consumer.service);
            Assert.AreEqual("impl", consumer.service.Name);
        }

        [Test]
        public void ConstructorInjectionSatisfiesSingleCtorParameter()
        {
            _binder.Bind<ITestService>().To<TestServiceImpl>().ToSingleton();
            _binder.Bind<ConsumerWithCtor>().ToSingleton();

            var consumer = _binder.GetInstance<ConsumerWithCtor>();

            Assert.IsNotNull(consumer.service);
            Assert.AreEqual("impl", consumer.service.Name);
        }

        [Test]
        public void PostConstructMethodRunsAfterInjection()
        {
            _binder.Bind<ConsumerWithPostConstruct>().ToSingleton();

            var consumer = _binder.GetInstance<ConsumerWithPostConstruct>();

            Assert.IsTrue(consumer.postConstructed);
        }

        [Test]
        public void UnmappedSetterDependencyThrowsWithHelpfulMessage()
        {
            _binder.Bind<ServiceConsumer>().ToSingleton();

            var ex = Assert.Throws<Exception>(() => _binder.GetInstance<ServiceConsumer>());
            StringAssert.Contains("null binding", ex.Message);
        }

        // ---- contextual supply ----

        [Test]
        public void SupplyToProvidesBindingOnlyToTargetType()
        {
            _binder.Bind<ITestService>().To<TestServiceImpl>().SupplyTo<ServiceConsumer>();
            _binder.Bind<ServiceConsumer>().ToSingleton();

            var consumer = _binder.GetInstance<ServiceConsumer>();

            Assert.IsNotNull(consumer.service, "supplier 应向 ServiceConsumer 提供 ITestService");
            Assert.IsNotNull(_binder.GetSupplier(typeof(ITestService), typeof(ServiceConsumer)));
        }

        [Test]
        public void GetSupplierIgnoresUnrelatedTargetType()
        {
            _binder.Bind<ITestService>().To<TestServiceImpl>().SupplyTo<ServiceConsumer>();

            Assert.IsNull(_binder.GetSupplier(typeof(ITestService), typeof(ConsumerWithCtor)));
        }

        // 3.3：供给关系收敛到绑定自身后，Unbind 必须让供应一同失效
        // （原实现 suppliers 是第二份注册表，Unbind 只删 bindings → 这里会查到失效绑定）
        [Test]
        public void UnbindAlsoDropsSupplier()
        {
            _binder.Bind<ITestService>().To<TestServiceImpl>().SupplyTo<ServiceConsumer>();
            _binder.Bind<ServiceConsumer>().ToSingleton();
            Assert.IsNotNull(_binder.GetInstance<ServiceConsumer>().service);

            _binder.Unbind<ITestService>();

            Assert.IsNull(_binder.GetSupplier(typeof(ITestService), typeof(ServiceConsumer)));

            //换一个尚未注入过的消费者，验证供给确实不再生效
            _binder.Unbind<ServiceConsumer>();
            _binder.Bind<ServiceConsumer>().ToSingleton();
            Assert.Throws<Exception>(() => _binder.GetInstance<ServiceConsumer>());
        }

        [Test]
        public void UnsupplyKeepsBindingButDropsPromise()
        {
            _binder.Bind<ITestService>().To<TestServiceImpl>().SupplyTo<ServiceConsumer>();

            _binder.Unsupply<ITestService, ServiceConsumer>();

            Assert.IsNull(_binder.GetSupplier(typeof(ITestService), typeof(ServiceConsumer)));
            Assert.IsNotNull(_binder.GetBinding<ITestService>(), "Unsupply 只解除供给承诺，绑定本身仍在");
        }

        // 3.3：跨域绑定会被 ResolveBinding 转交到根 binder，本地注册表查不到 →
        // GetSupplier 需按"本地优先、跨域次之"回落（与 GetBinding 同策略）
        [Test]
        public void CrossContextSupplierIsFoundThroughRootBinder()
        {
            var root = new CrossContextInjectionBinder();
            var child = new CrossContextInjectionBinder { crossContextBinder = root };

            child.Bind<ITestService>().To<TestServiceImpl>().CrossContext().SupplyTo<ServiceConsumer>();

            Assert.IsNull(root.GetSupplier(typeof(ITestService), typeof(ConsumerWithCtor)));
            Assert.IsNotNull(child.GetSupplier(typeof(ITestService), typeof(ServiceConsumer)));
        }
    }
}
