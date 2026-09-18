using System;

namespace SimplifyIoC.Framework
{
    /// <summary>
    /// 3.4：调用级注入作用域。
    ///
    /// 背景：框架原本没有作用域概念，"仅本次调用有效"的参数只能临时 Bind 进全局容器、
    /// 用完再 Unbind（信号载荷、Command 解析、Mediation 往返三处都是这个套路）。由此带来
    /// 两个结构性问题：
    ///   1) 嵌套/并发调用会互相覆盖同一 key 的临时绑定（重入即崩）；
    ///   2) 与用户既有的全局同名绑定直接冲突，Binder 进入 conflicted 状态后连查绑定都抛异常。
    ///
    /// 现在改为把这类参数放进一个轻量查找表，沿调用栈显式传递：Injector 解析注入点时
    /// 先查 Scope，未命中再回落 GetSupplier / GetBinding。它不持有任何静态或长生命周期状态，
    /// 调用结束即不可达——天然可重入、无泄漏，也不会污染容器。
    ///
    /// 有意不采用"字段暂存当前 Scope"的写法：那只是把静态污染换成实例污染，重入问题原样保留。
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
        /// 按类型精确查找。信号载荷按信号声明类型匹配，与原本"按声明类型临时 Bind"的
        /// 语义保持一致（不做 IsAssignableFrom 的宽松匹配，以免改变既有可解析范围）。
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
