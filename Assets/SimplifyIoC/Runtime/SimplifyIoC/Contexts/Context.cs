/*
 * Copyright 2013 ThirdMotion, Inc.
 *
 *	Licensed under the Apache License, Version 2.0 (the "License");
 *	you may not use this file except in compliance with the License.
 *	You may obtain a copy of the License at
 *
 *		http://www.apache.org/licenses/LICENSE-2.0
 *
 *		Unless required by applicable law or agreed to in writing, software
 *		distributed under the License is distributed on an "AS IS" BASIS,
 *		WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *		See the License for the specific language governing permissions and
 *		limitations under the License.
 */



/*
 * @class SimplifyIoC.Contexts.Context
 * 
 * A Context is the entry point to the binding framework.
 * 
 * Extend this class to create the binding context suitable 
 * for your application.
 * 
 * In a typical Unity3D setup, extend MVCSContext and instantiate 
 * your extension from the ContextView.
 */

using System;
using System.Collections.Generic;
using SimplifyIoC.Commands;
using SimplifyIoC.Framework;
using SimplifyIoC.Injectors;
using SimplifyIoC.Mediations;

namespace SimplifyIoC.Contexts
{
    public enum ContextKeys
    {
        /// Marker for the named Injection of the Context
        Context,
        /// Marker for the named Injection of the ContextView
        Bootstrap
    }
    [Flags]
    public enum ContextStartupFlags
    {
        /// Context will map bindings and launch automatically (default).
        Automatic = 0,
        /// Context startup will halt after Core bindings are mapped, but before instantiation or any custom bindings.
        /// If this flag is invoked, the developer must call context.Start()
        ManualMapping = 1,
        /// Context startup will halt after all bindings are mapped, but before firing ContextEvent.START (or the analogous Signal).
        /// If this flag is invoked, the developer must call context.Launch()
        ManualLaunch = 2,
    }
    /*
     * @class SimplifyIoC.Contexts.Context
     *
     * A Context is the entry point to the binding framework.
     *
     * Implement this interface to create the binding context suitable for your application.
     *
     * In a typical Unity3D setup, an extension of MVCSContext should be instantiated from the ContextView.
     */
    
    // 3.2：不再继承 Binder——Context 自带的绑定注册表从未被使用（Context 的绑定
    // 都走 injectionBinder/commandBinder/mediationBinder 三个组件 Binder），
    // 继承只会把 Binder 的公开面错误地暴露成 Context 的 API。
    public class Context : IDisposable
    {
        /// The top of the View hierarchy.
        /// This is your top-level GameObject
        protected Bootstrap bootstrap;

        /// If false, the `Launch()` method won't fire.
        private readonly bool _autoStartup;

        /// 3.1：Dispose 幂等标志
        protected bool _disposed;

        /// 3.5：子 Context 列表（Composite）。父 Dispose 时级联释放，实现成链回收。
        /// protected 以便派生类（含测试夹具）检查链结构。
        protected readonly List<Context> _children = new List<Context>();

        /// 3.5：父 Context 引用。子 Dispose 时据此从父的子列表摘除自己，实现双向解绑。
        private Context _parentContext;

        /// 3.5：本 Context 是否为链根——只有链根持有共享 crossContextBinder
        /// （CrossContextBridge 单例与所有 .CrossContext() 绑定的宿主）。
        /// 5.6：判定不再依赖静态 firstContext，改由构造期的归属推导（见 <see cref="ResolveParentContext"/>）。
        private bool _isRoot;

        /// 5.6：划归关系的内读取入口（供测试夹具核对链结构）。<see cref="AddContext"/> / Dispose 维护它。
        internal Context parentContext => _parentContext;

        /// 5.6：本 Context 是否为链根。
        internal bool isRoot => _isRoot;
        
        /// A Binder that handles dependency injection binding and instantiation
        /// All cross-context capable contexts must implement an injectionBinder
        protected ICrossContextInjectionBinder injectionBinder { get; } = new CrossContextInjectionBinder();
        
