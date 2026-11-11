/*
 * @class SimplifyIoC.Mediations.Mediator
 *
 * 所有 Mediator 的基类。
 *
 * @see SimplifyIoC.Mediations.IMediationBinder
 */

using SimplifyIoC.Injectors;
using SimplifyIoC.Signals;
using UnityEngine;

namespace SimplifyIoC.Mediations
{
    public class Mediator : MonoBehaviour
    {
	    [Inject]
	    public IInjectionBinder injectionBinder { get; set; }

        /// <summary>
        /// 从 injectionBinder 取一个实例（绝大多数场景就是取信号单例），与 View.Get&lt;T&gt;() 同形。
        /// </summary>
        protected T Get<T>() where T : BaseSignal
        {
            return injectionBinder != null ? injectionBinder.GetInstance<T>() : null;
        }

        /// 订阅组（惰性创建，从未订阅过则不分配）。
        private SignalSubscriptionGroup _subscriptions;

        /// <summary>
        /// 随本 Mediator 生命周期的订阅组。组内订阅在 Mediator 被摘除时自动退订，
        /// 无需在 OnRemove 里逐条手写 RemoveListener（漏一行就是泄漏）。
        ///
        ///     public override void OnRegister()
        ///     {
        ///         subscriptions.Listen(view.OnDead, OnDead);   //组内订阅自动回收
        ///     }
        /// </summary>
        protected SignalSubscriptionGroup subscriptions => _subscriptions ??= new SignalSubscriptionGroup();

        /// <summary>
        /// 由框架在摘除 Mediator 时调用（MediationBinder.DestroyMediator，紧接 OnRemove 之后）。
        /// 用户覆写 OnRemove 时无需做任何事，订阅一律回收。
        /// </summary>
        internal void DisposeSubscriptions() => _subscriptions?.Dispose();

        /**
		 * Fires directly after creation and before injection
		 */
        public virtual void PreRegister() { }

        /**
		 * Fires after all injections satisfied.
		 *
		 * Override and place your initialization code here.
		 */
        public virtual void OnRegister() { }

        /**
		 * Fires on removal of view.
		 *
		 * Override and place your cleanup code here
		 */
        public virtual void OnRemove() { }

        /**
		 * Fires on enabling of view.
		 */
        public virtual void OnEnabled() { }

        /**
		 * Fires on disabling of view.
		 */
        public virtual void OnDisabled() { }

        /// <summary>
        /// 强类型视图直赋值通道。非泛型 Mediator 走 [Inject] view（调用级作用域），
        /// 此处为 no-op；Mediator&lt;TView&gt; 覆写后由泛型类型参数直赋，无需经容器解析。
        /// </summary>
        internal virtual void SetViewFromBinder(View view) { }
    }

    /*
     * 强类型视图 Mediator。
     *
     * 用法：
     *     public class LifeTimeMediator : Mediator<LifeTimeView>
     *     {
     *         public override void OnRegister()
     *         {
     *             view.OnDead.AddListener(OnDead);   // view 已是 LifeTimeView，编译期强类型
     *         }
     *     }
     *
     * 视图赋值发生在 注入之后、OnRegister 之前，OnRegister 里可直接使用强类型 view。
     *
     * 说明：Mediator<TView> 与基类同文件（本类型的脚本资产即本文件）。它是抽象泛型类，
     * 由 MediationBinder 通过 AddComponent(mediatorType) 实例化其具体派生类，不依赖独立脚本资产。
     */
    public abstract class Mediator<TView> : Mediator where TView : View
    {
        /// <summary>
        /// 本 Mediator 所中介的视图。由框架在注入完成后赋值，早于 OnRegister。
        /// </summary>
        public TView view { get; private set; }

        internal override void SetViewFromBinder(View boundView)
        {
            view = (TView)boundView;
        }
    }
}
