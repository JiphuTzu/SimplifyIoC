/*
 * @class SimplifyIoC.Bootstraps.Bootstrap
 *
 * Bootstrap 是绑定框架的入口。
 *
 * 继承本类，创建适合你的应用的绑定容器。
 *
 * 典型的 Unity 用法是：继承 Bootstrap（或使用推荐的 SimplifyBootstrap<T>），
 * 把派生组件挂到 GameObject 上——Awake 即建容器并确定归属，Start 触发启动命令。
 *
 * 2026-10-15（2.0.0）：旧 `Context` 更名为本类，并与旧的 `Bootstrap`（纯层级锚点）合并——
 * Bootstrap 自己就是 MonoBehaviour，「层级锚点」与「绑定容器」不再是两个对象。
 * 改前两者靠 context ⇄ bootstrap 双向可变链接相连，忘写其中一半就会静默分叉
 * （跨域绑定各持一个 binder）；合并后这类失效在类型层面不可能发生。
 */

using System;
using System.Collections.Generic;
using SimplifyIoC.Commands;
using SimplifyIoC.Framework;
using SimplifyIoC.Injectors;
using SimplifyIoC.Mediations;
using UnityEngine;

namespace SimplifyIoC.Bootstraps
{
    public enum BootstrapKeys
    {
        /// Marker for the named Injection of the Bootstrap
        Bootstrap
    }
    // 3.2：不再继承 Binder——Bootstrap 自带的绑定注册表从未被使用（Bootstrap 的绑定
    // 都走 injectionBinder/commandBinder/mediationBinder 三个组件 Binder），
    // 继承只会把 Binder 的公开面错误地暴露成 Bootstrap 的 API。
    public class Bootstrap : MonoBehaviour, IDisposable
    {
        /// 3.1：Dispose 幂等标志
        protected bool _disposed;

        /// 3.5：子 Bootstrap 列表（Composite）。父 Dispose 时级联释放，实现成链回收。
        /// protected 以便派生类（含测试夹具）检查链结构。
        protected readonly List<Bootstrap> _children = new List<Bootstrap>();

        /// 3.5：父 Bootstrap 引用。子 Dispose 时据此从父的子列表摘除自己，实现双向解绑。
        private Bootstrap _parentBootstrap;

        /// 5.6：划归关系的内读取入口（供测试夹具核对链结构）。<see cref="AddBootstrap"/> / Dispose 维护它。
        internal Bootstrap parentBootstrap => _parentBootstrap;

        /// 本 Bootstrap 是否为链根——只有链根持有共享 crossContextBinder
        /// （CrossContextBridge 单例与所有 .CrossContext() 绑定的宿主）。
        ///
        /// 5.6+：与 <see cref="root"/> 同源——占住锚点的那个就是链根。
        /// 全框架只有这一个"谁算链根"的判断（5.6 之前的 <c>_isRoot</c> 也只是
        /// <c>firstContext == this</c> 的缓存，缓存那一步是为了绕开 Dispose 里先清 firstContext 的顺序问题，
        /// 现在改为先做清理、后清锚点，缓存就没有必要了）。
        internal bool isRoot => ReferenceEquals(root, this);

        /// 5.6+：本 Bootstrap 是否已 Init 到"容器已建"（走过 InstantiateCoreComponents）。
        /// 取代原来"祖先 Bootstrap 的 context 属性非空"这条就绪判定：父查找据此跳过尚未 Init 的祖先，
        /// 免得把子 Bootstrap 挂到一个还没有跨域根 binder 的父上（两条链各持一个 binder ⇒ 跨域绑定静默分叉）。
        internal bool isReady => _coreInstantiated;

