# CHANGELOG
## 2.1.1
### 接口变更 
+ 增加``UISettings.GetActiveView<T>(out T view)``
+ 增加``UISettings.IsViewShowing<T>()``
+ 增加``UISettings.ReleaseView<T>()``
+ 删除``UISettings.GetView<T>()``
+ 增加``IToast.SetDuration(float)``
### 修复BUG
+ 没有设置StartupCommand时报错

## 2.1.0
### 破坏性变更（程序集拆分）
+ **基础 UI 能力拆为独立程序集 `Aixuma.SimplifyIoC.UI`**：`SimplifyUI.cs` / `UISettings.cs`（含 `UIType`、`IPrefabCreator`）/ `IToast.cs` / `CustomPrefabCreator.cs` / `DefaultPrefabCreator.cs` 从 `Runtime/SimplifyIoC/Mediations/` 移到与 `Runtime` 平级的 `UI/`，由新 asmdef 编译；新程序集单向引用核心 `Aixuma.SimplifyIoC`，核心不再包含任何 UI 类型，也不再依赖 `com.unity.ugui`。文件连同 `.cs.meta` 一起迁移，脚本 GUID 保持不变，下游已有的 `UISettings` / `DefaultPrefabCreator` 资产与预置体脚本引用不会断。命名空间随后按目录规则调整，见下节。
+ **迁移**：使用 asmdef 且用到上述 UI 类型的下游工程，需在业务 asmdef 的 `references` 里补一条 `Aixuma.SimplifyIoC.UI`（或 `GUID:85be5796041942a8867159794464d6aa`）；只用核心 IoC、或不用 asmdef（Assembly-CSharp）的工程无需改动。`link.xml` 同步为新程序集加了 `preserve="all"` 裁剪兜底。

### 破坏性变更（声明式解析收归 UI 层）
+ **`Runtime/SimplifyIoC/Utils/` 整体迁入 `UI/SimplifyIoC/Utils/`**：`[Child]` / `[BindEvent]` / `[BindMethod]` / `[MainThread]`、`ReflectionExtension`（含 `ReflectedTarget<T>`）与 `DebugX` 现归属 `Aixuma.SimplifyIoC.UI`。至此核心程序集才真正不再引用 `com.unity.ugui`——`DebugX` 原先也 `using UnityEngine.UI`，上一轮只挪 `UISettings` 时并未摘干净。UI 目录按两级归类：`UI/`（`SimplifyUI`、`IToast`）、`Settings/`（`UISettings`、两个 `PrefabCreator`）、`Utils/`。
+ **`View.InitAttributes()` 移到 `SimplifyUI`**：`View` / `Mediator` 删掉 `InitAttributes` 与 `EnsureAttributesInitialized`，`MediationBinder` / `Bootstrap` 删掉全部触发调用与 `UnbindMethods()` 清理，编辑器菜单 `MapChildren` 也随解析能力只留在 UI 层。核心只保留容器注入、`[ListensTo]` 与生命周期上报，彻底不依赖 `SimplifyIoC.Utils`。`SimplifyUI` 在首次 `Start`（`base.Start()` 完成注入之后）幂等解析一次，并在 `OnDestroy` 里释放 `[BindMethod]` 静态表条目。
+ **影响面**：普通 `View` / `Mediator` 上的 `[Child]` / `[BindEvent]` / `[BindMethod]` / `[MainThread]` 不再自动生效；`Assets/Examples` 的 `MenuView` / `RecordView` / `CameraMediator` / `LifeTimeMediator` 即属此类（编译仍通过，但需改为显式解析或迁移基类）。`[Inject]` / `[PostConstruct]` / `[Construct]` / `[Name]` / `[ListensTo]` 不受影响。Mediator 的 `[BindMethod]` 条目不再主动摘除，由 `ConditionalWeakTable` 随 GC 回收。

