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
        private bool _disposed;

        /// 3.5：子 Bootstrap 列表（Composite）。父 Dispose 时级联释放，实现成链回收。
        /// protected 以便派生类（含测试夹具）检查链结构。
        protected readonly List<Bootstrap> _children = new();

        // 3.5：父 Bootstrap 引用。子 Dispose 时据此从父的子列表摘除自己，实现双向解绑。
        //private Bootstrap _parentBootstrap;

        /// 5.6：划归关系的内读取入口（供测试夹具核对链结构）。<see cref="AddBootstrap"/> / Dispose 维护它。
        protected Bootstrap parentBootstrap { get; private set; }

        /// 本 Bootstrap 是否为链根——只有链根持有共享 crossContextBinder
        /// （CrossContextBridge 单例与所有 .CrossContext() 绑定的宿主）。
        ///
        /// 5.6+：与 <see cref="root"/> 同源——占住锚点的那个就是链根。
        /// 全框架只有这一个"谁算链根"的判断（5.6 之前的 <c>_isRoot</c> 也只是
        /// <c>firstContext == this</c> 的缓存，缓存那一步是为了绕开 Dispose 里先清 firstContext 的顺序问题，
        /// 现在改为先做清理、后清锚点，缓存就没有必要了）。
        protected bool isRoot => ReferenceEquals(root, this);

        /// 5.6+：本 Bootstrap 是否已 Init 到"容器已建"（走过 InstantiateCoreComponents）。
        /// 取代原来"祖先 Bootstrap 的 context 属性非空"这条就绪判定：父查找据此跳过尚未 Init 的祖先，
        /// 免得把子 Bootstrap 挂到一个还没有跨域根 binder 的父上（两条链各持一个 binder ⇒ 跨域绑定静默分叉）。
        protected bool isReady { get; private set; }

        /// <summary>
        /// 5.6+：框架级锚点——「跨场景 / 并列的 Bootstrap 组件」在 Init 时取得父 Bootstrap 的公开入口。
        ///
        /// 自动登记：**第一个构造的链根 Bootstrap** 占住锚点（先到先得，不抢占）；
        /// 它 Dispose 时清空（自己的 OnDestroy → Dispose，契约保证），此后下一个 Init 的链根接任。
        ///
        /// 它同时是两条**兜底**的落点——这两条就是 5.6 之前静态 firstContext 的行为，
        /// 于 2026-10-15 恢复（当时一并删掉的还有 View 侧那条 last-ditch，见 View.ResolveAndRegister）：
        ///
        ///   ① Init 新 Bootstrap 时，**Transform 层级找不到父** ⇒ 挂到本锚点下
        ///      （旧语义"谁先构造谁当根，后来的全挂在它下面"）；
        ///   ② View 向上**找不到任何 Bootstrap** ⇒ 注册到本锚点，而不是抛异常
        ///      （仅 requiresBootstrap = true 的 View；声明不依赖 Bootstrap 的不参与）。
        ///
        /// 归属优先级：**Transform 层级 > 锚点兜底**。层级上说得清楚时永远优先，
        /// 锚点只在层级表达不出来时才生效——这是它与旧 firstContext 的实质差别（旧版无条件通吃）；
        /// 要指定"锚点之外的某个父"请用 <see cref="AddBootstrap"/>。
        ///
        /// 典型用法（App 根常驻、各场景分别加载，两侧在 Transform 上毫无关系且分属不同程序集）：
        ///
        ///     //App 层：常驻（DontDestroyOnLoad），先 Init 即可——锚点自动登记
        ///     class AppBootstrap : SimplifyBootstrap&lt;AppStartupCommand&gt; { }
        ///     //场景层：什么都不用写（连 Awake 都不必覆写）——Init() 无父参数，
        ///     //Transform 上没有父时自动落到本锚点下，跨域绑定随之可用
        ///     class LevelBootstrap : SimplifyBootstrap&lt;LevelStartupCommand&gt; { }
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
        private readonly ViewCache _viewCache = new();

        /// <summary>
        /// Unity 生命周期：组件装载即建容器（内部调 <see cref="Init"/>，幂等）。
        /// 派生类一般不必覆写它——归属由 <see cref="Init"/> 自动解析；要控制启动时机请覆写
        /// <c>SimplifyBootstrap&lt;T&gt;.Start</c>。
        /// 编辑模式不派发本消息，包内 EditMode 测试请显式调用 <see cref="Init"/>（<c>internal</c>，经 InternalsVisibleTo 可见）。
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
        /// 由 <see cref="Awake"/> 自动调用；可见性是 <c>internal</c>——它是框架自用的生命周期入口，
        /// 只有框架自身与包内测试程序集可见（<c>AssemblyInfo.cs</c> 的 <c>InternalsVisibleTo</c>）。
        /// 运行时 <c>AddComponent&lt;Bootstrap&gt;()</c> 照常由 Unity 派发 <see cref="Awake"/>，
        /// 下游正常使用不需要（也调不到）它；下游若要在 EditMode 里驱动 Bootstrap，请改用 PlayMode 测试。
        ///
        /// **无父参数**：归属自动解析——先沿 Transform 向上找最近的已就绪 Bootstrap，
        /// 找不到才回落到框架级锚点 <see cref="root"/>（第一个构造成链根的那个，即"后来者自动成为子"）。
        /// 调用方因此不需要持有、也不需要知道任何别的 Bootstrap 的类型（跨程序集/热更新分层友好）；
        /// 要指定"锚点之外的某个父"请用 <see cref="AddBootstrap"/>。
        ///
        /// 幂等仅指"重复 Init 无副作用"；<see cref="Dispose"/> 之后本方法会抛 <see cref="ObjectDisposedException"/>，
        /// 已释放的 Bootstrap 不可复活（见 <see cref="Dispose"/> 的契约说明）。
        /// </summary>
        internal void Init()
        {
            //3.1：守卫必须排在幂等判据之前——_coreInstantiated 在 Dispose 之后仍为 true，
            //排在后面会让"Dispose 后再 Init"静默返回，而不是抛 ObjectDisposedException。
            //（幂等判据是 2.0.0 合并时新增的；旧 Context.Start() 没有它，守卫在首位，这条契约才成立。）
            ThrowIfDisposed();

            if (isReady) return;

            //归属确定顺序 —— Transform 层级向上查找 > 回落框架级锚点。
            //后一段就是 5.6 之前静态 firstContext 的语义：谁先构造谁当根，后来者自动挂成它的子。
            //这样任何 Bootstrap 都不会因为调用方没写层级关系而丢掉跨域共享（2026-10-15 恢复）；
            //锚点只在这一处登记：占住 root 的就是链根（isRoot 由它派生）——
            //"谁算链根"全框架仅此一个判断，不存在第二个会与之打架的字段。
            //注：Init 一度接收过 parent 参数（2.0.0 开发期），但全仓没有任何一处用它表达"锚点之外的父"
            //（传的都是锚点本身，与不传等价），却在父未就绪时被静默丢弃、还逼出"覆写 Awake 后别先调
            //base.Awake()"这条陷阱式文档，故 2026-10-16 移除。
            var parent = ResolveParentBootstrap() ?? root;
            if (parent != null && parent != this)
                parent.AddBootstrap(this);
            else if (root == null)
                root = this;

            AddCoreComponents();
            //启动语义（单一、显式）：Init = 建容器 + 归属 + 映射；
            //启动（Launch）永远由调用方显式触发——推荐路径由 SimplifyBootstrap<T>.Start 补上，
            //裸派生 Bootstrap 的类需自行调用 Launch()。
            //注意 MapBindings 在 Init 内即被调用（虚调用陷阱）：派生类实例字段尚未赋值，
            //MapBindings 里只放类型绑定，值绑定请在 Init 完成后再补。
            InstantiateCoreComponents();
            isReady = true;
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
        /// 返回 null 只表示"Transform 层级上没有父"——<see cref="Init"/> 会再回落到框架级锚点
        /// <see cref="root"/>；锚点也为空时本 Bootstrap 才自任链根（见 <see cref="isRoot"/>）。
        /// </summary>
        protected virtual Bootstrap ResolveParentBootstrap()
        {
            const int LOOP_MAX = 100;
            var loopLimiter = 0;
            var trans = transform.parent;
            while (trans != null && loopLimiter < LOOP_MAX)
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

        /// 启动 Bootstrap：启动语义由派生类定义（如 SimplifyBootstrap 在此广播 StartupSignal）。
        /// 启动永远显式——推荐路径由 SimplifyBootstrap.Start 调用；
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
            //mediationBinder.Trigger(MediationEvent.AWAKE, this);
        }

        /// Add another Bootstrap to this one.
        /// 3.5：显式登记父子关系（Composite）——父持有子引用，子持有父引用，
        /// 并在两端共享同一个跨域根 binder。这样父 Dispose 可级联释放子，
        /// 子 Dispose 也可把自己从父的链上摘除。
        ///
        /// 5.6：这是"指定具体父"的唯一通道——<see cref="Init"/> 无父参数，自动解析只会走
        /// 「Transform 层级 → 框架级锚点」，挂不上锚点之外的某个 Bootstrap。
        /// 建议在 Pending Bootstrap 尚未 Init（尚未登记 .CrossContext() 绑定）时调用；
        /// 事后再认父不会迁移已经落到"原跨域根 binder"上的跨域绑定
        /// （那些绑定留在旧 binder 上，随旧 binder 一起无人清理）。
        public virtual void AddBootstrap(Bootstrap bootstrap)
        {
            ThrowIfDisposed();
            if (bootstrap == null || bootstrap == this) return;

            //5.6：转移归属前先从旧父摘除，避免出现"同一个 Bootstrap 同时是两个节点的子"
            if (bootstrap.parentBootstrap != null && bootstrap.parentBootstrap != this)
                bootstrap.parentBootstrap._children.Remove(bootstrap);

            //已登记则只保证共享桥接仍然成立（幂等）
            if (!_children.Contains(bootstrap))
                _children.Add(bootstrap);
            bootstrap.parentBootstrap = this;
            //非链根：与父共享同一个跨域根 binder（链根由锚点派生，挂上父之后自动不再成立）
            bootstrap.injectionBinder.crossContextBinder = injectionBinder.crossContextBinder;
        }

        /// Remove a bootstrap from this one.
        public void RemoveBootstrap(Bootstrap bootstrap)
        {
            if (bootstrap == null) return;
            //3.5：双向解绑——从子列表摘除并断开子的父引用
            _children.Remove(bootstrap);
            if (bootstrap.parentBootstrap == this)
            {
                bootstrap.parentBootstrap = null;
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
                child.parentBootstrap = null;
                child.Dispose();
            }
            _children.Clear();

            //3.5：双向解绑的另一半——把自己从父的子列表摘除
            if (parentBootstrap != null)
            {
                parentBootstrap._children.Remove(this);
                parentBootstrap = null;
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
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
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
            if (isReady)
                InjectViewWithoutMediation(view);
            else
                CacheView(view);
        }

        /// Remove a View from this Bootstrap
        public virtual void RemoveView(View view)
        {
            //场景卸载期 binder 可能已销毁（P0#9）；无 Mediator 机制时也无需处理
            mediationBinder?.Trigger(MediationEvent.Destroyed, view);
        }

        /// Enable a View from this Bootstrap
        public virtual void EnableView(View view)
        {
            //无 Mediator 时没有 mediator 需要 enable
            mediationBinder?.Trigger(MediationEvent.Enabled, view);
        }

        /// Disable a View from this Bootstrap
        public virtual void DisableView(View view)
        {
            //无 Mediator 时没有 mediator 需要 disable
            mediationBinder?.Trigger(MediationEvent.Disabled, view);
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

        private void OnRemove()
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
                _viewCache.MediateWith(mediationBinder);
            else
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
        /// <summary>
        /// 3.2：从 Bootstrap 抽出的"早期视图"缓存。
        ///
        /// 背景：若某个 View 在 Bootstrap 初始化完成之前就 Awake（on-stage 的 View 很常见），
        /// 它无法立刻被 Mediate，于是先缓存，等 Bootstrap 的 PostBindings 阶段统一补挂 Mediator。
        ///
        /// 原实现是一个 static 的 SemiBinding：所有 Bootstrap 共用一份缓存，
        /// 任意 Bootstrap 的 PostBindings 都会把全局缓存清空——跨 Bootstrap 互相抢视图，
        /// 同时 static 长期持有 View 引用造成跨场景泄漏。
        /// 现在改为每个 Bootstrap 持有一个实例，隔离性与释放语义都随实例走。
        /// </summary>
        private class ViewCache
        {
            private readonly List<View> _views = new();

            /// 当前缓存的 View 数量（测试与调试用）
            public int count => _views.Count;

            public void Add(View view)
            {
                if (view != null)
                    _views.Add(view);
            }

            /// <summary>
            /// 用传入的 mediationBinder 把这些"早到"的 View 补挂 Mediator，然后清空缓存。
            /// </summary>
            public void MediateWith(IMediationBinder binder)
            {
                if (binder == null)
                    throw new Exception("Bootstrap cannot mediate views without a mediationBinder");

                ForEach(v => binder.Trigger(MediationEvent.Awake, v));
            }

            /// <summary>
            /// 4.7：对缓存中的每个 View 执行一次给定动作，然后清空缓存。
            /// 供"没有 mediationBinder"的 Bootstrap 走兜底路径（注入 + 声明式解析）使用，
            /// 不再只能依赖 Mediator 机制把缓存耗尽。
            /// </summary>
            public void ForEach(Action<View> action)
            {
                if (action == null) return;

                foreach (var view in _views)
                {
                    action(view);
                }

                _views.Clear();
            }

            /// <summary>Bootstrap 销毁时清空，避免残留对已销毁 View 的引用。</summary>
            public void Clear()
            {
                _views.Clear();
            }
        }
    }
}