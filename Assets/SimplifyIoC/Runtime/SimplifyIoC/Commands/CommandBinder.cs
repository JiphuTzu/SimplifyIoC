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

        public CommandBinder()
        {
            usePooling = true;
        }
        public override void ResolveBinding(IBinding binding, object key)
        {
            base.ResolveBinding(binding, key);

            if (bindings.ContainsKey(key)) //If this key already exists, don't bind this again
            {
                var signal = (BaseSignal)key;
                signal.AddListener(ReactTo); //Do normal bits, then assign the commandlistener to be reactTo
            }
        }
        public override void OnRemove()
        {
            foreach (var key in bindings.Keys)
            {
                var signal = (BaseSignal)key;
                signal?.RemoveListener(ReactTo);
            }
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
            var signal = (BaseSignal)binding.key;
            var command = CreateCommandForSignal(cmd, data, signal.GetTypes()); //Special signal-only command creation
            command.sequenceId = depth;
            TrackCommand(command, binding);
            ExecuteCommand(command);
            return command;
        }
        /// Create a Command and bind its injectable parameters to the Signal types
        protected Command CreateCommandForSignal(Type cmd, object data, List<Type> signalTypes)
        {
            if (data != null)
            {
                var signalData = (object[])data;

                //Iterate each signal type, in order. 
                //Iterate values and find a match
                //If we cannot find a match, throw an error
                var injectedTypes = new HashSet<Type>();
                var values = new List<object>(signalData);

                foreach (var type in signalTypes)
                {
                    if (!injectedTypes.Contains(type)) // Do not allow more than one injection of the same Type
                    {
                        var foundValue = false;
                        foreach (var value in values)
                        {
                            if (value != null)
                            {
                                if (type.IsAssignableFrom(value.GetType())) //IsAssignableFrom lets us test interfaces as well
                                {
                                    injectionBinder.Bind(type).ToValue(value).ToInject(false);
                                    injectedTypes.Add(type);
                                    values.Remove(value);
                                    foundValue = true;
                                    break;
                                }
                            }
                            else //Do not allow null injections
                            {
                                throw new Exception("SignalCommandBinder attempted to bind a null value from a signal to Command: " + cmd.GetType() + " to type: " + type);
                            }
                        }
                        if (!foundValue)
                        {
                            throw new Exception("Could not find an unused injectable value to inject in to Command: " + cmd.GetType() + " for Type: " + type);
                        }
                    }
                    else
                    {
                        throw new Exception("SignalCommandBinder: You have attempted to map more than one value of type: " + type +
                            " in Command: " + cmd.GetType() + ". Only the first value of a type will be injected. You may want to place your values in a VO, instead.");
                    }
                }
            }
            var command = GetCommand(cmd);
            command.data = data;

            foreach (var typeToRemove in signalTypes) //clean up these bindings
                injectionBinder.Unbind(typeToRemove);
            return command;
        }
        
        protected Command GetCommand(Type type)
        {
            if (usePooling && pools.TryGetValue(type, out var pool))
            {
                var command = pool.GetInstance() as Command;
                if (command.isClean)
                {
                    injectionBinder.injector.Inject(command);
                    command.isClean = false;
                }
                return command;
            }
            else
            {
                injectionBinder.Bind<Command>().To(type);
                var command = injectionBinder.GetInstance<Command>();
                injectionBinder.Unbind<Command>();
                return command;
            }
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
            if (command.retain) return;
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

        public bool usePooling { get; set; }

        public Pool<T> GetPool<T>()
        {
            var t = typeof(T);
            if (pools.TryGetValue(t, out var pool))
                return pool as Pool<T>;
            return null;
        }
        
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
            var binding = injectionBinder.GetBinding(value);
            BaseSignal signal = null;

            if (value is Type type)
            {
                if (binding == null) //If this isn't injected yet, inject a new one as a singleton
                {
                    binding = injectionBinder.Bind(value) as IInjectionBinding;
                    binding.ToSingleton();
                }
                signal = injectionBinder.GetInstance(type,false) as BaseSignal;
            }
            return base.Bind(signal ?? value) as ICommandBinding;
        }
        /// <summary>Unbind by Signal Type</summary>
        /// <exception cref="InjectionException">If there is no binding for this type.</exception>
        public override void Unbind<T>()
        {
            var binding = GetBinding<T>();
            if (binding == null) return;
            var signal = injectionBinder.GetInstance<T>();
            Unbind(signal, null);
        }

        /// <summary>Unbind by Signal Instance</summary>
        /// <param name="key">Instance of IBaseSignal</param>
        public override void Unbind(object key, object name)
        {
            if (bindings.ContainsKey(key))
            {
                var signal = (BaseSignal)key;
                signal.RemoveListener(ReactTo);
            }
            base.Unbind(key, name);
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
            const string POOL_NAME = "COMMAND_POOL";
            injectionBinder.Bind<Pool>().To(poolType).ToName(POOL_NAME);
            var pool = injectionBinder.GetInstance<Pool>(POOL_NAME);
            injectionBinder.Unbind<Pool>(POOL_NAME);
            return pool;
        }

        public new virtual ICommandBinding GetBinding<T>()
        {
            var signal = injectionBinder.GetInstance<T>();
            return base.GetBinding(signal) as ICommandBinding;
        }
    }
}