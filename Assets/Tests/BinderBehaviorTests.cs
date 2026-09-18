// Binder 行为快照测试：钉住 Framework.Binder 当前的绑定/冲突消解/移除语义。
// 已知缺陷相关断言放在 KnownDefectsTests（Ignore 状态），此处只测"现状确实如此"的行为。
using System;
using NUnit.Framework;
using SimplifyIoC.Framework;

namespace SimplifyIoC.Tests
{
    public class BinderBehaviorTests
    {
        private Binder _binder;

        [SetUp]
        public void SetUp()
        {
            _binder = new Binder();
        }

        [Test]
        public void BindAndGetBindingResolvesValueList()
        {
            _binder.Bind("myKey").To("aValue");

            var binding = _binder.GetBinding("myKey");

            Assert.IsNotNull(binding);
            // 约束为 Many，value 是 object[]
            var values = binding.value as object[];
            Assert.IsNotNull(values);
            Assert.AreEqual(1, values.Length);
            Assert.AreEqual("aValue", values[0]);
        }

        [Test]
        public void MultipleToCallsAccumulateValues()
        {
            _binder.Bind("myKey").To("a").To("b");

            var values = _binder.GetBinding("myKey").value as object[];

            Assert.AreEqual(2, values.Length);
            Assert.Contains("a", values);
            Assert.Contains("b", values);
        }

        [Test]
        public void NamedBindingIsStoredUnderItsName()
        {
            _binder.Bind("k2").To("a").ToName("nm");

            Assert.IsNull(_binder.GetBinding("k2"), "ToName 后绑定应从默认 NULL_BINDING 槽位迁走");
            Assert.IsNotNull(_binder.GetBinding("k2", "nm"));
        }

        [Test]
        public void GetBindingReturnsNullForUnknownKey()
        {
            Assert.IsNull(_binder.GetBinding("nope"));
        }

        [Test]
        public void UnbindRemovesBinding()
        {
            _binder.Bind("myKey").To("a");
            Assert.IsNotNull(_binder.GetBinding("myKey"));

            _binder.Unbind("myKey");

            Assert.IsNull(_binder.GetBinding("myKey"));
        }

        [Test]
        public void UnbindUnknownKeyIsSilentNoop()
        {
            Assert.DoesNotThrow(() => _binder.Unbind("nope"));
        }

        // ---- 冲突消解 ----

        [Test]
        public void SameKeyTwoBindingsRegistersConflictAndGetBindingThrows()
        {
            _binder.Bind("ck").To("a");
            _binder.Bind("ck").To("b"); // 同名（NULL_BINDING）非 weak → 冲突

            Assert.Throws<Exception>(() => _binder.GetBinding("ck"),
                "存在未消解冲突时 GetBinding 必须抛异常");
        }

        [Test]
        public void NamingTheSecondBindingClearsTheConflict()
        {
            _binder.Bind("ck").To("a");
            _binder.Bind("ck").To("b").ToName("second");

            Assert.DoesNotThrow(() => _binder.GetBinding("ck"));
            Assert.IsNotNull(_binder.GetBinding("ck", "second"));
            Assert.IsNull(_binder.GetBinding("ck", "nonexistent"));
        }

        // ---- weak 绑定语义（现状快照）----

        [Test]
        public void WeakBindingDoesNotReplaceExistingStrongBinding()
        {
            // 注意顺序：resolver 在 To() 内立即触发，Weak() 必须写在 To() 之前，
            // 否则解析发生时 isWeak 仍为 false，会走强绑定冲突路径（见下一条测试）。
            _binder.Bind("wk").To("strong");
            _binder.Bind("wk").Weak().To("weak");

            var values = _binder.GetBinding("wk").value as object[];
            Assert.AreEqual("strong", values[0],
                "现状：strong 存在时，后续 weak 绑定被忽略（TryAdd 失败即静默丢弃）");
        }

        [Test]
        public void WeakCalledAfterToLeavesPermanentConflict()
        {
            // 顺序陷阱快照：已有强绑定时，To() 触发 resolver 的瞬间 isWeak 尚未设置
            // → 按双强绑定注册冲突；之后的 Weak() 不会重新解析，
            // 冲突永远无法消解，本 Binder 之后所有 GetBinding 都抛异常。
            _binder.Bind("wk").To("strong");
            _binder.Bind("wk").To("weak").Weak();

            Assert.Throws<Exception>(() => _binder.GetBinding("wk"),
                "现状快照：.To(x).Weak() 顺序会留下永久冲突（潜在易用性缺陷，暂不修）");
        }

        [Test]
        public void StrongBindingReplacesExistingWeakBinding()
        {
            _binder.Bind("wk").Weak().To("weak");
            _binder.Bind("wk").To("strong");

            var values = _binder.GetBinding("wk").value as object[];
            Assert.AreEqual("strong", values[0],
                "现状：weak 可被非 weak 绑定覆盖");
        }

        // ---- 多 key 绑定 ----

        [Test]
        public void BindingAcceptsMultipleKeysViaKeyConstraint()
        {
            // Plain Binding 的 keyConstraint 是 One；显式改为 Many 后可挂多 key
            var binding = _binder.Bind("k1");
            binding.keyConstraint = BindingConstraintType.Many;
            binding.Bind("k2");
            binding.To("shared");

            Assert.IsNotNull(_binder.GetBinding("k1"));
            Assert.IsNotNull(_binder.GetBinding("k2"));
        }

        // ---- RemoveValue 现状快照（已知缺陷 P0#2）----
        // 阶段 1 修复 RemoveValue 后，本测试会失败——届时删除本测试，
        // 并启用 KnownDefectsTests.RemoveValueShouldRemoveValueFromBinding。

        [Test]
        public void RemoveValueCurrentBehaviorSnapshotDoesNotRemove()
        {
            var binding = _binder.Bind("rk").To("a").To("b");

            _binder.RemoveValue(binding, "a");

            var values = _binder.GetBinding("rk").value as object[];
            Assert.AreEqual(new[] { "a", "b" }, values,
                "现状快照：RemoveValue 因条件写反而什么都不删（P0#2）");
        }
    }
}
