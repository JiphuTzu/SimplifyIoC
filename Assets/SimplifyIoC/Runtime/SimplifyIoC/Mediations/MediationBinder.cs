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
 * @class SimplifyIoC.Mediations.MediationBinder
 * 
 * Highest-level abstraction of the MediationBinder. Agnostic as to View and Mediator Type.
 * 
 * Please read SimplifyIoC.Mediations.IMediationBinder
 * where I've extensively explained the purpose of View mediation
 */

using System;
using System.Linq;
using System.Reflection;
using SimplifyIoC.Injectors;
using SimplifyIoC.Framework;
using SimplifyIoC.Signals;
using SimplifyIoC.Utils;
using UnityEngine;
using Binder = SimplifyIoC.Framework.Binder;

namespace SimplifyIoC.Mediations
{
    public enum MediationEvent
    {
        /// The View is Awake
        Awake,

        /// The View is about to be Destroyed
        Destroyed,

        /// The View is being Enabled
        Enabled,

        /// The View is being Disabled
        Disabled
    }
    public class MediationBinder : Binder, IMediationBinder
    {

        [Inject]
        public IInjectionBinder injectionBinder { get; set; }
        public override IBinding GetRawBinding()
        {
            return new MediationBinding(Resolver);
        }

        public virtual void Trigger(MediationEvent evt, View view)
        {
            var viewType = view.GetType();
            if (GetBinding(viewType) is IMediationBinding binding)
            {
                switch (evt)
                {
                    case MediationEvent.Awake:
                        InjectViewAndChildren(view);
                        MapView(view, binding);
                        break;
                    case MediationEvent.Destroyed:
                        UnmapView(view, binding);
                        break;
                    case MediationEvent.Enabled:
                        EnableView(view, binding);
                        break;
                    case MediationEvent.Disabled:
                        DisableView(view, binding);
                        break;
                }
            }
            else if (evt == MediationEvent.Awake)
            {
                //Even if not mapped, Views (and their children) have potential to be injected
                InjectViewAndChildren(view);
            }
            else if (evt == MediationEvent.Destroyed)
            {
                UnmapView(view, null);
            }
        }

        /// Add a Mediator to a View. If the mediator is a "true" Mediator (i.e., it
        /// implements IMediator), perform PreRegister and OnRegister.
        protected virtual void ApplyMediationToView(IMediationBinding binding, View view, Type mediatorType)
        {
            var isTrueMediator = IsTrueMediator(mediatorType);
            if (isTrueMediator && HasMediator(view, mediatorType)) return;
            var viewType = view.GetType();
            var mediator = CreateMediator(view, mediatorType);

            if (mediator == null)
                ThrowNullMediatorError(viewType, mediatorType);
            if (isTrueMediator && mediator is Mediator m0)
                m0.PreRegister();

            var typeToInject = binding.abstraction == null || binding.abstraction.Equals(Binder.NULL_BINDING) ? viewType : binding.abstraction as Type;
            //3.4.c：view 改走调用级作用域（3.4.a 定形的 InjectionScope），不再临时写进全局容器。
            //原实现 Bind(typeToInject).ToValue(view).ToInject(false) → Inject → Unbind 有三个问题：
            //  1) 容器里若已有同 key 的绑定，Bind 会触发 RegisterNameConflict，Binder 被打进 conflicted 状态，
            //     此后任何 GetBinding 都抛异常（mediation 自身随即失败）；
            //  2) 注入期间发生的嵌套 mediation（注入点 setter / PostConstruct 内再创建 View）会重复 Bind 同一 key，
            //     同样触发冲突，且收尾的 Unbind 会把绑定整个删掉；
            //  3) 收尾的 Unbind 会连带删掉用户本已存在的同 key 全局绑定。
            //走 Scope 后容器全程不被触碰，且调用级参数优先于全局绑定（见 Injector.GetValueInjection 的解析顺序）。
            var scope = new InjectionScope(new[] { typeToInject }, new object[] { view });
            //P0#6：mediator 是 MonoBehaviour，已由 Unity 构造，构造注入会凭空多造一个实例。只做 setter/PostConstruct 注入。
            injectionBinder.injector.Inject(mediator, false, scope);
            if (isTrueMediator && mediator is Mediator m1)
            {
                //4.2：强类型视图直赋值。Mediator<TView> 覆写后由泛型参数直赋（零容器往返），
                //非泛型 Mediator 为 no-op（view 已由上面的 scope 注入）。
                m1.SetViewFromBinder(view);
                //4.1：声明式解析放在注入完成之后、OnRegister 之前——
                //[BindEvent(nameof(view))] 这类写法依赖已注入/已赋值的成员。
                m1.EnsureAttributesInitialized();
                m1.OnRegister();
            }
        }

