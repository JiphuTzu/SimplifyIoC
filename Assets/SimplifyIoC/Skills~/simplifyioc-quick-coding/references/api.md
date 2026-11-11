# SimplifyIoC 2.1.0 API 速查

版本依据：`package.json` 2.1.0、包内 `README.md` 与 `CHANGELOG.md` 的 `2.1.0` 段（程序集拆分后的现行结构）；源码在 `Assets/SimplifyIoC/Runtime/` 与 `Assets/SimplifyIoC/UI/`，两者可相互参照。本文件只记录**现行 API**。

## 1. 命名空间与类型归属

| 层 | 命名空间 | 常用类型 |
| --- | --- | --- |
| 启动/容器 | `SimplifyIoC.Bootstraps` | `Bootstrap`、`SimplifyBootstrap`、`BootstrapKeys` |
| 命令 | `SimplifyIoC.Commands` | `Command`、`CommandBinder`、`ICommandBinder`、`ICommandBinding` |
| 信号 | `SimplifyIoC.Signals` | `Signal`、`Signal<T...>`、`BaseSignal`、`SignalSubscription`、`SignalSubscriptionGroup` |
| 注入 | `SimplifyIoC.Injectors` | `IInjectionBinder`、`IInjectionBinding`、`Injector` |
| 视图（核心程序集） | `SimplifyIoC.Mediations` | `View`、`Mediator`、`IMediationBinder` |
| UI 界面（UI 程序集） | `SimplifyIoC.UI` | `SimplifyUI`、`IToast` |
| UI 配置（UI 程序集） | `SimplifyIoC.Settings` | `UISettings`、`UIType`、`IPrefabCreator`、`CustomPrefabCreator`、`DefaultPrefabCreator` |
| 工具特性（UI 程序集） | `SimplifyIoC.Utils` | `[BindEvent]`、`[Child]`、`[BindMethod]`、`[MainThread]`、`ReflectionExtension`、`DebugX` |
| 框架内核 | `SimplifyIoC.Framework` | `IBinder`、`IBinding`、`Binding`、`CrossContextBridge`、`InjectionScope` |
| 池 | `SimplifyIoC.Pools` | `IPool`、`Pool`、`IPoolable` |
| 反射 | `SimplifyIoC.Reflectors` | `ReflectedClass`、`ReflectionBinder` |

**全局命名空间（无需 using）**：`Inject`、`Name`、`Construct`、`PostConstruct`（`Injectors/InjectAttribute.cs`）、`ListensTo`（`Mediations/ListensTo.cs`）。

**程序集归属**：`SimplifyIoC.Mediations` 在核心 `Aixuma.SimplifyIoC`；`SimplifyIoC.UI` / `.Settings` / `.Utils` 都在可选程序集 `Aixuma.SimplifyIoC.UI`（依赖 uGUI）。使用 asmdef 的业务工程若用到后三者，需在 `references` 里补 `Aixuma.SimplifyIoC.UI`。UI 程序集按目录分层，`UI/SimplifyIoC/<目录>/` → `SimplifyIoC.<目录>`，与核心同规则。

业务命名空间按目录分层，示例工程用 `Aixuma.Bootstraps` / `.Commands` / `.Signals` / `.Views`。框架另提供 `Mediator` / `Mediator<TView>` / `IMediationBinder`，但**本工作流不使用**，除非用户明确要求，不生成也不绑定。

## 2. Bootstrap 入口（2.1.0）

`SimplifyBootstrap : Bootstrap` —— `Bootstraps/SimplifyBootstrap.cs:17`，`abstract` 基类。

| 成员 | 签名 / 说明 |
| --- | --- |
| 启动命令（唯一必须实现） | `protected abstract void SetStartupCommand()`，体内调 `SetStartupCommand<T>()`（`T : Command`），内部为 `BindCommand<StartupSignal, T>(true)` |
| 四组绑定钩子 | `protected virtual void BindCommands()` / `BindViews()` / `BindSignals()` / `BindValues()`，由 `sealed MapBindings()` 依次调用——**不要覆写 `MapBindings()`** |
| 启动 | `protected virtual void Start()` → `SetStartupCommand()` + `Launch()`；覆写必须 `base.Start()` |
| 派发启动信号 | `public override void Launch()` → `injectionBinder.GetInstance<StartupSignal>().Dispatch()` |

