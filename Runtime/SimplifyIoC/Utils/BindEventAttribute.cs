using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.Events;
using UnityEngine.Scripting;

namespace SimplifyIoC.Utils
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class BindEventAttribute : PreserveAttribute
    {
        public readonly string eventName;
        public readonly string[] targetNames;

        /// <summary>
        /// bind field event to some method
        /// </summary>
        /// <param name="eventName">The event name to bind</param>
        /// <param name="targetNames">The target field/property/method names to bind. If []，will bind the method named "on"+filedName</param>
        public BindEventAttribute(string eventName, params string[] targetNames)
        {
            this.eventName = eventName;
            this.targetNames = targetNames;
        }
    }

    [Flags]
    public enum BindUsage : byte
    {
        Field = 1 << 0,
        Property = 1 << 1,
        Method = 1 << 2
    }

    public static class BindEventExtension
    {
        public static Action<T, BindEventAttribute, MethodInfo, Type> GetEventMethodParser<T>(this T target,
            BindUsage usage = BindUsage.Method)
        {
            return (usage & BindUsage.Method) == BindUsage.Method
                ? MethodParser
                : null;
        }

        public static Action<T, BindEventAttribute, FieldInfo, Type> GetEventFieldParser<T>(this T target,
            BindUsage usage = BindUsage.Field)
        {
            return (usage & BindUsage.Field) == BindUsage.Field
                ? FieldParser
                : null;
        }

        public static Action<T, BindEventAttribute, PropertyInfo, Type> GetEventPropertyParser<T>(this T target,
            BindUsage usage = BindUsage.Property)
        {
            return (usage & BindUsage.Property) == BindUsage.Property
                ? PropertyParser
                : null;
        }

        private static void MethodParser<T>(T target, BindEventAttribute attribute, MethodInfo method,
            Type targetType)
        {
            if (string.IsNullOrWhiteSpace(attribute.eventName)
                || attribute.targetNames.Length == 0) return;
            const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.IgnoreCase;
            foreach (var targetName in attribute.targetNames)
            {
                var fieldInfo = targetType.GetField(targetName, FLAGS);
                if (fieldInfo != null)
                {
                    if (fieldInfo.FieldType.HasElementType)
                    {
                        //是数组
                        var arr = fieldInfo.GetValue(target) as object[];
                        TryAddListener(arr, attribute.eventName, target, method);
                        return;
                    }
                    if (fieldInfo.FieldType.IsGenericType &&
                             fieldInfo.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        var toArray = fieldInfo.FieldType.GetMethod("ToArray");
                        // ReSharper disable once PossibleNullReferenceException
                        var arr = toArray.Invoke(fieldInfo.GetValue(target), new object[] { }) as object[];
                        TryAddListener(arr,attribute.eventName, target, method);
                        return;
                    }
                    TryAddListener(fieldInfo.GetValue(target), attribute.eventName, target, method);
                    return;
                }
                var propertyInfo = targetType.GetProperty(targetName, FLAGS);
                if (propertyInfo == null) return;
                if (propertyInfo.PropertyType.HasElementType)
                {
                    var arr = propertyInfo.GetValue(target) as object[];
                    TryAddListener(arr, attribute.eventName, target, method);
                    return;
                }

                if (propertyInfo.PropertyType.IsGenericType &&
                    propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var toArray = propertyInfo.PropertyType.GetMethod("ToArray");
                    // ReSharper disable once PossibleNullReferenceException
                    var arr = toArray.Invoke(propertyInfo.GetValue(target), new object[] { }) as object[];
                    TryAddListener(arr, attribute.eventName, target, method);
                    return;
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

        // private static bool TryToArray(object obj, out object[] arr)
        // {
        //     if(Array.)
        // }

        private static void FieldParser<T>(T target, BindEventAttribute attribute, FieldInfo field, Type targetType)
        {
            if (string.IsNullOrWhiteSpace(attribute.eventName)) return;
            var targetNames = attribute.targetNames;
            if (targetNames.Length == 0) targetNames = new []{$"on{field.Name}"};
            const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.IgnoreCase;
            foreach (var targetName in targetNames)
            {
                var methodInfo = targetType.GetMethod(targetName, FLAGS);
                if (methodInfo == null) continue;
                if (field.FieldType.HasElementType)
                {
                    //是数组
                    var arr = field.GetValue(target) as object[];
                    TryAddListener(arr, attribute.eventName, target, methodInfo);
                } 
                else if (field.FieldType.IsGenericType &&
                    field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var toArray = field.FieldType.GetMethod("ToArray");
                    // ReSharper disable once PossibleNullReferenceException
                    var arr = toArray.Invoke(field.GetValue(target), new object[] { }) as object[];
                    TryAddListener(arr,attribute.eventName, target, methodInfo);
                } 
                else
                {
                    TryAddListener(field.GetValue(target), attribute.eventName, target, methodInfo);
                }
            }
            
        }

        private static void PropertyParser<T>(T target, BindEventAttribute attribute, PropertyInfo property,
            Type targetType)
        {
            if (string.IsNullOrWhiteSpace(attribute.eventName)) return;
            var targetNames = attribute.targetNames;
            if (targetNames.Length == 0) targetNames = new []{$"on{property.Name}"};
            const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.IgnoreCase;
            foreach (var targetName in targetNames)
            {
                var methodInfo = targetType.GetMethod(targetName, FLAGS);
                if (methodInfo == null) continue;
                if (property.PropertyType.HasElementType)
                {
                    var arr = property.GetValue(target) as object[];
                    TryAddListener(arr, attribute.eventName, target, methodInfo);
                }
                else if (property.PropertyType.IsGenericType &&
                         property.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var toArray = property.PropertyType.GetMethod("ToArray");
                    // ReSharper disable once PossibleNullReferenceException
                    var arr = toArray.Invoke(property.GetValue(target), new object[] { }) as object[];
                    TryAddListener(arr, attribute.eventName, target, methodInfo);
                }
                else
                {
                    TryAddListener(property.GetValue(target), attribute.eventName, target, methodInfo);
                }
            }
        }

        private static void AddListener(UnityEventBase ue, object target, MethodInfo method)
        {
            var type = ue.GetType();
            var ual = type.GetMethod("AddListener", BindingFlags.Instance | BindingFlags.NonPublic);
            ual?.Invoke(ue, new[] { target, method });
        }

        private static UnityEventBase GetEvent(object target, string name)
        {
            if (target == null) return null;
            var type = target.GetType();
            var fieldInfo = type.GetField(name, BindingFlags.Instance | BindingFlags.Public);
            if (fieldInfo != null)
            {
                if (fieldInfo.GetValue(target) is UnityEventBase ue)
                    return ue;
            }
            else
            {
                var propInfo = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                if (propInfo != null && propInfo.GetValue(target) is UnityEventBase ue)
                    return ue;
            }

            return null;
        }
    }
}