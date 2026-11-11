/*
 * @class SimplifyIoC.Mediations.View
 *
 * 所有 View 的父类，继承 MonoBehaviour。
 * 它把自身的 Awake、Start 与 OnDestroy 生命周期事件上报给归属的 Bootstrap，
 * 使框架能够感知 View 生命周期中的这些关键时刻。
 */

using System;
using SimplifyIoC.Bootstraps;
using SimplifyIoC.Injectors;
using SimplifyIoC.Signals;
using UnityEngine;

namespace SimplifyIoC.Mediations
{
    public abstract class View : MonoBehaviour
    {
        /// Determines the type of event the View is bubbling to the Bootstrap
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
        /// a view to exist outside a bootstrap you can set it to false. The only
        /// difference is whether an error gets generated.
        public bool requiresBootstrap { get; set; } = true;
        
        /// A flag for allowing the View to register with the Bootstrap
        /// In general you can ignore this. But some developers have asked for a way of disabling
        ///  View registration with a checkbox from Unity, so here it is.
        /// If you want to expose this capability either
        /// (1) uncomment the commented-out line immediately below, or
        /// (2) subclass View and override the autoRegisterWithBootstrap method using your own custom (public) field.
        //[SerializeField]
        public virtual bool autoRegisterWithBootstrap { get; set; } = true;

        public bool registeredWithBootstrap { get; set; }
        
        public bool shouldRegister => enabled && gameObject.activeInHierarchy;

        /// 本 View 的归属 Bootstrap，在注册成功时确定（<see cref="Bootstrap.AddView"/>）；
        /// 之后 Remove / Enable / Disable 直接发回本 Bootstrap，无需再遍历 Transform 链。
        /// 向上找不到 Bootstrap 时，注册回落到框架级锚点 <see cref="Bootstrap.root"/>；
        /// 兜底只决定"注册到谁"，归属仍在注册时确定。
        public Bootstrap owningBootstrap { get; private set; }

        /// 由 Bootstrap 在注册时写入（不要从别处调用）。
        internal void SetOwningBootstrap(Bootstrap bootstrap)
        {
            owningBootstrap = bootstrap;
        }

        /// 订阅组（惰性创建，从未订阅过则不分配）。
        private SignalSubscriptionGroup _subscriptions;

        /// <summary>
        /// 随本 View 生命周期的订阅组。OnDestroy 时自动退订组内全部订阅。
        /// View 可以没有 Mediator，因此这条回收不能挂在 Mediator 上——
        /// 和 Mediator 的 subscriptions 是两条独立的组，各自负责自己的订阅。
        ///
        ///     subscriptions.Listen(someSignal, OnSomething);   //随 View 销毁自动退订
        /// </summary>
        protected SignalSubscriptionGroup subscriptions => _subscriptions ??= new SignalSubscriptionGroup();

        /// A MonoBehaviour Awake handler.
        /// The View will attempt to connect to the Bootstrap at this moment.
        protected virtual void Awake()
        {
            if (autoRegisterWithBootstrap && !registeredWithBootstrap && shouldRegister)
                BubbleToBootstrap(BubbleType.Add, false);
        }

        /// A MonoBehaviour Start handler
        /// If the View is not yet registered with the Bootstrap, it will 
        /// attempt to connect again at this moment.
        protected virtual void Start()
        {
            if (autoRegisterWithBootstrap && !registeredWithBootstrap && shouldRegister)
                BubbleToBootstrap(BubbleType.Add, true);
        }

        /// A MonoBehaviour OnDestroy handler
        /// The View will inform the Bootstrap that it is about to be
        /// destroyed.
        protected virtual void OnDestroy()
        {
            //先退订组内订阅（此处执行回调移除，避免后续 BubbleToBootstrap 期间误触发）。
            _subscriptions?.Dispose();
            BubbleToBootstrap(BubbleType.Remove, false);
            //通知发出后断开归属引用，避免已销毁的 View 继续 root 住整个 Bootstrap 链
            owningBootstrap = null;
            registeredWithBootstrap = false;
        }

        /// A MonoBehaviour OnEnable handler
        /// The View will inform the Bootstrap that it was enabled
        protected virtual void OnEnable()
        {
            BubbleToBootstrap(BubbleType.Enable, false);
        }

        /// A MonoBehaviour OnDisable handler
        /// The View will inform the Bootstrap that it was disabled
        protected virtual void OnDisable()
        {
            BubbleToBootstrap(BubbleType.Disable, false);
        }

        /// <summary>
        /// 把生命周期事件交给所属的 Bootstrap。只有 Add 会"向上查找"（归属尚未确定）；
        /// Remove / Enable / Disable 一律发给注册时记下的 <see cref="owningBootstrap"/>——
        /// Mediate 是谁做的，通知就得回到谁那里，中间不管 GameObject 被挂到什么地方。
        /// </summary>
        protected void BubbleToBootstrap(BubbleType type, bool finalTry)
        {
            switch (type)
            {
                case BubbleType.Add:
                    ResolveAndRegister(finalTry);
                    return;
                case BubbleType.Remove:
                    owningBootstrap?.RemoveView(this);
                    return;
                case BubbleType.Enable:
                    owningBootstrap?.EnableView(this);
                    return;
                case BubbleType.Disable:
                    owningBootstrap?.DisableView(this);
                    return;
                default:
                    return;
            }
        }

        /// <summary>
        /// 沿 Transform 向上找最近的 Bootstrap 组件，注册并记下归属。
        /// 只看组件是否存在；尚未 Init 的 Bootstrap 也能收下 View
        /// （<see cref="Bootstrap.AddView"/> 会把它放进早到缓存，由 PostBindings 统一处理）。
        ///
        /// 找不到时：Awake 阶段不动声色（finalTry=false，Start 还有一次机会）；
        /// Start 阶段若 requiresBootstrap 则先回落到框架级锚点（Bootstrap.root），
        /// 锚点也不在场才抛异常。
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
                var bootstrap = trans.GetComponent<Bootstrap>();
                if (bootstrap != null)
                {
                    bootstrap.AddView(this);
                    registeredWithBootstrap = true;
                    return;
                }
            }

            if (!requiresBootstrap || !finalTry) return;

            //last ditch：向上找不到任何 Bootstrap 的 View 注册到框架级锚点，而不是直接抛。
            //运行期 Instantiate、层级被重排、Bootstrap 尚未 Init 等情形下"层级里找不到家"并不罕见，
            //宁可让归属落到一个进程级兜底，也不要让整个 View 崩在 Start。
            //注意：声明"不依赖 Bootstrap"（requiresBootstrap = false）的 View 在上面就返回了，不参与兜底。
            var anchor = Bootstrap.root;
            if (anchor != null)
            {
                anchor.AddView(this);
                registeredWithBootstrap = true;
                return;
            }

            var msg = loopLimiter == LOOP_MAX ?
                "A view couldn't find a bootstrap. Loop limit reached." :
                "A view was added with no bootstrap. Views must be added into the hierarchy of their Bootstrap lest all hell break loose.";
            msg += "\nView: " + this;
            throw new Exception(msg);
        }
    }
}
