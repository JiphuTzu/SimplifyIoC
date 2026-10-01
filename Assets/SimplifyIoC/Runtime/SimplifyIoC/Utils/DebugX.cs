/*
 * 使用方法：
 * PlayerSettings > OtherSettings > ScriptingDefineSymbols 中添加：
 * DEBUG_X
 * 或者
 * DEBUG_X_HIDE 初始时隐藏按钮，需要在左上角连续点击6次后，显示调试按钮
 * 或者
 * this.EnableDebug(true)
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SimplifyIoC.Utils;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class DebugXExtensions
{
    private static DebugX _instance;

    public static bool ShowInDebugger(this Transform child)
    {
        if (_instance == null) return false;
        _instance.AddToContainer(child);
        return true;
    }

    public static void EnableDebug(this MonoBehaviour target, bool value)
    {
        if (value && _instance == null)
        {
            CheckOrCreate();
        }

        if (_instance == null) return;
        _instance.available = value;
    }

    public static void AddToCrashReport(this ExceptionExtra extra)
    {
        if (_instance == null) return;
        _instance.extra = extra;
    }

    public static void AddExceptionSeverityKeywords(this ExceptionSeverity severity, params string[] keywords)
    {
        if (_instance == null || keywords.Length == 0) return;
        var single = (ExceptionSeverity)0;
        for (var i = 3; i >= 0; i--)
        {
            var s = (ExceptionSeverity)(1 << i);
            if ((severity & s) == s)
                single = s;
        }
        if(single == 0) return;
        foreach (var keyword in keywords)
        {
            _instance.AddSeverityKeyword(keyword, single);
        }
    }

    public static void RemoveFromExceptionSeverity(this string keyword)
    {
        if(_instance == null) return;
        _instance.RemoveSeverityKeyword(keyword);
    }

    public static void RemoveFromExceptionSeverity(this ExceptionSeverity severity)
    {
        if (_instance == null) return;
        _instance.RemoveSeverityKeyword(severity);
    }

    public static void AddToExceptionReport(this ExceptionSeverity severity, Action<ExceptionInfo> callback)
    {
        if (_instance == null) return;
        _instance.AddReportLevel(severity, callback);
    }

    public static void RemoveFromExceptionReport(this ExceptionSeverity severity, Action<ExceptionInfo> callback)
    {
        if (_instance == null) return;
        _instance.RemoveReportLevel(severity, callback);
    }

    public static string[] GetCrashReports(this MonoBehaviour target)
    {
        var emergencyDir = Path.Combine(Application.persistentDataPath, "CrashReports");
        if (!Directory.Exists(emergencyDir)) return Array.Empty<string>();
        var files = Directory.GetFiles(emergencyDir, "*.json");
        var reports = new string[files.Length];
        for (var i = 0; i < files.Length; i++)
        {
            reports[i] = File.ReadAllText(files[i]);
        }

        return reports;
    }

    public static void ClearCrashReports(this MonoBehaviour target)
    {
        var emergencyDir = Path.Combine(Application.persistentDataPath, "CrashReports");
        if (Directory.Exists(emergencyDir)) Directory.Delete(emergencyDir,true);
    }
    public static void EmergencySave(this ExceptionInfo exceptionInfo)
    {
        try
        {
            var emergencyDir = Path.Combine(Application.persistentDataPath, "CrashReports");
            if (!Directory.Exists(emergencyDir)) Directory.CreateDirectory(emergencyDir);
            //
            var saveFile = Path.Combine(emergencyDir, $"crash_report_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.json");
            if (File.Exists(saveFile)) return;
            var json = JsonUtility.ToJson(exceptionInfo);
            //
            File.WriteAllText(saveFile, json);
        }
        catch
        {
            // ignored
        }
    }
#if DEBUG_X || DEBUG_X_HIDE
    [RuntimeInitializeOnLoadMethod]
#endif
    private static void CheckOrCreate()
    {
        if (_instance) return;
        var go = new GameObject("DebugX", typeof(DebugX));
        _instance = go.GetComponent<DebugX>();
        Object.DontDestroyOnLoad(go);
    }
}

namespace SimplifyIoC.Utils
{
    [Serializable]
    public class ExceptionInfo
    {
        public string message;
        public string stackTrace;
        public string type;
        public ExceptionSeverity severity;
        public DateTime timestamp;
        public ExceptionExtra extra;
    }

    [Serializable]
    public class ExceptionExtra
    {
        public string unity = Application.unityVersion;
        public string platform = Enum.GetName(typeof(RuntimePlatform), Application.platform);
        public string device = SystemInfo.deviceModel;
        public string os = SystemInfo.operatingSystem;
        public string processor = SystemInfo.processorType;
        public int memory = SystemInfo.systemMemorySize;
        public string graphics = SystemInfo.graphicsDeviceName;
    }

    [Flags]
    public enum ExceptionSeverity
    {
        Low = 1 << 0, // 不影响功能
        Medium = 1 << 1, // 部分功能受影响
        High = 1 << 2, // 主要功能受影响
        Critical = 1 << 3 // 应用崩溃
    }

    [RequireComponent(typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))]
    public class DebugX : MonoBehaviour, ILogHandler
    {
        private bool _available = true;

        public bool available
        {
            get => _available;
            set
            {
                if (_available == value) return;
                _available = value;
                if (!_available) _container.gameObject.SetActive(false);
            }
        }

        public ExceptionExtra extra { private get; set; }
        private Text _text;
        private int _lines = 10;
        private const string _AUTHOR = "<color=#CCCCCC>\t\t[DebugX@JiphuTzu]</color>\n";

        private const string _ICON =
            "iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAAeUlEQVQ4EWNkAIKzQACiSQXGQMBIrmaYZUwwBrn0wBvAgux037Qz/5H5m2eZMMLE0NkwdSheACkCSYBomAaYGEwDOh/FAJgidBqXYSB1RBmAbiAynygDQOEAcwWyZhAbxQBYgIFoXJpgamAGjaZEBgZwwiE3R4KyMwAjrj6HJzm5/wAAAABJRU5ErkJggg==";

        private readonly List<string> _logs = new();
        private Transform _container;

        private readonly Dictionary<string, ExceptionSeverity> _severityKeywords = new()
        {
            { "OutOfMemory", ExceptionSeverity.Critical },
            { "StackOverflow", ExceptionSeverity.Critical },
            { "NullReference", ExceptionSeverity.Critical },
            { "MissingReference", ExceptionSeverity.Critical },
            { "ArgumentException", ExceptionSeverity.High },
            { "ArgumentNullException", ExceptionSeverity.High },
            { "InvalidOperation", ExceptionSeverity.High }
        };

        private Dictionary<ExceptionSeverity, Action<ExceptionInfo>> _reporters;
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
            // 2. 设置监听：用于监控和响应
            Application.logMessageReceived += OnUnityMessage;

            // 3. 设置任务异常处理
            TaskScheduler.UnobservedTaskException += HandleTaskExceptions;

            // 4. 设置.NET全局异常（作为最后防线）
            AppDomain.CurrentDomain.UnhandledException += HandleGlobalExceptions;
        }

        public void AddSeverityKeyword(string keyword, ExceptionSeverity severity)
        {
            if(!_severityKeywords.TryAdd(keyword, severity))
                _severityKeywords[keyword] = severity;
        }

        public void RemoveSeverityKeyword(string keyword)
        {
            _severityKeywords.Remove(keyword);
        }

        public void RemoveSeverityKeyword(ExceptionSeverity severity)
        {
            var keys = _severityKeywords.Keys.ToArray();
            foreach (var key in keys)
            {
                if((_severityKeywords[key] & severity) == _severityKeywords[key])
                    _severityKeywords.Remove(key);
            }
        }

        private void HandleGlobalExceptions(object sender, UnhandledExceptionEventArgs args)
        {
            if (args.ExceptionObject is not Exception exception) return;
            // 紧急保存
            ExceptionToInfo(exception.Message, exception.StackTrace, exception.GetType().Name, ExceptionSeverity.Critical)
                .EmergencySave();
        }

        private void HandleTaskExceptions(object sender, UnobservedTaskExceptionEventArgs args)
        {
            var exception = args.Exception;
            if (exception == null) return;
            // 紧急保存
            ExceptionToInfo(exception.Message, exception.StackTrace, exception.GetType().Name, ExceptionSeverity.Critical)
                .EmergencySave();
        }

        public void AddReportLevel(ExceptionSeverity severity, Action<ExceptionInfo> callback)
        {
            _reporters ??= new Dictionary<ExceptionSeverity, Action<ExceptionInfo>>();
            var values = new[]
                { ExceptionSeverity.Critical, ExceptionSeverity.Medium, ExceptionSeverity.High, ExceptionSeverity.Low };
            foreach (var v in values)
            {
                if ((v & severity) != v) continue;
                if (!_reporters.TryAdd(v, callback))
                    _reporters[v] += callback;
            }
        }

        public void RemoveReportLevel(ExceptionSeverity severity, Action<ExceptionInfo> callback)
        {
            if (_reporters == null) return;
            var keys = _reporters.Keys.ToArray();
            foreach (var key in keys)
            {
                if ((key & severity) == 0) continue;
                if (callback == null)
                {
                    _reporters.Remove(key);
                }
                else
                {
                    _reporters[key] -= callback;
                    if (_reporters[key] == null) _reporters.Remove(key);
                }
            }
        }

        private void OnUnityMessage(string message, string stackTrace, LogType logType)
        {
            if (logType != LogType.Exception) return;
            var severity = AnalyzeSeverity(message, stackTrace);
            var exceptionInfo = ExceptionToInfo(message, stackTrace, "UnknownException", severity);
            if (severity == ExceptionSeverity.Critical)
                exceptionInfo.EmergencySave();
            if (_reporters == null) return;
            foreach (var kv in _reporters)
            {
                if (severity != kv.Key) continue;
                kv.Value?.Invoke(exceptionInfo);
                break;
            }
        }

        private ExceptionInfo ExceptionToInfo(string message, string stackTrace, string type,
            ExceptionSeverity severity)
        {
            extra ??= new ExceptionExtra();
            var exceptionInfo = new ExceptionInfo
            {
                message = message,
                stackTrace = stackTrace,
                type = type,
                severity = severity,
                timestamp = DateTime.Now,
                extra = extra
            };
            return exceptionInfo;
        }

        public void AddToContainer(Transform child)
        {
            child.SetParent(_container, false);
        }

        private IEnumerator Start()
        {
            yield return null;
            _lines = (int)((GetComponent<RectTransform>().rect.height - 100) / (_text.fontSize * 1.12f));
        }

        private void OnDestroy()
        {
            Debug.unityLogger.logHandler = _defaultHandler;
            Application.logMessageReceived -= OnUnityMessage;
            TaskScheduler.UnobservedTaskException -= HandleTaskExceptions;
            AppDomain.CurrentDomain.UnhandledException -= HandleGlobalExceptions;
        }

        private void OnLogVisible()
        {
            if (!available) return;
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
            _container.gameObject.SetActive(!_container.gameObject.activeSelf);
        }

        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            _defaultHandler?.LogFormat(logType, context, format, args);
            if (available)
                Log(string.Format(format, args));
        }

        public void LogException(Exception exception, Object context)
        {
            _defaultHandler?.LogException(exception, context);
            if (available)
                Log(exception.ToString());
        }

        private ExceptionSeverity AnalyzeSeverity(string message, string stackTrace)
        {
            message = message.ToLower();
            stackTrace = stackTrace.ToLower();

            // 检查关键词
            foreach (var kv in _severityKeywords)
            {
                var lk = kv.Key.ToLower();
                if (message.Contains(lk) || stackTrace.Contains(lk))
                {
                    return kv.Value;
                }
            }
            // 根据异常类型判断
            return message.Contains("warning") ? ExceptionSeverity.Low : ExceptionSeverity.Medium;
        }

        private void Log(string log)
        {
            _logs.Insert(0, $"[{DateTime.Now:HH:mm:ss.fff}]{log}");
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
            image.sprite = Sprite.Create(t, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f));
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
            bgo.GetComponent<AlphaAdjuster>().onUpdate += UpdateText;
            //
            var tgo = new GameObject("Log", typeof(Text));
            tgo.transform.SetParent(bgo.transform);
            var trt = tgo.GetComponent<RectTransform>();
            trt.anchorMax = Vector2.one;
            trt.anchorMin = Vector2.zero;
            //-right,-top
            trt.offsetMax = new Vector2(-15, -36);
            //left,bottom
            trt.offsetMin = new Vector2(15, 15);
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

        private void UpdateText()
        {
            if (_logs.Count >= _lines)
            {
                _logs.RemoveRange(_lines - 1, _logs.Count + 1 - _lines);
            }

            _text.text = _AUTHOR + string.Join("\n", _logs);
        }

        [RequireComponent(typeof(Image), typeof(CanvasGroup))]
        private class AlphaAdjuster : MonoBehaviour
        {
            public event Action onUpdate;
            private CanvasGroup _cg;

            private void Start()
            {
                var brt = GetComponent<RectTransform>();
                brt.anchorMax = Vector2.one;
                brt.anchorMin = Vector2.zero;
                //-right,-top
                brt.offsetMax = new Vector2(-15, -15);
                //left,bottom
                brt.offsetMin = new Vector2(15, 15);
                //
                var image = GetComponent<Image>();
                image.color = new Color(0.3f, 0.3f, 0.3f, 1f);
                image.raycastTarget = false;
                //
                _cg = GetComponent<CanvasGroup>();
                //_cg.blocksRaycasts = false;
                //_cg.interactable = false;
                _cg.alpha = 0.3f;
            }

            private void Update()
            {
                if (Time.frameCount % 30 == 0) onUpdate?.Invoke();
                if (!Input.GetMouseButton(0)) return;
                _cg.alpha += Input.GetAxis("Mouse Y") * 0.05f;
                if (_cg.alpha < 0.1f) _cg.alpha = 0.1f;
            }
        }
    }
}