### 破坏性变更（UI 命名空间按目录分层）
+ **命名空间与目录一一对应**：沿用核心既有规则（`Runtime/SimplifyIoC/<目录>/` → `SimplifyIoC.<目录>`），UI 程序集内 `UI/SimplifyIoC/UI/` 下的 `SimplifyUI` / `IToast` 改为 `SimplifyIoC.UI`，`UI/SimplifyIoC/Settings/` 下的 `UISettings` / `UIType` / `IPrefabCreator` / `CustomPrefabCreator` / `DefaultPrefabCreator` 改为 `SimplifyIoC.Settings`；`Utils/` 下的 6 个类原本就是 `SimplifyIoC.Utils`，保持不变。跨命名空间引用已在各文件补齐 `using`，`DebugX.cs` 中全局 `DebugXExtensions` 依赖的 `using SimplifyIoC.Utils;` 保留。
+ **迁移**：下游源码把 UI 类型对应的 `using SimplifyIoC.Mediations;` 换成 `using SimplifyIoC.UI;` / `using SimplifyIoC.Settings;`；只用核心 `View` / `Mediator` / `MediationBinder` 的代码不受影响（它们仍在 `SimplifyIoC.Mediations`）。脚本 GUID 与资产序列化引用不变。
+ **`Runtime/Materials/` 迁入 `UI/Materials/`**：`ColorMask` / `Voronoi` 的 shader 与 mat 连 `.meta` 一起迁移，目录 GUID 也不变；4 个资源仅存在 mat → shader 的内部引用，工程内没有其它资产或代码引用它们，路径变化不影响序列化。

### 破坏性变更（`View.GetSignal<T>()` 移除）
+ `View` 不再提供 `protected T GetSignal<T>()`：它只是 `injectionBinder.GetInstance<T>()` 的转发，与 `SimplifyUI.Signal<T>()` / `Mediator.Get<T>()` 三套同义 API 并存容易混淆。**迁移**：普通 View 改用 `injectionBinder.GetInstance<T>()` 或 `[Inject]` 属性注入，UI 层用 `Signal<T>()`，Mediator 侧 `Get<T>()` 不变。包内 README 的信号示例、配套 skill 的 `View.cs.tpl` 模板与 W205 自检规则已同步。

### 其它
+ `IPrefabCreator` 从 `UISettings.cs` 拆到同目录独立文件 `UI/SimplifyIoC/Settings/IPrefabCreator.cs`，命名空间仍为 `SimplifyIoC.Settings`；`UIType` 枚举仍留在 `UISettings.cs`。接口不参与 Unity 资产序列化，新脚本 GUID 独立生成，`UISettings` / `DefaultPrefabCreator` 等存量资产的引用不受影响。
+ 随包附带可选的 **WorkBuddy Skill** `Skills~/simplifyioc-quick-coding/`（API 速查、5 个模板、`lint_mvcs.py`）。它只是随 `Assets/SimplifyIoC/` 一起分发的文件，**不自动生效**：使用者需自行复制到 `~/.workbuddy/skills/` 或工程的 `.workbuddy/skills/`。目录名以 `~` 结尾是 UPM 的既有惯例（同 `Samples~`）：Unity 资产管线完全忽略该目录，**不生成也不需要任何 `.meta`**，但 git 与 `git subtree split --prefix=Assets/SimplifyIoC --branch upm` 照常把它随包分发；通过 git URL 安装时可从 `Library/PackageCache/com.aixuma.simplifyioc@<版本>/Skills~/` 复制安装。
+ 校正随包分发的 Skill 与包元数据里残留的过时描述：移除「`View` 自动解析声明式特性 / `EnsureAttributesInitialized()`」、不存在的 `UISettings.ReleaseView<T>()`、`InitRoots` 四参与虚构的 `UIRoots` 类型——`InitRoots` 实为三参 `(Transform screen, Transform overlay, Transform popup)`、返回 `UISettings`，遮罩取 `popup.GetChild(0)`。`package.json` 的 `description` 与包内 README 首段同步改为描述 UI 可选程序集；skill 的 `SKILL.md` / `README.md` / `references/{api,pitfalls}.md`、两个模板与 `lint_mvcs.py` 一并更新（新增 W207：普通 View 上用了声明式特性却未显式解析时告警；`View.cs.tpl` 改用 `[SerializeField]` 以免生成的绑定静默失效）。历史版本段落（1.3.0 等）保持原样。

