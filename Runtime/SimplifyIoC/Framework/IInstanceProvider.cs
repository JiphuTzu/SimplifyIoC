/*
 * @interface SimplifyIoC.Framework.IInstanceProvider
 *
 * 提供指定 Type 的实例。
 * 只需要“新建一个实例”时，用本接口代替 IInjectionBinder。
 */

using System;

namespace SimplifyIoC.Framework
{
    public interface IInstanceProvider
    {
        // Retrieve an Instance based on the key.
        // ex. `injectionBinder.Get<ISomeInterface>();`
        T GetInstance<T>();

        // Retrieve an Instance based on the key.
        // ex. `injectionBinder.Get(typeof(ISomeInterface));`
        object GetInstance(Type key, bool ignoreException);

        // 带调用级作用域的实例获取。scope 为 null 时与上面的重载等价。
        // Pool 只依赖 IInstanceProvider，池化命令创建实例时需要把作用域内的参数一并注入。
        object GetInstance(Type key, bool ignoreException, InjectionScope scope);
    }
}