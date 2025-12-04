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
 * @interface SimplifyIoC.Mediations.MediationBinding
 * 
 * Subclass of Binding for MediationBinding.
 * 
 * MediationBindings support the following extensions of standard Bindings:
 *
 * ToMediator - Porcelain for To<T> providing a little extra clarity and security.
 *
 * ToAbstraction<T> - Provide an Interface or base Class adapter for the View.
 * When the binding specifies ToAbstraction<T>, the Mediator will be expected to inject <T>
 * instead of the concrete View class.
 */

using System;
using SimplifyIoC.Framework;

namespace SimplifyIoC.Mediations
{
    public class MediationBinding : Binding, IMediationBinding
    {
        private readonly ISemiBinding _abstraction= new SemiBinding();


        public MediationBinding(Binder.BindingResolver resolver) : base(resolver)
        {
            _abstraction.constraint = BindingConstraintType.One;
        }

        IMediationBinding IMediationBinding.ToMediator<T>()
        {
            return base.To(typeof(T)) as IMediationBinding;
        }

        IMediationBinding IMediationBinding.ToAbstraction<T>()
        {
            return ((IMediationBinding)this).ToAbstraction(typeof(T));
        }

        IMediationBinding IMediationBinding.ToAbstraction(Type t)
        {
            var abstractionType = t;
            if (key != null)
            {
                var keyType = key as Type;
                if (abstractionType.IsAssignableFrom(keyType) == false)
                    throw new Exception("The View " + key + " has been bound to the abstraction " + t + " which the View neither extends nor implements. ");
            }
            _abstraction.Add(abstractionType);
            return this;
        }

        public object abstraction => _abstraction.value ?? BindingConst.Nulloid;

        public new IMediationBinding Bind<T>()
        {
            return base.Bind<T>() as IMediationBinding;
        }

        public new IMediationBinding Bind(object key)
        {
            return base.Bind(key) as IMediationBinding;
        }

        public new IMediationBinding To<T>()
        {
            return base.To<T>() as IMediationBinding;
        }

        public new IMediationBinding To(object o)
        {
            return base.To(o) as IMediationBinding;
        }
    }
}