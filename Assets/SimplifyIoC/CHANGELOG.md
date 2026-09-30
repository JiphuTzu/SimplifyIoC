# CHANGELOG
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
+ **清理**：删除 `Promises/`（`IPromise` / `BasePromise` / `Promise` 及泛型变体）、`ImplicitBinds/`（`ImplicitBinder` / `[Implements]` / `[ImplementedBy]`）、`WhitelistBindings`、`ReflectedClass.setterNames` / `HasSetterFor`、`CommandBinder.GetPool<T>()`，以及多处注释遗留代码。

### 升级注意（破坏性变更）
+ **不要重复注册解析器**：若你的 View / Mediator 里仍保留旧的 `this.AddAttributeParser(this.GetEventMethodParser()).ParseAttributes()`（或同类手写调用），请删除，否则会解析两次、`[BindEvent]` 重复挂载。
+ **`Pool` 不再实现 `IPoolable`**：容器与元素两种身份已拆开（旧实现里归还元素会连带清空整个池）。元素清理契约由 `IPoolable` 独占。
+ **`usePooling` 失效**：命令全部池化，该开关不再影响行为。
+ **`Context.firstContext` 已删除**：两个没有 Transform 层级关系的 `Context` 不再自动结为父子；跨域共享请用 `SimplifyContext<T>(Bootstrap, Context parent)` 显式指定父级。
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