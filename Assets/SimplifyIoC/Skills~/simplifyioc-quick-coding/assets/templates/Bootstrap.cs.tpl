// 模板：启动入口（2.1.0）。替换 {{FEATURE}}（帕斯卡命名，如 Score）与 {{ROOT_NS}}（默认 Aixuma）。
// 挂到显示层级顶部的 GameObject 上；不要覆写 Awake / Init，归属由框架自动解析。
// 默认不使用 Mediator：不覆写 BindViews()、不写 BindView<V,M>()。
// 启动命令是本类内的私有嵌套类，不单独建 .cs 文件。
// 框架自身 SimplifyBootstrap.cs:163 的 private class StartupSignal 走的也是同一条反射实例化路径。
// 嵌套类保持编译器默认 public 无参构造，不要手写 private 构造，否则反射找不到构造函数。
using SimplifyIoC.Bootstraps;
using SimplifyIoC.Commands;
using {{ROOT_NS}}.Commands;
using {{ROOT_NS}}.Signals;

namespace {{ROOT_NS}}.Bootstraps
{
    public class {{FEATURE}}Bootstrap : SimplifyBootstrap
    {
        protected override void SetStartupCommand() => SetStartupCommand<StartupCommand>();

        protected override void BindCommands() =>
            commandBinder.Bind<{{FEATURE}}Signal>().To<{{FEATURE}}Command>();

        // 只用于 UI 通知、没有绑定命令的信号，必须在这里注册成单例
        // （commandBinder.Bind<S>() 会自动注册，无需重复）：
        // protected override void BindSignals() =>
        //     injectionBinder.Bind<{{FEATURE}}ChangedSignal>().ToSingleton();

        // 服务与值绑定写成一条链；跨场景共享只在链根声明一次：
        // protected override void BindValues() =>
        //     injectionBinder.Bind<I{{FEATURE}}Service>().To<{{FEATURE}}Service>().ToSingleton();
        // protected override void BindValues() =>
        //     injectionBinder.Bind<I{{FEATURE}}Service>()
        //         .To<{{FEATURE}}Service>().ToSingleton().CrossContext();

        // 使用 SimplifyUI 时补 using SimplifyIoC.Settings;（UISettings），绑定资产并在启动路径调一次 InitRoots(screen, overlay, popup)：
        // [SerializeField] private UISettings uiSettingsAsset;
        // protected override void BindValues() =>
        //     injectionBinder.Bind<UISettings>().ToValue(uiSettingsAsset);

        // 控制启动时机才覆写 Start，且务必保留 base.Start()：
        // protected override void Start() { /* ... */ base.Start(); }

        // 启动命令：只做初始化编排，执行完即回池，不承载持续状态。
        // 需要 UI 根节点时（补 using SimplifyIoC.Settings; / UnityEngine;）：
        // [Inject] public UISettings uiSettings { get; set; }
        // 三个 Transform 由业务侧提供（例如 Bootstrap 上的 [SerializeField] 字段经命名绑定注入）；
        // 框架里没有 UIRoots 类型，InitRoots 是三参、返回 UISettings：
        // public override void Execute() =>
        //     uiSettings.InitRoots(screenRoot, overlayRoot, popupRoot);
        private class StartupCommand : Command
        {
            public override void Execute()
            {
                // 预加载、注册服务、派发首个信号等。
            }
        }
    }
}
