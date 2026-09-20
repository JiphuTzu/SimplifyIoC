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
 * @class SimplifyIoC.Commands.Command
 * 
 * Commands are where you place your business logic.
 * 
 * In the MVCSContext setup, commands are mapped to IEvents. 
 * The firing of a specific event on the global event bus triggers 
 * the instantiation, injection and execution of any Command(s) bound to that event.
 * 
 * By default, commands are cleaned up immediately on completion of the `Execute()` method.
 * For asynchronous Commands (e.g., calling a service and awaiting a response),
 * call `Retain()` at the top of your `Execute()` method, which will prevent
 * premature cleanup. But remember, having done so it is your responsipility
 * to call `Release()` once the Command is complete.
 * 
 * Calling `Fail()` will terminate any sequence in which the Command is operating, but
 * has no effect on Commands operating in parallel.
 */

using SimplifyIoC.Injectors;
using SimplifyIoC.Pools;
using SimplifyIoC.Signals;

namespace SimplifyIoC.Commands
{
    public abstract class Command : IPoolable
    {
        /// Back reference to the CommandBinder that instantiated this Commmand
        [Inject]
        public ICommandBinder commandBinder { get; set; }

        /// The InjectionBinder for this Context
        [Inject]
        public IInjectionBinder injectionBinder { get; set; }

        public object data { get; set; }

        public bool cancelled { get; set; }

        public bool isClean { get; set; }

        public int sequenceId { get; set; }
        public bool retain { get; set; }

        /// 5.1：订阅组（惰性创建，从未订阅过则不分配）。
        private SignalSubscriptionGroup _subscriptions;

        /// <summary>
        /// 5.1：随本 Command 生命周期的订阅组。Restore()（回收前清理）时自动退订组内全部订阅。
        /// Retain() 的异步 Command 最容易漏解绑——它活得比 Execute 长，
        /// 挂在长期存在的 Signal 上就是一条稳定的泄漏路径。
        /// </summary>
        protected SignalSubscriptionGroup subscriptions => _subscriptions ??= new SignalSubscriptionGroup();

        public Command()
        {
            //Set to false on construction to ensure that it's not double-injected on first use.
            //The pool will satisfy all injections on first use. The CommandBinder re-injects
            //every time the Command is recycled.
            isClean = false;
        }

        public abstract void Execute();

        public virtual void Retain()
        {
            retain = true;
        }

        public virtual void Release()
        {
            retain = false;
            commandBinder?.ReleaseCommand(this);
        }

        /// Use/override this method to clean up the Command for recycling
        public virtual void Restore()
        {
            //5.1：回收前退订组内订阅。覆写本方法时记得调 base.Restore()。
            _subscriptions?.Dispose();
            //5.5：统一池化后每条命令执行完都会走到这里，Restore 从"仅 Pooled() 命令才会触发"
            //变成主路径上的常规动作。注入保证了 injectionBinder 非空，但派生类被回收的路径不止一条
            //（例如注入中途失败后落到池里），这里守住 NRE——回收动作本身不该再抛异常。
            injectionBinder?.injector.Uninject(this);
            isClean = true;
        }

        public virtual void Fail()
        {
            commandBinder?.Stop(this);
        }

        public void Cancel()
        {
            cancelled = true;
        }
    }
}