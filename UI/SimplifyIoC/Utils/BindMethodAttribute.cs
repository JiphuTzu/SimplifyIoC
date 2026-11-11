using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
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
        /// <summary>
        /// 排序结果在解析完成后不再变化，首次调用后缓存为快照，避免每次 InvokeBind 重复 Sort + ToArray。
        /// </summary>
        private sealed class BindGroup
        {
            public readonly List<BindMethodAttribute> attributes = new();
            public BindMethodAttribute[] sorted;
        }

        //以 ConditionalWeakTable 按组件实例持有：组件不可达时条目被 GC 连带清除，无需清理钩子；
        //按引用标识比较，不受 UnityEngine.Object 重写的 ==/Equals 影响。
        private static readonly ConditionalWeakTable<Component, Dictionary<object, BindGroup>> _METHODS = new();

        public static Action<T,BindMethodAttribute, MethodInfo, Type> GetBindMethodParser<T>(this T target) where T : Component
        {
            //这里不建条目：没有 [BindMethod] 的组件不必常驻空字典，条目由 MethodParser 按需惰性创建。
            return MethodParser;
        }

        private static void MethodParser<T>(T target, BindMethodAttribute attribute, MethodInfo method, Type
            targetType) where T : Component
        {
            if (!_METHODS.TryGetValue(target, out var methods))
            {
                methods = new Dictionary<object, BindGroup>();
                _METHODS.Add(target, methods);
            }
            var names = attribute.names;
            if (names.Length == 0) names = new object[] { method.Name };
            for (var i = 0; i < names.Length; i++)
            {
                var name = names[i];
                if (name == null || string.IsNullOrEmpty(name.ToString()))
                    continue;
                attribute.method = method;
                if (methods.TryGetValue(name, out var group))
                {
                    //同一 (目标, 名称) 下按实例去重——重复解析不会让 InvokeBind 调两次
                    if (group.attributes.Contains(attribute)) continue;
                    group.attributes.Add(attribute);
                    group.sorted = null;
                }
                else
                {
                    group = new BindGroup();
                    group.attributes.Add(attribute);
                    methods.Add(name, group);
                }
            }
        }

        public static void UnbindMethods(this Component target)
        {
            //CWT 无 Keys 可枚举，且已 GC 的组件条目自动消失，这里对 target 做定向清理。
            //用 ReferenceEquals 而非 GameObject 重载的 == ：View.OnDestroy 期间组件可能已是
            //"伪 null"（== null 为真），若在这里提前返回，条目就摘不掉了。
            if (ReferenceEquals(target, null)) return;
            if (_METHODS.TryGetValue(target, out var map)) ClearBinds(map);
            _METHODS.Remove(target);
        }


        public static void InvokeBind(this Component target, object name, params object[] parameters)
        {
            if (target == null) return;
            //自动解析由 View/Mediator.InitAttributes 完成，这里直接查表。
            if (!_METHODS.TryGetValue(target, out var attributeMap))
            {
                Debug.Log($"{target} has no method bound to {name}()");
                return;
            }

            if (attributeMap == null || !attributeMap.TryGetValue(name, out var group))
            {
                Debug.Log($"{target} has no method bound to {name}()");
                return;
            }

            var attributes = group.sorted;
            if (attributes == null)
            {
                group.attributes.Sort((a, b) => a.order.CompareTo(b.order));
                attributes = group.sorted = group.attributes.ToArray();
            }

            foreach (var attribute in attributes)
            {
                try
                {
                    //绑定方法签名任意，CreateDelegate 需与委托类型精确匹配，值类型下
                    //MakeGenericMethod 泛型共享不安全，故用 method.Invoke 反射调用。
                    attribute.method.Invoke(target, parameters);
                }
                catch (Exception e)
                {
                    Debug.Log($"invoke bind method {name} -> {attribute.method.Name}() error :: {e.Message}");
                }
            }
        }

        private static void ClearBinds(Dictionary<object, BindGroup> attributeMap)
        {
            if (attributeMap == null) return;
            foreach (var pair in attributeMap)
            {
                pair.Value?.attributes.Clear();
            }

            attributeMap.Clear();
        }
    }
}