        /// <summary>
        /// 5.6+：框架级锚点——「跨场景 / 并列的 Bootstrap 组件」在 Init 时取得父 Bootstrap 的公开入口。
        ///
        /// 自动登记：**第一个构造的链根 Bootstrap** 占住锚点（先到先得，不抢占）；
        /// 它 Dispose 时清空（自己的 OnDestroy → Dispose，契约保证），此后下一个 Init 的链根接任。
        ///
        /// 它同时是两条**兜底**的落点——这两条就是 5.6 之前静态 firstContext 的行为，
        /// 于 2026-10-15 恢复（当时一并删掉的还有 View 侧那条 last-ditch，见 View.ResolveAndRegister）：
        ///
        ///   ① Init 新 Bootstrap 时，**显式 parent 与 Transform 层级都找不到父** ⇒ 挂到本锚点下
        ///      （旧语义"谁先构造谁当根，后来的全挂在它下面"）；
        ///   ② View 向上**找不到任何 Bootstrap** ⇒ 注册到本锚点，而不是抛异常
        ///      （仅 requiresBootstrap = true 的 View；声明不依赖 Bootstrap 的不参与）。
        ///
        /// 归属优先级：**显式 parent > Transform 层级 > 锚点兜底**。前两条能表达清楚时永远优先，
        /// 锚点只在调用方表达不出来时才生效——这是它与旧 firstContext 的唯一实质差别（旧版无条件通吃）。
        ///
        /// 典型用法（App 根常驻、各场景分别加载，两侧在 Transform 上毫无关系且分属不同程序集）：
        ///
        ///     //App 层：常驻（DontDestroyOnLoad），先 Init 即可——锚点自动登记
        ///     class AppBootstrap : SimplifyBootstrap<AppStartupCommand> { }
        ///     //场景层：显式认父语义最清楚；实在拿不到父引用时不传也会自动挂到锚点下
        ///     class LevelBootstrap : SimplifyBootstrap<LevelStartupCommand>
        ///     {
        ///         protected override void Awake() => Init(Bootstrap.root);
        ///     }
        ///
        /// 另：本成员与已删除的 `Context.firstContext` 是**同一语义的新名字**，旧名不再提供。
        /// 存为弱引用：锚点不会把整条链（注入容器、单例、订阅）长期吊住。
        /// </summary>
        public static Bootstrap root
        {
            get => _rootRef != null && _rootRef.TryGetTarget(out var c) ? c : null;
            set
            {
                var current = root;
                if (value != null && current != null && !ReferenceEquals(current, value))
                {
                    Debug.LogWarning("[SimplifyIoC] Bootstrap.root 被 " + value.GetType().Name +
                                     " 覆盖（原锚点：" + current.GetType().Name + "）。" +
                                     "锚点默认由第一个构造的 Bootstrap 自动持有，通常无需手工赋值。");
                }
                _rootRef = value == null ? null : new WeakReference<Bootstrap>(value);
            }
        }

        private static WeakReference<Bootstrap> _rootRef;
        
        /// A Binder that handles dependency injection binding and instantiation
        /// All cross-context capable bootstraps must implement an injectionBinder
        protected ICrossContextInjectionBinder injectionBinder { get; } = new CrossContextInjectionBinder();
        
        /// A Binder that maps Signals to Commands
        protected ICommandBinder commandBinder { get; set; }
        /// A Binder that maps Views to Mediators
        protected IMediationBinder mediationBinder { get; set; }
        /// A list of Views Awake before the Bootstrap is fully set up.
        /// 3.2：由 static SemiBinding 改为实例级 ViewCache——多 Bootstrap 互不干扰，
        /// 也不再因 static 长期持有 View 引用而跨场景泄漏。
        protected readonly ViewCache _viewCache = new ViewCache();

        /// 4.7：核心组件是否已实例化（Init 走过 InstantiateCoreComponents 之后）。
        /// 用于区分两种"没有 mediationBinder"：
        ///   ① Bootstrap 尚未就绪（早到的 View）——先缓存，等 PostBindings 统一处理；
        ///   ② Bootstrap 本就不使用 Mediator 机制——直接给 View 走注入 + 声明式解析的兜底路径。
        private bool _coreInstantiated;

        /// <summary>
        /// Unity 生命周期：组件装载即建容器（内部调 <see cref="Init"/>，幂等）。
        /// 需要显式认父的派生类覆写它并直接调用 <c>Init(父)</c>——不必再调 base（Init 幂等）。
        /// 编辑模式不派发本消息，EditMode 测试请显式调用 <see cref="Init"/>。
        /// </summary>
        protected virtual void Awake()
        {
            Init();
        }

