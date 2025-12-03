using System;
using System.Reflection;
using SimplifyIoC.Signals;
using UnityEngine;

namespace SimplifyIoC.Mediations
{
    public class SignalViewBinder : MediationBinder
    {
        public override void Trigger(MediationEvent evt, IView view)
        {
            switch (evt)
            {
                case MediationEvent.AWAKE:
                    InjectViewAndChildren(view);
                    break;
                case MediationEvent.DESTROYED:
                    UnmapView(view, null);
                    break;
                default:
                    break;
            }
        }
        protected override void InjectViewAndChildren(IView view)
        {
            base.InjectViewAndChildren(view);
            if (view is MonoBehaviour mono)
                HandleDelegates(view, mono.GetType(), true);
        }
        protected override void UnmapView(IView view, IMediationBinding binding)
        {
            if(view is not MonoBehaviour mono) return;
            HandleDelegates(view, mono.GetType(), false);
        }

        /// Determine whether to add or remove ListensTo delegates
        private void HandleDelegates(object mono, Type mediatorType, bool toAdd)
        {
            var reflectedClass = injectionBinder.injector.reflector.Get(mediatorType);
            //GetInstance Signals and add listeners
            foreach (var pair in reflectedClass.attrMethods)
            {
                if (pair.Value is not ListensTo attr) continue;
                //
                var signal = (ISignal)injectionBinder.GetInstance(attr.type);
                if (toAdd) AssignDelegate(mono, signal, pair.Key);
                else RemoveDelegate(mono, signal, pair.Key);
            }
        }

        /// Remove any existing ListensTo Delegates
        private void RemoveDelegate(object mediator, ISignal signal, MethodInfo method)
        {
            var baseType = signal.GetType().BaseType;
            if (baseType == null) return;
            if (baseType.IsGenericType) //e.g. Signal<T>, Signal<T,U> etc.
            {
                var toRemove = Delegate.CreateDelegate(signal.listener.GetType(), mediator, method);
                signal.listener = Delegate.Remove(signal.listener, toRemove);
            }
            else
            {
                ((Signal)signal).RemoveListener((Action)Delegate.CreateDelegate(typeof(Action), mediator, method)); //Assign and cast explicitly for Type == Signal case
            }
        }

        /// Apply ListensTo delegates
        private void AssignDelegate(object mediator, ISignal signal, MethodInfo method)
        {
            var baseType = signal.GetType().BaseType;
            if (baseType == null) return;
            if (baseType.IsGenericType)
            {
                var toAdd = Delegate.CreateDelegate(signal.listener.GetType(), mediator, method); //e.g. Signal<T>, Signal<T,U> etc.
                signal.listener = Delegate.Combine(signal.listener, toAdd);
            }
            else
            {
                ((Signal)signal).AddListener((Action)Delegate.CreateDelegate(typeof(Action), mediator, method)); //Assign and cast explicitly for Type == Signal case
            }
        }
    }
}
