using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.Events;
using UnityEngine.Scripting;

namespace SimplifyIoC.Utils
{
    [AttributeUsage(AttributeTargets.Method)]
    public class BindEventAttribute : PreserveAttribute
    {
        public readonly string eventName;
        public readonly string[] targetNames;

        /// <summary>
        /// bind the event(s) on target member(s) to this method
        /// </summary>
        /// <param name="eventName">The event name to bind</param>
        /// <param name="targetNames">The target field/property names (the event hosts) to bind. Names must resolve to UnityEvent holders.</param>
        public BindEventAttribute(string eventName, params string[] targetNames)
        {
            this.eventName = eventName;
            this.targetNames = targetNames;
        }
    }

    public static class BindEventExtension
    {
        //成员查找按 (Type, name) 缓存（targetNames 解析事件宿主用，两个查找的 FLAGS 相同，共用常量）
        private const BindingFlags _LOOKUP_FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                                  BindingFlags.IgnoreCase;
        private static readonly Dictionary<(Type, string), FieldInfo> _fieldLookup = new();
        private static readonly Dictionary<(Type, string), PropertyInfo> _propertyLookup = new();

        private static FieldInfo GetFieldCached(Type type, string name)
        {
            var key = (type, name);
            if (!_fieldLookup.TryGetValue(key, out var value))
            {
                value = type.GetField(name, _LOOKUP_FLAGS);
                _fieldLookup[key] = value;
            }
            return value;
        }

        private static PropertyInfo GetPropertyCached(Type type, string name)
        {
            var key = (type, name);
            if (!_propertyLookup.TryGetValue(key, out var value))
            {
                value = type.GetProperty(name, _LOOKUP_FLAGS);
                _propertyLookup[key] = value;
            }
            return value;
        }

        public static Action<T, BindEventAttribute, MethodInfo, Type> GetEventMethodParser<T>(this T target)
        {
            return MethodParser;
        }

        private static void MethodParser<T>(T target, BindEventAttribute attribute, MethodInfo method,
            Type targetType)
        {
            if (string.IsNullOrWhiteSpace(attribute.eventName)
                || attribute.targetNames.Length == 0) return;
            //多目标支持：每个 targetName 独立解析、互不影响（原来各分支命中即 return，
            //导致多按钮只有第一个生效；名字写错还会静默跳过后面全部目标）
            foreach (var targetName in attribute.targetNames)
            {
                var fieldInfo = GetFieldCached(targetType, targetName);
                if (fieldInfo != null)
                {
                    if (fieldInfo.FieldType.HasElementType)
                    {
                        //是数组
                        var arr = fieldInfo.GetValue(target) as object[];
                        TryAddListener(arr, attribute.eventName, target, method);
                        continue;
                    }
                    if (fieldInfo.FieldType.IsGenericType &&
                             fieldInfo.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        //2.3.e：原走 GetMethod("ToArray")+Invoke 反射，改为 IList 拷贝，零反射
                        var arr = MaterializeArray(fieldInfo.GetValue(target));
                        TryAddListener(arr,attribute.eventName, target, method);
                        continue;
                    }
                    TryAddListener(fieldInfo.GetValue(target), attribute.eventName, target, method);
                    continue;
                }
                var propertyInfo = GetPropertyCached(targetType, targetName);
                if (propertyInfo == null) continue;
                if (propertyInfo.PropertyType.HasElementType)
                {
                    var arr = propertyInfo.GetValue(target) as object[];
                    TryAddListener(arr, attribute.eventName, target, method);
                    continue;
                }

                if (propertyInfo.PropertyType.IsGenericType &&
                    propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    //2.3.e：同上，IList 拷贝替代反射 ToArray
                    var arr = MaterializeArray(propertyInfo.GetValue(target));
                    TryAddListener(arr, attribute.eventName, target, method);
                    continue;
                }
                TryAddListener(propertyInfo.GetValue(target), attribute.eventName, target, method);
            }
            
        }

        private static void TryAddListener(object[] eventSources, string eventName, object handlerTarget,
            MethodInfo method)
        {
            foreach (var eventSource in eventSources)
            {
                TryAddListener(eventSource, eventName, handlerTarget, method);
            }
        }

        private static void TryAddListener(object eventSource, string eventName, object handlerTarget, MethodInfo method)
        {
            var ue = GetEvent(eventSource, eventName);
            if (ue != null) AddListener(ue, handlerTarget, method);
        }

        /// <summary>
        /// 2.3.e：把 List&lt;T&gt; 物化为 object[]。原实现 GetMethod("ToArray") + Invoke（反射），
        /// 现走 IList.CopyTo，零反射；元素均为引用类型，无装箱。
        /// </summary>
        private static object[] MaterializeArray(object source)
        {
            if (source is object[] arr) return arr;
            var il = (IList)source;
            var res = new object[il.Count];
            il.CopyTo(res, 0);
            return res;
        }

        //2.3.e：AddListener 的 MethodInfo 按事件类型缓存（原每次调用都 GetMethod）
        private static readonly Dictionary<Type, MethodInfo> _addListenerCache = new();

        private static void AddListener(UnityEventBase ue, object target, MethodInfo method)
        {
            var type = ue.GetType();
            if (!_addListenerCache.TryGetValue(type, out var ual))
            {
                ual = type.GetMethod("AddListener", BindingFlags.Instance | BindingFlags.NonPublic);
                _addListenerCache[type] = ual;
            }
            ual?.Invoke(ue, new[] { target, method });
        }

        //2.3.e：事件成员查找按 (类型, 名称) 缓存；MemberInfo 为 null 表示确认无此成员
        private static readonly Dictionary<(Type, string), MemberInfo> _eventMemberCache = new();

        private static UnityEventBase GetEvent(object target, string name)
        {
            if (target == null) return null;
            var type = target.GetType();
            var key = (type, name);
            if (!_eventMemberCache.TryGetValue(key, out var member))
            {
                member = type.GetField(name, BindingFlags.Instance | BindingFlags.Public)
                         ?? (MemberInfo)type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                _eventMemberCache[key] = member;
            }
            switch (member)
            {
                case FieldInfo fieldInfo: return fieldInfo.GetValue(target) as UnityEventBase;
                case PropertyInfo propInfo: return propInfo.GetValue(target) as UnityEventBase;
                default: return null;
            }
        }
    }
}