**启动命令写成派生 `Bootstrap` 内的私有嵌套类**：`private class StartupCommand : Command`，不单独建 `.cs` 文件。框架自身 `SimplifyBootstrap.cs:163` 的 `private class StartupSignal : Signal` 已由容器反射实例化，路径一致。嵌套类保持编译器默认 public 无参构造，不要显式写 private 构造（`ReflectionBinder.FindPreferredConstructor` 只扫 public 构造）。

### 便捷绑定方法（protected，等价简写）

| 签名 | 说明 |
| --- | --- |
| `BindCommand<S, C>(bool once = false, bool crossContext = false)` | `S : BaseSignal`、`C : Command`。**第一个 bool 是 once，第二个才是 crossContext** |
| `BindCommand<S, C, D>(bool once = false, bool crossContext = false, bool inSequence = false)` | 两个 Command；`inSequence` 控制串行（默认并行） |
| `BindCommand<S, C, D, E>` / `BindCommand<S, C, D, E, F>` | 三 / 四个 Command，参数同上 |
| `BindSignal<S>(bool crossContext = false)` | `S : BaseSignal`；内部是 `BindValue<S>(crossContext)`，把信号注册成单例 |
| `BindValue<V>(bool crossContext = false)` | `Bind<V>().ToSingleton()` |
| `BindValue<I, V>(bool crossContext = false)` | `Bind<I>().To<V>().ToSingleton()` |
| `BindValue<V>(V value, string name = null, bool crossContext = false)` | `ToValue(value)`，可带字符串名字 |

### `Bootstrap : MonoBehaviour, IDisposable`（`Bootstraps/Bootstrap.cs`）

- `public static Bootstrap root { get; set; }` —— 框架级锚点，第一个构造成功的链根自动占住（先到先得、不抢占），Dispose 时清空。
- `internal void Init()` —— 无参、幂等、仅框架程序集可见；`Awake` 自动调用。归属顺序：沿 Transform 向上找最近的已就绪 Bootstrap > `Bootstrap.root` 兜底。
- `public virtual void Launch()` —— 启动永远显式；直接派生 `Bootstrap`（不经 `SimplifyBootstrap`）的类需自行调用。
- `public virtual void AddView(View)` / `RemoveView` / `EnableView` / `DisableView`；`public virtual void AddBootstrap(Bootstrap)` / `void RemoveBootstrap(Bootstrap)`。
- `public void Dispose()` —— 幂等；级联释放子链、清共享根 binder、清锚点。之后 `Init` / `Launch` 抛 `ObjectDisposedException`。
- `public enum BootstrapKeys { Bootstrap }` —— 命名注入取 Bootstrap 自身：`[Inject(BootstrapKeys.Bootstrap)] public Bootstrap root { get; set; }`。

## 3. 链式绑定（默认写法）

框架的 `Binding` / `InjectionBinding` / `CommandBinding` / `Pool` / `SemiBinding` 的修饰方法一律 `return this`，**优先写完整 fluent 链**，钩子用表达式体：

```csharp
protected override void SetStartupCommand() => SetStartupCommand<StartupCommand>(); // 私有嵌套类

protected override void BindCommands() =>
    commandBinder.Bind<ScoreSignal>().To<AddScoreCommand>();

protected override void BindSignals() =>
    injectionBinder.Bind<ScoreChangedSignal>().ToSingleton();

protected override void BindValues() =>
    injectionBinder.Bind<IScoreService>().To<ScoreService>().ToSingleton();
```

### 注入容器

| 链 | 语义 |
| --- | --- |
| `injectionBinder.Bind<I>().To<V>()` | 每次新实例 |
| `injectionBinder.Bind<I>().To<V>().ToSingleton()` | 单例 |
| `injectionBinder.Bind<I>().ToValue(instance)` | 固定值 |
| `injectionBinder.Bind<I>().To<V>().ToSingleton().ToName(Key.X)` | 命名绑定（名字是枚举原值） |
| `injectionBinder.Bind<I>().To<V>().ToSingleton().CrossContext()` | 搬到共享根 binder |
| `injectionBinder.Bind<I>().To<V>().ToSingleton().SupplyTo<T>()` | 声明供给关系 |

`IInjectionBinding` 另有 `SetValue(object)`、`ToInject(bool)`、`Unsupply<T>()`、`isCrossContext`、`type`；读取口 `GetInstance<T>()` / `GetInstance<T>(name)` / `GetBinding<T>()` / `GetSupplier(...)`。

