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
 * @interface SimplifyIoC.Framework.IInstanceProvider
 *
 * Provides an instance of the specified Type
 * When all you need is a new instance, use this instead of IInjectionBinder.
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