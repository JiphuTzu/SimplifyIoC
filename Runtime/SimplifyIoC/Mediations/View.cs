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
using SimplifyIoC.Utils;
using UnityEngine;

namespace SimplifyIoC.Mediations
{
    public abstract class View : MonoBehaviour
    {
        /// Determines the type of event the View is bubbling to the Context
        protected enum BubbleType
        {
            Add,
            Remove,
            Enable,
            Disable
        }
        
        [Inject]
        public IInjectionBinder injectionBinder { get; set; }
        /// Leave this value true most of the time. If for some reason you want
        /// a view to exist outside a context you can set it to false. The only
        /// difference is whether an error gets generated.
        public bool requiresContext { get; set; } = true;
        
        /// A flag for allowing the View to register with the Context
        /// In general you can ignore this. But some developers have asked for a way of disabling
        ///  View registration with a checkbox from Unity, so here it is.
        /// If you want to expose this capability either
        /// (1) uncomment the commented-out line immediately below, or
        /// (2) subclass View and override the autoRegisterWithContext method using your own custom (public) field.
        //[SerializeField]
        public virtual bool autoRegisterWithContext { get; set; } = true;

        public bool registeredWithContext { get; set; }
        
        public bool shouldRegister => enabled && gameObject.activeInHierarchy;

        /// 4.1：解析只跑一次的哨兵。UnityEvent.AddListener 不去重，二次解析会让回调执行两次。
        private bool _attributesInitialized;

        /// 5.1：订阅组（惰性创建，从未订阅过则不分配）。
        private SignalSubscriptionGroup _subscriptions;

        /// <summary>
        /// 5.1：随本 View 生命周期的订阅组。OnDestroy 时自动退订组内全部订阅。
        /// View 现在可以没有 Mediator（4.7 起的契约），因此这条回收不能挂在 Mediator 上——
        /// 和 Mediator 的 subscriptions 是两条独立的组，各自负责自己的订阅。
        ///
        ///     subscriptions.Listen(someSignal, OnSomething);   //随 View 销毁自动退订
        /// </summary>
        protected SignalSubscriptionGroup subscriptions => _subscriptions ??= new SignalSubscriptionGroup();

        /// A MonoBehaviour Awake handler.
        /// The View will attempt to connect to the Context at this moment.
        protected virtual void Awake()
        {
            //4.1：解析时机从"Awake 最早处"挪到"注入完成之后"（由 MediationBinder 调用
            //EnsureAttributesInitialized）。原因是 [BindEvent(nameof(x))] 的 x 可能是 [Inject] 成员，
            //Awake 时尚未注入。
            //4.7：因此 Awake 只对"明确不向 Context 注册"的 View 兜底解析——这类 View 拿不到
            //注入，也不会有人替它解析。其余 View 交给框架：有 Context 时由 MediationBinder 在
            //注入后解析，无 Context 时由 Start 兜底（见下）。
            if (!autoRegisterWithContext)
                EnsureAttributesInitialized();

            if (autoRegisterWithContext && !registeredWithContext && shouldRegister)
                BubbleToContext(BubbleType.Add, false);
        }

        /// <summary>
        /// 4.1：声明式绑定解析。框架在注入完成后自动调用一次（见 MediationBinder）。
        /// 子类覆写此方法即可扩展解析内容，无需关心调用时机。
        /// </summary>
        protected virtual void InitAttributes()
        {
            //4.4：[BindMethod] 已随静态注册表治理（Dictionary → ConditionalWeakTable）一并接入。
            //顺序约束：必须先把表换成弱引用，否则自动解析会把"偶发泄漏"变成"必然泄漏"。
            var bindMethodParser = this.GetBindMethodParser();
            var reflected = this.AddAttributeParser(this.GetEventMethodParser())
                .AddAttributeParser(this.GetChildParser())
                .AddAttributeParser(this.GetMainThreadParser());
            //判空是防御性的：GetBindMethodParser 现恒返回委托，但 null 委托传给
            //AddAttributeParser 会在 CreateParser 里因 parser.Method 立即 NRE，不值得赌。
            if (bindMethodParser != null)
                reflected = reflected.AddAttributeParser(bindMethodParser);
            reflected.ParseAttributes();
        }

