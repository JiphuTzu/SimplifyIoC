using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SimplifyIoC.Utils
{
    public static class ReflectionExtension
    {
        public static ReflectedTarget<TTarget> AddAttributeParser<TTarget, TAttribute>(this TTarget target,
            Action<TTarget, TAttribute, MethodInfo, Type> parser) where TTarget:Component where TAttribute : Attribute
        {
            var rt = new ReflectedTarget<TTarget>(target);
            rt.methodParsers.Add(new Parser()
            {
                attributeType = typeof(TAttribute),
                parser = parser.Method
            });
            return rt;
        }
        public static ReflectedTarget<TTarget> AddAttributeParser<TTarget, TAttribute>(this ReflectedTarget<TTarget> target,
            Action<TTarget, TAttribute, MethodInfo, Type> parser) where TTarget:Component where TAttribute : Attribute
        {
            target.methodParsers.Add(new Parser()
            {
                attributeType = typeof(TAttribute),
                parser = parser.Method
            });
            return target;
        }

        public static ReflectedTarget<TTarget> AddAttributeParser<TTarget, TAttribute>(this TTarget target,
            Action<TTarget, TAttribute, FieldInfo, Type> parser) where TTarget:Component where TAttribute : Attribute
        {
            var rt = new ReflectedTarget<TTarget>(target);
            rt.fieldParsers.Add(new Parser()
            {
                attributeType = typeof(TAttribute),
                parser = parser.Method
            });
            return rt;
        }
        public static ReflectedTarget<TTarget> AddAttributeParser<TTarget, TAttribute>(this ReflectedTarget<TTarget> target,
            Action<TTarget, TAttribute, FieldInfo, Type> parser) where TTarget:Component where TAttribute : Attribute
        {
            target.fieldParsers.Add(new Parser()
            {
                attributeType = typeof(TAttribute),
                parser = parser.Method
            });
            return target;
        }

        public static ReflectedTarget<TTarget> AddAttributeParser<TTarget, TAttribute>(this TTarget target,
            Action<TTarget, TAttribute, PropertyInfo, Type> parser) where TTarget:Component where TAttribute : Attribute
        {
            var rt = new ReflectedTarget<TTarget>(target);
            rt.propertyParsers.Add(new Parser()
            {
                attributeType = typeof(TAttribute),
                parser = parser.Method
            });
            return rt;
        }
        public static ReflectedTarget<TTarget> AddAttributeParser<TTarget, TAttribute>(this ReflectedTarget<TTarget> target,
            Action<TTarget, TAttribute, PropertyInfo, Type> parser) where TTarget:Component where TAttribute : Attribute
        {
            target.propertyParsers.Add(new Parser()
            {
                attributeType = typeof(TAttribute),
                parser = parser.Method
            });
            return target;
        }
        //BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase
        public static void ParseAttributes<TTarget>(this ReflectedTarget<TTarget> target,BindingFlags flags = BindingFlags.Instance 
            | BindingFlags.Public | BindingFlags.IgnoreCase) where TTarget:Component
        {
            target.ParseFields(flags)
                .ParseProperties(flags)
                .ParseMethods(flags)
                .Clear();
        }

        public static ReflectedTarget<TTarget> ParseMethods<TTarget>(this ReflectedTarget<TTarget> target, BindingFlags flags)  where TTarget:Component
        {
            if (target.methodParsers.Count == 0) return target;
            var methods = target.targetType.GetMethods(flags);
            foreach (var method in methods)
            {
                foreach (var attributeParser in target.methodParsers)
                {
                    var attribute = method.GetCustomAttribute(attributeParser.attributeType, true);
                    if(attribute == null) continue;
                    attributeParser.parser.Invoke(target,new object[]{target.target, attribute, method, target.targetType});
                }
            }

            return target;
        }

        public static ReflectedTarget<TTarget> ParseFields<TTarget>(this ReflectedTarget<TTarget> target, BindingFlags flags)  where TTarget:Component
        {
            if (target.fieldParsers.Count == 0) return target;
            var fields = target.targetType.GetFields(flags);
            foreach (var field in fields)
            {
                foreach (var attributeParser in target.fieldParsers)
                {
                    var attribute = field.GetCustomAttribute(attributeParser.attributeType, true);
                    if(attribute == null) continue;
                    attributeParser.parser.Invoke(target,new object[]{target.target, attribute, field, target.targetType});
                }
            }
            
            return target;
        }

        public static ReflectedTarget<TTarget> ParseProperties<TTarget>(this ReflectedTarget<TTarget> target, BindingFlags flags)  where TTarget:Component
        {
            if (target.propertyParsers.Count == 0) return target;
            var properties = target.targetType.GetProperties(flags);
            foreach (var property in properties)
            {
                foreach (var attributeParser in target.propertyParsers)
                {
                    var attribute = property.GetCustomAttribute(attributeParser.attributeType, true);
                    if(attribute == null) continue;
                    attributeParser.parser.Invoke(target, new object[]{target.target, attribute, property, target.targetType});
                }
            }

            return target;
        }
    }
    public struct Parser
    {
        public Type attributeType;
        public MethodInfo parser;
    }

    public class ReflectedTarget<T> where T :Component
    {
        public readonly T target;
        public readonly Type targetType;
        public readonly List<Parser> methodParsers = new();
        public readonly List<Parser> fieldParsers = new();
        public readonly List<Parser> propertyParsers = new();
        public ReflectedTarget(T target)
        {
            this.target = target;
            targetType = target.GetType();
        }

        public void Clear()
        {
            methodParsers.Clear();
            fieldParsers.Clear();
            propertyParsers.Clear();
        }
    }
}