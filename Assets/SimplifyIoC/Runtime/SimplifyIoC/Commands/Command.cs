/*
 * @class SimplifyIoC.Commands.Command
 *
 * Command 是承载业务逻辑的地方。
 *
 * 在本框架中，Command 通过 CommandBinder 绑定到 Signal 上：
 * 派发某个 Signal 时，框架会实例化、注入并执行绑定到该 Signal 的所有 Command。
 *
 * 默认情况下，Command 在 Execute() 执行完毕后立即被回收。
 * 对于异步 Command（例如调用服务并等待响应），请在 Execute() 开头调用 Retain()
 * 以阻止过早回收；但请注意，此时你必须在 Command 完成后自行调用 Release()。
 *
 * 调用 Fail() 会终止 Command 所在的执行序列，但对并行执行的 Command 没有影响。
 */

using SimplifyIoC.Injectors;
using SimplifyIoC.Pools;
using SimplifyIoC.Signals;

namespace SimplifyIoC.Commands
{
    public abstract class Command : IPoolable
    {
        /// Back reference to the CommandBinder that instantiated this Commmand
        [Inject]
        public ICommandBinder commandBinder { get; set; }

        /// The InjectionBinder for this Bootstrap
        [Inject]
        public IInjectionBinder injectionBinder { get; set; }

        public object data { get; set; }

        public bool cancelled { get; set; }

        public bool isClean { get; set; }

        public int sequenceId { get; set; }
        public bool retain { get; set; }

        /// 5.1：订阅组（惰性创建，从未订阅过则不分配）。
        private SignalSubscriptionGroup _subscriptions;

        /// <summary>
        /// 5.1：随本 Command 生命周期的订阅组。Restore()（回收前清理）时自动退订组内全部订阅。
        /// Retain() 的异步 Command 最容易漏解绑——它活得比 Execute 长，
        /// 挂在长期存在的 Signal 上就是一条稳定的泄漏路径。
        /// </summary>
        protected SignalSubscriptionGroup subscriptions => _subscriptions ??= new SignalSubscriptionGroup();

        public Command()
        {
            //Set to false on construction to ensure that it's not double-injected on first use.
            //The pool will satisfy all injections on first use. The CommandBinder re-injects
            //every time the Command is recycled.
            isClean = false;
        }

        public abstract void Execute();

        public virtual void Retain()
        {
            retain = true;
        }

        public virtual void Release()
        {
            retain = false;
            commandBinder?.ReleaseCommand(this);
        }

        /// Use/override this method to clean up the Command for recycling
        public virtual void Restore()
        {
            //5.1：回收前退订组内订阅。覆写本方法时记得调 base.Restore()。
            _subscriptions?.Dispose();
            //5.5：统一池化后每条命令执行完都会走到这里，Restore 从"仅 Pooled() 命令才会触发"
            //变成主路径上的常规动作。注入保证了 injectionBinder 非空，但派生类被回收的路径不止一条
            //（例如注入中途失败后落到池里），这里守住 NRE——回收动作本身不该再抛异常。
            injectionBinder?.injector.Uninject(this);
            isClean = true;
        }

        public virtual void Fail()
        {
            commandBinder?.Stop(this);
        }

        public void Cancel()
        {
            cancelled = true;
        }
    }
}