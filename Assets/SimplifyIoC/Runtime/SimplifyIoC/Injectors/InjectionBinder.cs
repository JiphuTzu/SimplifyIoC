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
 * @class SimplifyIoC.Injectors.InjectionBinder
 * 
 * The Binder for creating Injection mappings.
 * 
 * @see SimplifyIoC.Injectors.IInjectionBinder
 * @see SimplifyIoC.Injectors.IInjectionBinding
 */

using System;
using System.Collections.Generic;
using SimplifyIoC.Reflectors;
using SimplifyIoC.Framework;

namespace SimplifyIoC.Injectors
{
    public class InjectionBinder : Binder, IInjectionBinder
    {
        private Injector _injector;
        protected Dictionary<Type, Dictionary<Type, IInjectionBinding>> suppliers = new Dictionary<Type, Dictionary<Type, IInjectionBinding>>();

        public InjectionBinder()
        {
            injector = new Injector();
            injector.binder = this;
            injector.reflector = new ReflectionBinder();
        }

        public object GetInstance(Type key, bool ignoreException)
        {
            return GetInstance(key, null, ignoreException);
        }

        public virtual object GetInstance(Type key, object name, bool ignoreException)
        {
            var binding = GetBinding(key, name);
            if (binding == null)
            {
                if (ignoreException) return null;
                throw new Exception("InjectionBinder has no binding for:\n\tkey: " + key + "\nname: " + name);
            }
            var instance = GetInjectorForBinding(binding).Instantiate(binding, false);
            injector.TryInject(binding, instance);

            return instance;
        }

        protected virtual Injector GetInjectorForBinding(IInjectionBinding binding)
        {
            return injector;
        }

        public T GetInstance<T>()
        {
            var instance = GetInstance(typeof(T),false);
            var retv = (T)instance;
            return retv;
        }

        public T GetInstance<T>(object name)
        {
            var instance = GetInstance(typeof(T), name,false);
            var retv = (T)instance;
            return retv;
        }

        public override IBinding GetRawBinding()
        {
            return new InjectionBinding(Resolver);
        }

        public Injector injector
        {
            get
            {
                return _injector;
            }
            set
            {
                if (_injector != null)
                {
                    _injector.binder = null;
                }
                _injector = value;
                _injector.binder = this;
            }
        }

        public new IInjectionBinding Bind<T>()
        {
            return base.Bind<T>() as IInjectionBinding;
        }

        public IInjectionBinding Bind(Type key)
        {
            return base.Bind(key) as IInjectionBinding;
        }

        public new virtual IInjectionBinding GetBinding<T>()
        {
            return base.GetBinding<T>() as IInjectionBinding;
        }

        public new virtual IInjectionBinding GetBinding<T>(object name)
        {
            return base.GetBinding<T>(name) as IInjectionBinding;
        }

        public new virtual IInjectionBinding GetBinding(object key)
        {
            return base.GetBinding(key) as IInjectionBinding;
        }

        public new virtual IInjectionBinding GetBinding(object key, object name)
        {
            return base.GetBinding(key, name) as IInjectionBinding;
        }

        public int ReflectAll()
        {
            var list = new List<Type>();
            foreach (var pair in bindings)
            {
                var dict = pair.Value;
                foreach (var bPair in dict)
                {
                    var binding = bPair.Value;
                    var t = (binding.value is Type) ? (Type)binding.value : binding.value.GetType();
                    if (list.IndexOf(t) == -1)
                    {
                        list.Add(t);
                    }
                }
            }
            return Reflect(list);
        }

        public int Reflect(List<Type> list)
        {
            var count = 0;
            foreach (var t in list)
            {
                //Reflector won't permit primitive types, so screen them
                if (t.IsPrimitive || t == typeof(Decimal) || t == typeof(string))
                {
                    continue;
                }
                count++;
                injector.reflector.Get(t);
            }
            return count;
        }

        public IInjectionBinding GetSupplier(Type injectionType, Type targetType)
        {
            if (suppliers.ContainsKey(targetType))
            {
                if (suppliers[targetType].ContainsKey(injectionType))
                {
                    return suppliers[targetType][injectionType];
                }
            }
            return null;
        }

        public void Unsupply(Type injectionType, Type targetType)
        {
            var binding = GetSupplier(injectionType, targetType);
            if (binding != null)
            {
                suppliers[targetType].Remove(injectionType);
                binding.Unsupply(targetType);
            }
        }

        public void Unsupply<T, U>()
        {
            Unsupply(typeof(T), typeof(U));
        }

        protected override void Resolver(IBinding binding)
        {
            if (binding is IInjectionBinding iBinding)
            {
                var supply = iBinding.GetSupply();

                if (supply != null)
                {
                    foreach (var a in supply)
                    {
                        if (a is not Type aType) continue;
                        if (!suppliers.ContainsKey(aType))
                        {
                            suppliers[aType] = new Dictionary<Type, IInjectionBinding>();
                        }
                        var keys = iBinding.key as object[];
                        foreach (var key in keys)
                        {
                            var keyType = key as Type;
                            if (!suppliers[aType].ContainsKey(keyType))
                            {
                                suppliers[aType][keyType] = iBinding;
                            }
                        }
                    }
                }
            }

            base.Resolver(binding);
        }
    }
}