        /// A Binder that maps Signals to Commands
        protected ICommandBinder commandBinder { get; set; }
        /// A Binder that maps Views to Mediators
        protected IMediationBinder mediationBinder { get; set; }
        /// A list of Views Awake before the Context is fully set up.
        /// 3.2：由 static SemiBinding 改为实例级 ViewCache——多 Context 互不干扰，
        /// 也不再因 static 长期持有 View 引用而跨场景泄漏。
        protected readonly ViewCache _viewCache = new ViewCache();

        /// 4.7：核心组件是否已实例化（Start 走过 InstantiateCoreComponents 之后）。
        /// 用于区分两种"没有 mediationBinder"：
        ///   ① Context 尚未就绪（早到的 View）——先缓存，等 PostBindings 统一处理；
        ///   ② Context 本就不使用 Mediator 机制——直接给 View 走注入 + 声明式解析的兜底路径。
        private bool _coreInstantiated;

        public Context() { }

        public Context(Bootstrap view, ContextStartupFlags flags = ContextStartupFlags.Automatic)
            : this(view, null, flags)
        {
        }

        /// <summary>
        /// 5.6：带显式父 Context 的构造入口。跨场景 / 跨 Prefab 组合（两边没有 Transform 层级关系，
        /// 但要共享跨域绑定）时用这个重载，或事后调用 <see cref="AddContext"/>。
        /// </summary>
        public Context(Bootstrap view, Context parent, ContextStartupFlags flags = ContextStartupFlags.Automatic)
        {
            // ReSharper disable once VirtualMemberCallInConstructor
            SetBootstrap(view);

            //5.6：归属确定顺序 —— 显式 parent > Bootstrap 层级向上查找 > 自任链根。
            //原实现用静态 firstContext："谁先构造谁当根，后来的全挂在它下面"。
            //那条链的传播方式是进程级全局状态，与 GameObject 层级毫无关系，
            //导致两个毫无关联的 Context 被隐式耦合、级联释放范围不可预测，
            //也让 View 的归属无法确定（见 View.BubbleToContext 里被一并删掉的 last-ditch 兜底）。
            // ReSharper disable once VirtualMemberCallInConstructor
            var parentContext = parent ?? ResolveParentContext(view);
            if (parentContext != null && parentContext != this)
                parentContext.AddContext(this);
            else
                _isRoot = true;

            // ReSharper disable once VirtualMemberCallInConstructor
            AddCoreComponents();
            _autoStartup = (flags & ContextStartupFlags.ManualLaunch) != ContextStartupFlags.ManualLaunch;
            if ((flags & ContextStartupFlags.ManualMapping) != ContextStartupFlags.ManualMapping)
            {
                Start();
            }
        }

        /// <summary>
        /// 5.6：沿 Transform 向上找最近的、已经持有 Context 的 Bootstrap，作为本 Context 的父。
        ///
        /// 与 View.BubbleToContext 的查找规则保持一致（同一套"向上找 Bootstrap + context 非空"的约定，
        /// 都跳过自己所在的 GameObject、都不要求中途每一层都有 Bootstrap），
        /// 这是"Context 归属 = GameObject 归属"这条单一规则的运行时依据。
        ///
        /// 返回 null 表示本 Context 是链根，据此 <see cref="AddCoreComponents"/> 决定是否创建共享跨域根 binder。
        /// </summary>
        protected virtual Context ResolveParentContext(Bootstrap view)
        {
            const int loopMax = 100;
            var loopLimiter = 0;
            var trans = view != null ? view.transform : null;
            while (trans != null && trans.parent != null && loopLimiter < loopMax)
            {
                loopLimiter++;
                trans = trans.parent;
                var parentBootstrap = trans.GetComponent<Bootstrap>();
                var candidate = parentBootstrap?.context;
                //自己的 Bootstrap 此刻 context 尚未赋值（要到 ctor 返回后才由派生 Bootstrap 写入），跳过自身即可
                if (candidate != null && candidate != this)
                    return candidate;
            }
            return null;
        }

        // 原 Context(Bootstrap, bool autoMapping) 重载已删除（P0#1）：
        // 其两个取值无论怎么传都不会执行 Start()（ManualMapping 标志恒被设置），
        // 语义反了且无任何调用方。需要手动控制时请使用 ContextStartupFlags。

