using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimplifyIoC.Utils
{
    public static class RunInMainThreadExtension
    {
        private static MainThreadRunner _runner;
        public static void RunInMainThread(this object target,Action callback,int times = -1)
        {
            Initialize();
            _runner.Add(target,callback,times);
        }

        public static void RemoveFromMainThread(this object target,Action callback)
        {
            if(_runner==null) return;
            _runner.Remove(target,callback);
        }

        private static void Initialize()
        {
            if(_runner!=null) return;
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
            }
            private readonly List<Record> _records = new List<Record>();
            
            public void Add(object target,Action callback,int times)
            {
                var r = GetRecord(target, callback);
                if(r == null)
                {
                    _records.Add(new Record
                    {
                        target = target,
                        callback = callback,
                        times = times
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
                        try
                        {
                            r.callback.Invoke();
                        }
                        catch
                        {
                            // ignored
                        }

                        r.times--;
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