        /// <summary>
        /// Unity 生命周期：组件销毁即走 <see cref="Dispose"/>（幂等）——级联释放子 Bootstrap、
        /// 清共享跨域根 binder、清锚点，与改前 Bootstrap.OnDestroy 的行为一致。
        /// </summary>
        protected virtual void OnDestroy()
        {
            Dispose();
        }

        /// <summary>
        /// 5.6+：建容器 + 定归属 + 映射（InstantiateCoreComponents + MapBindings + PostBindings）。幂等。
        ///
        /// 由 <see cref="Awake"/> 自动调用；EditMode 测试与运行时动态加组件请显式调用它。
        /// 要显式认父：覆写 <see cref="Awake"/> 后直接 <c>Init(父)</c>——不要先 base.Awake() 再 Init(父)，
        /// 那样显式父会被忽略（本方法会打一条警告）。
        /// </summary>
        /// <param name="parent">显式父 Bootstrap；传 null 则按"Transform 层级 → 框架级锚点"解析。</param>
        public void Init(Bootstrap parent = null)
        {
            if (_coreInstantiated)
            {
                if (parent != null && !ReferenceEquals(parent, _parentBootstrap))
                    Debug.LogWarning("[SimplifyIoC] " + GetType().Name +
                                     " 已经完成 Init，本次 Init(parent) 的父参数被忽略。" +
                                     "要显式认父，请在覆写 Awake 时直接调用 Init(parent)，不要先调 base.Awake()。");
                return;
            }

            ThrowIfDisposed();

            //显式传入的父必须已经就绪：父的跨域根 binder 是在它自己的 Init 里建的，
            //若父尚未 Init 就被挂上去，两条链会各持一个 binder、跨域绑定静默分叉（5.6 撞过这个坑）。
            //Awake 顺序不受保证，所以这里只告警 + 退回常规归属解析，不抛。
            if (parent != null && !parent.isReady)
            {
                Debug.LogWarning("[SimplifyIoC] " + GetType().Name + " 收到尚未 Init 的父 Bootstrap（" +
                                 parent.GetType().Name + "），已忽略该父并改用常规归属解析（层级 → 锚点）。");
                parent = null;
            }

            //5.6+：归属确定顺序 —— 显式 parent > Transform 层级向上查找 > 回落框架级锚点。
            //最后一段就是 5.6 之前静态 firstContext 的语义：谁先构造谁当根，后来者自动挂成它的子。
            //放在最后不是弱化它，而是让能表达清楚的表达清楚：显式 parent 与 GameObject 层级都
            //说得明白时永远优先，只有在两者都落空时才轮到"第一个构造的 Bootstrap"兜底——
            //这样任何 Bootstrap 都不会因为调用方没说清楚而丢掉跨域共享（2026-10-15 恢复）。
            //锚点只在这一处登记：占住 root 的就是链根（isRoot 由它派生）——
            //"谁算链根"全框架仅此一个判断，不存在第二个会与之打架的字段。
            var parentBootstrap = parent ?? ResolveParentBootstrap() ?? root;
            if (parentBootstrap != null && parentBootstrap != this)
                parentBootstrap.AddBootstrap(this);
            else if (root == null)
                root = this;

            AddCoreComponents();
            //启动语义（单一、显式）：Init = 建容器 + 归属 + 映射；
            //启动（Launch）永远由调用方显式触发——推荐路径由 SimplifyBootstrap<T>.Start 补上，
            //裸派生 Bootstrap 的类需自行调用 Launch()。
            //注意 MapBindings 在 Init 内即被调用（虚调用陷阱）：派生类实例字段尚未赋值，
            //MapBindings 里只放类型绑定，值绑定请在 Init 完成后再补。
            InstantiateCoreComponents();
            _coreInstantiated = true;
            MapBindings();
            PostBindings();
        }

