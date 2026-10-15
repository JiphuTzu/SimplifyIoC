/*
 * @class SimplifyIoC.Contexts.CrossContextBridge
 *
 * 跨 Context 转发信号的中继器。
 *
 * 本类把本地 Context 内派发并映射到它的信号“放行”，
 * 转发给跨域根 Binder 供其他 Context 消费，
 * 从而免去在终点（如 Command 或 Mediator）注入跨域 Binder 的必要。
 *
 * 由于 Bridge 本身就是跨域共享的，在哪里建立跨域映射由开发者决定。
 * 这种自由同时也是一个潜在的坑：建议统一在链根 Context 中建立跨域映射，以免混乱。
 *
 * 示例：

	crossContextBridge.Bind(GameEvent.MISSILE_HIT);

 * 在应用的任意 Context 中执行上述语句后，任何 Context 派发该信号都会被转发到其他 Context。
 */

using SimplifyIoC.Framework;
using System.Collections.Generic;

namespace SimplifyIoC.Contexts
{
    public class CrossContextBridge : Binder
	{
		public override IBinding Bind(object key)
		{
			var binding = GetRawBinding();
			binding.Bind(key);
			Resolver(binding);
			return binding;
		}
	}
}