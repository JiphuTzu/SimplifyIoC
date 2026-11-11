# 现行约束清单（2.1.0）

只记录现行有效的约定与约束，生成代码前通读一遍。包内 README 与源码已对齐，两者可相互参照。

## 一、默认架构约定

1. **View 承接全部逻辑，不使用 Mediator。** 不生成 `Mediator` / `Mediator<TView>`，不写 `BindView<V,M>()`，不覆写 `BindViews()`，不建 `.Mediators` 目录。
   - **依据**：`MediationBinder.Trigger(Awake)` 在 `GetBinding(viewType)` 为空时仍调用 `InjectViewAndChildren(view)`（`MediationBinder.cs`），后者执行 `injectionBinder.injector.Inject(view, false)` 与 `HandleDelegates(view, view.GetType(), true)`。
   - **结果**：`[Inject]` / `[PostConstruct]` / `[Construct]` / `[ListensTo]` 在纯 View 下可用，界面逻辑放心写在 View 里。**但四个声明式特性不再自动解析**（见第三节），普通 View 要用得显式跑解析链，或改继承 `SimplifyUI`。
   - 只有用户**明确要求** Mediator 时才例外，按 `api.md` 的现成类型处理。
2. **UI 开发以 `SimplifyUI` 为基类。** 面板、弹窗、Toast、HUD 一律 `XxxUI : SimplifyUI`；只有非 UI 的场景表现层组件才直接继承 `View`。UI 类里不要再套一层逻辑转发类。
3. **优先链式编程。** 绑定写完整 fluent 链，不要拆成中间变量再分步赋值；钩子与简单成员用表达式体；UI 配置器写成 `public XxxUI SetTitle(string v) { ...; return this; }`，调用侧 `Show<XxxUI>().SetTitle("...").SetData(...)`；信号 `Signal<XxxSignal>().Dispatch(...)`。`BindCommand` / `BindValue` / `BindSignal` 便捷方法与直接操作 binder 的链等价，按更短者选用，但不要把同一绑定拆开写。

## 二、容器与绑定

- **CommandBinder 的 key 只能是 Signal 类型**：派发由 `CommandBinder.ResolveBinding` 挂 `signal.AddListener(ReactTo)` 驱动，字符串 / 枚举 key 能编译通过却没有触发路径。
- **`[Inject]` 只作用于属性**（`AttributeTargets.Property`），写法必须是 `public X x { get; set; }`。
- **命名绑定的名字是装箱的枚举原值**（如 `BootstrapKeys.Bootstrap`），不要归一化成 `Enum.ToString()`，也不要一侧用字符串、另一侧用枚举。
- **信号必须先注册成单例才能取到**：`commandBinder.Bind<S>()` 与 `BindCommand<S,C>()` 会自动把 S 注册为单例；只用于 UI 通知、没有绑定命令的信号，要在 `BindSignals()` 里显式 `injectionBinder.Bind<S>().ToSingleton()`，否则 `Signal<T>()` / `injectionBinder.GetInstance<S>()` / `[Inject] S` 都拿不到。
- **`MapBindings()` 在 `SimplifyBootstrap` 里是 `sealed override`**，只覆写四个 `Bind*` 钩子。它在 `Init` 的构造期被虚调用：只放**类型绑定**；Unity 序列化字段此时已由反序列化填好、可安全读取，但不要依赖派生类里 C# 字段初始化器赋的值。
- **`Bootstrap.Init()` 是 `internal` 无参**，由 `Awake` 驱动，业务侧不直接调用。归属自动解析（Transform 层级 > `Bootstrap.root`）。
- **覆写 `Start()` 必须 `base.Start()`**（内含 `SetStartupCommand()` + `Launch()`）；只覆写 `Awake()` 也要 `base.Awake()`。直接派生 `Bootstrap` 的类需自行显式 `Launch()`。
- **启动命令写成 `Bootstrap` 内的私有嵌套类**（`private class StartupCommand : Command`），不单独建 `.cs` 文件。链路 `CommandInstanceProvider` → `Injector.Instantiate` → `ReflectionBinder.FindPreferredConstructor` → `ConstructorInfo.Invoke` 对私有嵌套类型可行（框架自身 `SimplifyBootstrap.cs:163` 的 `private class StartupSignal` 已走通）。**必须保持编译器默认 public 无参构造**，显式写 private 构造会让 `FindPreferredConstructor` 返回 null 并抛 `The reflector requires concrete classes`。Unity 的「MonoBehaviour 派生类独占同名 `.cs`」只约束 Component；`Command` 不是 MonoBehaviour。
- **netstandard2.1 / C# 9**：无协变返回（CS8830）；AOT 禁止 `Expression.Compile` / `Reflection.Emit`，反射解析一律委托化。包内自带 `link.xml` 裁剪兜底。

## 三、View / SimplifyUI 生命周期

