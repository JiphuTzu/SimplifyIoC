using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Scripting;

namespace SimplifyIoC.Utils
{
    [AttributeUsage(AttributeTargets.Method)]
    public class MainThreadAttribute : PreserveAttribute
    {
        public readonly int times;
        public readonly float interval;
        public MainThreadAttribute(int times = -1, float interval = 0)
        {
            this.times = times;
            this.interval = interval;
        }
    }
    public static class RunInMainThreadExtension
    {
        private static MainThreadRunner _runner;
        public static void RunInMainThread(this object target, Action callback, int times = -1, float interval = 0)
        {
            Initialize();
            _runner.Add(target,callback,times,interval);
        }

        public static void RemoveFromMainThread(this object target,Action callback)
        {
            if(_runner==null) return;
            _runner.Remove(target,callback);
        }

        public static Action<T, MainThreadAttribute, MethodInfo, Type> GetMainThreadParser<T>(this T target)
        {
            Initialize();
            return ParseMainThread;
        }
        private static void ParseMainThread<T>(T target, MainThreadAttribute attribute, MethodInfo method, Type
            targetType)
        {
            if (method.GetParameters().Length == 0)
            {
                _runner.Add(target,(Action)method.CreateDelegate(typeof(Action),target),attribute.times,attribute.interval);
            }
            else
            {
                Debug.Log("RunInMainThread");
            }
        }

        private static void Initialize()
        {
            if(_runner != null) return;
            var go = new GameObject("MainThreadRunner");
            _runner = go.AddComponent<MainThreadRunner>();
        }
        private class MainThreadRunner:MonoBehaviour
        {
            private class Record
            {
                public object target;
                public Action callback;
                public int times;
                public float interval;
                public float lastTime;
            }
            private readonly List<Record> _records = new();
            
            public void Add(object target,Action callback,int times,float interval)
            {
                var r = GetRecord(target, callback);
                if(r == null)
                {
                    _records.Add(new Record
                    {
                        target = target,
                        callback = callback,
                        times = times,
                        interval = interval,
                        lastTime = 0
                    });
                }
                else
                {
                    r.times = times;
                }
            }

            private Record GetRecord(object target, Action callback)
            {
                foreach (var r in _records)
                {
                    if (r.target == target && r.callback == callback)
                    {
                        return r;
                    }
                }

                return null;
            }
            

            public void Remove(object target,Action callback)
            {
                for (var i = _records.Count-1; i>=0; i--)
                {
                    if (_records[i].target == target && _records[i].callback == callback)
                    {
                        _records.RemoveAt(i);
                        break;
                    }
                }
            }
            private void Awake()
            {
                DontDestroyOnLoad(gameObject);
            }

            private void Update()
            {
                var rs = _records.ToArray();
                foreach (var r in rs)
                {
                    if (r.target != null && r.callback != null)
                    {
                        if(r.interval > 0.00001f && Time.time - r.lastTime < r.interval) continue;
                        try
                        {
                            r.callback.Invoke();
                        }
                        catch
                        {
                            // ignored
                        }
                        finally
                        {
                            r.lastTime = Time.time;
                            r.times--;
                        }
                    }

                    if (r.target == null || r.callback == null || r.times == 0)
                    {
                        _records.Remove(r);
                    }
                }
            }
        }
    }
}
