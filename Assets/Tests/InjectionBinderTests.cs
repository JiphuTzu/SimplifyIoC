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
    }
}
