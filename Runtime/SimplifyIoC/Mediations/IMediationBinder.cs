/*
 * @interface SimplifyIoC.Mediations.IMediationBinder
 *
 * 把 View 映射到 Mediator 的 Binder 接口。
 *
 * 广义上说，mediation（中介）就是用一个东西隔开并连接另外两个东西。
 * 本框架中的 mediation 指的是把 View 与应用的其他部分隔开。
 * 这样做的理由非常实际：View 通常有两种形态——
 * 一种是“组件”，稳定、会被放进各种不同的场景反复使用；
 * 一种是“工作 View”，随最后关头的设计变动快速变化。
 * 无论哪种，Mediator 都能把 View 与周围的应用隔离开，反之亦然：
 * 组件因此更易复用，工作 View 的混乱也被结构性地控制在局部。
 *
 * MediationBinder 做的事很简单：把两个（或多个）类绑定在一起。
 * key 是 View 类，value 是一个或多个 Mediator。
 * View 出现时，对应的 Mediator 会 join 上来，为 View 与应用之间做缓冲。
 * View 不需要知道 Mediator 的存在，也不知道应用在它之外还有什么；
 * 它只声明自己的 API，然后等待指令。
 * 而 Mediator 恰恰相反，它允许相当了解 View 与应用：它可以被注入、
 * 可以监听和派发信号。它应当保持“薄”——只保留完成中介所需的最低限度的知识，
 * 把逻辑、数据存储和 View 行为留给其他类。
 *
 * 在 Unity 中，View 是挂在 GameObject 上的 MonoBehaviour，
 * Mediator 同样是 MonoBehaviour，因此能访问 MonoBehaviour 的一切能力。
 * 最佳实践是：Unity 中只把 View 挂到 GameObject 上。
 *
 * 最后两点提醒。
 * 第一：View 也可以被注入（例如注入局部 Dispatcher 或配置文件），
 * 但强烈建议不要注入全 Context 共享的 Dispatcher 或任何超出 View
 * 关注范围的类，否则 mediation 的意义就名存实亡。
 * 第二：经验表明，划分“什么属于 Mediator、什么属于 View”是这套系统最难的部分。
 * 建议坚持“薄”类原则：除了完成中介所必需的，什么都别放进 Mediator。
 */

using SimplifyIoC.Framework;

namespace SimplifyIoC.Mediations
{
    public interface IMediationBinder : IBinder
    {
        /// An event that just happened, and the View it happened to.
        /// If the event was Awake, it will trigger creation of a mapped Mediator.
        void Trigger(MediationEvent evt, View view);

        /// Recast binding as IMediationBinding.
        new IMediationBinding Bind<T>();

        /// Porcelain for Bind<T> providing a little extra clarity and security.
        IMediationBinding BindView<T>();
    }
}