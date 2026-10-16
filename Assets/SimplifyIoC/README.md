# SimplifyIoC

[StrangeIoC](https://github.com/strangeioc/strangeioc) 的精简版：面向 Unity 的轻量 IoC / MVCS 框架，提供绑定容器、命令、类型安全信号，以及由框架自动解析的声明式绑定。

- **环境要求**：Unity 2022.3+（C# 9 / .NET Standard 2.1）
- **示例工程**：[Assets/Examples](https://github.com/JiphuTzu/SimplifyIoC/tree/main/Assets/Examples)
- **完整变更**：[CHANGELOG.md](CHANGELOG.md)

## 安装

在 Unity 的 Package Manager 中，左上角 `+` → *Add package from git URL*，输入：

```
https://github.com/JiphuTzu/SimplifyIoC.git#upm
```

## 快速上手

### 1. 启动入口：`SimplifyBootstrap<T>`

2.0.0 起框架里只有 `Bootstrap`——它自己就是 `MonoBehaviour`，既是**层级锚点**（挂在显示层级顶部的那块组件），也是**绑定容器**（1.3.2 及以前，这两个角色分属 `Context` 与 `Bootstrap` 两个类）。
派生 `SimplifyBootstrap<TStartupCommand>`、实现四个 `Bind*` 分组，挂到场景 GameObject 上即可：`Awake` 建容器并确定归属，`Start` 派发 `StartupSignal` 触发启动命令。

```csharp
public class GameBootstrap : SimplifyBootstrap<GameStartupCommand>
{
    protected override void BindCommands() => BindCommand<CreateObjectSignal, CreateObjectCommand>();
    protected override void BindViews()    => BindView<LifeTimeView, LifeTimeMediator>();
    protected override void BindSignals()  => BindSignal<RecordChangedSignal>(true);
    protected override void BindValues()   => BindValue<IClock, SystemClock>(true);
}
```

把 `GameBootstrap` 挂到场景中层级最顶部的 GameObject 上，View 放在它的子树里。

需要控制启动时机时，覆写 `Start`——归属不必在那里表达，`Init()` 会自动解析父：

```csharp
public class LevelBootstrap : SimplifyBootstrap<LevelStartupCommand>
{
    // 想在自己挑的时机启动就覆写它；base.Start() 即 Launch()
    protected override void Start()
    {
        // ...先做自己的事
        base.Start();
    }
}
```

- `Init()` 幂等、**无父参数**，且可见性是 **`internal`**：`Awake` 自动调用，正常使用不需要（也调不到）它——运行时 `AddComponent<XxxBootstrap>()` 照常由 Unity 派发 `Awake`；只有框架自身与包内测试程序集经 `InternalsVisibleTo` 可见。归属自动解析——沿 Transform 向上找最近的已就绪 `Bootstrap`，找不到才落到框架级锚点 `Bootstrap.root`；要指定「锚点之外的某个父」用 `AddBootstrap`。
- `Dispose()` 之后调用 `Init()` 会抛 `ObjectDisposedException`——幂等不等于可复活。
- `Launch()` 永远显式：`SimplifyBootstrap<T>.Start` 自动补调；直接派生 `Bootstrap`（不经 `SimplifyBootstrap<T>`）的类需自行在 `Start` 里调用。
- Unity 在 `Awake` 之前完成反序列化，所以 `BindValues` 里读序列化字段是安全的。

### 2. 信号与命令

信号是类型安全的事件。把信号绑到命令之后，`Dispatch` 会构造命令，并把信号载荷按类型注入进去。

```csharp
public class CreateObjectSignal : Signal<Vector3> { }

public class CreateObjectCommand : Command
{
    [Inject] public Vector3 pos { get; set; }                          // 信号载荷
    [Inject(BootstrapKeys.Bootstrap)] public Bootstrap root { get; set; }

    public override void Execute() { /* ... */ }
}

// 在 View / Mediator 内派发（5.6 起提供 Get<T>() 取信号单例）
Get<CreateObjectSignal>().Dispatch(new Vector3(1, 0, 0));
```

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
它同时承担 5.6 之前 `firstBootstrap` 的两条兜底，场景侧只依赖框架类型即可取得父 Bootstrap，跨域绑定随之可用：

```csharp
//App 层：常驻（DontDestroyOnLoad）——Awake 默认就是建容器 + 定归属，这里只补自己的事
public class AppBootstrap : SimplifyBootstrap<AppStartupCommand>
{
    protected override void Awake()
    {
        base.Awake();                 // 建容器 + 定归属 + 映射；锚点自动登记，无需额外声明
        DontDestroyOnLoad(gameObject);
    }
}

//场景层：不认识 AppBootstrap，也不需要认识——连 Awake 都不必覆写
public class LevelBootstrap : SimplifyBootstrap<LevelStartupCommand> { }
```

- 锚点**自动**产生：第一个构造的链根持有，后来的 Bootstrap 不抢占；锚点被 `Dispose()`（App 根销毁）后清空，下一个链根接任。
- 两条**兜底**（1.3.2 恢复 5.6 之前 `firstBootstrap` 的行为）：
  - `Init` 时 Transform 层级找不到父 ⇒ 自动挂到锚点下（跨场景并列加载时不必手写任何代码）；
  - View 找不到任何 Bootstrap ⇒ 注册到锚点而不是抛异常（`requiresBootstrap = false` 的 View 不参与）。
  归属始终是 **Transform 层级 > 锚点兜底**——层级上说得清楚时永远优先；要指定「锚点之外的某个父」用 `AddBootstrap`。
- 锚点缺席（单独跑某个场景调试）时 `Bootstrap.root` 为 `null`，该 Bootstrap 即 **自任链根** 并接任锚点，功能照常。
- 跨域绑定请只在锚点根声明一次并标 `.CrossContext()`，场景 Bootstrap 不要重复登记（会在共享根 binder 上同名冲突）。

## 声明式绑定

在 `View` / `Mediator` 的成员上标注即可，框架会在**注入完成后自动解析**，无需手写调用。

| 特性 | 可用位置 | 作用 |
| --- | --- | --- |
| `[Inject]` / `[Inject(name)]` | 属性 | 从容器注入；带名字取命名绑定（如 `[Inject(BootstrapKeys.Bootstrap)]`） |
| `[Child]` | 字段 | 按名字 / 路径收集子物体上的组件，支持 `List<>` |
| `[BindEvent("事件名", "成员名")]` | 字段 / 属性 / 方法 | 把成员上的 `UnityEvent` 绑到方法；事件源支持数组与 `List<>` |
| `[BindMethod("名1", "名2")]` | 方法 | 把方法注册到名字，之后用 `InvokeBind("名", 参数)` 调用；`order` 控制同名方法的顺序 |
| `[PostConstruct(priority)]` | 方法 | 注入完成后立即调用（无参），`priority` 控制多个后置构造的顺序 |
| `[MainThread(times, interval)]` | 方法 | 保证方法在主线程执行，可指定执行次数与间隔 |
| `[ListensTo(typeof(SomeSignal))]` | 方法 | 把 Mediator / View 的方法直接挂到信号上 |
| `[Construct]` / `[Name]` | 构造函数 / 参数 | 指定优先使用的构造函数；对构造参数做命名注入 |

> `[Inject]` 只作用于**属性**（`public X x { get; set; }`），不要写在字段上。

## 支持平台

PC · WebGL · iOS · Android · UWP

## 相关链接

- [CHANGELOG](CHANGELOG.md)
- [示例工程](https://github.com/JiphuTzu/SimplifyIoC/tree/main/Assets/Examples)
- [Apache-2.0 许可证](https://github.com/JiphuTzu/SimplifyIoC/blob/main/LICENSE)
