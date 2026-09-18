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
		/// 4.3 勘误：To&lt;T&gt;() / ToName&lt;T&gt;() 在接口上被 new 重声明，这里也是 new（不是 override），
		/// 只在 To(object) 上守卫并不能覆盖它们——链式 ToHandler(...).To&lt;C&gt;() 走的正是 To&lt;T&gt;()，
		/// 原实现因此静默产出一个"既有 handler 又有命令值"的半配置绑定。统一走本方法守卫。
		/// </summary>
		private void ThrowIfHandlerSet(string memberName)
		{
			if (handler != null)
				throw new Exception($"{memberName} cannot be combined with ToHandler(...): the signal handler is invoked directly and there is no command to resolve.");
		}

		//Everything below this point is simply facade on Binding to ensure fluent interface
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
			ThrowIfHandlerSet("To<T>()");
			return base.To<T> () as ICommandBinding;
		}

		public new ICommandBinding To(object o)
		{
			//4.3：ToHandler 之后不能再接 To<T>()/To(o)，反之亦然
			ThrowIfHandlerSet("To<T>() / To(value)");
			return base.To (o) as ICommandBinding;
		}

		public new ICommandBinding ToName<T>()
		{
			ThrowIfHandlerSet("ToName<T>()");
			return base.ToName<T> () as ICommandBinding;
		}

		public new ICommandBinding ToName(object o)
		{
			ThrowIfHandlerSet("ToName(value)");
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