        /// <summary>
        /// 4.1：InitAttributes 的幂等包装。注入完成后由框架调用；Awake 兜底路径也走这里。
        /// </summary>
        internal void EnsureAttributesInitialized()
        {
            if (_attributesInitialized) return;
            _attributesInitialized = true;
            InitAttributes();
        }

        /// A MonoBehaviour Start handler
        /// If the View is not yet registered with the Context, it will 
        /// attempt to connect again at this moment.
        protected virtual void Start()
        {
            if (autoRegisterWithContext && !registeredWithContext && shouldRegister)
                BubbleToContext(BubbleType.Add, true);

            //4.7：走到这里仍未注册到任何 Context ⇒ 不会有任何人替它做"注入后解析"。
            //自行兜底一次（幂等），使 View 在没有 Mediator / 没有 Context 时，
            //[Child] / [BindEvent] / [BindMethod] / [MainThread] 依然照常生效。
            //（requiresContext 为 true 的 View 在上一行就会抛异常，走不到这里——这是既有契约。）
            if (!registeredWithContext)
                EnsureAttributesInitialized();
        }

        /// A MonoBehaviour OnDestroy handler
        /// The View will inform the Context that it is about to be
        /// destroyed.
        protected virtual void OnDestroy()
        {
            //5.1：先退订组内订阅（此处执行回调移除，避免后续 BubbleToContext 期间误触发）。
            _subscriptions?.Dispose();
            //4.4：立即释放本组件的 [BindMethod] 静态注册表条目（不依赖 GC 时机）。
            //静态表本身已改为 ConditionalWeakTable，这里是"及时性"而非"正确性"的补充。
            this.UnbindMethods();
            BubbleToContext(BubbleType.Remove, false);
        }

        /// A MonoBehaviour OnEnable handler
        /// The View will inform the Context that it was enabled
        protected virtual void OnEnable()
        {
            BubbleToContext(BubbleType.Enable, false);
        }

        /// A MonoBehaviour OnDisable handler
        /// The View will inform the Context that it was disabled
        protected virtual void OnDisable()
        {
            BubbleToContext(BubbleType.Disable, false);
        }

        /// Recurses through Transform.parent to find the GameObject to which ContextView is attached
        /// Has a loop limit of 100 levels.
        /// By default, raises an Exception if no Context is found.
        protected void BubbleToContext(BubbleType type, bool finalTry)
        {
            const int LOOP_MAX = 100;
            var loopLimiter = 0;
            var trans = transform;
            while (trans.parent != null && loopLimiter < LOOP_MAX)
            {
                loopLimiter++;
                trans = trans.parent;
                var contextView = trans.GetComponent<Bootstrap>();
                if (contextView != null && contextView.context != null)
                {
                    var context = contextView.context;
                    switch (type)
                    {
                        case BubbleType.Add:
                            context.AddView(this);
                            registeredWithContext = true;
                            return;
                        case BubbleType.Remove:
                            context.RemoveView(this);
                            return;
                        case BubbleType.Enable:
                            context.EnableView(this);
                            return;
                        case BubbleType.Disable:
                            context.DisableView(this);
                            return;
                        default:
                            return;
                    }
                }
            }

            if (!requiresContext || !finalTry || type != BubbleType.Add) return;
            //last ditch. If there's a Context anywhere, we'll use it!
            if (Context.firstContext != null)
            {
                Context.firstContext.AddView(this);
                registeredWithContext = true;
                return;
            }

            var msg = loopLimiter == LOOP_MAX ?
                "A view couldn't find a context. Loop limit reached." :
                "A view was added with no context. Views must be added into the hierarchy of their ContextView lest all hell break loose.";
            msg += "\nView: " + this;
            throw new Exception(msg);
        }
    }
}
