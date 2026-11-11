# simplifyioc-quick-coding 使用说明

- **类型**：随 UPM 包 `com.aixuma.simplifyioc` 分发的**可选** WorkBuddy Skill（不是 Unity 包的一部分，不自动生效）
- **适配框架**：SimplifyIoC **2.1.0**（UPM 包 `com.aixuma.simplifyioc`）
- **文档日期**：2026-11-11
- **依据**：`Assets/SimplifyIoC/Runtime/` 与 `Assets/SimplifyIoC/UI/` 源码、包内 `README.md`、`CHANGELOG.md` 的 `2.1.0` 段（文档与源码已对齐）

---

## 零、安装（可选，由你决定）

本目录只是**随包携带的副本**：WorkBuddy 不会自动加载它，Unity 也不会编译它（模板是 `.tpl`，没有 `.cs`）。不安装完全不影响框架使用。

想用时，把整个 `simplifyioc-quick-coding/` 目录复制到用户级或项目级 skills 目录（路径与生效范围见「三、安装」）。从 clone 下来的主仓库或 Package Manager 缓存安装：

```bash
# 主仓库（用户级）
cp -R Assets/SimplifyIoC/Skills~/simplifyioc-quick-coding ~/.workbuddy/skills/

# 通过 git URL 装包时，源目录在 PackageCache 下，例如：
# cp -R Library/PackageCache/com.aixuma.simplifyioc@<hash>/Skills~/simplifyioc-quick-coding ~/.workbuddy/skills/
```

复制后刷新 / 重启 WorkBuddy，技能列表里出现 `simplifyioc-quick-coding` 即安装成功；不想用了直接删掉该目录即可卸载。**已安装的副本不会随 UPM 包自动更新**，升级后需重新复制一次。

---

## 一、它能做什么

把一句功能需求翻译成符合 SimplifyIoC 分层的业务 C# 代码，产出 `Bootstrap` / `Signal` / `Command` / `SimplifyUI` / `View` 及绑定声明。内置：

- 经源码核对的**现行 API 速查**（`references/api.md`）
- **现行硬约束与反模式**清单（`references/pitfalls.md`）
- **5 个 C# 骨架模板**（`assets/templates/`；启动命令作为 Bootstrap 的私有嵌套类，不单独建模板）
- **静态自检脚本**（`scripts/lint_mvcs.py`）

边界：**不修改框架源码**，不替代 Unity 编译，不跑自动化测试。生成后由人在 Unity 编辑器里挂载、连线并验证。

## 二、三条默认约定（优先级高于框架通用示例）

1. **View 承接全部逻辑，不使用 Mediator。** 不生成 `Mediator`、不写 `BindView<V,M>()`、不覆写 `BindViews()`。
   依据：`MediationBinder.Trigger(Awake)` 在没有 Mediator 绑定时仍会执行 `InjectViewAndChildren`（`MediationBinder.cs`），完成容器注入与 `[ListensTo]` 挂载，所以 `[Inject]` / `[PostConstruct]` / `[ListensTo]` 在纯 View 下照常生效。**声明式特性不在此列**：`[Child]` / `[BindEvent]` / `[BindMethod]` / `[MainThread]` 只有 `SimplifyUI` 自动解析，普通 View 要显式解析或改用 `[SerializeField]`。
2. **UI 界面一律以 `SimplifyUI` 为基类**（`SimplifyIoC.UI`）；只有非 UI 的场景表现层才直接继承 `View`（`SimplifyIoC.Mediations`）。
3. **优先链式编程**：绑定写完整 fluent 链，钩子与简单成员用表达式体，UI 配置器 `SetXxx()` 返回 `this`，UI 内派发写 `Signal<T>().Dispatch(...)`。

## 三、安装

把整个 `simplifyioc-quick-coding/` 目录复制到下列任一位置（**目录名不要改**，`SKILL.md` 的 `name` 需与目录名一致）：