### 升级注意（迁移速查）
+ 使用 asmdef 的下游工程补一条 UI 程序集引用；用到 `SimplifyIoC.Utils` 的工程**也必须引用 UI 程序集**，这些类型已不在核心。`UI/` 与 `Runtime/` 一同随 UPM 包发布，脚本 GUID 均未变；命名空间已按目录分层，见上一节。
+ 排查所有普通 `View` / `Mediator` 上的四个声明式特性：改为在 `Start` 里显式调用解析链，或把组件迁到 `SimplifyUI` 派生类。

## 2.0.2
### 破坏性变更
+ **`SimplifyBootstrap<T>` 改为非泛型 `SimplifyBootstrap`**：启动命令类型不再写在类声明上，改由钩子指定——新增 `protected abstract void SetStartupCommand()`，派生类必须实现并在体内调用 `SetStartupCommand<T>()`（`T : Command`）；`BindCommand<StartupSignal, T>(true)` 从 `MapBindings()` 移到 `Start()`，由 `Start()` 先 `SetStartupCommand()` 再 `Launch()`。**迁移**：`class GameBootstrap : SimplifyBootstrap<GameStartupCommand>` 改为 `class GameBootstrap : SimplifyBootstrap`，并补 `protected override void SetStartupCommand() => SetStartupCommand<GameStartupCommand>();`；覆写 `Start()` 时仍须调 `base.Start()`（内含上述两步）。
+ **包内测试体系删除**：`Assets/Tests`、`Runtime/SimplifyIoC/AssemblyInfo.cs`（含 `InternalsVisibleTo("Aixuma.SimplifyIoC.Tests")`）及 Tests 的 csproj / sln 条目一并移除。`Init()` 现**仅框架程序集自身可见**，不再有外部程序集能显式驱动它；EditMode 下 Unity 不派发 `Awake`，自写测试一律走 PlayMode，包本身改为在 Unity 编辑器手动验收。

### 修复与改进
+ **`ToHandler` 短路通路收口**：`SignalHandler` 由独立类改为 `CommandBinding` 的**私有嵌套类**，`handler` 字段改为私有的 `_handler`；读取口 `bool hasHandler { get; }` 与 `void InvokeHandler(IInjectionBinder, object)` 加进 `ICommandBinding`（正常只应由 `CommandBinder` 在派发时调用），派发端由此免去 `is CommandBinding` 类型判断。独立 `SignalHandler.cs` 已删除。
+ **`ViewCache` 收为 `Bootstrap` 的私有内部类**：早期视图缓存不再有独立类型与文件；`parentBootstrap` / `isRoot` / `isReady` 由 `internal` 放宽为 `protected` 供派生类检查链结构，`_viewCache` 收紧为 `private`。
+ `RemoveView` / `EnableView` / `DisableView` 的空判统一为 `mediationBinder?.Trigger(...)`，行为不变。

### 升级注意（迁移速查）
+ 从 2.0.0 / 2.0.1 升级只需改启动入口：删掉类声明上的泛型实参，补一行 `SetStartupCommand()` 钩子；四个 `Bind*` 分组与 `MapBindings()`（sealed，不要覆写）的用法不变。

## 2.0.1
### 修复与改进
+ 修复 2.0.0 引入的基础 UI 能力（`SimplifyUI` / `UISettings`）里的条件分支问题：`GetBootstrapPrefab` 命中后未中断遍历会重复实例化预置体；预置体查找与弹窗回收改为早返回写法，逻辑等价但不再误触发。
+ **Runtime 字段命名统一**：非公开字段改为 `_camelCase` 前缀（如 `Binder.bindings` → `_bindings`），`Binder` / `Pool` / `SignalSubscription` / `ReflectionExtension` 等文件的引用同步更新；公开 API 未变。

### 升级注意（迁移速查）
+ 自定义 `Binder` / `Pool` 等派生类若直接引用过基类的 `protected` 字段（如 `bindings`），按 `_camelCase` 新名更新引用即可。

