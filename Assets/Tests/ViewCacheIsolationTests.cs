// 3.2：ViewCache 多 Bootstrap 隔离回归测试。
// 钉住的行为：早期视图缓存由 static 全局 SemiBinding 改为每 Bootstrap 实例一份——
// 一个 Bootstrap 缓存的 View 不会被另一个 Bootstrap 的 Mediate/Dispose 影响，也不会跨实例泄漏。
using System.Collections.Generic;
using NUnit.Framework;
using SimplifyIoC.Bootstraps;
using SimplifyIoC.Mediations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SimplifyIoC.Tests
{
    public class ViewCacheIsolationTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();

        //锚点是进程级静态槽（Bootstrap.root，2026-10-15 恢复 firstContext 语义）：
        //SetUp/TearDown 都要清，否则本类里"后构造的 Bootstrap 自动挂到先构造的那个之下"
        //会与其它用例的残留状态纠缠。

        [SetUp]
        public void SetUp()
        {
            Bootstrap.root = null;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _objects.Clear();
            Bootstrap.root = null;
        }

        [Test]
        public void EachBootstrapCachesItsOwnEarlyViews()
        {
            var contextA = CreateBootstrap();
            var contextB = CreateBootstrap();

            contextA.CacheEarlyView(CreateView());
            contextA.CacheEarlyView(CreateView());
            contextB.CacheEarlyView(CreateView());

            Assert.That(contextA.ExposedViewCache.Count, Is.EqualTo(2));
            Assert.That(contextB.ExposedViewCache.Count, Is.EqualTo(1));
        }

        [Test]
        public void DisposingOneBootstrapDoesNotAffectAnother()
        {
            //2026-10-15：三个 Bootstrap 依次构造、都没有显式 parent → 后两个自动挂到第一个（锚点）下。
            //级联关系不影响本用例要钉的隔离性：ViewCache 是每 Bootstrap 实例一份，
            //Dispose 一个不会动到另一个的缓存（3.2 之前是 static 缓存，才会互相覆盖）。
            var root = CreateBootstrap();
            var contextA = CreateBootstrap();
            var contextB = CreateBootstrap();

            //顺带确认三者确实成链（本用例的前提）
            Assert.That(root.parentBootstrap, Is.Null);
            Assert.That(contextA.parentBootstrap, Is.SameAs(root));
            Assert.That(contextB.parentBootstrap, Is.SameAs(root));

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
            var bootstrap = CreateBootstrap();
            bootstrap.CacheEarlyView(CreateView());
            bootstrap.CacheEarlyView(CreateView());

            bootstrap.Dispose();

            Assert.That(bootstrap.ExposedViewCache.Count, Is.EqualTo(0));
        }

        private CacheProbeBootstrap CreateBootstrap()
        {
            var go = new GameObject("ViewCacheIsolationTests.Bootstrap");
            _objects.Add(go);
            var bootstrap = go.AddComponent<CacheProbeBootstrap>();
            bootstrap.Init();
            return bootstrap;
        }

        private ProbeView CreateView()
        {
            var go = new GameObject("ViewCacheIsolationTests.View");
            _objects.Add(go);
            return go.AddComponent<ProbeView>();
        }

        private sealed class CacheProbeBootstrap : Bootstrap
        {
            public ViewCache ExposedViewCache => _viewCache;
            //本夹具要测的是"缓存中的早期视图"本身（ViewCache 的隔离与随 Dispose 清空）。
            //ContextStartupFlags 移除后 Init 期即完成映射，AddView 不再走缓存分支，
            //故直接写入 _viewCache 复现缓存状态（原缓存分支的效果就是这一行）。
            public void CacheEarlyView(View view) => _viewCache.Add(view);
        }

        /// 不自动向 Bootstrap 注册，测试里显式调用 CacheView，避免依赖 Awake 触发时机
        private sealed class ProbeView : View
        {
            public ProbeView()
            {
                autoRegisterWithBootstrap = false;
                requiresBootstrap = false;
            }
        }
    }
}
