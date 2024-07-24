/*
 * 使用方法：
 * PlayerSettings>OtherSettings>ScriptingDefineSymbols中添加：
 * DEBUG_X
 */
#if DEBUG_X
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
        private const string _AUTHOR = "\t<color=#CCCCCC><size=10>[DebugX@JiphuTzu]</size></color>\n";
        private const string _ICON =
            "iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAAeUlEQVQ4EWNkAIKzQACiSQXGQMBIrmaYZUwwBrn0wBvAgux037Qz/5H5m2eZMMLE0NkwdSheACkCSYBomAaYGEwDOh/FAJgidBqXYSB1RBmAbiAynygDQOEAcwWyZhAbxQBYgIFoXJpgamAGjaZEBgZwwiE3R4KyMwAjrj6HJzm5/wAAAABJRU5ErkJggg==";
        private readonly List<string> _logs = new();

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
            scaler.matchWidthOrHeight = 1;
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
            brt.anchoredPosition = new Vector2(2, -2);
            brt.sizeDelta = new Vector2(16, 16);
            bgo.GetComponent<Button>().onClick.AddListener(OnLogVisible);
            //
            var t = new Texture2D(2, 2);
            t.LoadImage(Convert.FromBase64String(_ICON));
            t.Apply();
            bgo.GetComponent<Image>().sprite = Sprite.Create(t,new Rect(0,0,16,16),new Vector2(0.5f,0.5f));
        }

        private void CreateDebugText()
        {
            var tgo = new GameObject("Log", typeof(Text));
            tgo.transform.SetParent(transform);
            var trt = tgo.GetComponent<RectTransform>();
            trt.anchorMax = Vector2.one;
            trt.anchorMin = Vector2.zero;
            //-right,-top
            trt.offsetMax = new Vector2(-10,-6);
            //left,bottom
            trt.offsetMin = new Vector2(10,10);
            _text = tgo.GetComponent<Text>();
#if UNITY_2022_1_OR_NEWER
            _text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            _text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
            _text.fontSize = 12;
            _text.color = new Color(1, 1, 1, 0.3f);
            _text.alignment = TextAnchor.UpperLeft;
            _text.raycastTarget = false;
            _text.text = _AUTHOR;
            tgo.SetActive(false);
        }
    }
}
#endif