        /// <summary>
        /// 5.6：沿 Transform 向上找最近的、已经就绪的 Bootstrap 组件，作为本 Bootstrap 的父。
        ///
        /// 与 View 的归属查找共用一套约定（都向上找 Bootstrap、都跳过自己所在的 GameObject、
        /// 都不要求中途每一层都有 Bootstrap），这是"Bootstrap 归属 = GameObject 归属"这条单一规则的运行时依据。
        ///
        /// 尚未 Init 的祖先会被跳过（见 <see cref="isReady"/>）：宁可继续往上找、或落到锚点上，
        /// 也不能挂到一个还没有跨域根 binder 的父上。
        /// 返回 null 表示本 Bootstrap 是链根，据此 <see cref="AddCoreComponents"/> 决定是否创建共享跨域根 binder。
        /// </summary>
        protected virtual Bootstrap ResolveParentBootstrap()
        {
            const int loopMax = 100;
            var loopLimiter = 0;
            var trans = transform.parent;
            while (trans != null && loopLimiter < loopMax)
            {
                loopLimiter++;
                var candidate = trans.GetComponent<Bootstrap>();
                if (candidate != null && candidate.isReady)
                    return candidate;
                trans = trans.parent;
            }
            return null;
        }

        // 原 Bootstrap(Bootstrap, bool autoMapping) 重载已删除（P0#1）：
        // 其两个取值无论怎么传都不会执行 Start()（映射恒被推迟），
        // 语义反了且无任何调用方。ContextStartupFlags 已随启动语义统一一并移除：
        // 映射永远在构造期完成，启动永远显式调用 Launch()。

        protected virtual void AddCoreComponents()
        {
            //5.6：非链根早在 AddBootstrap 时已从父继承，这里只为链根兜底创建。
            injectionBinder.crossContextBinder ??= new CrossContextInjectionBinder();

            if (isRoot)
            {
                //链根创建并持有共享的跨域根 binder（CrossContextBridge 单例宿主）。
                //级联释放时由它负责清理，见 Dispose。
                injectionBinder.Bind<CrossContextBridge>().ToSingleton ().CrossContext();
            }

            injectionBinder.Bind<IInstanceProvider>().Bind<IInjectionBinder>().ToValue(injectionBinder);
            injectionBinder.Bind<Bootstrap>().ToValue(this).ToName(BootstrapKeys.Bootstrap);
            injectionBinder.Bind<ICommandBinder>().To<CommandBinder>().ToSingleton();
            injectionBinder.Bind<IMediationBinder>().To<MediationBinder>().ToSingleton();
        }

        /// Override to instantiate componentry. Or just extend MVCSBootstrap.
        protected virtual void InstantiateCoreComponents()
        {
            commandBinder = injectionBinder.GetInstance<ICommandBinder>();
            //4.7：mediationBinder 允许缺失。不使用 Mediator 机制的 Bootstrap（派生类未绑定
            //IMediationBinder）不再在这里因"no binding"抛异常，而是留 null，
            //由 View 的兜底路径接管（见 AddView / MediateViewCache）。
            mediationBinder = injectionBinder.GetBinding<IMediationBinder>() != null
                ? injectionBinder.GetInstance<IMediationBinder>()
                : null;
        }

        /// 启动 Bootstrap：启动语义由派生类定义（如 SimplifyBootstrap<T> 在此广播 StartupSignal）。
        /// 启动永远显式——推荐路径由 SimplifyBootstrap<T>.Start 调用；
        /// 裸派生 Bootstrap 的类需自行调用 Launch()。
        public virtual void Launch()
        {
            ThrowIfDisposed();
        }

        /// Override to map project-specific bindings
        protected virtual void MapBindings() { }

        /// Override to do things after binding but before app launch
        protected virtual void PostBindings()
        {
            //It's possible for views to fire their Awake before bindings. This catches any early risers and attaches their Mediators.
            MediateViewCache();
            //Ensure that all Views underneath the BootstrapView are triggered
            //mediationBinder.Trigger(MediationEvent.AWAKE, _contextView);
        }

