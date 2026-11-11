/*
 * @class SimplifyIoC.Commands.CommandBinder
 *
 * 触发 Command 实例化的 Binder。
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
 * CommandBinder 还支持序列（Sequence）：默认所有 Command 并行执行；
 * 若绑定指定了 InSequence()，Command 将串行执行，并可在任意时刻挂起链条。
 *
 * 绑定的 key 只能是 Signal 类型：派发完全由 Signal 广播驱动，
 * 字符串 / 枚举 key 虽然能编译通过，却没有任何触发路径。
 *
 * 绑定示例：

		Bind<SomeSignal>().To<SomeCommand>();

		Bind<StartupSignal>().To<StartCommand>().Once(); //单次使用后立即销毁该绑定

		Bind<SomeSignal>().To<FirstCommand>().To<SecondCommand>().To<ThirdCommand>().InSequence(); //多个 Command 串行执行

 *
 * 异步 Command 与取消序列的细节参见 Command。
 */

using System;
using System.Collections.Generic;
using SimplifyIoC.Framework;
using SimplifyIoC.Injectors;
using SimplifyIoC.Pools;
using SimplifyIoC.Signals;

namespace SimplifyIoC.Commands
{
    public class CommandBinder : Binder, ICommandBinder
    {
        [Inject]
        public IInjectionBinder injectionBinder { get; set; }

        protected readonly Dictionary<Type, Pool> _pools = new();

        /// Tracker for parallel commands in progress
        protected readonly HashSet<Command> _activeCommands = new();

        /// Tracker for sequences in progress
        protected readonly Dictionary<Command, ICommandBinding> _activeSequences = new();

        public override void ResolveBinding(IBinding binding, object key)
        {
            base.ResolveBinding(binding, key);

            if (!_bindings.ContainsKey(key)) return; //If this key already exists, don't bind this again
            //key 未必是信号（字符串/枚举也可作 key），不能直接强转
            if (key is BaseSignal signal)
                signal.AddListener(ReactTo);
        }
        public override void OnRemove()
        {
            foreach (var key in _bindings.Keys)
            {
                if (key is BaseSignal signal)
                    signal.RemoveListener(ReactTo);
            }
            //归还各命令池持有的全部实例，并清空两组"进行中命令"追踪集合。
            foreach (var pool in _pools.Values)
                pool.Clean();
            _pools.Clear();
            _activeCommands.Clear();
            _activeSequences.Clear();
            //释放绑定注册表与冲突表（放在最后：上面还要读 bindings）
            base.OnRemove();
        }

        public override IBinding GetRawBinding()
        {
            return new CommandBinding(Resolver);
        }

        public virtual void ReactTo(object trigger)
        {
            ReactTo(trigger, null);
        }

        public virtual void ReactTo(object trigger, object data)
        {
            if (data is IPoolable poolable)
                poolable.Retain();
            if (GetBinding(trigger) is not ICommandBinding binding) return;

            //绑定声明了 handler 时直接解析服务并调用，不创建 Command、不进命令链；
            //与命令路径共用 ResolveBinding/Unbind/OnRemove 三处的 signal.AddListener/RemoveListener 订阅/退订逻辑。
            if (binding.hasHandler)
            {
                InvokeHandler(binding, data);
                return;
            }
            if(binding.value is not object[] values) return;
            if (binding.isSequence)
            {
                Next(binding, data, values, 0);
            }
            else
            {
                var aa = values.Length + 1;
                for (var a = 0; a < aa; a++)
                {
                    Next(binding, data, values, a);
                }
            }
        }

        /// <summary>
        /// 执行 ToHandler 绑定的短路处理器。
        /// 服务经完整注入链解析（单例/transient 语义与普通绑定一致）；
        /// isOneOff 与命令路径一致：调用一次后移除绑定。
        /// 无载荷信号（Signal）的 data 为 null，统一归一为空数组，避免 handler 侧出现 null/空两种形态。
        /// </summary>
        private void InvokeHandler(ICommandBinding binding, object data)
        {
            binding.InvokeHandler(injectionBinder, data);
            if (binding.isOneOff)
                Unbind(binding);
        }

