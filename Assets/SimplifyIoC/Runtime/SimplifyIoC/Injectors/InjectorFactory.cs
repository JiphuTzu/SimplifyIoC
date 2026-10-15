/*
 * @class SimplifyIoC.Injectors.InjectorFactory
 *
 * 负责实例化所有实例的工厂。
 */

using System;

namespace SimplifyIoC.Injectors
{
    public class InjectorFactory
    {
        public InjectorFactory() { }

        public object Get(IInjectionBinding binding, object[] args)
        {
            if (binding == null)
            {
                throw new Exception("InjectorFactory cannot act on null binding");
            }
            var type = binding.type;

            switch (type)
            {
                case InjectionBindingType.Singleton:
                    return SingletonOf(binding, args);
                case InjectionBindingType.Value:
                    return ValueOf(binding);
                default:
                    break;
            }

            return InstanceOf(binding, args);
        }

        public object Get(IInjectionBinding binding)
        {
            return Get(binding, null);
        }

        /// Generate a Singleton instance
        protected object SingletonOf(IInjectionBinding binding, object[] args)
        {
            if (binding.value != null)
            {
                if (binding.value.GetType().IsInstanceOfType(typeof(Type)))
                {
                    var o = CreateFromValue(binding.value, args);
                    if (o == null)
                        return null;
                    binding.SetValue(o);
                }
                else
                {
                    //no-op. We already have a binding value!
                }
            }
            else
            {
                binding.SetValue(GenerateImplicit((binding.key as object[])[0], args));
            }
            return binding.value;
        }

        protected object GenerateImplicit(object key, object[] args)
        {
            var type = key as Type;
            if (!type.IsInterface && !type.IsAbstract)
            {
                return CreateFromValue(key, args);
            }
            throw new Exception("InjectorFactory can't instantiate an Interface or Abstract Class. Class: " + key);
        }

        /// The binding already has a value. Simply return it.
        protected object ValueOf(IInjectionBinding binding)
        {
            return binding.value;
        }

        /// Generate a new instance
        protected object InstanceOf(IInjectionBinding binding, object[] args)
        {
            if (binding.value != null)
            {
                return CreateFromValue(binding.value, args);
            }
            //2.3.b 修复：原实现 GenerateImplicit 已构造出实例，
            //随后又把【实例】传回 CreateFromValue 再构造一次并丢弃第一个（双重构造）。
            //该路径仅存在于 value==null 的绑定（现行绑定机制下不会入库，属死路径），但保留即暗雷。
            return GenerateImplicit((binding.key as object[])[0], args);
        }

        /// Call the Activator to attempt instantiation the given object
        protected object CreateFromValue(object o, object[] args)
        {
            var value = (o is Type) ? o as Type : o.GetType();
            //P0#7 修复：原为裸 catch 静默吞掉构造异常并返回 null，
            //下游只看到莫名其妙的 NRE。现让异常直接上抛，由调用方决定如何处理。
            if (args == null || args.Length == 0)
            {
                return Activator.CreateInstance(value);
            }
            return Activator.CreateInstance(value, args);
        }
    }
}