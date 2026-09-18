using System;
using System.Collections.Generic;
using SimplifyIoC.Mediations;

namespace SimplifyIoC.Contexts
{
    /// <summary>
    /// 3.2：从 Context 抽出的"早期视图"缓存。
    ///
    /// 背景：若某个 View 在 Context 初始化完成之前就 Awake（on-stage 的 View 很常见），
    /// 它无法立刻被 Mediate，于是先缓存，等 Context 的 PostBindings 阶段统一补挂 Mediator。
    ///
    /// 原实现是一个 static 的 SemiBinding：所有 Context 共用一份缓存，
    /// 任意 Context 的 PostBindings 都会把全局缓存清空——跨 Context 互相抢视图，
    /// 同时 static 长期持有 View 引用造成跨场景泄漏。
    /// 现在改为每个 Context 持有一个实例，隔离性与释放语义都随实例走。
    /// </summary>
    public class ViewCache
    {
        private readonly List<View> _views = new List<View>();

        /// 当前缓存的 View 数量（测试与调试用）
        public int Count => _views.Count;

        public void Add(View view)
        {
            if (view != null)
                _views.Add(view);
        }

        /// <summary>
        /// 用传入的 mediationBinder 把这些"早到"的 View 补挂 Mediator，然后清空缓存。
        /// </summary>
        public void MediateWith(IMediationBinder binder)
        {
            if (binder == null)
                throw new Exception("MVCSContext cannot mediate views without a mediationBinder");

            ForEach(v => binder.Trigger(MediationEvent.Awake, v));
        }

        /// <summary>
        /// 4.7：对缓存中的每个 View 执行一次给定动作，然后清空缓存。
        /// 供"没有 mediationBinder"的 Context 走兜底路径（注入 + 声明式解析）使用，
        /// 不再只能依赖 Mediator 机制把缓存耗尽。
        /// </summary>
        public void ForEach(Action<View> action)
        {
            if (action == null) return;

            for (var i = 0; i < _views.Count; i++)
            {
                action(_views[i]);
            }

            _views.Clear();
        }

        /// <summary>Context 销毁时清空，避免残留对已销毁 View 的引用。</summary>
        public void Clear()
        {
            _views.Clear();
        }
    }
}
