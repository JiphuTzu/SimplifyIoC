/*
 * @interface SimplifyIoC.Framework.IBinding
 *
 * 把 key SemiBinding 绑定到 value SemiBinding。
 *
 * Binding 是开发者日常打交道最多的最小单元。
 *
 * 一个绑定由两个必需部分和一个可选部分（SemiBinding）组成：
 * - key：客户端为解锁一个值而提供的 Type 或值；
 * - value：与 key 绑定、凭 key 释放的一个或多个对象；
 * - name：可选的区分符，允许客户端区分同一 Type 的多个绑定。
 *
 * 必需部分是 key 与 value：key 触发 value——事件可以作为触发回调的 key，
 * 一个类的实例化也可以作为引出另一个类实例化的 key。
 * 可选部分是 name：当需要用相同的 key 建立两个绑定时，name 充当区分符。
 *
 * 注意 SemiBinding 内部维护的是列表，RemoveKey / RemoveValue / RemoveName
 * 都是从这些列表中删除条目。
 */

namespace SimplifyIoC.Framework
{
    public interface IBinding
    {
        /**
		 * Tie this binding to a Type key
		 */
        IBinding Bind<T>();
        /**
		 * Tie this binding to a value key, such as a string or class instance
		 */
        IBinding Bind(object key);

        /**
		 * Set the Binding's value to a Type
		 **/
        IBinding To<T>();
        /**
		 * Set the Binding's value to a value, such as a string or class instance
		 */
        IBinding To(object o);

        /**
		 * Qualify a binding using a marker type
		 */
        IBinding ToName<T>();

        /**
		 * Qualify a binding using a value, such as a string or class instance
		 */
        IBinding ToName(object o);

        /**
		 * Retrieve a binding if the supplied name matches, by Type
		 */
        IBinding Named<T>();

        /**
		 * Retrieve a binding if the supplied name matches, by value
		 */
        IBinding Named(object o);

        /**
		 * Remove a specific key from the binding.
		 */
        void RemoveKey(object o);

        /**
		 * Remove a specific value from the binding
		 */
        void RemoveValue(object o);

        /**
		 * Remove a name from the binding
		 */
        void RemoveName(object o);

        /// Get the binding''s key
        object key { get; }

        /// Get the binding's name
        object name { get; }

        /// Get the binding's value
        object value { get; }

        /// Get or set a MANY or ONE constraint on the Key
        BindingConstraintType keyConstraint { get; set; }

        /// Get or set a MANY or ONE constraint on the Value
        BindingConstraintType valueConstraint { get; set; }

        //Mark a binding as weak, so that any new binding will override it
        IBinding Weak();

        //Get whether or not the binding is weak, default false
        bool isWeak { get; }

    }
}