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
            rt.methodParsers.Add(CreateParser<TTarget, TAttribute, MethodInfo>(typeof(TAttribute), parser));
            return rt;
        }
        public static ReflectedTarget<TTarget> AddAttributeParser<TTarget, TAttribute>(this ReflectedTarget<TTarget> target,
            Action<TTarget, TAttribute, MethodInfo, Type> parser) where TTarget:Component where TAttribute : Attribute
        {
            target.methodParsers.Add(CreateParser<TTarget, TAttribute, MethodInfo>(typeof(TAttribute), parser));
            return target;
        }

        public static ReflectedTarget<TTarget> AddAttributeParser<TTarget, TAttribute>(this TTarget target,
            Action<TTarget, TAttribute, FieldInfo, Type> parser) where TTarget:Component where TAttribute : Attribute
        {
            var rt = new ReflectedTarget<TTarget>(target);
            rt.fieldParsers.Add(CreateParser<TTarget, TAttribute, FieldInfo>(typeof(TAttribute), parser));
            return rt;
        }
        public static ReflectedTarget<TTarget> AddAttributeParser<TTarget, TAttribute>(this ReflectedTarget<TTarget> target,
            Action<TTarget, TAttribute, FieldInfo, Type> parser) where TTarget:Component where TAttribute : Attribute
        {
            target.fieldParsers.Add(CreateParser<TTarget, TAttribute, FieldInfo>(typeof(TAttribute), parser));
            return target;
        }

        public static ReflectedTarget<TTarget> AddAttributeParser<TTarget, TAttribute>(this TTarget target,
            Action<TTarget, TAttribute, PropertyInfo, Type> parser) where TTarget:Component where TAttribute : Attribute
        {
            var rt = new ReflectedTarget<TTarget>(target);
            rt.propertyParsers.Add(CreateParser<TTarget, TAttribute, PropertyInfo>(typeof(TAttribute), parser));
            return rt;
        }
        public static ReflectedTarget<TTarget> AddAttributeParser<TTarget, TAttribute>(this ReflectedTarget<TTarget> target,
            Action<TTarget, TAttribute, PropertyInfo, Type> parser) where TTarget:Component where TAttribute : Attribute
        {
            target.propertyParsers.Add(CreateParser<TTarget, TAttribute, PropertyInfo>(typeof(TAttribute), parser));
            return target;
        }

        /// <summary>
        /// 2.3.d：注册时保留用户委托本体（原先 parser.Method 拆成 MethodInfo 后委托即被丢弃，
        /// 调用期再用 MethodInfo.Invoke + object[] 装箱重建），包装成统一调用形状。
        /// 附带修复：原 Invoke(this=ReflectedTarget) 在用户传实例方法组时会因 this 类型不符抛异常，
        /// 保留原委托后其捕获的目标实例原样生效。纯闭包转换，无泛型反射，AOT 安全。
        /// </summary>
        private static Parser CreateParser<TTarget, TAttribute, TMember>(Type attributeType,
            Action<TTarget, TAttribute, TMember, Type> parser) where TAttribute : Attribute where TMember : MemberInfo
        {
            return new Parser
            {
                attributeType = attributeType,
                //保留 MethodInfo 字段（兼容可能的外部读取/手工构造 Parser），调用路径优先走 invoker
                parser = parser.Method,
                invoker = (t, a, m, type) => parser((TTarget)t, (TAttribute)a, (TMember)m, type)
            };
        }
        //BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase
        public static void ParseAttributes<TTarget>(this ReflectedTarget<TTarget> target,BindingFlags flags = BindingFlags.Instance
            | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase) where TTarget:Component
        {
            target.ParseFields(flags)
                .ParseProperties(flags)
                .ParseMethods(flags)
                .Clear();
        }

        //2.4：Type+flags → 成员表缓存。ParseAttributes 每组件实例都会调用，
        //同一类型的成员枚举结果不变，按 (Type, flags) 只枚举一次
        private static readonly Dictionary<(Type, BindingFlags), MethodInfo[]> _methodsTable = new();
        private static readonly Dictionary<(Type, BindingFlags), FieldInfo[]> _fieldsTable = new();
        private static readonly Dictionary<(Type, BindingFlags), PropertyInfo[]> _propertiesTable = new();

        private static MethodInfo[] GetMethodsCached(Type type, BindingFlags flags)
        {
            var key = (type, flags);
            if (!_methodsTable.TryGetValue(key, out var value))
            {
                value = type.GetMethods(flags);
                _methodsTable[key] = value;
            }
            return value;
        }

        private static FieldInfo[] GetFieldsCached(Type type, BindingFlags flags)
        {
            var key = (type, flags);
            if (!_fieldsTable.TryGetValue(key, out var value))
            {
                value = type.GetFields(flags);
                _fieldsTable[key] = value;
            }
            return value;
        }

        private static PropertyInfo[] GetPropertiesCached(Type type, BindingFlags flags)
        {
            var key = (type, flags);
            if (!_propertiesTable.TryGetValue(key, out var value))
            {
                value = type.GetProperties(flags);
                _propertiesTable[key] = value;
            }
            return value;
        }

        public static ReflectedTarget<TTarget> ParseMethods<TTarget>(this ReflectedTarget<TTarget> target, BindingFlags flags)  where TTarget:Component
        {
            if (target.methodParsers.Count == 0) return target;
            var methods = GetMethodsCached(target.targetType, flags);
            foreach (var method in methods)
            {
                foreach (var attributeParser in target.methodParsers)
                {
                    var attribute = method.GetCustomAttribute(attributeParser.attributeType, true);
                    if(attribute == null) continue;
                    //2.3.d：委托优先；手工构造且未设 invoker 的 Parser 回落反射 Invoke
                    if (attributeParser.invoker != null)
                    {
                        attributeParser.invoker(target.target, attribute, method, target.targetType);
                    }
                    else
                    {
                        attributeParser.parser.Invoke(target.target, new object[]{target.target, attribute, method, target.targetType});
                    }
                }
            }

            return target;
        }

        public static ReflectedTarget<TTarget> ParseFields<TTarget>(this ReflectedTarget<TTarget> target, BindingFlags flags)  where TTarget:Component
        {
            if (target.fieldParsers.Count == 0) return target;
            var fields = GetFieldsCached(target.targetType, flags);
            foreach (var field in fields)
            {
                foreach (var attributeParser in target.fieldParsers)
                {
                    var attribute = field.GetCustomAttribute(attributeParser.attributeType, true);
                    if(attribute == null) continue;
                    //2.3.d：委托优先；手工构造且未设 invoker 的 Parser 回落反射 Invoke
                    if (attributeParser.invoker != null)
                    {
                        attributeParser.invoker(target.target, attribute, field, target.targetType);
                    }
                    else
                    {
                        attributeParser.parser.Invoke(target.target, new object[]{target.target, attribute, field, target.targetType});
                    }
                }
            }
            
            return target;
        }

        public static ReflectedTarget<TTarget> ParseProperties<TTarget>(this ReflectedTarget<TTarget> target, BindingFlags flags)  where TTarget:Component
        {
            if (target.propertyParsers.Count == 0) return target;
            var properties = GetPropertiesCached(target.targetType, flags);
            foreach (var property in properties)
            {
                foreach (var attributeParser in target.propertyParsers)
                {
                    var attribute = property.GetCustomAttribute(attributeParser.attributeType, true);
                    if(attribute == null) continue;
                    //2.3.d：委托优先；手工构造且未设 invoker 的 Parser 回落反射 Invoke
                    if (attributeParser.invoker != null)
                    {
                        attributeParser.invoker(target.target, attribute, property, target.targetType);
                    }
                    else
                    {
                        attributeParser.parser.Invoke(target.target, new object[]{target.target, attribute, property, target.targetType});
                    }
                }
            }

            return target;
        }
    }
    public struct Parser
    {
        public Type attributeType;
        public MethodInfo parser;

        /// <summary>
        /// 2.3.d：注册时从用户委托包装而来的统一调用路径。
        /// 形状为 (target, attribute, memberInfo, type)；为 null 时调用方回落 parser.Invoke。
        /// 闭包转换，无 MakeGenericMethod / Expression / Emit，IL2CPP AOT 安全。
        /// </summary>
        public Action<object, Attribute, MemberInfo, Type> invoker;
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