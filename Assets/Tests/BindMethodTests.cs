// 4.4：[BindMethod] 静态注册表治理 + 接入自动解析的回归测试。
// 钉住的行为：
//  1) 自动解析后 [BindMethod] 标注的方法可被 InvokeBind 调到；
//  2) 重复解析不会让同一方法被调用两次；
//  3) 同类型多实例互不串扰（注册表以实例为键）；
//  4) UnbindMethods() 立即摘除条目（View.OnDestroy 的即时释放路径）；
//  5) 注册表以弱引用持有组件，且对"已销毁（伪 null）"的组件仍能定向摘除条目。
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using SimplifyIoC.Mediations;
using SimplifyIoC.Utils;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SimplifyIoC.Tests
{
    public class BindMethodTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
            {
                if (go != null) Object.DestroyImmediate(go);
            }

            _objects.Clear();
        }

        // ---- 自动解析 ----

        [Test]
        public void BindMethodIsCallableAfterAutoParse()
        {
            var view = NewView<BindMethodView>();

            view.EnsureAttributesInitialized();
            view.InvokeBind("hit", 7);

            Assert.That(view.hits, Is.EqualTo(new[] { 7 }),
                "[BindMethod] 接入自动解析后，标注方法应能被 InvokeBind 调到");
        }

        [Test]
        public void ReparseDoesNotDoubleInvoke()
        {
            var view = NewView<BindMethodView>();

            view.EnsureAttributesInitialized();
            view.EnsureAttributesInitialized();

            view.InvokeBind("hit", 1);

            Assert.That(view.hits.Count, Is.EqualTo(1), "重复解析不得把同一 (目标, 名称) 登记两次");
        }

        [Test]
        public void MultipleInstancesOfSameTypeAreIsolated()
        {
            var a = NewView<BindMethodView>();
            var b = NewView<BindMethodView>();

            a.EnsureAttributesInitialized();
            b.EnsureAttributesInitialized();
            a.InvokeBind("hit", 1);
            b.InvokeBind("hit", 2);

            Assert.That(a.hits, Is.EqualTo(new[] { 1 }));
            Assert.That(b.hits, Is.EqualTo(new[] { 2 }));
        }

        // ---- 释放 ----

        [Test]
        public void UnbindMethodsRemovesTheEntryImmediately()
        {
            var view = NewView<BindMethodView>();
            view.EnsureAttributesInitialized();

            Assert.That(HasRegistryEntry(view), Is.True);

            view.UnbindMethods();

            Assert.That(HasRegistryEntry(view), Is.False);
        }

        [Test]
        public void ViewOnDestroyReleasesItsRegistryEntry()
        {
            var go = new GameObject("BindMethodTests.DestroyedView");
            var view = go.AddComponent<BindMethodView>();
            view.EnsureAttributesInitialized();
            Assert.That(HasRegistryEntry(view), Is.True);

            //编辑模式下不可依赖引擎为未标 [ExecuteInEditMode] 的脚本派发生命周期消息
            //（本工程既有结论，同类注释见 MediationScopeTests）。故这里显式派发一次 OnDestroy，
            //钉住"View.OnDestroy → UnbindMethods"这条释放链本身；真实销毁序列由 PlayMode / 示例工程覆盖。
            //UnbindMethods 幂等：即便引擎在 DestroyImmediate 时也派发过一次，重复调用无副作用。
            InvokeLifecycle(view, "OnDestroy");

            Assert.That(HasRegistryEntry(view), Is.False, "View 销毁应立即摘除 [BindMethod] 条目");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void RegistryHoldsComponentsWeaklyAndClearsDestroyedOnes()
        {
            //(1) 结构性契约：注册表必须以弱键持有组件。
            //旧实现 static Dictionary<Component,…> 既让条目在组件销毁后永久残留，又反向强引用组件。
            var fieldType = RegistryField.FieldType;
            Assert.That(fieldType.IsGenericType
                        && fieldType.GetGenericTypeDefinition() == typeof(ConditionalWeakTable<,>), Is.True,
                "注册表必须以弱引用持有组件，否则组件销毁后条目永久残留");

            //(2) 行为契约：对"已销毁（伪 null）"的组件，UnbindMethods 仍能定向摘除条目。
            //这是 View.OnDestroy 释放链成立的前提——若 UnbindMethods 用 GameObject 重载的 == 判空，
            //销毁期会提前 return，条目静默残留（此时没有任何报错，最难查）。
            var go = new GameObject("BindMethodTests.DestroyedTarget");
            var target = go.AddComponent<PlainBindTarget>();
            //手动解析路径（与本类夹具不同，这里刻意不用 View，避免 OnDestroy 钩子干扰本用例）
            target.AddAttributeParser(target.GetBindMethodParser()).ParseAttributes();
            Assert.That(HasRegistryEntry(target), Is.True);

            Object.DestroyImmediate(go);
            target.UnbindMethods();

            Assert.That(HasRegistryEntry(target), Is.False, "对已销毁组件的定向清理必须生效");

            //注：此处刻意不做 WeakReference + GC 的"可回收"断言。编辑模式下 Unity 会把已销毁对象的
            //托管包装器留在可达集合里（编辑器/测试运行器的引用，与注册表无关），断言必然假红。
            //注册表本身的可回收性由 B1 的 Memory Profiler 实测验收（见 phase23-breakdown 的 3.6 章节）。
        }

        // ---- 辅助 ----

        private T NewView<T>() where T : View
        {
            var go = new GameObject(typeof(T).Name);
            _objects.Add(go);
            return go.AddComponent<T>();
        }

        private static readonly FieldInfo RegistryField =
            typeof(BindMethodExtension).GetField("_methods", BindingFlags.Static | BindingFlags.NonPublic);

        /// <summary>
        /// 显式派发一个 Unity 生命周期消息（编辑模式下引擎不保证为未标 [ExecuteInEditMode]
        /// 的脚本派发 Awake/OnDestroy，测试必须自己触发才能钉住回调体本身）。
        /// </summary>
        private static void InvokeLifecycle(object target, string message)
        {
            var method = target.GetType().GetMethod(message, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"未找到生命周期方法 {message}");
            method.Invoke(target, null);
        }

        /// <summary>
        /// 反射探测注册表是否仍含该目标。注册表类型已由 Dictionary 换为 ConditionalWeakTable，
        /// 二者都有 TryGetValue，故探测方式与实现细节解耦。
        /// </summary>
        private static bool HasRegistryEntry(Component target)
        {
            var table = RegistryField.GetValue(null);
            var tryGetValue = table.GetType().GetMethod("TryGetValue");
            var args = new object[] { target, null };
            return (bool)tryGetValue.Invoke(table, args);
        }

        // ---- 夹具 ----

        private sealed class BindMethodView : View
        {
            public readonly List<int> hits = new List<int>();

            public BindMethodView()
            {
                autoRegisterWithContext = false;
                requiresContext = false;
            }

            [BindMethod("hit")]
            private void OnHit(int n) => hits.Add(n);
        }

        /// 非 View 的普通组件：用于验证"注册表不构成静态强引用"（不受 View.OnDestroy 钩子影响）
        private sealed class PlainBindTarget : MonoBehaviour
        {
            [BindMethod("hit")]
            private void OnHit(int n) { }
        }
    }
}