        protected virtual void AddCoreComponents()
        {
            //5.6：非链根早在 AddContext 时已从父继承，这里只为链根兜底创建。
            injectionBinder.crossContextBinder ??= new CrossContextInjectionBinder();

            if (_isRoot)
            {
                //链根创建并持有共享的跨域根 binder（CrossContextBridge 单例宿主）。
                //级联释放时由它负责清理，见 Dispose。
                injectionBinder.Bind<CrossContextBridge>().ToSingleton ().CrossContext();
            }

            injectionBinder.Bind<IInstanceProvider>().Bind<IInjectionBinder>().ToValue(injectionBinder);
            injectionBinder.Bind<Context>().ToValue(this).ToName(ContextKeys.Context);
            injectionBinder.Bind<ICommandBinder>().To<CommandBinder>().ToSingleton();
            injectionBinder.Bind<IMediationBinder>().To<MediationBinder>().ToSingleton();
        }

        /// Override to instantiate componentry. Or just extend MVCSContext.
        protected virtual void InstantiateCoreComponents()
        {
            injectionBinder.Bind<Bootstrap>().ToValue(bootstrap).ToName(ContextKeys.Bootstrap);
            commandBinder = injectionBinder.GetInstance<ICommandBinder>();
            //4.7：mediationBinder 允许缺失。不使用 Mediator 机制的 Context（派生类未绑定
            //IMediationBinder）不再在这里因"no binding"抛异常，而是留 null，
            //由 View 的兜底路径接管（见 AddView / MediateViewCache）。
            mediationBinder = injectionBinder.GetBinding<IMediationBinder>() != null
                ? injectionBinder.GetInstance<IMediationBinder>()
                : null;
        }

        /// Set the object that represents the top of the Context hierarchy.
        /// In MVCSContext, this would be a GameObject.
        protected virtual void SetBootstrap(Bootstrap view)
        {
            bootstrap = view;
            //5.6：回写 Bootstrap → Context 归属（详见 Bootstrap.AttachContext 的说明）。
            //必须在 ResolveParentContext 之前完成：更晚构造的 Context 依赖祖先 Bootstrap
            //已经持有自己的 Context 才能找到父节点。
            if (view != null)
                view.AttachContext(this);
        }

        /// Call this from your Root to set everything in action.
        public void Start()
        {
            ThrowIfDisposed();
            InstantiateCoreComponents();
            _coreInstantiated = true;
            MapBindings();
            PostBindings();
            if (_autoStartup)
                Launch();
        }

        /// The final method to fire after mappings.
        /// If autoStartup is false, you need to call this manually.
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
            //Ensure that all Views underneath the ContextView are triggered
            //mediationBinder.Trigger(MediationEvent.AWAKE, _contextView);
        }

        /// Add another Context to this one.
        /// 3.5：显式登记父子关系（Composite）——父持有子引用，子持有父引用，
        /// 并在两端共享同一个跨域根 binder。这样父 Dispose 可级联释放子，
        /// 子 Dispose 也可把自己从父的链上摘除。
        ///
        /// 5.6：建议在 Pending Context 尚未进入 Start（尚未登记 .CrossContext() 绑定）时调用——
        /// 通常就是走带 parent 的构造重载。事后再认父不会迁移已经落到"原跨域根 binder"上的
        /// 跨域绑定（那些绑定留在旧 binder 上，随旧 binder 一起无人清理）。
        public virtual void AddContext(Context context)
        {
            ThrowIfDisposed();
            if (context == null || context == this) return;

            //5.6：转移归属前先从旧父摘除，避免出现"同一个 Context 同时是两个节点的子"
            if (context._parentContext != null && context._parentContext != this)
                context._parentContext._children.Remove(context);

            //已登记则只保证共享桥接仍然成立（幂等）
            if (!_children.Contains(context))
                _children.Add(context);
            context._parentContext = this;
            //非链根：与父共享同一个跨域根 binder
            if (context._isRoot && context != this)
                context._isRoot = false;
            context.injectionBinder.crossContextBinder = injectionBinder.crossContextBinder;
        }