        /// Add Mediators to Views. We make this virtual to allow for different concrete
        /// behaviors for different View/Mediation Types (e.g., MonoBehaviours require 
        /// different handling than EditorWindows)
        protected virtual void InjectViewAndChildren(View view)
        {
            var views = GetViews(view);
            var aa = views.Length;
            for (var a = aa - 1; a > -1; a--)
            {
                var iView = views[a];
                if (iView != null && iView.shouldRegister)
                {
                    if (iView.autoRegisterWithContext && iView.registeredWithContext)
                    {
                        continue;
                    }
                    iView.registeredWithContext = true;
                    if (!iView.Equals(view))
                        Trigger(MediationEvent.Awake, iView);
                }
            }
            injectionBinder.injector.Inject(view, false);
            //4.1：注入完成后再解析声明式绑定，使 [BindEvent(nameof(注入成员))] 在 View 侧也可用。
            view.EnsureAttributesInitialized();
            HandleDelegates(view, view.GetType(), true);
        }

        protected virtual bool IsTrueMediator(Type mediatorType)
        {
            return typeof(Mediator).IsAssignableFrom(mediatorType);
        }

        public new IMediationBinding Bind<T>()
        {
            return base.Bind<T>() as IMediationBinding;
        }

        public IMediationBinding BindView<T>()
        {
            return base.Bind<T>() as IMediationBinding;
        }

        /// <summary>
        /// 5.3：视图/中介者成对入口，Bind&lt;MyView, MyMediator&gt;() 等价于 Bind&lt;MyView&gt;().ToMediator&lt;MyMediator&gt;()。
        /// <c>where TMediator : Mediator</c> 把"绑上去的不是中介者"从让人摸不着头脑的运行时行为提前到编译期。
        /// </summary>
        public IMediationBinding Bind<TView, TMediator>() where TMediator : Mediator
        {
            return Bind<TView>().ToMediator<TMediator>();
        }

        /// Creates and registers one or more Mediators for a specific View instance.
        /// Takes a specific View instance and a binding and, if a binding is found for that type, creates and registers a Mediator.
        protected virtual void MapView(View view, IMediationBinding binding)
        {
            var viewType = view.GetType();

            if (bindings.ContainsKey(viewType))
            {
                var values = binding.value as object[];
                var aa = values.Length;
                for (var a = 0; a < aa; a++)
                {
                    var mediatorType = values[a] as Type;
                    if (mediatorType == viewType)
                    {
                        throw new Exception(viewType + "mapped to itself. The result would be a stack overflow.");
                    }
                    ApplyMediationToView(binding, view, mediatorType);

                    if (view.enabled)
                        EnableMediator(view, mediatorType);
                }
            }
        }

        /// Removes a mediator when its view is destroyed
        protected virtual void UnmapView(View view, IMediationBinding binding)
        {
            if(binding != null) TriggerInBindings(view, binding, DestroyMediator);
            HandleDelegates(view, view.GetType(), false);
        }

        /// Enables a mediator when its view is enabled
        protected virtual void EnableView(View view, IMediationBinding binding)
        {
            TriggerInBindings(view, binding, EnableMediator);
        }

        /// Disables a mediator when its view is disabled
        protected virtual void DisableView(View view, IMediationBinding binding)
        {
            TriggerInBindings(view, binding, DisableMediator);
        }

        /// Triggers given function in all mediators bound to given view
        protected virtual void TriggerInBindings(View view, IMediationBinding binding, Func<View, Type, object> method)
        {
            var viewType = view.GetType();

            if (bindings.ContainsKey(viewType))
            {
                var values = binding.value as object[];
                var aa = values.Length;
                for (var a = 0; a < aa; a++)
                {
                    var mediatorType = values[a] as Type;
                    method(view, mediatorType);
                }
            }
        }

        /// Create a new Mediator object based on the mediatorType on the provided view
        protected virtual object CreateMediator(View view, Type mediatorType)
        {
            var mediator = view.gameObject.AddComponent(mediatorType);
            if (mediator is Mediator)
            {
                HandleDelegates(mediator, mediatorType, true);
            }

            return mediator;
        }

