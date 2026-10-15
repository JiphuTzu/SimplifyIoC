/*
 * @interface SimplifyIoC.Framework.IManagedList
 *
 * Binding 各组成部分的公共接口，目前即 SemiBinding 或 Pool。
 * ManagedList 支持向其中添加或移除对象。
 */

namespace SimplifyIoC.Framework
{
    public interface IManagedList
    {
        /// Add a value to this List. 
        IManagedList Add(object value);

        /// Add a set of values to this List. 
        IManagedList Add(object[] list);

        /// Remove a value from this List. 
        IManagedList Remove(object value);

        /// Remove a set of values from this List. 
        IManagedList Remove(object[] list);

        /// Retrieve the value of this List.
        /// If the constraint is MANY, the value will be an Array.
        /// If the constraint is POOL, this becomes a synonym for GetInstance().
        object value { get; }
    }
}