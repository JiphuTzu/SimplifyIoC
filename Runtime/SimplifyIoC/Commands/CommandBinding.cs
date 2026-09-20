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
 * @class SimplifyIoC.Commands.CommandBinding
 * 
 * The Binding for CommandBinder.
 * 
 * The only real distinction between CommandBinding and Binding
 * is the addition of `Once()`, which signals that the Binding
 * should be destroyed immediately after a single use.
 */
using System;
using SimplifyIoC.Framework;
namespace SimplifyIoC.Commands
{
    public class CommandBinding : Binding, ICommandBinding
	{
		public bool isOneOff{ get; set; }

		public bool isSequence{ get; set; }

		/// <summary>
		/// 5.5：池化声明。统一池化之后所有命令都走池，本标记对实际行为已无影响，
		/// 保留只是为了兼容既有源码，以及继续参与 ToHandler 的互斥校验
		/// （把两条互斥写法保留在同一处报错，比让其中一条变成无声的空操作好）。
		/// </summary>
		public bool isPooled{ get; set; }

		/// <summary>
		/// 4.3：短路处理器。非 null 时该绑定不再创建 Command，派发直接调用它。
		/// 有意不放到 ICommandBinding 上——接口只暴露"怎么写"，读取只发生在 CommandBinder 内。
		/// </summary>
		internal SignalHandler handler;

		public CommandBinding() : base()
		{
			_value.uniqueValues = false;
		}

		public CommandBinding (Binder.BindingResolver resolver) : base(resolver)
		{
			_value.uniqueValues = false;
		}

		public ICommandBinding Once()
		{
			isOneOff = true;
			return this;
		}

		public ICommandBinding InParallel()
		{
			isSequence = false;
			return this;
		}

		public ICommandBinding InSequence()
		{
			//4.3：D3——没有命令链，isSequence 无意义，静默忽略比报错更糟，绑定期即抛
			if (handler != null)
				throw new Exception("InSequence() cannot be combined with ToHandler(...): a signal handler is invoked directly and has no command chain.");
			isSequence = true;
			return this;
		}

		/// <summary>
		/// 5.5：所有命令统一池化后，本方法已是 no-op（除 ToHandler 互斥校验外），保留仅为兼容。
		/// 既有的 .Pooled() 调用照常编译、照常工作（Commands 仍会是复用的），无需修改。
		/// </summary>
		public ICommandBinding Pooled()
		{
			//4.3：D3——handler 不是可池化对象，绑定期即抛
			if (handler != null)
				throw new Exception("Pooled() cannot be combined with ToHandler(...): a signal handler is not a poolable instance.");
			isPooled = true;
			resolver (this);
			return this;
		}

		// ---- 4.3：ToHandler 短路通路 ----

		public ICommandBinding ToHandler<TService>(Action<TService> handler)
		{
			return SetHandler(typeof(TService), (service, payload) =>
			{
				RequirePayloadCount(payload, 0);
				handler((TService)service);
			});
		}

		public ICommandBinding ToHandler<TService, T1>(Action<TService, T1> handler)
		{
			return SetHandler(typeof(TService), (service, payload) =>
			{
				RequirePayloadCount(payload, 1);
				handler((TService)service, CastPayload<T1>(payload[0]));
			});
		}

		public ICommandBinding ToHandler<TService, T1, T2>(Action<TService, T1, T2> handler)
		{
			return SetHandler(typeof(TService), (service, payload) =>
			{
				RequirePayloadCount(payload, 2);
				handler((TService)service, CastPayload<T1>(payload[0]), CastPayload<T2>(payload[1]));
			});
		}

		public ICommandBinding ToHandler<TService, T1, T2, T3>(Action<TService, T1, T2, T3> handler)
		{
			return SetHandler(typeof(TService), (service, payload) =>
			{
				RequirePayloadCount(payload, 3);
				handler((TService)service, CastPayload<T1>(payload[0]), CastPayload<T2>(payload[1]),
					CastPayload<T3>(payload[2]));
			});
		}

		public ICommandBinding ToHandler<TService, T1, T2, T3, T4>(Action<TService, T1, T2, T3, T4> handler)
		{
			return SetHandler(typeof(TService), (service, payload) =>
			{
				RequirePayloadCount(payload, 4);
				handler((TService)service, CastPayload<T1>(payload[0]), CastPayload<T2>(payload[1]),
					CastPayload<T3>(payload[2]), CastPayload<T4>(payload[3]));
			});
		}

		public ICommandBinding ToHandler<TService>(Action<TService, object[]> handler)
		{
			//弱类型兜底：载荷原样透传，不做个数/类型校验
			return SetHandler(typeof(TService), (service, payload) => handler((TService)service, payload));
		}

