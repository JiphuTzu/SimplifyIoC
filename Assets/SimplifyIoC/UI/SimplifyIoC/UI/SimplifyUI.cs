using System;
using System.Reflection;
using SimplifyIoC.Mediations;
using SimplifyIoC.Settings;
using SimplifyIoC.Signals;
using SimplifyIoC.Utils;
using UnityEngine;
using UnityEngine.Events;

namespace SimplifyIoC.UI
{

    public abstract class SimplifyUI : View
    {
        public virtual UIType type { get; } = UIType.Screen;
        //
        public virtual bool disposeOnHide => true;
        public virtual bool canHideByMask => type == UIType.Popup;
        //
        public UnityEvent<SimplifyUI> onRecycle { get; } = new();
        private bool _initialized;
        private bool _attributesInitialized;

        public void Show()
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            else if (type == UIType.Popup && _initialized)
                OnShow();
        }

        public virtual void Hide(bool withEvent = true)
        {
            if (withEvent) onRecycle.Invoke(this);
        }

        protected virtual void OnShow() { }
        protected virtual void Init(){}

        protected override void Start()
        {
            base.Start();
            EnsureAttributesInitialized();
            Init();
            _initialized = true;
            OnShow();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (_initialized) OnShow();
        }

        private UISettings _uiSettings;

        protected UISettings uiSettings
        {
            get
            {
                _uiSettings ??= injectionBinder.GetInstance<UISettings>();
                return _uiSettings;
            }
        }
        protected float canvasScale => uiSettings.canvasScale;

        protected T Show<T>() where T : SimplifyUI
        {
            return uiSettings.CreateView<T>();
        }

        protected void Show<T>(Action<SimplifyUI> callback) where T : SimplifyUI
        {
            uiSettings.CreateView<T>(callback);
        }

        protected void Interactable(bool value)
        {
            uiSettings.Interactable(name, value);
        }

        protected T Signal<T>() where T : BaseSignal
        {
            return injectionBinder.GetInstance<T>();
        }

        /// <summary>
        /// 声明式绑定解析（[Child] / [BindEvent] / [BindMethod] / [MainThread]）。
        /// 核心 View 不再承担解析，这套能力只属于 UI 层；本类在首次 Start、
        /// base.Start() 完成注入之后触发一次，业务可覆写以扩展解析内容。
        /// </summary>
        protected virtual void InitAttributes()
        {
            //[BindMethod] 依赖弱引用静态表（ConditionalWeakTable），避免自动解析放大泄漏。
            var bindMethodParser = this.GetBindMethodParser();
            var reflected = this.AddAttributeParser(this.GetEventMethodParser())
                .AddAttributeParser(this.GetChildParser())
                .AddAttributeParser(this.GetMainThreadParser());
            //判空是防御性的：GetBindMethodParser 恒返回委托，但 null 委托传给
            //AddAttributeParser 会在 CreateParser 里因 parser.Method 立即 NRE。
            if (bindMethodParser != null)
                reflected = reflected.AddAttributeParser(bindMethodParser);
            reflected.ParseAttributes();
        }

        private void EnsureAttributesInitialized()
        {
            if (_attributesInitialized) return;
            _attributesInitialized = true;
            InitAttributes();
        }

        protected override void OnDestroy()
        {
            //立即释放本组件的 [BindMethod] 静态注册表条目（不依赖 GC 时机）。
            this.UnbindMethods();
            base.OnDestroy();
        }

        protected virtual IToast Toast(string message)
        {
            return null;
        }
#if UNITY_EDITOR
        [ContextMenu("🔭 MapChildren",false,0)]
        private void AutoMapChildren()
        {
            this.AddAttributeParser(this.GetChildParser())
                .ParseFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
