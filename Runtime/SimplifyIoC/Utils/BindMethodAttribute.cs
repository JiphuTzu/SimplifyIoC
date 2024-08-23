using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Scripting;

namespace SimplifyIoC.Utils
{
    /// <summary>
    /// @usage:
    /// public class BindMethodAttributeTest : MonoBehaviour
    /// {
    ///     private void OnDestroy()
    ///     {
    ///         this.UnbindMethods();
    ///     }
    ///     private IEnumerator Start()
    ///     {
    ///         yield return new WaitForSeconds(1);
    ///         this.InvokeBind("Test1", "test1");
    ///         yield return new WaitForSeconds(1);
    ///         this.InvokeBind("Test1", 7);
    ///         yield return new WaitForSeconds(1);
    ///         this.InvokeBind("Test3", "test3");
    ///         yield return new WaitForSeconds(1);
    ///         this.InvokeBind("OnTest2", 27);
    ///     }
    /// 
    ///     [BindMethod("Test1","Test3")]
    ///     private void OnTest1(string param)
    ///     {
    ///         Debug.Log("OnTest1:::" + param);
    ///     }
    /// 
    ///     [BindMethod]
    ///     private void OnTest2(int n)
    ///     {
    ///         Debug.Log("OnTest2:::" + n);
    ///     }
    /// 
    ///     [BindMethod("OnTest2", order = -1)]
    ///     private void WithTest2(int n)
    ///     {
    ///         Debug.Log("WithTest2:::"+n);
    ///     }
    /// }
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class BindMethodAttribute : PreserveAttribute
    {
        public readonly object[] names;
        public int order = 0;
        internal MethodInfo method;

        /// <summary>
        /// 绑定方法到指定名称，一个方法可以同时绑定到多个名称，也可以多个方法绑定到一个名称
        /// </summary>
        /// <param name="names">为空时使用自己的方法名</param>
        public BindMethodAttribute(params object[] names)
        {
            this.names = names;
        }
    }

    public static class BindMethodExtension
    {
        private static readonly Dictionary<object, Dictionary<object, List<BindMethodAttribute>>> _methods = new();
        [Obsolete("use target.AddAttributeParser(this.GetBindMethodParser()).ParseAttributes() instead")]
        public static void BindMethods(this object target)
        {
            target.AddAttributeParser(target.GetBindMethodParser())
                .ParseAttributes();
        }

        public static Action<T,BindMethodAttribute, MethodInfo, Type> GetBindMethodParser<T>(this T target)
        {
            if (_methods.ContainsKey(target)) return null;
            var methods = new Dictionary<object, List<BindMethodAttribute>>();
            _methods.Add(target, methods);
            return MethodParser;
        }

        private static void MethodParser<T>(T target, BindMethodAttribute attribute, MethodInfo method, Type
            targetType)
        {
            var methods = _methods[target];

            var names = attribute.names;
            if (names.Length == 0) names = new object[] { method.Name };
            for (int i = 0; i < names.Length; i++)
            {
                var name = names[i];
                if (name == default || string.IsNullOrEmpty(name.ToString()))
                    continue;
                //
                attribute.method = method;
                //var sn = name.ToString().ToLower();
                if (methods.ContainsKey(name)) methods[name].Add(attribute);
                else methods.Add(name, new List<BindMethodAttribute> { attribute });
            }
        }
    
        public static void UnbindMethods(this object target)
        {
            var keys = new object[_methods.Count];
            _methods.Keys.CopyTo(keys, 0);
            var count = 0;
            //清除传入的指定对象或者key为空的对象
            foreach (var key in keys)
            {
                if (key != null && key != target) continue;
                ClearBinds(_methods[key]);
                _methods.Remove(key);
                count++;
            }
            //Debug.Log($"unbind methods for {count} target(s) and {_methods.Count} left");
        }
    

        public static void InvokeBind(this object target, object name, params object[] parameters)
        {
            if (target == null) return;
            //target.AddAttributeParser(target.GetBindMethodParser()).ParseAttributes();
            //Debug.Log($"Invoke Bind {name}");
            if (!_methods.TryGetValue(target, out var attributeMap))
            {
                Debug.Log($"{target} has no method bound to {name}()");
                return;
            }

            if (attributeMap == null || !attributeMap.TryGetValue(name,out var targetAttributes))
            {
                Debug.Log($"{target} has no method bound to {name}()");
                return;
            }

            targetAttributes.Sort((a, b) => a.order.CompareTo(a.order));
            var attributes = targetAttributes.ToArray();
            
            foreach (var attribute in attributes)
            {
                try
                {
                    attribute.method.Invoke(target, parameters);
                }
                catch (Exception e)
                {
                    Debug.Log($"invoke bind method {name} -> {attribute.method.Name}() error :: {e.Message}");
                }
            }
        }

        private static void ClearBinds(Dictionary<object, List<BindMethodAttribute>> attributeMap)
        {
            if (attributeMap == null) return;
            foreach (var pair in attributeMap)
            {
                pair.Value?.Clear();
            }

            attributeMap.Clear();
        }
    }
}