## 2.0.0
### 破坏性变更
+ **`Context` 更名为 `Bootstrap`，并与旧的 `Bootstrap`（纯层级锚点类）合并为一个类**：框架里现在只剩 `Bootstrap`——它自己就是 `MonoBehaviour`，既是**层级锚点**（挂在显示层级顶部的那块组件），也是**绑定容器**。旧的 `Bootstrap` / `SimplifyBootstrap`（非泛型、负责创建 Context 的那两个）已删除；`SimplifyContext<T>` 更名为 `SimplifyBootstrap<T>`；`Context` 这个类名不再存在。改前两个类靠 `context ⇄ bootstrap` 双向可变链接相连，忘写其中一半就会**静默**分叉（Context 悄悄自任链根、跨域绑定各持一个 binder，报错现场离原因很远）；合并后这类失效在类型层面不可能发生。
+ 启动入口只剩**一个组件**：派生 `SimplifyBootstrap<T>`（或裸 `Bootstrap`）后直接挂到 GameObject 上——`Awake` → `Init()`（建容器 + 确定归属 + 映射），`Start` → `Launch()`（`SimplifyBootstrap<T>` 自动补调）。（**2.0.2 起泛型入口已改为非泛型 `SimplifyBootstrap` + `SetStartupCommand()` 钩子，见文首 2.0.2 段。**）
+ **`Bootstrap.Init()` 取代带参构造**（MonoBehaviour 没有带参构造）：**无父参数**、幂等，`Awake` 自动调用，且**可见性为 `internal`**——它是框架自用的生命周期入口，当时仅框架自身与包内测试程序集（经 `InternalsVisibleTo`）可见，下游程序集调不到；**2.0.2 起测试程序集与 `AssemblyInfo.cs` 已删除，现仅框架程序集自身可见**；运行时 `AddComponent<XxxBootstrap>()` 照常由 Unity 派发 `Awake`，下游正常使用不受影响。归属自动解析，调用方不需要持有、也不需要知道任何别的 Bootstrap 的类型——**沿 Transform 向上找最近的已就绪 Bootstrap → 框架级锚点 `Bootstrap.root` 兜底**；要指定「锚点之外的某个父」用 `AddBootstrap(父)`。**幂等仅指"重复调用无副作用"**：`Dispose()` 之后调用 `Init()` 会抛 `ObjectDisposedException`（与 `Launch()` / `AddBootstrap()` 一致，已释放的 Bootstrap 不可复活）。
+ **框架级锚点改名 `Context.rootContext` → `Bootstrap.root`**（语义不变：第一个 `Init` 成功的链根自动持有、先到先得、弱引用、`Dispose()` 时清空）。
+ **命名注入键 `ContextKeys` → `BootstrapKeys`**，其成员 `Context` → `Bootstrap`（框架在 `Bootstrap.AddCoreComponents` 里 `Bind<Bootstrap>().ToValue(this).ToName(BootstrapKeys.Bootstrap)`）。
+ **跨域一族的命名不随本次更名而变**：`ICrossContextInjectionBinder`、`CrossContextInjectionBinder`、`CrossContextBridge`（宿主在 `Framework/`）、`crossContextBinder`、链式入口 `.CrossContext()`，以及 `SimplifyBootstrap.BindValue` / `BindSignal` 的 `crossContext` 参数，**一律保持 `Context` 命名**（“跨域”跨的是 `Context` 边界，与 `Context` 这个类本身是否更名无关）。绑定标记 `IInjectionBinding.isCrossContext` 同样**未动**。
+ **`View` 侧成员改名**：`owningContext` → `owningBootstrap`、`SetOwningContext` → `SetOwningBootstrap`、`BubbleToContext` → `BubbleToBootstrap`、`autoRegisterWithContext` → `autoRegisterWithBootstrap`、`registeredWithContext` → `registeredWithBootstrap`、`requiresContext` → `requiresBootstrap`。
+ **`Bootstrap` 链成员改名**：`AddContext` → `AddBootstrap`、`RemoveContext` → `RemoveBootstrap`、`ResolveParentContext` → `ResolveParentBootstrap`、`parentContext` → `parentBootstrap`；命名空间 `SimplifyIoC.Contexts` → `SimplifyIoC.Bootstraps`（文件与目录同步更名）。
+ `MapBindings` 仍在 `Init()` 内被调用（虚调用陷阱不变）：只放类型绑定，值绑定请在 `Init()` 之后补；Unity 序列化字段此时**已经可用**（Unity 在 `Awake` 之前完成反序列化）。
+ **命令绑定侧的池化开关全部移除**：`CommandBinder.usePooling`（5.5 起即为 `[Obsolete]` 的 no-op）、`ICommandBinding.Pooled()` 及配套的 `isPooled` 一并未声明删除——命令自 5.5 起就统一走池，这三个成员早已不影响任何行为。随之移除的还有 `.Pooled()` 与 `.ToHandler(...)` 的互斥校验（该组合现已无从表达）。**迁移：删掉 `.Pooled()` 调用与 `usePooling = ...` 赋值即可，行为完全不变。**

