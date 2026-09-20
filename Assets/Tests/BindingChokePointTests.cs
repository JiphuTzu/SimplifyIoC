// 5.2：Binding 单一收口（single choke point）的回归测试。
//
// 为什么这样改：本机实测 netstandard2.1 下 Roslyn 报 CS8830（目标运行时不支持协变返回），
// 派生类无法把返回类型由 IBinding 收窄成 ICommandBinding 的同时 override，
// 因此 `<T>` / `<object>` 两组外观只能继续用 new 隐藏。既然 new 消不掉，就让它无害：
// 所有逻辑集中到 Binding 的 BindCore / ToCore / ToNameCore / NamedCore，
// 派生类要加语义只覆写这几个 Core，于是"每多一个入口签名就要再抄一遍守卫"的遗漏不可能再发生
// ——4.3 的 ToHandler 互斥守卫正是被 To<T>() 这样一个漏网入口绕过的。
//
// 钉住的行为：
//  1) 四个 Core 各自被 <T> 与 <object> 两个入口命中；
//  2) CommandBinding 的 ToHandler 互斥守卫：无论走 <T> / <object>，
//     也无论静态类型是 CommandBinding 还是基类 Binding，一律抛异常（改前基类静态类型的调用可以静默通过）。
using System;
using System.Collections.Generic;
using NUnit.Framework;
using SimplifyIoC.Commands;
using SimplifyIoC.Framework;

namespace SimplifyIoC.Tests
{
    public class BindingChokePointTests
    {
        [Test]
        public void BindEntriesAllReachBindCore()
        {
            var binding = new ProbeBinding();

            binding.Bind<ProbeFixture>();
            binding.Bind(new ProbeFixture());

            Assert.That(binding.hits, Is.EqualTo(new[] { "BindCore", "BindCore" }),
                "Bind<T>() 与 Bind(object) 必须都走 BindCore");
        }

        [Test]
        public void ToEntriesAllReachToCore()
        {
            var binding = new ProbeBinding();

            binding.To<ProbeFixture>();
            binding.To(new ProbeFixture());

            Assert.That(binding.hits, Is.EqualTo(new[] { "ToCore", "ToCore" }),
                "To<T>() 与 To(object) 必须都走 ToCore");
        }

        [Test]
        public void ToNameEntriesAllReachToNameCore()
        {
            var binding = new ProbeBinding();

            binding.ToName<ProbeFixture>();
            binding.ToName("named");

            Assert.That(binding.hits, Is.EqualTo(new[] { "ToNameCore", "ToNameCore" }),
                "ToName<T>() 与 ToName(object) 必须都走 ToNameCore");
        }

        [Test]
        public void NamedEntriesAllReachNamedCore()
        {
            var binding = new ProbeBinding();

            binding.Named<ProbeFixture>();
            binding.Named("named");

            Assert.That(binding.hits, Is.EqualTo(new[] { "NamedCore", "NamedCore" }),
                "Named<T>() 与 Named(object) 必须都走 NamedCore");
        }

        [Test]
        public void ToCoreSemanticsAreUnchanged()
        {
            //收口不能改写语义：To 要落值并触发 resolver
            var resolverCalls = 0;
            Binder.BindingResolver resolver = _ => resolverCalls++;
            var binding = new ProbeBinding(resolver);

            binding.To(new ProbeFixture());

            Assert.That(binding.value, Is.Not.Null, "To(object) 必须把值写进 value");
            Assert.That(resolverCalls, Is.EqualTo(1), "To(object) 必须通知 resolver");
        }

        // ---- CommandBinding：守卫收敛后不再有漏网入口 ----

        private static CommandBinding NewGuardedCommandBinding()
        {
            var binding = new CommandBinding();
            binding.ToHandler<ProbeFixture>(service => { });
            return binding;
        }

        [Test]
        public void CommandValueEntriesAreBlockedAfterToHandler()
        {
            var binding = NewGuardedCommandBinding();

            Assert.Throws<Exception>(() => binding.To<ProbeFixture>(), "To<T>() 必须被拒");
            Assert.Throws<Exception>(() => binding.To(new ProbeFixture()), "To(object) 必须被拒");
        }

        [Test]
        public void CommandNameEntriesAreBlockedAfterToHandler()
        {
            var binding = NewGuardedCommandBinding();

            Assert.Throws<Exception>(() => binding.ToName<ProbeFixture>(), "ToName<T>() 必须被拒");
            Assert.Throws<Exception>(() => binding.ToName("named"), "ToName(object) 必须被拒");
        }

        [Test]
        public void GuardsHoldEvenWhenCalledThroughTheBaseType()
        {
            //改前守卫只写在 CommandBinding 的 new 外观上，用基类静态类型调用会被静态绑定到
            //Binding.To(object) / Binding.ToName(object)，那条路完全没有守卫，
            //于是产出一个"既有 handler 又有命令值"的半配置绑定——正是 4.3 翻车的形状。
            Binding asBase = NewGuardedCommandBinding();

            Assert.Throws<Exception>(() => asBase.To(new ProbeFixture()),
                "基类静态类型的 To(object) 也必须经过 CommandBinding 的守卫");
            Assert.Throws<Exception>(() => asBase.ToName("named"),
                "基类静态类型的 ToName(object) 也必须经过 CommandBinding 的守卫");
        }

        [Test]
        public void NonConflictingentriesStillWorkAfterToHandler()
        {
            //反向钉一只：收口不能过宽——Named / Weak 这类只读或无关的链式步骤不该被互斥守卫误伤
            var binding = NewGuardedCommandBinding();

            Assert.DoesNotThrow(() => binding.Weak(), "Weak() 与 handler 不冲突");
            Assert.DoesNotThrow(() => binding.Named("whatever"), "Named(...) 只读判定，不写 name");
        }

        // ---- 夹具 ----

        private sealed class ProbeFixture { }

        private sealed class ProbeBinding : Binding
        {
            public readonly List<string> hits = new List<string>();

            public ProbeBinding() : base() { }

            public ProbeBinding(Binder.BindingResolver resolver) : base(resolver) { }

            protected override IBinding BindCore(object o)
            {
                hits.Add("BindCore");
                return base.BindCore(o);
            }

            protected override IBinding ToCore(object o)
            {
                hits.Add("ToCore");
                return base.ToCore(o);
            }

            protected override IBinding ToNameCore(object o)
            {
                hits.Add("ToNameCore");
                return base.ToNameCore(o);
            }

            protected override IBinding NamedCore(object o)
            {
                hits.Add("NamedCore");
                return base.NamedCore(o);
            }
        }
    }
}