		private ICommandBinding SetHandler(Type serviceType, Action<object, object[]> invoke)
		{
			//4.3：两条通路互斥，后到者抛异常（早失败，避免半配置的绑定静默失效）
			if (isSequence)
				throw new Exception("ToHandler(...) cannot be combined with InSequence().");
			if (isPooled)
				throw new Exception("ToHandler(...) cannot be combined with Pooled().");
			if (_value.value != null)
				throw new Exception("ToHandler(...) cannot be combined with To<T>() / To(value).");
			//4.3：命名是另一处"来源声明"。Binding._name 是基类私有字段，故走公开的 name 属性——
			//未被 ToName(...) 设置时它对外暴露为 Binder.NULL_BINDING。
			if (!Equals(name, Binder.NULL_BINDING))
				throw new Exception("ToHandler(...) cannot be combined with ToName(...).");

			handler = new SignalHandler { serviceType = serviceType, invoke = invoke };
			//与 To<T>() 同路径：通知 Binder 把此绑定登记进注册表
			resolver?.Invoke(this);
			return this;
		}

		private static void RequirePayloadCount(object[] payload, int expected)
		{
			var actual = payload?.Length ?? 0;
			if (actual != expected)
				throw new Exception($"Signal handler expects {expected} payload value(s) but the signal dispatched {actual}.");
		}

		/// <summary>
		/// 按位置取载荷并校验类型——与 BuildScopeFromSignal 的策略一致：不匹配即报错，
		/// 不静默传 null。载荷缺失/类型不符都在派发时抛出，而不是让 handler 拿到错值。
		/// </summary>
		private static T CastPayload<T>(object value)
		{
			if (value == null)
				throw new Exception("Signal handler refuses a null payload value for type: " + typeof(T));
			if (value is T typed) return typed;
			throw new Exception($"Signal handler expected payload of type {typeof(T)} but got {value.GetType()}.");
		}

		/// <summary>
		/// 5.2：ToHandler 互斥守卫，**收敛到 value / name 两个收口方法**。
		///
		/// 4.3 的写法是把 ThrowIfHandlerSet 逐个抄进 To&lt;T&gt;() / To(object) / ToName&lt;T&gt;() / ToName(object)，
		/// 那是在"修补 coincidence 而不修补结构"：入口每多一个泛型/object 重载就要再抄一遍，
		/// 漏一个就悄悄产出半配置绑定（当时正是这样翻的车）。
		/// 现在两侧都只在这两个 Core 上设卡，任何入口（含将来新增的重载、
		/// 以及 SetValue 绕过 To 直接进 ToCore 的路径）都必然经过。
		/// </summary>
		/// <param name="memberName">报错文案里的入口名。收口处已无法区分调用的是哪个重载，故统称。</param>
		private void ThrowIfHandlerSet(string memberName)
		{
			if (handler != null)
				throw new Exception($"{memberName} cannot be combined with ToHandler(...): the signal handler is invoked directly and there is no command to resolve.");
		}

		protected override IBinding ToCore(object o)
		{
			ThrowIfHandlerSet("To<T>() / To(value)");
			return base.ToCore(o);
		}

		protected override IBinding ToNameCore(object o)
		{
			ThrowIfHandlerSet("ToName<T>() / ToName(value)");
			return base.ToNameCore(o);
		}

		//Everything below this point is simply facade on Binding to ensure fluent interface.
		//5.2：这些方法不含任何逻辑（守卫已在 ToCore/ToNameCore 上），只负责把返回类型收窄以便链式书写。
		//不要往这里加语义——加了就等于重新引入一个可以被绕过得掉的入口。
		public new ICommandBinding Bind<T>()
		{
			return base.Bind<T> () as ICommandBinding;
		}

		public new ICommandBinding Bind(object key)
		{
			return base.Bind (key) as ICommandBinding;
		}

		public new ICommandBinding To<T>()
		{
			return base.To<T> () as ICommandBinding;
		}

		public new ICommandBinding To(object o)
		{
			return base.To (o) as ICommandBinding;
		}

		public new ICommandBinding ToName<T>()
		{
			return base.ToName<T> () as ICommandBinding;
		}

		public new ICommandBinding ToName(object o)
		{
			return base.ToName (o) as ICommandBinding;
		}

		public new ICommandBinding Named<T>()
		{
			return base.Named<T> () as ICommandBinding;
		}

		public new ICommandBinding Named(object o)
		{
			return base.Named (o) as ICommandBinding;
		}
	}
}

