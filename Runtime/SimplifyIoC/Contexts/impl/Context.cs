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

using SimplifyIoC.Commands;
using SimplifyIoC.Framework;
using SimplifyIoC.Injectors;
using SimplifyIoC.Mediations;
using UnityEngine;

namespace SimplifyIoC.Contexts
{
    
    public class Context : Binder, IContext, ICrossContextCapable
    {
        /// The top of the View hierarchy.
        /// In MVCSContext, this is your top-level GameObject
        protected IContextView contextView { get; set; }

        /// In a multi-Context app, this represents the first Context to instantiate.
        public static IContext firstContext;

        /// If false, the `Launch()` method won't fire.
        private readonly bool _autoStartup;
        

        /// A Binder that handles dependency injection binding and instantiation
        public ICrossContextInjectionBinder injectionBinder { get; set;} = new CrossContextInjectionBinder();
        
        // A specific instance of EventDispatcher that communicates 
        // across multiple contexts. An event sent across this 
        // dispatcher will be re-dispatched by the various context-wide 
        // dispatchers. So a dispatch to other contexts is simply 
        // 
        // `crossContextDispatcher.Dispatch(MY_EVENT, payload)`;
        // 
        // Other contexts don't need to listen to the cross-context dispatcher
        // as such, just map the necessary event to your local context
        // dispatcher and you'll receive it.
        // protected IEventDispatcher _crossContextDispatcher;
        // public virtual IDispatcher crossContextDispatcher
        // {
        //     get => _crossContextDispatcher;
        //     set => _crossContextDispatcher = value as IEventDispatcher;
        // }
        // private ICrossContextInjectionBinder _injectionBinder;
        // private IBinder _crossContextBridge;
        // public virtual IBinder crossContextBridge
        // {
        //     get
        //     {
        //         if (_crossContextBridge == null)
        //         {
        //             _crossContextBridge = injectionBinder.GetInstance<CrossContextBridge> ();
        //         }
        //         return _crossContextBridge;
        //     }
        //     set
        //     {
        //         //_crossContextDispatcher = value as IEventDispatcher;
        //     }
        // }
        /// A Binder that maps Signals to Commands
        public ICommandBinder commandBinder { get; set; }
        /// A Binder that maps Views to Mediators
        public IMediationBinder mediationBinder { get; set; }
        /// A list of Views Awake before the Context is fully set up.
        protected static ISemiBinding viewCache = new SemiBinding();

        public Context() { }

        public Context(IContextView view, ContextStartupFlags flags = ContextStartupFlags.AUTOMATIC)
        {
            //If firstContext was unloaded, the contextView will be null. Assign the new context as firstContext.
            if (firstContext == null || firstContext.GetContextView() == null)
            {
                firstContext = this;
            }
            else
            {
                firstContext.AddContext(this);
            }
            SetContextView(view);
            AddCoreComponents();
            _autoStartup = (flags & ContextStartupFlags.MANUAL_LAUNCH) != ContextStartupFlags.MANUAL_LAUNCH;
            if ((flags & ContextStartupFlags.MANUAL_MAPPING) != ContextStartupFlags.MANUAL_MAPPING)
            {
                Start();
            }
        }

        public Context(IContextView view, bool autoMapping)
            : this(view, autoMapping ? ContextStartupFlags.MANUAL_MAPPING : ContextStartupFlags.MANUAL_LAUNCH | ContextStartupFlags.MANUAL_MAPPING)
        {
        }

        /// Override to add componentry. Or just extend MVCSContext.
        protected virtual void AddCoreComponents()
        {
            //Only null if it could not find a parent context / firstContext
            injectionBinder.crossContextBinder ??= new CrossContextInjectionBinder();

            if (firstContext == this)
                injectionBinder.Bind<CrossContextBridge>().ToSingleton ().CrossContext();
            
            injectionBinder.Bind<IInstanceProvider>().Bind<IInjectionBinder>().ToValue(injectionBinder);
            injectionBinder.Bind<IContext>().ToValue(this).ToName(ContextKeys.CONTEXT);
            injectionBinder.Bind<ICommandBinder>().To<SignalCommandBinder>().ToSingleton();
            injectionBinder.Bind<IMediationBinder>().To<SignalMediationBinder>().ToSingleton();
        }

