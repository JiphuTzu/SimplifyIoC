// 5.6+：框架级 Bootstrap 锚点（Bootstrap.root）回归测试。
//
// 场景：App 根与各场景的 Bootstrap 组件在 Transform 上并列、跨场景分别加载，
// 且分属不同程序集（热更新分层要求"场景/玩法层不编译期依赖 App 层"）。
// 锚点让场景侧只依赖框架类型就能拿到父 Bootstrap：
//
//     class LevelBootstrap : SimplifyBootstrap<LevelStartupCommand>
//     {
//         // 什么都不必写：Init() 无父参数，Transform 上没有父时自动落到 Bootstrap.root 下
//     }
//
// 2026-10-15：锚点同时承担 5.6 之前静态 firstContext 的三条语义（本文件刻意钉住）：
//   1. 锚点自动产生——第一个 Init 的链根持有，后 Init 者不抢占；
//   2. 后来者自动挂成子——Transform 层级上没有父的 Bootstrap 挂到锚点下；
//   3. View 兜底——找不到任何 Bootstrap 的 View（requiresBootstrap = true）注册到锚点，而不是抛异常。
//
// 注意：锚点是进程级静态槽，SetUp / TearDown 都要清干净，否则用例之间互相污染。
using System.Collections.Generic;
using NUnit.Framework;
using SimplifyIoC.Bootstraps;
using SimplifyIoC.Injectors;
using SimplifyIoC.Mediations;
using SimplifyIoC.Signals;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SimplifyIoC.Tests
{
    public class RootBootstrapTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<Bootstrap> _bootstraps = new List<Bootstrap>();

        [SetUp]
        public void SetUp()
        {
            //锚点是静态槽：别的用例（或残留的 Bootstrap）可能已经占用它，先归零
            Bootstrap.root = null;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var bootstrap in _bootstraps)
            {
                bootstrap?.Dispose();
            }
            _bootstraps.Clear();

            //静态槽不得留给下一个用例
            Bootstrap.root = null;

            foreach (var go in _objects)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _objects.Clear();
        }

        [Test]
        public void FirstConstructedBootstrapAutomaticallyBecomesTheAnchor()
        {
            var app = CreateAnchorRoot("AppRoot");

            Assert.That(Bootstrap.root, Is.SameAs(app), "第一个构造成功的 Bootstrap 自动成为锚点");
        }

        [Test]
        public void LaterBootstrapAttachesToTheAnchorInsteadOfTakingItOver()
        {
            var app = CreateAnchorRoot("AppRoot");
            var second = CreatePlain("SecondRoot");

            Assert.That(Bootstrap.root, Is.SameAs(app), "锚点先到先得，后来的 Bootstrap 不许抢占");
            Assert.That(second.parentBootstrap, Is.SameAs(app), "后来者自动挂到锚点下（firstContext 的挂子语义）");
            Assert.That(second.isRoot, Is.False);
            Assert.That(app.exposedChildren, Does.Contain(second));
            Assert.That(second.exposedInjectionBinder.crossContextBinder,
                Is.SameAs(app.exposedInjectionBinder.crossContextBinder),
                "自动认父后共享同一个跨域根 binder");
        }

        [Test]
        public void AnchorIsClearedWhenItsBootstrapIsDisposed()
        {
            var app = CreateAnchorRoot("AppRoot");

            app.Dispose();

            Assert.That(Bootstrap.root, Is.Null, "锚点销毁后不得残留（弱引用 + Dispose 判定）");
        }

        [Test]
        public void NextBootstrapSucceedsTheAnchorAfterItWasDisposed()
        {
            var app = CreateAnchorRoot("AppRoot");
            app.Dispose();

            var next = CreatePlain("Next");

            Assert.That(Bootstrap.root, Is.SameAs(next), "锚点空出后由下一个构造的 Bootstrap 接任");
        }

        [Test]
        public void DisposingAnotherBootstrapKeepsTheAnchor()
        {
            var app = CreateAnchorRoot("AppRoot");
            var other = CreatePlain("Other");

            other.Dispose();

            Assert.That(Bootstrap.root, Is.SameAs(app), "别的 Bootstrap 销毁不许误清锚点");
            Assert.That(app.exposedChildren, Is.Empty, "子销毁只把自己从链上摘除");
        }

        [Test]
        public void SceneBootstrapAttachesToAnchorWithoutKnowingItsType()
        {
            //核心用例：两个 Bootstrap 组件在 Transform 上毫无关系（模拟不同场景分别加载），
            //场景侧只通过 Bootstrap.root 取父引用——不需要知道 App 侧的任何具体类型。
            var app = CreateAnchorRoot("AppRoot");
            var level = CreatePlain("Level");

            Assert.That(level.parentBootstrap, Is.SameAs(app));
            Assert.That(level.isRoot, Is.False);
            Assert.That(app.exposedChildren, Does.Contain(level));
            Assert.That(level.exposedInjectionBinder.crossContextBinder,
                Is.SameAs(app.exposedInjectionBinder.crossContextBinder),
                "认父后两端共享同一个跨域根 binder");

            //锚点根上登记的跨域绑定，场景 Bootstrap 里要能拿到，且是同一个单例
            var fromRoot = app.exposedInjectionBinder.GetInstance<ITestService>();
            var fromLevel = level.exposedInjectionBinder.GetInstance<ITestService>();
            Assert.That(fromLevel, Is.Not.Null, "跨域绑定跨场景可用");
            Assert.That(fromLevel, Is.SameAs(fromRoot), "跨域单例整条链共享一个实例");
        }

        [Test]
        public void MissingAnchorDegradesToSelfRootAndThenHoldsTheAnchor()
        {
            //单独跑某个场景调试时锚点不在：Init 时读到的是 null，
            //于是该 Bootstrap 自任链根、功能照常；又因为锚点空着，它自己接下锚点。
            var level = CreatePlain("Level");

            Assert.That(level.parentBootstrap, Is.Null);
            Assert.That(level.isRoot, Is.True, "锚点缺席时自任链根，功能照常");
            Assert.That(level.exposedChildren, Is.Empty);
            Assert.That(Bootstrap.root, Is.SameAs(level), "锚点空着，于是它就是唯一在册的锚点");
        }

        [Test]
        public void AnchorSurvivesALevelBootstrapDisposal()
        {
            //场景卸载：子 Bootstrap Dispose 只摘自己，不带走锚点，也不清共享根 binder
            var app = CreateAnchorRoot("AppRoot");
            var level = CreatePlain("Level");

            level.Dispose();

            Assert.That(Bootstrap.root, Is.SameAs(app), "场景卸载不得带走锚点");
            Assert.That(app.exposedChildren, Is.Empty);
        }

        [Test]
        public void AnchorRescuesViewsThatFindNoBootstrap()
        {
            //last-ditch 兜底（2026-10-15 恢复 firstContext 语义）：
            //找不到任何 Bootstrap 的 View 注册到框架级锚点，而不是在 Start 抛异常。
            var app = CreateAnchorRoot("AppRoot");

            var orphanRoot = NewObject("Orphan");
            var view = orphanRoot.AddComponent<OrphanView>();

            Assert.DoesNotThrow(() => view.CallAwake(), "Awake 阶段不动声色：finalTry = false");
            Assert.DoesNotThrow(() => view.CallStart(), "Start 阶段回落到锚点，不再抛");
            Assert.That(view.owningBootstrap, Is.SameAs(app), "兜底注册同样在那一刻写下归属");
            Assert.That(view.registeredWithBootstrap, Is.True);
        }

        [Test]
        public void RequiresBootstrapFalseViewDoesNotFallBackToTheAnchor()
        {
            //作者已声明"不依赖 Bootstrap"的 View 不参与兜底——它依旧只做声明式解析。
            CreateAnchorRoot("AppRoot");

            var orphanRoot = NewObject("Orphan");
            var view = orphanRoot.AddComponent<OrphanView>();
            view.requiresBootstrap = false;

            Assert.DoesNotThrow(() => view.CallAwake());
            Assert.DoesNotThrow(() => view.CallStart());
            Assert.That(view.owningBootstrap, Is.Null);
        }

        [Test]
        public void BootstrapWithoutExplicitParentFallsBackToTheAnchor()
        {
            //不传 parent、也不在锚点的 Transform 链上（模拟两个场景并列加载），
            //仍旧自动挂到锚点下——这就是 firstContext 的"后来者自动成为子"。
            var app = CreateAnchorRoot("AppRoot");
            var level = CreatePlain("Level");

            Assert.That(level.parentBootstrap, Is.SameAs(app));
            Assert.That(level.isRoot, Is.False);
            Assert.That(app.exposedChildren, Does.Contain(level));
        }

        // ---- 辅助 ----

        private GameObject NewObject(string name)
        {
            var go = new GameObject("RootBootstrapTests." + name);
            _objects.Add(go);
            return go;
        }

        /// <summary>锚点根：登记一个跨域单例（解析时会被搬到共享根 binder 上）。</summary>
        private AnchorRootBootstrap CreateAnchorRoot(string name)
        {
            var bootstrap = NewObject(name).AddComponent<AnchorRootBootstrap>();
            bootstrap.Init();
            _bootstraps.Add(bootstrap);
            return bootstrap;
        }

        /// <summary>普通 Bootstrap：只做本地绑定；归属由框架自动解析（无 Transform 父时落到锚点）。</summary>
        private PlainBootstrap CreatePlain(string name)
        {
            var bootstrap = NewObject(name).AddComponent<PlainBootstrap>();
            bootstrap.Init();
            _bootstraps.Add(bootstrap);
            return bootstrap;
        }

        private sealed class AnchorRootBootstrap : Bootstrap
        {
            public IReadOnlyList<Bootstrap> exposedChildren => _children;
            public ICrossContextInjectionBinder exposedInjectionBinder => injectionBinder;

            protected override void MapBindings()
            {
                //跨域单例：解析时会被搬到共享根 binder，供整条链（含各场景）使用
                injectionBinder.Bind<ITestService>().To<TestServiceImpl>().ToSingleton().CrossContext();
            }
        }

        private sealed class PlainBootstrap : Bootstrap
        {
            public IReadOnlyList<Bootstrap> exposedChildren => _children;
            public ICrossContextInjectionBinder exposedInjectionBinder => injectionBinder;

            protected override void MapBindings()
            {
                injectionBinder.Bind<TestSignal>().ToSingleton();
            }
        }

        /// <summary>把生命周期回调显式暴露出来——EditMode 下引擎不会派发 MonoBehaviour 消息。</summary>
        private sealed class OrphanView : View
        {
            public void CallAwake() => Awake();
            public void CallStart() => Start();
        }
    }
}