### 命令

```csharp
commandBinder.Bind<SomeSignal>().To<ACommand>().Once();
commandBinder.Bind<SomeSignal>().To<ACommand>().To<BCommand>().InSequence();
commandBinder.Bind<SomeSignal, SomeCommand>();          // 成对快捷入口
commandBinder.Bind<ScoreSignal>().ToHandler<IScoreService, int>((svc, n) => svc.Add(n));
```

`ICommandBinding`：`Once()` / `InParallel()` / `InSequence()` / `To<T>()` / `ToName(...)` / `ToHandler<TService[, T1..T4]>(...)` 均返回 `ICommandBinding`。`ToHandler` 是短路通路，与 `To<C>()` 互斥；服务经完整注入链解析，不创建 Command。

**绑定的 key 只能是 Signal 类型**：派发由 `CommandBinder.ResolveBinding` 挂 `signal.AddListener(ReactTo)` 驱动，字符串/枚举 key 没有触发路径。

### UI 与信号链

```csharp
Signal<ScoreSignal>().Dispatch(10);                       // SimplifyUI.Signal<T>() 返回 T
Show<ScorePopup>().SetTitle("结算").SetData(data);         // Show<T>() 返回 T；SetXxx() 返回 this
uiSettings.InitRoots(screen, overlay, popup);             // 三参，返回 UISettings 可链
```

`UISettings` 没有 `ReleaseView<T>()`；关闭界面用 `SimplifyUI.Hide()`，回收由 `onRecycle` / `disposeOnHide` 决定销毁还是回缓存。

## 4. Signal

`Signal` / `Signal<T>` / `Signal<T,U>` / `Signal<T,U,V>` / `Signal<T,U,V,W>` —— 最多 4 个载荷（`Signals/Signal.cs`）。

- `Dispatch(...)`、`AddListener(cb)` → `SignalSubscription`、`AddOnce(cb)` → 句柄、`RemoveListener(cb)`、`RemoveAllListeners()`。
- `SignalSubscription.Dispose()` 退订，幂等；可用 `using` 限定作用域。
- `SignalSubscriptionGroup`（`Signals/SignalSubscriptionGroup.cs`）：`View` / `Command` 各持一个 `protected subscriptions`。
  - `subscriptions.Listen(signal, cb)` —— 扩展方法，覆盖无参到四参全部 Signal 形态。
  - `subscriptions.Track(signal.AddOnce(cb))` —— AddOnce 用 Track；另有 `Untrack(handle)` / `Dispose()`。

## 5. Command

`Command : IPoolable`（`Commands/Command.cs`）：

- 框架已注入：`ICommandBinder commandBinder`、`IInjectionBinder injectionBinder`。
- `public abstract void Execute()`；`Retain()` / `Release()` / `Fail()` / `Cancel()`。
- `public virtual void Restore()` —— 回收前退订订阅组 + `Uninject`；覆写必须 `base.Restore()`。
- `protected SignalSubscriptionGroup subscriptions`；`Execute()` 返回即回池（所有命令统一池化）。

## 6. View（默认承接全部逻辑）

`View : MonoBehaviour`（`Mediations/View.cs`）：

- 取信号单例：`injectionBinder.GetInstance<T>()`（或 `[Inject]` 属性注入）；`View` 不再提供 `GetSignal<T>()`，UI 层用 `SimplifyUI.Signal<T>()`。
- `[Inject] public IInjectionBinder injectionBinder`、`public Bootstrap owningBootstrap`、`requiresBootstrap`（默认 true）、`autoRegisterWithBootstrap`（默认 true）。
- `protected SignalSubscriptionGroup subscriptions`。
- **不解析声明式特性**：`View` 没有 `InitAttributes()` / `EnsureAttributesInitialized()`，`[Child]` / `[BindEvent]` / `[BindMethod]` / `[MainThread]` 在此类上不会生效；解析能力只在 UI 程序集的 `SimplifyUI`（`protected virtual void InitAttributes()`）与 `SimplifyIoC.Utils` 扩展方法里。
- 生命周期 `Awake` / `Start` / `OnEnable` / `OnDisable` / `OnDestroy` 覆写时先 `base`。

