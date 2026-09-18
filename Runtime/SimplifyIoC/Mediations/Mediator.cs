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
 *	limitations under the License.
 */

/*
 * @class SimplifyIoC.Mediations.Mediator
 * 
 * Base class for all Mediators.
 * 
 * @see SimplifyIoC.Mediations.IMediationBinder
 */

using SimplifyIoC.Injectors;
using SimplifyIoC.Utils;
using UnityEngine;

namespace SimplifyIoC.Mediations
{
    public class Mediator : MonoBehaviour
    {
	    [Inject]
	    public IInjectionBinder injectionBinder { get; set; }

        //public Mediator() { }

        /// 4.1：解析只跑一次的哨兵（与 View 同策略）。
        private bool _attributesInitialized;

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
        /// 4.1：Mediator 侧声明式绑定解析。框架在注入完成后、OnRegister 之前自动调用一次
        /// （见 MediationBinder.ApplyMediationToView）。
        ///
        /// 这一侧尤其需要"注入之后"这个时机：Mediator 的 [BindEvent("onClick", nameof(view))]
        /// 里的 view 通常是 [Inject] 成员，注入前取不到。
        ///
        /// 4.4：GetBindMethodParser 治理完成，[BindMethod] 一并接入。
        /// </summary>
        protected virtual void InitAttributes()
        {
            var bindMethodParser = this.GetBindMethodParser();
            var reflected = this.AddAttributeParser(this.GetEventMethodParser())
                .AddAttributeParser(this.GetChildParser())
                .AddAttributeParser(this.GetMainThreadParser());
            if (bindMethodParser != null)
                reflected = reflected.AddAttributeParser(bindMethodParser);
            reflected.ParseAttributes();
        }

        /// <summary>
        /// 4.1：InitAttributes 的幂等包装，由框架在注入完成后调用。
        /// </summary>
        internal void EnsureAttributesInitialized()
        {
            if (_attributesInitialized) return;
            _attributesInitialized = true;
            InitAttributes();
        }

        /// <summary>
        /// 4.2：强类型视图直赋值通道。非泛型 Mediator 走 [Inject] view（3.4.c 的调用级作用域），
        /// 此处为 no-op；Mediator&lt;TView&gt; 覆写后由泛型类型参数直赋，不再经容器解析。
        /// </summary>
        internal virtual void SetViewFromBinder(View view) { }
    }

    /*
     * 4.2：强类型视图 Mediator。
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
     * 与既有写法的关系：
     *  - 泛型版：视图引用由泛型类型参数给出，框架注入后直赋值（零容器往返），不需要也不能再写 [Inject] view；
     *  - 非泛型 Mediator + [Inject] TView view：完全保留，走 3.4.c 的调用级作用域
     *    （见 MediationBinder.ApplyMediationToView），两种写法可以混用。
     *
     * 视图赋值发生在 注入之后、InitAttributes/OnRegister 之前，
     * 因此 [BindEvent(..., nameof(view))] 在两条路径上都可用。
     *
     * 说明：Mediator<TView> 与基类同文件（本类型的脚本资产即本文件）。它是抽象泛型类，
     * 由 MediationBinder 通过 AddComponent(mediatorType) 实例化其具体派生类，不依赖独立脚本资产。
     */
    public abstract class Mediator<TView> : Mediator where TView : View
    {
        /// <summary>
        /// 本 Mediator 所中介的视图。由框架在注入完成后赋值，早于 InitAttributes 与 OnRegister。
        /// </summary>
        public TView view { get; private set; }

        internal override void SetViewFromBinder(View boundView)
        {
            view = (TView)boundView;
        }
    }
}
