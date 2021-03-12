using SimplifyIoC.Commands;
using SimplifyIoC.Injectors;
using SimplifyIoC.Signals;
//============================================================
//@author	JiphuTzu
//@create	2019/11/13
//@company	UMa
//
//@description:修改启动模式为手动启动，需要调用Launch方法，在Launch时会广播StartupSignal
//             0.指定绑定一个启动命令到StartupSignal
//             1.添加简便绑定方法
//             2.完成虚方法，在方法中进行分类绑定
//============================================================
namespace SimplifyIoC.Contexts
{
    public abstract class SimplifyContext<B, T> : MVCSContext where B : SimplifyBootstrap where T : Command
    {
        protected B bootstrap { get; private set; }

        public SimplifyContext(B view) : base(view, ContextStartupFlags.MANUAL_LAUNCH) { }
        public override IContext SetContextView(object view)
        {
            bootstrap = view as B;
            return base.SetContextView(view);
        }
        protected sealed override void mapBindings()
        {
            base.mapBindings();
            BindCommand<StartupSignal, T>(true, false);
            BindCommands();
            BindViews();
            BindSignals();
            BindValues();
        }
        public override void Launch()
        {
            injectionBinder.GetInstance<StartupSignal>().Dispatch();
        }
        protected abstract void BindCommands();

        protected abstract void BindViews();

        protected abstract void BindSignals();

        protected abstract void BindValues();
        /// <summary>
        /// 信号和命令绑定
        /// </summary>
        /// <typeparam name="S">信号类型</typeparam>
        /// <typeparam name="C">命令类型</typeparam>
        /// <param name="once">该命令是否只执行一次</param>
        protected void BindCommand<S, C>(bool once = false, bool crossContext = false)
        {
            if (crossContext) BindSignal<S>(true);
            var binding = commandBinder.Bind<S>();
            binding = binding.To<C>();
            if (once) binding = binding.Once();
        }
        protected void BindCommand<S, C, D>(bool once = false, bool crossContext = false, bool inSequence = false)
        {
            if (crossContext) BindSignal<S>(true);
            var binding = commandBinder.Bind<S>();
            binding = binding.To<C>();
            binding = binding.To<D>();
            if (once) binding = binding.Once();
            if (inSequence) binding = binding.InSequence();
        }
        protected void BindCommand<S, C, D, E>(bool once = false, bool crossContext = false, bool inSequence = false)
        {
            if (crossContext) BindSignal<S>(true);
            var binding = commandBinder.Bind<S>();
            binding = binding.To<C>();
            binding = binding.To<D>();
            binding = binding.To<E>();
            if (once) binding = binding.Once();
            if (inSequence) binding = binding.InSequence();
        }
        protected void BindCommand<S, C, D, E, F>(bool once = false, bool crossContext = false, bool inSequence = false)
        {
            if (crossContext) BindSignal<S>(true);
            var binding = commandBinder.Bind<S>();
            binding = binding.To<C>();
            binding = binding.To<D>();
            binding = binding.To<E>();
            binding = binding.To<F>();
            if (once) binding = binding.Once();
            if (inSequence) binding = binding.InSequence();
        }
        /// <summary>
        /// View和Mediator绑定
        /// </summary>
        /// <typeparam name="V">View类型</typeparam>
        /// <typeparam name="M">Mediator类型</typeparam>
        protected void BindView<V, M>()
        {
            mediationBinder.Bind<V>().ToMediator<M>();
        }
        /// <summary>
        /// View和Mediator绑定到View的父类A上
        /// </summary>
        /// <typeparam name="V">View的类型</typeparam>
        /// <typeparam name="M">Mediator类型</typeparam>
        /// <typeparam name="A">View的父类</typeparam>
        protected void BindView<V, M, A>() where V : A
        {
            mediationBinder.Bind<V>().ToMediator<M>().ToAbstraction<A>();
        }
        /// <summary>
        /// 绑定为信号单例
        /// 信号为一个特殊的值类型
        /// </summary>
        /// <typeparam name="S">信号类型</typeparam>
        protected void BindSignal<S>(bool crossContext = false)
        {
            BindValue<S>(crossContext);
        }
        /// <summary>
        /// 绑定值类型为单例
        /// </summary>
        /// <typeparam name="V">类型</typeparam>
        /// <param name="crossContext">是否跨域</param>
        protected void BindValue<V>(bool crossContext = false)
        {
            IInjectionBinding binding = injectionBinder.Bind<V>().ToSingleton();
            if (crossContext) binding.CrossContext();
        }
        /// <summary>
        /// 绑定值类型到接口
        /// </summary>
        /// <typeparam name="I">接口类型</typeparam>
        /// <typeparam name="V">值类型</typeparam>
        /// <param name="crossContext">是否跨域</param>
        protected void BindValue<I, V>(bool crossContext = false)
        {
            IInjectionBinding binding = injectionBinder.Bind<I>().To<V>().ToSingleton();
            if (crossContext) binding.CrossContext();
        }
        /// <summary>
        /// 绑定值类型到指定值
        /// </summary>
        /// <typeparam name="V">类型</typeparam>
        /// <param name="value">值</param>
        /// <param name="name">绑定名称</param>
        /// <param name="crossContext">是否跨域</param>
        protected void BindValue<V>(V value, string name = null, bool crossContext = false)
        {
            IInjectionBinding binding = injectionBinder.Bind<V>().ToValue(value);
            if (!string.IsNullOrEmpty(name)) binding.ToName(name);
            if (crossContext) binding.CrossContext();
        }
        //================================
        private class StartupSignal : Signal { }
    }
}