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

        /// 5.6：本 View 的归属 Context，在注册成功那一刻确定（<see cref="Context.AddView"/>），
        /// 之后 Remove / Enable / Disable 一律直接发给它，不再逐次向上遍历 Transform 链。
        ///
        /// 改前每次生命周期事件都要重新走一遍 100 层上限的向上查找，且查出来的 Context
        /// 未必是当初做 Mediate 的那个（中途被重新挂到别的父节点下时尤其容易错），
        /// 更有一条"找不到就挂到 Context.firstContext"的全局暗道——现与那条暗道一并删除。
        public Context owningContext { get; private set; }

        /// 5.6：由 Context 在一锤定音的注册点写入（不要从别处调用）。
        internal void SetOwningContext(Context context)
        {
            owningContext = context;
        }

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
            if (autoRegisterWithContext && !registeredWithContext && shouldRegister)
                BubbleToContext(BubbleType.Add, false);

            //4.1：解析时机在"注入完成之后"（由 MediationBinder 调用 EnsureAttributesInitialized）。
            //4.7：只有明确不向 Context 注册、且此刻确实没有归属的 View 才在 Awake 兜底解析——
            //5.6：判定挪到注册之后。"是否会注册"这件事到这里已经确定，
            //不再靠"看一眼 autoRegisterWithContext 就猜"来决定要不要抢跑，
            //消除了"声明不注册、事实上却被注册（例如被其它脚本改变 autoRegisterWithContext，
            //或被显式 AddView）的 View 在注入之前就被抢先解析"这一类错配。
            if (!autoRegisterWithContext && owningContext == null)
                EnsureAttributesInitialized();
        }

        /// <summary>
        /// 5.6：从 <see cref="injectionBinder"/> 取一个实例（绝大多数场景就是取信号单例）。
        /// 等价于 injectionBinder.GetInstance&lt;T&gt;()，省掉每个 View 都要写一遍的容器往返。
        ///
        ///     [Inject] 之外的轻量写法：startSignal = Get&lt;StartSignal&gt;();
        /// 没有归属容器时返回 null（孤儿 View 的既有契约，不抛）。
        /// </summary>
        protected T Get<T>() where T : BaseSignal
        {
            return injectionBinder != null ? injectionBinder.GetInstance<T>() : null;
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
            if (owningContext == null)
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
            //5.6：通知发出后断开归属引用，避免已销毁的 View 继续 root 住整个 Context 链
            owningContext = null;
            registeredWithContext = false;
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

        /// <summary>
        /// 把生命周期事件交给所属的 Context。
        ///
        /// 5.6：只有 Add 会发生"向上查找"（归属尚未确定）；Remove / Enable / Disable
        /// 一律发给注册时记下的 <see cref="owningContext"/>—— Mediate 是谁做的，通知就得回到谁那里，
        /// 中间不管 GameObject 被挂到什么地方。
        /// </summary>
        protected void BubbleToContext(BubbleType type, bool finalTry)
        {
            switch (type)
            {
                case BubbleType.Add:
                    ResolveAndRegister(finalTry);
                    return;
                case BubbleType.Remove:
                    owningContext?.RemoveView(this);
                    return;
                case BubbleType.Enable:
                    owningContext?.EnableView(this);
                    return;
                case BubbleType.Disable:
                    owningContext?.DisableView(this);
                    return;
                default:
                    return;
            }
        }

        /// <summary>
        /// 沿 Transform 向上找最近一个已持有 Context 的 Bootstrap，注册并记下归属。
        /// 找不到时：Awake 阶段不动声色（finalTry=false，Start 还有一次机会）；
        /// Start 阶段若 requiresContext 则按既有契约抛异常。
        /// </summary>
        private void ResolveAndRegister(bool finalTry)
        {
            const int LOOP_MAX = 100;
            var loopLimiter = 0;
            var trans = transform;
            while (trans.parent != null && loopLimiter < LOOP_MAX)
            {
                loopLimiter++;
                trans = trans.parent;
                var contextBootstrap = trans.GetComponent<Bootstrap>();
                var context = contextBootstrap?.context;
                if (context != null)
                {
                    context.AddView(this);
                    registeredWithContext = true;
                    return;
                }
            }

            if (!requiresContext || !finalTry) return;
            //5.6：原实现在这里有一条"last ditch"——找不到任何 Context 时把 View 挂到
            //Context.firstContext（谁先构造谁当兜底）。那条暗道让 View 的归属彻底失控：
            //一个层级上毫无关系的 Context 会莫名其妙地 Mediate 这个 View，
            //连级联 Dispose 与跨域单例都会被牵扯进来。已删除：找不到归属就按契约抛。
            var msg = loopLimiter == LOOP_MAX ?
                "A view couldn't find a context. Loop limit reached." :
                "A view was added with no context. Views must be added into the hierarchy of their ContextView lest all hell break loose.";
            msg += "\nView: " + this;
            throw new Exception(msg);
        }
    }
}