        /// Destroy the Mediator on the provided view object based on the mediatorType
        protected virtual Mediator DestroyMediator(View view, Type mediatorType)
        {
            var mediator = view.GetComponent(mediatorType) as Mediator;
            if (mediator != null)
            {
                mediator.OnRemove();
                //5.1：OnRemove 之后由框架统一退订组内订阅（用户无需手写 RemoveListener）。
                mediator.DisposeSubscriptions();
                //4.4：Mediator 的 [BindMethod] 条目同步立即摘除（Mediator 没有 Unity 销毁钩子）
                mediator.UnbindMethods();
                HandleDelegates(mediator, mediatorType, false);
            }
            return mediator;
        }

        /// Calls the OnEnabled method of the mediator
        protected virtual object EnableMediator(View view, Type mediatorType)
        {
            var mediator = view.GetComponent(mediatorType) as Mediator;
            if (mediator != null)
                mediator.OnEnabled();

            return mediator;
        }

        /// Calls the OnDisabled method of the mediator
        protected virtual object DisableMediator(View view, Type mediatorType)
        {
            var mediator = view.GetComponent(mediatorType) as Mediator;
            if (mediator != null)
                mediator.OnDisabled();

            return mediator;
        }

        /// Retrieve all views including children for this view
        protected virtual View[] GetViews(View view)
        {
            var components = view.GetComponentsInChildren(typeof(View), true);
            var views = components.Cast<View>().ToArray();
            return views;
        }

        /// Whether or not an instantiated Mediator of this type exists
        protected virtual bool HasMediator(View view, Type mediatorType)
        {
            return view.GetComponent(mediatorType) != null;
        }

        /// Error thrown when a Mediator can't be instantiated
        /// Abstract because this happens for different reasons. Allow implementing
        /// class to specify the nature of the error.
        protected virtual void ThrowNullMediatorError(Type viewType, Type mediatorType)
        {
            var fullTypeName = mediatorType.ToString();
            var className = fullTypeName.Substring(fullTypeName.LastIndexOf('.') + 1);
            throw new Exception($"The view: {viewType} is mapped to mediator: {mediatorType}. AddComponent resulted in null, which probably means {className} is not a MonoBehaviour.");
        }
        /// Determine whether to add or remove ListensTo delegates
        private void HandleDelegates(object mono, Type mediatorType, bool toAdd)
        {
            var reflectedClass = injectionBinder.injector.reflector.Get(mediatorType);
            //GetInstance Signals and add listeners
            foreach (var pair in reflectedClass.attrMethods)
            {
                if (pair.Value is not ListensTo attr) continue;
                var signal = (ISignal)injectionBinder.GetInstance(attr.type,!toAdd);
                if(signal == null) continue;
                if (toAdd) AssignDelegate(mono, signal, pair.Key);
                else RemoveDelegate(mono, signal, pair.Key);
            }
        }

        /// Remove any existing ListensTo Delegates
        private void RemoveDelegate(object target, ISignal signal, MethodInfo method)
        {
            //P0#4 修复：原用 BaseType.IsGenericType 判断，直接使用 Signal<T> 或二级继承会走错分支。
            //改为：无载荷 Signal（及其子类）走 Action 路径，其余（Signal<T,...> 及任意子类）走委托路径。
            if (signal is Signal plainSignal)
            {
                var toRemove = (Action)Delegate.CreateDelegate(typeof(Action), target, method);
                plainSignal.RemoveListener(toRemove);
            }
            else
            {
                var toRemove = Delegate.CreateDelegate(signal.listener.GetType(), target, method);
                signal.listener = Delegate.Remove(signal.listener, toRemove);
            }
        }

        /// Apply ListensTo delegates
        private void AssignDelegate(object target, ISignal signal, MethodInfo method)
        {
            //P0#4 修复：判别逻辑同 RemoveDelegate
            if (signal is Signal plainSignal)
            {
                plainSignal.AddListener((Action)Delegate.CreateDelegate(typeof(Action), target, method));
            }
            else
            {
                var toAdd = Delegate.CreateDelegate(signal.listener.GetType(), target, method); //e.g. Signal<T>, Signal<T,U> etc.
                signal.listener = Delegate.Combine(signal.listener, toAdd);
            }
        }
    }
}