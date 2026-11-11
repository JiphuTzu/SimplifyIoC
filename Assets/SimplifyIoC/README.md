# SimplifyIoC

面向 Unity 的分层 MVCS 框架：绑定容器、类型安全信号、命令与 View 调解四层各司其职，由 Bootstrap 层级链统一管理作用域与跨域共享。UI 能力位于可选程序集 `Aixuma.SimplifyIoC.UI`（`UI/`），核心 `Runtime/` 不依赖 uGUI；声明式绑定只在 `SimplifyUI` 上自动解析。

- **环境要求**：Unity 2022.3+（C# 9 / .NET Standard 2.1）
- **示例工程**：[Assets/Examples](https://github.com/JiphuTzu/SimplifyIoC/tree/main/Assets/Examples)
- **完整变更**：[CHANGELOG.md](CHANGELOG.md)

## 安装

在 Unity 的 Package Manager 中，左上角 `+` → *Add package from git URL*，输入：

```
https://github.com/JiphuTzu/SimplifyIoC.git#upm
```

## 程序集结构

| 程序集 | 目录 | 内容 |
| --- | --- | --- |
| `Aixuma.SimplifyIoC` | `Runtime/` | 绑定容器、Signal / Command、View 调解、Bootstrap 层级链 —— 不依赖 uGUI，也不含 `SimplifyIoC.Utils` |
| `Aixuma.SimplifyIoC.UI` | `UI/` | `SimplifyUI`、`UISettings`、`IToast`、两个 `PrefabCreator`、声明式特性与反射工具、`Materials/` 下的遮罩 / 转场 shader —— 单向依赖核心 + uGUI |

UI 是可选层：不需要 uGUI 的工程可以只引用核心程序集。**使用 asmdef 且用到 UI 类型或 `SimplifyIoC.Utils` 的下游工程需要补一条引用**（`Aixuma.SimplifyIoC.UI`，或 `GUID:85be5796041942a8867159794464d6aa`）到业务 asmdef 的 `references`。未使用 asmdef 的工程（Assembly-CSharp）由 `autoReferenced` 自动可见，无需改动。

UI 目录下按两级归类，**命名空间与目录一一对应**，规则和核心一致（`Runtime/SimplifyIoC/<目录>/` → `SimplifyIoC.<目录>`）：

| 目录 | 命名空间 | 类型 |
| --- | --- | --- |
| `UI/SimplifyIoC/UI/` | `SimplifyIoC.UI` | `SimplifyUI`、`IToast` |
| `UI/SimplifyIoC/Settings/` | `SimplifyIoC.Settings` | `UISettings`、`UIType`、`IPrefabCreator`、`CustomPrefabCreator`、`DefaultPrefabCreator` |
| `UI/SimplifyIoC/Utils/` | `SimplifyIoC.Utils` | `[Child]` / `[BindEvent]` / `[BindMethod]` / `[MainThread]`、`ReflectionExtension`、`DebugX` |

`UI/Materials/` 放 `ColorMask` / `Voronoi` 的 shader 与材质，是纯资源、不参与命名空间；连 `.meta` 一起从 `Runtime/Materials/` 迁入，GUID 未变。核心程序集至此不再引用 `com.unity.ugui`（`DebugX` 原先也 `using UnityEngine.UI`）。

> **升级注意（命名空间）**：原本位于 `SimplifyIoC.Mediations` 的 5 个 UI 类型已按目录分层，下游源码要把对应的 `using SimplifyIoC.Mediations;` 换成 `using SimplifyIoC.UI;`（界面基类 / `IToast`）或 `using SimplifyIoC.Settings;`（配置 / 预置体创建器）；只用核心 `View` / `Mediator` 的代码不用动。脚本 GUID 保持不变，Unity 资产与预置体上的序列化引用不会断。

> **声明式解析的归属变了**：核心 `View` / `Mediator` 不再自动解析 `[Child]` / `[BindEvent]` / `[BindMethod]` / `[MainThread]`，只有 `SimplifyUI` 在首次 `Start`（`base.Start()` 完成注入之后）解析一次，并在 `OnDestroy` 释放 `[BindMethod]` 静态表条目。普通 `View` 若仍要用这些特性，需在自己的 `Start` 里显式调用解析链（扩展方法在 UI 程序集）。`[Inject]` / `[PostConstruct]` / `[Construct]` / `[ListensTo]` 仍在核心，行为不变。

## 快速上手

### 1. 启动入口：`SimplifyBootstrap`（非泛型）

2.0.0 起框架里只有 `Bootstrap`——它自己就是 `MonoBehaviour`，既是**层级锚点**（挂在显示层级顶部的那块组件），也是**绑定容器**（1.3.2 及以前，这两个角色分属 `Context` 与 `Bootstrap` 两个类）。
派生非泛型的 `SimplifyBootstrap`、实现 `SetStartupCommand()` 指定启动命令，再按需覆写四个 `Bind*` 分组，挂到场景 GameObject 上即可：`Awake` 建容器并确定归属，`Start` 依次调用 `SetStartupCommand()` 与 `Launch()`，派发 `StartupSignal` 触发启动命令。

```csharp
public class GameBootstrap : SimplifyBootstrap
{
    protected override void SetStartupCommand() => SetStartupCommand<GameStartupCommand>();
    protected override void BindCommands() => BindCommand<CreateObjectSignal, CreateObjectCommand>();
    protected override void BindViews()    => BindView<LifeTimeView, LifeTimeMediator>();
    protected override void BindSignals()  => BindSignal<RecordChangedSignal>(true);
    protected override void BindValues()   => BindValue<IClock, SystemClock>(true);
}
```

`SetStartupCommand()` 是唯一必须实现的抽象方法；四个 `Bind*` 按需覆写，`MapBindings()` 已 sealed，不要覆写。注意 `BindCommand<S, C>(once, crossContext)` 的第一个 bool 是 `once`，而 `BindSignal` / `BindValue` 的第一个 bool 是 `crossContext`。

把 `GameBootstrap` 挂到场景中层级最顶部的 GameObject 上，View 放在它的子树里。

需要控制启动时机时覆写 `Start`（务必调 `base.Start()`）——归属不必在那里表达，`Init()` 会自动解析父：

```csharp
public class LevelBootstrap : SimplifyBootstrap
{
    protected override void SetStartupCommand() => SetStartupCommand<LevelStartupCommand>();

    // 想自己挑启动时机就覆写它；base.Start() = SetStartupCommand() + Launch()
    protected override void Start()
    {
        // ...先做自己的事
        base.Start();
    }
}
```

- `Init()` 幂等、**无父参数**，且可见性是 **`internal`**：`Awake` 自动调用，正常使用不需要（也调不到）它——运行时 `AddComponent<XxxBootstrap>()` 照常由 Unity 派发 `Awake`；仅框架程序集自身可见，下游程序集无法显式驱动（要驱动生命周期请用 PlayMode）。归属自动解析——沿 Transform 向上找最近的已就绪 `Bootstrap`，找不到才落到框架级锚点 `Bootstrap.root`；要指定「锚点之外的某个父」用 `AddBootstrap`。
- `Dispose()` 之后调用 `Init()` 会抛 `ObjectDisposedException`——幂等不等于可复活。
- `Launch()` 永远显式：`SimplifyBootstrap.Start` 自动补调；直接派生 `Bootstrap`（不经 `SimplifyBootstrap`）的类需自行在 `Start` 里调用。
- Unity 在 `Awake` 之前完成反序列化，所以 `BindValues` 里读序列化字段是安全的。

### 2. 信号与命令

信号是类型安全的事件。把信号绑到命令之后，`Dispatch` 会从池中取一个命令实例，并把信号载荷按类型注入进去。

```csharp
public class CreateObjectSignal : Signal<Vector3> { }

public class CreateObjectCommand : Command
{
    [Inject] public Vector3 pos { get; set; }                          // 信号载荷
    [Inject(BootstrapKeys.Bootstrap)] public Bootstrap root { get; set; }

    public override void Execute() { /* ... */ }
}

// 普通 View 内：直接从容器取信号单例（也可用 [Inject] 属性注入）
injectionBinder.GetInstance<CreateObjectSignal>().Dispatch(new Vector3(1, 0, 0));

// SimplifyUI 内：简写 Signal<T>()
Signal<CreateObjectSignal>().Dispatch(new Vector3(1, 0, 0));

// Mediator 内：同名能力叫 Get<T>()
Get<CreateObjectSignal>().Dispatch(new Vector3(1, 0, 0));
```

> `View` 不再提供 `GetSignal<T>()`：UI 层用 `SimplifyUI.Signal<T>()`，普通 View 用 `injectionBinder.GetInstance<T>()` 或 `[Inject]` 属性注入；Mediator 侧仍是 `Get<T>()`。

### 3. View 与 Mediator

`View` 是挂在 GameObject 上的表现层基类；`Mediator<TView>` 是它的逻辑层。视图引用由泛型参数给出，不需要（也不能）再写 `[Inject] TView view`。

```csharp
public class HudView : View
{
    public Button confirm;

    [BindEvent("onClick", nameof(confirm))]
    private void OnConfirm() => Debug.Log("confirm");
}

public class HudMediator : Mediator<HudView>
{
    [Inject] public RecordChangedSignal changed { get; set; }

    public override void OnRegister()
    {
        base.OnRegister();
        subscriptions.Listen(changed, OnChanged);   // 随 Mediator 销毁自动退订
    }

    private void OnChanged(bool value) { /* ... */ }
}
```

### 4. 跨场景 / 并列 Bootstrap：`Bootstrap.root`

多个场景各自带一个 Bootstrap 时，它们在 Transform 上往往并列，且分属不同程序集（热更新分层：场景/玩法层不应编译期依赖 App 层）。
**第一个构造成功的链根 Bootstrap 自动成为框架级锚点**（`Bootstrap.root`，先到先得、不抢占），
它同时是下面两条兜底的落点（1.3.2 恢复的 `Context.firstContext` 语义），场景侧只依赖框架类型即可取得父 Bootstrap，跨域绑定随之可用：

```csharp
//App 层：常驻（DontDestroyOnLoad）——Awake 默认就是建容器 + 定归属，这里只补自己的事
public class AppBootstrap : SimplifyBootstrap
{
    protected override void SetStartupCommand() => SetStartupCommand<AppStartupCommand>();

    protected override void Awake()
    {
        base.Awake();                 // 建容器 + 定归属 + 映射；锚点自动登记，无需额外声明
        DontDestroyOnLoad(gameObject);
    }
}

//场景层：不认识 AppBootstrap，也不需要认识——连 Awake 都不必覆写
public class LevelBootstrap : SimplifyBootstrap
{
    protected override void SetStartupCommand() => SetStartupCommand<LevelStartupCommand>();
}
```

- 锚点**自动**产生：第一个构造的链根持有，后来的 Bootstrap 不抢占；锚点被 `Dispose()`（App 根销毁）后清空，下一个链根接任。
- 两条**兜底**（1.3.2 恢复的 `Context.firstContext` 兜底语义）：
  - `Init` 时 Transform 层级找不到父 ⇒ 自动挂到锚点下（跨场景并列加载时不必手写任何代码）；
  - View 找不到任何 Bootstrap ⇒ 注册到锚点而不是抛异常（`requiresBootstrap = false` 的 View 不参与）。
  归属始终是 **Transform 层级 > 锚点兜底**——层级上说得清楚时永远优先；要指定「锚点之外的某个父」用 `AddBootstrap`。
- 锚点缺席（单独跑某个场景调试）时 `Bootstrap.root` 为 `null`，该 Bootstrap 即 **自任链根** 并接任锚点，功能照常。
- 跨域绑定请只在锚点根声明一次并标 `.CrossContext()`，场景 Bootstrap 不要重复登记（会在共享根 binder 上同名冲突）。

## 声明式绑定

`[Inject]` / `[PostConstruct]` / `[Construct]` / `[Name]` / `[ListensTo]` 由核心程序集在注入与 View 注册时处理，行为不变。

`[Child]` / `[BindEvent]` / `[BindMethod]` / `[MainThread]` 这一组的类型与解析器已随 `SimplifyIoC.Utils` 迁入 UI 程序集，**只有 `SimplifyUI` 会自动解析**（首次 `Start`、注入完成之后）。普通 `View` / `Mediator` 上的这四个特性不再自动生效，需要显式调用解析链，或改为继承 `SimplifyUI`。

| 特性 | 可用位置 | 作用 |
| --- | --- | --- |
| `[Inject]` / `[Inject(name)]` | 属性 | 从容器注入；带名字取命名绑定（如 `[Inject(BootstrapKeys.Bootstrap)]`） |
| `[Child]` | 字段 | 按名字 / 路径收集子物体上的组件，支持数组与 `List<>` |
| `[BindEvent("onClick", nameof(confirm))]` | **仅方法** | 把事件宿主上的 `UnityEvent` 绑到该方法；第二参起是事件宿主的成员名（可为字段 / 属性，支持数组与 `List<>`） |
| `[BindMethod("名1", "名2")]` | 方法 | 把方法注册到名字，之后用 `InvokeBind("名", 参数)` 调用；`order` 控制同名方法的顺序 |
| `[PostConstruct(priority)]` | 方法 | 注入完成后立即调用（无参），`priority` 控制多个后置构造的顺序 |
| `[MainThread(times, interval)]` | 方法 | 让无参方法在主线程 Update 中按次数 / 间隔重复执行（`times` 默认 -1 不限次） |
| `[ListensTo(typeof(SomeSignal))]` | 方法 | 把 Mediator / View 的方法直接挂到信号上；方法签名须与信号载荷一致，基类 private 方法扫不到 |
| `[Construct]` / `[Name]` | 构造函数 / 参数 | 指定优先使用的构造函数；对构造参数做命名注入 |

> `[Inject]` 只作用于**属性**（`public X x { get; set; }`），不要写在字段上。
> `[Child]` / `[BindEvent]` / `[BindMethod]` / `[MainThread]` 在 `SimplifyIoC.Utils` 命名空间、**UI 程序集**里；其余在全局命名空间、核心程序集。

## 支持平台

PC · WebGL · iOS · Android · UWP

## WorkBuddy Skill（可选，不自动生效）

包内 `Skills~/simplifyioc-quick-coding/` 附带一份配套 Skill（目录名以 `~` 结尾是 UPM 惯例，同 `Samples~`：Unity 资产管线忽略它、不生成 `.meta`，但随包正常分发）：把一句功能需求翻译成符合 SimplifyIoC 分层的业务代码（`Bootstrap` / `Signal` / `Command` / `SimplifyUI` / `View`），内含现行 API 速查、5 个 C# 模板与静态自检脚本。

**它只是随包分发的文件**，不会被 WorkBuddy 自动加载，也不参与 Unity 编译（模板是 `.tpl`，目录内没有 `.cs`）。装不装完全由使用者决定：把 `Skills~/simplifyioc-quick-coding/` 整个目录复制到 `~/.workbuddy/skills/`（用户级）或你工程的 `.workbuddy/skills/`（项目级），刷新 WorkBuddy 后即可使用；卸载时删掉该目录即可。命令与说明见该目录内的 `README.md`。

由于本包通过 `git subtree split --prefix=Assets/SimplifyIoC --branch upm` 发布，这份 Skill 会随 UPM 包一起到达通过 git URL 安装的使用者。

## 相关链接

- [CHANGELOG](CHANGELOG.md)
- [示例工程](https://github.com/JiphuTzu/SimplifyIoC/tree/main/Assets/Examples)
- [Apache-2.0 许可证](https://github.com/JiphuTzu/SimplifyIoC/blob/main/LICENSE)
