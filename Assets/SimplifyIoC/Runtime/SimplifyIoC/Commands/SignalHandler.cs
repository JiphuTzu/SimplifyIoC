using System;

namespace SimplifyIoC.Commands
{
    /// <summary>
    /// 4.3：Bind&lt;TSignal&gt;().ToHandler(...) 的短路处理器。
    ///
    /// 信号派发时由 CommandBinder 直接解析服务并调用 invoke，**不创建 Command、不进命令链**。
    /// 这是框架里第三条"信号 → 目标"通路（另两条：To&lt;C&gt;() 信号→命令，[ListensTo] 信号→Mediator 方法）：
    /// 它补的是"在绑定期声明、直达任意服务、且不需要 Mediator/View"这条路。
    ///
    /// 不持有静态/长生命周期状态，随绑定一起被回收。
    /// </summary>
    public sealed class SignalHandler
    {
        /// <summary>要解析的服务类型（经完整注入链获取实例）。</summary>
        public Type serviceType;

        /// <summary>调用体：(服务实例, 信号载荷数组)。typed 重载在闭包里做参数个数与类型校验。</summary>
        public Action<object, object[]> invoke;
    }
}
