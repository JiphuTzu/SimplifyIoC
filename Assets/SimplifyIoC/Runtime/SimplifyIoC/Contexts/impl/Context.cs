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

using SimplifyIoC.Dispatchers;
using SimplifyIoC.Framework;
using SimplifyIoC.Injectors;

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
        private bool _autoStartup;
        

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
        protected IEventDispatcher _crossContextDispatcher;
        public virtual IDispatcher crossContextDispatcher
        {
            get => _crossContextDispatcher;
            set => _crossContextDispatcher = value as IEventDispatcher;
        }
        // private ICrossContextInjectionBinder _injectionBinder;
        private IBinder _crossContextBridge;
        public virtual IBinder crossContextBridge
        {
            get
            {
                if (_crossContextBridge == null)
                {
                    _crossContextBridge = injectionBinder.GetInstance<CrossContextBridge> ();
                }
                return _crossContextBridge;
            }
            set
            {
                _crossContextDispatcher = value as IEventDispatcher;
            }
        }

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
            if (injectionBinder.crossContextBinder == null)  //Only null if it could not find a parent context / firstContext
            {
                injectionBinder.crossContextBinder = new CrossContextInjectionBinder();
            }

            if (firstContext == this)
            {
                injectionBinder.Bind<IEventDispatcher>().To<EventDispatcher>().ToSingleton().ToName(ContextKeys.CROSS_CONTEXT_DISPATCHER).CrossContext();
                injectionBinder.Bind<CrossContextBridge> ().ToSingleton ().CrossContext();
            }
        }

        /// Override to instantiate componentry. Or just extend MVCSContext.
        protected virtual void InstantiateCoreComponents()
        {
            var dispatcherBinding = injectionBinder.GetBinding<IEventDispatcher> (ContextKeys.CONTEXT_DISPATCHER);

            if (dispatcherBinding != null) {
                var dispatcher = injectionBinder.GetInstance<IEventDispatcher> (ContextKeys.CONTEXT_DISPATCHER);

                if (dispatcher != null) {
                    crossContextDispatcher = injectionBinder.GetInstance<IEventDispatcher> (ContextKeys.CROSS_CONTEXT_DISPATCHER);
                    (crossContextDispatcher as ITriggerProvider).AddTriggerable (dispatcher as ITriggerable);
                    (dispatcher as ITriggerProvider).AddTriggerable (crossContextBridge as ITriggerable);
                }
            }
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
        protected virtual void PostBindings() { }

        /// Add another Context to this one.
        public virtual IContext AddContext(IContext context)
        {
            if (context is ICrossContextCapable)
            {
                AssignCrossContext((ICrossContextCapable)context);
            }
            return this;
        }
        public virtual void AssignCrossContext(ICrossContextCapable childContext)
        {
            childContext.crossContextDispatcher = crossContextDispatcher;
            childContext.injectionBinder.crossContextBinder = injectionBinder.crossContextBinder;
        }
        public virtual void RemoveCrossContext(ICrossContextCapable childContext)
        {
            if (childContext.crossContextDispatcher != null)
            {
                ((childContext.crossContextDispatcher) as ITriggerProvider)?.RemoveTriggerable(childContext.GetComponent<IEventDispatcher>(ContextKeys.CONTEXT_DISPATCHER) as ITriggerable);
                childContext.crossContextDispatcher = null;
            }
        }

        /// Remove a context from this one.
        public virtual IContext RemoveContext(IContext context)
        {
            if (context is ICrossContextCapable)
            {
                RemoveCrossContext((ICrossContextCapable)context);
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
            return null;
        }


        /// Retrieve a component from this Context by generic type and name
        public virtual object GetComponent<T>(object name)
        {
            return null;
        }

        /// Register a View with this Context
        public virtual void AddView(object view)
        {
            //Override in subclasses
        }

        /// Remove a View from this Context
        public virtual void RemoveView(object view)
        {
            //Override in subclasses
        }

        /// Enable a View from this Context
        public virtual void EnableView(object view)
        {
            //Override in subclasses
        }

        /// Disable a View from this Context
        public virtual void DisableView(object view)
        {
            //Override in subclasses
        }
    }
}