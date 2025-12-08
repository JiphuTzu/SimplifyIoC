/*
 * 使用方法：
 * PlayerSettings > OtherSettings > ScriptingDefineSymbols 中添加：
 * DEBUG_X
 * 或者
 * DEBUG_X_HIDE 初始时隐藏按钮，需要在左上角连续点击6次后，显示调试按钮
 */
#if DEBUG_X || DEBUG_X_HIDE
using System;
using System.Collections;
using System.Collections.Generic;
using SimplifyIoC.Utils;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
public static class DebugXExtensions
{
    private static  DebugX _instance;
    public static void ShowInDebugger(this Transform child)
    {
        if(_instance == null) return;
        _instance.AddToContainer(child);
    }
        
    [RuntimeInitializeOnLoadMethod]
    private static void CheckOrCreate()
    {
        if(_instance) return;
        var go = new GameObject("DebugX",typeof(DebugX));
        _instance = go.GetComponent<DebugX>();
        Object.DontDestroyOnLoad(go);
    }
}
namespace SimplifyIoC.Utils
{
    [RequireComponent(typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster))]
    public class DebugX : MonoBehaviour, ILogHandler
    {
        private Text _text;
        private int _lines = 10;
        private const string _AUTHOR = "<color=#CCCCCC>\t\t[DebugX@JiphuTzu]</color>\n";
        private const string _ICON =
            "iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAAeUlEQVQ4EWNkAIKzQACiSQXGQMBIrmaYZUwwBrn0wBvAgux037Qz/5H5m2eZMMLE0NkwdSheACkCSYBomAaYGEwDOh/FAJgidBqXYSB1RBmAbiAynygDQOEAcwWyZhAbxQBYgIFoXJpgamAGjaZEBgZwwiE3R4KyMwAjrj6HJzm5/wAAAABJRU5ErkJggg==";
        private readonly List<string> _logs = new();
        private Transform _container;
#if DEBUG_X_HIDE
        private bool _hideOnStart;
        private float _lastClickTime;
        private int _clickCount;
#endif

        private ILogHandler _defaultHandler;
        // Start is called before the first frame update
        private void Awake()
        {
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
            CreateDebugText();
            CreateDebugButton();
            
            _defaultHandler = Debug.unityLogger.logHandler;
            Debug.unityLogger.logHandler = this;
        }

        public void AddToContainer(Transform child)
        {
            child.SetParent(_container,false);
        }

        private IEnumerator Start()
        {
            yield return null;
            _lines = (int)((GetComponent<RectTransform>().rect.height-100) / (_text.fontSize*1.12f));
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
            _text.transform.parent.gameObject.SetActive(!_text.transform.parent.gameObject.activeSelf);
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
            brt.anchoredPosition = new Vector2(10, -10);
            brt.sizeDelta = new Vector2(80, 80);
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
            var bgo = new GameObject("Container", typeof(AlphaAdjuster));
            _container = bgo.transform;
            _container.SetParent(transform);
            //
            var tgo = new GameObject("Log", typeof(Text));
            tgo.transform.SetParent(bgo.transform);
            var trt = tgo.GetComponent<RectTransform>();
            trt.anchorMax = Vector2.one;
            trt.anchorMin = Vector2.zero;
            //-right,-top
            trt.offsetMax = new Vector2(-15,-36);
            //left,bottom
            trt.offsetMin = new Vector2(15,15);
            _text = tgo.GetComponent<Text>();
#if UNITY_2022_1_OR_NEWER
            _text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            _text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
            _text.fontSize = 34;
            _text.color = new Color(0.93f, 0.95f, 0.92f, 1f);
            _text.alignment = TextAnchor.UpperLeft;
            _text.fontStyle = FontStyle.Bold;
            _text.raycastTarget = false;
            _text.text = _AUTHOR;
            bgo.SetActive(false);
        }
        [RequireComponent(typeof(Image),typeof(CanvasGroup))]
        private class AlphaAdjuster : MonoBehaviour
        {
            private CanvasGroup _cg;
            private void Start()
            {
                var brt = GetComponent<RectTransform>();
                brt.anchorMax = Vector2.one;
                brt.anchorMin = Vector2.zero;
                //-right,-top
                brt.offsetMax = new Vector2(-15,-15);
                //left,bottom
                brt.offsetMin = new Vector2(15,15);
                //
                var image = GetComponent<Image>();
                image.color = new Color(0.3f,0.3f,0.3f,1f);
                image.raycastTarget = false;
                //
                _cg = GetComponent<CanvasGroup>();
                //_cg.blocksRaycasts = false;
                //_cg.interactable = false;
                _cg.alpha = 0.6f;
            }

            private void Update()
            {
                if(!Input.GetMouseButton(0)) return;
                _cg.alpha +=Input.GetAxis("Mouse Y") * 0.1f;
            }
        }
    }
}
#endif