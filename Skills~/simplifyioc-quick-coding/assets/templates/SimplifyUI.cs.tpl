// 模板：UI 界面（View 承接全部逻辑，不使用 Mediator）。
// 替换 {{FEATURE}} / {{ROOT_NS}} / {{PAYLOAD_TYPE}}。
// 预置体必须登记到 CustomPrefabCreator（DefaultPrefabCreator 的 _views 列表），否则 Show<T>() 返回 null。
using SimplifyIoC.Settings;
using SimplifyIoC.UI;
using SimplifyIoC.Utils;
using UnityEngine;
using UnityEngine.UI;
using {{ROOT_NS}}.Signals;

namespace {{ROOT_NS}}.Views
{
    public class {{FEATURE}}UI : SimplifyUI
    {
        // public override UIType type => UIType.Popup;   // Screen(默认) / Overlay / Popup / Toast
        // public override bool disposeOnHide => false;  // false = 隐藏后回池复用

        [Child] public Text title;
        [Child] public Button confirm;

        private {{PAYLOAD_TYPE}} _data;

        // 链式配置器：一律返回 this，供 Show<{{FEATURE}}UI>().SetTitle(...).SetData(...) 使用。
        public {{FEATURE}}UI SetTitle(string value)
        {
            title.text = value;
            return this;
        }

        public {{FEATURE}}UI SetData({{PAYLOAD_TYPE}} value)
        {
            _data = value;
            return this;
        }

        public {{FEATURE}}UI Refresh()
        {
            // 依据 _data 刷新表现；返回 this 继续链
            return this;
        }

        protected override void Init()
        {
            // 一次性初始化。订阅信号时补 using SimplifyIoC.Signals;：
            // subscriptions.Listen(Signal<SomeChangedSignal>(), OnChanged);
        }

        protected override void OnShow()
        {
            // 每次展示都会调用；复用型 UI 的状态重置放这里
            Refresh();
        }

        [BindEvent("onClick", nameof(confirm))]
        private void OnConfirm() => Signal<{{FEATURE}}Signal>().Dispatch(_data);

        // private void OnChanged({{PAYLOAD_TYPE}} value) => SetData(value).Refresh();

        // 打开另一个界面：Show<OtherUI>().SetTitle("...").SetData(...);
        // 关闭自己：Hide();  业务侧别用 Hide(false)，那会跳过 onRecycle 回收。
        // 阻塞输入：Interactable(false); ... Interactable(true); 记得成对。
        // 覆写 Start() / OnEnable() 时必须调 base。
    }
}