        /// Add another Bootstrap to this one.
        /// 3.5：显式登记父子关系（Composite）——父持有子引用，子持有父引用，
        /// 并在两端共享同一个跨域根 binder。这样父 Dispose 可级联释放子，
        /// 子 Dispose 也可把自己从父的链上摘除。
        ///
        /// 5.6：建议在 Pending Bootstrap 尚未 Init（尚未登记 .CrossContext() 绑定）时调用——
        /// 通常就是 Init(parent)。事后再认父不会迁移已经落到"原跨域根 binder"上的
        /// 跨域绑定（那些绑定留在旧 binder 上，随旧 binder 一起无人清理）。
        public virtual void AddBootstrap(Bootstrap bootstrap)
        {
            ThrowIfDisposed();
            if (bootstrap == null || bootstrap == this) return;

            //5.6：转移归属前先从旧父摘除，避免出现"同一个 Bootstrap 同时是两个节点的子"
            if (bootstrap._parentBootstrap != null && bootstrap._parentBootstrap != this)
                bootstrap._parentBootstrap._children.Remove(bootstrap);

            //已登记则只保证共享桥接仍然成立（幂等）
            if (!_children.Contains(bootstrap))
                _children.Add(bootstrap);
            bootstrap._parentBootstrap = this;
            //非链根：与父共享同一个跨域根 binder（链根由锚点派生，挂上父之后自动不再成立）
            bootstrap.injectionBinder.crossContextBinder = injectionBinder.crossContextBinder;
        }

        /// Remove a bootstrap from this one.
        public void RemoveBootstrap(Bootstrap bootstrap)
        {
            if (bootstrap == null) return;
            //3.5：双向解绑——从子列表摘除并断开子的父引用
            _children.Remove(bootstrap);
            if (bootstrap._parentBootstrap == this)
            {
                bootstrap._parentBootstrap = null;
                //3.1/5.6+：被摘掉的若正是锚点，锚点随之落空，由下一个 Init 的链根接任。
                //不能留：它的跨域根 binder 在下一行就被断开了，留着会让新 Bootstrap 挂到
                //一个没有跨域能力的锚点上。（5.6 之前这里是「摘掉的正好是锚点就把锚点置空」那一行，语义同下）
                if (ReferenceEquals(root, bootstrap))
                    root = null;
            }
            bootstrap.injectionBinder.crossContextBinder = null;
            bootstrap.OnRemove();
        }

        /// <summary>
        /// 3.1：销毁契约（幂等）。脱离 Bootstrap 链、执行 OnRemove 清理
        /// （信号监听移除、命令池释放、三个 Binder 注册表清空）。
        /// Dispose 后再 Init/Launch/AddBootstrap/RemoveBootstrap 抛 ObjectDisposedException；
        /// 再经 injectionBinder GetInstance 因注册表已空按既有行为抛"no binding"异常。
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            //3.5：级联释放子 Bootstrap（Composite 成链回收）。
            //倒序 + 先断开子侧的父引用，避免子 Dispose 时反向再操作本对象。
            for (var i = _children.Count - 1; i >= 0; i--)
            {
                var child = _children[i];
                if (child == null) continue;
                child._parentBootstrap = null;
                child.Dispose();
            }
            _children.Clear();

            //3.5：双向解绑的另一半——把自己从父的子列表摘除
            if (_parentBootstrap != null)
            {
                _parentBootstrap._children.Remove(this);
                _parentBootstrap = null;
            }

            //3.5：无论角色如何都断开共享桥接。
            //链根额外负责清空共享的跨域根 binder —— 它是 CrossContextBridge 单例与
            //所有 .CrossContext() 绑定的宿主，不清空则整条链的跨域单例都会泄漏
            //（子已在上方全部级联释放，此处清空不会影响其它存活 Bootstrap）。
            //顺序要紧：isRoot 与锚点同源，所以这一段必须排在"脱离锚点"之前，
            //否则链根先把自己从锚点上摘掉，isRoot 当场变 false，共享 binder 就没人清了。
            var sharedBinder = injectionBinder.crossContextBinder;
            if (isRoot && sharedBinder != null)
                (sharedBinder as IBinder)?.OnRemove();
            injectionBinder.crossContextBinder = null;

            //5.6+：脱离锚点——只有自己仍是锚点时才清（别误清别人的）；清空后由下一个 Init 的链根接任
            if (ReferenceEquals(root, this))
                root = null;

