/*
 * @class Inject
 *
 * [Inject] 特性标记一个属性注入点。
 *
 * 示例：

		[Inject]
		public IMyInterface myInstance{get;set;}

 * 注入点也可以指定名字：

		[Inject(SomeEnum.VALUE)]
		public IMyInterface myInstance{get;set;}

 *
 * @class Name
 *
 * 当构造函数或伪构造函数的参数标注 [Name] 时，注入器可以在满足同一接口的
 * 不同实现之间做出区分——构造注入因此也能像属性注入一样使用命名注入。
 *
 * @class Construct
 *
 * [Construct] 特性标记首选的构造函数。若省略，反射器会选择参数最少的构造函数。
 * 显然，只有一个构造函数时无需该标记。
 *
 * @class PostConstruct
 *
 * [PostConstruct] 特性把一个或多个方法标记为 PostConstructor（注入后回调）。
 * 它在注入完成后立即触发，因此可以像构造函数一样使用，
 * 并保证此时注入的依赖不会为空。PostConstructor 不接受参数。
 *
 * 可以为 PostConstructor 指定可选的优先级 int，
 * 多个 PostConstructor 将按可预期的顺序依次执行。
 */

using System;
using UnityEngine.Scripting;

[AttributeUsage(AttributeTargets.Property)]
public class Inject : PreserveAttribute
{
    public Inject() { }

    public Inject(object n)
    {
        name = n;
    }

    public object name { get; set; }
}

//Tag [Name] to perform named injection in constructors and pseudo-constructors
[AttributeUsage(AttributeTargets.Parameter)]
public class Name : Attribute
{
    public Name(object n)
    {
        name = n;
    }

    public object name { get; set; }
}

//Tag [Construct] to perform construction injection
[AttributeUsage(AttributeTargets.Constructor)]
public class Construct : Attribute
{
    public Construct() { }
}

//Tag [PostConstruct] to perform post-injection construction actions
[AttributeUsage(AttributeTargets.Method)]
public class PostConstruct : Attribute
{
    public PostConstruct() { }

    public PostConstruct(int p)
    {
        priority = p;
    }

    public int priority { get; set; }
}