using System.Reflection;
using SimplifyIoC.Signals;
using SimplifyIoC.Utils;
using UnityEngine;
using UnityEngine.Events;

namespace SimplifyIoC.Mediations
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

        protected void Interactable(bool value)
        {
            uiSettings.Interactable(name, value);
        }

        protected T Signal<T>() where T : BaseSignal
        {
            return injectionBinder.GetInstance<T>();
        }

        protected virtual void BGM(string path)
        {
            //Signal<SoundSignal>().PlayBGM(path);
        }

        protected virtual void Sound(string path)
        {
            //Signal<SoundSignal>().PlaySFX(path);
        }

        protected virtual void Toast(string message)
        {
            //if(string.IsNullOrWhiteSpace(message)) return;
            //Show<Toast>().SetMessage(message);
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
