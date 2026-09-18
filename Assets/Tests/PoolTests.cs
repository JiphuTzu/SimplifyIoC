// Pool 行为快照测试。
using System;
using NUnit.Framework;
using SimplifyIoC.Injectors;
using SimplifyIoC.Pools;

namespace SimplifyIoC.Tests
{
    public class PoolTests
    {
        private InjectionBinder _provider;

        [SetUp]
        public void SetUp()
        {
            _provider = new InjectionBinder();
            // 必须给出 To：绑定只在 To()/ToName() 触发 resolver 后才真正入库，
            // 只 Bind 不 To 的绑定 GetBinding 查不到（GetInstance 会报 no binding）。
            _provider.Bind<PooledThing>().To<PooledThing>();
        }

        [Test]
        public void FixedSizePoolCreatesAllInstancesOnFirstRequest()
        {
            var pool = new Pool<PooledThing> { instanceProvider = _provider, size = 2 };

            var a = pool.GetInstance();

            Assert.IsNotNull(a);
            Assert.AreEqual(2, pool.instanceCount, "首次取用应一次性灌满 size");
            Assert.AreEqual(1, pool.available);
        }

        [Test]
        public void ReturnInstanceRecyclesAndCallsRestore()
        {
            var pool = new Pool<PooledThing> { instanceProvider = _provider, size = 1 };
            var a = pool.GetInstance();

            pool.ReturnInstance(a);

            Assert.IsTrue(((PooledThing)a).restored, "归还时应调用 IPoolable.Restore");
            Assert.AreEqual(1, pool.available);

            var b = pool.GetInstance();
            Assert.AreSame(a, b, "回收后应复用同一实例");
        }

        [Test]
        public void ReturningUnknownInstanceIsIgnored()
        {
            var pool = new Pool<PooledThing> { instanceProvider = _provider, size = 1 };

            Assert.DoesNotThrow(() => pool.ReturnInstance(new PooledThing()));
            Assert.AreEqual(0, pool.available);
        }

        [Test]
        public void OverflowThrowsWhenConfiguredTo()
        {
            var pool = new Pool<PooledThing> { instanceProvider = _provider, size = 1 };

            pool.GetInstance();

            Assert.Throws<Exception>(() => pool.GetInstance());
        }

        [Test]
        public void ZeroSizePoolInflates()
        {
            var pool = new Pool<PooledThing> { instanceProvider = _provider, size = 0 };

            var a = pool.GetInstance();

            Assert.IsNotNull(a);
            Assert.AreEqual(1, pool.instanceCount);
        }

        [Test]
        public void EmptyPoolWithoutProviderThrows()
        {
            var pool = new Pool<PooledThing> { size = 1 };

            Assert.Throws<Exception>(() => pool.GetInstance(),
                "无 instanceProvider 时应显式报错");
        }

        [Test]
        public void PoolTypeMismatchThrowsOnAdd()
        {
            var pool = new Pool<PooledThing>();

            Assert.Throws<Exception>(() => pool.Add("不是 PooledThing"));
        }
    }
}
