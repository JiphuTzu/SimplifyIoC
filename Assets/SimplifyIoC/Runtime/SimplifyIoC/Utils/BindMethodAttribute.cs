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
        /// <summary>
        /// 2.3.e：InvokeBind 是运行期可重复调用的入口，原实现每次都 Sort + ToArray（分配 + 比较开销）。
        /// 排序结果在解析完成后不再变化，首次调用后缓存为快照。
        /// </summary>
        private sealed class BindGroup
        {
            public readonly List<BindMethodAttribute> attributes = new();
            public BindMethodAttribute[] sorted;
        }

        private static readonly Dictionary<Component, Dictionary<object, BindGroup>> _methods = new();

        public static Action<T,BindMethodAttribute, MethodInfo, Type> GetBindMethodParser<T>(this T target) where T : Component
        {
            if (_methods.ContainsKey(target)) return null;
            var methods = new Dictionary<object, BindGroup>();
            _methods.Add(target, methods);
            return MethodParser;
        }

        private static void MethodParser<T>(T target, BindMethodAttribute attribute, MethodInfo method, Type
            targetType) where T : Component
        {
            var methods = _methods[target];

            var names = attribute.names;
            if (names.Length == 0) names = new object[] { method.Name };
            for (var i = 0; i < names.Length; i++)
            {
                var name = names[i];
                if (name == null || string.IsNullOrEmpty(name.ToString()))
                    continue;
                //
                attribute.method = method;
                //var sn = name.ToString().ToLower();
                if (methods.TryGetValue(name, out var group)) group.attributes.Add(attribute);
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
            var keys = new Component[_methods.Count];
            _methods.Keys.CopyTo(keys, 0);
            //var count = 0;
            //清除传入的指定对象或者key为空的对象
            foreach (var key in keys)
            {
                if (key != null && key != target) continue;
                ClearBinds(_methods[key]);
                _methods.Remove(key);
                //count++;
            }
            //Debug.Log($"unbind methods for {count} target(s) and {_methods.Count} left");
        }


        public static void InvokeBind(this Component target, object name, params object[] parameters)
        {
            if (target == null) return;
            //target.AddAttributeParser(target.GetBindMethodParser()).ParseAttributes();
            //Debug.Log($"Invoke Bind {name}");
            if (!_methods.TryGetValue(target, out var attributeMap))
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
                //P0#3 修复：原为 a.order.CompareTo(a.order)，恒为 0，order 失效
                group.attributes.Sort((a, b) => a.order.CompareTo(b.order));
                attributes = group.sorted = group.attributes.ToArray();
            }

            foreach (var attribute in attributes)
            {
                try
                {
                    //2.3.e 勘误：method.Invoke 无法安全委托化——绑定方法签名任意，
                    //CreateDelegate 需与委托类型精确匹配，按参数类型 MakeGenericMethod 遇到
                    //int 等值类型时泛型共享不安全（同 2.3.b 构造函数的结论），
                    //留待 v2 源生成器（preGenerated 钩子）在编译期生成
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