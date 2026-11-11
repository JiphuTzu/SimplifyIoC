using System;

namespace SimplifyIoC.Framework
{
    /// <summary>
    /// 调用级注入作用域：把仅在本次调用有效的参数放进轻量查找表，沿调用栈显式传递。
    /// Injector 解析注入点时先查 Scope，未命中再回落 GetSupplier / GetBinding。
    /// 不持有静态或长生命周期状态，调用结束即不可达，可重入、无泄漏，也不会污染容器。
    /// </summary>
    public sealed class InjectionScope
    {
        private readonly Type[] _types;
        private readonly object[] _values;

        public InjectionScope(Type[] types, object[] values)
        {
            _types = types ?? Array.Empty<Type>();
            _values = values ?? Array.Empty<object>();
        }

        /// <summary>
        /// 按类型精确查找（信号载荷按信号声明类型匹配），不做 IsAssignableFrom 的宽松匹配。
        /// </summary>
        public bool TryGet(Type type, out object value)
        {
            var types = _types;
            for (var i = 0; i < types.Length; i++)
            {
                if (types[i] == type)
                {
                    value = _values[i];
                    return true;
                }
            }

            value = null;
            return false;
        }
    }
}
