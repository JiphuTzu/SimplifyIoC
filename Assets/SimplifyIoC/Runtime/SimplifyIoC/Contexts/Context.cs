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
    
    public class Context : Binder
    {
        /// In a multi-Context app, this represents the first Context to instantiate.
        public static Context firstContext;
        
        /// The top of the View hierarchy.
        /// This is your top-level GameObject
        protected Bootstrap bootstrap;

        /// If false, the `Launch()` method won't fire.
        private readonly bool _autoStartup;
        
        /// A Binder that handles dependency injection binding and instantiation
        /// All cross-context capable contexts must implement an injectionBinder
        protected ICrossContextInjectionBinder injectionBinder { get; } = new CrossContextInjectionBinder();
        
        /// A Binder that maps Signals to Commands
        protected ICommandBinder commandBinder { get; set; }
        /// A Binder that maps Views to Mediators
        protected IMediationBinder mediationBinder { get; set; }
        /// A list of Views Awake before the Context is fully set up.
        private static ISemiBinding _viewCache = new SemiBinding();

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

        public Context(Bootstrap view, bool autoMapping)
            : this(view, autoMapping ? ContextStartupFlags.ManualMapping : ContextStartupFlags.ManualLaunch | ContextStartupFlags.ManualMapping)
        {
        }

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
            InstantiateCoreComponents();
            MapBindings();
            PostBindings();
            if (_autoStartup)
                Launch();
        }

        /// The final method to fire after mappings.
        /// If autoStartup is false, you need to call this manually.
        public virtual void Launch() { }

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
            context.injectionBinder.crossContextBinder = injectionBinder.crossContextBinder;
        }

        /// Remove a context from this one.
        public void RemoveContext(Context context)
        {
            context.injectionBinder.crossContextBinder = null;
            //If we're removing firstContext, set firstContext to null
            if (context == firstContext)
                firstContext = null;
            else
                context.OnRemove();
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
            mediationBinder.Trigger(MediationEvent.Destroyed, view);
        }

        /// Enable a View from this Context
        public virtual void EnableView(View view)
        {
            mediationBinder.Trigger(MediationEvent.Enabled, view);
        }

        /// Disable a View from this Context
        public virtual void DisableView(View view)
        {
            mediationBinder.Trigger(MediationEvent.Disabled, view);
        }

        public override void OnRemove()
        {
            base.OnRemove();
            commandBinder.OnRemove();
        }

        protected virtual void MediateViewCache()
        {
            if (mediationBinder == null)
                throw new Exception("MVCSContext cannot mediate views without a mediationBinder");

            var values = _viewCache.value as object[];
            if (values == null)
            {
                return;
            }
            var aa = values.Length;
            for (var a = 0; a < aa; a++)
            {
                mediationBinder.Trigger(MediationEvent.Awake, values[a] as View);
            }
            _viewCache = new SemiBinding();
        }
        /// Caches early-riser Views.
        /// 
        /// If a View is on stage at startup, it's possible for that
        /// View to be Awake before this Context has finished initing.
        /// `cacheView()` maintains a list of such 'early-risers'
        /// until the Context is ready to mediate them.
        protected virtual void CacheView(View view)
        {
            if (_viewCache.constraint.Equals(BindingConstraintType.One))
            {
                _viewCache.constraint = BindingConstraintType.Many;
            }
            _viewCache.Add(view);
        }
    }
}