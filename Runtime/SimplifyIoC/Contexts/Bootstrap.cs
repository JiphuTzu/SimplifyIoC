/*
 * Copyright 2013 ThirdMotion, Inc.
 *
 *	Licensed under the Apache License, Version 2.0 (the "License");
 *	you may not use this file except in compliance with the License.
 *	You may obtain a copy of the License at
 *
 *		http://www.apache.org/licenses/LICENSE-2.0
 *
 *		Unless required by applicable law or agreed to in writing, software
 *		distributed under the License is distributed on an "AS IS" BASIS,
 *		WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *		See the License for the specific language governing permissions and
 *		limitations under the License.
 */

/*
 * @class SimplifyIoC.Contexts.ContextView
 * 
 * The Root View of a Context hierarchy.
 * 
 * Extend this class to create your AppRoot, then attach
 * that MonoBehaviour to the GameObject at the very top of
 * your display hierarchy.
 * 
 * The startup sequence looks like this:

		void Awake()
		{
			context = new MyContext(this, true);
			context.Start ();
		}

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

		#region IView implementation

		public bool requiresContext {get;set;}

		public bool registeredWithContext {get;set;}

		public bool autoRegisterWithContext{ get; set; }
		public bool shouldRegister => true;

		#endregion
	}
}