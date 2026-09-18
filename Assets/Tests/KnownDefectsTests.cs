// 已知缺陷的回归测试。
//
// 阶段 1 已修复 #2/#5/#7/#8 四条，对应测试已启用并转绿。
// 后续新发现的缺陷按同样模式加入：断言【正确行为】，修复前 Ignore，修复后移除 Ignore。
// 缺陷编号对应 .workbuddy/review/code-review-2026-09-18.md（P0 清单）。
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

        // ---- P0 #2：Binder.RemoveValue 判断反向（已修复）----
        [Test]
        public void RemoveValueShouldRemoveValueFromBinding()
        {
            var binder = new Framework.Binder();
            var binding = binder.Bind("rk").To("a").To("b");

            binder.RemoveValue(binding, "a");

            var values = binder.GetBinding("rk").value as object[];
            Assert.AreEqual(new[] { "b" }, values, "RemoveValue 应移除指定值");
        }

        // ---- P0 #5：循环依赖保护失效（已修复：改为递归深度计数，环形依赖在限次后抛可捕获异常）----
        [Test]
        public void CircularDependencyShouldThrowExceptionNotStackOverflow()
        {
            _binder.Bind<DepA>();
            _binder.Bind<DepB>();

            Assert.Throws<Exception>(() => _binder.GetInstance<DepA>(),
                "环形依赖应在限次后被检测到并抛出可捕获的异常");
        }

        // ---- P0 #7：InjectorFactory 裸 catch 吞异常（已修复：异常上抛）----
        [Test]
        public void ConstructorFailureShouldSurfaceException()
        {
            _binder.Bind<Boom>();

            Assert.Throws<Exception>(() => _binder.GetInstance<Boom>(),
                "构造异常不应被静默吞掉变成 null");
        }

        // ---- P0 #8：PostConstruct 排序取 [0] 属性（已修复：GetCustomAttribute<PostConstruct>）----
        [Test]
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
