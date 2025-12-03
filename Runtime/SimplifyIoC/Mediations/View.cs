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
 * @class SimplifyIoC.Mediations.View
 * 
 * Parent class for all your Views. Extends MonoBehaviour.
 * Bubbles its Awake, Start and OnDestroy events to the
 * ContextView, which allows the Context to know when these
 * critical moments occur in the View lifecycle.
 */

using System;
using SimplifyIoC.Contexts;
using SimplifyIoC.Injectors;
using SimplifyIoC.Signals;
using UnityEngine;

namespace SimplifyIoC.Mediations
{
    public abstract class View : MonoBehaviour, IView
    {
        [Inject]
        public IInjectionBinder injectionBinder { get; set; }
        /// Leave this value true most of the time. If for some reason you want
        /// a view to exist outside a context you can set it to false. The only
        /// difference is whether an error gets generated.
        public bool requiresContext { get; set; } = true;

        /// Determines the type of event the View is bubbling to the Context
        protected enum BubbleType
        {
            Add,
            Remove,
            Enable,
            Disable
        }

        /// A flag for allowing the View to register with the Context
        /// In general you can ignore this. But some developers have asked for a way of disabling
        ///  View registration with a checkbox from Unity, so here it is.
        /// If you want to expose this capability either
        /// (1) uncomment the commented-out line immediately below, or
        /// (2) subclass View and override the autoRegisterWithContext method using your own custom (public) field.
        //[SerializeField]
        public virtual bool autoRegisterWithContext { get; set; } = true;

        public bool registeredWithContext { get; set; }

        /// A MonoBehaviour Awake handler.
        /// The View will attempt to connect to the Context at this moment.
        protected virtual void Awake()
        {
            InitAttributes();
            if (autoRegisterWithContext && !registeredWithContext && shouldRegister)
                BubbleToContext(this, BubbleType.Add, false);
        }

        protected virtual void InitAttributes()
        {
            // this.AddAttributeParser(this.GetEventFieldParser())
            //     .AddAttributeParser(this.GetEventPropertyParser())
            //     .AddAttributeParser(this.GetEventMethodParser())
            //     .AddAttributeParser(this.GetMainThreadParser())
            //     .ParseAttributes();
        }

        protected T Get<T>() where T : BaseSignal
        {
            return injectionBinder.GetInstance<T>();
        }

        /// A MonoBehaviour Start handler
        /// If the View is not yet registered with the Context, it will 
        /// attempt to connect again at this moment.
        protected virtual void Start()
        {
            if (autoRegisterWithContext && !registeredWithContext && shouldRegister)
                BubbleToContext(this, BubbleType.Add, true);
        }

        /// A MonoBehaviour OnDestroy handler
        /// The View will inform the Context that it is about to be
        /// destroyed.
        protected virtual void OnDestroy()
        {
            BubbleToContext(this, BubbleType.Remove, false);
        }

        /// A MonoBehaviour OnEnable handler
        /// The View will inform the Context that it was enabled
        protected virtual void OnEnable()
        {
            BubbleToContext(this, BubbleType.Enable, false);
        }

        /// A MonoBehaviour OnDisable handler
        /// The View will inform the Context that it was disabled
        protected virtual void OnDisable()
        {
            BubbleToContext(this, BubbleType.Disable, false);
        }

        /// Recurses through Transform.parent to find the GameObject to which ContextView is attached
        /// Has a loop limit of 100 levels.
        /// By default, raises an Exception if no Context is found.
        protected void BubbleToContext(MonoBehaviour view, BubbleType type, bool finalTry)
        {
            const int LOOP_MAX = 100;
            var loopLimiter = 0;
            var trans = view.gameObject.transform;
            while (trans.parent != null && loopLimiter < LOOP_MAX)
            {
                loopLimiter++;
                trans = trans.parent;
                var contextView = trans.gameObject.GetComponent<ContextView>();
                if (contextView != null && contextView.context != null)
                {
                    var context = contextView.context;
                    switch (type)
                    {
                        case BubbleType.Add:
                            context.AddView(view);
                            registeredWithContext = true;
                            break;
                        case BubbleType.Remove:
                            context.RemoveView(view);
                            break;
                        case BubbleType.Enable:
                            context.EnableView(view);
                            break;
                        case BubbleType.Disable:
                            context.DisableView(view);
                            break;
                        default:
                            return;
                    }
                }
            }
            if (requiresContext && finalTry && type == BubbleType.Add)
            {
                //last ditch. If there's a Context anywhere, we'll use it!
                if (Context.firstContext != null)
                {
                    Context.firstContext.AddView(view);
                    registeredWithContext = true;
                    return;
                }

                var msg = (loopLimiter == LOOP_MAX) ?
                    "A view couldn't find a context. Loop limit reached." :
                        "A view was added with no context. Views must be added into the hierarchy of their ContextView lest all hell break loose.";
                msg += "\nView: " + view;
                throw new Exception(msg);
            }
        }

        public bool shouldRegister => enabled && gameObject.activeInHierarchy;
    }
}
