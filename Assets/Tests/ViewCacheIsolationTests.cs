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

        //5.6：不再需要在 SetUp/TearDown 里重置全局 Context.firstContext——
        //Context 的归属只由 GameObject 层级或显式 parent 决定，测试之间没有任何共享状态。

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _objects.Clear();
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
            //5.6：三个 Context 各自挂在独立的根 GameObject 上 → 彼此都是独立链根，互不隶属。
            //这比原先"都挂在静态 firstContext 下面当兄弟"更能说明隔离性：
            //连父子链都不存在，Dispose 一个也不会波及另一个。
            var root = CreateContext();
            var contextA = CreateContext();
            var contextB = CreateContext();

            //顺带确认三者互不隶属（本用例的前提）
            Assert.That(contextA.parentContext, Is.Null);
            Assert.That(contextB.parentContext, Is.Null);
            Assert.That(root.parentContext, Is.Null);

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
            //ManualMapping：ctor 不调用 Start() → mediationBinder 为 null，
            //AddView/CacheView 走缓存分支（正是早期视图的路径）
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
            public CacheProbeContext(Bootstrap view) : base(view, ContextStartupFlags.ManualMapping) { }

            public ViewCache ExposedViewCache => _viewCache;
            public void CacheEarlyView(View view) => CacheView(view);
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