### 修复与改进
+ **修复：`Dispose()` 之后调用 `Init()` 会抛 `ObjectDisposedException`（此前静默返回）。** 2.0.0 合并时新增的幂等判据（内部 `_coreInstantiated`）原本排在 `ThrowIfDisposed()` **之前**，而 `Dispose()` 并不重置该字段，于是「已释放的 Bootstrap 再 `Init()`」既不生效、也不报错，调用方无从察觉；现守卫前置，与 `Launch()` / `AddBootstrap()` 的契约一致。1.3.x 无此问题——旧 `Context.Start()` 没有幂等判据，守卫本就在首位（本项修的是 2.0.0 开发期的内部回归，不影响 1.3.x 升级者）。
+ **明确「命令绑定的 key 只能是 Signal 类型」**：`CommandBinder` / `ICommandBinder` 类头示例中原有的字符串 key 示例已删除，示例统一为 Signal 类型——`Bind<SomeSignal>().To<SomeCommand>()`，以及 `.Once()`（单次使用后立即销毁该绑定）/ `.InSequence()`（多个 Command 串行执行）两条链式用法。命令派发完全由 Signal 广播驱动，字符串 / 枚举 key 虽然能编译通过，却**没有任何触发路径**（本框架已无 Event 系统）——照旧写法绑定的命令会静默不执行。

### 升级注意（迁移速查）
+ **一处机械替换可覆盖大半**：把工程里的 `Context` 换成 `Bootstrap`（`SimplifyContext<T>` → `SimplifyBootstrap<T>`、`ContextKeys.Context` → `BootstrapKeys.Bootstrap` 均自动覆盖；**2.0.2 起入口改为非泛型 `SimplifyBootstrap` + `SetStartupCommand()`，见文首**）；两处例外——锚点属性 `Context.rootContext` 现名 **`Bootstrap.root`**、命名空间 `SimplifyIoC.Contexts` 现名 **`SimplifyIoC.Bootstraps`**。
+ **`Init(parent)` 的参数已移除**：原先写 `Init(Bootstrap.root)` 或 `Init(FindObjectOfType<XxxBootstrap>())` 的地方直接改成 `Init()` 即可，行为一致——父尚未就绪时该参数本来就会被忽略并退回锚点解析（跨场景加载的 Awake 顺序不受保证，所以它从来没能真正"指定"父）；它唯一多出的能力是"指定锚点之外的父"，请改用 `AddBootstrap(父)`。随之不再需要「覆写 `Awake` 后不要先调 `base.Awake()`，否则父被忽略」这条注意事项。
+ 旧的 `context = new MyContext(this)` / `new MyContext(bootstrap)` 写法请改为「派生 `Bootstrap` 组件」——建容器与归属已由 `Awake` 自动完成，`Bootstrap.context` 属性不再存在。
+ `[Inject(ContextKeys.Bootstrap)] Bootstrap root` 这类旧写法统一改成 `[Inject(BootstrapKeys.Bootstrap)] Bootstrap root`（`Bootstrap` 本身就是 MonoBehaviour，`root.transform` 照常可用）。
+ **自写的 EditMode 测试请整体迁移为 PlayMode 测试**：`Init()` 的可见性已收为 `internal`，下游程序集无法显式调用它，而 EditMode 下 Unity 又不派发 `Awake`——旧写法「先 `AddComponent<Bootstrap>()` 再 `new XxxContext(bootstrap)`」的替代品，是 PlayMode 里 `AddComponent<XxxBootstrap>()` 后让 Unity 自己走 `Awake → Init()`。（2.0.2 起包内测试程序集与 `InternalsVisibleTo` 已删除，本条仅适用于外部使用者自写的测试。）
+ 示例工程只需跟随框架改类型引用：`using SimplifyIoC.Contexts` → `using SimplifyIoC.Bootstraps`、`Context` 类型引用 → `Bootstrap`、`[Inject(ContextKeys.Context)]` → `[Inject(BootstrapKeys.Bootstrap)]`。示例自身的类名 / 文件名 / 命名空间（`Aixuma.Bootstraps`）与场景 `CrossContextExample` 本就按 `Bootstrap` 命名，无需改动；`.unity` 按脚本 GUID 引用脚本，更不受影响。
+ 各版本条目均保留当时真实的 API 名（本段的 `SimplifyBootstrap<T>`、包内测试程序集与 `InternalsVisibleTo`，以及 1.3.x 及更早的 `Context`、`Context.rootContext`、`Context.firstContext`、`ContextKeys` 等），仅供追溯；现行 API 以文首 **2.0.2** 段与包内 README 为准。

