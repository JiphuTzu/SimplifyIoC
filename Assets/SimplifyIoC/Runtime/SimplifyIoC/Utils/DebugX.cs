/*
 * 使用方法：
 * PlayerSettings > OtherSettings > ScriptingDefineSymbols 中添加：
 * DEBUG_X
 * 或者
 * DEBUG_X_HIDE 初始时隐藏按钮，需要在左上角连续点击6次后，显示调试按钮
 */
#if DEBUG_X || DEBUG_X_HIDE
namespace SimplifyIoC.Utils
{

    using System;
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.UI;
    using Object = UnityEngine.Object;
    [RequireComponent(typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster))]
    public class DebugX : MonoBehaviour,ILogHandler
    {

        [RuntimeInitializeOnLoadMethod]
        private static void CheckOrCreate()
        {
            if(_instance) return;
            _ = new GameObject("DebugX",typeof(DebugX));
        }

        private static  DebugX _instance;

        private Text _text;
        private int _lines = 10;
        private const string _AUTHOR = "<color=#CCCCCC>\t [DebugX@JiphuTzu]</color>\n";
        private const string _ICON =
            "iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAAeUlEQVQ4EWNkAIKzQACiSQXGQMBIrmaYZUwwBrn0wBvAgux037Qz/5H5m2eZMMLE0NkwdSheACkCSYBomAaYGEwDOh/FAJgidBqXYSB1RBmAbiAynygDQOEAcwWyZhAbxQBYgIFoXJpgamAGjaZEBgZwwiE3R4KyMwAjrj6HJzm5/wAAAABJRU5ErkJggg==";
        private readonly List<string> _logs = new();
#if DEBUG_X_HIDE
        private bool _hideOnStart;
        private float _lastClickTime;
        private int _clickCount;
#endif

        private ILogHandler _defaultHandler;
        // Start is called before the first frame update
        private void Awake()
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            //
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            
            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            if (Screen.width > Screen.height)
            {
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 1;
            }
            else
            {
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.matchWidthOrHeight = 0;
            }
            
            //
            CreateDebugButton();

            CreateDebugText();
            
            _defaultHandler = Debug.unityLogger.logHandler;
            Debug.unityLogger.logHandler = this;
        }

        private IEnumerator Start()
        {
            //等到更新之后才能得到真实高度
            yield return new WaitForEndOfFrame();
            _lines = (int)(_text.rectTransform.rect.height / (_text.fontSize*1.2f));
        }

        private void OnDestroy()
        {
            Debug.unityLogger.logHandler = _defaultHandler;
        }

        private void OnLogVisible()
        {
#if DEBUG_X_HIDE
            if (_hideOnStart)
            {
                _clickCount++;
                if (Time.time - _lastClickTime > 0.5f)
                    _clickCount = 1;
                
                if (_clickCount == 6)
                {
                    _hideOnStart = false;
                    GetComponentInChildren<Image>().color = Color.white;
                }
                _lastClickTime = Time.time;
                return;
            }
#endif
            _text.gameObject.SetActive(!_text.gameObject.activeSelf);
            if(_text.gameObject.activeSelf)
                _text.text = _AUTHOR + string.Join("\n",_logs);
        }

        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            _defaultHandler?.LogFormat(logType, context, format, args);
            Log(string.Format(format, args));
        }

        public void LogException(Exception exception, Object context)
        {
            _defaultHandler?.LogException(exception, context);
            Log(exception.ToString());
        }

        private void Log(string log)
        {
            log = $"[{DateTime.Now:HH:mm:ss:fff}]{log}";
            _logs.Insert(0,log);
            if (_logs.Count >= _lines)
            {
                _logs.RemoveAt(_lines-1);
            }
            if(_text.gameObject.activeSelf)
                _text.text = _AUTHOR + string.Join("\n",_logs);
        }

        private void CreateDebugButton()
        {
            var bgo = new GameObject("Button", typeof(Image), typeof(Button));
            bgo.transform.SetParent(transform);
            var brt = bgo.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0, 1);
            brt.anchorMax = new Vector2(0, 1);
            brt.pivot = new Vector2(0, 1);
            brt.anchoredPosition = new Vector2(5, -5);
            brt.sizeDelta = new Vector2(50, 50);
            bgo.GetComponent<Button>().onClick.AddListener(OnLogVisible);
            //
            var t = new Texture2D(2, 2);
            t.LoadImage(Convert.FromBase64String(_ICON));
            t.Apply();
            var image = bgo.GetComponent<Image>();
            image.sprite = Sprite.Create(t,new Rect(0,0,16,16),new Vector2(0.5f,0.5f));
#if DEBUG_X_HIDE
            _hideOnStart = true;
            image.color = new Color(1, 1, 1, 0);
#endif
        }

        private void CreateDebugText()
        {
            var tgo = new GameObject("Log", typeof(Text));
            tgo.transform.SetParent(transform);
            var trt = tgo.GetComponent<RectTransform>();
            trt.anchorMax = Vector2.one;
            trt.anchorMin = Vector2.zero;
            //-right,-top
            trt.offsetMax = new Vector2(-20,-20);
            //left,bottom
            trt.offsetMin = new Vector2(20,20);
            _text = tgo.GetComponent<Text>();
#if UNITY_2022_1_OR_NEWER
            _text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            _text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
            _text.fontSize = 32;
            _text.color = new Color(0.93f, 0.95f, 0.92f, 0.8f);
            _text.alignment = TextAnchor.UpperLeft;
            _text.fontStyle = FontStyle.Bold;
            _text.raycastTarget = false;
            _text.text = _AUTHOR;
            tgo.SetActive(false);
        }
    }
}
#endif