            OnRemove();
            commandBinder = null;
            mediationBinder = null;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(Bootstrap));
        }

        /// Register a View with this Bootstrap
        /// 4.7：View 的注入与声明式解析（[Child]/[BindEvent]/[BindMethod]/[MainThread]）
        /// 都不依赖 Mediator——没有 mediationBinder 时走兜底路径，View 照常可用。
        public virtual void AddView(View view)
        {
            if (view == null) return;
            //5.6：归属在注册成功这一刻确定，之后 View 的 Remove/Enable/Disable 直接发回本 Bootstrap，
            //不再逐次遍历 Transform 链。（"注册到谁"仍可能由锚点兜底决定，见 View.ResolveAndRegister；
            //但一旦注册成功，归属就钉死在本对象上，不会在生命周期里漂移。）
            view.SetOwningBootstrap(this);

            if (mediationBinder != null)
            {
                mediationBinder.Trigger(MediationEvent.Awake, view);
                return;
            }

            //Bootstrap 已就绪却仍无 mediationBinder ⇒ 本 Bootstrap 不使用 Mediator 机制。
            //尚未就绪则说明是"早到的 View"，照旧缓存，等 PostBindings 统一处理。
            if (_coreInstantiated)
                InjectViewWithoutMediation(view);
            else
                CacheView(view);
        }

        /// Remove a View from this Bootstrap
        public virtual void RemoveView(View view)
        {
            if (mediationBinder == null) return; //场景卸载期 binder 可能已销毁（P0#9）；无 Mediator 机制时也无需处理
            mediationBinder.Trigger(MediationEvent.Destroyed, view);
        }

        /// Enable a View from this Bootstrap
        public virtual void EnableView(View view)
        {
            if (mediationBinder == null) return; //无 Mediator 时没有 mediator 需要 enable
            mediationBinder.Trigger(MediationEvent.Enabled, view);
        }

        /// Disable a View from this Bootstrap
        public virtual void DisableView(View view)
        {
            if (mediationBinder == null) return; //无 Mediator 时没有 mediator 需要 disable
            mediationBinder.Trigger(MediationEvent.Disabled, view);
        }

        /// <summary>
        /// 4.7：无 Mediator 机制时 View 的兜底处理——只做 View 自身必需的两件事：
        /// 容器注入 + 声明式解析。不涉及任何 Mediator 概念，也不递归子视图
        /// （子视图各自走自己的 AddView / Awake 兜底路径，避免重复注入）。
        ///
        /// 这正是"没有 Mediator 的 View 也能正常工作"的实现点：
        /// 4.1 之后 View 的解析由框架触发，若只在 MediationBinder 里触发，
        /// 不使用 Mediator 的项目就永远解析不到 [Child]/[BindEvent]。
        /// </summary>
        protected virtual void InjectViewWithoutMediation(View view)
        {
            if (view == null) return;
            injectionBinder.injector.Inject(view, false);
            view.EnsureAttributesInitialized();
        }

        public virtual void OnRemove()
        {
            //3.2：不再有 base.OnRemove()——Bootstrap 已不继承 Binder。
            //3.1：三个组件 Binder 各自执行真实清理（信号监听/命令池/绑定注册表；3.3 起 suppliers 已并入绑定自身）。
            //injectionBinder 的静态类型是 ICrossContextInjectionBinder（未继承 IBinder），
            //但实际实例必为 Binder 派生类，向下转型恒成立
            (injectionBinder as IBinder)?.OnRemove();
            commandBinder?.OnRemove();
            mediationBinder?.OnRemove();
            //未被 Mediate 的残留视图引用一并释放
            _viewCache.Clear();
        }

        protected virtual void MediateViewCache()
        {
            //3.2：补挂 + 清空由 ViewCache 负责（原实现操作全局 static 缓存）
            //4.7：没有 mediationBinder 时不再抛异常——View 的注入与声明式解析本就
            //不依赖 Mediator，改走兜底路径，使"不使用 Mediator 机制"的 Bootstrap 也能驱动 View。
            if (mediationBinder != null)
            {
                _viewCache.MediateWith(mediationBinder);
                return;
            }

            _viewCache.ForEach(InjectViewWithoutMediation);
        }
        /// Caches early-riser Views.
        /// 
        /// If a View is on stage at startup, it's possible for that
        /// View to be Awake before this Bootstrap has finished initing.
        /// `cacheView()` maintains a list of such 'early-risers'
        /// until the Bootstrap is ready to mediate them.
        protected virtual void CacheView(View view)
        {
            //3.2：原 SemiBinding 的 One→Many 约束逻辑随 static 缓存一并移除
            _viewCache.Add(view);
        }
    }
}