## 1.3.2
### 新增
+ **框架级 Context 锚点 `Context.rootContext`（恢复 `firstContext` 的兜底语义）**：供「跨场景 / 并列 Bootstrap」
  取得父 Context，调用方因此无需知道 App 侧的具体类型（热更新分层：场景/玩法层不依赖 App 层）。
  **自动登记**：第一个构造的**链根** Context 持有（先到先得、不抢占；锚点被 `Dispose()`
  ——含 App 根 Bootstrap 被卸载——后，由下一个构造的链根接任）。它同时是两条兜底的落点：
  - Context 构造时，**显式 parent 与 Transform 层级都找不到父 ⇒ 自动挂到锚点下**（"后来者自动成为子"）；
  - View 向上**找不到任何 Bootstrap ⇒ 注册到锚点，而不是抛异常**（仅 `requiresContext = true`；`false` 不参与）。
  归属优先级始终是 **显式 parent > Transform 层级 > 锚点兜底**。弱引用存储，锚点 `Dispose()` 时自动清空。

### 升级注意（行为变更）
+ **1.3.0 移除的全局兜底已恢复**（旧名 `Context.firstContext`，现名 `Context.rootContext`）：
  1.3.0 曾规定"View 必须位于某个 Bootstrap 的子孙层级，否则抛异常"、"无明显父子关系的两个 Context 互不隶属"——
  这两条**不再成立**：层级外的 View 会注册到锚点，没有 Transform 关系的 Context 会挂到锚点下。
  行为仍然是确定的：**显式 parent 与 Transform 层级优先**，锚点只在两者都表达不出来时才生效。
+ `Context.firstContext` 这个名字**没有恢复**，请统一使用 `Context.rootContext`。把 View 放进 Bootstrap 子树、
  或显式传 parent 仍是推荐做法（归属最清晰、级联 Dispose 范围最小）。

## 1.3.1
### 修复与改进
+ **`[BindEvent]` 方法多目标修复**：方法上标注多个 targetName（如 `[BindEvent("onClick", "btn1", "btn2")]`）时，原实现命中第一个目标即返回，其余目标被静默忽略；现每个目标独立解析、互不影响，且某个名字解析失败不再中断后续目标。
+ **`[BindEvent]` 收敛为仅限方法**：特性不再允许标注在字段 / 属性上（`AttributeTargets.Method`），`BindUsage` 枚举、`GetEventFieldParser` / `GetEventPropertyParser` 扩展及对应解析路径已删除——它们在框架内从未被注册（View / Mediator 只注册方法解析器），全仓也无调用方。targetNames 解析"事件宿主"时对字段 / 属性 / 数组 / List 的支持保持不变。

### 升级注意（破坏性变更）
+ **`[BindEvent]` 仅支持标注在方法上**：此前标在字段 / 属性上的写法（由手写 `GetEventFieldParser` / `GetEventPropertyParser` 驱动）不再可用，请改为标注在监听方法上、targetNames 指向事件宿主；`BindUsage` 枚举与上述两个扩展方法已删除。

