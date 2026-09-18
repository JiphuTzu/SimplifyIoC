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
        /// 2.3.e：InvokeBind 是运行期可重复调用的入口，原实现每次都 Sort + ToArray（分配 + 比较开销）。
        /// 排序结果在解析完成后不再变化，首次调用后缓存为快照。
        /// </summary>
        private sealed class BindGroup
        {
            public readonly List<BindMethodAttribute> attributes = new();
            public BindMethodAttribute[] sorted;
        }

        //4.4：原实现是 static Dictionary<Component, …>——以组件"实例"为 key，
        //唯一清理入口是用户手写 UnbindMethods()，框架不保证调用，组件销毁后条目永久残留。
        //改为 ConditionalWeakTable：组件一旦不可达，条目被 GC 连带清除，零钩子依赖。
        //（CWT 按引用标识比较，不受 UnityEngine.Object 重写的 ==/Equals 影响。）
        private static readonly ConditionalWeakTable<Component, Dictionary<object, BindGroup>> _methods = new();

        public static Action<T,BindMethodAttribute, MethodInfo, Type> GetBindMethodParser<T>(this T target) where T : Component
        {
            //4.4（自动解析接入后的优化）：这里**不再建条目**。取 parser 是每个组件实例的常规解析步骤，
            //若在此建表，没有 [BindMethod] 的组件也会常驻一个空字典。改为惰性——由 MethodParser
            //在第一次真正命中时创建（见下）。注册表因此只包含"确实用了 [BindMethod]"的组件。
            return MethodParser;
        }

        private static void MethodParser<T>(T target, BindMethodAttribute attribute, MethodInfo method, Type
            targetType) where T : Component
        {
            if (!_methods.TryGetValue(target, out var methods))
            {
                methods = new Dictionary<object, BindGroup>();
                _methods.Add(target, methods);
            }
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
                if (methods.TryGetValue(name, out var group))
                {
                    //4.4：同一 (目标, 名称) 下按实例去重——重复解析不会让 InvokeBind 调两次
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
            //4.4：CWT 无 Keys 可枚举，且已 GC 的组件条目自动消失，
            //原实现"key 为 null 时清空全部"的分支已无对象可清，退化为对 target 的定向清理。
            //用 ReferenceEquals 而非 GameObject 重载的 == ：View.OnDestroy 期间组件可能已是
            //"伪 null"（== null 为真），若在这里提前返回，条目就摘不掉了。
            if (ReferenceEquals(target, null)) return;
            if (_methods.TryGetValue(target, out var map)) ClearBinds(map);
            _methods.Remove(target);
            //Debug.Log($"unbind methods for {target}");
        }


        public static void InvokeBind(this Component target, object name, params object[] parameters)
        {
            if (target == null) return;
            //4.4：自动解析已在 View/Mediator.InitAttributes 接入，这里不再需要懒解析
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