        private void Next(ICommandBinding binding, object data,object[] values, int depth)
        {
            if (depth < values.Length)
            {
                var cmd = values[depth] as Type;
                var command = InvokeCommand(cmd, binding, data, depth);
                ReleaseCommand(command);
            }
            else
            {
                DisposeOfSequencedData(data);
                if (binding.isOneOff)
                {
                    Unbind(binding);
                }
            }
        }

        //Subclasses can override this method to dispose of the data in sequenced commands.
        protected virtual void DisposeOfSequencedData(object data)
        {
            //No-op. Override if necessary.
        }

        protected virtual Command InvokeCommand(Type cmd, ICommandBinding binding, object data, int depth)
        {
            //key 未必是信号，用 as 判别；载荷类型只在有信号时解析
            var signal = binding.key as BaseSignal;
            var command = CreateCommandForSignal(cmd, data, signal?.GetTypes()); //Special signal-only command creation
            command.sequenceId = depth;
            TrackCommand(command, binding);
            ExecuteCommand(command);
            return command;
        }
        /// Create a Command and pass the Signal payload to it through a call-local scope
        protected Command CreateCommandForSignal(Type cmd, object data, List<Type> signalTypes)
        {
            //载荷通过调用级 InjectionScope 沿调用栈传递：嵌套/并发派发各自持有自己的载荷、互不覆盖，
            //且不会与用户已有的全局同名绑定冲突。
            var scope = BuildScopeFromSignal(signalTypes, data, cmd);

            var command = GetCommand(cmd, scope);
            command.data = data;
            return command;
        }

        /// <summary>
        /// 把信号声明类型与载荷配成调用级作用域：按顺序配对，同类型只注入一次，
        /// 未匹配到值或值为 null 时报错。key 非信号时 signalTypes 为 null，直接返回 null。
        /// </summary>
        private static InjectionScope BuildScopeFromSignal(List<Type> signalTypes, object data, Type cmd)
        {
            if (data == null || signalTypes == null || signalTypes.Count == 0)
            {
                return null;
            }

            var injectedTypes = new HashSet<Type>();
            var types = new List<Type>();
            var values = new List<object>((object[])data);
            var matched = new List<object>();

            //Iterate each signal type, in order.
            //Iterate values and find a match
            //If we cannot find a match, throw an error
            foreach (var type in signalTypes)
            {
                if (injectedTypes.Contains(type)) // Do not allow more than one injection of the same Type
                {
                    throw new Exception("SignalCommandBinder: You have attempted to map more than one value of type: " + type +
                        " in Command: " + cmd + ". Only the first value of a type will be injected. You may want to place your values in a VO, instead.");
                }

                var foundValue = false;
                foreach (var value in values)
                {
                    if (value == null) //Do not allow null injections
                    {
                        throw new Exception("SignalCommandBinder attempted to bind a null value from a signal to Command: " + cmd + " to type: " + type);
                    }

                    if (type.IsAssignableFrom(value.GetType())) //IsAssignableFrom lets us test interfaces as well
                    {
                        types.Add(type);
                        matched.Add(value);
                        injectedTypes.Add(type);
                        values.Remove(value);
                        foundValue = true;
                        break;
                    }
                }

                if (!foundValue)
                {
                    throw new Exception("Could not find an unused injectable value to inject in to Command: " + cmd + " for Type: " + type);
                }
            }

            return new InjectionScope(types.ToArray(), matched.ToArray());
        }

