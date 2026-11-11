// 模板：业务逻辑命令。信号载荷按类型注入；Execute() 返回即回池，不要长期持有状态。
// 需要 Bootstrap 自身时额外 using SimplifyIoC.Bootstraps;
using SimplifyIoC.Commands;

namespace {{ROOT_NS}}.Commands
{
    public class {{FEATURE}}Command : Command
    {
        // 与 Signal<T> 的 T 对应，框架按类型把信号载荷注入进来。
        [Inject]
        public {{PAYLOAD_TYPE}} {{PAYLOAD_NAME}} { get; set; }

        // 命名注入的 key 是枚举原值：
        // [Inject(BootstrapKeys.Bootstrap)] public Bootstrap root { get; set; }

        public override void Execute()
        {
            // 业务逻辑；结果用信号通知 UI，一条链写完：
            // injectionBinder.GetInstance<{{FEATURE}}ChangedSignal>().Dispatch(result);
        }

        // 异步命令：开头 Retain()，完成后必须 Release()，两条成对出现。
        // 订阅用 subscriptions.Listen(sig, cb) / subscriptions.Track(sig.AddOnce(cb))。
        // 覆写 Restore() 必须调 base.Restore()（回收订阅组并 Uninject）。
    }
}
