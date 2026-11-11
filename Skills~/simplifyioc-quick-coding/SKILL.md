---
name: simplifyioc-quick-coding
description: 按 SimplifyIoC 2.1.0（Unity 分层 MVCS / IoC 框架）快速生成业务功能代码。当用户在 SimplifyIoC / Aixuma.* 工程中要求新增功能、加信号、加命令、写界面或 View、配置注入绑定、处理跨 Bootstrap 共享、搭建启动入口，或提出「按框架快速编程」「用 SimplifyIoC 实现 X」时使用。默认由 View 承接全部逻辑、不生成 Mediator，UI 一律以 SimplifyUI 为基类，绑定与调用优先写成链式 fluent 风格。产出 Bootstrap、Signal、Command、SimplifyUI/View 及绑定声明，并校验容器、订阅、生命周期等约束。
agent_created: true
---

# SimplifyIoC 快速编程

## 定位

面向**框架使用者**：把功能需求翻译成 SimplifyIoC **2.1.0** 的业务代码，而不是修改框架本身。

框架包内 `README.md` 与 `Assets/SimplifyIoC/Runtime/`、`Assets/SimplifyIoC/UI/` 源码**已对齐**，两者可相互参照；遇到签名疑问以源码为准。UI 能力已拆到独立程序集 `Aixuma.SimplifyIoC.UI`（与 `Runtime/` 平级的 `UI/`），核心 `Runtime/` 不含 UI 类型、也不依赖 uGUI。未经用户明确同意，不修改 `Assets/SimplifyIoC/Runtime/` 或 `Assets/SimplifyIoC/UI/` 下的框架源码；只在业务目录（如 `Assets/Examples/Scripts/Aixuma/` 或用户指定目录）新增文件。安装、分发与常见问题见本 Skill 同目录的 `README.md`（面向人阅读，生成代码时无需加载）。

## 三条默认约定（优先级高于框架文档里的通用示例）

1. **View 承接全部逻辑，不使用 Mediator。** 不生成 `Mediator` / `Mediator<TView>`，不写 `BindView<V, M>()`，不覆写 `BindViews()`。框架保证：即使 View 没有绑定 Mediator，`MediationBinder.Trigger(Awake)` 仍会执行容器注入与 `[ListensTo]` 挂载，因此 `[Inject]` / `[PostConstruct]` / `[ListensTo]` 在纯 View 下照常生效（依据 `MediationBinder.cs`）。**但 `[Child]` / `[BindEvent]` / `[BindMethod]` / `[MainThread]` 不再由核心自动解析**：只有 `SimplifyUI` 在首次 `Start` 解析一次；普通 View 要用这四个特性，得在 `Start` 里显式跑解析链（见 `references/api.md` 第 9 节），或改用 `[SerializeField]` / `transform.Find`。
2. **UI 开发以 `SimplifyUI` 为基类**（`SimplifyIoC.UI`，UI 程序集），它继承 `View` 并提供 `Show<T>()` / `Signal<T>()` / `uiSettings` / `canvasScale` / `Init()` / `OnShow()` 等能力。`UISettings` / `UIType` / `IPrefabCreator` 在 `SimplifyIoC.Settings`，声明式特性在 `SimplifyIoC.Utils`（后两者也在 UI 程序集）。只有非 UI 的场景表现层才直接继承 `View`。
3. **优先链式编程**：绑定写完整 fluent 链，钩子用表达式体，信号与 UI 配置用返回值继续调用，避免中间变量和分步赋值。详见下方「链式编程规范」。

## 工作流

### 第 1 步：读取参考资料

生成代码前加载 `references/api.md`（现行 API 与签名）与 `references/pitfalls.md`（有效约束与反模式）。签名查前者，能不能这么写查后者。

### 第 2 步：把需求映射到分层

按需求选出最小文件集：

| 需求 | 产出 | 绑定位置 |
| --- | --- | --- |
| 应用/场景启动入口 | `XxxBootstrap`（内含私有嵌套类 `StartupCommand`） | `SetStartupCommand()` |
| 一个交互或业务事件 | `XxxSignal` + `XxxCommand` | `BindCommands()` → `commandBinder.Bind<S>().To<C>()` |
| 纯服务/数据单例 | 接口 + 实现 | `BindValues()` → `injectionBinder.Bind<I>().To<V>().ToSingleton()` |
| UI 界面（面板/弹窗/Toast） | `XxxUI : SimplifyUI` | 不需要绑定视图；只绑它用到的信号与服务 |
| 非 UI 场景表现层 | `XxxView : View` | 同上 |
| UI 基础设施 | `UISettings` 资产 + 一次 `InitRoots(screen, overlay, popup)`（三参） | `BindValues()` → `injectionBinder.Bind<UISettings>().ToValue(asset)` |
| 跨场景共享实例 | 无需新增类型 | **链根** 一处 `Bind<I>().To<V>().ToSingleton().CrossContext()` |
| 事件只需调用一个服务方法 | 无需 Command | `commandBinder.Bind<S>().ToHandler<TService>(...)`（与 `To<C>()` 互斥） |

判断原则：数据向下走注入绑定，事件横向走 Signal → Command，表现层逻辑全部留在 View/SimplifyUI 内。禁止用 `[Inject]` 或绑定值传递一次性事件数据。

### 第 3 步：链式编程规范