| 目标 | 路径 | 生效范围 |
| --- | --- | --- |
| 用户级（推荐） | `~/.workbuddy/skills/`（Windows：`%USERPROFILE%\.workbuddy\skills\`） | 本机所有项目 |
| 项目级 | `<项目根>/.workbuddy/skills/` | 仅该工程；不少仓库的 `.gitignore` 会忽略 `.workbuddy/`，团队共享需自行调整 |

复制后重启会话或刷新技能列表，出现 `simplifyioc-quick-coding` 即安装成功。本目录随 UPM 包 `com.aixuma.simplifyioc` 分发时，源路径见随包副本 README 的「零、安装」。

### 升级 / 卸载

- 升级：同名目录整体覆盖。改过 `assets/templates/` 或 `scripts/` 的先备份；**已安装的副本不会随框架包自动更新**，升级后要重新复制一次。
- 卸载：删除该目录即可，不写入注册表、不改动其他文件。

## 四、怎么触发

会话里直接说需求即可，按描述自动命中，例如：

- 「用 SimplifyIoC 写个计分功能」
- 「加一个 `OrderSubmittedSignal` 和对应命令」
- 「做一个结算弹窗 UI」
- 「把这个服务改成跨场景共享」
- 「按框架搭一个场景启动入口」

技能表未刷新时可指名调用：`simplifyioc-quick-coding`。

一次说清这几项，产出会更准：**功能名、信号载荷类型、是不是 UI、要不要跨场景共享、代码落到哪个目录**。未指定目录时，Skill 会先询问或按项目惯例放到业务目录（如 `Assets/Examples/Scripts/Aixuma/`）。

## 五、它会生成什么

| 需求 | 产出 | 绑定位置 |
| --- | --- | --- |
| 应用 / 场景启动入口 | `XxxBootstrap`（内含私有嵌套类 `StartupCommand`） | `SetStartupCommand()` |
| 一个交互或业务事件 | `XxxSignal` + `XxxCommand` | `BindCommands()` → `commandBinder.Bind<S>().To<C>()` |
| 纯服务 / 数据单例 | 接口 + 实现 | `BindValues()` → `injectionBinder.Bind<I>().To<V>().ToSingleton()` |
| UI 界面（面板 / 弹窗 / Toast） | `XxxUI : SimplifyUI` | 不绑定视图，只绑它用到的信号与服务 |
| 非 UI 场景表现层 | `XxxView : View` | 同上 |
| UI 基础设施 | `UISettings` 资产 + 一次 `InitRoots(screen, overlay, popup)`（三参） | `BindValues()` → `injectionBinder.Bind<UISettings>().ToValue(asset)` |
| 跨场景共享实例 | 无需新增类型 | **链根**一处 `.ToSingleton().CrossContext()` |
| 事件只需调用一个服务方法 | 无需 Command | `commandBinder.Bind<S>().ToHandler<TService>(...)`（与 `To<C>()` 互斥） |

工作流：**读参考 → 需求映射分层 → 套模板落地 → 按链式规范写 → 跑自检 → 给交付说明**。数据走注入绑定，事件走 Signal → Command，表现层逻辑全部留在 View / SimplifyUI。

## 六、模板与占位符

| 模板 | 产物 | 用到的占位符 |
| --- | --- | --- |
| `Bootstrap.cs.tpl` | `XxxBootstrap`（含私有嵌套 `StartupCommand`） | `FEATURE`, `ROOT_NS` |
| `Signal.cs.tpl` | `XxxSignal` | `FEATURE`, `ROOT_NS`, `PAYLOAD_TYPE` |
| `Command.cs.tpl` | `XxxCommand` | `FEATURE`, `ROOT_NS`, `PAYLOAD_TYPE`, `PAYLOAD_NAME` |
| `SimplifyUI.cs.tpl` | `XxxUI` | `FEATURE`, `ROOT_NS`, `PAYLOAD_TYPE` |
| `View.cs.tpl` | `XxxView` | `FEATURE`, `ROOT_NS`, `PAYLOAD_TYPE` |

| 占位符 | 含义 | 示例 |
| --- | --- | --- |
| `{{FEATURE}}` | 帕斯卡命名功能名 | `Score` → `ScoreSignal` / `ScoreCommand` / `ScoreUI` |
| `{{ROOT_NS}}` | 业务根命名空间，默认 `Aixuma` | `Aixuma.Signals` / `Aixuma.Commands` |
| `{{PAYLOAD_TYPE}}` | Signal 载荷的 C# 类型 | `int`、`ScoreData`（同类型多值用值对象打包） |
| `{{PAYLOAD_NAME}}` | Command 注入属性的名字 | `amount`、`score` |

Signal 载荷最多 4 个，且同类型参数不能重复；超过或重复时用值对象（VO）作为单个载荷。

## 七、静态自检 `lint_mvcs.py`

只做文本层检查，**不替代 Unity 编译**。需要 Python 3。

```bash
python scripts/lint_mvcs.py <新生成文件所在目录>
python scripts/lint_mvcs.py Assets/Examples/Scripts/Aixuma/ScoreUI.cs
```

- 输出 `[ERROR] / [WARN] 编号 文件:行号: 说明`，末尾给汇总。
- **ERROR 必须为 0** 才能交付（脚本此时退出码 0；有 ERROR 则退出码 1，便于接 CI）。
- WARN 逐条人工确认，例如工程里既有的 Mediator 旧代码会触发 W201，属预期提示。

| 编号 | 级别 | 检查内容 |
| --- | --- | --- |
| E002 | ERROR | AOT 禁用 `Expression.Compile` / `Reflection.Emit` |
| E003 | ERROR | `[Inject]` 只能标属性 `{ get; set; }`，不能标字段 |
| E004 | ERROR | 派生 `SimplifyBootstrap` 必须声明 `SetStartupCommand()` |
| E101 / E102 / E103 | ERROR | `\r\r\n` / CRLF / 孤立 CR —— 本仓库要求纯 LF |
| W201 | WARN | 出现 `Mediator` / `BindView<` / `.Mediators` 命名空间 |
| W202 | WARN | UI 类名却直接继承 `View`（应以 `SimplifyUI` 为基类） |
| W203 | WARN | `BindCommand` 的 key 不是 Signal 类型（没有触发路径） |
| W204 | WARN | 绑定拆成中间变量分步赋值（链式优先） |
| W205 | WARN | 用了已移除的 `GetSignal<T>()`（UI 用 `Signal<T>()`，普通 View 用 `injectionBinder.GetInstance<T>()`） |
| W206 | WARN | 启动命令单独建了 `XxxStartupCommand.cs`（应为 Bootstrap 内私有嵌套类） |
| W207 | WARN | 普通 `View` / `Mediator` 上用了声明式特性却未显式解析（会静默失效） |
| W301 | WARN | 有 `Retain()` 却无 `Release()` |
| W302 | WARN | 覆写 `Restore()` 未调 `base.Restore()` |
| W303 | WARN | 裸 `AddListener` 且未走订阅组 / 未退订 |
| W304 | WARN | 覆写 `Start()` 未调 `base.Start()` |

本 Skill **不做旧名 / 废弃 API 禁用检查**：文档已与 2.1.0 源码对齐，只记录现行 API；写错旧写法由 Unity 编译器报错把关。

## 八、生成后必做的手工事项

1. 把 `XxxBootstrap` 挂到显示层级顶部的 GameObject；框架由 `Awake` 驱动 `Init()`，归属沿 Transform 自动解析。
2. 用 `SimplifyUI` 时：绑定 `UISettings` 资产，并在启动路径上**调用一次** `InitRoots(screen, overlay, popup)`（三参，返回 `UISettings`；框架内无调用点）。
3. 每个 UI 预置体登记到 `CustomPrefabCreator`（`DefaultPrefabCreator` 的 `_views` 列表），否则 `Show<T>()` 返回 null；`UIType.Toast` 的视图还要实现 `IToast`。
4. 只用于 UI 通知、没有绑定命令的信号，在 `BindSignals()` 里显式注册成单例。
5. `.meta` 交给 Unity 自动生成，不要手写 GUID；改名 / 移动资产后校验 `*.unity` / `*.prefab` 的 `m_Script` GUID 链路。
6. 文本文件保持**纯 LF** 行尾。
7. 在 Unity 编辑器里手动验证：本项目不跑自动化测试，Skill 不会声称「已编译通过」。

## 九、常见问题

**Q：为什么没有生成 Mediator？**
A：这是本 Skill 的默认约定（见第二节）。View / SimplifyUI 已能拿到注入与 `[ListensTo]`，逻辑全部留在视图内即可。注意 `[Child]` / `[BindEvent]` / `[BindMethod]` / `[MainThread]` **只有 `SimplifyUI` 自动解析**，普通 View 需显式跑解析链或改用 `[SerializeField]`。只有你明确要求时才例外。

**Q：`Show<T>()` 返回 null / 界面不显示？**
A：先查 UI 预置体是否登记、 `UISettings` 是否绑定且 `InitRoots` 是否调用过；Toast 还要实现 `IToast`。

**Q：`Signal<T>()` 取不到信号？**
A：该信号未注册成单例。`commandBinder.Bind<S>()` / `BindCommand<S,C>()` 会自动注册；纯通知信号要显式 `BindSignals()`。注意 UI 里 `Signal<T>()` 是方法，`SimplifyIoC.Signals.Signal<T>` 是类型。

**Q：lint 报 W201，要改吗？**
A：若是你有意保留的既有 Mediator 代码，人工确认后可忽略；新写的代码应为 0 WARN。

**Q：写了 `SimplifyBootstrap<T>` 之类旧写法，lint 怎么不报错？**
A：本 Skill 只检查现行有效性约束，旧名由编译器把关。请按 `references/api.md` 的现行 API 改写。

**Q：启动命令为什么不单独建一个 `.cs` 文件？**
A：这是本 Skill 的结构约定，写成 `Bootstrap` 内的 `private class StartupCommand : Command`。框架自身 `SimplifyBootstrap.cs:163` 的 `private class StartupSignal : Signal` 就是私有嵌套类并由容器反射实例化，路径一致。嵌套类保持编译器默认 public 无参构造即可，不要手写 private 构造；Unity 的「MonoBehaviour 派生类独占同名 `.cs`」只约束 Component，`Command` 不是 MonoBehaviour。

**Q：可以直接改框架源码吗？**
A：不可以，除非你明确要求。发现框架缺陷时 Skill 只报告，业务代码只落在业务目录。

**Q：框架升级到 3.x 后还能用吗？**
A：需要重新核对 `references/api.md` 与 `references/pitfalls.md`。本包按 2.1.0 源码固化，版本跨度较大时不要直接套用。

## 十、包内文件

```
simplifyioc-quick-coding/
├── SKILL.md                      入口：决策树、5 步工作流、自检清单
├── README.md                     本说明
├── references/
│   ├── api.md                    现行 API 速查（含 SimplifyUI / UISettings / UIType / IToast）
│   └── pitfalls.md               现行硬约束与反模式
├── assets/templates/
│   ├── Bootstrap.cs.tpl
│   ├── Signal.cs.tpl
│   ├── Command.cs.tpl
│   ├── SimplifyUI.cs.tpl
│   └── View.cs.tpl
└── scripts/
    └── lint_mvcs.py
```

生成代码时由 `SKILL.md` 决定是否加载 `references/`；`README.md` 面向人阅读，不必加载。