        /// Remove a context from this one.
        public void RemoveContext(Context context)
        {
            if (context == null) return;
            //3.5：双向解绑——从子列表摘除并断开子的父引用
            _children.Remove(context);
            if (context._parentContext == this)
            {
                context._parentContext = null;
                //脱离父链后自己就是链根：此后它的 Dispose 要负责清理共享跨域根 binder（若共享）
                context._isRoot = true;
            }
            context.injectionBinder.crossContextBinder = null;
            context.OnRemove();
        }

        /// <summary>
        /// 3.1：销毁契约（幂等）。脱离 Context 链、执行 OnRemove 清理
        /// （信号监听移除、命令池释放、三个 Binder 注册表清空）。
        /// Dispose 后再 Start/Launch/AddContext/RemoveContext 抛 ObjectDisposedException；
        /// 再经 injectionBinder GetInstance 因注册表已空按既有行为抛"no binding"异常。
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            //3.5：级联释放子 Context（Composite 成链回收）。
            //倒序 + 先断开子侧的父引用，避免子 Dispose 时反向再操作本对象。
            for (var i = _children.Count - 1; i >= 0; i--)
            {
                var child = _children[i];
                if (child == null) continue;
                child._parentContext = null;
                child.Dispose();
            }
            _children.Clear();

            //3.5：双向解绑的另一半——把自己从父的子列表摘除
            if (_parentContext != null)
            {
                _parentContext._children.Remove(this);
                _parentContext = null;
            }

            //3.5：无论角色如何都断开共享桥接。
            //链根额外负责清空共享的跨域根 binder —— 它是 CrossContextBridge 单例与
            //所有 .CrossContext() 绑定的宿主，不清空则整条链的跨域单例都会泄漏
            //（子已在上方全部级联释放，此处清空不会影响其它存活 Context）。
            var sharedBinder = injectionBinder.crossContextBinder;
            if (_isRoot && sharedBinder != null)
                (sharedBinder as IBinder)?.OnRemove();
            injectionBinder.crossContextBinder = null;

            OnRemove();
            commandBinder = null;
            mediationBinder = null;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(Context));
        }

        /// Register a View with this Context
        /// 4.7：View 的注入与声明式解析（[Child]/[BindEvent]/[BindMethod]/[MainThread]）
        /// 都不依赖 Mediator——没有 mediationBinder 时走兜底路径，View 照常可用。
        public virtual void AddView(View view)
        {
            if (view == null) return;
            //5.6：归属在注册成功这一刻确定，之后 View 的 Remove/Enable/Disable 直接发回本 Context，
            //不再逐次遍历 Transform 链，也不再有"随便找一个 Context"的余地。
            view.SetOwningContext(this);

            if (mediationBinder != null)
            {
                mediationBinder.Trigger(MediationEvent.Awake, view);
                return;
            }

            //Context 已就绪却仍无 mediationBinder ⇒ 本 Context 不使用 Mediator 机制。
            //尚未就绪则说明是"早到的 View"，照旧缓存，等 PostBindings 统一处理。
            if (_coreInstantiated)
                InjectViewWithoutMediation(view);
            else
                CacheView(view);
        }

        /// Remove a View from this Context
        public virtual void RemoveView(View view)
        {
            if (mediationBinder == null) return; //场景卸载期 binder 可能已销毁（P0#9）；无 Mediator 机制时也无需处理
            mediationBinder.Trigger(MediationEvent.Destroyed, view);
        }

        /// Enable a View from this Context
        public virtual void EnableView(View view)
        {
            if (mediationBinder == null) return; //无 Mediator 时没有 mediator 需要 enable
            mediationBinder.Trigger(MediationEvent.Enabled, view);
        }

        /// Disable a View from this Context
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
            //3.2：不再有 base.OnRemove()——Context 已不继承 Binder。
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
            //不依赖 Mediator，改走兜底路径，使"不使用 Mediator 机制"的 Context 也能驱动 View。
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
        /// View to be Awake before this Context has finished initing.
        /// `cacheView()` maintains a list of such 'early-risers'
        /// until the Context is ready to mediate them.
        protected virtual void CacheView(View view)
        {
            //3.2：原 SemiBinding 的 One→Many 约束逻辑随 static 缓存一并移除
            _viewCache.Add(view);
        }
    }
}