**无 Mediator 也照常注入**：`MediationBinder.Trigger(Awake)` 在 `GetBinding(viewType)` 为空时仍调用 `InjectViewAndChildren(view)`（`MediationBinder.cs`），后者执行 `injectionBinder.injector.Inject(view, false)` 与 `HandleDelegates(view, view.GetType(), true)`。所以 `[Inject]` / `[PostConstruct]` / `[Construct]` / `[ListensTo]` 在纯 View 下可用；**声明式特性不在此列**，普通 View 需显式调用 `this.AddAttributeParser(...).ParseAttributes()`（见第 9 节），或改继承 `SimplifyUI`。

## 7. SimplifyUI（UI 基类）

`public abstract class SimplifyUI : View`（`UI/SimplifyIoC/UI/SimplifyUI.cs`）。

| 成员 | 签名 | 说明 |
| --- | --- | --- |
| `type` | `public virtual UIType type { get; } = UIType.Screen` | `Screen` / `Overlay` / `Popup` / `Toast` |
| `disposeOnHide` | `public virtual bool disposeOnHide => true` | false 时隐藏后回 UI 缓存复用 |
| `canHideByMask` | `public virtual bool canHideByMask => type == UIType.Popup` | 遮罩点击是否可关 |
| `onRecycle` | `public UnityEvent<SimplifyUI> onRecycle { get; }` | `Hide(true)` 时广播 |
| `Show()` | `public void Show()` | 激活 GameObject；已初始化的 Popup 会再调 `OnShow()` |
| `Hide(bool withEvent = true)` | `public virtual void Hide(bool withEvent = true)` | 关界面；`false` 不广播回收事件 |
| `Init()` | `protected virtual void Init()` | `Start` 内调用一次，放一次性初始化 |
| `InitAttributes()` | `protected virtual void` | 首次 `Start` 幂等触发的完整四 parser 解析链，业务可覆写扩展；`OnDestroy` 调 `UnbindMethods()` 释放 `[BindMethod]` 静态表条目 |
| `OnShow()` | `protected virtual void OnShow()` | `Start` / `Show()` / 已初始化时的 `OnEnable` 调用，放每次展示的刷新 |
| `Start()` / `OnEnable()` | `protected override` | `Start` = `base.Start()` → `EnsureAttributesInitialized()`（幂等解析一次声明式特性）→ `Init()` → `_initialized = true` → `OnShow()`；覆写要调 `base` |
| `uiSettings` | `protected UISettings uiSettings` | 懒加载 `injectionBinder.GetInstance<UISettings>()` |
| `canvasScale` | `protected float canvasScale` | `uiSettings.canvasScale` |
| `Show<T>()` | `protected T Show<T>() where T : SimplifyUI` | `uiSettings.CreateView<T>()`，**返回 T 供链式配置** |
| `Interactable(bool)` | `protected void Interactable(bool value)` | `uiSettings.Interactable(name, value)`，控制射线遮罩 |
| `Signal<T>()` | `protected T Signal<T>() where T : BaseSignal` | `injectionBinder.GetInstance<T>()`，**返回 T 供链式 Dispatch** |
| `BGM` / `Sound` / `Toast` | `protected virtual void ...(string)` | 占位空实现，业务自行覆写 |

`Signal<T>()` 是 UI 层唯一的取信号简写（`View.GetSignal<T>()` 已移除）；普通 View 请写 `injectionBinder.GetInstance<T>()`。注意它是方法，`SimplifyIoC.Signals.Signal<T>` 是类型：`Signal<X>()` 带括号即调用方法；不要在 UI 类里声明 `Signal<T>` 类型成员。

## 8. UISettings / UIType / 预置体

`UISettings : ScriptableObject`（`UI/SimplifyIoC/Settings/UISettings.cs`），`[CreateAssetMenu(menuName = "Aixuma/UISettings")]`。`IPrefabCreator` 已拆到同目录独立文件 `IPrefabCreator.cs`（命名空间同为 `SimplifyIoC.Settings`），`UIType` 枚举仍留在 `UISettings.cs`。

