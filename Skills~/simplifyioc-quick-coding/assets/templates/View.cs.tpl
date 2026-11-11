// 模板：非 UI 场景表现层。UI 界面一律改用 SimplifyUI.cs.tpl。
// 逻辑全部写在 View 内，不生成 Mediator；无 Mediator 绑定时框架仍会注入并挂 [ListensTo]。
// 注意：普通 View 不自动解析 [Child] / [BindEvent] / [BindMethod] / [MainThread]，
// 所以本模板用 [SerializeField]，不依赖 UI 程序集；需要声明式特性请改用 SimplifyUI.cs.tpl。
using SimplifyIoC.Mediations;
using UnityEngine;
using {{ROOT_NS}}.Signals;

namespace {{ROOT_NS}}.Views
{
    public class {{FEATURE}}View : View
    {
        [SerializeField] private Transform anchor;

        protected override void Start()
        {
            base.Start();
            // 订阅信号时补 using SimplifyIoC.Signals;：
            // subscriptions.Listen(injectionBinder.GetInstance<SomeChangedSignal>(), OnChanged);
        }

        // 非 UI View 侧从容器取信号单例；派发写成一条链：
        private void Send({{PAYLOAD_TYPE}} value) => injectionBinder.GetInstance<{{FEATURE}}Signal>().Dispatch(value);

        // private void OnChanged(T value) { ... }

        // 覆写 Awake / Start / OnEnable / OnDisable / OnDestroy 时都要先调 base。
    }
}