1. **绑定一条链写完**：`injectionBinder.Bind<IScore>().To<ScoreService>().ToSingleton();`、`commandBinder.Bind<ScoreSignal>().To<AddScoreCommand>().Once();`。`SimplifyBootstrap` 的 `BindCommand<S,C>()` / `BindValue<I,V>()` / `BindSignal<S>()` 是等价简写，链更短时可用，但不要引入中间变量再分步调用。
2. **钩子与简单成员用表达式体**：`protected override void SetStartupCommand() => SetStartupCommand<StartupCommand>();`
3. **UI 内取信号直接链到派发**：`Signal<ScoreChangedSignal>().Dispatch(score);`——`SimplifyUI.Signal<T>()` 返回信号单例本身。
4. **UI 配置器返回自身**：为 `SimplifyUI` 派生类写 `public XxxUI SetTitle(string v) { ...; return this; }`，调用侧 `Show<XxxUI>().SetTitle("...").SetData(...)`；`Show<T>()` 返回 T，`UISettings.InitRoots(...)` 返回 `UISettings`（`UISettings` 没有 `ReleaseView<T>()`），均可继续链。关界面用 `SimplifyUI.Hide()`，回收由 `onRecycle` / `disposeOnHide` 处理。
5. **命令链的修饰串在尾部**：`.To<A>().To<B>().InSequence()` / `.Once()`。

注意 `SimplifyUI.Signal<T>()` 是方法、`SimplifyIoC.Signals.Signal<T>` 是类型：UI 类里写 `Signal<X>()` 带括号即调用方法；声明信号字段时用 `[Inject]` 属性注入，不要写 `Signal<T>` 类型成员以免歧义。

### 第 4 步：落地文件

1. 读取 `assets/templates/` 对应模板，替换 `{{FEATURE}}`（帕斯卡命名，如 `Score`）、`{{ROOT_NS}}`（默认 `Aixuma`）、`{{PAYLOAD_*}}` 等占位符。
2. MonoBehaviour 派生类独占同名 `.cs` 文件；**启动命令是例外**——写成 `Bootstrap` 内的 `private class StartupCommand : Command`，不单独建文件（框架自身 `SimplifyBootstrap.cs:163` 的 `private class StartupSignal` 即私有嵌套类）。业务命名空间按目录分层：`{ROOT_NS}.Bootstraps` / `.Commands` / `.Signals` / `.Views`（或 `.UI`）。框架类型用 `SimplifyIoC.*`。
3. 文本一律 **LF** 行尾。新脚本的 `.meta` 交由 Unity 自动生成，不手写、不编造 GUID。
4. 覆写生命周期方法时先调 `base`（`SimplifyUI.Start` 内含 `Init()` / `OnShow()`；`Bootstrap.Start` 内含 `SetStartupCommand()` + `Launch()`）。

### 第 5 步：自检（不可跳过）

- [ ] 没有任何 `Mediator` / `BindView` / `Mediators` 命名空间产物；视图逻辑全在 View / SimplifyUI 内。
- [ ] UI 类继承 `SimplifyUI`，非 UI 表现层才继承 `View`。
- [ ] 启动命令是 `Bootstrap` 内的 `private class StartupCommand`，没有独立的 `XxxStartupCommand.cs`；经 `SetStartupCommand()` 指定；覆写 `Start()` 保留 `base.Start()`。
- [ ] 绑定是 fluent 链或等价简写，钩子为表达式体，无中间变量分步赋值。
- [ ] UI 用到的信号已注册为单例（`BindSignals()` 或经 `BindCommand`/`commandBinder.Bind<S>()` 隐式注册）。
- [ ] 使用 `SimplifyUI` 时 `UISettings` 已绑定，且 `InitRoots(screen, overlay, popup)`（三参，返回 `UISettings`）在启动路径上被调用过一次。
- [ ] 普通 `View` / `Mediator` 上没有裸用的 `[Child]` / `[BindEvent]` / `[BindMethod]` / `[MainThread]`；要用就显式解析或改继承 `SimplifyUI`。
- [ ] `[Inject]` 只标**属性**；订阅走 `subscriptions.Listen(...)` / `subscriptions.Track(signal.AddOnce(...))`。
- [ ] 异步 Command 成对出现 `Retain()` / `Release()`；覆写 `Restore()` 调 `base.Restore()`。
- [ ] 跨 Bootstrap 绑定只在链根声明一次 `.CrossContext()`。
- [ ] 运行 `python scripts/lint_mvcs.py <新生成文件所在目录>`，ERROR 数必须为 0；WARN 逐条人工确认。

### 第 6 步：交付说明

列出新增文件、绑定落在哪个 Bootstrap、需要在 Unity 编辑器里挂到哪个 GameObject、UI 预置体需要登记到哪个 `CustomPrefabCreator`，以及手动验证路径。项目不跑自动化测试，验收由用户在 Unity 编辑器完成；不要声称已编译通过。

## 资源

- `references/api.md` — 现行 API 速查：Bootstrap 与链式绑定、Signal / Command、SimplifyUI / UISettings / UIType / IToast、声明式特性与命名空间归属。
- `references/pitfalls.md` — 现行硬约束：容器与注入、无 Mediator 时的视图行为、UI 基础设施、订阅与命令生命周期、跨域与工程约束。
- `assets/templates/` — 五个 C# 骨架：`Bootstrap`（含私有嵌套启动命令）、`Signal`、`Command`、`SimplifyUI`（UI）、`View`（非 UI 表现层）。
- `scripts/lint_mvcs.py` — 确定性静态自检：启动命令声明、注入点、行尾、AOT 禁用法、订阅与异步命令漏项、链式与 UI 约定、误用 Mediator / 误用 View 基类。用法 `python scripts/lint_mvcs.py <路径> [路径...]`。