        protected Command GetCommand(Type type, InjectionScope scope = null)
        {
            if (type == null) return null;

            //命令实例一律来自该命令类型的池。
            var pool = GetOrCreatePool(type);
            if (pool.GetInstance(scope) is not Command command) return null;
            if (!command.isClean) return command;
            //池中实例已由 Unity/工厂构造完成，构造注入会多造一个不在池名单里的实例、归还时被静默丢弃；
            //回收实例只做 setter/PostConstruct 注入。
            injectionBinder.injector.Inject(command, false, scope);
            command.isClean = false;
            return command;
        }

        /// <summary>
        /// 命令类型的池按需创建：首次派发该命令类型时才创建，实例由 <see cref="CommandInstanceProvider"/> 供应，
        /// 不写入容器注册表，因此不会与用户自己的同名绑定冲突。
        /// </summary>
        protected Pool GetOrCreatePool(Type commandType)
        {
            if (_pools.TryGetValue(commandType, out var existing)) return existing;

            var pool = MakePoolFromType(commandType);
            _pools[commandType] = pool;
            return pool;
        }

        /// <summary>
        /// 池的创建出口（GetOrCreatePool 唯一调用，override 它即替换全部命令的池实现）。
        /// 显式指定 <see cref="CommandInstanceProvider"/>，不写入容器注册表，因此不会与用户同名绑定冲突；
        /// 直接构造非泛型 Pool 并显式设 poolType，避免 MakeGenericType 的 IL2CPP 裁剪风险。
        /// </summary>
        protected virtual Pool MakePoolFromType(Type type)
        {
            return new Pool
            {
                poolType = type,
                instanceProvider = new CommandInstanceProvider(this)
            };
        }

        protected void TrackCommand(Command command, ICommandBinding binding)
        {
            if (binding.isSequence)
            {
                _activeSequences.Add(command, binding);
            }
            else
            {
                _activeCommands.Add(command);
            }
        }

        protected void ExecuteCommand(Command command)
        {
            command?.Execute();
        }

        public virtual void Stop(object key)
        {
            if (key is Command command1 && _activeSequences.ContainsKey(command1))
            {
                RemoveSequence(command1);
            }
            else if (GetBinding(key) is ICommandBinding binding)
            {
                if (!_activeSequences.ContainsValue(binding)) return;
                foreach (var sequence in _activeSequences)
                {
                    if (sequence.Value != binding) continue;
                    var command = sequence.Key;
                    RemoveSequence(command);
                }
            }
        }

        public virtual void ReleaseCommand(Command command)
        {
            //Next 在绑定值为空等情况下会传入 null
            if (command == null) return;
            if (command.retain) return;
            var t = command.GetType();
            //该命令类型的池一定存在；不存在说明本 Binder 没创建过它，忽略即可
            if (_pools.TryGetValue(t, out var pool))
            {
                pool.ReturnInstance(command);
            }
            if (_activeCommands.Contains(command))
            {
                _activeCommands.Remove(command);
            }
            else if (_activeSequences.ContainsKey(command))
            {
                var binding = _activeSequences[command];
                var data = command.data;
                _activeSequences.Remove(command);
                Next(binding, data, binding.value as object[], command.sequenceId + 1);
            }
        }

        private void RemoveSequence(Command command)
        {
            if (!_activeSequences.ContainsKey(command)) return;
            command.Cancel();
            _activeSequences.Remove(command);
        }

        public new virtual ICommandBinding Bind<T>()
        {
            var binding = injectionBinder.GetBinding<T>();
            if (binding == null) //If this isn't injected yet, inject a new one as a singleton
            {
                injectionBinder.Bind<T>().ToSingleton();
            }

            var signal = injectionBinder.GetInstance<T>();
            return Bind(signal);
        }

        public new virtual ICommandBinding Bind(object value)
        {
            if (value is not Type type) return base.Bind(value) as ICommandBinding;
            //If this isn't injected yet, inject a new one as a singleton
            if (injectionBinder.GetBinding(type) == null)
            {
                injectionBinder.Bind(type).ToSingleton();
            }
            if(injectionBinder.GetInstance(type,false) is BaseSignal signal)
                return base.Bind(signal) as ICommandBinding;
            return base.Bind(value) as ICommandBinding;
        }
        /// <summary>Unbind by Signal Type</summary>
        public override void Unbind<T>()
        {
            if (GetBinding<T>() == null) return;
            var signal = injectionBinder.GetInstance<T>();
            if(signal == null) return;
            Unbind(signal, null);
        }

