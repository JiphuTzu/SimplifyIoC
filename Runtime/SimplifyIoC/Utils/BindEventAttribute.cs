using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting;

namespace SimplifyIoC.Utils
{
    [AttributeUsage(AttributeTargets.Field|AttributeTargets.Property)]
    public class BindEventAttribute : PreserveAttribute
    {
        public readonly string eventName;
        public readonly string methodName;

        /// <summary>
        /// bind field event to some method
        /// </summary>
        /// <param name="eventName">The event name to bind</param>
        /// <param name="methodName">The method name to bind. If null，will bind the method named "on"+filedName</param>

        public BindEventAttribute(string eventName,string methodName = null)
        {
            this.eventName = eventName;
            this.methodName = methodName;
        }
    }
    [AttributeUsage(AttributeTargets.Method)]
    public class BindToEventAttribute : PreserveAttribute
    {
        public readonly string eventName;
        public readonly string fieldName;

        /// <summary>
        /// bind field event to some method
        /// </summary>
        /// <param name="eventName">the event name to bind</param>
        /// <param name="fieldName">the field name to bind</param>

        public BindToEventAttribute(string eventName,string fieldName)
        {
            this.eventName = eventName;
            this.fieldName = fieldName;
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
        public static void BindEvents(this Component target,BindUsage usage = BindUsage.FIELD | BindUsage.PROPERTY | BindUsage.METHOD)
        {
            if((usage & BindUsage.FIELD) == BindUsage.FIELD) BindFields(target);
            if((usage & BindUsage.PROPERTY) == BindUsage.PROPERTY) BindProperties(target);
            if((usage & BindUsage.METHOD) == BindUsage.METHOD) BindMethods(target);
        }

        private static void BindMethods(Component target)
        {
            var type = target.GetType();
            var methods = type.GetMethods(BindingFlags.Instance
                                          | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var method in methods)
            {
                var attribute = method.GetCustomAttribute<BindToEventAttribute>();
                if (attribute == null
                    || string.IsNullOrWhiteSpace(attribute.eventName)
                    || string.IsNullOrWhiteSpace(attribute.fieldName)) continue;
                var ue = GetEvent(type.GetField(attribute.fieldName)?.GetValue(target), attribute.eventName);
                if (ue == null) continue;
                AddListener(ue, target, method);
            }
        }

        private static void BindFields(Component target)
        {
            var type = target.GetType();
            var fields = type.GetFields(BindingFlags.Instance
                                        | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                var attribute = field.GetCustomAttribute<BindEventAttribute>();
                if (attribute == null || string.IsNullOrWhiteSpace(attribute.eventName)) continue;
                var mn = attribute.methodName;
                if (string.IsNullOrWhiteSpace(mn)) mn = $"on{field.Name}";
                var methodInfo = type.GetMethod(mn,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                if (methodInfo == null) continue;
                var ue = GetEvent(field.GetValue(target), attribute.eventName);
                if (ue == null) continue;
                AddListener(ue, target, methodInfo);
            }
        }

        private static void BindProperties(Component target)
        {
            var type = target.GetType();
            var fields = type.GetProperties(BindingFlags.Instance
                                            | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                var attribute = field.GetCustomAttribute<BindEventAttribute>();
                if (attribute == null || string.IsNullOrWhiteSpace(attribute.eventName)) continue;
                var mn = attribute.methodName;
                if (string.IsNullOrWhiteSpace(mn)) mn = $"on{field.Name}";
                var methodInfo = type.GetMethod(mn,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                if (methodInfo == null) continue;
                var ue = GetEvent(field.GetValue(target), attribute.eventName);
                if (ue == null) continue;
                AddListener(ue, target, methodInfo);
            }
        }

        private static void AddListener(UnityEventBase ue, Component target, MethodInfo method)
        {
            var type = ue.GetType();
            var ual = type.GetMethod("AddListener", BindingFlags.Instance | BindingFlags.NonPublic);
            ual?.Invoke(ue, new object[] { target, method });
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