## 1.3.0
本轮为框架内部质量与 API 现代化改造的累积成果（对应内部"阶段 0–5"，20+ 次提交）。
**含破坏性变更**，升级前请先读文末「升级注意」。

### 新增
+ **声明式绑定全自动解析**：`[Child]` / `[BindEvent]` / `[BindMethod]` / `[MainThread]` 现由框架在**注入完成后统一解析**，View / Mediator 子类不再需要手抄 `this.AddAttributeParser(...).ParseAttributes()`。
+ 解析过程引入**类型级匹配缓存**（按「目标类型 + 特性类型 + 成员种类 + 查找标志」缓存命中成员与特性实例）与幂等哨兵，避免每次实例化重复反射。
+ **信号订阅句柄**：`AddListener` / `AddOnce` 返回 `SignalSubscription`（源兼容，忽略返回值的旧代码照常编译），`Dispose()` 即退订、幂等、可用 `using`；新增 `SignalSubscriptionGroup`，View / Mediator / Command 销毁时集中回收订阅。
+ **强类型绑定入口**：`Bind<T>(string)`、`Bind<T>(Enum)`、`Bind<TKey, TValue>()`、`Bind<TKey, TValue>(string / Enum)`。
+ `Mediator<TView>` 强类型视图（免写 `[Inject]` 视图字段）；`Bind<TSignal>().ToHandler(...)` 信号直达，短路容器往返。
+ **`IDisposable` 契约**：`Context` 销毁会级联销毁其所有子 `Context`，并集中回收订阅。
+ 随包提供 `link.xml` 保留配置，固定 Stripping 相关保留项。

### 修复与改进
+ **P0 八项**：`Binder.RemoveValue` 改走正常路径；`BindMethodAttribute` 排序改用 `order.CompareTo`；`MediationBinder` 的委托判别改为 `signal is Signal`（`Signal<T>` 及任意深度子类均走委托路径）；`Injector.Instantiate` 引入实例化深度，环形依赖由"静默无界递归"改为限次后可捕获异常；构造注入的多实例路径统一走 `Inject(x, false)`；`InjectorFactory.CreateFromValue` 不再吞掉构造异常；删除 `Context` 无调用方的构造重载。
+ **反射与 AOT 加固**：setter 注入、`constructor.Invoke`、`[PostConstruct]` 调用、`ReflectionExtension` 解析器调用全部委托化（无 `MakeGenericType` / `Expression` / `Emit`）；`ReflectionBinder.MapMethods` 去掉排序期逐次 `GetCustomAttribute`（`PriorityComparer` 删除）；`Pool.FailIf` 改为惰性求值（新增 `FailIf(bool, Func<string>)` 重载）；`Pool` 内部由 `System.Collections.Stack` 改为 `Stack<object>`。
+ **作用域治理**：Mediation 往返、`GetCommand`、信号载荷改走**调用级作用域**，去掉全局临时绑定与固定 key；`suppliers` 双注册表收敛；`Context` 与 Binder 由继承改为组合；Context 启动扫描优化。
+ **命令池化统一**：所有命令统一走池，非池化路径移除（`usePooling` 已标 `[Obsolete]`，不再影响行为）。
+ **View 归属**：View 注册成功一刻记录 `owningContext`，此后的 Add / Remove / Enable / Disable 只发给它；`Bootstrap.context` 改由框架写入。
+ **启动语义统一**：移除 `ContextStartupFlags`。`Context` 构造函数固定完成映射（原 Automatic 行为），启动（`Launch()`）永远显式调用——推荐路径 `SimplifyBootstrap.Start` 自动补调，裸 `Bootstrap` 派生类需自行在 `Start` 里调用 `context.Launch()`。
+ **品牌更名 UMa → Aixuma**：包名 `com.uma.simplifyioc` → `com.aixuma.simplifyioc`；程序集 `UMa.SimplifyIoC` / `.Tests` / `.Examples` → `Aixuma.…`（asmdef 同名更名）；示例命名空间 `UMa.*` → `Aixuma.*`，目录 `Examples/Scripts/UMa` → `Aixuma`；`link.xml`、`InternalsVisibleTo` 与注释署名同步更新。
+ **清理**：删除 `Promises/`（`IPromise` / `BasePromise` / `Promise` 及泛型变体）、`ImplicitBinds/`（`ImplicitBinder` / `[Implements]` / `[ImplementedBy]`）、`WhitelistBindings`、`ReflectedClass.setterNames` / `HasSetterFor`、`CommandBinder.GetPool<T>()`，以及多处注释遗留代码。