        /// <summary>Unbind by Signal Instance</summary>
        /// <param name="key">Instance of IBaseSignal</param>
        public override void Unbind(object key, object name)
        {
            if (_bindings.ContainsKey(key) && key is BaseSignal signal)
            {
                signal.RemoveListener(ReactTo);
            }
            base.Unbind(key, name);
            //绑定删除后，若命令池不再被任何绑定引用则归还（见 DropOrphanPools）。
            DropOrphanPools();
        }

        public new virtual ICommandBinding GetBinding<T>()
        {
            var signal = injectionBinder.GetInstance<T>();
            return base.GetBinding(signal) as ICommandBinding;
        }

        /// <summary>
        /// 信号/命令成对入口：Bind&lt;MySignal, MyCommand&gt;() 等价于 Bind&lt;MySignal&gt;().To&lt;MyCommand&gt;()。
        /// <c>where TSignal : BaseSignal</c> 保证 key 是信号类型，<c>where TCommand : Command</c> 保证绑定的是命令类型。
        /// </summary>
        public ICommandBinding Bind<TSignal, TCommand>()
            where TSignal : BaseSignal
            where TCommand : Command
        {
            return Bind<TSignal>().To<TCommand>();
        }

        /// <summary>
        /// 回收不再被任何绑定引用的命令池。池会缓存已创建的实例，Once() 等一次性绑定若不清理，
        /// 实例会被持有到 Bootstrap 销毁。
        /// 按当前存活的绑定重新统计引用，不调用 GetBinding（在有冲突未消解的 Binder 上它会抛异常）。
        /// </summary>
        protected void DropOrphanPools()
        {
            if (_pools.Count == 0) return;

            var referenced = CommandTypesInUse();
            List<Type> orphans = null;
            foreach (var pair in _pools)
            {
                if (referenced.Contains(pair.Key)) continue;
                orphans ??= new List<Type>();
                orphans.Add(pair.Key);
            }
            if (orphans == null) return;

            foreach (var type in orphans)
            {
                _pools[type].Clean();
                _pools.Remove(type);
            }
        }

        private HashSet<Type> CommandTypesInUse()
        {
            var types = new HashSet<Type>();
            foreach (var dict in _bindings.Values)
            {
                foreach (var binding in dict.Values)
                {
                    if (binding.value is not object[] values) continue;
                    foreach (var value in values)
                    {
                        if (value is Type type) types.Add(type);
                    }
                }
            }
            return types;
        }

        /// <summary>
        /// 池内命令实例的供应者：构造一个不入注册表的一次性绑定直接交给注入器，
        /// 既不依赖容器里有没有该命令类型的绑定，也不会往容器里留下任何东西。
        /// </summary>
        private sealed class CommandInstanceProvider : IInstanceProvider
        {
            private readonly CommandBinder _binder;

            public CommandInstanceProvider(CommandBinder binder)
            {
                _binder = binder;
            }

            public T GetInstance<T>()
            {
                return (T)GetInstance(typeof(T), false);
            }

            public object GetInstance(Type key, bool ignoreException)
            {
                return GetInstance(key, ignoreException, null);
            }

            public object GetInstance(Type key, bool ignoreException, InjectionScope scope)
            {
                var transient = new InjectionBinding(null);
                transient.Bind(typeof(Command)).To(key);
                var injector = _binder.injectionBinder.injector;
                //强转语义：类型不符时抛 InvalidCastException，而非静默返回 null
                var instance = (Command)injector.Instantiate(transient, false, scope);
                injector.TryInject(transient, instance, scope);
                return instance;
            }
        }
    }
}