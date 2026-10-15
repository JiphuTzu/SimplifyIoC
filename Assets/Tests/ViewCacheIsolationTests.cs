// 3.2：ViewCache 多 Context 隔离回归测试。
// 钉住的行为：早期视图缓存由 static 全局 SemiBinding 改为每 Context 实例一份——
// 一个 Context 缓存的 View 不会被另一个 Context 的 Mediate/Dispose 影响，也不会跨实例泄漏。
using System.Collections.Generic;
using NUnit.Framework;
using SimplifyIoC.Contexts;
using SimplifyIoC.Mediations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SimplifyIoC.Tests
{
    public class ViewCacheIsolationTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();

        //锚点是进程级静态槽（Context.rootContext，2026-10-15 恢复 firstContext 语义）：
        //SetUp/TearDown 都要清，否则本类里"后构造的 Context 自动挂到先构造的那个之下"
        //会与其它用例的残留状态纠缠。

        [SetUp]
        public void SetUp()
        {
            Context.rootContext = null;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _objects.Clear();
            Context.rootContext = null;
        }

        [Test]
        public void EachContextCachesItsOwnEarlyViews()
        {
            var contextA = CreateContext();
            var contextB = CreateContext();

            contextA.CacheEarlyView(CreateView());
            contextA.CacheEarlyView(CreateView());
            contextB.CacheEarlyView(CreateView());

            Assert.That(contextA.ExposedViewCache.Count, Is.EqualTo(2));
            Assert.That(contextB.ExposedViewCache.Count, Is.EqualTo(1));
        }

        [Test]
        public void DisposingOneContextDoesNotAffectAnother()
        {
            //2026-10-15：三个 Context 依次构造、都没有显式 parent → 后两个自动挂到第一个（锚点）下。
            //级联关系不影响本用例要钉的隔离性：ViewCache 是每 Context 实例一份，
            //Dispose 一个不会动到另一个的缓存（3.2 之前是 static 缓存，才会互相覆盖）。
            var root = CreateContext();
            var contextA = CreateContext();
            var contextB = CreateContext();

            //顺带确认三者确实成链（本用例的前提）
            Assert.That(root.parentContext, Is.Null);
            Assert.That(contextA.parentContext, Is.SameAs(root));
            Assert.That(contextB.parentContext, Is.SameAs(root));

            contextA.CacheEarlyView(CreateView());
            contextB.CacheEarlyView(CreateView());

            contextA.Dispose();

            //3.2 之前：static 缓存被 A 清空，B 的视图随之丢失
            Assert.That(contextA.ExposedViewCache.Count, Is.EqualTo(0));
            Assert.That(contextB.ExposedViewCache.Count, Is.EqualTo(1));
            Assert.That(root.ExposedViewCache.Count, Is.EqualTo(0));
        }

        [Test]
        public void DisposeClearsRemainingCachedViews()
        {
            var context = CreateContext();
            context.CacheEarlyView(CreateView());
            context.CacheEarlyView(CreateView());

            context.Dispose();

            Assert.That(context.ExposedViewCache.Count, Is.EqualTo(0));
        }

        private CacheProbeContext CreateContext()
        {
            var go = new GameObject("ViewCacheIsolationTests.Bootstrap");
            _objects.Add(go);
            return new CacheProbeContext(go.AddComponent<Bootstrap>());
        }

        private ProbeView CreateView()
        {
            var go = new GameObject("ViewCacheIsolationTests.View");
            _objects.Add(go);
            return go.AddComponent<ProbeView>();
        }

        private sealed class CacheProbeContext : Context
        {
            public CacheProbeContext(Bootstrap view) : base(view) { }

            public ViewCache ExposedViewCache => _viewCache;
            //本夹具要测的是"缓存中的早期视图"本身（ViewCache 的隔离与随 Dispose 清空）。
            //ContextStartupFlags 移除后构造期即完成映射，AddView 不再走缓存分支，
            //故直接写入 _viewCache 复现缓存状态（原缓存分支的效果就是这一行）。
            public void CacheEarlyView(View view) => _viewCache.Add(view);
        }

        /// 不自动向 Context 注册，测试里显式调用 CacheView，避免依赖 Awake 触发时机
        private sealed class ProbeView : View
        {
            public ProbeView()
            {
                autoRegisterWithContext = false;
                requiresContext = false;
            }
        }
    }
}
