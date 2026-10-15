/*
 * @class SimplifyIoC.Contexts.Bootstrap
 *
 * Context 层级结构的根节点（扮演 StrangeIoC 中 ContextView 的角色）。
 *
 * 继承本类创建应用根，并把该 MonoBehaviour 挂到显示层级最顶部的 GameObject 上。
 * 注意：Bootstrap 并不继承 View，两者是平行的 MonoBehaviour——
 * View 负责"注册进已存在的 Context"，而 Bootstrap 负责"创建 Context"，职责相反、时序也不同；
 * 它需要的只是 Transform 锚点与注入根两个身份（详见 AttachContext 与 Context 的绑定）。
 *
 * 5.6 起本类的 context 属性由框架回写：Context 构造函数会经 SetBootstrap → AttachContext
 * 写入自己，因此你只需要在 Awake 里创建 Context，无需再手动给 context 赋值。
 * 创建 Context 本身仍必须在 Awake 里完成（框架不会替你实例化派生 Context）。
 *
 * 启动语义（单一、显式）：构造 = 建容器 + 归属 + 映射；启动永远显式调用 context.Launch()。
 * 推荐直接派生 SimplifyBootstrap——它在 Start 里替你调用 Launch：
 *
 *      //推荐：SimplifyBootstrap 子类只需在 Awake 里创建 Context
 *      void Awake() { new MyContext(this); }        //MyContext : SimplifyContext<T>
 *
 * 若派生裸 Bootstrap（直接继承 Context），请自行补启动：
 *
 *      void Awake()  { context = new MyContext(this); }
 *      void Start()  { context.Launch(); }
 *
 * 旧的只创建不启动写法现在会真的停在"已映射、未启动"状态——StartupSignal 不会广播。
 * 注意：MapBindings 在构造期即被调用（派生类实例字段尚未赋值），里面只放类型绑定，
 * 值绑定请在构造完成后再补。
 */

using SimplifyIoC.Mediations;
using UnityEngine;

namespace SimplifyIoC.Contexts
{
    public class Bootstrap : MonoBehaviour
	{
		public Context context { get; protected set; }

		/// <summary>
		/// 5.6：由 <see cref="Context"/> 在构造期回写"本 Bootstrap 归属哪个 Context"。
		///
		/// 这条反向链接是框架两条归属规则的唯一依据：
		///   • Context.ResolveParentContext —— 新建 Context 沿 Transform 向上找父 Context；
		///   • View.BubbleToContext         —— View 沿 Transform 向上找自己所属的 Context。
		/// 二者都要求"祖先 Bootstrap 的 context 非空"。
		///
		/// 改前这个属性只由用户代码在 Awake 里赋值（`context = new MyContext(this)`），
		/// 框架读它、却无权写它：一旦有人直接 `new MyContext(bootstrap)` 而忘了赋值，
		/// 上面两条查找会**静默**失败——Context 悄悄自任链根、跨域绑定从此分叉，
		/// View 则找不到归属按契约抛，报错现场离真正的原因很远。
		///
		/// Context 构造时本就拿得到 Bootstrap，这条链接完全可推导，因此改由框架建立。
		/// 用户原有的 `context = new XxxContext(this)` 写法不受影响（赋的是同一个对象）。
		/// </summary>
		internal void AttachContext(Context context)
		{
			this.context = context;
		}

		/// <summary>
		/// When a ContextView is Destroyed, automatically disposes the associated Context.
		/// 3.1：改为 Dispose 契约（幂等），不再依赖任何全局 Context 引用存活。
		/// </summary>
		protected virtual void OnDestroy()
		{
			context?.Dispose();
		}
	}
}