/*
 * @interface SimplifyIoC.Commands.ICommandBinder
 *
 * 触发 Command 实例化的 Binder 接口。
 *
 * Command 是应用逻辑的归属地，通常只聚焦单一职责，
 * 例如添加一个 View、请求一个服务、读取或保存一个 Model。
 *
 * 把 Signal 绑定到 Command 意味着派发方无需知道事件由谁消费、
 * 更无需知道事件将如何被使用。例如，一个 Mediator 发出“两个 View 相撞”的信号，
 * 随后由 Command 决定处理结果：销毁这两个对象、通知记分 Model 修改分数、
 * 并向服务器发送消息。这一结果用一个 Command 还是三个 Command 表达，
 * 取决于你的编码偏好——同一个信号可以触发一个或多个 Command。
 *
 * 该 Binder 同样支持序列：默认所有 Command 并行执行；
 * 若绑定指定了 InSequence()，Command 将串行执行，并可在任意时刻挂起链条。
 *
 * 绑定的 key 只能是 Signal 类型：派发完全由 Signal 广播驱动，
 * 字符串 / 枚举 key 虽然能编译通过，却没有任何触发路径（本框架已无 Event 系统）。
 *
 * 绑定示例：
		Bind<SomeSignal>().To<SomeCommand>();
		Bind<StartupSignal>().To<StartCommand>().Once(); //单次使用后立即销毁该绑定
		Bind<SomeSignal>().To<FirstCommand>().To<SecondCommand>().To<ThirdCommand>().InSequence();
 *
 * 异步 Command 与取消序列的细节参见 Command。
 */

using SimplifyIoC.Framework;

namespace SimplifyIoC.Commands
{
    public interface ICommandBinder : IBinder
	{

		/// Trigger a key that unlocks one or more Commands
		void ReactTo (object trigger);

		/// Trigger a key that unlocks one or more Commands and provide a data injection to that Command
		void ReactTo (object trigger, object data);

		/// Release a previously retained Command.
		/// By default, a Command is garbage collected at the end of its `Execute()` method. 
		/// But the Command can be retained for asynchronous calls.
		void ReleaseCommand(Command command);

		/// Called to halt execution of a currently running command group
		void Stop(object key);

		/// Bind a trigger Key by generic Type
		new ICommandBinding Bind<T>();

		/// Bind a trigger Key by value
		new ICommandBinding Bind(object value);

		new ICommandBinding GetBinding<T>();
	}
}