- `public CustomPrefabCreator[] prefabCreators` —— UI 预置体来源。
- `UISettings InitRoots(Transform screen, Transform overlay, Transform popup)` —— **三参**（没有 `mask` 参数，遮罩取 `popup.GetChild(0)`），返回 `this` 供链式调用；建立各 `UIType` 根节点、遮罩按钮、安全区与 `canvasScale`。**框架内没有调用点，业务侧必须在启动路径上调用一次**，否则 `Show<T>()` 会因 `_viewRoot` 为空而失败。
- `T CreateView<T>(Dictionary<UIType, Transform> roots = null) where T : SimplifyUI`；`SimplifyUI CreateView(string name, ...)`。
- **没有 `ReleaseView<T>()`**：关界面用 `SimplifyUI.Hide()`；`Hide(true)` 广播 `onRecycle`，由 `disposeOnHide` 决定销毁还是回 `_viewCache`。`GetView<T>()` 只查询当前已显示的视图。
- `void Clear()`、`void Interactable(string, bool)`、`Vector2 ScreenToAnchored` / `WorldToAnchored` / `WorldToScreen` / `Vector3 ScreenToWorld`。
- 只读属性：`safeAreaTop` / `safeAreaBottom` / `canvasHeight` / `canvasScale` / `canvasRectTrans` / `camera`。

```csharp
public enum UIType { Screen, Overlay, Popup, Toast }
```

`IToast`（`UI/SimplifyIoC/UI/IToast.cs`）：`IToast SetMessage(string message, float duration = 2)`、`IToast MoveUp()`，均返回 `IToast` 供链式调用。`UIType.Toast` 的视图**必须实现 `IToast`**，否则 `UISettings.ShowToast` 直接返回、界面不会显示。

`CustomPrefabCreator : ScriptableObject`：`bool GetUIPrefab(string, out SimplifyUI)` / `bool GetUIPrefab(Type, out SimplifyUI)` / `bool GetBootstrapPrefab(string, out Bootstrap)`。`DefaultPrefabCreator` 用 `[SerializeField] List<SimplifyUI> _views` 建名字与类型索引——**每个 UI 预置体都要登记，否则 `Show<T>()` 返回 null**。

## 9. 声明式特性

**只有 `SimplifyUI` 自动解析**：它在首次 `Start`（`base.Start()` 完成注入之后）幂等跑一次四个 parser 链，并在 `OnDestroy` 释放 `[BindMethod]` 静态表条目；`InitAttributes()` 是 `protected virtual`，可覆写扩展。

普通 `View` / `Mediator` 上的这四个特性**不会自动生效**（核心程序集已不含 `SimplifyIoC.Utils`）。要用需在自己的 `Start` 里显式解析，或改继承 `SimplifyUI`：

```csharp
protected override void Start()
{
    base.Start();
    var bind = this.GetBindMethodParser();
    var reflected = this.AddAttributeParser(this.GetEventMethodParser())
        .AddAttributeParser(this.GetChildParser())
        .AddAttributeParser(this.GetMainThreadParser());
    if (bind != null) reflected = reflected.AddAttributeParser(bind);
    reflected.ParseAttributes();
}
```

> 特性类型与解析器都在 `Aixuma.SimplifyIoC.UI` 程序集，业务 asmdef 需引用它；`UnityEvent.AddListener` 不去重，务必只解析一次（不要既覆写 `InitAttributes()` 又手写一遍）。

| 特性 | 命名空间 | 目标 | 作用 / 关键约束 |
| --- | --- | --- | --- |
| `[Inject]` / `[Inject(name)]` | 全局 | **仅 Property** | 容器注入；带名字取命名绑定 |
| `[Name(name)]` | 全局 | Parameter | 构造参数命名注入 |
| `[Construct]` | 全局 | Constructor | 标记首选构造函数 |
| `[PostConstruct(priority)]` | 全局 | Method | 注入后无参回调；priority 控制顺序 |
| `[ListensTo(typeof(S))]` | 全局 | Method | 信号→方法；签名必须与 Signal 完全匹配；基类 private 方法扫不到 |
| `[Child]` / `[Child("path")]` / `[Child("path", true)]` | `SimplifyIoC.Utils` | Field | 按名字或路径收集子物体组件，支持数组与 `List<>` |
| `[BindEvent("onClick", nameof(btn))]` | `SimplifyIoC.Utils` | **Method** | UnityEvent → 方法；第二参起是事件宿主成员名，可多个，支持数组与 `List<>` |
| `[BindMethod("n1", "n2")]` / `[BindMethod(order = -1)]` | `SimplifyIoC.Utils` | Method | 注册到名字，之后 `InvokeBind("名", 参数)` |
| `[MainThread(times, interval)]` | `SimplifyIoC.Utils` | Method | 让无参方法在主线程 Update 中按次数/间隔重复执行（`times` 默认 -1） |
