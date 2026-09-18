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

        //4.1：类型级"匹配结果"缓存。
        //原实现是 成员×解析器 双循环，每个成员对每个解析器各调一次 GetCustomAttribute，
        //一个 30 成员 / 3 解析器的 View ≈ 90 次反射调用，且每次实例化都重跑。
        //成员枚举结果与特性归属只取决于 (类型, 解析器特性类型, 成员种类, 查找标志)，
        //故按该四元组缓存"命中的成员 + 对应的特性实例"（平行数组，按下标配对）。
        private enum MemberKind
        {
            Field = 0,
            Property = 1,
            Method = 2
        }

        private sealed class Match
        {
            public static readonly Match Empty = new Match
            {
                members = Array.Empty<MemberInfo>(),
                attributes = Array.Empty<Attribute>()
            };

            public MemberInfo[] members;
            public Attribute[] attributes;
        }

        private static readonly Dictionary<(Type, Type, MemberKind, BindingFlags), Match> _matchCache = new();

        private static Match GetMatchCached(Type targetType, Type attributeType, BindingFlags flags, MemberKind kind)
        {
            var key = (targetType, attributeType, kind, flags);
            if (_matchCache.TryGetValue(key, out var cached)) return cached;

            //三分支返回 MethodInfo[]/FieldInfo[]/PropertyInfo[]，无目标类型时 switch 表达式
            //推导不出共同「最佳类型」（数组协变不参与推导），故显式标注 MemberInfo[]。
            MemberInfo[] all = kind switch
            {
                MemberKind.Method => GetMethodsCached(targetType, flags),
                MemberKind.Field => GetFieldsCached(targetType, flags),
                _ => GetPropertiesCached(targetType, flags)
            };

            var hitMembers = new List<MemberInfo>();
            var hitAttributes = new List<Attribute>();
            for (var i = 0; i < all.Length; i++)
            {
                //2.3.d 注释保留：getCustomAttribute 结果按 (类型,成员,特性类型) 恒定，
                //特性实例本身也被复用——这四个特性类的字段均为只读（BindMethodAttribute.method
                //例外，但其写入值 = (该类型,该方法) 唯一，同 key 内覆盖幂等）。
                var attribute = all[i].GetCustomAttribute(attributeType, true);
                if (attribute == null) continue;
                hitMembers.Add(all[i]);
                hitAttributes.Add(attribute);
            }

            var match = hitMembers.Count == 0
                ? Match.Empty
                : new Match { members = hitMembers.ToArray(), attributes = hitAttributes.ToArray() };
            _matchCache[key] = match;
            return match;
        }

        /// <summary>
        /// 2.3.d：委托优先；手工构造且未设 invoker 的 Parser 回落反射 Invoke。
        /// </summary>
        private static void InvokeParser(Parser parser, object target, Attribute attribute, MemberInfo member, Type targetType)
        {
            if (parser.invoker != null)
            {
                parser.invoker(target, attribute, member, targetType);
            }
            else
            {
                parser.parser.Invoke(target, new object[] { target, attribute, member, targetType });
            }
        }

        public static ReflectedTarget<TTarget> ParseMethods<TTarget>(this ReflectedTarget<TTarget> target, BindingFlags flags)  where TTarget:Component
        {
            var parsers = target.methodParsers;
            var aa = parsers.Count;
            if (aa == 0) return target;
            var targetType = target.targetType;
            for (var a = 0; a < aa; a++)
            {
                var attributeParser = parsers[a];
                var match = GetMatchCached(targetType, attributeParser.attributeType, flags, MemberKind.Method);
                var members = match.members;
                for (var i = 0; i < members.Length; i++)
                {
                    InvokeParser(attributeParser, target.target, match.attributes[i], members[i], targetType);
                }
            }

            return target;
        }

        public static ReflectedTarget<TTarget> ParseFields<TTarget>(this ReflectedTarget<TTarget> target, BindingFlags flags)  where TTarget:Component
        {
            var parsers = target.fieldParsers;
            var aa = parsers.Count;
            if (aa == 0) return target;
            var targetType = target.targetType;
            for (var a = 0; a < aa; a++)
            {
                var attributeParser = parsers[a];
                var match = GetMatchCached(targetType, attributeParser.attributeType, flags, MemberKind.Field);
                var members = match.members;
                for (var i = 0; i < members.Length; i++)
                {
                    InvokeParser(attributeParser, target.target, match.attributes[i], members[i], targetType);
                }
            }

            return target;
        }

        public static ReflectedTarget<TTarget> ParseProperties<TTarget>(this ReflectedTarget<TTarget> target, BindingFlags flags)  where TTarget:Component
        {
            var parsers = target.propertyParsers;
            var aa = parsers.Count;
            if (aa == 0) return target;
            var targetType = target.targetType;
            for (var a = 0; a < aa; a++)
            {
                var attributeParser = parsers[a];
                var match = GetMatchCached(targetType, attributeParser.attributeType, flags, MemberKind.Property);
                var members = match.members;
                for (var i = 0; i < members.Length; i++)
                {
                    InvokeParser(attributeParser, target.target, match.attributes[i], members[i], targetType);
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