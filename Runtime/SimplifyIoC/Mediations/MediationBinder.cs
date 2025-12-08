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
            injectionBinder.Bind(typeToInject).ToValue(view).ToInject(false);
            injectionBinder.injector.Inject(mediator);
            injectionBinder.Unbind(typeToInject);
            if (isTrueMediator && mediator is Mediator m1)
                m1.OnRegister();
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
            HandleDelegates(view, view.GetType(), true);
        }

        protected virtual bool IsTrueMediator(Type mediatorType)
        {
            return typeof(Mediator).IsAssignableFrom(mediatorType);
        }

        // protected override IBinding PerformKeyValueBindings(List<object> keyList, List<object> valueList)
        // {
        //     IBinding binding = null;
        //
        //     // Bind in order
        //     foreach (var key in keyList)
        //     {
        //         var keyType = Type.GetType(key as string);
        //         if (keyType == null)
        //         {
        //             throw new BinderException("A runtime Mediation Binding has resolved to null. Did you forget to register its fully-qualified name?\n View:" + key, BinderExceptionType.RUNTIME_NULL_VALUE);
        //         }
        //         binding = Bind(keyType);
        //     }
        //     foreach (var value in valueList)
        //     {
        //         var valueType = Type.GetType(value as string);
        //         if (valueType == null)
        //         {
        //             throw new BinderException("A runtime Mediation Binding has resolved to null. Did you forget to register its fully-qualified name?\n Mediator:" + value, BinderExceptionType.RUNTIME_NULL_VALUE);
        //         }
        //         binding = binding.To(valueType);
        //     }
        //
        //     return binding;
        // }

        // override protected Dictionary<string, object> ConformRuntimeItem(Dictionary<string, object> dictionary)
        // {
        // 	Dictionary<string, object> bindItems = new Dictionary<string, object> ();
        // 	Dictionary<string, object> toItems = new Dictionary<string, object> ();
        // 	foreach (var item in dictionary) 
        // 	{
        // 		if (item.Key == "BindView")
        // 		{
        // 			bindItems.Add ("Bind", item.Value);
        // 		}
        // 		else if (item.Key == "ToMediator")
        // 		{
        // 			toItems.Add ("To", item.Value);
        // 		}
        // 	}
        // 	foreach (var item in bindItems)
        // 	{
        // 		dictionary.Remove ("BindView");
        // 		dictionary.Add ("Bind", item.Value);
        // 	}
        // 	foreach (var item in toItems) 
        // 	{
        // 		dictionary.Remove ("ToMediator");
        // 		dictionary.Add ("To", item.Value);
        // 	}
        // 	return dictionary;
        // }

        // override protected IBinding ConsumeItem(Dictionary<string, object> item, IBinding testBinding)
        // {
        // 	IBinding binding = base.ConsumeItem(item, testBinding);

        // 	foreach (var i in item)
        // 	{
        // 		if (i.Key == "ToAbstraction")
        // 		{
        // 			Type abstractionType = Type.GetType (i.Value as string);
        // 			IMediationBinding mediationBinding = (binding as IMediationBinding);
        // 			if (abstractionType == null)
        // 			{
        // 				throw new BinderException ("A runtime abstraction in the MediationBinder returned a null Type. " + i.ToString(), BinderExceptionType.RUNTIME_NULL_VALUE);
        // 			}
        // 			if (mediationBinding == null)
        // 			{
        // 				throw new MediationException ("During an attempt at runtime abstraction a MediationBinding could not be found. " + i.ToString(), MediationExceptionType.BINDING_RESOLVED_TO_NULL);
        // 			}

        // 			mediationBinding.ToAbstraction (abstractionType);
        // 		}
        // 	}
        // 	return binding;
        // }

        public new IMediationBinding Bind<T>()
        {
            return base.Bind<T>() as IMediationBinding;
        }

        public IMediationBinding BindView<T>()
        {
            return base.Bind<T>() as IMediationBinding;
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
            var memberInfo = signal.GetType().BaseType;
            if (memberInfo != null && memberInfo.IsGenericType) //e.g. Signal<T>, Signal<T,U> etc.
            {
                var toRemove = Delegate.CreateDelegate(signal.listener.GetType(), target, method);
                signal.listener = Delegate.Remove(signal.listener, toRemove);
            }
            else
            {
                ((Signal)signal).RemoveListener((Action)Delegate.CreateDelegate(typeof(Action), target, method)); //Assign and cast explicitly for Type == Signal case
            }
        }

        /// Apply ListensTo delegates
        private void AssignDelegate(object target, ISignal signal, MethodInfo method)
        {
            var memberInfo = signal.GetType().BaseType;
            if (memberInfo != null && memberInfo.IsGenericType)
            {
                var toAdd = Delegate.CreateDelegate(signal.listener.GetType(), target, method); //e.g. Signal<T>, Signal<T,U> etc.
                signal.listener = Delegate.Combine(signal.listener, toAdd);
            }
            else
            {
                ((Signal)signal).AddListener((Action)Delegate.CreateDelegate(typeof(Action), target, method)); //Assign and cast explicitly for Type == Signal case
            }
        }
    }
}