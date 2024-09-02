using System;
using System.Reflection;
using UnityEngine;
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
        FIELD = 1 << 0,
        PROPERTY = 1 << 1,
        METHOD = 1 << 2
    }

    public static class BindEventExtension
    {
        [Obsolete(
            "use target.AddFieldParser(this.GetEventFieldParser(usage)).AddPropertyParser(this.GetEventPropertyParser(usage)).AddMethodParser(target.GetEventMethodParser(usage)).ParseAttributes() instead")]
        public static void BindEvents(this Component target,
            BindUsage usage = BindUsage.FIELD | BindUsage.PROPERTY | BindUsage.METHOD)
        {
            target.AddAttributeParser(target.GetEventFieldParser(usage))
                .AddAttributeParser(target.GetEventPropertyParser(usage))
                .AddAttributeParser(target.GetEventMethodParser(usage))
                .ParseAttributes();
        }

        public static Action<T, BindEventAttribute, MethodInfo, Type> GetEventMethodParser<T>(this T target,
            BindUsage usage = BindUsage.METHOD)
        {
            return (usage & BindUsage.METHOD) == BindUsage.METHOD
                ? MethodParser
                : null;
        }

        public static Action<T, BindEventAttribute, FieldInfo, Type> GetEventFieldParser<T>(this T target,
            BindUsage usage = BindUsage.FIELD)
        {
            return (usage & BindUsage.FIELD) == BindUsage.FIELD
                ? FieldParser
                : null;
        }

        public static Action<T, BindEventAttribute, PropertyInfo, Type> GetEventPropertyParser<T>(this T target,
            BindUsage usage = BindUsage.PROPERTY)
        {
            return (usage & BindUsage.PROPERTY) == BindUsage.PROPERTY
                ? PropertyParser
                : null;
        }

        private static void MethodParser<T>(T target, BindEventAttribute attribute, MethodInfo method,
            Type targetType)
        {
            if (string.IsNullOrWhiteSpace(attribute.eventName)
                || attribute.targetNames.Length == 0) return;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.IgnoreCase;
            foreach (var targetName in attribute.targetNames)
            {
                var obj = targetType.GetField(targetName, flags)?.GetValue(target)
                          ?? targetType.GetProperty(targetName, flags)?.GetValue(target);
                var ue = GetEvent(obj, attribute.eventName);
                if (ue != null) AddListener(ue, target, method);
            }
            
        }

        private static void FieldParser<T>(T target, BindEventAttribute attribute, FieldInfo field, Type targetType)
        {
            if (string.IsNullOrWhiteSpace(attribute.eventName)) return;
            var targetNames = attribute.targetNames;
            if (targetNames.Length == 0) targetNames = new []{$"on{field.Name}"};
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.IgnoreCase;
            foreach (var targetName in targetNames)
            {
                var methodInfo = targetType.GetMethod(targetName, flags);
                if (methodInfo == null) continue;
                var ue = GetEvent(field.GetValue(target), attribute.eventName);
                if (ue != null) AddListener(ue, target, methodInfo);
            }
            
        }

        private static void PropertyParser<T>(T target, BindEventAttribute attribute, PropertyInfo property,
            Type targetType)
        {
            if (string.IsNullOrWhiteSpace(attribute.eventName)) return;
            var targetNames = attribute.targetNames;
            if (targetNames.Length == 0) targetNames = new []{$"on{property.Name}"};
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.IgnoreCase;
            foreach (var targetName in targetNames)
            {
                var methodInfo = targetType.GetMethod(targetName, flags);
                if (methodInfo == null) continue;
                var ue = GetEvent(property.GetValue(target), attribute.eventName);
                if (ue != null) AddListener(ue, target, methodInfo);
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