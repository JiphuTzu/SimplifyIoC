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
    public static class BindEventExtension
    {
        public static void BindEvents(this MonoBehaviour target)
        {
            BindFields(target);
            BindMethods(target);
        }

        private static void BindMethods(MonoBehaviour target)
        {
            var type = target.GetType();
            var methods = type.GetMethods(BindingFlags.Instance
                                          | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var method in methods)
            {
                var attribute = method.GetCustomAttribute<BindToEventAttribute>();
                if(attribute == null 
                   || string.IsNullOrWhiteSpace(attribute.eventName)
                   || string.IsNullOrWhiteSpace(attribute.fieldName)) continue;
                var ue = GetEvent(type.GetField(attribute.fieldName)?.GetValue(target), attribute.eventName);
                if (ue == null) continue;
                var delegateMethod = method.CreateDelegate(typeof(UnityAction),target);
                ue.AddListener((UnityAction)delegateMethod);
            }
        }

        private static void BindFields(MonoBehaviour target)
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
                var ue = GetEvent(field.GetValue(target),attribute.eventName);
                if (ue == null) continue;
                var delegateMethod = methodInfo.CreateDelegate(typeof(UnityAction),target);
                ue.AddListener((UnityAction)delegateMethod);
            }
        }

        private static UnityEvent GetEvent(object target, string name)
        {
            if(target == null) return null;
            var type = target.GetType();
            var fieldInfo = type.GetField(name,BindingFlags.Instance | BindingFlags.Public);
            if (fieldInfo != null)
            {
                if (fieldInfo.GetValue(target) is UnityEvent ue)
                    return ue;
            }else{
                var propInfo = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                if(propInfo!=null && propInfo.GetValue(target) is UnityEvent ue)
                    return ue;
            }

            return null;
        }
    }
}
