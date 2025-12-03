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

/**
 * @interface SimplifyIoC.Dispatchers.EventBinding
 * 
 * A Binding for the EventDispatcher.
 * 
 * EventBindings technically allow any Key, but require either an 
 * EmptyCallback (no arguments) or an EventCallback (one argument).
 *
 * @see SimplifyIoC.Dispatchers.IEvent
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using SimplifyIoC.Framework;

namespace SimplifyIoC.Dispatchers
{
    public class EventBinding : Binding, IEventBinding
    {
        private Dictionary<Delegate, EventCallbackType> callbackTypes;

        public EventBinding() : this(null){}

        public EventBinding(SimplifyIoC.Framework.Binder.BindingResolver resolver) : base(resolver)
        {
            keyConstraint = BindingConstraintType.ONE;
            valueConstraint = BindingConstraintType.MANY;
            callbackTypes = new Dictionary<Delegate, EventCallbackType>();
        }

        public EventCallbackType TypeForCallback(EmptyCallback callback)
        {
            if (callbackTypes.ContainsKey(callback))
            {
                return callbackTypes[callback];
            }
            return EventCallbackType.NOT_FOUND;
        }

        public EventCallbackType TypeForCallback(EventCallback callback)
        {
            if (callbackTypes.ContainsKey(callback))
            {
                return callbackTypes[callback];
            }
            return EventCallbackType.NOT_FOUND;
        }

        public new IEventBinding Bind(object key)
        {
            return base.Bind(key) as IEventBinding;
        }

        public IEventBinding To(EventCallback value)
        {
            base.To(value);
            storeMethodType(value as Delegate);
            return this;
        }

        public IEventBinding To(EmptyCallback value)
        {
            base.To(value);
            storeMethodType(value as Delegate);
            return this;
        }

        public new IEventBinding To(object value)
        {
            base.To(value);
            storeMethodType(value as Delegate);
            return this;
        }

        public override void RemoveValue(object value)
        {
            base.RemoveValue(value);
            callbackTypes.Remove(value as Delegate);
        }

        private void storeMethodType(Delegate value)
        {
            if (value == null)
            {
                throw new DispatcherException("EventDispatcher can't map something that isn't a delegate'", DispatcherExceptionType.ILLEGAL_CALLBACK_HANDLER);
            }
            var methodInfo = value.Method;
            var argsLen = methodInfo.GetParameters().Length;
            switch (argsLen)
            {
                case 0:
                    callbackTypes[value] = EventCallbackType.NO_ARGUMENTS;
                    break;
                case 1:
                    callbackTypes[value] = EventCallbackType.ONE_ARGUMENT;
                    break;
                default:
                    throw new DispatcherException("Event callbacks must have either one or no arguments", DispatcherExceptionType.ILLEGAL_CALLBACK_HANDLER);
            }
        }
    }
}