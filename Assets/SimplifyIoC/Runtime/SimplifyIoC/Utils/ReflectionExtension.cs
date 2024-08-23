using System;
using System.Collections.Generic;
using System.Reflection;

namespace SimplifyIoC.Utils
{
    public static class ReflectionExtension
    {
        private struct Parser
        {
            public Type attributeType;
            public MethodInfo parser;
        }

        private static object _target;
        private static readonly List<Parser> _METHOD_PARSERS = new();
        private static readonly List<Parser> _FIELD_PARSERS = new();
        private static readonly List<Parser> _PROPERTY_PARSERS = new();

        public static TTarget AddAttributeParser<TTarget, TAttribute>(this TTarget target,
            Action<TTarget, TAttribute, MethodInfo, Type> parser) where TAttribute : Attribute
        {
            if (parser == null) return target;
            if (_target != (target as object))
            {
                _target.Clear();
                _target = target;
            }

            _METHOD_PARSERS.Add(new Parser()
            {
                attributeType = typeof(TAttribute),
                parser = parser.Method
            });
            return target;
        }

        public static TTarget AddAttributeParser<TTarget, TAttribute>(this TTarget target,
            Action<TTarget, TAttribute, FieldInfo, Type> parser) where TAttribute : Attribute
        {
            if(parser == null) return target;
            if (_target != (target as object))
            {
                _target = target;
                target.Clear();
            }

            _FIELD_PARSERS.Add(new Parser()
            {
                attributeType = typeof(TAttribute),
                parser = parser.Method
            });
            return target;
        }

        public static TTarget AddAttributeParser<TTarget, TAttribute>(this TTarget target,
            Action<TTarget, TAttribute, PropertyInfo, Type> parser) where TAttribute : Attribute
        {
            if(parser == null) return target;
            if (_target != (target as object))
            {
                _target = target;
                target.Clear();
            }

            _PROPERTY_PARSERS.Add(new Parser()
            {
                attributeType = typeof(TAttribute),
                parser = parser.Method
            });
            return target;
        }
        //BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase
        public static void ParseAttributes<TTarget>(this TTarget target,BindingFlags flags = BindingFlags.Instance 
            | BindingFlags.Public | BindingFlags.IgnoreCase)
        {
            if (_target != (target as object)) return;
            var type = target.GetType();
            target.ParseFields(type, flags)
                .ParseProperties(type, flags)
                .ParseMethods(type, flags)
                .Clear();
        }

        private static TTarget ParseMethods<TTarget>(this TTarget target, Type targetType, BindingFlags flags)
        {
            if (_METHOD_PARSERS.Count == 0) return target;
            var methods = targetType.GetMethods(flags);
            foreach (var method in methods)
            {
                foreach (var attributeParser in _METHOD_PARSERS)
                {
                    var attribute = method.GetCustomAttribute(attributeParser.attributeType, true);
                    if(attribute == null) continue;
                    attributeParser.parser.Invoke(target,new object[]{target, attribute, method, targetType});
                }
            }

            return target;
        }

        private static TTarget ParseFields<TTarget>(this TTarget target, Type targetType, BindingFlags flags)
        {
            if (_FIELD_PARSERS.Count == 0) return target;
            var fields = targetType.GetFields(flags);
            foreach (var field in fields)
            {
                foreach (var attributeParser in _FIELD_PARSERS)
                {
                    var attribute = field.GetCustomAttribute(attributeParser.attributeType, true);
                    if(attribute == null) continue;
                    attributeParser.parser.Invoke(target,new object[]{target, attribute, field, targetType});
                }
            }

            return target;
        }

        private static TTarget ParseProperties<TTarget>(this TTarget target, Type targetType, BindingFlags flags)
        {
            if (_PROPERTY_PARSERS.Count == 0) return target;
            var properties = targetType.GetProperties(flags);
            foreach (var property in properties)
            {
                foreach (var attributeParser in _PROPERTY_PARSERS)
                {
                    var attribute = property.GetCustomAttribute(attributeParser.attributeType, true);
                    if(attribute == null) continue;
                    attributeParser.parser.Invoke(target, new object[]{target, attribute, property, targetType});
                }
            }

            return target;
        }

        private static void Clear(this object target)
        {
            if(_target == null || _target != target) return;
            _METHOD_PARSERS.Clear();
            _FIELD_PARSERS.Clear();
            _PROPERTY_PARSERS.Clear();
            _target = null;
        }
    }
}