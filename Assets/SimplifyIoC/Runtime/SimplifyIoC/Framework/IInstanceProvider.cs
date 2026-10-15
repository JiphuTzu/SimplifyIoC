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

        // 3.4：带调用级作用域的实例获取。scope 为 null 时与上面的重载完全等价。
        // 之所以放在这里而不是 IInjectionBinder：Pool 只依赖 IInstanceProvider，
        // 而池化命令首次创建实例时同样需要把作用域内的参数注入进去。
        object GetInstance(Type key, bool ignoreException, InjectionScope scope);
    }
}