### 升级注意（破坏性变更）
+ **不要重复注册解析器**：若你的 View / Mediator 里仍保留旧的 `this.AddAttributeParser(this.GetEventMethodParser()).ParseAttributes()`（或同类手写调用），请删除，否则会解析两次、`[BindEvent]` 重复挂载。
+ **`Pool` 不再实现 `IPoolable`**：容器与元素两种身份已拆开（旧实现里归还元素会连带清空整个池）。元素清理契约由 `IPoolable` 独占。
+ **`usePooling` 失效**：命令全部池化，该开关不再影响行为。
+ **`Context.firstContext` 已删除**：两个没有 Transform 层级关系的 `Context` 不再自动结为父子；跨域共享请用 `SimplifyContext<T>(Bootstrap, Context parent)` 显式指定父级。
+ **`ContextStartupFlags` 已删除（原 Automatic / ManualMapping / ManualLaunch）**：构造固定完成映射，`Launch()` 不再自动触发。直接派生 `Context`（非 `SimplifyContext<T>`）且覆写了 `Launch` 的代码，必须在 `Start` 里自行调用 `context.Launch()`，或改用 `SimplifyBootstrap`；若仍传 flags 参数会编译失败，直接删除实参即可。
+ **`Bootstrap.context` 由框架写入**：不要手工赋值（会被覆盖，导致 Context 静默自任链根、跨域绑定分叉）。
+ **`Binding` 扩展点收敛**：逻辑收口到 `protected virtual BindCore / ToCore / ToNameCore / NamedCore`，覆写过旧外观方法的子类需迁移。
+ **`AddListener` / `AddOnce` 返回类型**由 `void` 变为 `SignalSubscription`：调用处忽略返回值不受影响，覆写过这两个方法的子类需改签名。
+ **已删除的 API**：若引用过 `Promises/`、`ImplicitBinds/`（`[Implements]` / `[ImplementedBy]`）、`WhitelistBindings`、`ReflectedClass.setterNames` / `HasSetterFor`，需一并移除。

## 1.2.2
+ BindEvent可以直接绑定数组
+ 删除废除的方法
+ 优化DebugX的更新
## 1.2.1
+ DebugX中可自定义显示内容
## 1.2.0
+ 使用Bootstrap替换ContextView
+ 优化工具类
## 1.1.0
+ 彻底删除EventDispatcher
+ 调整类的文件目录
+ 清理一些不常用的类，去除一些接口
+ 优化工具类
## 1.0.8
+ 修复ChildAttribute的不可用问题
+ 调整AddAttributeParser方法
## 1.0.7
+ BindEvent添加对私有变量的支持，参数改为可选的targetNames，支持多对象或方法绑定
+ BindToEvent修改为BindEvent
+ 使用target.AddAttributeParser(this.GetXxxParser()).ParseAttributes()统一处理Attribute
+ Mediator和View添加Get<T>() where T:BaseSignal方法，省去Inject环节，直接使用。
## 1.0.6
+ BindEventAttribute添加对property的支持
+ BindEvent添加对所有UnityEventBase的支持
+ 自定义BindEvent的使用对象
## 1.0.5
+ 修复MapChildren时报错
+ 添加BindEvent和BindToEvent两个Attribute
+ 修改命名空间
## 1.0.3
+ 添加Child和BindMethod两个Attribute
## 1.0.2
+ 允许View使用ListenTo直接监听信号
## 1.0.1
+ 修改package.json的编码格式为UTF8，解决windows版本U3D不能解析的问题
## 1.0.0
+ 修改命名空间和方法、属性名，更符合Unity3D的习惯
+ 放弃运行时JSON注入
+ 删除事件总线，统一使用信号
+ 解决CodeStrip时引起的缺少构造函数和setter的问题