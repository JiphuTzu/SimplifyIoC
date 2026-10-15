/*
 * @class SimplifyIoC.Mediations.View
 *
 * 所有 View 的父类，继承 MonoBehaviour。
 * 它把自身的 Awake、Start 与 OnDestroy 生命周期事件上报给归属的 Bootstrap，
 * 使框架能够感知 View 生命周期中的这些关键时刻。
 */

using System;
using System.Reflection;
using SimplifyIoC.Bootstraps;
using SimplifyIoC.Injectors;
using SimplifyIoC.Signals;
using SimplifyIoC.Utils;
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

        /// 5.6：本 View 的归属 Bootstrap，在注册成功那一刻确定（<see cref="Bootstrap.AddView"/>），
        /// 之后 Remove / Enable / Disable 一律直接发给它，不再逐次向上遍历 Transform 链。
        ///
        /// 改前每次生命周期事件都要重新走一遍 100 层上限的向上查找，且查出来的 Bootstrap
        /// 未必是当初做 Mediate 的那个（中途被重新挂到别的父节点下时尤其容易错）。
        ///
        /// 与兜底的关系：向上找不到任何 Bootstrap 时，注册会回落到框架级锚点
        /// <see cref="Bootstrap.root"/>（5.6 之前叫 Context.firstContext，2026-10-15 恢复）。
        /// 兜底只决定"注册到谁"，不影响"之后通知给谁"——归属照样在注册那一刻钉死。
        public Bootstrap owningBootstrap { get; private set; }

        /// 5.6：由 Bootstrap 在一锤定音的注册点写入（不要从别处调用）。
        internal void SetOwningBootstrap(Bootstrap bootstrap)
        {
            owningBootstrap = bootstrap;
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
        /// The View will attempt to connect to the Bootstrap at this moment.
        protected virtual void Awake()
        {
            if (autoRegisterWithBootstrap && !registeredWithBootstrap && shouldRegister)
                BubbleToBootstrap(BubbleType.Add, false);

            //4.1：解析时机在"注入完成之后"（由 MediationBinder 调用 EnsureAttributesInitialized）。
            //4.7：只有明确不向 Bootstrap 注册、且此刻确实没有归属的 View 才在 Awake 兜底解析——
            //5.6：判定挪到注册之后。"是否会注册"这件事到这里已经确定，
            //不再靠"看一眼 autoRegisterWithBootstrap 就猜"来决定要不要抢跑，
            //消除了"声明不注册、事实上却被注册（例如被其它脚本改变 autoRegisterWithBootstrap，
            //或被显式 AddView）的 View 在注入之前就被抢先解析"这一类错配。
            if (!autoRegisterWithBootstrap && owningBootstrap == null)
                EnsureAttributesInitialized();
        }

        /// <summary>
        /// 5.6：从 <see cref="injectionBinder"/> 取一个实例（绝大多数场景就是取信号单例）。
        /// 等价于 injectionBinder.GetInstance&lt;T&gt;()，省掉每个 View 都要写一遍的容器往返。
        ///
        ///     [Inject] 之外的轻量写法：startSignal = Get&lt;StartSignal&gt;();
        /// 没有归属容器时返回 null（孤儿 View 的既有契约，不抛）。
        /// </summary>
        protected T GetSignal<T>() where T : BaseSignal
        {
            return injectionBinder?.GetInstance<T>();
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
        /// If the View is not yet registered with the Bootstrap, it will 
        /// attempt to connect again at this moment.
        protected virtual void Start()
        {
            if (autoRegisterWithBootstrap && !registeredWithBootstrap && shouldRegister)
                BubbleToBootstrap(BubbleType.Add, true);

            //4.7：走到这里仍未注册到任何 Bootstrap ⇒ 不会有任何人替它做"注入后解析"。
            //自行兜底一次（幂等），使 View 在没有 Mediator / 没有 Bootstrap 时，
            //[Child] / [BindEvent] / [BindMethod] / [MainThread] 依然照常生效。
            //（requiresBootstrap 为 true 的 View 在上一行要么已注册、要么已抛异常，走不到这里——这是既有契约。）
            if (owningBootstrap == null)
                EnsureAttributesInitialized();
        }

        /// A MonoBehaviour OnDestroy handler
        /// The View will inform the Bootstrap that it is about to be
        /// destroyed.
        protected virtual void OnDestroy()
        {
            //5.1：先退订组内订阅（此处执行回调移除，避免后续 BubbleToBootstrap 期间误触发）。
            _subscriptions?.Dispose();
            //4.4：立即释放本组件的 [BindMethod] 静态注册表条目（不依赖 GC 时机）。
            //静态表本身已改为 ConditionalWeakTable，这里是"及时性"而非"正确性"的补充。
            this.UnbindMethods();
            BubbleToBootstrap(BubbleType.Remove, false);
            //5.6：通知发出后断开归属引用，避免已销毁的 View 继续 root 住整个 Bootstrap 链
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
        /// 把生命周期事件交给所属的 Bootstrap。
        ///
        /// 5.6：只有 Add 会发生"向上查找"（归属尚未确定）；Remove / Enable / Disable
        /// 一律发给注册时记下的 <see cref="owningBootstrap"/>—— Mediate 是谁做的，通知就得回到谁那里，
        /// 中间不管 GameObject 被挂到什么地方。
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
        /// 沿 Transform 向上找最近一个 Bootstrap 组件，注册并记下归属。
        ///
        /// 5.6+：2.0.0 起锚点就是这个组件本身；尚未 Init 的 Bootstrap 也照样能收下 View
        /// （<see cref="Bootstrap.AddView"/> 会把它放进早到缓存，由 PostBindings 统一处理），所以只看组件在不在。
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
            //这是 5.6 之前的行为（当时叫 Context.firstContext），2026-10-15 恢复：
            //运行期 Instantiate、层级被重排、Bootstrap 尚未 Init 等情形下"层级里找不到家"并不罕见，
            //宁可让归属落到一个进程级兜底，也不要让整个 View 崩在 Start。
            //注意：作者已声明"不依赖 Bootstrap"（requiresBootstrap = false）的 View 在上面就返回了，不参与兜底。
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
#if UNITY_EDITOR
        [ContextMenu("🔭 MapChildren", false, 0)]
        private void AutoMapChildren()
        {
            this.AddAttributeParser(this.GetChildParser())
                .ParseFields(BindingFlags.Instance | BindingFlags.Public  | BindingFlags.NonPublic);
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
