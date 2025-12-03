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

/**
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

namespace SimplifyIoC.Commands
{
    public class CommandBinder : Binder, ICommandBinder, IPooledCommandBinder
    {
        [Inject]
        public IInjectionBinder injectionBinder { get; set; }

        protected Dictionary<Type, Pool> pools = new Dictionary<Type, Pool>();

        /// Tracker for parallel commands in progress
        protected HashSet<ICommand> activeCommands = new HashSet<ICommand>();

        /// Tracker for sequences in progress
        protected Dictionary<ICommand, ICommandBinding> activeSequences = new Dictionary<ICommand, ICommandBinding>();

        public CommandBinder()
        {
            usePooling = true;
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
            if (data is IPoolable)
            {
                (data as IPoolable).Retain();
            }
            var binding = GetBinding(trigger) as ICommandBinding;
            if (binding != null)
            {
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

        protected virtual ICommand InvokeCommand(Type cmd, ICommandBinding binding, object data, int depth)
        {
            var command = CreateCommand(cmd, data);
            command.sequenceId = depth;
            TrackCommand(command, binding);
            ExecuteCommand(command);
            return command;
        }

        protected virtual ICommand CreateCommand(object cmd, object data)
        {
            var command = GetCommand(cmd as Type);

            if (command == null)
            {
                var msg = "A Command ";
                if (data != null)
                {
                    msg += "tied to data " + data.ToString();
                }
                msg += " could not be instantiated.\nThis might be caused by a null pointer during instantiation or failing to override Execute (generally you shouldn't have constructor code in Commands).";
                throw new CommandException(msg, CommandExceptionType.BAD_CONSTRUCTOR);
            }

            command.data = data;
            return command;
        }

        protected ICommand GetCommand(Type type)
        {
            if (usePooling && pools.ContainsKey(type))
            {
                var pool = pools[type];
                var command = pool.GetInstance() as ICommand;
                if (command.isClean)
                {
                    injectionBinder.injector.Inject(command);
                    command.isClean = false;
                }
                return command;
            }
            else
            {
                injectionBinder.Bind<ICommand>().To(type);
                var command = injectionBinder.GetInstance<ICommand>();
                injectionBinder.Unbind<ICommand>();
                return command;
            }
        }

        protected void TrackCommand(ICommand command, ICommandBinding binding)
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

        protected void ExecuteCommand(ICommand command)
        {
            if (command == null)
            {
                return;
            }
            command.Execute();
        }

        public virtual void Stop(object key)
        {
            if (key is ICommand && activeSequences.ContainsKey(key as ICommand))
            {
                RemoveSequence(key as ICommand);
            }
            else
            {
                var binding = GetBinding(key) as ICommandBinding;
                if (binding != null)
                {
                    if (activeSequences.ContainsValue(binding))
                    {
                        foreach (var sequence in activeSequences)
                        {
                            if (sequence.Value == binding)
                            {
                                var command = sequence.Key;
                                RemoveSequence(command);
                            }
                        }
                    }
                }
            }
        }

        public virtual void ReleaseCommand(ICommand command)
        {
            if (command.retain == false)
            {
                var t = command.GetType();
                if (usePooling && pools.ContainsKey(t))
                {
                    pools[t].ReturnInstance(command);
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
        }

        public bool usePooling { get; set; }

        public Pool<T> GetPool<T>()
        {
            var t = typeof(T);
            if (pools.ContainsKey(t as Type))
                return pools[t] as Pool<T>;
            return null;
        }

        // protected override IBinding PerformKeyValueBindings(List<object> keyList, List<object> valueList)
        // {
        //     IBinding binding = null;
        //
        //     // Bind in order
        //     foreach (var key in keyList)
        //     {
        //         //Attempt to resolve key as a class
        //         var keyType = Type.GetType(key as string);
        //         Enum enumerator = null;
        //         if (keyType == null)
        //         {
        //             //If it's not a class, attempt to resolve as an Enum
        //             var keyString = key as string;
        //             var separator = keyString.LastIndexOf(".");
        //             if (separator > -1)
        //             {
        //                 var enumClassName = keyString.Substring(0, separator);
        //                 var enumType = Type.GetType(enumClassName as string);
        //                 if (enumType != null)
        //                 {
        //                     var enumName = keyString.Substring(separator + 1);
        //                     enumerator = Enum.Parse(enumType, enumName) as Enum;
        //                 }
        //             }
        //         }
        //         //If all else fails, just bind the original key
        //         var bindingKey = keyType ?? enumerator ?? key;
        //         binding = Bind(bindingKey);
        //     }
        //     foreach (var value in valueList)
        //     {
        //         var valueType = Type.GetType(value as string);
        //         if (valueType == null)
        //         {
        //             throw new BinderException("A runtime Command Binding has resolved to null. Did you forget to register its fully-qualified name?\n Command:" + value, BinderExceptionType.RUNTIME_NULL_VALUE);
        //         }
        //         binding = binding.To(valueType);
        //     }
        //
        //     return binding;
        // }

        /// Additional options: Once, InParallel, InSequence, Pooled
        // override protected IBinding AddRuntimeOptions(IBinding b, List<object> options)
        // {
        //     base.AddRuntimeOptions(b, options);
        //     ICommandBinding binding = b as ICommandBinding;
        //     if (options.IndexOf("Once") > -1)
        //     {
        //         binding.Once();
        //     }
        //     if (options.IndexOf("InParallel") > -1)
        //     {
        //         binding.InParallel();
        //     }
        //     if (options.IndexOf("InSequence") > -1)
        //     {
        //         binding.InSequence();
        //     }
        //     if (options.IndexOf("Pooled") > -1)
        //     {
        //         binding.Pooled();
        //     }

        //     return binding;
        // }

        private void RemoveSequence(ICommand command)
        {
            if (activeSequences.ContainsKey(command))
            {
                command.Cancel();
                activeSequences.Remove(command);
            }
        }

        public new virtual ICommandBinding Bind<T>()
        {
            return base.Bind<T>() as ICommandBinding;
        }

        public new virtual ICommandBinding Bind(object value)
        {
            return base.Bind(value) as ICommandBinding;
        }

        protected override void Resolver(IBinding binding)
        {
            base.Resolver(binding);
            if (usePooling && (binding as ICommandBinding).isPooled)
            {
                if (binding.value != null)
                {
                    var values = binding.value as object[];
                    foreach (Type value in values)
                    {
                        if (pools.ContainsKey(value) == false)
                        {
                            var myPool = MakePoolFromType(value);
                            pools[value] = myPool;
                        }
                    }
                }
            }
        }

        protected virtual Pool MakePoolFromType(Type type)
        {
            var poolType = typeof(Pool<>).MakeGenericType(type);

            injectionBinder.Bind(type).To(type);
            injectionBinder.Bind<Pool>().To(poolType).ToName(CommandKeys.COMMAND_POOL);
            var pool = injectionBinder.GetInstance<Pool>(CommandKeys.COMMAND_POOL);
            injectionBinder.Unbind<Pool>(CommandKeys.COMMAND_POOL);
            return pool;
        }

        public new virtual ICommandBinding GetBinding<T>()
        {
            return base.GetBinding<T>() as ICommandBinding;
        }
    }
}