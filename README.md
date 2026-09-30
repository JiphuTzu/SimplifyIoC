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

### 1. 启动入口：Bootstrap + Context

`SimplifyBootstrap` 负责创建 `Context`，`SimplifyContext<TStartupCommand>` 负责分类绑定；它的 `Start()` 会派发 `StartupSignal`，从而触发启动命令。

```csharp
public class GameBootstrap : SimplifyBootstrap
{
    protected override void Awake() => context = new GameContext(this);

    private class GameContext : SimplifyContext<GameStartupCommand>
    {
        public GameContext(Bootstrap view) : base(view) { }

        protected override void BindCommands() => BindCommand<CreateObjectSignal, CreateObjectCommand>();
        protected override void BindViews()    => BindView<LifeTimeView, LifeTimeMediator>();
        protected override void BindSignals()  => BindSignal<RecordChangedSignal>(true);
        protected override void BindValues()   => BindValue<IClock, SystemClock>(true);
    }
}
```

把 `GameBootstrap` 挂到场景中的 GameObject 上即可。

### 2. 信号与命令

信号是类型安全的事件。把信号绑到命令之后，`Dispatch` 会构造命令，并把信号载荷按类型注入进去。

```csharp
public class CreateObjectSignal : Signal<Vector3> { }

public class CreateObjectCommand : Command
{
    [Inject] public Vector3 pos { get; set; }                          // 信号载荷
    [Inject(ContextKeys.Bootstrap)] public Bootstrap root { get; set; }

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

## 声明式绑定

在 `View` / `Mediator` 的成员上标注即可，框架会在**注入完成后自动解析**，无需手写调用。

| 特性 | 可用位置 | 作用 |
| --- | --- | --- |
| `[Inject]` / `[Inject(name)]` | 属性 | 从容器注入；带名字取命名绑定（如 `[Inject(ContextKeys.Bootstrap)]`） |
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
