// 模板：类型安全信号。按载荷个数二选一，最多 4 参。
// 同类型多值或超过 4 个值时，定义一个值对象（VO）作为单个载荷类型。
using SimplifyIoC.Signals;

namespace {{ROOT_NS}}.Signals
{
    // 无载荷：public class {{FEATURE}}Signal : Signal { }
    // 单载荷：public class {{FEATURE}}Signal : Signal<{{PAYLOAD_TYPE}}> { }
    // 多载荷：public class {{FEATURE}}Signal : Signal<T1, T2, T3> { }
    public class {{FEATURE}}Signal : Signal<{{PAYLOAD_TYPE}}> { }
}
