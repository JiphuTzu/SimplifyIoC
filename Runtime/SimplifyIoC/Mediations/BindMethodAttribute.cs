using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Scripting;

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
    public object[] names;
    public int order = 0;

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
    private static Dictionary<MonoBehaviour, Dictionary<object, List<MethodInfo>>> _methods =
        new Dictionary<MonoBehaviour, Dictionary<object, List<MethodInfo>>>();

    public static void BindMethods(this MonoBehaviour target)
    {
        if (_methods.ContainsKey(target)) return;
        var methods = new Dictionary<object, List<MethodInfo>>();
        var type = target.GetType();
        var methodInfos = type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (var methodInfo in methodInfos)
        {
            var attribute = methodInfo.GetCustomAttribute<BindMethodAttribute>();
            if (attribute == null) continue;
            var names = attribute.names;
            if (names.Length == 0) names = new object[] {methodInfo.Name};
            for (int i = 0; i < names.Length; i++)
            {
                var name = names[i];
                if (name == default || string.IsNullOrEmpty(name.ToString()))
                    continue;
                //
                var sn = name.ToString().ToLower();
                if (methods.ContainsKey(sn)) methods[sn].Add(methodInfo);
                else methods.Add(sn, new List<MethodInfo> {methodInfo});
            }
        }

        //按照order排序
        foreach (var pair in methods)
        {
            pair.Value.Sort((a, b) =>
            {
                var aa = a.GetCustomAttribute<BindMethodAttribute>();
                var ba = b.GetCustomAttribute<BindMethodAttribute>();
                return aa.order.CompareTo(ba.order);
            });
        }

        _methods.Add(target, methods);
    }
    
    public static void UnbindMethods(this MonoBehaviour target)
    {
        var keys = new MonoBehaviour[_methods.Count];
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
        Debug.Log($"unbind methods for {count} target(s) and {_methods.Count} left");
    }
    

    public static void InvokeBind(this MonoBehaviour target, object name, params object[] parameters)
    {
        if (target == null) return;
        target.BindMethods();
        Debug.Log($"Invoke Bind {name}");
        if (!_methods.ContainsKey(target))
        {
            Debug.Log($"{target} has no method bound to {name}()");
            return;
        }

        var targetMethods = _methods[target];
        var sn = name.ToString().ToLower();
        if (targetMethods == null || !targetMethods.ContainsKey(sn))
        {
            Debug.Log($"{target} has no method bound to {name}()");
            return;
        }

        var methods = targetMethods[sn].ToArray();
        foreach (var method in methods)
        {
            try
            {
                method.Invoke(target, parameters);
            }
            catch (Exception e)
            {
                Debug.Log($"invoke bind method {name} -> {method.Name}() error :: {e.Message}");
            }
        }
    }

    private static void ClearBinds(Dictionary<object, List<MethodInfo>> targetMethods)
    {
        if (targetMethods == null) return;
        foreach (var pair in targetMethods)
        {
            pair.Value?.Clear();
        }

        targetMethods.Clear();
    }
}
