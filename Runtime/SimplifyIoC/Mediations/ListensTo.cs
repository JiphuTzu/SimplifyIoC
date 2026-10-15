/*
 * @class ListensTo
 *
 * [ListensTo] 特性为 Mediator 提供“信号-方法”绑定的快捷方式。
 * 示例：

		[ListensTo(typeof(PlayerUpdateSignal))]
		public void onPlayerUpdate(int hitpoints)
		{
			//do some stuff
		}

 * 上例等价于在 OnRegister 中执行 PlayerUpdateSignal.AddListener(onPlayerUpdate)，
 * 在 OnRemove 中执行 PlayerUpdateSignal.RemoveListener(onPlayerUpdate)。
 *
 * 注意：方法可见性不限——private / protected / internal 均可生效
 * （扫描用 BindingFlags.NonPublic；Delegate.CreateDelegate 绑定已解析的 MethodInfo
 * 不做可见性检查，两者均已实验验证）。
 *
 * 两个已知限制：
 *   1) 基类中声明的 private 方法扫不到（GetMethods 的 FlattenHierarchy 不传播基类
 *      private 成员），基类的 protected / public 方法不受影响；
 *   2) 方法签名必须与信号匹配：无载荷 Signal → 无参方法；Signal<T,...> → 对应参数
 *      签名。不匹配时 CreateDelegate 抛异常，不是静默失败。
 */


using System;
using UnityEngine.Scripting;

[AttributeUsage(AttributeTargets.Method)]
public class ListensTo : PreserveAttribute
{
	public ListensTo(){}

	public ListensTo(Type t)
	{
		type = t;
	}

	public Type type {get; set;}
}