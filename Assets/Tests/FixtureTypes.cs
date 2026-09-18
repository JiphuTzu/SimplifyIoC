// 行为快照测试的共用夹具类型。
// 注意：本文件只定义"形状"，不做任何断言；断言见各 *Tests.cs。
// 阶段 0 原则：钉住现有行为，不为修复而写。
using SimplifyIoC.Commands;
using SimplifyIoC.Pools;
using SimplifyIoC.Signals;

namespace SimplifyIoC.Tests
{
    // ---- 注入夹具 ----

    public interface ITestService
    {
        string Name { get; }
    }

    public class TestServiceImpl : ITestService
    {
        public static int InstanceCount;

        public TestServiceImpl()
        {
            InstanceCount++;
        }

        public string Name => "impl";
    }

    /// 持有 [Inject] 属性的普通消费者
    public class ServiceConsumer
    {
        [Inject] public ITestService service { get; set; }
    }

    /// 构造注入消费者（单一带参构造）
    public class ConsumerWithCtor
    {
        public ITestService service;

        public ConsumerWithCtor(ITestService s)
        {
            service = s;
        }
    }

    /// PostConstruct 消费者
    public class ConsumerWithPostConstruct
    {
        public bool postConstructed;

        [PostConstruct]
        public void OnInit()
        {
            postConstructed = true;
        }
    }

    /// 互相依赖的两个类型（用于循环依赖缺陷的记录，见 KnownDefectsTests）
    public class DepA
    {
        [Inject] public DepB b { get; set; }
    }

    public class DepB
    {
        [Inject] public DepA a { get; set; }
    }

    /// 构造函数抛异常的类型（用于工厂吞异常缺陷的记录）
    public class Boom
    {
        public Boom()
        {
            throw new System.InvalidOperationException("boom");
        }
    }

    // ---- Signal 夹具 ----

    /// 按框架约定的一级子类信号
    public class TestSignal : Signal { }

    /// 带一个载荷的信号
    public class TestValueSignal : Signal<int> { }

    /// 3.4.a：带一个引用类型载荷的信号（池化用例需要引用类型，见 PayloadVO 注释）
    public class PayloadSignal : Signal<PayloadVO> { }

    // ---- 3.4.a 调用级作用域夹具 ----

    /// 载荷载体。用引用类型而非 int：池化命令归还时 Command.Restore 会 Uninject，
    /// 而反射把值类型属性置 null 会抛异常，这是值类型载荷不适合池化路径的既有约束。
    public class PayloadVO
    {
        public int value;
    }

    /// 池化命令：每次取用都必须拿到本次派发的载荷
    public class PooledPayloadCommand : Command
    {
        public static readonly System.Collections.Generic.List<PayloadVO> Received = new();

        [Inject] public PayloadVO payload { get; set; }

        public override void Execute() => Received.Add(payload);
    }

    /// 嵌套派发：在 Execute 内再次派发同一信号，验证内外层载荷互不覆盖
    public class ReentrantValueCommand : Command
    {
        public static readonly System.Collections.Generic.List<int> Received = new();
        public static bool reentered;

        [Inject] public int value { get; set; }

        public override void Execute()
        {
            Received.Add(value);
            if (reentered) return;
            reentered = true;
            injectionBinder.GetInstance<TestValueSignal>().Dispatch(77);
        }
    }

    /// 请求一个载荷中并未声明的类型，用于钉住"不静默注入 null"
    public class StringConsumerCommand : Command
    {
        [Inject] public string text { get; set; }

        public override void Execute() { }
    }

    // ---- Command 夹具 ----

    public class TestCommand : Command
    {
        public static readonly System.Collections.Generic.List<string> ExecutionLog = new();
        public static int ExecutionCount;
        public static Command LastInstance;

        public override void Execute()
        {
            ExecutionCount++;
            ExecutionLog.Add(nameof(TestCommand));
            LastInstance = this;
        }
    }

    /// 接收信号载荷的命令
    public class ValueCommand : Command
    {
        public static readonly System.Collections.Generic.List<int> Received = new();

        [Inject] public int value { get; set; }

        public override void Execute()
        {
            Received.Add(value);
        }
    }

    public class SeqCommandA : Command
    {
        public override void Execute() => TestCommand.ExecutionLog.Add(nameof(SeqCommandA));
    }

    public class SeqCommandB : Command
    {
        public override void Execute() => TestCommand.ExecutionLog.Add(nameof(SeqCommandB));
    }

    // ---- Pool 夹具 ----

    public class PooledThing : IPoolable
    {
        public bool restored;

        public void Restore() => restored = true;
        public void Retain() { }
        public void Release() { }
        public bool retain { get; set; }
    }
}