- `SimplifyUI.Start` = `base.Start()` → `EnsureAttributesInitialized()`（幂等解析一次声明式特性）→ `Init()` → `_initialized = true` → `OnShow()`；`OnEnable` 在已初始化时也会调 `OnShow()`。覆写 `Start()` / `OnEnable()` 必须调 `base`。
- **`Init()` 是一次性初始化，`OnShow()` 是每次展示的刷新**。`disposeOnHide = false` 的 UI 会从缓存复用，状态重置必须放 `OnShow()`，不要放 `Init()`。
- `Hide(false)` 只关闭、不广播 `onRecycle`（切换 Screen 时框架内部使用）；业务侧关界面用 `Hide()`。
- `disposeOnHide` 默认 `true` → 隐藏即 `Destroy`；`false` → 回 `UISettings._viewCache` 复用。
- **`Show<T>()` 依赖预置体登记**：`CustomPrefabCreator.GetUIPrefab(Type, out ...)` 找不到就返回 `null`。`DefaultPrefabCreator` 用 `[SerializeField] List<SimplifyUI> _views` 按名字与类型建索引，每个 UI 预置体都要登记。
- **`UISettings` 必须绑定且 `InitRoots` 必须调用一次**：框架内没有 `InitRoots` 的调用点，业务侧要在启动路径上完成 `injectionBinder.Bind<UISettings>().ToValue(asset)` 与 `InitRoots(screen, overlay, popup)`（**三参**、返回 `UISettings`；遮罩取 `popup.GetChild(0)`，没有 `mask` 参数），否则 `_viewRoot` 为空、`Show<T>()` 直接失败。
- **`UIType.Toast` 的视图必须实现 `IToast`**（`SetMessage` / `MoveUp` 均返回 `IToast` 供链式调用），否则 `UISettings.ShowToast` 直接返回、界面不显示。
- `Interactable(false)` 会往遮罩计数里加入当前名字，记得在对应路径成对调用 `Interactable(true)`。
- `SimplifyUI.Signal<T>()` 是**方法**，`SimplifyIoC.Signals.Signal<T>` 是**类型**：`Signal<X>()` 带括号即调用方法；UI 类里不要声明 `Signal<T>` 类型成员。
- **声明式特性只有 `SimplifyUI` 自动解析**（首次 `Start` 幂等一次，`OnDestroy` 释放 `[BindMethod]` 静态表条目）。普通 `View` / `Mediator` 上的这四个特性静默失效：要用就在 `Start` 里显式跑解析链（见 `api.md` 第 9 节）、改用 `[SerializeField]`，或改继承 `SimplifyUI`。`UnityEvent.AddListener` 不去重，无论哪种写法都不要解析两次。
- **`[ListensTo]` 的签名必须与 Signal 完全匹配**（无参 Signal → 无参方法），不匹配时 `CreateDelegate` 抛异常；基类里声明的 private 方法扫不到。不确定时改用 `subscriptions.Listen`。
- View 向上找不到任何 Bootstrap 时会注册到框架级锚点；`requiresBootstrap = false` 的 View 不参与兜底。

## 四、命令与订阅

- **Signal 载荷最多 4 个**；同一 Signal 声明中出现两个相同类型参数，`BuildScopeFromSignal` 会抛 "attempted to map more than one value of type"；载荷不允许 `null`。多值或同类型值用**值对象（VO）**打包。
- **命令统一池化：`Execute()` 返回即回收。** 异步命令必须在开头 `Retain()`、完成后显式 `Release()`；覆写 `Restore()` 必须调 `base.Restore()`（否则订阅组不 Dispose、注入不清）。
- **订阅一律走订阅组**：`subscriptions.Listen(sig, cb)`；`AddOnce` 写 `subscriptions.Track(sig.AddOnce(cb))`。直接 `AddListener` 就得自己 `RemoveListener`，Retain 的异步命令是最常见的泄漏点。
- **命令里不要长期持有状态**：实例回池后被复用，字段不会自动清空，靠 `Restore()` 归位。

## 五、跨 Bootstrap

- 每个 Bootstrap 一个 `CrossContextInjectionBinder`，普通绑定互不可见；只有标 `.CrossContext()` 的绑定被搬到共享根 binder。
- **生效前提：同一条链 + 父先于子构造。** 同类型只在**链根**声明一次；子 Bootstrap 重复登记会在共享根 binder 上同名冲突。
- 锚点 `Bootstrap.root`：第一个构造成功的链根自动占住（先到先得、不抢占），根 `Dispose()` 时清空、下一个链根接任。归属优先级 **Transform 层级 > 锚点兜底**；要指定锚点之外的父用 `AddBootstrap`，且应在子 Bootstrap `Init` 之前调用。
- `Dispose()` 之后不可复活：再 `Init()` / `Launch()` 抛 `ObjectDisposedException`。

## 六、工程约束与反模式

- **文本一律 LF**（仓库根 `.gitattributes`）。禁止 `\r\r\n` 与混合行尾；用 `git ls-files --eol` 判定。
- 新 `.cs` 的 `.meta` **交给 Unity 自动生成**，不要手编 GUID。改名/移动 Unity 资产后校验 `*.unity` / `*.prefab` 里 `m_Script` 的 GUID 链路——编译期查不出来。
- 项目不跑自动化测试，**验收由用户在 Unity 编辑器手动完成**，不要声称「已编译通过」。
- **未经明确同意不修改框架源码**（`Assets/SimplifyIoC/Runtime/` 与 `Assets/SimplifyIoC/UI/`）。业务代码只落在业务目录；发现框架缺陷时只报告。
- **反模式**：用 `[Inject]` / 绑定值传一次性事件数据（应走 Signal → Command）；为一句简单逻辑铺满多层（可用 `ToHandler` 短路）；把一次性初始化放进 `OnShow()`；在 UI 里再手写一层逻辑转发类。框架目前**没有**「Model → View 自动同步」，不要假装存在 MVVM 绑定。
