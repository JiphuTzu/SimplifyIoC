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
        /// In a multi-Context app, this represents the first Context to instantiate.
        public static Context firstContext;
        
        /// The top of the View hierarchy.
        /// This is your top-level GameObject
        protected Bootstrap bootstrap;

        /// If false, the `Launch()` method won't fire.
        private readonly bool _autoStartup;

        /// 3.1：Dispose 幂等标志
        private bool _disposed;
        
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

        public Context() { }

        public Context(Bootstrap view, ContextStartupFlags flags = ContextStartupFlags.Automatic)
        {
            //If firstContext was unloaded, the contextView will be null. Assign the new context as firstContext.
            if (firstContext == null || firstContext.bootstrap == null)
                firstContext = this;
            else
                firstContext.AddContext(this);
            // ReSharper disable once VirtualMemberCallInConstructor
            SetBootstrap(view);
            // ReSharper disable once VirtualMemberCallInConstructor
            AddCoreComponents();
            _autoStartup = (flags & ContextStartupFlags.ManualLaunch) != ContextStartupFlags.ManualLaunch;
            if ((flags & ContextStartupFlags.ManualMapping) != ContextStartupFlags.ManualMapping)
            {
                Start();
            }
        }

        // 原 Context(Bootstrap, bool autoMapping) 重载已删除（P0#1）：
        // 其两个取值无论怎么传都不会执行 Start()（ManualMapping 标志恒被设置），
        // 语义反了且无任何调用方。需要手动控制时请使用 ContextStartupFlags。

        protected virtual void AddCoreComponents()
        {
            //Only null if it could not find a parent context / firstContext
            injectionBinder.crossContextBinder ??= new CrossContextInjectionBinder();

            if (firstContext == this)
                injectionBinder.Bind<CrossContextBridge>().ToSingleton ().CrossContext();
            
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
            mediationBinder = injectionBinder.GetInstance<IMediationBinder>();
        }

        /// Set the object that represents the top of the Context hierarchy.
        /// In MVCSContext, this would be a GameObject.
        protected virtual void SetBootstrap(Bootstrap view)
        {
            bootstrap = view;
        }

        /// Call this from your Root to set everything in action.
        public void Start()
        {
            ThrowIfDisposed();
            InstantiateCoreComponents();
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
        public virtual void AddContext(Context context)
        {
            ThrowIfDisposed();
            context.injectionBinder.crossContextBinder = injectionBinder.crossContextBinder;
        }

        /// Remove a context from this one.
        public void RemoveContext(Context context)
        {
            context.injectionBinder.crossContextBinder = null;
            //3.1 修复：原 firstContext 分支不调用 OnRemove，
            //主 Context 销毁时 commandBinder 信号监听等清理链路从根上断裂
            if (context == firstContext)
                firstContext = null;
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

            //脱离 Context 链：firstContext 静态引用与 crossContextBinder 共享桥接
            if (firstContext == this)
            {
                firstContext = null;
            }
            else if (firstContext != null)
            {
                injectionBinder.crossContextBinder = null;
            }

            OnRemove();
            commandBinder = null;
            mediationBinder = null;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(Context));
        }

        /// Register a View with this Context
        public virtual void AddView(View view)
        {
            if (mediationBinder != null)
                mediationBinder.Trigger(MediationEvent.Awake, view);
            else
                CacheView(view);
        }

        /// Remove a View from this Context
        public virtual void RemoveView(View view)
        {
            if (mediationBinder == null) return; //场景卸载期 binder 可能已销毁（P0#9）
            mediationBinder.Trigger(MediationEvent.Destroyed, view);
        }

        /// Enable a View from this Context
        public virtual void EnableView(View view)
        {
            if (mediationBinder == null) return;
            mediationBinder.Trigger(MediationEvent.Enabled, view);
        }

        /// Disable a View from this Context
        public virtual void DisableView(View view)
        {
            if (mediationBinder == null) return;
            mediationBinder.Trigger(MediationEvent.Disabled, view);
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
            if (mediationBinder == null)
                throw new Exception("MVCSContext cannot mediate views without a mediationBinder");

            //3.2：补挂 + 清空由 ViewCache 负责（原实现操作全局 static 缓存）
            _viewCache.MediateWith(mediationBinder);
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