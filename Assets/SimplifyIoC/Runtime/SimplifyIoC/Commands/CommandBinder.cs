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
 * @class SimplifyIoC.Commands.CommandBinder
 * 
 * A Binder that triggers the instantiation of Commands.
 * 
 * Commands are where the logic of your application belongs.
 * These Commands typically focus on a single function, such as
 * adding a View, requesting a service, reading from or saving to a model.
 * 
 * The act of binding events to Commands means that code needn't know
 * anything about an event recipient, or even how the event will be used.
 * For example, a Mediator might send out an event that two View objects
 * collided. A Command would then determine that the result of that event
 * was to Destroy both objects, tell a ScoreKeeper model to change the
 * score and request a message be sent to the server. Whether that
 * example means one Command or three is up to your coding preference...
 * CommandBinder can trigger one Command or multiple Commands off the
 * same event.
 * 
 * Note that CommandBinder also features sequencing. By default, CommandBinder fires all
 * Commands in parallel. If your binding specifies `InSequence()`, commands will run serially,
 * with the option of suspending the chain at any time.
 * 
 * Example bindings:

		Bind("someEvent").To<SomeCommand>(); //Works, but poor form to use strings. Use the next example instead

		Bind(EventMap.SOME_EVENT).To<SomeCommand>(); //Make it a constant

		Bind(ContextEvent.START).To<StartCommand>().Once(); //Destroy the binding immediately after a single use

		Bind(EventMap.END_GAME_EVENT).To<FirstCommand>().To<SecondCommand>().To<ThirdGCommand>().InSequence();

 * 
 * See Command for details on asynchronous Commands and cancelling sequences.
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

        protected Dictionary<Type, Pool> pools = new Dictionary<Type, Pool>();

        /// Tracker for parallel commands in progress
        protected HashSet<Command> activeCommands = new HashSet<Command>();

        /// Tracker for sequences in progress
        protected Dictionary<Command, ICommandBinding> activeSequences = new Dictionary<Command, ICommandBinding>();

        public override void ResolveBinding(IBinding binding, object key)
        {
            base.ResolveBinding(binding, key);

            if (bindings.ContainsKey(key)) //If this key already exists, don't bind this again
            {
                //P0#9 修复：key 未必是信号（字符串/枚举也可作 key），无条件强转会崩
                if (key is BaseSignal signal)
                    signal.AddListener(ReactTo);
            }
        }
        public override void OnRemove()
        {
            foreach (var key in bindings.Keys)
            {
                if (key is BaseSignal signal)
                    signal.RemoveListener(ReactTo);
            }
            //5.5：归还池持有的全部命令实例（统一池化后每条命令类型都常驻一个池），
            //并清空两组"进行中命令"追踪集合——旧实现只 Clear() 了 pools 字典本身，
            //实例与追踪集合都留着等 GC，Context 销毁后这些引用依然存在。
            foreach (var pool in pools.Values)
                pool.Clean();
            pools.Clear();
            activeCommands.Clear();
            activeSequences.Clear();
            //3.1：释放绑定注册表与冲突表（放在最后：上面还要读 bindings）
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

            //4.3：短路通路——绑定声明了 handler 时直接解析服务并调用，不创建 Command、不进命令链。
            //放在这里（而不是另起一个 signal listener）是为了与命令路径共用同一套订阅/退订逻辑：
            //ResolveBinding/Unbind/OnRemove 三处的 signal.AddListener/RemoveListener 都无需改动。
            if (binding is CommandBinding commandBinding && commandBinding.handler != null)
            {
                InvokeHandler(commandBinding, data);
                return;
            }

            if (binding.isSequence)
            {
                Next(binding, data, 0);
            }
            else
            {
                var values = binding.value as object[];
                var aa = values.Length + 1;
                for (var a = 0; a < aa; a++)
                {
                    Next(binding, data, a);
                }
            }
        }

        /// <summary>
        /// 4.3：执行 ToHandler 绑定的短路处理器。
        /// 服务经完整注入链解析（单例/transient 语义与普通绑定一致）；
        /// isOneOff 沿用命令路径语义——调用一次后移除绑定。
        /// 无载荷信号（Signal）的 data 为 null，统一归一为空数组，避免 handler 侧出现 null/空两种形态。
        /// </summary>
        private void InvokeHandler(CommandBinding binding, object data)
        {
            var service = injectionBinder.GetInstance(binding.handler.serviceType, false);
            binding.handler.invoke(service, data as object[] ?? Array.Empty<object>());
            if (binding.isOneOff)
                Unbind(binding);
        }

        protected void Next(ICommandBinding binding, object data, int depth)
        {
            var values = binding.value as object[];
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

        //EventCommandBinder (and perhaps other sub-classes) use this method to dispose of the data in sequenced commands
        protected virtual void DisposeOfSequencedData(object data)
        {
            //No-op. Override if necessary.
        }

        protected virtual Command InvokeCommand(Type cmd, ICommandBinding binding, object data, int depth)
        {
            //P0#9 修复：key 未必是信号，改用 as 判别；载荷类型只在有信号时解析
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
            //3.4.a：载荷不再临时 Bind 进全局容器（原 Bind(type).ToValue(value).ToInject(false)
            //→ 用完 Unbind）。改为构建调用级 InjectionScope 沿调用栈显式传递：
            //  - 嵌套/并发派发各自持有自己的载荷，互不覆盖；
            //  - 与用户已有的全局同名绑定不再产生冲突（原实现会直接把 Binder 打进 conflicted 状态）；
            //  - 池化与临时绑定两条命令创建路径共用同一个入口。
            var scope = BuildScopeFromSignal(signalTypes, data, cmd);

            var command = GetCommand(cmd, scope);
            command.data = data;
            return command;
        }

        /// <summary>
        /// 3.4.a：把信号声明类型与载荷配成调用级作用域。
        /// 配对规则与原"逐类型临时 Bind"的逻辑逐条对齐（顺序、去重、未匹配即报错、null 载荷即报错），
        /// 只是落点由全局容器改为调用级 Scope——所以报错行为保持不变。
        /// 另：P0#9 守卫——key 非信号时 signalTypes 为 null，原实现在此处 foreach null 直接 NRE；
        /// 原错误信息里的 cmd.GetType() 恒为 System.RuntimeType，一并改为直接输出 cmd。
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

            //5.5：单一创建路径——无论绑定声明没声明 Pooled()，命令实例一律来自该命令类型的池。
            //旧实现在这两条路径间分叉（池化 vs 一次性 new），分叉的存在又反过来要求
            //usePooling / isPooled 两个开关的表达一致，是既难解释又难维护的一处状态。
            var pool = GetOrCreatePool(type);
            if (pool.GetInstance(scope) is not Command command) return null;
            if (!command.isClean) return command;
            //P0#6 修复：池中实例已由 Unity/工厂构造完成，构造注入会凭空多造一个实例
            //且新实例不在池的使用名单里、归还时被静默丢弃。回收实例只做 setter/PostConstruct 注入。
            injectionBinder.injector.Inject(command, false, scope);
            command.isClean = false;
            return command;
        }

        /// <summary>
        /// 5.5：命令类型的池，按需创建。相对旧 <see cref="MakePoolFromType"/> 的两处关键差别：
        /// ① 不再由 Resolver 在绑定期扫绑定值预先灌好，改为首次派发该命令类型时惰性创建——
        ///    没跑到过的命令不再白占一个池；
        /// ② 不再往 injectionBinder 里补那条 `Bind(cmdType).To(cmdType)`：
        ///    它一旦与用户自己的同名绑定相遇就会把容器打进 conflicted 状态（此后任何 GetBinding 都抛异常）。
        ///    改为由内建的 <see cref="CommandInstanceProvider"/> 供应实例，容器注册表全程不被触碰。
        /// </summary>
        protected Pool GetOrCreatePool(Type commandType)
        {
            if (pools.TryGetValue(commandType, out var existing)) return existing;

            var pool = MakePoolFromType(commandType);
            pools[commandType] = pool;
            return pool;
        }

        /// <summary>
        /// 5.5：保留为可扩展点（GetOrCreatePool 的唯一创建出口，override 它就换了全部命令的池实现）。
        /// 相对旧实现的两处关键差别：
        /// ① 不再往 injectionBinder 里补那条 `Bind(type).To(type)`：它一旦与用户自己的同名绑定相遇，
        ///    就会把容器打进 conflicted 状态（此后任何 GetBinding 都抛异常）。
        ///    改为显式指定 <see cref="CommandInstanceProvider"/>，容器注册表全程不被触碰；
        /// ② 池不再经 injectionBinder.injector.Inject(pool) 去凑 instanceProvider——
        ///    注入进来的本来就是 injectionBinder 自身，显式赋值把这层绕开了。
        /// 2.5 的结论仍然有效：Pool&lt;T&gt; 相对非泛型 Pool 只多一层编译期转换糖，
        /// 这里直接构造非泛型 Pool 并显式设 poolType，避免 MakeGenericType 的 IL2CPP 裁剪风险。
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
                activeSequences.Add(command, binding);
            }
            else
            {
                activeCommands.Add(command);
            }
        }

        protected void ExecuteCommand(Command command)
        {
            command?.Execute();
        }

        public virtual void Stop(object key)
        {
            if (key is Command command1 && activeSequences.ContainsKey(command1))
            {
                RemoveSequence(command1);
            }
            else if (GetBinding(key) is ICommandBinding binding)
            {
                if (!activeSequences.ContainsValue(binding)) return;
                foreach (var sequence in activeSequences)
                {
                    if (sequence.Value != binding) continue;
                    var command = sequence.Key;
                    RemoveSequence(command);
                }
            }
        }

        public virtual void ReleaseCommand(Command command)
        {
            //P0#9 修复：Next 在绑定值为空等情况下会传入 null
            if (command == null) return;
            if (command.retain) return;
            var t = command.GetType();
            //5.5：不再分支——统一池化后该命令类型的池一定存在（不存在说明本 Binder 没创建过它，忽略即可）
            if (pools.TryGetValue(t, out var pool))
            {
                pool.ReturnInstance(command);
            }
            if (activeCommands.Contains(command))
            {
                activeCommands.Remove(command);
            }
            else if (activeSequences.ContainsKey(command))
            {
                var binding = activeSequences[command];
                var data = command.data;
                activeSequences.Remove(command);
                Next(binding, data, command.sequenceId + 1);
            }
        }

        /// <summary>
        /// 5.5：已废弃的开关，保留为 no-op。
        /// 旧语义是"派发时是否回收命令实例"：false 时每次派发都新建实例，true 且绑定声明 Pooled() 时才走池。
        /// 现在所有命令统一池化，两条路径合并成一条，本开关不再有任何作用；
        /// 读写仍然编译通过，避免升级时出现大面积源码报错。计划在后续主版本中移除。
        /// </summary>
        [Obsolete("所有命令现已统一池化，usePooling 不再影响行为（非池化路径已移除）。")]
        public bool usePooling { get; set; }

        private void RemoveSequence(Command command)
        {
            if (!activeSequences.ContainsKey(command)) return;
            command.Cancel();
            activeSequences.Remove(command);
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
            if (bindings.ContainsKey(key) && key is BaseSignal signal)
            {
                signal.RemoveListener(ReactTo);
            }
            base.Unbind(key, name);
            //5.5：绑定已删，为它服务过的命令池若无人再引用就该归还（见 DropOrphanPools 注释）。
            DropOrphanPools();
        }

        public new virtual ICommandBinding GetBinding<T>()
        {
            var signal = injectionBinder.GetInstance<T>();
            return base.GetBinding(signal) as ICommandBinding;
        }

        /// <summary>
        /// 5.3：信号/命令成对入口，Bind&lt;MySignal, MyCommand&gt;() 等价于 Bind&lt;MySignal&gt;().To&lt;MyCommand&gt;()。
        /// 两条约束是这一步真正的收益：
        /// <c>where TSignal : BaseSignal</c> —— 旧写法里 Bind&lt;T&gt;() 会用 injectionBinder 去 GetInstance&lt;T&gt;()
        /// 再把结果当信号用，T 不是信号时只能在运行期崩；
        /// <c>where TCommand : Command</c> —— 命令一如既往做到了委托/强转校验之前就该明确的事。
        /// </summary>
        public ICommandBinding Bind<TSignal, TCommand>()
            where TSignal : BaseSignal
            where TCommand : Command
        {
            return Bind<TSignal>().To<TCommand>();
        }

        /// <summary>
        /// 5.5：绑定消失后回收"只为它服务"的命令池。
        /// 统一池化后每个命令类型都常驻一个池（默认 size=0 的池会按需膨胀并缓存峰值并发数的实例），
        /// Once() 这类一次性绑定如果不清理，实例会被持有到 Context 销毁为止。
        /// 实现上不回头看"刚刚删掉的是谁"，而是按当前还活着的绑定重新统计——
        /// 这样也不用调 GetBinding（在有冲突未消解的 Binder 上它会抛异常）。
        /// </summary>
        protected void DropOrphanPools()
        {
            if (pools.Count == 0) return;

            var referenced = CommandTypesInUse();
            List<Type> orphans = null;
            foreach (var pair in pools)
            {
                if (referenced.Contains(pair.Key)) continue;
                orphans ??= new List<Type>();
                orphans.Add(pair.Key);
            }
            if (orphans == null) return;

            foreach (var type in orphans)
            {
                pools[type].Clean();
                pools.Remove(type);
            }
        }

        private HashSet<Type> CommandTypesInUse()
        {
            var types = new HashSet<Type>();
            foreach (var dict in bindings.Values)
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
        /// 5.5：池内命令实例的供应者。
        /// 手法沿用 3.4.b：构造一个不入注册表的一次性绑定直接交给注入器，
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
                //保持原 GetInstance<Command>() 的强转语义（类型不符时抛 InvalidCastException 而非静默 null）
                var instance = (Command)injector.Instantiate(transient, false, scope);
                injector.TryInject(transient, instance, scope);
                return instance;
            }
        }
    }
}