        /// Override to instantiate componentry. Or just extend MVCSContext.
        protected virtual void InstantiateCoreComponents()
        {
            injectionBinder.Bind<ContextView>().ToValue(contextView).ToName(ContextKeys.CONTEXT_VIEW);
            commandBinder = injectionBinder.GetInstance<ICommandBinder>();
            mediationBinder = injectionBinder.GetInstance<IMediationBinder>();
        }

        /// Set the object that represents the top of the Context hierarchy.
        /// In MVCSContext, this would be a GameObject.
        public virtual IContext SetContextView(IContextView view)
        {
            contextView = view;
            return this;
        }

        public virtual IContextView GetContextView()
        {
            return contextView;
        }

        /// Call this from your Root to set everything in action.
        public virtual IContext Start()
        {
            InstantiateCoreComponents();
            MapBindings();
            PostBindings();
            if (_autoStartup)
                Launch();
            return this;
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
            mediationBinder.Trigger(MediationEvent.AWAKE, contextView);
        }

        /// Add another Context to this one.
        public virtual IContext AddContext(IContext context)
        {
            if (context is ICrossContextCapable capable)
            {
                AssignCrossContext(capable);
            }
            return this;
        }
        public virtual void AssignCrossContext(ICrossContextCapable childContext)
        {
            //childContext.crossContextDispatcher = crossContextDispatcher;
            childContext.injectionBinder.crossContextBinder = injectionBinder.crossContextBinder;
        }
        public virtual void RemoveCrossContext(ICrossContextCapable childContext)
        {
            // if (childContext.crossContextDispatcher != null)
            // {
            //     ((childContext.crossContextDispatcher) as ITriggerProvider)?.RemoveTriggerable(childContext.GetComponent<IEventDispatcher>(ContextKeys.CONTEXT_DISPATCHER) as ITriggerable);
            //     childContext.crossContextDispatcher = null;
            // }
            childContext.injectionBinder.crossContextBinder = null;
        }

        /// Remove a context from this one.
        public virtual IContext RemoveContext(IContext context)
        {
            if (context is ICrossContextCapable capable)
            {
                RemoveCrossContext(capable);
            }
            //If we're removing firstContext, set firstContext to null
            if (context == firstContext)
            {
                firstContext = null;
            }
            else
            {
                context.OnRemove();
            }
            return this;
        }

        /// Retrieve a component from this Context by generic type
        public virtual object GetComponent<T>()
        {
            return GetComponent<T>(null);
        }


        /// Retrieve a component from this Context by generic type and name
        public virtual object GetComponent<T>(object name)
        {
            var binding = injectionBinder.GetBinding<T>(name);
            if (binding != null)
            {
                return injectionBinder.GetInstance<T>(name);
            }
            return null;
        }

        /// Register a View with this Context
        public virtual void AddView(object view)
        {
            if (mediationBinder != null)
            {
                mediationBinder.Trigger(MediationEvent.AWAKE, view as IView);
            }
            else
            {
                CacheView(view as MonoBehaviour);
            }
        }

        /// Remove a View from this Context
        public virtual void RemoveView(object view)
        {
            mediationBinder.Trigger(MediationEvent.DESTROYED, view as IView);
        }

        /// Enable a View from this Context
        public virtual void EnableView(object view)
        {
            mediationBinder.Trigger(MediationEvent.ENABLED, view as IView);
        }

        /// Disable a View from this Context
        public virtual void DisableView(object view)
        {
            mediationBinder.Trigger(MediationEvent.DISABLED, view as IView);
        }

        public override void OnRemove()
        {
            base.OnRemove();
            commandBinder.OnRemove();
        }

        protected virtual void MediateViewCache()
        {
            if (mediationBinder == null)
                throw new ContextException("MVCSContext cannot mediate views without a mediationBinder", ContextExceptionType.NO_MEDIATION_BINDER);

            var values = viewCache.value as object[];
            if (values == null)
            {
                return;
            }
            var aa = values.Length;
            for (var a = 0; a < aa; a++)
            {
                mediationBinder.Trigger(MediationEvent.AWAKE, values[a] as IView);
            }
            viewCache = new SemiBinding();
        }
        /// Caches early-riser Views.
        /// 
        /// If a View is on stage at startup, it's possible for that
        /// View to be Awake before this Context has finished initing.
        /// `cacheView()` maintains a list of such 'early-risers'
        /// until the Context is ready to mediate them.
        protected virtual void CacheView(MonoBehaviour view)
        {
            if (viewCache.constraint.Equals(BindingConstraintType.ONE))
            {
                viewCache.constraint = BindingConstraintType.MANY;
            }
            viewCache.Add(view);
        }
    }
}