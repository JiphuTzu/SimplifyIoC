// 已知缺陷的回归测试（阶段 0 状态：Ignore）。
//
// 原则：这些测试断言的是【正确行为】，但当前代码不满足，因此全部 Ignore。
// 阶段 1 修复对应缺陷时，移除对应测试的 Ignore 属性，测试转绿即视为修复完成。
// 每条 Ignore 理由都标注了 review 文档里的缺陷编号：
//   见 .workbuddy/review/code-review-2026-09-18.md（P0 清单）。
using System;
using NUnit.Framework;
using SimplifyIoC.Framework;
using SimplifyIoC.Injectors;

namespace SimplifyIoC.Tests
{
    public class KnownDefectsTests
    {
        private InjectionBinder _binder;

        [SetUp]
        public void SetUp()
        {
            _binder = new InjectionBinder();
            TestServiceImpl.InstanceCount = 0;
        }

        // ---- P0 #2：Binder.RemoveValue 判断反向 ----
        // 现状：绑定存在时直接 return（删不掉）；不存在时抛 KeyNotFoundException。
        // 另有对应"现状快照"测试见 BinderBehaviorTests.RemoveValueCurrentBehaviorSnapshot*。
        [Test]
        [Ignore("P0#2 RemoveValue 判断写反 —— 阶段 1 修复后移除 Ignore")]
        public void RemoveValueShouldRemoveValueFromBinding()
        {
            var binder = new Framework.Binder();
            var binding = binder.Bind("rk").To("a").To("b");

            binder.RemoveValue(binding, "a");

            var values = binder.GetBinding("rk").value as object[];
            Assert.AreEqual(new[] { "b" }, values, "RemoveValue 应移除指定值");
        }

        // ---- P0 #5：循环依赖保护失效 ----
        // 现状：`_infinityLock` 在递归内层返回时被清空，环形依赖会一直递归到 StackOverflow
        //（StackOverflowException 无法被测试进程捕获，故不能测现状，只能测预期）。
        [Test]
        [Ignore("P0#5 循环依赖锁失效，当前会栈溢出 —— 修复后应改为抛出普通 Exception")]
        public void CircularDependencyShouldThrowExceptionNotStackOverflow()
        {
            _binder.Bind<DepA>();
            _binder.Bind<DepB>();

            Assert.Throws<Exception>(() => _binder.GetInstance<DepA>(),
                "环形依赖应在限次后被检测到并抛出可捕获的异常");
        }

        // ---- P0 #7：InjectorFactory 裸 catch 吞异常 ----
        // 现状：构造函数抛异常被静默转为 null，GetInstance 返回 null。
        [Test]
        [Ignore("P0#7 工厂吞异常 —— 修复后构造失败应上抛")]
        public void ConstructorFailureShouldSurfaceException()
        {
            _binder.Bind<Boom>();

            Assert.Throws<Exception>(() => _binder.GetInstance<Boom>(),
                "构造异常不应被静默吞掉变成 null");
        }

        // ---- P0 #8：PostConstruct 排序取 [0] 属性 ----
        // 现状：方法上 [ListensTo] 写在 [PostConstruct] 之前时，
        // PriorityComparer.GetPriority 用 GetCustomAttributes[0] 拿到的是 ListensTo，
        // as PostConstruct 为 null → 访问 priority 时 NRE（发生在 MapMethods 的 Sort 中）。
        [Test]
        [Ignore("P0#8 属性序位 NRE —— 改用 GetCustomAttribute<PostConstruct>() 后移除 Ignore")]
        public void PostConstructWithOtherAttributeFirstShouldStillWork()
        {
            _binder.Bind<MixedAttrConsumer>().ToSingleton();

            var consumer = _binder.GetInstance<MixedAttrConsumer>();

            Assert.IsTrue(consumer.postConstructed);
        }
    }

    // ---- 仅被 KnownDefectsTests 使用的类型 ----

    public class MixedAttrConsumer
    {
        public bool postConstructed;

        // 注意顺序：[ListensTo] 必须写在 [PostConstruct] 之前——这正是 P0#8 的触发条件
        //（GetCustomAttributes(true)[0] 取到 ListensTo，as PostConstruct 得 null）。
        [ListensTo(typeof(TestSignal))]
        [PostConstruct]
        public void OnInit()
        {
            postConstructed = true;
        }
    }
}
