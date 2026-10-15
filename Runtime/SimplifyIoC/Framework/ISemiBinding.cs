/*
 * @interface SimplifyIoC.Framework.ISemiBinding
 *
 * 一个受管理的值列表。
 *
 * SemiBinding 是绑定框架中最小的原子单元，代表绑定的 key、value 或 name 三者之一。
 *
 * SemiBinding 存储某个值——系统 Type、列表或具体值。
 * 它还带有 ONE / MANY 约束：约束为 ONE 时，SemiBinding 只维护单个值而非列表。
 * 默认约束为：
 * - key   —— ONE
 * - value —— MANY
 * - name  —— ONE
 *
 * @see SimplifyIoC.Framework.BindingConstraintType
 */

namespace SimplifyIoC.Framework
{
    public interface ISemiBinding : IManagedList
    {
        /// Set or get the constraint. 
        BindingConstraintType constraint { get; set; }

        /// A secondary constraint that ensures that this SemiBinding will never contain multiple values equivalent to each other. 
        bool uniqueValues { get; set; }
    }
}